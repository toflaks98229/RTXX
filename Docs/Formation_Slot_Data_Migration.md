# 진형 슬롯 데이터화 설계안

> 작성 근거: 9,600명 규모 실측. 모든 수치는 배치모드 에디터 실행 기준입니다.

---

## 1. 왜 이 작업이 필요한가

유닛 후처리(`Unit._Update()`)를 Burst Job으로 옮기려는 시도가 **세 번 모두 실패**했습니다.

| 방식 | Apply | 합계 |
|---|---|---|
| **기준선 (메인 스레드)** | **4.66** | **15.10 ms** |
| ① 부대별 Job + 즉시 Complete | 7.22 | 18.46 ms |
| ② 전 부대 배치 후 1회 Complete | 7.26 | 18.95 ms |
| ③ 슬롯 Transform을 필요분만 읽기 | 5.66 | 17.28 ms |

Job 자체는 제 몫을 했습니다. `U_Fight`가 **2.22 → 0.12 ms**로 줄었습니다.

문제는 **입력 준비**입니다. Job에 넘길 진형 슬롯 위치를 메인 스레드가 모아야 하는데,
슬롯이 `Transform`으로 존재하므로 그 수집이 곧 네이티브 왕복입니다.
Job으로 아낀 것을 입력 수집이 도로 까먹습니다.

②번이 ①번과 거의 같았다는 점이 결정적입니다. `Complete()` 왕복이 원인이 아니었다는 뜻이고,
따라서 **"더 크게 묶으면 이긴다"는 방향 자체가 현 구조에서 성립하지 않습니다.**

### 전환 시 기대 이익 (실측)

```
Transform 읽기 : 2.955 ms  <- 매 틱 발생
배열 읽기      : 0.029 ms  <- 전환 후
절감 추정      : 2.926 ms / 틱   (약 102배)
```

이 값이 열리면 U_Fight의 Job 전환이 비로소 이득이 됩니다.
현재 합계 15.10 ms 기준으로 **12 ms대 진입**이 목표치입니다.

---

## 2. 현재 구조

### 2-1. 슬롯의 정체

```csharp
// Army.Spawn_Units()
GameObject formation_Move = new GameObject("Formation_Move");
formation_Move.transform.SetParent(formation_Move_Transform, false);  // ★ 부모 = 부대 기준점
formation_Move.transform.position = slot;
formation_Moves.Add(formation_Move.transform);
```

**슬롯이 Transform인 유일한 이유는 이 부모 관계입니다.**
부대 기준점이 움직이면 Unity의 트랜스폼 계층이 슬롯 전부를 자동으로 따라 옮깁니다.
즉 코드가 매 틱 슬롯을 갱신하지 않아도 되는 대신, 읽을 때마다 네이티브 왕복을 냅니다.

9,600명이면 빈 GameObject가 9,600개 존재한다는 뜻이기도 합니다.

### 2-2. 슬롯의 생명주기

| 시점 | 동작 | 빈도 |
|---|---|---|
| 생성 | `Spawn_Units`에서 GameObject 생성 | 1회 |
| 갱신 | `Set_Formation_Move`가 월드 좌표로 대입 | **명령 시에만** |
| 이동 | 부모(부대 기준점)를 따라 자동 이동 | 매 틱 (Unity 내부) |
| 소비 | `Unit.Move()`가 `targetMoveTo.position` 읽기 | **매 틱** |
| 소비 | `Get_Formation_Move_Positions()`가 전량 읽기 | 재정비 시 |

**핵심: 갱신은 드물고 소비는 매 틱입니다.** 읽기가 비싼 현재 구조는 정확히 반대로 최적화되어 있습니다.

### 2-3. 의존 지점 (전수 조사 결과)

`formation_Moves` — 6개 실사용 지점

| 위치 | 용도 |
|---|---|
| `Army.cs:114` | 필드 선언 |
| `Army.cs:1038` | 생성 |
| `Army_Formation.cs:233,237` | 위치 대입 |
| `Army_Move.cs:336,377` | `Move_Start(formation_Moves[matchX[i]])` |
| `Army_Move.cs:857,862` | 전량 읽어 매칭에 사용 |

`targetMoveTo` — 3개 실사용 지점

| 위치 | 용도 |
|---|---|
| `Unit.cs:36` | 필드 선언 |
| `Unit_Move.cs:719-722` | `Move_Start(Transform)` |
| `Unit_Move.cs:850,857` | **매 틱 읽기 (제거 대상)** |

**총 9개 지점.** 진형 시스템 전체를 건드릴 것으로 우려했으나, 실제 표면은 좁습니다.

---

## 3. 설계

### 3-1. 자료 구조

슬롯을 부대가 소유하는 배열로 바꿉니다.

```csharp
partial class Army
{
    /// <summary>
    /// 진형 슬롯의 '부대 기준점 기준' 지역 좌표입니다.
    ///
    /// 왜 지역 좌표인가:
    /// 예전에는 슬롯이 기준점의 자식 Transform이라, 기준점이 움직이면
    /// Unity가 알아서 따라 옮겼습니다. 배열로 바꾸면 그 자동 이동이
    /// 사라지므로, 지역 좌표로 저장했다가 읽을 때 기준점 기준으로
    /// 변환해야 같은 동작이 됩니다.
    ///
    /// 월드 좌표로 저장하면 기준점이 움직일 때마다 전량을 다시 써야 하고,
    /// 그건 지금 없애려는 바로 그 비용입니다.
    /// </summary>
    private Vector3[] slotLocalPositions;

    /// <summary>슬롯의 현재 월드 좌표입니다. 틱당 한 번만 계산합니다.</summary>
    private NativeArray<Vector3> slotWorldPositions;

    /// <summary>월드 좌표 캐시가 이번 틱 기준으로 유효한지 여부입니다.</summary>
    private bool bslotWorldValid;
}
```

### 3-2. 변환 규칙

```csharp
world = pivot.position + (pivot.rotation * local)
```

기존 `SetParent(..., false)` + `transform.position = slot`과 정확히 같은 계산입니다.
(스케일은 1이므로 무시)

### 3-3. 갱신 시점

```
명령 시 (Set_Formation_Move)
  → 월드 좌표 계산 후 지역 좌표로 변환해 slotLocalPositions에 저장

틱 시작 (_Update_Begin)
  → bslotWorldValid = false 로만 표시 (계산은 미룸)

첫 소비 시
  → slotWorldPositions를 한 번 계산하고 bslotWorldValid = true
```

**지연 계산이 핵심입니다.** 이동 중이 아닌 부대는 슬롯을 아무도 읽지 않으므로
계산 자체가 일어나지 않습니다.

### 3-4. 유닛이 슬롯을 참조하는 방법

`Unit.targetMoveTo`(Transform)를 **슬롯 인덱스**로 대체합니다.

```csharp
// 이전
public Transform targetMoveTo;

// 이후
/// <summary>배정받은 진형 슬롯의 인덱스입니다. -1이면 배정 없음입니다.</summary>
public int targetSlotIndex = -1;
```

이렇게 하면 `Unit_Post_Update_Job`이 슬롯 배열을 통째로 받고,
유닛은 자기 인덱스로 조회합니다. **메인 스레드의 수집 루프가 통째로 사라집니다.**

---

## 4. 단계별 전환 경로

각 단계는 **독립적으로 컴파일되고 검증 가능**해야 합니다.

### 1단계 — 배열 병행 도입 (되돌리기 쉬움)

- `slotLocalPositions` 배열을 추가하고, `Set_Formation_Move`가 Transform과 **양쪽 모두** 갱신
- 읽기는 아직 Transform 사용
- **검증**: 두 값이 항상 일치하는지 프로브로 대조 (불일치 시 즉시 경고)

> 위험: 없음. 기존 동작이 그대로이며 배열은 검증용으로만 존재합니다.

### 2단계 — 읽기를 배열로 전환

- `Get_Formation_Move_Positions()`가 배열을 읽도록 변경
- `Unit.Move()`의 `targetMoveTo.position`을 슬롯 배열 조회로 변경
- `Unit.targetMoveTo` → `targetSlotIndex`
- **검증**: 9,600명 소크 테스트에서 좌표·인덱스 이상 0건, 전투 진행 정상

> 위험: **중간.** 슬롯 인덱스와 유닛의 대응이 어긋나면 유닛이 엉뚱한 자리로 갑니다.
> `Match_Units_To_Slots`의 결과를 그대로 인덱스로 쓰므로 매칭 로직은 건드리지 않습니다.

### 3단계 — Transform 슬롯 제거

- `formation_Moves` 필드와 GameObject 생성 코드 삭제
- **부수 효과**: 9,600개 GameObject가 사라져 메모리와 씬 로드 시간이 함께 줄어듭니다

> 위험: 낮음. 2단계에서 모든 읽기가 이미 배열로 옮겨진 뒤입니다.

### 4단계 — 유닛 후처리 Job 전환 (본래 목표)

- `Unit_Post_Update_Job`에 슬롯 배열을 넘김 (수집 루프 없음)
- 전 부대의 Job을 걸어 두고 마지막에 한 번만 `Complete()`
- **검증**: Apply 단계가 기준선(4.66ms)보다 낮아지는지 확인

> 위험: 낮음. 앞서 세 번 시도해 실패 원인을 이미 알고 있습니다.
> 이번에는 그 원인(입력 수집)이 제거된 상태입니다.

---

## 5. 위험과 대응

| 위험 | 내용 | 대응 |
|---|---|---|
| **슬롯-유닛 대응 붕괴** | 인덱스가 어긋나면 유닛이 엉뚱한 자리로 이동 | 1단계에서 Transform과 배열을 대조 검증. 에디터 전용 어서션 추가 |
| **전사 시 인덱스 밀림** | `formation_Moves`는 생성 시점 인원, 유닛은 생존 인원 | 기존 코드도 `Mathf.Min`으로 방어 중. 같은 규약 유지 |
| **회전 반영 누락** | 지역→월드 변환에서 `pivot.rotation`을 빼먹으면 진형이 돌지 않음 | 1단계 대조 검증이 즉시 잡아냄 |
| **패주/산개 경로** | 슬롯을 버리고 움직이는 경로들 | 이 경로들은 슬롯을 읽지 않으므로 영향 없음. 소크 테스트의 `-forceRout`으로 확인 |
| **결정론 영향** | 부동소수점 연산 순서 변화 | 변환식이 결정적이므로 영향 없음. 다만 소크 테스트로 확인 |

---

## 6. 검증 계획

각 단계마다 **동일한 절차**를 반복합니다.

```bash
# 1. 컴파일
dotnet build Assembly-CSharp.csproj

# 2. 씬 재생성
Unity.exe -batchmode -nographics -quit -executeMethod Mass_Battle_Builder.Build_From_CLI

# 3. 정확성 + 성능
Unity.exe -batchmode -executeMethod Mass_Battle_Runner.Run_From_CLI \
          -runTicks 700 -seed 12345 -profile

# 4. 패주 경로 확인
Unity.exe -batchmode -executeMethod Mass_Battle_Runner.Run_From_CLI \
          -runTicks 700 -seed 12345 -forceRout
```

**통과 기준**

- 좌표/인덱스 이상 0건
- 지형 밀착 이탈 0%
- 사망자 500명 이상 (전투가 실제로 진행됨)
- 교전 부대 30개 이상
- Apply 단계가 이전 단계보다 나빠지지 않음

---

## 7. 예상 효과와 한계

### 기대

| 항목 | 현재 | 전환 후 (추정) |
|---|---|---|
| 슬롯 읽기 | 2.955 ms | 0.029 ms |
| U_Fight | 2.12 ms | 0.12 ms |
| **합계** | **15.10 ms** | **12 ms대** |
| GameObject 수 | 9,600개 추가 | 0개 |

### 한계 — 솔직히 밝힙니다

1. **12ms대는 추정입니다.** 슬롯 읽기 절감분이 그대로 합계에 반영된다는 보장은 없습니다.
   앞선 세 번의 시도가 모두 예상과 다르게 나왔으므로, 이 수치도 실측 전까지는 가설입니다.

2. **에디터 배치모드 기준입니다.** IL2CPP 빌드에서는 Transform 접근 비용이 다를 수 있습니다.

3. **이 작업은 성능만을 위한 것입니다.** 게임 기능은 하나도 늘지 않습니다.
   지금 15.10ms(60fps 예산의 91%)로도 목표 규모가 돌아가므로, **급하지 않습니다.**
   기능 개발이 우선이라면 미뤄도 무방합니다.

4. **9,600명이 목표 규모가 아니라면 재검토가 필요합니다.**
   5,000명 규모에서는 현재 구조로도 충분히 여유가 있습니다.

---

## 8. 착수 판단 기준

다음 중 하나라도 해당하면 착수를 권합니다.

- 목표 규모를 **15,000명 이상**으로 올리려 할 때
- 씬 로드 시간이나 메모리가 문제가 될 때 (GameObject 9,600개 제거 효과)
- 프레임 타임을 **12ms 이하**로 확보해야 하는 다른 요구(렌더링 등)가 생겼을 때

반대로, 현재 규모와 프레임에 만족한다면 **이 문서를 남겨 두고 기능 개발로 넘어가는 편이 낫습니다.**
