using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

partial struct Unit_Data
{
    // Public methods
    /// <summary>유닛의 전투 상태를 업데이트합니다.</summary>
    public void _Update_Fight()
    {
        if (bhitTarget)
            bhitTarget = false;

        switch (e_Unit_Fight)
        {
            case E_Unit_Fight.Attack_Able:
                if (btarget)
                {
                    Attack_Start();
                }
                break;

            case E_Unit_Fight.Attack:
                Attack();
                break;

            case E_Unit_Fight.Attack_Disable:
                Attack_Disable();
                break;
        }
    }

    /// <summary>목표 유닛 데이터를 업데이트합니다.</summary>
    public void _Update_Target(Unit_Data unit_Data)
    {
        if (unit_Data.bPlayer == bPlayer)
            return;

        float distance_existing;
        float distance_current;

        distance_existing = Vector3.Distance(position, unit_Target_Data.position);
        distance_current = Vector3.Distance(position, unit_Data.position);

        if (btarget)
        {
            if (unit_Data.num == unit_Target_Data.num)
            {
                unit_Target_Data.SetTarget(unit_Data);

                if (distance_existing < army_Data.GetMeleeRange())
                {
                    e_Unit_AttackType = E_Unit_AttackType.Melee;
                }
                else if (army_Data.GetE_Unit_AttackType() == E_Unit_AttackType.Range)
                {
                    if (distance_existing < army_Data.GetRangeRange())
                    {
                        e_Unit_AttackType = E_Unit_AttackType.Range;
                    }
                }
                else
                {
                    btarget = false;
                    unit_Target_Data.RemoveTarget();
                }
            }
        }
        else
        {


            if (distance_current < army_Data.GetMeleeRange())
            {
                btarget = true;
                unit_Target_Data.SetTarget(unit_Data);
                e_Unit_AttackType = E_Unit_AttackType.Melee;
            }
            else if (army_Data.GetE_Unit_AttackType() == E_Unit_AttackType.Range)
            {
                if (distance_current < army_Data.GetRangeRange())
                {

                    btarget = true;
                    e_Unit_AttackType = E_Unit_AttackType.Range;

                    unit_Target_Data.SetTarget(unit_Data);

                }
            }
        }
    }

    /// <summary>공격을 시작합니다.</summary>
    public void Attack_Start()
    {
        switch (e_Unit_AttackType)
        {
            case E_Unit_AttackType.Melee:
                timer_AttackSpeed = new Timer(army_Data.GetMeleeAttackSpeed());

                break;
            case E_Unit_AttackType.Range:
                timer_AttackSpeed = new Timer(army_Data.GetRangeAttackSpeed());
                break;
        }

        e_Unit_Fight = E_Unit_Fight.Attack;
    }

    /// <summary>공격 딜레이 시간을 업데이트합니다.</summary>
    public void Attack_Disable()
    {
        timer_AttackDelay._Update();

        if (timer_AttackDelay.IsOverTime())
        {
            e_Unit_Fight = E_Unit_Fight.Attack_Able;
        }
    }

    /// <summary>피해를 입은 상태를 시작합니다.</summary>
    public void BeAttacked_Start()
    {

    }

    /// <summary>피해를 입는 상태를 업데이트합니다.</summary>
    public void BeAttacked()
    {

    }

    /// <summary>피해를 입는 상태를 종료합니다.</summary>
    public void BeAttacked_End()
    {

    }

    /// <summary>유닛이 피해를 입었을 때 HP를 감소시킵니다.</summary>
    public void GetDamage(float damage)
    {
        bgetDamage = true;

        HP -= damage;

        if (HP < 0.0f)
        {

        }
    }

    /// <summary>유닛이 피해를 입었을 때 HP를 감소시키고 피해 벡터를 설정합니다.</summary>
    public void GetDamage(float damage, Vector3 damageVector)
    {
        this.damageVector = damageVector.normalized * damage;
        GetDamage(damage);
    }

    // Private methods
    /// <summary>공격을 처리합니다.</summary>
    private void Attack()
    {
        timer_AttackSpeed._Update();

        if (timer_AttackSpeed.IsOverTime())
        {
            Attack_End();
        }
    }

    /// <summary>공격을 종료하고 딜레이를 설정합니다.</summary>
    private void Attack_End()
    {
        timer_AttackDelay.ReSetTimer();

        float distance;
        distance = Vector3.Distance(position, unit_Target_Data.position);

        if (distance < army_Data.GetMeleeRange())
        {
            bhitTarget = true;
        }

        e_Unit_Fight = E_Unit_Fight.Attack_Disable;
    }
}

partial class Unit
{
    // Public methods
    /// <summary>공격을 시작합니다.</summary>
    public void Attack_Start()
    {

    }

    /// <summary>공격을 처리합니다.</summary>
    public void Attack()
    {

    }

    /// <summary>공격을 종료합니다.</summary>
    public void Attack_End()
    {

    }

    /// <summary>피격 상태를 시작합니다.</summary>
    public void BeAttacked_Start()
    {

    }

    /// <summary>피격 상태를 업데이트합니다.</summary>
    public void BeAttacked()
    {

    }

    /// <summary>피격 상태를 종료합니다.</summary>
    public void BeAttacked_End()
    {

    }

    // Private methods
    /// <summary>유닛의 전투 관련 로직을 업데이트합니다.</summary>
    private void _Update_Fight()
    {
        if (unit_Data.bgetDamage)
        {
            rigidbody.velocity += unit_Data.damageVector;

            unit_Data.bgetDamage = false;
            unit_Data.damageVector = new Vector3();
        }
    }
}
