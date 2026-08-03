using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대의 "돌격 충돌 정산" 책임을 담당하는 부분 클래스입니다.
///
/// 담는 것: 돌격 충격의 사기 반영, 창벽 반사, 대형 유닛의 충돌 공격,
///          그리고 그 순간을 눈에 보이게 하는 점멸 요청.
///
/// 이 영역이 따로 있는 이유:
/// 돌격은 이 게임에서 '한순간에 전황을 바꾸는' 유일한 사건입니다.
/// 피해, 사기, 넉백, 시각 효과가 한 시점에 함께 터지므로 규칙이 몰려 있고,
/// 밸런싱할 때도 이 묶음을 통째로 보게 됩니다.
///
/// 결정론 주의:
/// 여기서 상대 부대의 상태를 직접 바꾸지 않습니다. Apply_Morale_Shock과
/// Request_Charge_Flash로 '예약'만 하고, 실제 반영은 모든 부대의 틱이 끝난 뒤
/// Commit_Pending_Morale_Shock()이 합니다. 그래야 부대 갱신 순서가
/// 전투 결과를 바꾸지 않습니다.
/// </summary>
partial class Army
{

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

            // 대형 유닛은 부딪히는 것만으로 피해를 줍니다. (충돌 공격)
            if (units[i].unit_Data.bcollisionAttack)
            {
                units[i].unit_Data.bcollisionAttack = false;
                Apply_Collision_Attack(units[i]);
            }

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
            targetArmy.Request_Charge_Flash(GetPosition(), momentum);
            return;
        }

        targetArmy.Apply_Morale_Shock(shock);

        // 충격을 받은 쪽에서도 접촉면의 유닛들이 번쩍이도록 합니다.
        // 어느 방향에서 돌격이 들어왔는지 눈으로 알 수 있게 하기 위함입니다.
        targetArmy.Request_Charge_Flash(GetPosition(), momentum);
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
    /// 충돌 공격을 적용합니다. 대형 유닛이 '몸으로' 들이받는 피해입니다.
    ///
    /// 무기 공격과 별개인 이유:
    /// 전속력으로 달려든 말은 창을 쓰기 전에 부딪히는 것만으로 사람을 넘어뜨립니다.
    /// 이 처리가 있어야 기병 돌파가 보병 대열을 실제로 흩뜨립니다.
    ///
    /// 피해량은 질량비와 속도에 비례합니다.
    /// 무거운 쪽이 가벼운 쪽을 밀어내는 것이지 그 반대는 성립하지 않습니다.
    /// </summary>
    private void Apply_Collision_Attack(Unit attacker)
    {
        if (targetArmy == null) return;
        if (attacker == null) return;

        float myMass = army_Data.GetMass();
        float otherMass = targetArmy.army_Data.GetMass();
        if (otherMass <= 0.0f) otherMass = 1.0f;

        // 질량이 앞설수록 강하게 들이받습니다. 동급이면 거의 효과가 없습니다.
        float massRatio = myMass / otherMass;
        if (massRatio < 1.0f) return;

        float t = (massRatio - 1.0f)
                  / Mathf.Max(0.0001f, Constant.collision_Mass_Full_Ratio - 1.0f);
        if (t > 1.0f) t = 1.0f;

        float power = t * attacker.unit_Data.chargeImpactPower;
        if (power <= 0.0f) return;

        float damage = Constant.collision_Damage_Base * power;
        float impulse = Constant.collision_Knockback_Impulse * power;

        // 충돌 지점 주변의 적들이 함께 밀려납니다.
        // 말 한 마리가 정확히 한 명만 치고 지나가지는 않습니다.
        float radius = army_Data.GetRadius() + targetArmy.army_Data.GetRadius();
        if (radius <= 0.0f) radius = 1.0f;

        float radiusSqr = radius * radius;
        Vector3 origin = attacker.transform.position;
        Vector3 forward = attacker.transform.forward;

        List<Unit> victims = targetArmy.units;

        for (int i = 0; i < victims.Count; i++)
        {
            Unit victim = victims[i];
            if (victim == null) continue;
            if (victim.IsDead()) continue;

            Vector3 to = victim.transform.position - origin;
            to.y = 0.0f;

            if (to.sqrMagnitude > radiusSqr) continue;

            // 앞으로 밀어냅니다. 겹쳐 있으면 진행 방향을 씁니다.
            Vector3 push = to.sqrMagnitude > 0.000001f ? to.normalized : forward;

            victim.Take_Collision_Hit(damage, push, impulse,
                                      attacker.unit_Data.num, attacker.unit_Data.armyIndex);
        }
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
        float radius = army_Data.GetInterval() * Constant.charge_Flash_Radius_Rate;
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

}
