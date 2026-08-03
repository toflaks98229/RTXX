using System;
using System.Linq;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 다른 부대와의 접촉 정보를 저장하는 클래스입니다.
/// </summary>
public class Army_Count
{
    /// <summary>
    /// 접촉한 부대의 수입니다.
    /// </summary>
    public int num;
    /// <summary>
    /// 접촉한 부대입니다.
    /// </summary>
    public Army army;

    /// <summary>
    /// 물리 접촉 없이 '시야'로만 탐지된 상태인지 여부입니다.
    ///
    /// num은 실제로 몸이 닿은 유닛 수이고, 이 플래그는 사거리 안에
    /// 들어왔다는 뜻입니다. 원거리 부대는 닿지 않아도 쏠 수 있어야 하므로
    /// 두 가지를 구분해서 들고 있어야 합니다.
    /// </summary>
    public bool bsighted;

    /// <summary>
    /// Army_Count 클래스의 생성자입니다.
    /// </summary>
    /// <param name="army">접촉한 부대 인스턴스입니다.</param>
    /// <param name="num">접촉한 횟수입니다.</param>
    public Army_Count(Army army, int num)
    {
        this.army = army;
        this.num = num;
    }
}

/// <summary>
/// 유닛들의 집합인 부대를 제어하는 주 스크립트입니다.
/// 부대 이동, 전투, 상태 관리 등 전반적인 부대 제어 로직을 포함합니다.
/// </summary>
public partial class Army : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// 부대의 전반적인 데이터를 담고 있는 구조체입니다.
    /// </summary>
    public Army_Data army_Data;
    /// <summary>
    /// 병종 스탯 에셋입니다. (선택 사항)
    /// 지정하면 _Start에서 army_Data.unit_Stat을 이 에셋 값으로 덮어씁니다.
    /// 비워 두면 인스펙터에 직접 입력한 기존 값을 그대로 사용합니다.
    /// </summary>
    public UnitStatSO unit_Stat_Asset;
    /// <summary>
    /// 유닛의 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Unit;
    /// <summary>
    /// 무기 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Weapon;
    /// <summary>
    /// 방패 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Shield;
    /// <summary>
    /// 생성할 유닛 프리팹입니다.
    /// </summary>
    public GameObject unit;
    /// <summary>
    /// 생성된 모든 유닛 인스턴스 리스트입니다.
    /// </summary>
    public List<Unit> units;
    /// <summary>
    /// 부대의 깃발 오브젝트입니다.
    /// </summary>
    public Transform Flag;
    /// <summary>
    /// 깃발을 들고 있는 유닛입니다.
    /// </summary>
    public Unit unit_Bearing_Flag;
    /// <summary>
    /// 부대의 이동을 제어하는 NavMeshAgent 컴포넌트입니다.
    /// </summary>
    public NavMeshAgent navMeshAgent;
    /// <summary>
    /// 부대가 이동할 목표 위치입니다.
    /// </summary>
    public Vector3 locationMoveTo;
    /// <summary>
    /// 현재 부대의 진형 데이터입니다.
    /// </summary>
    public Formation_Data formation_Data;
    /// <summary>
    /// 포메이션 이동 중인지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool bformation_Move = false;
    /// <summary>
    /// 부대 이동의 기준이 되는 Transform입니다.
    /// </summary>
    public Transform formation_Move_Transform;
    /// <summary>
    /// 각 유닛의 포메이션 목표 위치를 나타내는 Transform 리스트입니다.
    /// </summary>
    public List<Transform> formation_Moves;
    /// <summary>
    /// 현재 부대가 선택되었는지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool bselected;
    /// <summary>
    /// 탐지된 다른 부대들의 리스트입니다.
    /// </summary>
    public List<Army_Count> army_Detected;
    /// <summary>
    /// 현재 부대의 공격 목표인 다른 부대입니다.
    /// </summary>
    public Army targetArmy;
    /// <summary>
    /// 포메이션의 최대 길이입니다.
    /// </summary>
    public int formationLength_Max;
    /// <summary>
    /// 유닛 UI 프리팹입니다.
    /// </summary>
    public GameObject UIData;
    /// <summary>
    /// 생성된 유닛 UI 인스턴스 리스트입니다.
    /// </summary>
    public List<UI_Unit> uI_Units;

    // 비공개 멤버 변수
    /// <summary>
    /// 유닛들의 데이터를 저장하는 NativeArray입니다. 잡 시스템에 사용됩니다.
    /// </summary>
    private NativeArray<Unit_Data> unit_Datas;
    /// <summary>
    /// 타겟 유닛들의 데이터를 저장하는 NativeArray입니다. 잡 시스템에 사용됩니다.
    /// </summary>
    private NativeArray<Unit_Data> target_Unit_Datas;

    // -------------------------------------------------------------------------
    // 틱마다 재사용하는 네이티브 버퍼들
    //
    // 예전에는 이 배열들을 매 틱 Allocator.TempJob으로 새로 만들고 버렸습니다.
    // 부대 20개면 틱당 100회의 할당/해제가 일어납니다.
    // Controller가 unitDataMap을 Persistent로 재사용하는 것과 같은 방식으로,
    // '필요한 만큼 자라는' 버퍼를 들고 계속 씁니다.
    //
    // 주의: 버퍼는 실제 인원보다 클 수 있습니다. 따라서 잡을 스케줄할 때
    //       .Length가 아니라 반드시 '현재 인원'을 넘겨야 합니다.
    //       (남는 뒤쪽 칸에는 지난 틱의 값이 그대로 남아 있습니다)
    // -------------------------------------------------------------------------
    private NativeArray<RaycastCommand> raycastCommands;
    private NativeArray<RaycastHit> raycastResults;
    private NativeArray<Unit_Animation_Data> unitAnimationDatas;

    /// <summary>
    /// 애니메이션이 볼 유닛 자세만 추려 담는 버퍼입니다.
    ///
    /// 이 배열이 있어야 애니메이션 Job이 unit_Datas를 건드리지 않게 되고,
    /// 그래야 전투 Job과 병렬로 실행될 수 있습니다.
    /// </summary>
    private NativeArray<Unit_Pose> unitPoses;

    /// <summary>
    /// 이번 틱에 건 유닛 Job 체인의 핸들입니다.
    /// 스케줄 단계와 완료 대기 단계가 분리되어 있어 사이에 들고 있어야 합니다.
    /// </summary>
    private JobHandle unitJobHandle;
    /// <summary>이번 틱에 유닛 Job을 실제로 걸었는지 여부입니다.</summary>
    private bool bunitJobScheduled;

    /// <summary>
    /// 이번 틱에 건 애니메이션 Job 체인의 핸들입니다.
    ///
    /// unitJobHandle과 따로 두는 이유: 애니메이션을 그 뒤에 이어 붙이면
    /// 전투 Job이 애니메이션을 기다리게 되어 체인이 다시 길어집니다.
    /// 두 갈래를 각각 들고 있다가 완료 단계에서 함께 기다립니다.
    /// </summary>
    private JobHandle animationJobHandle;

    /// <summary>
    /// 자세 추출 Job의 핸들입니다.
    ///
    /// 이 Job은 unit_Datas를 읽으므로, 같은 배열에 쓰는 전투 Job은
    /// 반드시 이것을 기다려야 합니다. 짧은 Job이라 대기 비용은 작습니다.
    /// </summary>
    private JobHandle poseExtractHandle;

    /// <summary>이번 틱에 건 전투 Job의 핸들입니다.</summary>
    private JobHandle fightJobHandle;
    /// <summary>이번 틱에 전투 Job을 실제로 걸었는지 여부입니다.</summary>
    private bool bfightJobScheduled;
    /// <summary>전투 Job이 쓰던 공간 격자입니다. 완료 후 해제해야 합니다.</summary>
    private NativeParallelMultiHashMap<int, int> fightGrid;

    /// <summary>
    /// 네이티브 버퍼가 요청한 크기 이상이 되도록 보장합니다.
    /// 모자라면 더 크게 다시 할당하고, 충분하면 그대로 씁니다.
    /// </summary>
    private static void Ensure_Capacity<T>(ref NativeArray<T> buffer, int required)
        where T : struct
    {
        if (buffer.IsCreated && buffer.Length >= required) return;

        if (buffer.IsCreated) buffer.Dispose();

        // 조금씩 늘리면 재할당이 잦아지므로 여유를 두고 잡습니다.
        int capacity = Mathf.Max(required, buffer.IsCreated ? buffer.Length * 2 : 0);
        buffer = new NativeArray<T>(capacity, Allocator.Persistent);
    }

    /// <summary>재사용 버퍼를 모두 해제합니다. Persistent 할당은 반드시 명시적으로 반납해야 합니다.</summary>
    private void Dispose_Buffers()
    {
        if (unit_Datas.IsCreated) unit_Datas.Dispose();
        if (raycastCommands.IsCreated) raycastCommands.Dispose();
        if (raycastResults.IsCreated) raycastResults.Dispose();
        if (unitAnimationDatas.IsCreated) unitAnimationDatas.Dispose();
        if (unitPoses.IsCreated) unitPoses.Dispose();
        if (target_Unit_Datas.IsCreated) target_Unit_Datas.Dispose();
        if (fightGrid.IsCreated) fightGrid.Dispose();
    }
    /// <summary>
    /// 타겟 재설정을 위한 타이머입니다.
    /// </summary>
    private Timer timer_ReTarget;

    /// <summary>
    /// 인접 아군의 붕괴로 누적된 사기 페널티입니다.
    /// GameEvents.OnArmyRouted 구독으로 쌓이고, 시간이 지나면 사그라듭니다.
    /// </summary>
    private float alliedRoutPenalty;

    /// <summary>
    /// 이번 틱의 부대 평균 위치 캐시입니다.
    /// 계산은 _Upadate_Data()에서 틱당 한 번만 이루어집니다.
    /// 자세한 배경은 Army_Move.GetPosition()의 주석을 참고하십시오.
    /// </summary>
    private Vector3 centerPosition;

    /// <summary>캐시된 평균 위치가 이번 틱 기준으로 유효한지 여부입니다.</summary>
    private bool bcenterPositionValid;

    /// <summary>
    /// 이번 틱에 예약된 연쇄 붕괴 페널티입니다.
    /// OnArmyRouted 구독으로 쌓이고, 틱 마지막에 alliedRoutPenalty로 합산됩니다.
    /// </summary>
    private float pendingAlliedRoutPenalty;

    /// <summary>
    /// 이번 틱에 다른 부대로부터 받은 사기 충격의 누적분입니다.
    ///
    /// 왜 즉시 적용하지 않는가:
    /// 부대 갱신(Army._Update)은 Controller.armies 순서대로 '순차' 실행됩니다.
    /// 여기서 상대 부대의 morale_Shock을 직접 더하면, 아직 갱신되지 않은 부대는
    /// 그 충격을 이번 틱에 반영하고 이미 갱신된 부대는 다음 틱에 반영하게 되어
    /// 리스트 순서가 전투 결과를 바꿉니다.
    ///
    /// 그래서 충격은 여기에 쌓아 두기만 하고, 모든 부대의 틱이 끝난 뒤
    /// Controller가 Commit_Pending_Morale_Shock()으로 일괄 적용합니다.
    /// 이렇게 해야 Controller가 틱 시작 시 만드는 유닛 스냅샷과 마찬가지로
    /// '모든 부대가 같은 시점의 세계를 본다'는 규칙이 사기에도 성립합니다.
    /// </summary>
    private float pendingMoraleShock;

    /// <summary>
    /// 이번 틱에 예약된 돌격 피격 점멸입니다.
    ///
    /// 시각 효과라 시뮬레이션 결과를 바꾸지는 않지만, 다른 부대의 유닛을
    /// 틱 도중에 건드리면 그 부대가 아직 갱신 전인지 후인지에 따라
    /// 점멸이 한 틱 어긋나 보입니다. 충격과 같은 시점에 적용해 맞춥니다.
    /// </summary>
    private Vector3 pendingFlashFrom;
    /// <summary>예약된 점멸의 세기입니다. 0이면 예약이 없다는 뜻입니다.</summary>
    private float pendingFlashPower;

    /// <summary>
    /// "Unit" 레이어 마스크를 캐시한 값입니다. 문자열 조회를 매 틱 반복하지 않기 위함입니다.
    /// </summary>
    private static int unitLayerMaskCache = -1;

    /// <summary>"Unit" 레이어 마스크입니다. 최초 접근 시 한 번만 조회합니다.</summary>
    private static int unitLayerMask
    {
        get
        {
            if (unitLayerMaskCache == -1) unitLayerMaskCache = LayerMask.GetMask("Unit");
            return unitLayerMaskCache;
        }
    }

    /// <summary>
    /// 씬에 존재하는 모든 부대입니다. 돌격 대상처럼 '접촉 전'에
    /// 주변 적을 찾아야 하는 경우에 사용합니다.
    /// (army_Detected는 충돌 기반이라 이미 닿은 뒤에만 채워집니다)
    /// </summary>
    public static readonly List<Army> allArmies = new List<Army>();

    /// <summary>
    /// 유닛의 armyIndex로 부대를 즉시 찾기 위한 색인입니다.
    ///
    /// Unit_Data.armyIndex와 반드시 같은 순서여야 하므로,
    /// 두 값을 함께 정하는 Controller.Start에서 Set_Army_Index_Table()로 채웁니다.
    /// (따로 관리하면 어긋나서 엉뚱한 부대에 킬이 기록됩니다)
    /// </summary>
    private static Army[] armyIndexTable;

    /// <summary>
    /// armyIndex -> Army 색인표를 설정합니다.
    ///
    /// 직접 부르지 마십시오. Army_Registry.Rebuild_Indices()가 유닛의 armyIndex를
    /// 부여하면서 같은 배열로 호출합니다. 따로 부르면 두 값이 어긋납니다.
    /// </summary>
    public static void Set_Army_Index_Table(Army[] armies)
    {
        armyIndexTable = armies;
    }

    /// <summary>
    /// 이 부대와 소속 유닛 전체에 armyIndex를 부여합니다.
    ///
    /// Army_Registry가 색인표를 만드는 것과 '같은 루프'에서 호출하므로,
    /// 색인표와 유닛이 보는 인덱스는 구조적으로 같을 수밖에 없습니다.
    ///
    /// 유닛이 아직 생성되기 전(_Start 이전)에 호출되면 units가 비어 있어
    /// 아무 일도 하지 않습니다. 그 경우 Spawn_Units 이후 다시 부르면 됩니다.
    /// </summary>
    /// <param name="armyIndex">이 부대에 부여할 인덱스입니다.</param>
    public void Assign_Army_Index(int armyIndex)
    {
        this.armyIndex = armyIndex;

        if (units == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].unit_Data.armyIndex = armyIndex;
        }
    }

    /// <summary>
    /// 이 부대의 색인입니다. Army_Registry가 부여합니다.
    /// 아직 등록되지 않았으면 -1입니다.
    /// </summary>
    public int armyIndex { get; private set; } = -1;

    /// <summary>
    /// armyIndex로 부대를 찾습니다. 범위를 벗어나면 null입니다.
    /// 예전의 Find_Army_Of_Unit(전 부대 x 전 유닛 순회)을 대체합니다.
    /// </summary>
    private static Army Get_Army_By_Index(int armyIndex)
    {
        if (armyIndexTable == null) return null;
        if (armyIndex < 0 || armyIndex >= armyIndexTable.Length) return null;

        return armyIndexTable[armyIndex];
    }

    /// <summary>
    /// 이 부대가 파괴되었음을 등록소에 알리는 콜백입니다.
    ///
    /// Army는 자기를 소유한 Controller를 알지 못하므로(그래야 테스트가 쉽습니다),
    /// 등록소 쪽에서 이 자리를 채워 둡니다. 비어 있으면 아무 일도 하지 않습니다.
    /// </summary>
    public static System.Action<Army> onArmyDestroyed;

    /// <summary>이 부대를 전역 목록에서 제거합니다.</summary>
    private void OnDestroy()
    {
        allArmies.Remove(this);

        // 등록소의 색인을 무효화합니다.
        // 이 알림이 없으면 파괴된 부대가 색인표에 남아, 그 자리를 조회한
        // 킬 귀속이 조용히 null을 받습니다.
        onArmyDestroyed?.Invoke(this);

        // 정적 이벤트 구독은 반드시 해지해야 합니다.
        // 남겨 두면 파괴된 부대가 호출되어 예외가 납니다.
        GameEvents.OnArmyRouted -= On_Other_Army_Routed;

        // 진행 중인 Job이 있으면 반드시 끝내고 반납해야 합니다.
        // 아직 도는 Job이 참조하는 버퍼를 해제하면 즉시 크래시가 납니다.
        _Complete_Unit();
        _Complete_Target();

        // Persistent로 잡아 둔 재사용 버퍼를 반납합니다.
        Dispose_Buffers();
    }

    /// <summary>
    /// 다른 부대가 붕괴했을 때 호출됩니다.
    ///
    /// 옆의 아군이 무너지는 광경은 그 자체로 사기를 흔듭니다.
    /// 이 처리가 있어야 전열이 한 번 뚫렸을 때 붕괴가 옆으로 번지고,
    /// '한 점을 무너뜨려 전선을 무너뜨린다'는 토탈워식 전개가 성립합니다.
    ///
    /// 이 이벤트는 붕괴한 부대의 틱 도중에 발행되므로, 여기서 alliedRoutPenalty를
    /// 곧바로 올리면 아직 갱신 전인 이웃은 이번 틱에, 이미 갱신된 이웃은 다음 틱에
    /// 반응하게 됩니다. 그래서 예약분(pendingAlliedRoutPenalty)에만 쌓아 두고
    /// 틱 마지막에 일괄 반영합니다.
    /// </summary>
    private void On_Other_Army_Routed(Army routed)
    {
        if (routed == null) return;
        if (routed == this) return;

        // 적이 무너지는 것은 오히려 반가운 일입니다.
        if (routed.army_Data.bplayer != army_Data.bplayer) return;

        // 이미 무너진 부대는 더 흔들릴 것이 없습니다.
        if (army_Data.IsBroken()) return;

        Vector3 to = routed.GetPosition() - GetPosition();
        to.y = 0.0f;

        float radius = Constant.morale_Allied_Rout_Radius;
        if (to.sqrMagnitude > radius * radius) return;

        pendingAlliedRoutPenalty += Constant.morale_Shock_Allied_Rout;
    }

    /// <summary>
    /// 가장 가까운 적 부대를 찾습니다. 없으면 null입니다.
    /// </summary>
    /// <param name="distance">찾은 부대까지의 거리입니다.</param>
    public Army Find_Nearest_Enemy_Army(out float distance)
    {
        Vector3 myPosition = formation_Move_Transform.position;

        // 반경을 넓혀 가며 찾습니다.
        //
        // '가장 가까운 하나'는 반경이 정해져 있지 않아 격자와 잘 맞지 않습니다.
        // 그래서 좁은 반경부터 훑고, 못 찾으면 두 배로 넓힙니다.
        // 대부분의 호출은 첫 반경에서 끝나므로 전수 순회보다 훨씬 쌉니다.
        //
        // 주의: 한 반경 안에서 찾았더라도 그 결과가 전역 최근접입니다.
        // 질의 반경 안의 후보를 '빠짐없이' 받아 그중 최소를 고르기 때문입니다.
        float radius = Army_Grid.cellSize;

        for (int step = 0; step < 6; step++)
        {
            Army nearest = Find_Nearest_Enemy_In(myPosition, radius, out float bestSqr);

            // 찾았고, 그 거리가 질의 반경 안이면 전역 최근접이 확정됩니다.
            // 반경 경계에 걸친 경우에는 더 넓혀 봐야 확실합니다.
            if (nearest != null && bestSqr <= radius * radius)
            {
                distance = Mathf.Sqrt(bestSqr);
                return nearest;
            }

            radius *= 2.0f;
        }

        // 여기까지 왔으면 아주 멀리 흩어져 있는 경우입니다. 전수로 확인합니다.
        return Find_Nearest_Enemy_Exhaustive(myPosition, out distance);
    }

    /// <summary>주어진 반경 안에서 가장 가까운 적 부대를 찾습니다.</summary>
    private Army Find_Nearest_Enemy_In(Vector3 myPosition, float radius, out float bestSqr)
    {
        Army nearest = null;
        bestSqr = float.MaxValue;

        List<Army> candidates = Query_Nearby(myPosition, radius);

        for (int i = 0; i < candidates.Count; i++)
        {
            Army other = candidates[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            Vector3 to = other.formation_Move_Transform.position - myPosition;
            to.y = 0.0f;

            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = other;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 전수 순회로 가장 가까운 적 부대를 찾습니다.
    /// 격자 탐색이 반경을 계속 넓혀도 못 찾았을 때의 마지막 수단입니다.
    /// </summary>
    private Army Find_Nearest_Enemy_Exhaustive(Vector3 myPosition, out float distance)
    {
        Army nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < allArmies.Count; i++)
        {
            Army other = allArmies[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            Vector3 to = other.formation_Move_Transform.position - myPosition;
            to.y = 0.0f;

            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = other;
            }
        }

        distance = nearest != null ? Mathf.Sqrt(bestSqr) : float.MaxValue;
        return nearest;
    }

    /// <summary>
    /// 주어진 지점에 가장 가까운 적 부대를 찾습니다.
    /// 돌격 대상을 '명령받은 진격 방향'으로 한정하기 위해 사용합니다.
    /// </summary>
    public Army Find_Enemy_Army_Near(Vector3 point)
    {
        Army nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < allArmies.Count; i++)
        {
            Army other = allArmies[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            Vector3 to = other.GetPosition() - point;
            to.y = 0.0f;

            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = other;
            }
        }

        return nearest;
    }

    // Unity 이벤트 함수
    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// </summary>
    public void _Start()
    {
        if (!allArmies.Contains(this)) allArmies.Add(this);

        // 이 스크립트를 비활성화하여 Start()와 Update()가 자동으로 호출되지 않도록 합니다.
        // 이 스크립트의 로직은 수동으로 호출되는 _Update(NativeHashMap<EntityId, Unit_Data>)에 의해 제어됩니다.
        enabled = false;

        // 스탯 에셋이 지정된 경우에만 덮어씁니다. (비파괴)
        // 유닛 생성 전에 적용해야 콜라이더/간격 등이 올바른 값으로 만들어집니다.
        if (unit_Stat_Asset != null)
        {
            army_Data.unit_Stat = unit_Stat_Asset.stat;
        }

        Spawn_Units(); // 유닛들을 생성합니다.

        army_Data.unit_Num_Max = army_Data.unit_Num;
        army_Data._Start();
        army_Data.position = formation_Move_Transform.position;
        army_Data.rotation = formation_Move_Transform.rotation;

        navMeshAgent = formation_Move_Transform.GetComponent<NavMeshAgent>();
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        army_Detected = new List<Army_Count>();

        timer_ReTarget = new Timer(Constant.time_ReTarget);

        // 인접 아군의 붕괴에 반응하기 위해 구독합니다.
        // 중복 구독을 막기 위해 먼저 해지한 뒤 등록합니다.
        GameEvents.OnArmyRouted -= On_Other_Army_Routed;
        GameEvents.OnArmyRouted += On_Other_Army_Routed;
    }

    // 공개 메서드
    /// <summary>
    /// 부대 전체를 업데이트하는 함수입니다.
    /// 유닛 데이터, 이동, 타겟 탐지, 통계, 진형 상태 등을 갱신합니다.
    /// </summary>
    /// <param name="unitDataMap">모든 유닛의 데이터를 담고 있는 해시맵입니다.</param>
    public void _Update(NativeHashMap<EntityId, Unit_Data> unitDataMap)
    {
        // 단일 호출 경로입니다. 4단계를 순서대로 실행합니다.
        // Controller는 부대 간 병렬성을 얻기 위해 단계별로 나눠 호출하지만,
        // 테스트나 단독 사용을 위해 이 형태도 유지합니다.
        _Update_Prepare();
        _Update_Schedule(unitDataMap);
        _Update_Complete();
        _Update_Apply();
    }

    /// <summary>
    /// 0단계: 사망 유닛을 정리하고 전멸 여부를 판정합니다.
    /// 이후 단계들은 이 결과(units.Count)를 보고 스스로 건너뜁니다.
    /// </summary>
    public void _Update_Prepare()
    {
        // 파괴되었거나 사망 처리가 끝난 유닛을 리스트에서 제거합니다.
        // 뒤에서부터 순회해야 인덱스가 밀리지 않습니다.
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units[i] == null)
            {
                units.RemoveAt(i);
            }
        }

        // 부대가 전멸했으면 더 이상 갱신할 것이 없습니다.
        if (units.Count == 0)
        {
            army_Data.unit_Num = 0;
            army_Data.HP_All = 0.0f;
            return;
        }

        _Update_Begin();
    }

    // =====================================================================
    // 틱을 3단계로 나눈 이유
    //
    // 예전에는 _Update() 하나가 Job 스케줄과 Complete()를 함께 했습니다.
    // 그러면 부대마다 메인 스레드가 멈춰 워커를 기다리므로,
    // 부대가 12개면 틱당 24회 이상 스톨이 생기고 부대 간 병렬성은 0이 됩니다.
    // 유닛을 2배로 늘려도 Job 시간만 2배지, 스톨 횟수는 부대 수에 그대로 비례합니다.
    //
    // 이제 Controller가 전 부대에 대해
    //   1) _Update_Begin      : 메인 스레드 전처리 (이동, 탐지, 지형)
    //   2) _Update_Schedule   : Job을 '걸어만' 둠
    //   3) _Update_Complete   : 여기서 한 번만 대기
    //   4) _Update_Apply      : 결과 반영과 사후 정산
    // 을 단계별로 돌립니다. 2번이 모든 부대에 대해 끝난 뒤 3번이 오므로
    // 부대들의 Job이 서로 겹쳐 실행됩니다.
    //
    // 결정론: 1번은 틱 시작 시점의 스냅샷만 읽고, 부대 간 상호작용(사기 충격)은
    // 이미 지연 큐로 분리되어 있어 단계를 나눠도 결과가 달라지지 않습니다.
    // =====================================================================

    /// <summary>1단계: 메인 스레드 전처리입니다. Job을 걸기 전에 끝나야 합니다.</summary>
    private void _Update_Begin()
    {
        _Upadate_Data();     // 부대 데이터 + 평균 위치 캐시
        _Update_Terrain();   // 고지 우위와 경사
        _Update_Detection(); // 사거리 안의 적을 접촉 없이 탐지
        _Update_Move();      // 이동 상태
        _Update_Flag();      // 깃발 위치

        // 재사용 버퍼입니다. 실제 인원보다 클 수 있으므로 항상 units.Count까지만 씁니다.
        int unitCount = units.Count;
        Ensure_Capacity(ref unit_Datas, unitCount);

        for (int i = 0; i < unitCount; i++)
        {
            unit_Datas[i] = units[i].unit_Data;
        }
    }

    /// <summary>2단계: Job을 스케줄만 하고 즉시 반환합니다. 여기서 기다리지 않습니다.</summary>
    public void _Update_Schedule(NativeHashMap<EntityId, Unit_Data> unitDataMap)
    {
        if (units.Count == 0) return;

        _Schedule_Unit(unitDataMap); // 레이캐스트 -> 유닛 -> 애니메이션 체인
        _Schedule_Target();          // 표적 선정 + 전투 정산
    }

    /// <summary>3단계: 이 부대가 건 Job들이 끝나기를 기다립니다.</summary>
    public void _Update_Complete()
    {
        if (units.Count == 0) return;

        _Complete_Unit();
        _Complete_Target();
    }

    /// <summary>4단계: Job 결과를 유닛에 반영하고 사후 정산을 합니다.</summary>
    public void _Update_Apply()
    {
        if (units.Count == 0) return;

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Animation);
        _Apply_Unit_Animation();
        Tick_Profiler.End_Sub();

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Formation);
        _Update_Formation();
        Tick_Profiler.End_Sub();

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_UnitUpdate);

        int applyCount = units.Count;

        for (int i = 0; i < applyCount; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            // 264바이트 구조체를 한 번만 씁니다.
            // units[i]를 두 번 인덱싱하지 않는 것도 함께 아낍니다.
            u.unit_Data = unit_Datas[i];
            u._Update();
        }

        Tick_Profiler.End_Sub();

        // unit_Datas는 재사용 버퍼이므로 해제하지 않습니다. (OnDestroy에서 반납)

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Charge);
        _Update_Charge_Impact(); // 돌격 충돌을 상대 부대의 사기 충격으로 정산
        Tick_Profiler.End_Sub();

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Dead);
        _Update_Dead();          // 사망 유닛 정리 및 부대 통계 갱신
        Tick_Profiler.End_Sub();

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Morale);
        _Update_Morale_Input();  // 다음 틱 사기 계산에 쓰일 상황값 산출
        Tick_Profiler.End_Sub();
    }

    /// <summary>
    /// 부대의 재정비 태세를 바꿉니다.
    ///
    /// Line(방어): 명령으로 잡아 둔 전열의 방향과 폭을 유지하며 정비합니다.
    /// Loose(공격): 전열을 신경 쓰지 않고 앞쪽 빈자리만 메웁니다.
    ///
    /// 어느 쪽이든 재정비가 적 위치로 전열 각도를 바꾸지는 않습니다.
    /// </summary>
    public void Set_Stance(E_Army_Stance stance)
    {
        army_Data.e_Army_Stance = stance;
    }

    /// <summary>현재 재정비 태세를 반환합니다.</summary>
    public E_Army_Stance GetStance()
    {
        return army_Data.e_Army_Stance;
    }

    /// <summary>
    /// 사기에 순간적인 충격을 예약합니다. 시간이 지나면 회복됩니다.
    ///
    /// 즉시 반영되지 않고 이번 틱의 마지막(Commit_Pending_Morale_Shock)에 적용됩니다.
    /// 부대 갱신 순서에 따라 결과가 달라지는 것을 막기 위함입니다.
    /// 자세한 이유는 pendingMoraleShock 필드 주석을 참고하십시오.
    /// </summary>
    public void Apply_Morale_Shock(float amount)
    {
        if (amount <= 0.0f) return;
        pendingMoraleShock += amount;
    }

    /// <summary>
    /// 이번 틱에 예약된 사기 충격과 시각 효과를 실제로 적용합니다.
    ///
    /// 모든 부대의 _Update가 끝난 뒤 Controller가 전 부대에 대해 호출합니다.
    /// 이 시점에는 어떤 부대도 더 이상 남의 상태를 건드리지 않으므로,
    /// 적용 순서가 결과에 영향을 주지 않습니다.
    /// </summary>
    public void Commit_Pending_Morale_Shock()
    {
        if (pendingMoraleShock > 0.0f)
        {
            army_Data.morale_Shock += pendingMoraleShock;
            pendingMoraleShock = 0.0f;
        }

        if (pendingAlliedRoutPenalty > 0.0f)
        {
            alliedRoutPenalty += pendingAlliedRoutPenalty;
            pendingAlliedRoutPenalty = 0.0f;
        }

        if (pendingFlashPower > 0.0f)
        {
            Flash_Charge_Received(pendingFlashFrom, pendingFlashPower);
            pendingFlashPower = 0.0f;
        }
    }

    /// <summary>
    /// 돌격 피격 점멸을 예약합니다. 실제 재생은 틱 마지막에 이루어집니다.
    ///
    /// 같은 틱에 여러 방향에서 돌격을 받으면 가장 강한 것만 남깁니다.
    /// 점멸은 '무슨 일이 일어났는지' 알리는 신호이므로 가장 큰 사건을 보여야 합니다.
    /// </summary>
    /// <param name="fromPosition">돌격이 들어온 지점입니다.</param>
    /// <param name="power">충돌 세기(0~1)입니다.</param>
    public void Request_Charge_Flash(Vector3 fromPosition, float power)
    {
        if (power <= pendingFlashPower) return;

        pendingFlashFrom = fromPosition;
        pendingFlashPower = power;
    }

    /// <summary>
    /// 이번 틱에 사망한 유닛을 처리하고 부대 통계(생존 수, 총 HP)를 갱신합니다.
    /// </summary>
    private void _Update_Dead()
    {
        float hpAll = 0.0f;

        for (int i = units.Count - 1; i >= 0; i--)
        {
            Unit unit = units[i];
            if (unit == null)
            {
                units.RemoveAt(i);
                continue;
            }

            if (unit.unit_Data.bdead)
            {
                unit.Dead(); // 선택 해제, 콜라이더 비활성화 등

                // 나를 죽인 유닛이 속한 부대에 킬을 부여합니다.
                // 공격 시점에 기록해 둔 인덱스로 즉시 찾습니다. (O(1))
                Army killerArmy = Get_Army_By_Index(unit.unit_Data.killerArmyIndex);
                if (killerArmy != null) killerArmy.AddKillCount();

                // 사운드/이펙트/통계는 이 이벤트를 구독해서 처리합니다.
                GameEvents.RaiseUnitKilled(unit, this, killerArmy);

                units.RemoveAt(i);
                Destroy(unit.gameObject);
                continue;
            }

            hpAll += unit.unit_Data.HP;
        }

        bool bwasAlive = army_Data.unit_Num > 0;

        army_Data.unit_Num = units.Count;
        army_Data.HP_All = hpAll;

        // 이번 틱에 전멸했다면 한 번만 알립니다.
        if (bwasAlive && units.Count == 0)
        {
            GameEvents.RaiseArmyWiped(this);

            // 장군이 쓰러지면 지휘 계통이 끊겨 전군이 흔들립니다.
            // 반경과 무관하게 같은 편 전체에 적용됩니다.
            if (army_Data.bgeneral)
            {
                On_General_Died();
            }
        }

        // 깃발을 든 유닛이 죽었으면 다른 생존 유닛에게 넘깁니다.
        if (unit_Bearing_Flag == null || unit_Bearing_Flag.unit_Data.bdead)
        {
            unit_Bearing_Flag = units.Count > 0 ? units[0] : null;
        }
    }

    // Find_Army_Of_Unit / Contains_Unit은 제거되었습니다.
    // 사망마다 전 부대의 전 유닛을 훑어 killerNum을 비교하던 함수인데,
    // 이제는 공격 시점에 기록한 killerArmyIndex로 Get_Army_By_Index()가
    // 즉시 찾습니다. (O(부대수 x 인원) -> O(1))

    /// <summary>
    /// 부대 데이터를 업데이트합니다.
    /// </summary>
    public void _Upadate_Data()
    {
        // 부대 평균 위치를 이번 틱에 한 번만 계산합니다.
        //
        // 이 호출은 반드시 _Update의 가장 앞이어야 합니다.
        // 뒤따르는 _Update_Detection, _Update_Target, 사기/돌격 정산이
        // 모두 GetPosition()을 읽기 때문입니다.
        Update_Center_Position();

        Vector3 newPosition = formation_Move_Transform.position;

        // 실측 이동 속도입니다. 돌격 충격량이 이 값에 비례합니다.
        Vector3 delta = newPosition - army_Data.position;
        delta.y = 0.0f;
        army_Data.currentSpeed = delta.magnitude / Constant.deltaTime;

        army_Data.position = newPosition;
        army_Data.rotation = formation_Move_Transform.rotation;
    }

    /// <summary>
    /// 한 지점을 지면 높이로 내려놓습니다.
    ///
    /// 생성 시점에만 쓰는 단발 조회입니다. 매 틱 갱신은
    /// Controller의 Unit_Ground_Sync가 일괄 레이캐스트로 처리합니다.
    /// 지면을 찾지 못하면 원래 높이를 그대로 둡니다.
    /// </summary>
    private static Vector3 Snap_To_Ground(Vector3 position)
    {
        Vector3 origin = position;
        origin.y += Unit_Ground_Sync.rayStartHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            Unit_Ground_Sync.rayDistance, Unit_Ground_Sync.Ground_Mask))
        {
            position.y = hit.point.y;
        }

        return position;
    }

    /// <summary>
    /// 모든 유닛을 생성하고 초기 위치를 설정합니다.
    /// </summary>
    void Spawn_Units()
    {
        if (army_Data.unit_Num <= 0) return;

        // 1. 유닛을 만들기 '전에' 진형 좌표부터 계산합니다.
        //    (uI_Units가 아직 비어 있어도 Set_Formation은 안전하게 건너뜁니다)
        //
        //    예전에는 전원을 formation_Move_Transform.position 한 점에 생성한 뒤
        //    나중에 옮겼습니다. 그러면 물리 엔진이 '전원이 겹쳐 있다'고 판단해
        //    첫 스텝에서 서로를 폭발적으로 밀어냈습니다.
        Vector3 origin = formation_Move_Transform.position
                         - formation_Move_Transform.right
                           * formationLength_Max * army_Data.GetInterval() * 0.5f;

        formation_Data = new Formation_Data(
            Set_Formation(formationLength_Max, transform.right, origin));

        List<Vector3> slots = formation_Data.formation;
        Quaternion spawnRotation = formation_Move_Transform.rotation;

        // 2. 각 유닛을 처음부터 자기 자리에 생성합니다.
        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            // 진형 계산이 실패한 경우를 대비한 안전장치입니다.
            Vector3 slot = i < slots.Count ? slots[i] : formation_Move_Transform.position;

            // 생성 시점에 지면 높이로 내려놓습니다.
            //
            // 진형 좌표는 부대 기준점의 Y를 그대로 물려받으므로, 경사진 곳에서는
            // 뒷열이 공중에 뜨거나 언덕에 파묻힌 채 시작합니다.
            // 주기적 지면 동기화가 곧 맞춰 주지만, 첫 몇 틱 동안 유닛이
            // 눈에 띄게 솟구쳤다 내려앉는 것을 막으려면 여기서 한 번 맞춰야 합니다.
            slot = Snap_To_Ground(slot);

            GameObject unitObject = Instantiate(this.unit, slot, spawnRotation);
            Unit unitComponent = unitObject.GetComponent<Unit>();
            unitComponent.SetArmy(this);

            // 물리 바디와 NavMeshAgent까지 확실히 같은 자리에 놓습니다.
            unitComponent.Place_At(slot, spawnRotation);

            units.Add(unitComponent);

            // 진형 목표 지점은 빈 GameObject 하나면 충분합니다.
            GameObject formation_Move = new GameObject("Formation_Move");
            formation_Move.transform.SetParent(formation_Move_Transform, false);
            formation_Move.transform.position = slot;
            formation_Moves.Add(formation_Move.transform);

            GameObject uiObject = Instantiate(this.UIData, slot, spawnRotation);
            UI_Unit uiUnit = uiObject.GetComponent<UI_Unit>();
            uiUnit._Start(army_Data);
            uI_Units.Add(uiUnit);
        }

        // 3. 이제 uI_Units가 채워졌으므로 진형 마커 위치를 한 번 더 맞춰 줍니다.
        for (int i = 0; i < army_Data.unit_Num && i < uI_Units.Count; i++)
        {
            Vector3 slot = i < slots.Count ? slots[i] : formation_Move_Transform.position;
            uI_Units[i].transform.position = slot;
        }

        // 4. 물리 엔진이 이번 프레임의 위치 변경을 즉시 반영하도록 강제합니다.
        //    이게 없으면 첫 FixedUpdate까지 옛 위치로 충돌을 계산할 수 있습니다.
        Physics.SyncTransforms();

        unit_Bearing_Flag = units[0];

        // 유닛을 방금 진형 자리에 배치했으므로 평균 위치 캐시를 다시 잡습니다.
        // 첫 틱이 돌기 전에 GetPosition()을 읽는 경로(배치 단계 UI, Battle_AI)가
        // 생성 이전의 값을 보지 않도록 하기 위함입니다.
        Invalidate_Center_Position();
    }

    /// <summary>
    /// 유닛 관련 Job(레이캐스트 -> 유닛 -> 애니메이션)을 스케줄만 합니다.
    /// 완료 대기는 _Complete_Unit()에서 별도로 합니다.
    /// </summary>
    /// <param name="unitDataMap">모든 유닛의 데이터를 담고 있는 해시맵입니다.</param>
    private void _Schedule_Unit(NativeHashMap<EntityId, Unit_Data> unitDataMap)
    {
        if (units.Count == 0) return;

        // 1. 레이캐스트 명령과 결과 버퍼를 확보합니다. (매 틱 재사용)
        Ensure_Capacity(ref raycastCommands, units.Count);
        Ensure_Capacity(ref raycastResults, units.Count);

        // ScheduleBatch는 배열 '전체'를 처리하므로, 남는 뒤쪽 칸이 있으면
        // 지난 틱의 명령이 다시 실행됩니다. 앞의 units.Count칸만 잘라서 넘깁니다.
        var commands = raycastCommands.GetSubArray(0, units.Count);
        var results = raycastResults.GetSubArray(0, units.Count);

        // 2. 각 유닛에 대한 레이캐스트 명령 생성
        //    LayerMask.GetMask는 문자열 조회이므로 루프 밖에서 한 번만 구합니다.
        //    (기존에는 '유닛 수 x 매 틱'으로 호출되고 있었습니다)
        int layerMask = unitLayerMask;
        // 밸런스 값이라 컴파일 타임 상수가 아닙니다. 루프 밖에서 한 번만 읽습니다.
        float maxDistance = Constant.distance_Front_Block_Ray;

        // Transform 대신 unit_Data를 읽습니다.
        // 위치는 이미 시뮬레이션이 들고 있고, 정면은 회전에서 유도할 수 있습니다.
        // Transform을 읽으면 인원수만큼 네이티브 왕복이 발생합니다.
        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.S_RaycastSetup);

        // 명령 생성을 Job으로 넘깁니다.
        //
        // 유닛마다 관리 객체를 역참조해 위치와 회전을 읽던 루프였습니다.
        // 그 값은 이미 unit_Datas 배열에 들어 있으므로(_Update_Begin이 채웁니다)
        // Job에서 그대로 읽으면 역참조가 통째로 사라집니다.
        var setupJob = new Raycast_Setup_Job
        {
            unit_Datas = unit_Datas,
            commands = commands,
            parameters = new QueryParameters(layerMask, false,
                                             QueryTriggerInteraction.UseGlobal, false),
            maxDistance = maxDistance
        };

        JobHandle setupHandle = setupJob.Schedule(units.Count, Constant.jobBatchCount);

        Tick_Profiler.End_Sub();

        // 애니메이션 입력을 '스케줄 전에' 채웁니다.
        //
        // 순서 주의: Job을 건 뒤에 이 배열에 쓰면, 아직 도는 Job이 같은 메모리를
        // 읽고 있어 Unity의 Job 안전 시스템이 예외를 던집니다.
        // 메인 스레드 쓰기는 반드시 스케줄보다 앞서야 합니다.
        Ensure_Capacity(ref unitAnimationDatas, units.Count);
        var unit_Animation_Datas = unitAnimationDatas.GetSubArray(0, units.Count);

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.S_AnimInput);

        for (int i = 0; i < units.Count; i++)
        {
            unit_Animation_Datas[i] = units[i].unit_Animation.unit_Animation_Data;
        }

        Tick_Profiler.End_Sub();

        // 3. 레이캐스트 배치 잡(Batch Job) 스케줄링
        //    명령을 만드는 Job이 끝난 뒤에 실행되어야 합니다.
        JobHandle raycastHandle = RaycastCommand.ScheduleBatch(
            commands, results, Constant.jobBatchCount, setupHandle);

        // 4. Unit_Job에 필요한 데이터 전달
        Unit_Job unit_Job = new Unit_Job();
        unit_Job.unit_Datas = unit_Datas;
        unit_Job.raycastHits = results;
        unit_Job.unitDataMap = unitDataMap;
        unit_Job.armyData = army_Data;

        // raycastHandle이 완료된 후에 unit_Job을 실행하도록 의존성 설정
        // (필드 unitJobHandle에 바로 담습니다. 지역 변수로 가리면 안 됩니다)
        unitJobHandle = unit_Job.Schedule(units.Count, Constant.jobBatchCount, raycastHandle);

        // 5. 애니메이션 계통을 전투 계통과 '갈라' 겁니다.
        //
        //    예전에는 애니메이션 Job이 unit_Datas를 받아 그 배열에 되썼습니다.
        //    그 쓰기 때문에 Job 안전 시스템이 전투 Job과 직렬화했고,
        //    한 부대 안에서 Job 5개가 한 줄로 이어졌습니다.
        //    메인 스레드가 그 체인을 기다리는 시간이 틱의 46%였습니다.
        //
        //    이제 자세(position/rotation)만 별도 배열로 추려낸 뒤,
        //    애니메이션은 그 배열만 봅니다. 전투 Job이 unit_Datas에 쓰는
        //    동안에도 함께 돌 수 있으므로 체인이 두 갈래로 갈라집니다.
        //
        //        Raycast -> Unit_Job -+-> Pose_Extract -> Animation
        //                             +-> (Fight_Job: Army_Fight에서 연결)
        Ensure_Capacity(ref unitPoses, units.Count);
        var poses = unitPoses.GetSubArray(0, units.Count);

        Unit_Pose_Extract_Job extractJob = new Unit_Pose_Extract_Job();
        extractJob.unit_Datas = unit_Datas;
        extractJob.poses = poses;

        // 추출 핸들은 전투 Job도 기다려야 하므로 필드에 담아 둡니다.
        poseExtractHandle =
            extractJob.Schedule(units.Count, Constant.jobBatchCount, unitJobHandle);

        Unit_Animation_Job unit_Animation_Job = new Unit_Animation_Job();
        unit_Animation_Job.poses = poses;
        unit_Animation_Job.unit_Animation_Datas = unit_Animation_Datas;
        unit_Animation_Job.cam_Position = Main_Camera.GetTransform().position;
        unit_Animation_Job.cam_Rotation = Main_Camera.GetTransform().rotation;

        // 애니메이션 핸들은 별도로 들고 있다가 완료 단계에서 함께 기다립니다.
        // unitJobHandle에 이어 붙이면 전투 Job이 다시 그 뒤로 밀립니다.
        animationJobHandle =
            unit_Animation_Job.Schedule(units.Count, Constant.jobBatchCount, poseExtractHandle);

        bunitJobScheduled = true;
    }

    /// <summary>
    /// 유닛 Job 체인이 끝나기를 기다립니다.
    ///
    /// 전투 갈래와 애니메이션 갈래를 하나로 묶어 한 번만 기다립니다.
    /// 각각 Complete()하면 스케줄 왕복을 두 번 내는 셈입니다.
    /// </summary>
    private void _Complete_Unit()
    {
        if (!bunitJobScheduled) return;

        JobHandle.CombineDependencies(unitJobHandle, animationJobHandle).Complete();
        bunitJobScheduled = false;
    }

    /// <summary>Job이 계산한 애니메이션 데이터를 컴포넌트에 되돌려 반영합니다.</summary>
    private void _Apply_Unit_Animation()
    {
        if (units.Count == 0) return;
        if (!unitAnimationDatas.IsCreated) return;

        int count = Mathf.Min(units.Count, unitAnimationDatas.Length);

        for (int i = 0; i < count; i++)
        {
            units[i].unit_Animation.unit_Animation_Data = unitAnimationDatas[i];
        }

        for (int i = 0; i < count; i++)
        {
            units[i].unit_Animation._Update();
        }

        // 전부 재사용 버퍼의 부분 배열이므로 여기서 해제하지 않습니다.
        // (부분 배열은 소유권이 없어 Dispose 대상이 아니며, 원본은 OnDestroy에서 반납합니다)
    }

    /// <summary>
    /// 모든 유닛 인스턴스 리스트를 반환합니다.
    /// </summary>
    /// <returns>유닛 리스트입니다.</returns>
    public List<Unit> GetUnits()
    {
        return units;
    }

    /// <summary>
    /// 장군이 전사했을 때 같은 편 전군에 사기 충격을 가합니다.
    ///
    /// 토탈워에서 장군의 죽음은 전선을 통째로 무너뜨리는 사건입니다.
    /// 그래서 '장군을 노려 전투를 끝낸다'는 전술이 성립합니다.
    ///
    /// 충격은 예약만 되고 틱 마지막에 일괄 적용되므로, 전군이 갱신 순서와 무관하게
    /// 정확히 같은 틱에 같은 크기로 흔들립니다.
    /// </summary>
    private void On_General_Died()
    {
        for (int i = 0; i < allArmies.Count; i++)
        {
            Army other = allArmies[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer != army_Data.bplayer) continue;

            other.Apply_Morale_Shock(Constant.general_Death_Shock);
        }

        GameEvents.RaiseGeneralDied(this);
    }

    /// <summary>
    /// 살아 있는 아군 장군에게서 받는 사기 보너스입니다.
    /// 가까울수록 크며, 반경 밖이면 0입니다.
    ///
    /// 장군 자신도 이 보너스를 받습니다. 지휘관은 스스로도 흔들리지 않습니다.
    /// </summary>
    private float Get_General_Aura()
    {
        float best = 0.0f;
        Vector3 myPosition = GetPosition();

        float radius = Constant.general_Aura_Radius;
        float radiusSqr = radius * radius;

        // 격자로 후보를 줄입니다. 장군은 몇 안 되지만 이 함수는
        // '모든 부대가 매 틱' 부르므로, 전수 순회하면 그것만으로 N^2입니다.
        List<Army> candidates = Query_Nearby(myPosition, radius);

        for (int i = 0; i < candidates.Count; i++)
        {
            Army other = candidates[i];
            if (other == null) continue;
            if (!other.army_Data.bgeneral) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer != army_Data.bplayer) continue;

            // 무너진 장군은 아무도 떠받치지 못합니다.
            if (other.army_Data.IsBroken()) continue;

            Vector3 to = other.GetPosition() - myPosition;
            to.y = 0.0f;

            float sqr = to.sqrMagnitude;
            if (sqr > radiusSqr) continue;

            // 가까울수록 강한 선형 감쇠입니다.
            float t = 1.0f - Mathf.Sqrt(sqr) / radius;
            float value = t * Constant.general_Aura_Morale;

            if (value > best) best = value;
        }

        return best;
    }

    /// <summary>
    /// 부대가 선택되었는지 여부를 반환합니다.
    /// </summary>
    /// <returns>선택되었으면 true, 아니면 false입니다.</returns>
    public bool IsSelected()
    {
        return bselected;
    }

    /// <summary>
    /// 부대를 선택 해제하고, 모든 유닛의 선택 상태를 해제합니다.
    /// </summary>
    public void UnSelected()
    {
        bselected = false;
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (unit.IsDead()) continue;
            unit.UnSelected();
        }

        GameEvents.RaiseArmySelectionChanged(this, false);
    }

    /// <summary>
    /// 부대를 선택하고, 모든 유닛의 선택 상태를 활성화합니다.
    /// </summary>
    public void Selected()
    {
        bselected = true;
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (unit.IsDead()) continue;
            unit.Selected();
        }

        GameEvents.RaiseArmySelectionChanged(this, true);
    }

    /// <summary>
    /// 킬 수를 증가시킵니다. (현재 로직은 비어있습니다.)
    /// </summary>
    public void AddKillCount()
    {
        // 킬 수 증가 로직 추가 예정
    }

    /// <summary>
    /// 부대 진형 UI를 보이게 합니다.
    /// </summary>
    public void Draw_Formation_UI()
    {
        // 생존 인원만큼만 표시합니다.
        int count = Mathf.Min(army_Data.unit_Num, uI_Units.Count);

        for (int i = 0; i < count; i++)
        {
            uI_Units[i].Visible();
        }
    }

    /// <summary>
    /// 부대 진형 UI를 숨깁니다.
    /// </summary>
    public void Erase_Formation_UI()
    {
        // 숨길 때는 전부 순회해야 합니다.
        // 인원이 많던 시절에 켜진 마커가 전사 후 unit_Num 밖으로 밀려나면
        // 영원히 켜진 채 남기 때문입니다.
        for (int i = 0; i < uI_Units.Count; i++)
        {
            uI_Units[i].Invisible();
        }
    }

    // 비공개 메서드
    /// <summary>
    /// 깃발의 위치를 업데이트합니다.
    /// </summary>
    private void _Update_Flag()
    {
        if (unit_Bearing_Flag == null) return;

        Vector3 targetVector = unit_Bearing_Flag.transform.position;
        targetVector.y = unit_Bearing_Flag.transform.position.y + 5.0f;

        Vector3 eulerAngles = Main_Camera.GetTransform().rotation.eulerAngles;
        eulerAngles = new Vector3(0.0f, eulerAngles.y, 0.0f);

        Flag.transform.position = Vector3.Lerp(Flag.transform.position, targetVector, 0.1f);
        Flag.rotation = Quaternion.Euler(eulerAngles);
    }
}
