# RTXX 심층 기술 분석 및 로드맵 보고서 (v2)

> 대상: `Assets/_Project/02.Scripts/` 전수 (84파일 / 26,745줄)
> 기준: 현재 **작업 트리** (마지막 커밋 `bd64c45`보다 앞섬 — 진형 슬롯 데이터화가 이미 구현되어 있음)
> 관점: Unity 테크니컬 디렉터 / 시스템 아키텍트
> 근거: 코드 전수 독해 + 코드 주석·커밋에 기록된 실측치

## 0. v1 보고서와 달라진 점

`Docs/Architecture_Report.md`(이전 세션)는 두 지점에서 이미 낡았습니다.

| v1의 진단 | 현재 상태 |
|---|---|
| 🔴 1순위 "deaths=0, 전투 판정이 돌지 않음" | **해결됨.** 커밋 `ccca11f` 검증에서 사망 554명 |
| "진형 슬롯 데이터화는 착수 판단 대상" | **완료됨.** `slotLocalPositions` 배열 + `targetSlotIndex`로 전환, GameObject 9,600개 제거 |
| "Prepare가 틱의 47%" | 그 이후 다섯 번의 최적화가 들어가 **재측정 필요** |

따라서 v1의 우선순위는 그대로 쓸 수 없습니다. 이 문서가 대체합니다.

### 규모

| 폴더 | 파일 | 줄 | 성격 |
|---|---|---|---|
| Character | 20 | 9,691 | 부대·유닛 시뮬레이션 (핵심) |
| Editor | 24 | 4,637 | 검증 프로브·임포터 |
| UI | 10 | 3,569 | IMGUI HUD |
| Gameplay | 8 | 3,035 | 전투 진행·캠페인·카메라 |
| Core | 8 | 2,598 | 틱 파이프라인·입력 |
| Framework | 11 | 2,429 | 격자·헝가리안·프로파일러 |
| Data | 3 | 786 | 밸런스 수치 |

Burst Job 14개, `GameEvents` 사용 29곳, `Balance_Data` 필드 122개.

---

## 1. 코드 아키텍처 및 품질 분석

### 1-1. 핵심 역할 요약

| 타입 | 한 문장 정의 |
|---|---|
| `Controller` | 틱 파이프라인의 지휘자. 전 부대의 Prepare→Schedule→Complete→Apply를 **단계별로 가로질러** 구동한다 |
| `Army_Registry` | `armyIndex` 색인표와 유닛의 `armyIndex`를 **한 루프에서 함께** 정해 어긋남을 구조적으로 차단한다 |
| `Simulation_Clock` / `Simulation_Random` | 난수를 상태가 아니라 `f(틱, 부대)`로 만들어 호출 순서 의존성을 없앤다 |
| `Army` | 부대의 상태·진형 슬롯·유닛 목록의 소유자. 7개 partial로 나뉜 5,547줄 |
| `Army_Data` | Burst가 읽는 부대 수치 전부를 담는 blittable 구조체 (336B) |
| `Unit` | 병사의 GameObject 껍데기. 상태는 전부 `Unit_Data`(280B)에 있다 |
| `Unit_Job` / `Unit_Fight_Job` | 이동·표적 선정·피해 판정을 유닛 단위로 병렬 계산 |
| `Unit_Collision` 계열 | PhysX를 대체한 격자 기반 겹침 해소 (질량비 반영) |
| `Unit_Transform_Sync` | `TransformAccessArray`로 Transform 읽기/쓰기를 일괄 처리 |
| `Spatial_Grid`(유닛) / `Army_Grid`(부대) | 두 층위의 공간 분할 |
| `Formation_Matcher` / `Hungarian` | 유닛↔슬롯 배정. 64명 초과 시 분대 단위 계층 매칭 |
| `Balance` (SharedStatic) | Burst 코드와 관리 코드가 공유하는 밸런스 저장소 |
| `Battle_Manager` / `Battle_AI` / `Campaign_Manager` | 전투 단계·적 지휘·캠페인 영속성 |
| `Tick_Profiler` | 30단계 계측기. 이 프로젝트의 판단 근거 |

### 1-2. 디자인 패턴 분석

**적절하게 쓰인 것**

| 패턴 | 적용 지점 | 평가 |
|---|---|---|
| **Data-Oriented Design** | `Army_Data`/`Unit_Data` + Burst Job 14개 | 프로젝트의 근간. 9,600명이 도는 유일한 이유 |
| **Staged Pipeline** | `_Update_Prepare/Schedule/Complete/Apply` | **가장 잘 된 설계.** 전 부대가 Schedule을 마친 뒤 한 번만 대기하므로 부대 간 Job이 겹쳐 실행됨 |
| **Deferred Commit** | `pendingMoraleShock` → `Commit_Pending_Morale_Shock` | 부대 갱신 순서가 결과를 바꾸지 못하게 하는 결정론 장치. 수준 높음 |
| **Coordinate-derived RNG** | `Simulation_Random.Seed_For(tick, armyIndex)` | 난수를 전역 상태에서 떼어낸 정석 해법 |
| **Observer** | `GameEvents` 10개 이벤트 / 29곳 | 단방향 통지에 적절. `try/catch` 보호와 `ClearAll()`까지 갖춤 |
| **Invariant-by-construction** | `Army_Registry.Rebuild_Indices()` | "두 값이 어긋날 수 있는 경로 자체를 없앤다"는 접근. 주석이 아니라 구조로 강제 |
| **Split-buffer for Job 병렬성** | `Unit_Pose`(28B) 추출 후 애니메이션 분기 | Job 안전 시스템의 직렬화를 푸는 정확한 처방. 31.8→15.3ms |

**의심스러운 것**

- **`Balance_Data`의 122개 필드** — SharedStatic 선택 자체는 옳습니다(Burst가 일반 static을 못 읽음). 그러나 전투·지형·사기·충돌·마커·시각효과가 한 구조체에 들어 있어 사실상 God Config입니다. 필드 하나를 추가하면 336B 구조체가 전 Job에 복사됩니다.
- **`Battle_Manager.bdeploying` 정적 사본** — `Army`가 전투 진행 계층을 모르게 하려는 우회지만, 결과적으로 전역 가변 상태를 하나 늘렸습니다. 주석에 부채로 명시되어 있는 점은 좋습니다.
- **`Formation_Data`의 이중 정체** — 진형 좌표 규약이 입력·내부·저장에서 각각 다른 뜻을 가집니다(1-3 참조). 패턴이라기보다 미완성 추상화입니다.

### 1-3. 코드 스멜

**(1) 거대 타입 — `Army`**

```
Army 계열 partial 합계 : 5,547줄 (7파일)
  Army.cs 단독         : 1,786줄
  public 멤버          : 59개
```

partial로 책임별로 잘랐지만 **하나의 타입**입니다. 진형·이동·전투·사기·돌격·인지를 모두 소유하므로, 어느 하나를 고치면 나머지가 함께 흔들립니다. public 59개는 "무엇이 외부 계약인지"에 대한 판단을 사실상 포기한 상태입니다.

**(2) 진형 좌표 규약이 세 갈래**

`Army_Formation.cs`(97~116행)에 경고 주석이 붙어 있을 만큼 이미 알려진 문제입니다.

```csharp
// 입력 position 은 '중심'처럼 다뤄지고
formation_Start = position + add_width * num_width * 0.5f;
// 저장되는 position 은 '첫 슬롯' 이다
return new Formation_Data(num_width, add_width, vector3s[0], vector3s);
```

출력을 그대로 입력으로 되먹이면 매번 절반 폭씩 밀려납니다. 현재는 "기준점(`formation_Move_Transform`)을 옮기고 `Set_Formation_Move`를 부르라"는 **관례**로 우회하고 있을 뿐, 규약 자체는 남아 있습니다. `Set_Formation` 계열을 직접 부르는 다음 사람이 같은 버그를 재발시킵니다.

**(3) 위치 소유권 규약의 국소적 위반**

"`unit_Data.position`이 위치의 유일한 주인이고 Transform은 하류"라는 규칙이 프로젝트 전반에 걸쳐 잘 지켜집니다(Rigidbody·Collider·물리 콜백을 전부 제거). 그런데 다음 지점은 여전히 `transform.position`을 읽습니다.

| 위치 | 호출 빈도 |
|---|---|
| `Army_Move.Get_Contact_Line_Center` | 재정비마다 전 유닛 |
| `Army_Move.Get_Unit_Positions` | 매칭마다 전 유닛 |
| `Army_Charge.Apply_Collision_Attack` | 충돌 공격마다 표적 부대 전원 |
| `Army_Charge.Flash_Charge_Received` | 돌격 피격마다 전 유닛 |

`Update_Center_Position`은 같은 이유로 이미 `unit_Data.position`으로 전환되었고 그 근거(9,600명 기준 6ms)가 주석에 남아 있습니다. 위 네 곳은 그 전환에서 누락된 것으로 보입니다. **읽는 값도 한 틱 낡습니다**(Transform은 틱 마지막에 쓰이므로).

**(4) 죽은 코드 — `Hungarian` 생성자 6개 중 4개 미사용**

```
사용:   Hungarian(float[,]) , Hungarian(List<Vector3>, List<Vector3>)
미사용: Hungarian() , (List<Vector3>,List<Transform>) ,
        (List<Unit>,List<Vector3>) , (List<Unit>,List<Transform>)
```

미사용 생성자들은 `Units[i].transform.position`을 읽는 구식 경로입니다. 슬롯 데이터화로 그 방식은 폐기되었으므로, 남겨 두면 다음 사람이 "슬롯을 Transform으로 다뤄도 된다"고 오해합니다.

**(5) 오타가 공개 API에 고착**

```csharp
_Upadate_Data()          // Update
AddAccelerationd(float)  // 끝에 d
GetMeleeDiffense()       // Defense
Drag_Formaion()          // Formation
```

`GetMeleeDiffense`는 `Unit_Stat.meleeDiffense` 필드명까지 이어져 있어, 고치면 인스펙터 직렬화가 끊깁니다(`FormerlySerializedAs` 필요). 사소하지만 검색과 자동완성을 계속 방해합니다.

**(6) `FindAnyObjectByType` 런타임 14곳**

대부분 `Start()` 1회라 당장 문제는 아니지만, **씬 배선이 코드에 숨습니다.** 이 패턴은 이미 한 번 성능 사고를 냈습니다 — `Unit_Fight`에서 "찾았지만 없음"을 캐시하지 않아 궁병이 일제 사격할 때마다 씬 전수 탐색이 돌았고, 그 틱만 0.51ms→16.46ms로 튀었습니다(현재는 `bprojectileRendererSearched`로 해결).

**(7) `Controller_Formation.Set_Army_Formation`의 계산 형태**

버블 정렬 + `guard_Max = 10000` 상한이 걸린 while 루프이며, 내부 루프에서 매번 합계를 다시 구합니다. 선택 부대가 수십 개 규모라 실측상 문제는 없으나, **무한 루프 가드가 필요한 코드**라는 사실 자체가 종료 조건이 자명하지 않다는 신호입니다.

---

## 2. 알고리즘 연결성 및 데이터 흐름

### 2-1. 상호작용 지도

```
[입력 — Update]
  Controller.Update
    │  UI_Input_Guard.IsOverUI() 로 HUD 위 클릭 차단
    ├─ _Update_MouseButton_Select  ──> armies_Selected (직접 조작)
    ├─ _Update_MouseButton_Command ──> Army.Move_Start
    └─ _Update_Stance_Command      ──> Army.Set_Stance
  Battle_AI.FixedUpdate            ──> Army.Move_Start / Set_Stance
        └ 플레이어와 '같은 입구'만 사용. 시뮬레이션 규칙을 우회하지 않음

[시뮬레이션 — FixedUpdate, Battle_Manager.phase == Deployment 면 통째로 정지]
  Controller._Update_Army()
    ├ Simulation_Clock.Advance()          틱 +1  (난수 시드의 유일한 근원)
    ├ armyRegistry.Rebuild_Indices()      dirty일 때만
    ├ Snapshot   : Unit ──> unitDataMap (EntityId → Unit_Data)
    ├ ArmyJob    : Army_Data._Update() 병렬 (사기 전이·피로)
    ├ LOD 선별   : _Select_Armies_To_Update()
    ├ Prepare    : 부대별 메인 스레드 (위치캐시·지형·탐지·이동·깃발)
    ├ Schedule   : 전 부대 Job 예약, 대기 없음
    │     Raycast_Setup → RaycastCommand → Unit_Job ─┬─ Pose_Extract → Animation
    │                                                 └─ Unit_Fight_Job
    ├ Complete   : 여기서 한 번만 대기
    ├ Apply      : Job 결과를 Unit/Army에 반영 + 사망·사기 정산
    ├ Collision  : 전역 격자 구축 → 겹침 해소 → 접촉 부대 기록
    ├ GroundSync : 지면 높이 (4틱 분산, 고속 부대 있으면 매 틱)
    ├ TransformWrite : TransformAccessArray 일괄 쓰기
    └ Commit_Pending_Morale_Shock()  전 부대 (LOD와 무관하게 전부)

[표시 — OnGUI]
  UI_* ──읽기 전용──> Army / Army_Data / Battle_Manager
```

**핵심 관찰 세 가지**

1. **입력 → 시뮬레이션은 명령 호출, 시뮬레이션 → 표시는 읽기 전용 조회.** UI를 통째로 들어내도 시뮬레이션은 그대로 돕니다. AI도 플레이어와 동일한 입구만 씁니다. 이 방향성은 잘 지켜집니다.
2. **부대 간 상호작용은 전부 '예약 → 일괄 커밋'.** 사기 충격, 연쇄 붕괴, 피격 점멸이 모두 지연 큐를 거칩니다. `armies` 리스트 순서가 전투 결과를 바꾸지 못하게 하는 장치이며, 이 프로젝트에서 가장 의식적으로 설계된 부분입니다.
3. **틱 시작 스냅샷이 '모두가 같은 세계를 본다'를 보장합니다.** `unitDataMap`을 틱 앞에서 한 번만 만들고, `Army_Grid`도 `builtTick`으로 틱당 1회 구축을 강제합니다.

### 2-2. 로직 흐름 추적 — 이동 명령 한 번

```
1. Controller_Selection      드래그 → armies_Selected
2. Controller_Formation      드래그 궤적 → 전열축·부대별 폭 배분
3. Army.Move_Start           → Move_Start_Internal  (모든 이동의 단일 관문)
4.   Set_Formation           Formation_Job 으로 슬롯 월드 좌표 계산
5.   Set_Army_Move_Position  기준점 이동 (GetPosition() 캐시에 의존)
6.   Set_Formation_Move      슬롯을 '기준점 지역 좌표' 배열로 저장
7.   Match_Units_To_Slots    Formation_Matcher (64명 초과 시 분대 매칭)
8.   Unit.Move_Start(slot, world)   유닛은 슬롯 '인덱스'만 보관
9.   navMeshAgent.SetDestination    부대 기준점 1개만 경로탐색
10.  [배치 단계면] Snap_Units_To_Slots + Invalidate_Center_Position
```

**이 체인에서 주목할 두 지점**

- **9번이 이 게임의 길찾기 전부입니다.** 유닛 9,600명은 경로탐색을 하지 않고 슬롯을 향한 벡터 연산만 합니다. 실측상 경로 재계산은 701틱 동안 180회(틱당 0.257회)뿐입니다. 군집 기반 길찾기가 이미 구현되어 있으며, 이것이 규모를 감당하는 두 번째 이유입니다.
- **5번은 여전히 취약합니다.** `GetPosition()`은 틱당 1회 캐시되는데 배치 단계에서는 틱이 멈춰 갱신 주체가 없습니다. 9번의 명시적 무효화로 막고 있지만, **호출부가 규칙을 알아야만 성립하는 구조**입니다.

### 2-3. 결합도 진단

| 지표 | 수치 | 판정 |
|---|---|---|
| `public static` (Constant 프로퍼티 제외) | 97 | 높음 |
| `FindAnyObjectByType` (런타임) | 14 | 중간 |
| `GameEvents` 사용 | 29 | 양호 (단방향) |
| partial class | 3타입 / 14파일 | 거대 타입의 신호 |

**진단: 계층 간 결합은 낮고, `Army` 내부 결합은 높습니다.**

- **낮음(좋음)** — UI↔시뮬레이션, Job↔관리 코드. Job은 순수 값 타입만 다루므로 테스트 가능하고, 실제로 `Editor/` 프로브 24개가 그 위에서 돕니다.
- **높음(위험)** — `Army`가 모든 것을 압니다. 태세별 밀집도 하나를 넣는 데 진형·재정비·배치·UI·충돌 5개 영역을 연달아 고쳐야 했던 것이 그 증거입니다(`Stance_Density_And_Collision.md`).
- **암묵적(가장 위험)** — 컴파일러가 지켜 주지 않고 주석에만 있는 계약들입니다.
  - `GetPosition()` 캐시 수명
  - `Formation_Data` 좌표 규약
  - "버퍼는 실제 인원보다 클 수 있으니 `.Length`가 아니라 현재 인원을 넘길 것"
  - `Battle_Manager.bdeploying` 정적 상태

  마지막 항목은 `Ensure_Capacity`로 재사용 버퍼를 도입한 대가입니다. `GetSubArray(0, count)`를 빠뜨리면 **지난 틱의 죽은 적을 표적으로 잡는** 버그가 조용히 납니다. `Army_Registry`가 인덱스 계약에 대해 해낸 "구조로 강제하기"를 버퍼 계약에는 아직 적용하지 못했습니다.

### 2-4. 🔴 이 절에서 가장 중요한 발견 — 피해는 한 번에 한 부대에서만 들어온다

피해가 들어오는 경로는 전수 조사 결과 **두 개뿐**이며, 둘 다 `targetArmy` 하나에 게이트되어 있습니다.

```csharp
// Army_Fight._Update_Target_Unit
List<Unit> targetUnits = targetArmy.GetUnits();      // ← 오직 이 부대
unit_Fight_Job.target_Unit_Datas = targetDatas;

// Unit_Fight_Job.Execute — defender = '내' 유닛
if (enemy.bhitTarget && enemy.unit_Target_Data.num == defender.num)
    GetDamage(enemy, ref attackRandom);              // ← 내가 맞는 계산
```

`Unit_Fight_Job`은 **자기 부대의 유닛에게 들어오는 피해**를 계산하며, 그 공격자 후보는 `자기 부대가 선택한 targetArmy`의 유닛뿐입니다. `Army_Charge.Apply_Collision_Attack`도 `targetArmy.units`만 순회합니다.

**따라서 어느 순간이든 한 부대는 정확히 한 적 부대에게만 피해를 입습니다.**

이것이 문제인 이유는 사기 시스템이 **정반대를 가정**하기 때문입니다.

```csharp
// Army_Morale._Update_Morale_Input
if (enemyArmies > 1)
    modifiers.surrounded = -(enemyArmies - 1) * Constant.morale_Penalty_Surrounded;
```

포위(`surrounded`)와 수적 열세(`outnumbered`)는 `army_Detected`를 세므로 **여러 부대에 둘러싸인 상황을 정확히 인식**합니다. 그런데 그 상황에서 실제로 칼을 넣는 것은 한 부대뿐입니다. 즉:

- 3개 부대로 1개 부대를 포위하면 **사기는 3배로 흔들리지만 피해는 1배**입니다.
- 측면 기습 부대의 공격은, 피격 부대가 `time_ReTarget` 주기(기본값)로 표적을 바꾸기 전까지 **한 대도 들어가지 않습니다.**
- `_Update_Target_Army()`는 "이미 근접 교전(Melee) 중이면 표적을 바꾸지 않는다"고 명시합니다. 정면에 붙들린 부대는 측면 공격자로 표적을 **영영 바꾸지 않을 수 있습니다.**

이 게임의 정체성이 "측후방이 방어를 무력화한다"인데(`hit_Angle_Back`에서 방어력과 방패가 0이 됨), **측면 공격자의 피해 자체가 전달되지 않는 경로가 존재**합니다. 밸런스 수치를 아무리 조정해도 이 구조 위에서는 포위 전술이 설계 의도만큼 작동하지 않습니다.

> 주의: 이것은 "버그"가 아니라 **의도된 성능 구조의 부작용**일 수 있습니다. 부대마다 격자를 하나만 만들면 되므로 비용이 접촉면에 비례합니다. 다만 그 대가가 무엇인지는 코드 어디에도 기록되어 있지 않습니다. **먼저 계측으로 실태를 확인한 뒤**(2vs1 교전에서 실제 DPS가 1배인지) 대응을 정하는 것이 이 프로젝트의 방식에 맞습니다.

---

## 3. 향후 기능 제언

### 3-1. 기술적 확장 — 지금 구조에서 싼 것

| 기능 | 근거 | 난이도 |
|---|---|---|
| **태세 추가 (쐐기/원형/사각)** | `E_Army_Stance` + `density_*` + `GetInterval()` 한 곳에서 밀집도가 결정됨. `Formation_Job`의 배치식만 갈아 끼우면 됨 | 하 |
| **탄약 UI** | `Unit_Data.ammunition`이 이미 소모되는데 화면에 없음. 카드에 막대 한 줄 | 하 |
| **일시정지 / 배속** | `Time.timeScale` 사용처 0곳. `Simulation_Clock`이 틱을 직접 세므로 배속과 결정론이 충돌하지 않음 | 하 |
| **부대 상태 아이콘** | `E_Army_Morale`/`E_Army_Fatigue`/`E_Army_Move` 열거형 + DCSS 아이콘 자산 확보됨 | 하 |
| **리플레이** | `Simulation_Random`이 이미 `f(틱, 부대)`. 명령을 `(틱, 부대, 명령)`으로 기록만 하면 재생 가능 — **단 4-1 해결이 선행** | 중 |
| **병종 추가 (공성·전차)** | `E_Unit_Class` + `UnitStatSO`. 다만 `Balance_Data`가 더 비대해짐 | 중 |
| **증원 / 런타임 부대 추가** | `Army_Registry.Register` + `Rebuild_Indices`가 이미 이 시나리오를 상정해 설계됨 | 중 |

### 3-2. 게임 디자인 제언

이 게임의 축은 **"각도와 대열이 수치를 이긴다"** 입니다. 이미 구현된 것에 얹는 순서로 제안합니다.

1. **포위 피해를 실제로 만들 것 (2-4의 게임 측면)**
   사기 툴팁은 "포위 -12"를 보여 주는데 체력바는 그만큼 줄지 않습니다. 플레이어는 이 불일치를 "사기 시스템이 이상하다"로 읽습니다. 포위가 **보이는 대로 아프면** 다대일 집중이라는 전술 축이 비로소 열립니다.

2. **지휘 체계(Chain of Command)** — `general_Aura_Radius`/`general_Death_Shock`가 이미 있습니다. 여기에 "장군에게서 멀면 명령 반영이 지연된다"를 얹으면, 우회 기동의 대가가 생기고 장군 호위라는 판단 지점이 만들어집니다. 토탈워에 없는 차별점입니다.

3. **피로로 인한 추격 실패의 가시화** — `GetFatigueRate()`가 이미 속도를 깎고, `rout_Speed_Rate`로 패주는 오히려 빠릅니다. 즉 **지친 부대는 구조적으로 추격에 실패합니다.** 이 사실이 화면에 드러나면 "무리한 추격"이 실제 판단이 됩니다. 데이터는 이미 다 있고 표시만 없습니다.

4. **부대별 무용담 (After-Action → 캠페인)** — `Battle_Report_Entry`(킬/손실/생존/최종 사기)와 `Campaign_Army`(누적 전과·`veterancy` 4단계)가 이미 연결되어 있습니다. 여기에 "이 부대가 무엇을 했는가" 한 줄만 붙이면 애착이 생깁니다. **새 시스템이 아니라 문자열 조립입니다.**

### 3-3. 최적화 제언

**이 프로젝트는 이미 추측이 아니라 계측으로 판단합니다.** 아래는 코드 주석과 커밋에 남은 실측치입니다.

| 조치 | 결과 | 출처 |
|---|---|---|
| PhysX → 자체 격자 충돌 | 물리 11.5ms(4,800명 기준 틱의 40%) 제거 | `Unit_Collision` 주석 |
| Transform 개별 접근 → `TransformAccessArray` | 4.45 → 0.97ms | `Unit_Transform_Sync` 주석 |
| 애니메이션 Job 분리(`Unit_Pose` 28B) | 31.8 → 15.3ms | 커밋 `89714b1` |
| 레이캐스트 명령 생성 Job화 | Schedule 2.44 → 1.34ms | 커밋 `75dff3d` |
| 애니메이션 입력 매 틱 복사 제거 | Schedule 1.74 → 0.62ms, 합계 17.17 → 15.86ms | 커밋 `ccca11f` |
| **진형 슬롯 Transform → 배열** | 슬롯 읽기 2.955 → 0.029ms, Apply 4.03 → **2.59ms** | `Army._Update_Apply` 주석 |

**반증된 가설도 함께 기록되어 있습니다 — 이것이 이 프로젝트의 최대 자산입니다.**

- **SoA 전환**: `A_UnitUpdate` 2.97ms를 "280B 구조체 복사 비용"으로 진단했으나 실측 결과 복사는 0.43ms, 나머지 2.68ms는 **유닛별 메서드 호출 자체**였습니다. 잘못된 처방이었음이 계측으로 드러났습니다.
- **유닛 후처리 Job 전환**: 네 번 시도해 전부 기준선보다 나빴습니다(2.59 vs 3.25~3.51ms). 슬롯을 배열로 바꿔 입력 수집을 **공짜로 만든 뒤에도** Job이 졌습니다. 원인은 유닛당 작업량이 워커 분배 비용보다 작기 때문입니다.

**이제 무엇을 할 것인가**

- **1순위: 재측정.** 슬롯 데이터화 이후의 단계별 분포가 아직 없습니다. v1이 지목한 "Prepare 47%"는 그 사이 다섯 번의 최적화를 반영하지 않은 값입니다. **`Tick_Profiler` 전체 리포트를 다시 뜨는 것이 다음 최적화 결정의 유일한 근거입니다.**
- **2순위: `Prepare` 안에 하위 계측을 심을 것.** 현재 `Prepare`의 하위 항목은 `P_SetDestination`/`P_AgentMove`뿐이고, 그 둘의 합은 실측상 0.15ms입니다. 나머지(`_Update_Detection`, `_Update_Terrain`, `_Upadate_Data`)가 **측정되지 않습니다.** `_Update_Terrain`이 부대마다 `Physics.Raycast`를 동기 호출하는 점이 유력한 후보입니다 — 60부대면 틱당 60회의 단발 레이캐스트이며, 이는 `RaycastCommand` 배치로 옮길 수 있습니다.
- **3순위: 하지 말 것.**
  - 충돌 최적화 (실측 3.0%)
  - 유닛 후처리 Job 전환 (4회 반증)
  - SoA 전환 (1회 반증)

**규모가 더 커질 때 먼저 무너지는 곳**

부대 수 N이 늘 때의 O(N²)는 이제 `Army_Perception`이 아니라 **`Battle_AI`에 있습니다.** `Army_Grid` 도입으로 탐지는 격자화되었지만 다음은 여전히 `allArmies` 전수 순회입니다.

```
Battle_AI.Find_Soft_Target      기병 판단마다 전 부대
Battle_AI.Is_Cavalry_Near       창병 판단마다 전 부대
Army.Find_Enemy_Army_Near       돌격 대상 탐색마다 전 부대
Army.Find_Nearest_Enemy_Exhaustive  격자 6단계 실패 시 폴백
```

판단 주기가 2초라 현재는 `AI_Decision 0.00ms`지만, 부대 수가 세 자리로 가면 여기가 먼저 병목이 됩니다. **고칠 방법은 이미 프로젝트 안에 있습니다** — `Query_Nearby()`로 바꾸기만 하면 됩니다.

> 참고: `Army_Perception.cs`의 클래스 주석은 아직 "이 영역은 O(N²)입니다"라고 적혀 있으나, 본문은 이미 `Query_Nearby`를 씁니다. 주석이 낡았습니다.

---

## 4. 리팩토링 로드맵

> **실행 완료** — 아래 로드맵은 실제로 수행되었습니다.
> 경과와 계측 근거는 [Combat_Damage_Path_Fix.md](Combat_Damage_Path_Fix.md)를 보십시오.
>
> 요약: 1순위의 진단(피해가 targetArmy 하나에서만 들어온다)은 사실이었으나,
> **계측을 돌리자 그 위에 두 개가 더 쌓여 있었습니다.** 셋 다 예외를 내지 않고
> 조용히 틀리는 종류였고, 그래서 "전투가 안 붙는다"는 증상만 보였습니다.
>
> | 결함 | 발견 경로 | 결과 |
> |---|---|---|
> | 정지한 NavMeshAgent의 `steeringTarget`을 읽어 부대가 월드 +Z로 행군 | 로그의 `LookRotation` 오류 20,496회 | 명중 423 → 1,426회 |
> | 피해가 `targetArmy` 하나에서만 전달 | 본 보고서 2-4절 | 사망 1 → 63명 |
> | 패주 부대가 아무에게도 맞지 않음 (`_Update_Target()` 조기 반환) | 위 수정 중 경로 추적 | 붕괴 0 → 48회 |
> | 패주 기준점이 지면에서 9.7m 이탈 | 전투가 돌기 시작하자 검증기가 포착 | 0.00m |
> | `Accelerate`의 영벡터 `LookRotation` | 대열이 자리를 잡기 시작하자 표면화 | — |
>
> 2,000틱 기준 사망 2명 → 1,942명. 사기·패주·추격 경로가 처음으로 실행되었습니다.
>
> **이 보고서가 놓쳤던 것**: 3-3절에서 "다음 최적화 결정의 유일한 근거는
> 재측정"이라고 썼지만, 정작 **전투가 성립하는지를 먼저 재야 한다는 점**은
> 짚지 못했습니다. 성능 지표(16.59ms)는 정상으로 보였고, 그래서 그 아래에서
> 전투가 통째로 죽어 있다는 사실이 가려졌습니다.
> 계측 항목을 고를 때 '빠른가'보다 '동작하는가'가 먼저입니다.

---

### 원래 로드맵 (참고용)

### 🔴 1순위 — 피해 전달 경로의 실태 계측과 결정

**대상:** `Army_Fight._Update_Target_Unit` / `Unit_Fight_Job`

먼저 **계측**합니다. 2개 부대가 1개 부대를 협공하는 씬을 만들고, 피격 부대의 초당 사망자가 1대1의 2배인지 1배인지 확인합니다. `Editor/Mass_Battle_Builder` + `Mass_Battle_Runner`가 이미 이 형태의 검증을 지원합니다.

1배로 확인되면 선택지는 둘입니다.

| 안 | 방법 | 비용 |
|---|---|---|
| **A. 피격 격자를 전역으로** | 부대별 `fightGrid` 대신 `Controller`가 전역 적 격자를 틱당 1회 구축하고, `Unit_Fight_Job`이 그것을 봄 | 격자 1개로 줄어 오히려 유리할 수 있음. 다만 `targetArmyData`(공격자 부대 스탯)를 유닛마다 조회해야 하므로 `Army_Data` 배열을 Job에 넘기는 구조 변경 필요 |
| **B. 다중 표적 허용** | `targetArmy` 하나 대신 `army_Detected` 상위 2~3개에 대해 Job을 반복 | 구현은 작지만 부대당 Job 수가 배로 늘어남 |

A가 구조적으로 옳습니다. `Unit_Data.armyIndex`가 이미 있고, `Controller`가 전역 `unitDataMap`을 만드는 선례가 있으므로 낯선 방향이 아닙니다.

**왜 1순위인가:** 이 게임의 핵심 재미(측후방·포위)가 성립하는지의 문제입니다. 여기가 흔들리면 그 위에서 조정한 밸런스 수치 122개가 전부 잘못된 전제 위에 서 있게 됩니다.

### 🟠 2순위 — 진형 좌표 규약 통일

```
목표: 진형의 기준은 언제나 '부대 중심'이다. 예외 없음.

  1. Formation_Data.position 을 실제 중심으로 저장
  2. Set_Formation 계열의 입력 규약을 중심 하나로 통일
  3. Move_Reformation_Line 의 center - halfLength 보정 제거
  4. Set_Formation(Formation_Data) 처럼 인자를 무시하는 오버로드 삭제
```

이미 두 번 버그를 냈고(태세 변경 시 대열이 밀림, 마커 180도 반전), 지금은 "기준점을 경유하라"는 **관례**로만 막고 있습니다. 관례는 다음 사람에게 전달되지 않습니다.

이 작업 중에 같은 파일을 열게 되므로 **`_Upadate_Data` 오타와 `Hungarian` 미사용 생성자 4개 제거를 함께 처리**하십시오. 별도 작업으로 잡을 가치는 없습니다.

### 🟡 3순위 — `Army`에서 진형 소유권 분리

5,547줄 / public 59개를 한 번에 줄일 수는 없습니다. **응집도가 가장 높은 덩어리 하나만** 떼십시오.

```
Army_Formation  →  Formation_Controller (슬롯 배열의 유일한 소유자)

  이관 대상: slotLocalPositions / slotWorldPositions / slotCount
             Get_Slot_World / Invalidate_Slot_World / Store_Slots_From_*
             Set_Formation 계열 / Clamp_Formation_Width

  Army 에 남는 것: "내 진형 컨트롤러에게 물어본다" 뿐
```

**2순위와 같은 코드를 건드리므로 반드시 연이어 하십시오.** 좌표 규약을 통일하면서 그 규약의 소유자를 새 타입으로 옮기면 한 번의 작업으로 끝납니다. 순서를 나누면 같은 파일을 두 번 갈아엎게 됩니다.

이 분리가 끝나면 진형 규약을 아는 타입이 **하나**가 되므로, "다음 사람이 `Set_Formation`을 직접 불러 버그를 재발시키는" 경로가 구조적으로 막힙니다. `Army_Registry`가 인덱스 계약에 대해 해낸 것과 같은 방식입니다.

### 우선순위에서 뺀 것

| 항목 | 이유 |
|---|---|
| `public static` 97개 정리 | 대부분 `Constant`/`Balance`의 Burst 접근 경로라 정당합니다. 실제 전역 가변 상태는 `bdeploying`, `armyIndexTable`, `armyGrid` 정도로 소수입니다 |
| IMGUI → UGUI 전환 | 부대 수십 개 규모에서 IMGUI로 충분하고, `StringBuilder` 재사용 등 할당 회피도 이미 되어 있습니다. 배선 비용만 큽니다 |
| `GetMeleeDiffense` 오타 수정 | `Unit_Stat` 필드명까지 이어져 있어 인스펙터 직렬화가 끊깁니다. `FormerlySerializedAs`를 붙일 만한 이득이 아직 없습니다 |
| 결정론 완전화 | 남은 편차 3.1%의 원인(격자 순회 순서에 따른 부동소수점 누적)은 코드에 규명되어 있습니다. 다만 리플레이나 락스텝 멀티를 **실제로 구현할 때** 그 요구사항에 맞춰 정하는 편이 낫습니다. 지금 고치면 성능만 잃습니다 |

---

## 5. 총평

**강점 — 상용 수준인 것**

1. **틱 파이프라인.** 전 부대가 Job을 예약한 뒤 한 번만 대기하는 구조는 부대 수가 늘어도 스톨이 늘지 않습니다. 이것이 9,600명이 도는 첫 번째 이유입니다.
2. **결정론을 위한 설계 의식.** 난수를 좌표의 함수로 만들고, 부대 간 상호작용을 지연 커밋으로 분리하고, 표적 동점을 유닛 번호로 고정한 것 — 이 셋은 "순서가 결과를 바꾸지 않게 한다"는 하나의 원칙에서 일관되게 나온 결정입니다.
3. **계측 문화.** 30단계 프로파일러, 24개 Editor 프로브, CLI 배치 검증(`Mass_Battle_Runner`). 무엇보다 **반증된 가설이 코드 주석에 남아 있습니다.** SoA·Job 전환·Hungarian 무죄 — 이 기록들이 같은 길을 다시 가는 것을 막습니다. 드문 미덕입니다.
4. **주석의 질.** "무엇을 하는가"가 아니라 **"왜 이렇게 했는가, 그러지 않으면 무엇이 깨지는가"**를 적습니다. `Unit.targetSlotIndex`, `Army_Registry`, `pendingMoraleShock`의 주석은 그 자체로 설계 문서입니다.

**약점 — 같은 뿌리에서 나온 것**

`Army`가 너무 많이 압니다. 그 결과 작은 기능 하나가 5개 영역을 건드리고, 계약이 컴파일러가 아니라 주석에 존재합니다. 흥미로운 점은 **이 프로젝트가 그 해법을 이미 알고 있다**는 것입니다 — `Army_Registry`는 "어긋날 수 있는 경로 자체를 없앤다"는 접근으로 인덱스 계약을 구조화했습니다. 같은 처방을 진형 좌표와 버퍼 길이 계약에 적용하면 됩니다.

**그러나 구조보다 먼저 확인할 것이 있습니다.**

포위한 세 부대 중 한 부대만 피해를 준다면, 이 게임이 표방하는 전술이 화면에서 성립하지 않습니다. 사기 시스템은 포위를 정확히 모델링하고 UI는 그것을 표시하는데 체력만 줄지 않는 상태입니다. **1순위를 계측으로 먼저 확인하십시오.** 이 프로젝트는 이미 그렇게 판단해 왔고, 그 방식이 옳았습니다.
