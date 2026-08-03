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
    /// 유닛 겹침을 물리 엔진 대신 격자로 직접 해소합니다.
    ///
    /// 켜면 Unit 프리팹의 Rigidbody/Collider 없이도 대열이 유지되고,
    /// 4800명 기준 틱당 11.5ms였던 물리 비용이 사라집니다.
    /// 문제가 생기면 꺼서 기존 물리 동작으로 되돌릴 수 있습니다.
    /// </summary>
    [Tooltip("유닛 겹침을 물리 엔진 대신 격자로 해소합니다. 끄면 기존 Rigidbody 동작을 씁니다.")]
    public bool benableUnitCollision = true;

    [Header("LOD")]
    /// <summary>
    /// 교전하지 않는 부대의 갱신을 여러 틱에 나눠 처리합니다.
    ///
    /// 전투 중인 부대는 매 틱 갱신해야 결과가 정확하지만,
    /// 아직 행군 중이거나 멀리 떨어진 부대는 몇 틱에 한 번만 갱신해도
    /// 눈에 띄는 차이가 없습니다. 대신 그 부대의 이동량을 건너뛴 틱만큼
    /// 보정해 실제 속도는 같게 유지합니다.
    ///
    /// 1이면 전부 매 틱 갱신합니다. (LOD 끔)
    /// </summary>
    [Tooltip("교전하지 않는 부대를 몇 틱에 한 번 갱신할지입니다. 1이면 LOD를 끕니다.")]
    [Range(1, 4)]
    public int idleArmyTickInterval = 2;

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

        // 자체 충돌을 쓸 때만 Transform 일괄 쓰기가 성립합니다.
        // 물리 모드에서는 Rigidbody가 Transform을 직접 움직이므로
        // 유닛이 스스로 써야 합니다.
        Unit.bbatchedTransform = benableUnitCollision;

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

        // 유닛이 죽으면 Transform이 파괴되므로 동기화 배열을 다시 만들어야 합니다.
        GameEvents.OnUnitKilled -= On_Unit_Killed_Sync;
        GameEvents.OnUnitKilled += On_Unit_Killed_Sync;
    }

    /// <summary>
    /// 컴포넌트가 파괴될 때 네이티브 버퍼를 해제합니다.
    /// Persistent 할당은 반드시 명시적으로 해제해야 메모리 누수 경고가 나지 않습니다.
    /// </summary>
    private void OnDestroy()
    {
        GameEvents.OnUnitKilled -= On_Unit_Killed_Sync;

        if (unitDataMap.IsCreated) unitDataMap.Dispose();
        if (army_Datas.IsCreated) army_Datas.Dispose();
        if (collisionBodies.IsCreated) collisionBodies.Dispose();
        if (collisionGrid.IsCreated) collisionGrid.Dispose();
        if (collisionPositions.IsCreated) collisionPositions.Dispose();
        if (collisionRadii.IsCreated) collisionRadii.Dispose();
        transformSync.Dispose();
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

        // 3-0. 이번 틱에 갱신할 부대를 고릅니다. (LOD)
        //
        //      교전 중인 부대는 반드시 매 틱 갱신합니다. 전투 결과가 달라지므로
        //      건너뛰면 안 됩니다. 교전하지 않는 부대만 틱을 나눠 처리합니다.
        //      선택 기준이 '부대 인덱스 % 간격'이라 매 실행 같은 순서로 갈리며,
        //      따라서 결정론이 유지됩니다.
        _Select_Armies_To_Update();

        // 3-1. 메인 스레드 전처리 (이동, 탐지, 지형)
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            if (!bupdateArmy[i]) continue;
            armies[i]._Update_Prepare();
        }

        // 3-2. Job 스케줄만 (여기서 기다리지 않습니다)
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            if (!bupdateArmy[i]) continue;
            armies[i]._Update_Schedule(unitDataMap);
        }

        // 3-3. 완료 대기
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            if (!bupdateArmy[i]) continue;
            armies[i]._Update_Complete();
        }

        // 3-4. 결과 반영과 사후 정산
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            if (!bupdateArmy[i]) continue;
            armies[i]._Update_Apply();
        }

        // 3-5. 유닛 겹침 해소 (자체 충돌)
        //
        //     이동이 모두 끝난 뒤 전 유닛을 한 번에 처리합니다.
        //     충돌은 부대 경계를 넘나들므로(적군끼리 부딪힘) 부대 단위로
        //     나눠 풀 수 없고, 전역에서 한 번에 봐야 합니다.
        _Update_Collision();

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

    // =====================================================================
    // 자체 충돌 (Rigidbody 대체)
    // =====================================================================

    /// <summary>충돌 계산용 전역 유닛 버퍼입니다. 매 틱 재사용합니다.</summary>
    private NativeArray<Collision_Body> collisionBodies;

    /// <summary>
    /// 충돌 공간 격자입니다. 매 틱 새로 만들지 않고 Clear해서 재사용합니다.
    /// 할당/해제 비용이 인원수에 비례해 커지기 때문입니다.
    /// </summary>
    private NativeParallelMultiHashMap<int, int> collisionGrid;

    /// <summary>충돌 안쪽 루프용 조밀 위치 배열입니다. 캐시 효율을 위해 분리했습니다.</summary>
    private NativeArray<Vector3> collisionPositions;
    /// <summary>충돌 안쪽 루프용 조밀 반지름 배열입니다.</summary>
    private NativeArray<float> collisionRadii;

    /// <summary>부대별 충돌 반지름 캐시입니다. 유닛마다 부대 스탯을 다시 읽지 않기 위함입니다.</summary>
    private float[] armyRadius;
    /// <summary>부대별 질량 캐시입니다.</summary>
    private float[] armyMass;

    /// <summary>
    /// 전 유닛 Transform을 Job으로 일괄 처리하는 동기화 계층입니다.
    /// Transform 접근이 틱 비용의 26%를 차지해 이 경로로 옮겼습니다.
    /// </summary>
    private readonly Unit_Transform_Sync transformSync = new Unit_Transform_Sync();

    /// <summary>동기화 배열을 다시 만들어야 하는지 여부입니다. (유닛 사망 시 등)</summary>
    private bool btransformSyncDirty = true;

    /// <summary>
    /// 이번 프레임에 사망한 유닛 수입니다.
    /// 0보다 크면 Transform이 파괴되었으므로 동기화 배열을 다시 만들어야 합니다.
    /// GameEvents.OnUnitKilled 구독으로 채워집니다.
    /// </summary>
    private int deadThisFrame;

    /// <summary>
    /// 유닛 목록이 바뀌었음을 알립니다.
    /// 다음 틱에 Transform 동기화 배열을 다시 만듭니다.
    /// </summary>
    public void Invalidate_Transform_Sync()
    {
        btransformSyncDirty = true;
    }

    /// <summary>
    /// 유닛이 죽었을 때 호출됩니다.
    /// Transform이 파괴되므로 동기화 배열을 다시 만들어야 합니다.
    /// </summary>
    private void On_Unit_Killed_Sync(Unit unit, Army victimArmy, Army killerArmy)
    {
        if (unit == null) return;

        // 죽은 유닛의 자리만 비웁니다.
        //
        // 전체를 다시 만들면(Rebuild) 전 유닛을 재등록하므로,
        // 사망이 잦은 전투 중에 틱이 40~60 ms까지 튑니다.
        // 실측에서 840틱 중 215틱이 30 ms를 넘었고 전부 사망 틱이었습니다.
        // 한 칸만 null로 바꾸면 O(1)이고 인덱스 정렬도 유지됩니다.
        int index = unit.unit_Data.num;

        if (index >= 0 && index < units.Count && units[index] == unit)
        {
            transformSync.Clear_At(index);
            return;
        }

        // 인덱스가 어긋난 예외적인 경우에만 전체 재구축으로 되돌립니다.
        btransformSyncDirty = true;
    }

    /// <summary>이번 틱에 갱신할 부대인지 여부입니다. LOD 판정 결과입니다.</summary>
    private bool[] bupdateArmy;

    /// <summary>LOD 틱 카운터입니다.</summary>
    private int lodTick;

    /// <summary>
    /// 이번 틱에 갱신할 부대를 고릅니다.
    ///
    /// 교전 중이거나 무너진 부대는 항상 갱신합니다. 전투 결과와 직결되므로
    /// 건너뛰면 눈에 보이는 차이가 납니다.
    /// 나머지(행군 중이거나 대기 중인 부대)만 인덱스로 나눠 분산합니다.
    /// </summary>
    private void _Select_Armies_To_Update()
    {
        int n = armies.Count;

        if (bupdateArmy == null || bupdateArmy.Length < n)
            bupdateArmy = new bool[Mathf.Max(n, 16)];

        int interval = idleArmyTickInterval;
        if (interval < 1) interval = 1;

        lodTick++;

        for (int i = 0; i < n; i++)
        {
            Army a = armies[i];
            if (a == null) { bupdateArmy[i] = false; continue; }

            if (interval == 1) { bupdateArmy[i] = true; continue; }

            ref Army_Data d = ref a.army_Data;

            // 건너뛸 수 있는 부대는 '움직이지도 싸우지도 않는' 부대뿐입니다.
            //
            // 이동 중인 부대를 건너뛰면 그 틱의 이동량이 통째로 사라져
            // 실제 행군 속도가 느려집니다. (틱 간격만큼 보정하려면
            // Army_Move 전체의 시간 항을 고쳐야 해 위험이 큽니다)
            // 그래서 Idle이면서 교전도 탐지도 없는 부대만 분산합니다.
            //
            // 대기 중인 부대는 진형 재정비 정도만 하므로 몇 틱 늦어도
            // 눈에 띄지 않고, 전투 결과에도 영향이 없습니다.
            bool bskippable = d.e_Army_Move == E_Army_Move.Idle
                           && d.e_Army_Fight == E_Army_Fight.Non
                           && !d.IsBroken()
                           && a.army_Detected.Count == 0;

            bupdateArmy[i] = !bskippable || ((lodTick + i) % interval == 0);
        }
    }

    /// <summary>
    /// 전 유닛의 겹침을 격자로 해소합니다.
    ///
    /// 왜 물리 엔진을 쓰지 않는가:
    /// PhysX에 맡기면 4800명 기준 틱당 11.5ms(전체의 40%)가 물리에 들어갑니다.
    /// 그런데 이 게임이 물리에서 실제로 필요로 하는 것은 '겹치지 않게 밀어내기'
    /// 하나뿐입니다. 관절도 마찰도 회전 관성도 쓰지 않습니다.
    /// 그 하나만 직접 계산하면 훨씬 쌉니다.
    /// </summary>
    private void _Update_Collision()
    {
        if (!benableUnitCollision) return;

        int count = units.Count;
        if (count == 0) return;

        // 버퍼 확보 (재사용)
        if (!collisionBodies.IsCreated || collisionBodies.Length < count)
        {
            if (collisionBodies.IsCreated) collisionBodies.Dispose();
            collisionBodies = new NativeArray<Collision_Body>(
                Mathf.Max(count, 64), Allocator.Persistent);
        }

        var bodies = collisionBodies.GetSubArray(0, count);

        // Transform 동기화 배열을 준비합니다.
        //
        // 유닛이 죽어 Destroy되면 units[i]가 null이 되고, 그 자리의 Transform도
        // 파괴됩니다. 파괴된 Transform이 배열에 남아 있으면 Job이 예외를 던지므로
        // 사망이 발생한 틱에는 반드시 다시 만들어야 합니다.
        //
        // 사망은 Clear_At으로 그 칸만 비우므로 재구축이 필요 없습니다.
        // 길이가 달라졌을 때만(유닛이 추가되었을 때) 다시 만듭니다.
        if (btransformSyncDirty || transformSync.Count != count)
        {
            transformSync.Rebuild(units);
            btransformSyncDirty = false;
        }

        // 위치를 Job으로 일괄 읽습니다.
        //
        // 유닛마다 transform.position을 읽으면 네이티브 왕복이 인원수만큼
        // 발생합니다. 9,600명 기준 6.12 ms였고, Job으로 옮기면 0.38 ms입니다.
        transformSync.Read_Positions();
        bool bsync = transformSync.IsCreated && transformSync.Count == count;

        // 1. 현재 상태를 모읍니다.
        //
        //    반지름과 질량은 '부대' 스탯이라 유닛마다 같습니다.
        //    유닛마다 GetArmy_Data()를 부르면 구조체 복사가 인원수만큼 일어나므로,
        //    부대별로 한 번만 읽어 두고 인덱스로 꺼내 씁니다.
        float maxRadius = 0.0f;

        int armyCount = armies.Count;
        if (armyRadius == null || armyRadius.Length < armyCount)
        {
            armyRadius = new float[Mathf.Max(armyCount, 16)];
            armyMass = new float[Mathf.Max(armyCount, 16)];
        }

        for (int a = 0; a < armyCount; a++)
        {
            if (armies[a] == null) { armyRadius[a] = 0.0f; armyMass[a] = 1.0f; continue; }

            armyRadius[a] = armies[a].army_Data.GetRadius();
            armyMass[a] = armies[a].army_Data.GetMass();

            if (armyRadius[a] > maxRadius) maxRadius = armyRadius[a];
        }

        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null)
            {
                bodies[i] = new Collision_Body { bdead = true, contactArmyIndex = -1 };
                continue;
            }

            int ai = u.unit_Data.armyIndex;
            bool bvalid = ai >= 0 && ai < armyCount;

            bodies[i] = new Collision_Body
            {
                position = bsync ? transformSync.positions[i] : u.transform.position,
                radius = bvalid ? armyRadius[ai] : 0.3f,
                mass = bvalid ? armyMass[ai] : 1.0f,
                bdead = u.IsDead(),
                bplayer = u.unit_Data.bPlayer,
                armyIndex = ai,
                contactArmyIndex = -1
            };
        }

        // 셀 크기는 '가장 큰 두 유닛이 닿을 수 있는 거리'와 정확히 같게 잡습니다.
        //
        // 이보다 크면 3x3 탐색이 필요 이상으로 넓은 범위를 훑어
        // 겹치지도 않을 이웃을 잔뜩 검사합니다. 난전에서 그 비용이 큽니다.
        // 이보다 작으면 3x3이 상호작용 거리를 못 덮어 겹침을 놓칩니다.
        //
        // minCellSize 하한은 좌표가 폭주하는 것을 막기 위한 것이며,
        // 유닛 반지름이 0에 수렴하는 비정상 설정에서만 걸립니다.
        float cellSize = Mathf.Max(maxRadius * 2.0f, Spatial_Grid.minCellSize);

        // 2. 격자 구축 -> 겹침 해소
        //
        //    격자는 매 틱 새로 만들지 않고 재사용합니다.
        //    NativeParallelMultiHashMap 할당/해제는 인원수에 비례해 커지는데,
        //    Clear()는 훨씬 쌉니다. (용량이 모자랄 때만 다시 만듭니다)
        if (!collisionGrid.IsCreated || collisionGrid.Capacity < count)
        {
            if (collisionGrid.IsCreated) collisionGrid.Dispose();
            collisionGrid = new NativeParallelMultiHashMap<int, int>(
                Mathf.Max(count, 64), Allocator.Persistent);
        }
        else
        {
            collisionGrid.Clear();
        }

        // 안쪽 루프가 쓸 조밀 배열입니다. (위치/반지름만)
        if (!collisionPositions.IsCreated || collisionPositions.Length < count)
        {
            if (collisionPositions.IsCreated) collisionPositions.Dispose();
            if (collisionRadii.IsCreated) collisionRadii.Dispose();

            int cap = Mathf.Max(count, 64);
            collisionPositions = new NativeArray<Vector3>(cap, Allocator.Persistent);
            collisionRadii = new NativeArray<float>(cap, Allocator.Persistent);
        }

        var densePos = collisionPositions.GetSubArray(0, count);
        var denseRad = collisionRadii.GetSubArray(0, count);

        var build = new Collision_Grid_Build_Job
        {
            bodies = bodies,
            cellSize = cellSize,
            grid = collisionGrid.AsParallelWriter(),
            positions = densePos,
            radii = denseRad
        };
        JobHandle buildHandle = build.Schedule(count, Constant.jobBatchCount);

        var resolve = new Collision_Resolve_Job
        {
            bodies = bodies,
            positions = densePos,
            radii = denseRad,
            grid = collisionGrid,
            cellSize = cellSize
        };
        resolve.Schedule(count, Constant.jobBatchCount, buildHandle).Complete();

        // 3. 결과를 유닛에 반영합니다.
        //
        //    Transform 쓰기는 배열에만 담고, 마지막에 Job으로 한 번에 씁니다.
        //    여기서 유닛마다 transform.position에 대입하면 네이티브 왕복이
        //    인원수만큼 다시 발생합니다.
        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            Collision_Body b = bodies[i];
            if (b.bdead)
            {
                // 죽은 유닛도 배열에는 현재 값을 유지해야 Write_Transforms가
                // 엉뚱한 자리로 옮기지 않습니다.
                if (bsync) transformSync.rotations[i] = u.unit_Data.rotation;
                continue;
            }

            Vector3 p = b.position;
            if (b.separation.sqrMagnitude > 0.0000001f)
            {
                p += b.separation;
                u.unit_Data.position = p;
            }

            if (bsync)
            {
                transformSync.positions[i] = p;
                transformSync.rotations[i] = u.unit_Data.rotation;

                // 스프라이트도 함께 담습니다. (카메라를 향한 자세, 반동, 내지르기)
                var anim = u.unit_Animation;
                if (anim != null)
                {
                    transformSync.spriteRotations[i] = anim.unit_Animation_Data.rotation;
                    transformSync.spriteScales[i] = anim.spriteScale;
                    transformSync.spriteLocalPositions[i] = anim.spriteLocalPosition;
                }
            }
            else if (b.separation.sqrMagnitude > 0.0000001f)
            {
                u.transform.position = p;
            }

            // 적 접촉 정보는 물리 콜백 대신 여기서 채웁니다.
            // (OnCollisionStay와 같은 역할이지만 콜백 없이 계산으로 얻습니다)
            u.unit_Data.benemyContact = b.benemyContact;
            u.unit_Data.enemyContactNormal = b.enemyContactNormal;
        }

        // 4. 위치/회전과 스프라이트를 Job으로 일괄 반영합니다.
        //    이 두 번의 호출이 유닛마다 하던 Transform 쓰기 전부를 대체합니다.
        if (bsync)
        {
            transformSync.Write_Transforms();
            transformSync.Write_Sprites();
        }

        _Update_Army_Contact();
    }

    /// <summary>
    /// 부대 간 '물리적 접촉' 수를 갱신합니다.
    ///
    /// 왜 필요한가:
    /// 표적 부대 선정(Army._Update_Target_Army)은 접촉 수(num)가 0보다 커야
    /// 근접 교전(Melee)으로 판정합니다. 예전에는 그 값이 OnCollisionEnter로
    /// 쌓였는데, 자체 충돌로 넘어오면 그 콜백이 없습니다.
    /// 이 값을 채워 주지 않으면 근접 부대가 영원히 Range 상태로 굳어
    /// 서로 맞붙어도 난전이 시작되지 않습니다.
    /// </summary>
    private void _Update_Army_Contact()
    {
        // 1. 이번 틱의 접촉 카운트를 초기화합니다.
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i].Clear_Contact_Counts();
        }

        // 2. 접촉 쌍의 카운트를 올립니다.
        //    상대 부대는 Job이 이미 기록해 두었으므로(contactArmyIndex) 바로 씁니다.
        //    예전에는 접촉 유닛마다 전 부대를 순회해 가장 가까운 적을 찾았는데,
        //    그 비용이 O(접촉 유닛 수 x 부대 수)라 난전에서 급격히 커졌습니다.
        int count = units.Count;
        var bodies = collisionBodies;

        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            int foeIndex = bodies[i].contactArmyIndex;
            if (foeIndex < 0 || foeIndex >= armies.Count) continue;

            Army mine = u.GetArmy();
            if (mine == null) continue;

            Army foe = armies[foeIndex];
            if (foe != null) mine.Add_Contact(foe);
        }
    }
}
