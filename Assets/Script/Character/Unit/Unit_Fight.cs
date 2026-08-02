using System;

using System.Collections.Generic;
using UnityEngine;

partial struct Unit_Data
{
    // Public methods
    /// <summary>유닛의 전투 상태를 업데이트합니다.</summary>
    public void _Update_Fight(in Army_Data armyData)
    {
        if (bhitTarget)
            bhitTarget = false;

        _Update_Charge(armyData);

        switch (e_Unit_Fight)
        {
            case E_Unit_Fight.Attack_Able:
                if (btarget)
                {
                    Attack_Start(armyData);
                }
                break;

            case E_Unit_Fight.Attack:
                Attack(armyData);
                break;

            case E_Unit_Fight.Attack_Disable:
                Attack_Disable();
                break;
        }
    }

    /// <summary>
    /// 이 적을 타겟으로 삼을 수 있는지 판정하고, 우선순위 점수를 반환합니다.
    /// 점수가 낮을수록 좋은 표적이며, 타겟 불가면 float.MaxValue를 돌려줍니다.
    ///
    /// 순수 최근접으로 고르면 옆이나 뒤의 적을 잡고 그쪽으로 이동하면서
    /// 자기 대열을 가로질러 버립니다. 그래서 '정면'에 가중치를 둡니다.
    /// (토탈워에서 병사는 자기 앞의 상대와 짝지어 싸웁니다)
    /// </summary>
    public float Get_Target_Score(in Unit_Data enemy, in Army_Data armyData)
    {
        if (enemy.bPlayer == bPlayer) return float.MaxValue;
        if (enemy.bdead) return float.MaxValue;

        Vector3 to = enemy.position - position;
        to.y = 0.0f;

        float distanceSqr = to.sqrMagnitude;

        // 사거리 밖이면 후보가 아닙니다.
        // 탄약이 떨어졌으면 원거리 사거리를 인정하지 않습니다. (근접만 가능)
        float reach = armyData.GetMeleeRange();
        if (Can_Shoot(armyData))
        {
            float rangeRange = armyData.GetEffectiveRangeRange();
            if (rangeRange > reach) reach = rangeRange;
        }

        if (distanceSqr > reach * reach) return float.MaxValue;

        float distance = Mathf.Sqrt(distanceSqr);

        // 정면일수록 낮은 점수를 줍니다.
        if (distance > 0.0001f)
        {
            Vector3 forward = rotation * Vector3.forward;
            float dot = Vector3.Dot(forward, to / distance);
            return distance + (1.0f - dot) * Constant.target_Front_Bias;
        }

        return distance;
    }

    /// <summary>
    /// 이미 잡고 있는 타겟의 위치/공격 타입을 갱신합니다.
    /// </summary>
    /// <returns>계속 유효한 타겟이면 true, 사거리를 벗어났으면 false입니다.</returns>
    public bool Refresh_Target(in Unit_Data enemy, in Army_Data armyData)
    {
        if (enemy.bdead) return false;

        unit_Target_Data.SetTarget(enemy);

        Vector3 to = enemy.position - position;
        to.y = 0.0f;
        float distanceSqr = to.sqrMagnitude;

        float meleeRange = armyData.GetMeleeRange();
        if (distanceSqr < meleeRange * meleeRange)
        {
            e_Unit_AttackType = E_Unit_AttackType.Melee;
            return true;
        }

        if (Can_Shoot(armyData))
        {
            float rangeRange = armyData.GetEffectiveRangeRange();
            if (distanceSqr < rangeRange * rangeRange)
            {
                e_Unit_AttackType = E_Unit_AttackType.Range;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 지금 사격할 수 있는지 판정합니다.
    /// 원거리 병종이어야 하고, 탄약이 남아 있어야 합니다.
    /// 부대 스탯의 ammunition이 0 이하이면 무한으로 취급합니다.
    /// </summary>
    public bool Can_Shoot(in Army_Data armyData)
    {
        if (armyData.GetE_Unit_AttackType() != E_Unit_AttackType.Range
            && !armyData.IsRangeAttackAble())
        {
            return false;
        }

        if (armyData.GetRangeRange() <= 0.0f) return false;

        // 탄약이 유한한데 다 썼으면 더 이상 쏘지 못합니다.
        if (armyData.IsAmmunitionLimited() && ammunition <= 0) return false;

        return true;
    }

    /// <summary>
    /// 새 타겟을 확정합니다. 후보 전체를 평가한 뒤 한 번만 호출해야 합니다.
    /// </summary>
    public void Set_Target(in Unit_Data enemy, in Army_Data armyData)
    {
        btarget = true;
        unit_Target_Data.SetTarget(enemy);

        Vector3 to = enemy.position - position;
        to.y = 0.0f;

        float meleeRange = armyData.GetMeleeRange();
        e_Unit_AttackType = to.sqrMagnitude < meleeRange * meleeRange
            ? E_Unit_AttackType.Melee
            : E_Unit_AttackType.Range;
    }

    /// <summary>공격을 시작합니다.</summary>
    public void Attack_Start(in Army_Data armyData)
    {
        switch (e_Unit_AttackType)
        {
            case E_Unit_AttackType.Melee:
                timer_AttackSpeed = new Timer(armyData.GetMeleeAttackSpeed());

                break;
            case E_Unit_AttackType.Range:
                timer_AttackSpeed = new Timer(armyData.GetRangeAttackSpeed());
                break;
        }

        e_Unit_Fight = E_Unit_Fight.Attack;
    }

    /// <summary>공격 딜레이 시간을 업데이트합니다.</summary>
    public void Attack_Disable()
    {
        timer_AttackDelay._Update();

        if (timer_AttackDelay.TryConsume())
        {
            e_Unit_Fight = E_Unit_Fight.Attack_Able;
        }
    }

    /// <summary>유닛이 피해를 입었을 때 HP를 감소시킵니다.</summary>
    public void GetDamage(float damage)
    {
        // 이미 사망한 유닛은 추가 피해를 받지 않습니다.
        if (bdead) return;

        // 음수 데미지(방어력이 공격력을 상회하는 경우)로 회복되지 않도록 하한을 둡니다.
        if (damage < 0.0f) damage = 0.0f;

        bgetDamage = true;

        HP -= damage;

        if (HP <= 0.0f)
        {
            HP = 0.0f;
            Dead();
        }
    }

    /// <summary>
    /// 돌격 상태와 보너스 감쇠를 갱신합니다.
    /// 돌격은 '충돌 순간'에 몰린 보너스이며, 접촉하는 즉시 난전으로 전환됩니다.
    /// </summary>
    private void _Update_Charge(in Army_Data armyData)
    {
        // 부대가 더 이상 돌격 중이 아니면 '먼저' 상태를 해제합니다.
        //
        // 순서 주의: 이 해제가 충돌 판정보다 뒤에 있으면,
        // 후퇴 명령 등으로 돌격이 취소된 바로 그 틱에 적과 스치기만 해도
        // 가짜 돌격 충격이 기록됩니다.
        if (bcharging && armyData.e_Army_Move != E_Army_Move.MoveCharge)
        {
            bcharging = false;
        }

        // 돌격 중 적과 맞닿으면 그 순간이 충격입니다.
        //
        // 접촉 판정은 두 가지를 모두 봅니다.
        //   1) benemyContact : 물리 충돌. 정확하지만 한 틱 늦습니다.
        //   2) 표적과의 거리 : 예측 판정. 물리보다 먼저 성립합니다.
        //
        // 2번이 없으면 물리 콜백을 기다리는 동안 이미 적 대열 안으로
        // 파고든 뒤에야 충격이 기록되어 두 대열이 뒤섞였습니다.
        bool bcontactByDistance = false;

        if (bcharging && btarget)
        {
            Vector3 toTarget = unit_Target_Data.position - position;
            toTarget.y = 0.0f;

            // 근접 사거리 안에 들어왔으면 이미 '닿은' 것으로 봅니다.
            float reach = armyData.GetMeleeRange();
            bcontactByDistance = toTarget.sqrMagnitude <= reach * reach;
        }

        if (bcharging && (benemyContact || bcontactByDistance))
        {
            bcharging = false;

            // 충격량은 '얼마나 빠르게 달려와 부딪혔는가'에 비례합니다.
            // 제자리에서 붙은 돌격은 위력이 거의 없습니다.
            float momentum = armyData.GetChargeMomentumRate();

            if (momentum < Constant.charge_Min_Speed_Rate)
            {
                // 속도가 붙지 않았으면 돌격으로 인정하지 않습니다.
                chargeBonus = 0.0f;
                return;
            }

            bchargeImpact = true;
            chargeImpactPower = momentum;
            chargeBonus = momentum;

            // 대형 유닛(기병 포함)은 몸으로도 들이받습니다.
            // 무기와 별개로 충돌 자체가 피해와 넉백을 만듭니다.
            if (armyData.IsLarge()) bcollisionAttack = true;

            timer_Charge.ReSetTimer();
            return;
        }

        // 보너스는 시간에 따라 선형으로 사라집니다.
        if (chargeBonus > 0.0f)
        {
            timer_Charge._Update();

            // 충돌 시점의 세기(chargeImpactPower)에서 시간에 따라 0으로 줄어듭니다.
            chargeBonus = chargeImpactPower * (1.0f - timer_Charge.GetRate());
            if (chargeBonus < 0.0f) chargeBonus = 0.0f;
        }
    }

    /// <summary>
    /// 공격 목표를 포기합니다. 부대가 붕괴해 패주할 때 사용합니다.
    /// </summary>
    public void Lose_Target()
    {
        btarget = false;
        bhitTarget = false;
        unit_Target_Data.RemoveTarget();
        e_Unit_Fight = E_Unit_Fight.Attack_Able;
    }

    /// <summary>유닛을 사망 상태로 전환합니다. 실제 오브젝트 제거는 Unit 계층에서 처리합니다.</summary>
    public void Dead()
    {
        if (bdead) return;

        bdead = true;

        // 사망 시 모든 전투/이동 상태를 정리합니다.
        btarget = false;
        bhitTarget = false;
        unit_Target_Data.RemoveTarget();

        e_Unit_Fight = E_Unit_Fight.Attack_Disable;

        Move_Cancel();
    }

    /// <summary>유닛이 피해를 입었을 때 HP를 감소시키고 피해 벡터를 설정합니다.</summary>
    /// <remarks>
    /// damageVector에는 '방향 x 확정 피해량'만 담습니다.
    /// 실제 밀리는 세기는 질량을 아는 물리 계층(Unit._Update_Fight)에서 결정합니다.
    ///
    /// 과거 버그: 여기서 클램프 전의 damage를 그대로 곱했습니다.
    /// 방어구가 공격력보다 높으면 damage가 음수가 되어
    ///   - 벡터 크기가 |음수| 만큼 커지고
    ///   - 부호가 뒤집혀 공격자 쪽으로 밀려나는
    /// 이중 오류가 났습니다. (예: -50 -> 50 m/s로 역방향 발사)
    /// </remarks>
    public void GetDamage(float damage, Vector3 damageVector)
    {
        if (bdead) return;

        // 반드시 클램프한 뒤에 벡터를 만들어야 합니다.
        if (damage < 0.0f) damage = 0.0f;

        this.damageVector = damageVector.normalized * damage;
        GetDamage(damage);
    }

    /// <summary>공격자 정보를 포함하여 피해를 적용합니다. 사망 시 킬 기여자를 기록합니다.</summary>
    public void GetDamage(float damage, Vector3 damageVector, int attackerNum)
    {
        if (bdead) return;

        GetDamage(damage, damageVector);

        // 이번 피해로 사망했다면 공격자를 킬 기여자로 기록합니다.
        if (bdead) killerNum = attackerNum;
    }

    // Private methods
    /// <summary>공격을 처리합니다.</summary>
    private void Attack(in Army_Data armyData)
    {
        timer_AttackSpeed._Update();

        if (timer_AttackSpeed.TryConsume())
        {
            Attack_End(armyData);
        }
    }

    /// <summary>공격을 종료하고 딜레이를 설정합니다.</summary>
    private void Attack_End(in Army_Data armyData)
    {
        timer_AttackDelay.ReSetTimer();

        if (e_Unit_AttackType == E_Unit_AttackType.Range)
        {
            // 원거리 타격은 사거리 안이면 성립합니다.
            float rangeRange = armyData.GetEffectiveRangeRange();

            if ((position - unit_Target_Data.position).sqrMagnitude
                < rangeRange * rangeRange)
            {
                bhitTarget = true;
                bfiredThisTick = true;

                // 화살 한 발을 소모합니다. 무한 탄약이면 줄이지 않습니다.
                if (armyData.IsAmmunitionLimited() && ammunition > 0) ammunition--;
            }
        }
        else
        {
            float meleeRange = armyData.GetMeleeRange();

            if ((position - unit_Target_Data.position).sqrMagnitude
                < meleeRange * meleeRange)
            {
                bhitTarget = true;
            }
        }

        e_Unit_Fight = E_Unit_Fight.Attack_Disable;
    }
}

partial class Unit
{
    // Private methods
    /// <summary>
    /// 원거리 발사의 시각 표현을 담당하는 렌더러입니다.
    /// 씬에 없으면 궤적만 안 그려질 뿐 전투는 정상 동작합니다.
    /// </summary>
    private static Projectile_Renderer projectileRenderer;

    /// <summary>투사체 렌더러를 찾아 캐시합니다. 없으면 null을 유지합니다.</summary>
    private static Projectile_Renderer Get_Projectile_Renderer()
    {
        // Unity의 == 오버로드 덕분에 파괴된 객체도 null로 판정됩니다.
        if (projectileRenderer == null)
        {
            projectileRenderer = FindAnyObjectByType<Projectile_Renderer>();
        }

        return projectileRenderer;
    }

    /// <summary>유닛의 전투 관련 로직을 업데이트합니다.</summary>
    private void _Update_Fight()
    {
        // 이번 틱에 근접 타격이 성립했으면 표적 쪽으로 짧게 내지릅니다.
        //
        // 매치드 컴뱃(두 병사를 짝지어 전용 애니메이션 재생)을 스프라이트로
        // 흉내 내는 것은 비용 대비 이득이 없습니다. 이 짧은 전진만으로도
        // 누가 누구를 치고 있는지 눈에 들어옵니다.
        if (unit_Data.bhitTarget
            && unit_Data.e_Unit_AttackType == E_Unit_AttackType.Melee
            && unit_Animation != null)
        {
            Vector3 toTarget = unit_Data.unit_Target_Data.position - transform.position;
            unit_Animation.Play_Attack_Lunge(toTarget);
        }

        // 이번 틱에 활을 쏘았으면 궤적을 하나 띄웁니다.
        // 피해 판정은 이미 Job에서 끝났으므로 이건 순수한 표현입니다.
        if (unit_Data.bfiredThisTick)
        {
            unit_Data.bfiredThisTick = false;

            Projectile_Renderer renderer = Get_Projectile_Renderer();
            if (renderer != null)
            {
                Vector3 from = transform.position + Vector3.up * 1.0f;
                Vector3 to = unit_Data.unit_Target_Data.position + Vector3.up * 0.8f;

                renderer.Fire(from, to);
            }
        }

        if (!unit_Data.bgetDamage) return;

        Vector3 damageVector = unit_Data.damageVector;
        float damage = damageVector.magnitude;

        unit_Data.bgetDamage = false;
        unit_Data.damageVector = new Vector3();

        if (damage > 0.0f)
        {
            // 피격 점멸: 대규모 전투에서 개별 타격이 묻히지 않도록 시각화합니다.
            if (unit_Animation != null) unit_Animation.Flash_Hit(damage);

            // 넉백은 '충격량'으로 가합니다.
            // ForceMode.Impulse는 내부적으로 질량으로 나누므로
            // 무거운 유닛일수록 덜 밀립니다. (기존에는 질량을 무시하고
            // 피해량을 그대로 m/s 속도에 더해 과하게 튕겨 나갔습니다)
            float impulse = damage * Constant.knockback_Per_Damage;
            if (impulse > Constant.knockback_Impulse_Max)
                impulse = Constant.knockback_Impulse_Max;

            rigidbody.AddForce(damageVector.normalized * impulse, ForceMode.Impulse);
        }

        // 피격 이펙트/사운드는 이 이벤트를 구독해서 처리합니다.
        GameEvents.RaiseUnitDamaged(this, damage, damageVector);
    }
}
