using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 부대를 자동으로 지휘하는 전투 AI입니다.
///
/// 왜 필요한가:
/// 지금까지 적 부대는 플레이어가 접촉할 때까지 가만히 서 있었습니다.
/// Army.allArmies와 Find_Nearest_Enemy_Army()는 준비되어 있었지만
/// 호출하는 주체가 없었기 때문입니다. 상대가 움직이지 않으면
/// 측면 기동도 포위도 성립하지 않아 전술 자체가 무의미해집니다.
///
/// 설계 방침:
/// 부대 단위 명령만 내립니다. 유닛 조작은 일절 하지 않습니다.
/// AI는 플레이어가 쓰는 것과 똑같은 입구(Move_Start / Set_Stance)만
/// 사용하므로, 시뮬레이션 규칙을 우회하거나 반칙하지 않습니다.
///
/// 판단 주기:
/// 매 프레임 재판단하면 부대가 갈팡질팡합니다.
/// 일정 간격으로만 명령을 갱신해 '결심하고 밀어붙이는' 모습을 만듭니다.
/// </summary>
public class Battle_AI : MonoBehaviour
{
    /// <summary>지휘할 부대 목록을 읽어 올 컨트롤러입니다. 비워 두면 전역 목록을 씁니다.</summary>
    [Header("연결")]
    public Controller controller;

    /// <summary>전투 단계를 볼 배틀 매니저입니다. 비워 두면 항상 동작합니다.</summary>
    [Tooltip("전투 단계를 볼 배틀 매니저입니다. 비워 두면 항상 동작합니다.")]
    public Battle_Manager battle_Manager;

    /// <summary>이 AI가 지휘할 진영입니다. 보통 적군(false)입니다.</summary>
    [Header("동작")]
    [Tooltip("AI가 지휘할 진영입니다. 보통 적군(false)입니다.")]
    public bool bcontrolPlayerSide = false;

    /// <summary>
    /// 명령을 다시 내리는 간격(초)입니다.
    /// 짧으면 부대가 갈팡질팡하고, 길면 상황 변화에 늦게 반응합니다.
    /// </summary>
    [Tooltip("명령을 다시 내리는 간격(초)입니다. 짧으면 부대가 갈팡질팡합니다.")]
    public float decisionInterval = 2.0f;

    /// <summary>이 거리 안에 들어오면 교전으로 보고 더 이상 재배치하지 않습니다.</summary>
    [Tooltip("이 거리 안에 들어오면 교전으로 보고 더 이상 재배치하지 않습니다.")]
    public float engageDistance = 6.0f;

    /// <summary>다음 판단을 내릴 시뮬레이션 틱입니다.</summary>
    private uint nextDecisionTick;

    /// <summary>
    /// 판단 주기를 시뮬레이션 틱으로 환산한 값입니다.
    ///
    /// 최소 1틱을 보장합니다. 0이 되면 매 틱 재판단해 부대가 갈팡질팡합니다.
    /// </summary>
    private uint Decision_Interval_Ticks()
    {
        int ticks = Mathf.RoundToInt(decisionInterval / Constant.deltaTime);
        return (uint)Mathf.Max(1, ticks);
    }

    /// <summary>
    /// AI 판단을 갱신합니다.
    ///
    /// 왜 Update가 아니라 FixedUpdate인가:
    /// 예전에는 Update에서 Time.time으로 주기를 쟀습니다. 그런데 Time.time은
    /// 실제 경과 시간이고 Update 호출 빈도는 프레임 레이트에 좌우되므로,
    /// '몇 번째 시뮬레이션 틱에 명령이 내려가는가'가 실행할 때마다 달라졌습니다.
    ///
    /// 명령 시점이 한 틱만 어긋나도 그 뒤의 전투는 통째로 달라집니다.
    /// 실제로 같은 시드로 두 번 돌렸을 때 사망자가 438명과 554명으로
    /// 갈렸고, 원인이 바로 이것이었습니다.
    ///
    /// 시뮬레이션 틱을 기준으로 삼으면 프레임 레이트와 무관하게
    /// 언제나 같은 틱에 같은 명령이 내려가므로 전투가 재현됩니다.
    /// </summary>
    private void FixedUpdate()
    {
        // 배치 단계에서는 움직이지 않습니다.
        if (battle_Manager != null
            && battle_Manager.phase != E_Battle_Phase.Fighting)
        {
            return;
        }

        uint tick = Simulation_Clock.tick;
        if (tick < nextDecisionTick) return;

        nextDecisionTick = tick + Decision_Interval_Ticks();

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.AI_Decision);
        _Update_Decisions();
        Tick_Profiler.End_Sub();
    }

    /// <summary>지휘 대상 후보 부대 목록을 반환합니다.</summary>
    /// <returns>컨트롤러가 있으면 그 목록, 없으면 전역 목록입니다.</returns>
    private List<Army> Get_Armies()
    {
        return controller != null ? controller.armies : Army.allArmies;
    }

    /// <summary>지휘 대상 부대들에 대해 각각 판단을 내립니다.</summary>
    private void _Update_Decisions()
    {
        List<Army> armies = Get_Armies();
        if (armies == null) return;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;
            if (army.army_Data.bplayer != bcontrolPlayerSide) continue;

            // 무너진 부대는 명령을 듣지 않습니다. 알아서 달아납니다.
            if (army.army_Data.IsBroken()) continue;

            // 판단 횟수를 셉니다.
            // '유닛이 아니라 부대만 판단한다'는 주장은 이 값과 유닛 수를
            // 비교해야 확인됩니다. 시간만 봐서는 알 수 없습니다.
            Tick_Profiler.Count_AI_Decision();

            Decide(army);
        }
    }

    /// <summary>부대 하나의 행동을 결정합니다.</summary>
    private void Decide(Army army)
    {
        Army enemy = army.Find_Nearest_Enemy_Army(out float distance);
        if (enemy == null) return;

        // 병종에 따라 태세와 거리 유지 방식이 달라집니다.
        E_Unit_Class unitClass = army.army_Data.GetE_Unit_Class();

        switch (unitClass)
        {
            case E_Unit_Class.Archer:
                Decide_Archer(army, enemy, distance);
                break;

            case E_Unit_Class.Spear:
                Decide_Spear(army, enemy, distance);
                break;

            case E_Unit_Class.Cavalry:
                Decide_Cavalry(army, enemy, distance);
                break;

            default:
                Decide_Infantry(army, enemy, distance);
                break;
        }
    }

    /// <summary>
    /// 궁병: 산개 태세로 거리를 유지하며 쏩니다.
    /// 탄약이 떨어지면 근접에 합류합니다.
    /// </summary>
    private void Decide_Archer(Army army, Army enemy, float distance)
    {
        army.Set_Stance(E_Army_Stance.Skirmish);

        // 고지 보정이 반영된 사거리입니다. 높은 곳에서는 더 멀리 쏩니다.
        float range = army.army_Data.GetEffectiveRangeRange();

        // 사거리 안이면 제자리에서 사격합니다. 자동 후퇴는 태세가 처리합니다.
        if (distance <= range) return;

        // 사거리 밖이면 쏠 수 있는 위치까지만 다가갑니다.
        //
        // 0.8을 곱해 사거리 경계보다 안쪽에 섭니다. 경계에 딱 맞춰 서면
        // 적이 조금만 물러나도 다시 사거리 밖이 되어 전진과 정지를 반복합니다.
        Advance_To(army, enemy, range * 0.8f);
    }

    /// <summary>
    /// 창병: 적 기병이 있으면 창벽을 세우고 버팁니다.
    /// 기병이 없으면 평범하게 전진합니다.
    /// </summary>
    private void Decide_Spear(Army army, Army enemy, float distance)
    {
        bool bcavalryNear = Is_Cavalry_Near(army);

        // 기병이 근처에 있으면 창벽을 세우고 그 자리에서 버팁니다.
        // 창벽은 정지 상태에서만 돌격을 반사하므로 전진하면 의미가 없습니다.
        if (bcavalryNear)
        {
            // 창벽은 정지 상태에서만 돌격을 반사합니다. 그러니 멈춰 섭니다.
            army.Set_Stance(E_Army_Stance.SpearWall);
            return;
        }

        army.Set_Stance(E_Army_Stance.Line);

        if (distance > engageDistance) Advance_To(army, enemy, engageDistance * 0.5f);
    }

    /// <summary>
    /// 기병: 창병 정면을 피하고 측후방을 노립니다.
    /// 창벽에 정면으로 뛰어들면 돌격이 반사되어 손해만 봅니다.
    /// </summary>
    private void Decide_Cavalry(Army army, Army enemy, float distance)
    {
        army.Set_Stance(E_Army_Stance.Loose);

        // 창병이나 창벽을 세운 상대에게는 정면으로 가지 않습니다.
        bool bavoidFront = enemy.army_Data.GetE_Unit_Class() == E_Unit_Class.Spear
                           || enemy.army_Data.e_Army_Stance == E_Army_Stance.SpearWall;

        // 더 나은 표적이 있으면 그쪽을 칩니다. (궁병이 최우선)
        Army softer = Find_Soft_Target(army);
        if (softer != null)
        {
            enemy = softer;
            bavoidFront = false;
        }

        if (bavoidFront)
        {
            Flank_To(army, enemy);
            return;
        }

        if (distance > engageDistance) Advance_To(army, enemy, engageDistance * 0.5f);
    }

    /// <summary>보병: 전열을 유지하며 곧장 전진합니다.</summary>
    private void Decide_Infantry(Army army, Army enemy, float distance)
    {
        army.Set_Stance(E_Army_Stance.Line);

        if (distance > engageDistance) Advance_To(army, enemy, engageDistance * 0.5f);
    }

    /// <summary>
    /// 적 쪽으로 전진합니다. 목표 지점은 상대 앞 stopDistance 지점입니다.
    ///
    /// 전열 방향은 '적을 향한 정면'에 수직이 되도록 잡습니다.
    /// 그래야 대열이 옆으로 펼쳐진 채로 부딪힙니다.
    /// </summary>
    private void Advance_To(Army army, Army enemy, float stopDistance)
    {
        Vector3 from = army.GetPosition();
        Vector3 to = enemy.GetPosition();

        Vector3 forward = to - from;
        forward.y = 0.0f;

        if (forward.sqrMagnitude < 0.0001f) return;

        float distance = forward.magnitude;
        forward /= distance;

        // 상대 앞에서 멈춥니다.
        float travel = distance - stopDistance;
        if (travel < 0.0f) travel = 0.0f;

        Vector3 center = from + forward * travel;

        Issue_Line_Order(army, center, forward);
    }

    /// <summary>
    /// 측면으로 우회합니다. 정면을 피해 옆으로 붙는 기동입니다.
    /// </summary>
    private void Flank_To(Army army, Army enemy)
    {
        Vector3 from = army.GetPosition();
        Vector3 to = enemy.GetPosition();

        Vector3 forward = to - from;
        forward.y = 0.0f;

        if (forward.sqrMagnitude < 0.0001f) return;

        forward = forward.normalized;

        // 적을 향한 방향에 수직인 축입니다. 이 축을 따라 옆으로 돕니다.
        Vector3 side = Vector3.Cross(Vector3.up, forward);

        Vector3 enemyForward = enemy.formation_Move_Transform.forward;
        enemyForward.y = 0.0f;

        // 상대 정면의 반대쪽(측후방)을 노립니다.
        //
        // side와 적의 정면이 같은 쪽을 가리키면(내적 > 0) 그 방향은
        // 적의 앞쪽입니다. 부호를 뒤집어야 뒤로 돌아가게 됩니다.
        // 이 게임에서 측후방 공격은 방어력과 방패를 무력화하므로
        // 기병이 정면을 피하는 것 자체가 큰 이득입니다.
        float sign = Vector3.Dot(side, enemyForward) > 0.0f ? 1.0f : -1.0f;

        float flankDistance = engageDistance * 2.0f;
        Vector3 center = to + side * sign * flankDistance;

        Vector3 approach = (center - from);
        approach.y = 0.0f;

        if (approach.sqrMagnitude < 0.0001f) return;

        Issue_Line_Order(army, center, approach.normalized);
    }

    /// <summary>
    /// 가장 무른 표적(궁병 등)을 찾습니다.
    /// 기병은 대열이 단단한 정면 대신 이런 상대를 노려야 합니다.
    /// </summary>
    private Army Find_Soft_Target(Army army)
    {
        Army best = null;
        float bestSqr = float.MaxValue;

        Vector3 myPosition = army.GetPosition();

        for (int i = 0; i < Army.allArmies.Count; i++)
        {
            Army other = Army.allArmies[i];
            if (other == null) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army.army_Data.bplayer) continue;

            // 궁병만 노립니다. 근접 대열은 정면으로 치지 않습니다.
            if (other.army_Data.GetE_Unit_Class() != E_Unit_Class.Archer) continue;

            Vector3 to = other.GetPosition() - myPosition;
            to.y = 0.0f;

            float sqr = to.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = other;
            }
        }

        return best;
    }

    /// <summary>
    /// 부대에 전열 이동 명령을 내립니다.
    ///
    /// 플레이어가 우클릭 드래그로 내리는 것과 같은 입구를 씁니다.
    /// 전열축은 진행 방향에 수직이어야 대열이 옆으로 펼쳐집니다.
    /// </summary>
    private void Issue_Line_Order(Army army, Vector3 center, Vector3 forward)
    {
        // 전열축은 진행 방향에 수직입니다.
        // 그래야 대열이 옆으로 펼쳐진 채 적과 부딪힙니다.
        // 진행 방향과 같게 두면 부대가 한 줄로 늘어서 각개격파당합니다.
        Vector3 lineDirection = Vector3.Cross(Vector3.up, forward);
        if (lineDirection.sqrMagnitude < 0.0001f) return;

        lineDirection = lineDirection.normalized;

        // 진형 좌표 규약은 '대열의 한가운데'입니다. (Army_Formation.cs 참조)
        // 예전에는 여기서 왼쪽 끝(center - 절반폭)으로 밀어 넘겼는데,
        // 규약이 중심으로 통일된 뒤에는 그 보정이 곧 오차가 됩니다.
        float length = army.GetFormation_Length();

        army.Move_Start(length, lineDirection, center);
    }

    /// <summary>가까이에 적 기병이 있는지 봅니다. 창벽을 세울지 결정하는 근거입니다.</summary>
    private bool Is_Cavalry_Near(Army army)
    {
        Vector3 myPosition = army.GetPosition();

        // 기병은 빠르므로 넉넉히 봅니다. 붙고 나서 세우면 늦습니다.
        //
        // 창벽으로 바꾸는 데도 대열이 정비될 시간이 필요하므로,
        // 교전 거리의 네 배쯤에서 미리 감지해야 제때 세울 수 있습니다.
        float radius = engageDistance * 4.0f;
        float radiusSqr = radius * radius;

        for (int i = 0; i < Army.allArmies.Count; i++)
        {
            Army other = Army.allArmies[i];
            if (other == null) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army.army_Data.bplayer) continue;
            if (!other.army_Data.IsLarge()) continue;

            Vector3 to = other.GetPosition() - myPosition;
            to.y = 0.0f;

            if (to.sqrMagnitude <= radiusSqr) return true;
        }

        return false;
    }
}
