# RTXX 폴더 구조

SCPPJ의 `Assets/_Project` 규칙을 따릅니다.
핵심 원칙은 **"내가 만든 것과 남이 만든 것을 섞지 않는다"** 입니다.

```
Assets/
├── _Project/          ← 이 프로젝트의 자작 에셋 (여기만 관리하면 됩니다)
└── ThirdParty/        ← 외부에서 가져온 패키지 (통째로 교체/삭제 가능)
```

## _Project

| 폴더 | 담는 것 |
|---|---|
| `01.Scenes` | 씬 파일과 씬 전용 데이터(NavMesh 등) |
| `02.Scripts` | 모든 C# 스크립트 (아래 참조) |
| `03.DataAssets` | ScriptableObject 에셋 (병종 스탯, 밸런스 설정) |
| `04.Art` | 이미지, 셰이더, 머티리얼, 지형 데이터 |
| `05.Prefabs` | 프리팹 |
| `07.Settings` | 렌더 파이프라인 등 프로젝트 설정 에셋 |
| `09.Docs` | 이 문서를 포함한 설계 문서 |

번호가 비어 있는 것(06, 08, 10)은 SCPPJ와 번호를 맞추기 위함입니다.
사운드가 생기면 `06.Sound`, 테스트가 생기면 `10.Tests`를 쓰십시오.

## 02.Scripts 분류 기준

스크립트는 **"무엇을 아는가"** 를 기준으로 나눕니다.

| 폴더 | 기준 | 예 |
|---|---|---|
| `Core` | 시뮬레이션 루프의 주인과 전역 규약. 다른 계층이 여기에 의존합니다. | `Controller`, `GameEvents`, `Constant` |
| `Character/Army` | 부대 단위 로직 | `Army`, `Army_Move`, `Army_Fight` |
| `Character/Unit` | 유닛 단위 로직과 Job | `Unit`, `Unit_Job`, `Unit_Data` |
| `Gameplay` | 전투 한 판의 진행과 연출. 시뮬레이션을 '사용'만 합니다. | `Battle_Manager`, `Battle_AI`, `Camera_Player` |
| `Data` | 밸런스 수치 정의 | `Balance`, `Balance_Data`, `Balance_Config` |
| `Framework` | 게임 내용을 모르는 범용 유틸. 다른 프로젝트에 그대로 옮겨도 동작합니다. | `Hungarian`, `Spatial_Grid`, `Timer` |
| `UI` | 화면 표시 | `UI_Unit`, `UI_Army_Banner` |

`Framework`에 게임 규칙이 들어가기 시작하면 분류가 무너진 신호입니다.
그때는 `Gameplay`나 `Core`로 옮기십시오.

## 주의: 파일을 옮길 때

Unity에서 에셋을 옮길 때는 **반드시 `.meta` 파일을 함께** 옮겨야 합니다.
`.meta`에 담긴 GUID로 씬과 프리팹이 서로를 참조하므로, 이것이 빠지면
Unity가 새 GUID를 만들어 **모든 참조가 끊어집니다.**

가장 안전한 방법은 Unity 에디터의 Project 창에서 드래그하는 것입니다.
에디터 밖에서 옮겼다면 `.meta`가 따라갔는지 확인하십시오.
