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

    /// <summary>이 부대를 전역 목록에서 제거합니다.</summary>
    private void OnDestroy()
    {
        allArmies.Remove(this);

        // 정적 이벤트 구독은 반드시 해지해야 합니다.
        // 남겨 두면 파괴된 부대가 호출되어 예외가 납니다.
        GameEvents.OnArmyRouted -= On_Other_Army_Routed;
    }

    /// <summary>
    /// 다른 부대가 붕괴했을 때 호출됩니다.
    ///
    /// 옆의 아군이 무너지는 광경은 그 자체로 사기를 흔듭니다.
    /// 이 처리가 있어야 전열이 한 번 뚫렸을 때 붕괴가 옆으로 번지고,
    /// '한 점을 무너뜨려 전선을 무너뜨린다'는 토탈워식 전개가 성립합니다.
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

        alliedRoutPenalty += Constant.morale_Shock_Allied_Rout;
    }

    /// <summary>
    /// 가장 가까운 적 부대를 찾습니다. 없으면 null입니다.
    /// </summary>
    /// <param name="distance">찾은 부대까지의 거리입니다.</param>
    public Army Find_Nearest_Enemy_Army(out float distance)
    {
        Army nearest = null;
        float bestSqr = float.MaxValue;
        Vector3 myPosition = formation_Move_Transform.position;

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

        _Upadate_Data(); // 부대 데이터 업데이트
        _Update_Terrain(); // 고지 우위와 경사 갱신
        _Update_Detection(); // 사거리 안의 적을 접촉 없이 탐지
        _Update_Move(); // 이동 상태 업데이트

        unit_Datas = new NativeArray<Unit_Data>(units.Count, Allocator.TempJob);
        for (int i = 0; i < units.Count; i++)
        {
            unit_Datas[i] = units[i].unit_Data;
        }

        _Update_Flag(); // 깃발 위치 업데이트
        _Update_Unit(unitDataMap); // 유닛 상태 업데이트
        _Update_Target(); // 타겟 상태 및 전투 정산
        _Update_Formation(); // 진형 상태 업데이트

        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Data = unit_Datas[i];
            units[i]._Update();
        }

        unit_Datas.Dispose();

        _Update_Charge_Impact(); // 돌격 충돌을 상대 부대의 사기 충격으로 정산
        _Update_Dead(); // 사망 유닛 정리 및 부대 통계 갱신
        _Update_Morale_Input(); // 다음 틱 사기 계산에 쓰일 상황값 산출
    }

    /// <summary>
    /// 이번 틱에 발생한 돌격 충돌을 정산합니다.
    /// 충돌한 유닛 수에 비례해 상대 부대에 사기 충격을 가합니다.
    /// </summary>
    private void _Update_Charge_Impact()
    {
        if (targetArmy == null) return;

        int impacts = 0;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            if (!units[i].unit_Data.bchargeImpact) continue;

            units[i].unit_Data.bchargeImpact = false;
            impacts++;

            // 돌격한 쪽: 충돌 순간을 크게 번쩍입니다.
            if (units[i].unit_Animation != null)
            {
                units[i].unit_Animation.Flash_Charge_Impact(units[i].unit_Data.chargeImpactPower);
            }
        }

        if (impacts == 0) return;

        // 충돌 비율만큼 충격을 가합니다. 전원이 부딪히면 전량입니다.
        float rate = army_Data.unit_Num > 0 ? (float)impacts / army_Data.unit_Num : 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        // 충격량은 부딪힌 속도에도 비례합니다.
        // 느릿하게 밀고 들어간 접촉은 사기를 거의 흔들지 못합니다.
        float momentum = 0.0f;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            if (units[i].unit_Data.chargeImpactPower > momentum)
                momentum = units[i].unit_Data.chargeImpactPower;
        }

        float shock = Constant.morale_Shock_Charge * rate * momentum;
        if (shock <= 0.0f) return;

        // 측후방에서 받은 돌격은 훨씬 크게 흔듭니다.
        Vector3 toVictim = targetArmy.GetPosition() - GetPosition();
        toVictim.y = 0.0f;

        if (toVictim.sqrMagnitude > 0.0001f)
        {
            Vector3 victimForward = targetArmy.formation_Move_Transform.forward;
            victimForward.y = 0.0f;

            // 피격 부대의 정면과 '돌격이 들어온 방향'이 이루는 각도로 판정합니다.
            float dot = Vector3.Dot(victimForward.normalized, toVictim.normalized);

            // dot이 양수면 뒤에서 찔린 것입니다.
            if (dot > 0.0f) shock *= Constant.morale_Shock_Flank_Rate;
        }

        // 창벽에 정면으로 뛰어들었다면 돌격이 되받아쳐집니다.
        //
        // 창을 세우고 버티는 대열에 말을 몰아넣으면 손해를 보는 쪽은
        // 돌격한 쪽입니다. 충격이 상대가 아니라 나에게 돌아옵니다.
        if (targetArmy.army_Data.IsChargeReflecting() && Is_Charging_Into_Front(targetArmy))
        {
            Apply_Morale_Shock(Constant.stance_SpearWall_Reflect_Shock * rate);

            // 되받아친 쪽도 접촉면이 번쩍여 무슨 일이 일어났는지 보이게 합니다.
            targetArmy.Flash_Charge_Received(GetPosition(), momentum);
            return;
        }

        targetArmy.Apply_Morale_Shock(shock);

        // 충격을 받은 쪽에서도 접촉면의 유닛들이 번쩍이도록 합니다.
        // 어느 방향에서 돌격이 들어왔는지 눈으로 알 수 있게 하기 위함입니다.
        targetArmy.Flash_Charge_Received(GetPosition(), momentum);
    }

    /// <summary>
    /// 내가 상대의 '정면'으로 돌격해 들어갔는지 판정합니다.
    /// 돌격 반사는 정면에서만 성립합니다. 측후방을 친 돌격은 반사되지 않습니다.
    /// </summary>
    private bool Is_Charging_Into_Front(Army victim)
    {
        Vector3 toMe = GetPosition() - victim.GetPosition();
        toMe.y = 0.0f;

        if (toMe.sqrMagnitude < 0.0001f) return true;

        Vector3 victimForward = victim.formation_Move_Transform.forward;
        victimForward.y = 0.0f;

        if (victimForward.sqrMagnitude < 0.0001f) return true;

        return Vector3.Dot(victimForward.normalized, toMe.normalized)
               >= Constant.stance_Front_Dot;
    }

    /// <summary>
    /// 돌격을 받은 쪽의 시각 피드백입니다.
    /// 돌격이 들어온 방향에 가까운 유닛들만 번쩍여 충돌 지점을 드러냅니다.
    /// </summary>
    /// <param name="fromPosition">돌격이 들어온 지점입니다.</param>
    /// <param name="power">충돌 세기(0~1)입니다.</param>
    public void Flash_Charge_Received(Vector3 fromPosition, float power)
    {
        // 접촉면 판정 반경입니다. 진형 간격을 기준으로 잡습니다.
        float radius = army_Data.GetInterval() * 4.0f;
        if (radius <= 0.0f) radius = 4.0f;

        float radiusSqr = radius * radius;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            if (units[i].unit_Animation == null) continue;

            Vector3 to = units[i].transform.position - fromPosition;
            to.y = 0.0f;

            if (to.sqrMagnitude > radiusSqr) continue;

            units[i].unit_Animation.Flash_Charge_Impact(power);
        }
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
    /// 사기에 순간적인 충격을 가합니다. 시간이 지나면 회복됩니다.
    /// </summary>
    public void Apply_Morale_Shock(float amount)
    {
        if (amount <= 0.0f) return;
        army_Data.morale_Shock += amount;
    }

    /// <summary>
    /// 사기의 '목표값'을 계산합니다. 실제 사기 이동은 Burst Job(Army_Data._Update)이 수행합니다.
    /// 토탈워식으로 손실률, 포위, 지휘(깃발) 상황을 종합합니다.
    /// </summary>
    private void _Update_Morale_Input()
    {
        // 항목별로 계산해 남겨 둡니다. UI가 "왜 사기가 떨어지는가"를
        // 이 내역에서 그대로 읽어 갑니다. (합계만 두면 그 화면을 못 만듭니다)
        Morale_Modifiers modifiers = army_Data.morale_Modifiers;
        modifiers.Clear();

        // 1. 사상자: 손실률이 클수록 크게 떨어집니다. 가장 지배적인 요인입니다.
        if (army_Data.unit_Num_Max > 0)
        {
            float lossRate = 1.0f - ((float)army_Data.unit_Num / army_Data.unit_Num_Max);
            if (lossRate < 0.0f) lossRate = 0.0f;
            modifiers.casualties = -lossRate * Constant.morale_Penalty_Casualty;
        }

        // 2. 포위: 나를 상대로 교전 중인 적 부대가 둘 이상이면 급격히 흔들립니다.
        //    동시에 교전 중인 적의 총 인원도 세어 수적 열세 판정에 씁니다.
        int enemyArmies = 0;
        int enemyUnits = 0;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            Army other = army_Detected[i].army;
            if (other == null) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            enemyArmies++;
            enemyUnits += other.units.Count;
        }

        if (enemyArmies > 1)
        {
            modifiers.surrounded = -(enemyArmies - 1) * Constant.morale_Penalty_Surrounded;
        }

        // 3. 수적 열세 / 우세: 눈앞의 적이 나보다 많으면 흔들리고, 적으면 버팁니다.
        if (enemyUnits > 0 && army_Data.unit_Num > 0)
        {
            float ratio = (float)enemyUnits / army_Data.unit_Num;

            if (ratio > 1.0f)
            {
                // 열세: 정해진 비율에서 페널티가 최대에 도달합니다.
                float t = (ratio - 1.0f)
                          / Mathf.Max(0.0001f, Constant.morale_Outnumbered_Full_Ratio - 1.0f);
                if (t > 1.0f) t = 1.0f;

                modifiers.outnumbered = -t * Constant.morale_Penalty_Outnumbered;
            }
            else
            {
                // 우세: 이기고 있다는 실감이 부대를 버티게 합니다.
                float t = 1.0f - ratio;
                modifiers.winning = t * Constant.morale_Bonus_Winning;
            }
        }

        // 4. 피로: 지친 병사는 먼저 무너집니다.
        modifiers.fatigue = -army_Data.GetFatigueMoralePenalty();

        // 5. 지휘: 깃발을 든 유닛이 살아 있으면 부대가 결속을 유지합니다.
        if (unit_Bearing_Flag != null && !unit_Bearing_Flag.IsDead())
        {
            modifiers.flag = Constant.morale_Bonus_Flag;
        }

        // 6. 지형: 고지를 점하면 사기가 오르고, 올려다보는 쪽은 떨어집니다.
        modifiers.terrain = army_Data.GetHighGroundMorale();

        // 6-1. 지휘: 장군이 가까이 있으면 부대가 버팁니다.
        modifiers.general = Get_General_Aura();

        // 7. 연쇄 붕괴: 옆의 아군이 무너지면 이쪽도 흔들립니다.
        //    (GameEvents.OnArmyRouted 구독으로 누적된 값을 소비합니다)
        modifiers.alliedRouting = -alliedRoutPenalty;

        // 충격은 별도 필드로 관리되므로 표시용으로만 옮겨 담습니다.
        modifiers.shock = -army_Data.morale_Shock;

        army_Data.morale_Modifiers = modifiers;

        // 충격을 뺀 나머지 항목의 합이 목표 사기입니다.
        // (충격은 _Update_Morale에서 별도로 차감되므로 여기서 이중 적용하면 안 됩니다)
        float target = Constant.morale_Max + modifiers.Sum() - modifiers.shock;

        if (target > Constant.morale_Max) target = Constant.morale_Max;
        if (target < 0.0f) target = 0.0f;

        army_Data.morale_Target = target;

        // 5. 패주 방향: 교전 상대의 반대편으로 달아납니다.
        if (targetArmy != null)
        {
            Vector3 away = GetPosition() - targetArmy.GetPosition();
            away.y = 0.0f;
            if (away.sqrMagnitude > 0.0001f) army_Data.escapeDirection = away.normalized;
        }

        // 연쇄 붕괴 페널티는 사기 충격과 같은 속도로 사그라듭니다.
        // 옆 부대가 무너진 충격이 영원히 남으면 재결집이 불가능해집니다.
        if (alliedRoutPenalty > 0.0f)
        {
            alliedRoutPenalty -= Constant.morale_Shock_Recover * Constant.deltaTime;
            if (alliedRoutPenalty < 0.0f) alliedRoutPenalty = 0.0f;
        }

        // 6. 붕괴/재결집 알림 (Job이 세운 1회성 플래그를 소비합니다)
        if (army_Data.broutedThisTick)
        {
            army_Data.broutedThisTick = false;
            On_Rout();
        }

        if (army_Data.bshatteredThisTick)
        {
            army_Data.bshatteredThisTick = false;
            GameEvents.RaiseArmyShattered(this);
        }

        if (army_Data.bralliedThisTick)
        {
            army_Data.bralliedThisTick = false;
            GameEvents.RaiseArmyRallied(this);
        }
    }

    /// <summary>
    /// 부대가 붕괴했을 때의 처리입니다. 교전을 끊고 모든 유닛의 목표를 해제합니다.
    /// </summary>
    private void On_Rout()
    {
        targetArmy = null;
        army_Data.e_Army_Fight = E_Army_Fight.Non;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].unit_Data.Lose_Target();
        }

        GameEvents.RaiseArmyRouted(this);
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
                Army killerArmy = Find_Army_Of_Unit(unit.unit_Data.killerNum);
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

    /// <summary>
    /// 주어진 유닛 고유 번호를 가진 유닛이 속한 부대를 찾습니다.
    /// 교전 상대(targetArmy)와 탐지된 부대들만 조회합니다.
    /// </summary>
    /// <param name="unitNum">찾을 유닛의 고유 번호입니다.</param>
    /// <returns>해당 부대, 찾지 못하면 null입니다.</returns>
    private Army Find_Army_Of_Unit(int unitNum)
    {
        if (unitNum == int.MaxValue) return null;

        if (Contains_Unit(targetArmy, unitNum)) return targetArmy;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (Contains_Unit(army_Detected[i].army, unitNum))
                return army_Detected[i].army;
        }

        return null;
    }

    /// <summary>
    /// 특정 부대가 주어진 고유 번호의 유닛을 보유하고 있는지 확인합니다.
    /// </summary>
    private static bool Contains_Unit(Army army, int unitNum)
    {
        if (army == null) return false;

        for (int i = 0; i < army.units.Count; i++)
        {
            if (army.units[i] == null) continue;
            if (army.units[i].unit_Data.num == unitNum) return true;
        }

        return false;
    }

    /// <summary>
    /// 부대 데이터를 업데이트합니다.
    /// </summary>
    public void _Upadate_Data()
    {
        Vector3 newPosition = formation_Move_Transform.position;

        // 실측 이동 속도입니다. 돌격 충격량이 이 값에 비례합니다.
        Vector3 delta = newPosition - army_Data.position;
        delta.y = 0.0f;
        army_Data.currentSpeed = delta.magnitude / Constant.deltaTime;

        army_Data.position = newPosition;
        army_Data.rotation = formation_Move_Transform.rotation;
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
    }

    /// <summary>
    /// 유닛의 상태를 업데이트합니다.
    /// </summary>
    /// <param name="unitDataMap">모든 유닛의 데이터를 담고 있는 해시맵입니다.</param>
    public void _Update_Unit(NativeHashMap<EntityId, Unit_Data> unitDataMap)
    {
        if (units.Count == 0) return;

        // 1. 레이캐스트 명령과 결과를 담을 NativeArray 준비
        var commands = new NativeArray<RaycastCommand>(units.Count, Allocator.TempJob);
        var results = new NativeArray<RaycastHit>(units.Count, Allocator.TempJob);

        // 2. 각 유닛에 대한 레이캐스트 명령 생성
        //    LayerMask.GetMask는 문자열 조회이므로 루프 밖에서 한 번만 구합니다.
        //    (기존에는 '유닛 수 x 매 틱'으로 호출되고 있었습니다)
        int layerMask = unitLayerMask;
        const float maxDistance = Constant.distance_Front_Block_Ray;

        for (int i = 0; i < units.Count; i++)
        {
            Vector3 origin = units[i].transform.position;
            Vector3 direction = units[i].transform.forward;

            commands[i] = new RaycastCommand(origin, direction, maxDistance, layerMask);
        }

        // 3. 레이캐스트 배치 잡(Batch Job) 스케줄링
        JobHandle raycastHandle = RaycastCommand.ScheduleBatch(commands, results, 1);

        // 4. Unit_Job에 필요한 데이터 전달
        Unit_Job unit_Job = new Unit_Job();
        unit_Job.unit_Datas = unit_Datas;
        unit_Job.raycastHits = results;
        unit_Job.unitDataMap = unitDataMap;
        unit_Job.armyData = army_Data;

        // raycastHandle이 완료된 후에 unit_Job을 실행하도록 의존성 설정
        JobHandle unitJobHandle = unit_Job.Schedule(units.Count, 1, raycastHandle);

        var unit_Animation_Datas = new NativeArray<Unit_Animation_Data>(units.Count, Allocator.TempJob);

        for (int i = 0; i < units.Count; i++)
        {
            unit_Animation_Datas[i] = units[i].unit_Animation.unit_Animation_Data;
        }

        Unit_Animation_Job unit_Animation_Job;
        unit_Animation_Job = new Unit_Animation_Job();
        unit_Animation_Job.unit_Datas = unit_Datas;
        unit_Animation_Job.unit_Animation_Datas = unit_Animation_Datas;
        unit_Animation_Job.cam_Position = Main_Camera.GetTransform().position;
        unit_Animation_Job.cam_Rotation = Main_Camera.GetTransform().rotation;

        // 5. 애니메이션 잡은 Unit_Job이 끝난 후에 실행되도록 의존성 체인 연결
        JobHandle animationJobHandle = unit_Animation_Job.Schedule(units.Count, 1, unitJobHandle);

        // 6. 모든 잡(레이캐스트 -> 유닛 -> 애니메이션)이 끝날 때까지 대기
        animationJobHandle.Complete();

        // 7. 결과 반영 및 메모리 해제
        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Animation.unit_Animation_Data = unit_Animation_Datas[i];
        }

        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Animation._Update();
        }

        commands.Dispose();
        results.Dispose();
        unit_Animation_Datas.Dispose();
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

        for (int i = 0; i < allArmies.Count; i++)
        {
            Army other = allArmies[i];
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
    /// 지형 상태(고지 우위, 경사)를 갱신합니다.
    ///
    /// 고지를 잡는 것은 토탈워 배치의 첫 번째 원칙입니다.
    /// 위에서 내려치는 쪽은 더 잘 맞히고 사기가 오르며 화살이 멀리 날아가고,
    /// 오르막을 오르는 쪽은 느려집니다.
    /// </summary>
    private void _Update_Terrain()
    {
        Vector3 myPosition = formation_Move_Transform.position;

        // 1. 고지 우위: 교전 상대와의 높이 차로 계산합니다.
        //    상대가 없으면 우위도 열세도 없습니다.
        float rate = 0.0f;

        if (targetArmy != null && targetArmy.units.Count > 0)
        {
            float heightDelta = myPosition.y - targetArmy.formation_Move_Transform.position.y;

            rate = heightDelta / Constant.terrain_Height_Full;
            if (rate < -1.0f) rate = -1.0f;
            if (rate > 1.0f) rate = 1.0f;
        }

        army_Data.highGroundRate = rate;

        // 2. 경사: 발밑 지면의 기울기를 재서 이동 속도에 반영합니다.
        //    레이캐스트 한 번이면 충분하므로 부대 기준점에서만 봅니다.
        //    (유닛마다 쏘면 부대 수 x 인원만큼 늘어나 낭비가 큽니다)
        if (Physics.Raycast(myPosition + Vector3.up * 2.0f, Vector3.down,
                            out RaycastHit hit, 10.0f, groundLayerMask))
        {
            army_Data.slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        }
        else
        {
            army_Data.slopeAngle = 0.0f;
        }
    }

    /// <summary>"Ground" 레이어 마스크를 캐시한 값입니다.</summary>
    private static int groundLayerMaskCache = -1;

    /// <summary>지면 레이어 마스크입니다. 최초 접근 시 한 번만 조회합니다.</summary>
    private static int groundLayerMask
    {
        get
        {
            if (groundLayerMaskCache == -1)
            {
                groundLayerMaskCache = LayerMask.GetMask("Ground");

                // "Ground" 레이어가 없는 프로젝트에서는 모든 레이어를 봅니다.
                // 그래야 경사 계산이 조용히 0으로 굳지 않습니다.
                if (groundLayerMaskCache == 0) groundLayerMaskCache = ~0;
            }

            return groundLayerMaskCache;
        }
    }

    /// <summary>
    /// 사거리 안의 적 부대를 '접촉 없이' 탐지합니다.
    ///
    /// 왜 필요한가:
    /// 기존 탐지(army_Detected)는 물리 충돌로만 채워졌습니다.
    /// 즉 몸이 닿아야 적으로 인식하므로, 원거리 부대는 구조적으로
    /// 사거리 밖의 적을 공격할 수 없었습니다. 이건 미구현이 아니라
    /// 아키텍처 차원의 차단이었습니다.
    ///
    /// 여기서 시야 기반 탐지를 더해 그 벽을 걷어냅니다.
    /// 충돌 기반 탐지(Add_Army_Detected)는 그대로 유지되며,
    /// 이 함수는 '아직 닿지 않은' 적을 추가로 등록할 뿐입니다.
    /// </summary>
    private void _Update_Detection()
    {
        // 이 부대가 실제로 닿을 수 있는 최대 거리입니다.
        float reach = army_Data.GetMeleeRange();

        if (army_Data.GetE_Unit_AttackType() == E_Unit_AttackType.Range
            || army_Data.IsRangeAttackAble())
        {
            float rangeRange = army_Data.GetEffectiveRangeRange();
            if (rangeRange > reach) reach = rangeRange;
        }

        // 근접 전용 부대는 시야 탐지가 필요 없습니다.
        // 어차피 닿아야 싸우므로 충돌 기반 탐지로 충분합니다.
        if (reach <= army_Data.GetMeleeRange()) return;

        float reachSqr = reach * reach;
        Vector3 myPosition = GetPosition();

        for (int i = 0; i < allArmies.Count; i++)
        {
            Army other = allArmies[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            Vector3 to = other.GetPosition() - myPosition;
            to.y = 0.0f;

            if (to.sqrMagnitude > reachSqr)
            {
                // 사거리를 벗어났으면 시야 탐지분을 거둬들입니다.
                Remove_Army_Sighted(other);
                continue;
            }

            Add_Army_Sighted(other);
        }
    }

    /// <summary>
    /// 시야로 탐지한 부대를 등록합니다.
    /// 충돌 탐지와 달리 카운트를 누적하지 않고 '있다/없다'만 표시합니다.
    /// </summary>
    private void Add_Army_Sighted(Army army)
    {
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].bsighted = true;
                return;
            }
        }

        Army_Count count = new Army_Count(army, 0);
        count.bsighted = true;
        army_Detected.Add(count);
    }

    /// <summary>
    /// 시야에서 벗어난 부대의 탐지 표시를 지웁니다.
    /// 물리 접촉 카운트가 남아 있으면 항목 자체는 유지합니다.
    /// </summary>
    private void Remove_Army_Sighted(Army army)
    {
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army != army) continue;

            army_Detected[i].bsighted = false;

            // 접촉 카운트도 없으면 더 이상 탐지 대상이 아닙니다.
            if (army_Detected[i].num <= 0)
            {
                army_Detected.RemoveAt(i);
            }
            return;
        }
    }

    /// <summary>
    /// 탐지된 부대 리스트에 새로운 부대를 추가합니다.
    /// </summary>
    /// <param name="army">탐지된 부대 인스턴스입니다.</param>
    public void Add_Army_Detected(Army army)
    {
        if (this == army) return;
        if (army.army_Data.bplayer == army_Data.bplayer) return;

        bool badd = false;
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].num = army_Detected[i].num + 1;
                badd = true;
            }
        }

        if (!badd)
        {
            army_Detected.Add(new Army_Count(army, 1));
        }
    }

    /// <summary>
    /// 탐지된 부대 리스트에서 부대를 제거합니다.
    /// </summary>
    /// <param name="army">제거할 부대 인스턴스입니다.</param>
    public void Remove_Army_Detected(Army army)
    {
        if (this == army) return;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].num = army_Detected[i].num - 1;

                // 아직 시야 안에 있으면 항목을 지우지 않습니다.
                // 지워 버리면 원거리 부대가 '떨어지는 순간' 표적을 놓칩니다.
                if (army_Detected[i].num <= 0 && !army_Detected[i].bsighted)
                {
                    army_Detected.RemoveAt(i);
                }
            }
        }
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
