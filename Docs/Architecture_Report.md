# RTXX 심층 기술 분석 및 로드맵 보고서

> 대상: `Assets/_Project/02.Scripts/` 전체 (84개 파일 / 26,745줄)
> 작성: Unity 테크니컬 디렉터 관점
> 근거: 코드 전수 계측 + 9,600 유닛 600틱 실측

---

## 0. 규모 실측

| 폴더 | 파일 | 줄 수 | 성격 |
|---|---|---|---|
| Character | 20 | 9,691 | 부대·유닛 시뮬레이션 (핵심) |
| Editor | 24 | 4,637 | 검증 도구·임포터 |
| UI | 10 | 3,569 | IMGUI HUD |
| Gameplay | 8 | 3,035 | 전투 진행·카메라·계측 |
| Core | 8 | 2,598 | 틱 파이프라인·입력 |
| Framework | 11 | 2,429 | 격자·헝가리안·프로파일러 |
| Data | 3 | 786 | 밸런스 값 |

런타임 코드는 약 22,100줄입니다. **Editor가 전체의 17%(4,637줄)** 를
차지하는데, 이것은 낭비가 아니라 이 프로젝트의 강점입니다(3-3 참조).

---

## 1. 코드 아키텍처 및 품질 분석

### 1-1. 핵심 역할 요약

| 클래스 | 한 문장 정의 |
|---|---|
| `Controller` | 틱 파이프라인의 지휘자. 전 부대의 Prepare→Schedule→Complete→Apply를 단계별로 일괄 구동한다 |
| `Army` | 부대 단위 상태와 진형의 주인. 유닛 목록·슬롯·사기·태세를 소유한다 |
| `Army_Data` | Burst에서 읽히는 부대 수치 전부를 담는 blittable 구조체 |
| `Unit` | 병사 한 명의 GameObject 껍데기. 실제 상태는 `Unit_Data`에 있다 |
| `Unit_Job` / `Unit_Fight_Job` | 이동·전투 판정을 병렬로 계산하는 Burst Job |
| `Spatial_Grid` / `Collision_*` | 이웃 탐색과 겹침 해소를 O(n)에 가깝게 만드는 공간 분할 |
| `Hungarian` | 유닛↔슬롯 최적 배정 (O(n³), 64명 이상은 계층 분할) |
| `Balance_Data` | 122개 밸런스 수치를 담는 SharedStatic 구조체 |
| `Battle_Manager` | 배치→교전→종료 단계와 승패 판정 |
| `UI_Command_Bar` 외 | IMGUI HUD 일체 |
| `Tick_Profiler` | 30개 단계별 시간 계측 |

### 1-2. 디자인 패턴 분석

**적절하게 쓰인 것**

| 패턴 | 적용 | 평가 |
|---|---|---|
| **Data-Oriented Design** | `Army_Data`/`Unit_Data` 구조체 + 14개 Job | 이 프로젝트의 근간. 9,600 유닛이 도는 유일한 이유 |
| **Staged Pipeline** | Prepare→Schedule→Complete→Apply | **가장 잘 된 설계.** 전 부대가 Schedule을 끝낸 뒤 한 번만 대기하므로 Job 병렬성이 살아남 |
| **Observer** | `GameEvents` (29곳) | 사망 알림 등 단방향 통지에 적절 |
| **Deferred Commit** | `pendingMoraleShock` → `Commit_Pending_Morale_Shock` | 부대 갱신 순서가 결과를 바꾸지 않게 하는 결정론 장치. 수준 높음 |
| **Partial Class 분할** | Army 7파일, Unit 3파일, Controller 4파일 | 거대 클래스를 책임별로 자른 현실적 선택 |

**의심스러운 것**

- **`Balance_Data`의 SharedStatic** — 옳은 선택이지만 필드가 **122개**입니다.
  단일 구조체가 전투·지형·사기·충돌·마커까지 전부 담고 있어, 사실상
  전역 설정 덩어리(God Config)로 자라고 있습니다.
- **`Battle_Manager.bdeploying` 정적 속성** — 제가 이번에 추가했습니다.
  `Army`가 전투 진행 계층을 참조하지 않는다는 원칙을 지키려는 우회였지만,
  **전역 가변 상태를 하나 늘린 것**이 사실입니다. 부채로 기록해 둡니다.

### 1-3. 코드 스멜

**(1) 거대 클래스 — Army**

```
Army 계열 partial 합계 : 5,470줄 (7파일)
  그중 Army.cs 단독    : 1,785줄
  public 멤버          : 59개
```

partial로 잘라 두었지만 **하나의 타입이 5,470줄**입니다. 진형·이동·전투·
사기·돌격·인지를 모두 소유합니다. 59개의 public 멤버는 "무엇이 외부
계약인지" 판단을 사실상 포기한 상태입니다.

**(2) 좌표 규약 불일치 (이번 세션에 실제 버그를 냈음)**

`Formation_Data.position`의 주석은 '진형의 중심 위치'인데 저장되는 값은
**첫 슬롯 좌표**입니다. 반면 `Set_Formation_Internal`의 입력 `position`은
중심처럼 다뤄집니다.

```csharp
formation_Start = position + add_width * num_width * 0.5f;   // 입력: 중심
return new Formation_Data(..., vector3s[0], vector3s);        // 출력: 끝점
```

**입력·내부·저장의 뜻이 전부 다릅니다.** 실제로 태세를 바꿀 때마다 대열이
절반 폭씩 밀려나는 버그로 나타났습니다. 지금은 기준점(`formation_Move_Transform`)
경유 경로로 우회했지만 **규약 자체는 그대로 남아 있습니다.**

**(3) 오타가 API에 고착됨**

```csharp
_Upadate_Data();        // Update 오타
AddAccelerationd(float) // 끝에 d
Move_Reformation / breformation / e_Army_Formation  // 혼재
```

`_Upadate_Data`는 호출부까지 퍼져 있어 이름을 고치려면 여러 파일을
건드려야 합니다. 사소해 보이지만 검색·자동완성을 방해합니다.

**(4) `FindAnyObjectByType` 14곳 (런타임)**

씬 배선 대신 런타임 탐색으로 참조를 얻습니다. `Unit_Fight`에서는 이것이
실제 성능 사고를 냈고(캐시 추가로 해결), 나머지는 대부분 `Start()`
1회라 당장 문제는 아니지만 **배선이 코드에 숨는** 구조입니다.

**(5) 죽은 파라미터**

`Set_Formation(Formation_Data)`처럼 인자를 받지만 실제로는 자기 필드를
다시 읽는 오버로드가 있습니다. 호출부가 "내가 넘긴 값이 쓰인다"고
착각하기 쉽습니다.

---

## 2. 알고리즘 연결성 및 데이터 흐름

### 2-1. 상호작용 지도

```
                        [입력]
  Controller.Update ──┬─ _Update_MouseButton_Select ──> armies_Selected
                      ├─ _Update_MouseButton_Command ─> Army.Move_Start
                      └─ _Update_Stance_Command ──────> Army.Set_Stance
                             ↑
                    UI_Input_Guard (HUD 위면 차단)

                        [시뮬레이션 — FixedUpdate]
  Controller.FixedUpdate
    └─ _Update_Army()
         ├─ Snapshot        : Unit → unitDataMap
         ├─ ArmyJob         : Army_Job (사기/피로 병렬)
         ├─ Prepare         : Army._Update_Prepare  ← 47%, 메인 스레드
         ├─ Schedule        : 전 부대 Job 예약 (대기 없음)
         ├─ Complete        : 여기서 한 번만 대기
         ├─ Apply           : 결과를 Unit/Army에 반영
         ├─ Collision       : 격자 구축 + 겹침 해소
         ├─ GroundSync      : 지면 높이 맞춤
         └─ TransformWrite  : TransformAccessArray 일괄 쓰기

                        [표시]
  UI_* (OnGUI) ──읽기 전용──> Army/Army_Data
  Battle_Manager ──정적──> bdeploying ──> Army.Set_Stance 분기
```

**핵심 관찰:** 입력→시뮬레이션은 **명령 호출**로, 시뮬레이션→표시는
**읽기 전용 조회**로 흐릅니다. UI가 시뮬레이션을 직접 바꾸지 않는 것은
잘 지켜지고 있습니다.

### 2-2. 로직 흐름 — 이동 명령 한 번의 추적

```
1. Controller_Selection      드래그로 armies_Selected 구성
2. Controller_Formation      드래그 궤적 → 전열축/길이
3. Army.Move_Start           → Move_Start_Internal (모든 이동의 단일 관문)
4.   Set_Formation           슬롯 월드 좌표 계산 (Formation_Job)
5.   Set_Army_Move_Position  기준점 이동 (GetPosition 사용 ← 캐시 의존)
6.   Set_Formation_Move      슬롯을 기준점 로컬로 저장
7.   Match_Units_To_Slots    헝가리안 배정
8.   Unit.Move_Start         유닛별 목표 지정
9.   [배치 단계면] Snap_Units_To_Slots → 즉시 이동 + 캐시 무효화
10.  Update_Markers          UI 마커 갱신
```

**이 체인의 취약점은 5번입니다.** `GetPosition()`은 틱당 1회 캐시되는데,
배치 단계에서는 틱이 멈춰 캐시를 갱신할 주체가 없습니다. 실제로 "드래그로
옮긴 뒤 태세를 바꾸면 원래 자리로 돌아가는" 버그가 여기서 나왔습니다.
지금은 9번에서 명시적으로 무효화해 막고 있습니다.

### 2-3. 결합도 진단

| 지표 | 수치 | 판정 |
|---|---|---|
| `public static` | 94 | **높음** |
| `FindAnyObjectByType` (런타임) | 14 | 중간 |
| `GameEvents` 사용 | 29 | 양호 (단방향) |
| partial class | 14파일 / 3타입 | 거대 타입의 신호 |

**진단: 중간 결합. 다만 위험이 한쪽에 몰려 있습니다.**

- **낮은 결합 (좋음)** — UI→시뮬레이션은 읽기 전용, Job은 순수 데이터.
  UI를 통째로 들어내도 시뮬레이션은 돕니다.
- **높은 결합 (위험)** — `Army`가 사실상 모든 것을 안다. 진형을 고치면
  이동·재정비·마커·태세가 함께 흔들립니다. **이번 세션에서 태세 밀집도
  하나를 넣는 데 진형·재정비·배치·UI·충돌 5개 영역을 연달아 고쳐야
  했던 것이 그 증거입니다.**
- **암묵적 결합 (가장 위험)** — `GetPosition()` 캐시, `Formation_Data`
  좌표 규약, `bdeploying` 정적 상태. 컴파일러가 잡아 주지 않고 주석에만
  적혀 있어, 모르는 사람이 만지면 조용히 깨집니다.

---

## 3. 향후 기능 제언

### 3-1. 기술적 확장 — 지금 구조에서 싼 것

| 기능 | 근거 | 난이도 |
|---|---|---|
| **태세 추가 (쐐기/원형)** | `E_Army_Stance` + `density_*` + `Formation_Job`만 손대면 됨. 밀집도 축이 이미 있음 | 하 |
| **탄약 표시** | `Unit_Data.ammo`가 이미 소비되는데 **화면에 안 나옴**. 카드에 막대 한 줄 | 하 |
| **부대 상태 아이콘** | `E_Unit_Move`/`E_Unit_Fight`/`E_Army_Morale` 열거형 존재. DCSS 아이콘 5,231장 확보됨 | 하 |
| **일시정지/배속** | `Time.timeScale` 사용처가 **0곳**. 새로 만들되 구조는 단순 | 중 |
| **병종 추가 (공성/전차)** | `E_Unit_Class` + `UnitStatSO` 확장. 다만 `Balance_Data`가 더 비대해짐 | 중 |

### 3-2. 게임 디자인 제언

이 게임의 정체성은 **"측후방이 방어를 무력화한다"** 입니다. 그 축을
살리는 것이 우선입니다.

1. **지휘 체계(Chain of Command)** — 장군 오라(`general_Aura_*`)가 이미
   있습니다. 여기에 '전령' 개념을 얹어 **명령이 즉시 전달되지 않게**
   하면, 우회 기동의 가치가 배가됩니다. 토탈워에 없는 차별점이 됩니다.
2. **피로 기반 추격 실패** — `GetFatigueRate()`가 이미 속도를 깎습니다.
   패주 추격이 피로 때문에 실패하는 장면을 명시적으로 보여 주면
   "무리한 추격"이라는 판단 지점이 생깁니다.
3. **전장 기억(After-Action)** — `Battle_Report_Entry`가 이미 킬/손실을
   집계합니다. 캠페인(`Campaign_Manager`)과 이어 **부대별 무용담**을
   누적하면 애착이 생깁니다. 데이터는 이미 다 있습니다.

### 3-3. 최적화 제언 — 실측 기반

**9,600 유닛 600틱 실측 (이번 세션):**

| 단계 | 평균(ms) | 비중 |
|---|---|---|
| **Prepare** | **9.02** | **47.0%** |
| Apply | 3.02 | 15.7% |
| TransformWrite | 2.53 | 13.2% |
| Snapshot | 1.44 | 7.5% |
| Complete | 1.27 | 6.6% |
| Collision | 0.58 | 3.0% |

**1순위: Prepare의 정체를 밝힐 것 (47%)**

`Prepare`는 메인 스레드 단일 루프이고, 그 안은 다음과 같습니다.

```csharp
_Upadate_Data();     // 부대 데이터 + 평균 위치 캐시
_Update_Terrain();   // 고지/경사
_Update_Detection(); // 사거리 안의 적 탐지
_Update_Move();      // 이동 상태
_Update_Flag();      // 깃발
```

그런데 **하위 계측이 P_SetDestination(0.00) + P_AgentMove(0.15)뿐**입니다.
**9.02ms 중 8.87ms가 어디로 가는지 측정되지 않고 있습니다.**

`_Update_Detection`이 유력한 후보입니다(부대마다 적 탐색). 하지만
**추측하지 말고 하위 단계를 먼저 심으십시오.** 이번 세션에서 "Hungarian이
스파이크의 원인"이라는 가설이 계측으로 반증되고 실제 범인이
`FindAnyObjectByType`였던 전례가 있습니다.

**2순위: Prepare의 병렬화**
정체가 밝혀지면 대부분 Job으로 옮길 수 있을 것으로 봅니다. 지형·탐지는
읽기 전용 조회라 병렬화에 적합합니다. 성공하면 **틱의 40%가 사라집니다.**

**3순위: 하지 말 것**

- **충돌 최적화** — 3.0%입니다. 여기를 만지는 것은 시간 낭비입니다.
- **유닛 후처리 Job 전환** — 이미 시도했고 **실패했습니다**
  (Apply 2.59→3.51ms). `Unit_Job.cs`에 기록되어 있습니다. 다시 시도하지
  마십시오.
- **SoA 전환** — 이미 계측으로 반증되었습니다.

**Editor 도구 17%에 대하여:** 검증 프로브가 24개 있습니다. 이것은
군살이 아니라 **이 프로젝트가 추측 대신 계측으로 판단할 수 있는 이유**
입니다. 유지하십시오.

---

## 4. 리팩토링 로드맵

### 🔴 1순위 — `deaths = 0` 원인 규명

```
ticks=601  alive=9600  deaths=0  charges=4~10
```

**부대가 만나 돌격까지 하는데 아무도 죽지 않습니다.**
`collide_Enemy_Radius_Rate`를 1.0으로 되돌려도 동일하므로 이번 변경과
무관한 기존 문제입니다.

이것은 리팩토링 이전에 **게임이 성립하는가**의 문제입니다. 전투 판정이
돌지 않는다면 지금까지의 모든 밸런스 수치가 검증된 적 없다는 뜻입니다.
다른 무엇보다 먼저 해결하십시오.

### 🟠 2순위 — 진형 좌표 규약 통일

입력·내부·저장의 의미가 다른 현재 규약은 **이미 두 번 버그를 냈습니다.**

```
목표: 진형의 기준은 언제나 '부대 중심'이다. 예외 없음.
  - Formation_Data.position 을 실제 중심으로 저장하도록 수정
  - Set_Formation 계열을 중심 기준 하나로 통일
  - Move_Reformation_Line 의 center - halfLength 보정 제거
```

지금은 기준점 경유로 우회했을 뿐이라, 다음 사람이 `Set_Formation`을
직접 부르면 같은 버그가 재발합니다.

### 🟡 3순위 — Army 책임 분리

5,470줄 / public 59개를 줄여야 합니다. **한 번에 하지 말고** 가장 응집도
높은 덩어리부터 떼십시오.

```
1) Army_Formation → Formation_Controller 로 분리 (슬롯의 유일한 주인)
2) Army_Morale    → 이미 독립적. 클래스로 승격 용이
3) Army_Perception→ 탐지는 읽기 전용이라 분리 후 Job화 가능 (3-3의 2순위와 연결)
```

3번은 성능 작업과 겹치므로 **함께 하면 이득이 두 배**입니다.

### 참고 — 우선순위에서 뺀 것

- **`public static` 94개 정리** — 양은 많지만 대부분 `Constant` 프로퍼티
  (Burst 접근용)라 정당합니다. 실제 전역 가변 상태는 소수입니다.
- **IMGUI → UGUI 전환** — 부대 수십 개 규모에서 IMGUI로 충분합니다.
  배선 비용만 큽니다.
- **오타 수정(`_Upadate_Data`)** — 가치는 있으나 위 셋보다 뒤입니다.
  2순위 작업 중에 같은 파일을 열 때 함께 처리하십시오.

---

## 5. 총평

**강점**

이 프로젝트의 **틱 파이프라인 설계와 계측 문화는 상용 수준**입니다.
전 부대가 Job을 예약한 뒤 한 번만 대기하는 구조, 결정론을 위한 지연
커밋, 30단계 프로파일러, 24개 검증 도구 — 이것들은 9,600 유닛이 도는
직접적인 이유이며 쉽게 얻어지지 않습니다.

특히 **가설을 계측으로 반증한 기록**(SoA 무효, Job 전환 실패, Hungarian
무죄)이 코드 주석에 남아 있는 것은 드문 미덕입니다.

**약점**

`Army`가 너무 많이 압니다. 그 결과 작은 기능 하나가 5개 영역을 건드리게
되고, 규약이 주석에만 존재해 컴파일러가 지켜 주지 못합니다.

**그러나 가장 시급한 것은 구조가 아닙니다.** 전투에서 사상자가 나오지
않는 상태로는 어떤 리팩토링도 검증할 수 없습니다. 1순위를 먼저
해결하십시오.
