# RTXX

Unity 기반 3D 실시간 전략(RTS) 프로토타입. 픽셀 스프라이트 병사들이 **부대(Army) 단위 진형**을 이루어 이동·전투하는 대규모 전열 전투 게임입니다.

## 개요

RTXX(빌드 산출물 이름 `RTSPSX`)는 3D 지형 위에서 2D 스프라이트 병사들이 진형을 짜고 부딪히는 부대 단위 RTS입니다. 플레이어는 개별 병사가 아니라 **부대**를 드래그로 선택하고, 우클릭 드래그로 진형의 **방향과 폭**을 직접 그려서 명령을 내립니다. 병사 배치·재정렬은 헝가리안 알고리즘(최적 이분 매칭)으로 계산되고, 수백 개 유닛의 이동·전투·애니메이션 갱신은 Unity DOTS(Burst + Job System)로 병렬 처리됩니다.

버전은 `0.1`(ProjectSettings)로, 코어 루프(선택 → 진형 명령 → 이동 → 접적 → 근접 전투)가 동작하는 초기 프로토타입 단계입니다.

## 기술 스택

- **엔진**: Unity **2022.3.62f2** (LTS)
- **언어**: C# (`Assembly-CSharp`, .NET / Mono)
- **렌더링**: Universal Render Pipeline (URP) **14.0.12**, Shader Graph
- **병렬 처리**: Unity Burst 1.8.21, Collections 1.2.4, Job System (`IJobParallelFor`)
- **길찾기**: Unity AI Navigation 1.1.6 (NavMesh / `NavMeshAgent`)
- **기타 패키지**: Terrain Tools 5.0.6, TextMeshPro 3.0.7, uGUI, Timeline, Visual Scripting
- **타깃**: Windows x64 (`Build/RTXX.exe`, `Build/RTSPSX.exe`), 기본 해상도 1920x1080, 목표 60 FPS

## 주요 기능 / 시스템

- **2계층 부대 구조**: `Army`(부대, `partial class`로 Move / Fight / Formation / Data 분할) → `Unit`(개별 병사). 부대 데이터(`Army_Data`)와 병사 데이터(`Unit_Data`)는 Job에서 쓰기 위해 `struct`로 설계.
- **중앙 집중식 업데이트 루프**: 모든 `MonoBehaviour`가 스스로 `enabled = false` 처리되고, `Controller`가 `FixedUpdate`에서 전 부대 → 전 유닛 순으로 `_Update()`를 직접 호출합니다. (Unity 개별 `Update()` 오버헤드 회피)
- **진형(Formation) 시스템**
  - 우클릭 **드래그로 진형선을 그으면** 그 방향과 길이에 맞춰 부대 폭(열 수)이 실시간으로 결정됨. 단순 클릭 시에는 부대 중심 기준으로 자동 진형 생성.
  - `Formation_Job`(Burst)이 각 병사의 진형 슬롯 좌표를 병렬 계산 (중앙에서 좌우로 번갈아 채우는 배치).
  - 여러 부대를 동시에 선택하면 진형선을 따라 좌우 순서대로 정렬 배치.
- **헝가리안 알고리즘 (`Hungarian.cs`)**: 최적 이분 매칭으로 (1) 병사 ↔ 진형 슬롯, (2) 부대 ↔ 진형 구간을 최소 이동 비용으로 배정. 이동 시 병사끼리 경로가 꼬이는 것을 방지.
- **자동 재정렬(Reformation)**: 부대가 Idle 상태로 일정 시간(3초, `Constant.time_Reformation`) 지나고 진형 이탈 병사가 절반 이상이면 헝가리안 재매칭으로 대열을 자동 복구.
- **전투 시스템**
  - 근접/원거리(`E_Unit_AttackType`) 및 공격 상태 머신(`Attack_Able` → `Attack` → `Attack_Disable`).
  - **피격 각도 판정**: 정면(45도 이내) 피격 시 방어/방패 방어력이 0, 측면은 방어력 50%·방패 0으로 감소 → 측후방 공격이 유리.
  - 명중 판정은 `방어 + 기본방어(30) - 공격력` 값과 난수를 비교하는 확률 롤.
  - 피격 시 데미지 벡터가 `Rigidbody.velocity`에 더해져 병사가 물리적으로 밀려남.
- **적 탐지 및 타게팅**: 병사 충돌(`OnCollisionEnter/Exit`)로 접촉한 적 부대를 카운트(`Army_Count`)하고, 가장 많이 접촉한 적 부대를 타깃으로 선택 후 `Unit_Fight_Job`으로 병사별 타깃을 배정.
- **DOTS 병렬 처리**: `Army_Job`, `Unit_Job`, `Unit_Fight_Job`, `Unit_Stat_Job`, `Unit_Animation_Job`, `Formation_Job` 모두 `[BurstCompile] IJobParallelFor`. 전방 시야 판정은 `RaycastCommand.ScheduleBatch`로 배치 레이캐스트, 유닛 조회는 `NativeHashMap<colliderInstanceID, Unit_Data>`로 처리.
- **이동/충돌 물리**: `NavMeshAgent` 기반 경로 탐색에 가속/최고속도/회전속도 제한을 얹음. 아군과 충돌하면 옆으로 비켜가는 밀어내기(push) 처리, 적과 충돌하면 **질량 비**에 따라 속도가 감소(돌파/저지).
- **빌보드 스프라이트 렌더링**: 병사는 3D 지형 위의 2D 스프라이트로, `Unit_Animation_Job`이 카메라 회전에 맞춰 빌보드 회전과 좌우 반전(flip)을 계산. 스프라이트는 DCSS / NetHack 타일셋 아틀라스 사용.
- **RTS 카메라 (`Camera_Player`)**: WASD + 화면 가장자리 스크롤 이동, Q/E 회전, 휠 줌(줌 거리에 이동 속도 연동), Shift 가속. URP `PixelizeFeature`(픽셀화 렌더러 피처)와 연동 구조를 가짐.
- **선택 UI**: 좌클릭 드래그 박스로 부대 선택(Ctrl로 추가 선택), 선택된 부대는 진형 슬롯 마커(`UI_Unit`)와 부대 깃발(`Flag`)을 표시.
- **디버그**: `FpsStatus`가 OnGUI로 FPS/ms를 화면에 표시.

## 프로젝트 구조

```
RTXX/
├─ Assets/
│  ├─ Script/
│  │  ├─ Constant.cs               # 전역 상수(속도, 거리, 타이머, 기본 방어력 등)
│  │  ├─ FpsStatus.cs              # FPS 디버그 오버레이
│  │  ├─ Character/
│  │  │  ├─ Army/                  # 부대 계층
│  │  │  │  ├─ Army.cs             # 부대 본체, 유닛 스폰, 통합 업데이트 루프
│  │  │  │  ├─ Army_Data.cs        # 부대 데이터 struct, 이동/진형/전투 상태 enum
│  │  │  │  ├─ Army_Move.cs        # 부대 이동, 정지, 재정렬(Reformation)
│  │  │  │  ├─ Army_Fight.cs       # 타깃 부대 선정, 스탯 갱신
│  │  │  │  ├─ Army_Formation.cs   # 진형 좌표 생성 및 진형 데이터 관리
│  │  │  │  └─ Army_Job.cs         # Army/Unit_Stat/Formation Burst Job
│  │  │  └─ Unit/                  # 개별 병사 계층
│  │  │     ├─ Unit.cs             # 병사 본체, 충돌 처리, 선택 상태
│  │  │     ├─ Unit_Data.cs        # 병사 데이터 struct 및 상태 enum
│  │  │     ├─ Unit_Move.cs        # 이동/회전/가속/밀어내기 로직
│  │  │     ├─ Unit_Fight.cs       # 공격 상태 머신, 피해 적용
│  │  │     ├─ Unit_Stat.cs        # 병사 스탯 struct(이동/근접/원거리/방어/크기)
│  │  │     ├─ Unit_Animation.cs   # 카메라 기준 빌보드 & 스프라이트 반전
│  │  │     └─ Unit_Job.cs         # Unit/Unit_Fight/Unit_Animation Burst Job
│  │  ├─ System/
│  │  │  ├─ Controller.cs          # 입력·선택·명령·전체 업데이트 오케스트레이션
│  │  │  ├─ Hungarian.cs           # 헝가리안 알고리즘(최적 이분 매칭)
│  │  │  ├─ Camera_Player.cs       # RTS 카메라(이동/회전/줌/픽셀화 연동)
│  │  │  └─ Timer.cs               # 경량 타이머 struct
│  │  └─ UI/UI_Unit.cs             # 진형 슬롯 마커 UI
│  ├─ Scenes/Scene1.unity          # 메인 플레이 씬
│  ├─ Render/                      # URP 에셋, Shader Graph, 픽셀화 렌더러 피처
│  ├─ Image/                       # DCSS / NetHack 스프라이트 아틀라스
│  ├─ PreFab/, Terrain/, TerrainSampleAssets/
│  └─ *.asset                      # TerrainData, URP Global Settings
├─ Packages/manifest.json          # 패키지 의존성
├─ ProjectSettings/                # Unity 프로젝트 설정 (productName: RTSPSX)
├─ Build/                          # Windows x64 빌드 산출물 (RTXX.exe / RTSPSX.exe)
└─ RTXX.sln, Assembly-CSharp.csproj  # Unity가 생성하는 IDE 솔루션 (편집 불필요)
```

## 실행 방법

### 에디터에서 실행
1. **Unity 2022.3.62f2** 를 Unity Hub로 설치합니다. (LTS, URP 14 요구)
2. Unity Hub에서 `E:\GamePJ\RTXX` 폴더를 프로젝트로 추가하고 엽니다. 최초 실행 시 `Packages/manifest.json` 기준으로 패키지가 자동 복원됩니다.
3. `Assets/Scenes/Scene1.unity` 씬을 열고 Play를 누릅니다.
   - `EditorBuildSettings`의 씬 목록이 비어 있으므로, 새로 빌드하려면 File > Build Settings에서 `Scene1`을 직접 추가해야 합니다.

### 빌드된 실행 파일
- `Build/RTXX.exe` (또는 `Build/RTSPSX.exe`) 를 바로 실행하면 됩니다. (Windows x64)

### 조작
| 입력 | 동작 |
|---|---|
| 좌클릭 / 좌클릭 드래그 | 부대 선택 (드래그 박스) |
| Ctrl + 좌클릭 | 선택 유지하며 추가 선택 |
| 우클릭 | 해당 지점으로 이동 명령 |
| 우클릭 드래그 | 드래그한 선을 따라 진형 방향·폭 지정 후 이동 |
| W / A / S / D, 화면 가장자리 | 카메라 이동 |
| Q / E | 카메라 회전 |
| 마우스 휠 | 줌 인/아웃 |
| Left Shift | 카메라 빠른 이동 |

## 개발 현황

- Git 히스토리는 `Initial commit` 1개뿐이며, 프로젝트 버전은 `0.1`입니다.
- **동작하는 것**: 부대 선택, 드래그 진형 명령, NavMesh 이동, 헝가리안 기반 진형 배치/재정렬, 근접 전투 판정 및 피격 넉백, 빌보드 스프라이트 렌더링, Burst/Job 병렬 처리.
- **미완성 / 스텁**: 원거리 공격 피해 계산(`GetDamage_Range`가 데미지 0), 유닛 사망 처리(HP가 0 이하가 되어도 처리 로직 비어 있음), 킬 카운트(`AddKillCount`), 사기/돌격(`MoveCharge`, `MoveEscape` 상태는 enum만 존재), 카메라 드래그 이동, 픽셀화 강도 연동(해당 코드가 주석 처리됨).
- 소스 파일 다수의 주석이 EUC-KR로 저장되어 UTF-8 환경에서 깨져 보입니다(로직에는 영향 없음).
