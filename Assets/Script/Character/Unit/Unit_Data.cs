using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

[Serializable]
public partial struct Unit_Data
{
    // Public member variables
    /// <summary>유닛의 고유 번호입니다.</summary>
    public int num;
    /// <summary>유닛이 플레이어 소속인지 여부입니다.</summary>
    public bool bPlayer;
    /// <summary>유닛의 현재 위치입니다.</summary>
    public Vector3 position;
    /// <summary>유닛의 현재 회전값입니다.</summary>
    public Quaternion rotation;
    /// <summary>유닛이 속한 군대의 데이터입니다.</summary>
    public Army_Data army_Data;
    /// <summary>유닛의 현재 HP입니다.</summary>
    public float HP;
    /// <summary>유닛의 이동 상태입니다 (이동 중, 대기 중).</summary>
    public E_Unit_Move e_Unit_Move;
    /// <summary>유닛이 특정 목표 지점으로 이동 중인지 여부입니다.</summary>
    public bool btargetMoveTo;
    /// <summary>유닛의 이동 벡터입니다.</summary>
    public Vector3 movementVector;
    /// <summary>유닛의 목표 위치입니다.</summary>
    public Vector3 location;
    /// <summary>유닛의 목표 벡터입니다.</summary>
    public Vector3 targetVector;
    /// <summary>유닛의 조종 목표 위치입니다.</summary>
    public Vector3 steeringTarget;
    /// <summary>유닛의 현재 이동 속도입니다.</summary>
    public float currentMoveSpeed;
    /// <summary>유닛이 멈춰야 하는지 여부입니다.</summary>
    public bool bstop;
    /// <summary>유닛의 방향 회전값입니다.</summary>
    public Quaternion direction;
    /// <summary>회전 관련 부동 소수점 변수입니다.</summary>
    public float rotateFloat;
    /// <summary>유닛의 공격 목표 데이터입니다.</summary>
    public Unit_target_Data unit_Target_Data;
    /// <summary>유닛이 공격 목표를 가지고 있는지 여부입니다.</summary>
    public bool btarget;
    /// <summary>유닛이 목표를 공격했는지 여부입니다.</summary>
    public bool bhitTarget;
    /// <summary>유닛이 피해를 입었는지 여부입니다.</summary>
    public bool bgetDamage;
    /// <summary>피해를 입은 방향과 크기 벡터입니다.</summary>
    public Vector3 damageVector;
    /// <summary>공격 속도 타이머입니다.</summary>
    public Timer timer_AttackSpeed;
    /// <summary>공격 딜레이 타이머입니다.</summary>
    public Timer timer_AttackDelay;
    /// <summary>유닛의 전투 상태입니다.</summary>
    public E_Unit_Fight e_Unit_Fight;
    /// <summary>유닛의 공격 타입입니다 (근접, 원거리).</summary>
    public E_Unit_AttackType e_Unit_AttackType;

    // Public methods
    /// <summary>Unit_Data 구조체의 생성자입니다.</summary>
    public Unit_Data(Unit unit, int num)
    {
        this.num = num;
        bPlayer = unit.GetArmy_Data().bplayer;

        position = unit.transform.position;
        rotation = unit.transform.rotation;

        army_Data = unit.GetArmy_Data();

        HP = army_Data.GetHP();

        e_Unit_Move = E_Unit_Move.Idle;

        btargetMoveTo = false;

        movementVector = new Vector3();

        location = position;
        targetVector = new Vector3();
        steeringTarget = new Vector3();

        currentMoveSpeed = 0.0f;

        bstop = true;

        direction = rotation;

        rotateFloat = 0.0f;

        unit_Target_Data = new Unit_target_Data();
        unit_Target_Data.RemoveTarget();

        btarget = false;

        bhitTarget = false;

        bgetDamage = false;
        damageVector = new Vector3();

        timer_AttackSpeed = new Timer(army_Data.GetMeleeAttackSpeed());
        timer_AttackDelay = new Timer(army_Data.GetAttackDelay());

        e_Unit_Fight = E_Unit_Fight.Attack_Able;
        e_Unit_AttackType = E_Unit_AttackType.Melee;
    }

    /// <summary>유닛 데이터를 업데이트합니다.</summary>
    public void _Update()
    {
        _Update_Move();
        _Update_Fight();
    }
}

public enum E_Unit_Move
{
    Move,
    Idle
}

public enum E_Unit_Fight
{
    Attack_Disable,
    Attack_Able,
    Attack,
}

public enum E_Unit_AttackType
{
    Melee,
    Range
}

[Serializable]
public struct Unit_target_Data
{
    // Public member variables
    /// <summary>목표의 위치입니다.</summary>
    public Vector3 position;
    /// <summary>목표의 회전값입니다.</summary>
    public Quaternion rotation;
    /// <summary>목표의 고유 번호입니다.</summary>
    public int num;

    // Public methods
    /// <summary>새로운 목표를 설정합니다.</summary>
    public void SetTarget(Unit_Data unit_Data)
    {
        position = unit_Data.position;
        rotation = unit_Data.rotation;
        num = unit_Data.num;
    }

    /// <summary>목표를 제거합니다.</summary>
    public void RemoveTarget()
    {
        position = Vector3.positiveInfinity;
        rotation = new Quaternion();

        num = int.MaxValue;
    }
}
