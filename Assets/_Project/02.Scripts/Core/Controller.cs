using System;

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Linq;

public partial class Controller : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// 게임 내 모든 유닛들을 저장하는 리스트입니다.
    /// </summary>
    public List<Unit> units;
    /// <summary>
    /// 게임 내 모든 부대들을 저장하는 리스트입니다.
    /// </summary>
    public List<Army> armies;
    /// <summary>
    /// 현재 선택된 부대들을 저장하는 리스트입니다.
    /// </summary>
    public List<Army> armies_Selected;
    /// <summary>
    /// 지면 레이어 마스크입니다.
    /// </summary>
    public LayerMask layer_Ground;
    /// <summary>
    /// 클릭 가능한 오브젝트 레이어 마스크입니다.
    /// </summary>
    public LayerMask layer_Clickable;
    /// <summary>
    /// 선택 해제 기능을 비활성화하는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_disable_clear = KeyCode.LeftControl;
    /// <summary>
    /// 모든 유닛을 선택하는 키 코드입니다. (현재 사용되지 않음)
    /// </summary>
    public KeyCode keyCode_select_All;
    /// <summary>
    /// 선택 기능을 위한 마우스 키 코드입니다 (기본: 마우스 왼쪽).
    /// </summary>
    public KeyCode keyCode_Select = KeyCode.Mouse0;
    /// <summary>
    /// 명령 기능을 위한 마우스 키 코드입니다 (기본: 마우스 오른쪽).
    /// </summary>
    public KeyCode keyCode_Command = KeyCode.Mouse1;

    // 태세 단축키
    /// <summary>방어 태세(전열 유지)로 전환합니다.</summary>
    public KeyCode keyCode_Stance_Line = KeyCode.Alpha1;
    /// <summary>공격 태세(느슨한 대열)로 전환합니다.</summary>
    public KeyCode keyCode_Stance_Loose = KeyCode.Alpha2;
    /// <summary>방패벽으로 전환합니다.</summary>
    public KeyCode keyCode_Stance_ShieldWall = KeyCode.Alpha3;
    /// <summary>창벽으로 전환합니다.</summary>
    public KeyCode keyCode_Stance_SpearWall = KeyCode.Alpha4;
    /// <summary>산개 태세로 전환합니다.</summary>
    public KeyCode keyCode_Stance_Skirmish = KeyCode.Alpha5;
    /// <summary>
    /// 드래그 선택 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bselect;
    /// <summary>
    /// 드래그 선택 시작점입니다.
    /// </summary>
    public Vector2 select_Start;
    /// <summary>
    /// 드래그 선택 끝점입니다.
    /// </summary>
    public Vector2 select_End;
    /// <summary>
    /// 드래그 선택 UI 박스를 나타내는 RectTransform입니다.
    /// </summary>
    [SerializeField]
    public RectTransform select_UI_Box;
    /// <summary>
    /// 명령 드래그 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bdrag;
    /// <summary>
    /// 진형 형성 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bformation;
    /// <summary>
    /// 진형 시작 위치입니다.
    /// </summary>
    public Vector3 formation_Start;
    /// <summary>
    /// 진형 끝 위치입니다.
    /// </summary>
    public Vector3 formation_End;
    /// <summary>
    /// 드래그로 진형을 형성할 때 표시되는 깃발 1번입니다.
    /// </summary>
    public Transform Flag1;
    /// <summary>
    /// 드래그로 진형을 형성할 때 표시되는 깃발 2번입니다.
    /// </summary>
    public Transform Flag2;

    // 비공개 멤버 변수
    /// <summary>
    /// 전투 진행 단계를 관리합니다. (선택 사항)
    /// 지정하면 배치 단계 동안 시뮬레이션이 멈춥니다.
    /// 비워 두면 예전처럼 시작하자마자 계속 돌아갑니다.
    /// </summary>
    public Battle_Manager battle_Manager;

    /// <summary>
    /// 밸런스 수치 에셋입니다. (선택 사항)
    ///
    /// 지정하면 Awake에서 전역 밸런스에 적용되고, 인스펙터에서 값을 바꾸면
    /// 플레이 중에도 즉시 반영됩니다.
    /// 비워 두면 Balance_Data.Default()가 쓰이며, 이는 리팩토링 이전
    /// Constant의 const 값과 완전히 동일합니다.
    /// </summary>
    public Balance_Config balance_Config;

    /// <summary>
    /// 드래그 선택 영역을 저장하는 Rect 구조체입니다.
    /// </summary>
    private Rect select_Box;

    /// <summary>
    /// 모든 유닛의 콜라이더 EntityId -> Unit_Data 조회 테이블입니다.
    /// 매 틱 새로 할당하지 않도록 Persistent로 유지하고 Clear해서 재사용합니다.
    /// </summary>
    private NativeHashMap<EntityId, Unit_Data> unitDataMap;
    /// <summary>
    /// 부대 데이터를 잡에 넘기기 위한 버퍼입니다. 역시 매 틱 재사용합니다.
    /// </summary>
    private NativeArray<Army_Data> army_Datas;

    // Unity 이벤트 함수
    /// <summary>
    /// MonoBehaviour 인스턴스가 생성될 때 호출됩니다.
    /// </summary>
    private void Awake()
    {
        // 밸런스 수치를 가장 먼저 적용합니다.
        //
        // 부대/유닛 생성(_Start)이 사기 최댓값, 피로 초기값, 공격 속도 등
        // 밸런스 값을 읽어 초기 상태를 만들므로, 그보다 반드시 앞서야 합니다.
        //
        // 에셋이 없으면 기본값으로 되돌립니다. 도메인 리로드를 끈 환경에서
        // 이전 세션이 남긴 수치가 그대로 이어지는 것을 막기 위함입니다.
        if (balance_Config != null) balance_Config.Apply();
        else Balance.Reset_To_Default();

        // 정적 이벤트는 도메인 리로드를 끄면 플레이 모드 종료 후에도 살아남습니다.
        // 이전 세션의 죽은 구독자를 제거해 두어야 예외가 나지 않습니다.
        GameEvents.ClearAll();
        Main_Camera.Clear();

        // 애플리케이션의 목표 프레임 속도를 설정합니다.
        Application.targetFrameRate = Constant.targetFrameRate;

        // 시뮬레이션 틱을 Constant.deltaTime에 맞춥니다.
        // Burst Job은 Constant.deltaTime을, 메인 스레드(Army_Move 등)는
        // Time.fixedDeltaTime을 사용하므로 두 값이 반드시 같아야 합니다.
        Time.fixedDeltaTime = Constant.deltaTime;
    }

    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// </summary>
    private void Start()
    {
        // 모든 부대를 초기화하고 유닛 리스트를 채웁니다.
        for (int i = 0; i < armies.Count; i++)
        {
            armies[i]._Start();
            units.AddRange(armies[i].units);
        }

        // armyIndex -> Army 색인표를 아래 루프와 '같은 리스트'로 만들어 둡니다.
        // 이 순서가 어긋나면 킬이 엉뚱한 부대에 기록되므로 반드시 함께 정합니다.
        Army.Set_Army_Index_Table(armies);

        // 모든 유닛을 초기화합니다.
        // num은 전역 고유 ID, armyIndex는 소속 부대의 인덱스입니다.
        int unitNum = 0;
        for (int armyIndex = 0; armyIndex < armies.Count; armyIndex++)
        {
            List<Unit> armyUnits = armies[armyIndex].units;
            for (int u = 0; u < armyUnits.Count; u++)
            {
                armyUnits[u]._Start(unitNum, armyIndex);
                unitNum++;
            }
        }

        // 시뮬레이션 버퍼를 한 번만 할당해 두고 매 틱 재사용합니다.
        unitDataMap = new NativeHashMap<EntityId, Unit_Data>(Mathf.Max(1, units.Count), Allocator.Persistent);
        army_Datas = new NativeArray<Army_Data>(Mathf.Max(1, armies.Count), Allocator.Persistent);
    }

    /// <summary>
    /// 컴포넌트가 파괴될 때 네이티브 버퍼를 해제합니다.
    /// Persistent 할당은 반드시 명시적으로 해제해야 메모리 누수 경고가 나지 않습니다.
    /// </summary>
    private void OnDestroy()
    {
        if (unitDataMap.IsCreated) unitDataMap.Dispose();
        if (army_Datas.IsCreated) army_Datas.Dispose();
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    private void Update()
    {
        // 마우스 버튼 입력에 따른 선택 및 명령 업데이트를 처리합니다.
        _Update_MouseButton_Select();
        _Update_MouseButton_Command();
        _Update_Stance_Command();
    }

    /// <summary>
    /// 선택된 부대의 태세를 바꾸는 단축키를 처리합니다.
    ///
    /// 태세는 '기동을 포기하고 버틴다'는 거래이므로 플레이어가
    /// 상황을 보고 직접 선택할 수 있어야 의미가 있습니다.
    /// </summary>
    private void _Update_Stance_Command()
    {
        if (armies_Selected.Count == 0) return;

        E_Army_Stance stance;

        if (Input.GetKeyDown(keyCode_Stance_Line)) stance = E_Army_Stance.Line;
        else if (Input.GetKeyDown(keyCode_Stance_Loose)) stance = E_Army_Stance.Loose;
        else if (Input.GetKeyDown(keyCode_Stance_ShieldWall)) stance = E_Army_Stance.ShieldWall;
        else if (Input.GetKeyDown(keyCode_Stance_SpearWall)) stance = E_Army_Stance.SpearWall;
        else if (Input.GetKeyDown(keyCode_Stance_Skirmish)) stance = E_Army_Stance.Skirmish;
        else return;

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            if (armies_Selected[i] == null) continue;
            armies_Selected[i].Set_Stance(stance);
        }
    }

    /// <summary>
    /// 고정된 시간 간격으로 호출됩니다.
    /// </summary>
    private void FixedUpdate()
    {
        // 드래그 중인 경우 진형 형성 로직을 처리합니다.
        if (bdrag)
        {
            RaycastHit raycastHit;
            Ray ray = Main_Camera.Get().ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layer_Ground))
            {
                formation_End = raycastHit.point;

                if (Vector3.Distance(formation_Start, formation_End) > Constant.distance_Formation && bformation == false)
                {
                    bformation = true;
                    Draw_Formation_UI();
                }
            }

            if (bformation == true)
            {
                Drag_Formaion();
            }
        }
        // 배치 단계에서는 시뮬레이션을 돌리지 않습니다.
        // 진형은 잡을 수 있어야 하므로 입력(Update)은 그대로 두고
        // 전투 틱만 멈춥니다.
        if (battle_Manager != null
            && battle_Manager.phase == E_Battle_Phase.Deployment)
        {
            return;
        }

        // 부대 상태를 업데이트합니다.
        _Update_Army();
    }

    /// <summary>
    /// 모든 부대의 데이터를 업데이트합니다.
    /// </summary>
    private void _Update_Army()
    {
        if (!unitDataMap.IsCreated) return;

        // 1. 프레임 시작 시점의 유닛 스냅샷을 만듭니다.
        //    모든 부대가 '같은' 스냅샷을 보므로 부대 처리 순서와 무관하게 결과가 같습니다.
        //    (부대마다 다시 만들면 순서에 따라 결과가 달라져 결정론이 깨집니다)
        unitDataMap.Clear();
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            unitDataMap.TryAdd(units[i].colliderEntityId, units[i].unit_Data);
        }

        // 2. 부대 상태 머신을 갱신합니다.
        if (army_Datas.Length < armies.Count)
        {
            army_Datas.Dispose();
            army_Datas = new NativeArray<Army_Data>(armies.Count, Allocator.Persistent);
        }

        for (int i = 0; i < armies.Count; i++)
        {
            army_Datas[i] = armies[i].army_Data;
        }

        Army_Job army_Job = new Army_Job();
        army_Job.army_Datas = army_Datas;

        // 이 잡은 '부대' 단위(보통 수십 개)라 유닛 잡과 성격이 다릅니다.
        // 항목 수가 적고 항목당 작업량은 크므로 잘게 나누는 편이 유리합니다.
        JobHandle jobHandle = army_Job.Schedule(armies.Count, 4);
        jobHandle.Complete();

        for (int i = 0; i < armies.Count; i++)
        {
            armies[i].army_Data = army_Datas[i];
        }

        // 3. 부대 갱신을 4단계로 나눠 돌립니다.
        //
        //    예전에는 armies[i]._Update() 하나가 Job 스케줄과 Complete()를
        //    함께 했습니다. 그러면 부대마다 메인 스레드가 워커를 기다려
        //    부대 12개면 틱당 24회 이상 멈추고, 부대 간 병렬성이 0이 됩니다.
        //
        //    이제 전 부대의 Job을 '먼저 전부 걸어 두고' 마지막에 한 번만
        //    기다립니다. 부대들의 Job이 서로 겹쳐 실행되므로 워커가 놀지 않습니다.

        // 3-1. 메인 스레드 전처리 (이동, 탐지, 지형)
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i]._Update_Prepare();
        }

        // 3-2. Job 스케줄만 (여기서 기다리지 않습니다)
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i]._Update_Schedule(unitDataMap);
        }

        // 3-3. 완료 대기
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i]._Update_Complete();
        }

        // 3-4. 결과 반영과 사후 정산
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i]._Update_Apply();
        }

        // 4. 부대 간 상호작용(사기 충격, 연쇄 붕괴, 피격 점멸)을 일괄 정산합니다.
        //
        //    3번 단계에서 부대들은 서로에게 충격을 '예약'하기만 합니다.
        //    그 자리에서 바로 적용하면, 아직 갱신되지 않은 부대는 이번 틱에
        //    충격을 반영하고 이미 갱신된 부대는 다음 틱에 반영하게 되어
        //    armies 리스트 순서가 전투 결과를 바꿉니다.
        //
        //    모든 부대의 갱신이 끝난 지금은 아무도 남의 상태를 건드리지 않으므로,
        //    여기서 적용하면 순서와 무관하게 같은 결과가 나옵니다.
        //    1번 단계의 유닛 스냅샷과 같은 목적(결정론)을 사기에 대해 달성합니다.
        for (int i = 0; i < armies.Count; i++)
        {
            armies[i].Commit_Pending_Morale_Shock();
        }
    }
}
