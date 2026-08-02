using System;

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
    /// <summary>유닛이 속한 부대의 인덱스입니다. 부대 스탯은 이 인덱스로 조회합니다.</summary>
    public int armyIndex;
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
    /// <summary>유닛이 사망했는지 여부입니다. Job 내부에서 판정되며 Unit 계층이 소비합니다.</summary>
    public bool bdead;
    /// <summary>사망 처리가 Unit 계층에 이미 반영되었는지 여부입니다.</summary>
    public bool bdeadHandled;
    /// <summary>이 유닛을 쓰러뜨린 유닛의 고유 번호입니다. 없으면 int.MaxValue입니다.</summary>
    public int killerNum;
    /// <summary>유닛이 피해를 입었는지 여부입니다.</summary>
    public bool bgetDamage;
    /// <summary>피해를 입은 방향과 크기 벡터입니다.</summary>
    public Vector3 damageVector;
    /// <summary>
    /// 적 유닛과 접촉해 전진이 막힌 상태인지 여부입니다.
    /// 물리 충돌(OnCollisionStay)에서 매 프레임 세워지고, 시뮬레이션 틱이 소비 후 해제합니다.
    /// </summary>
    public bool benemyContact;
    /// <summary>나를 막고 있는 적 방향(정규화)입니다. 이 방향으로의 전진 성분만 제거합니다.</summary>
    public Vector3 enemyContactNormal;

    // --- 돌격 ---
    /// <summary>돌격 중(아직 충돌 전)인지 여부입니다.</summary>
    public bool bcharging;
    /// <summary>이번 틱에 돌격 충돌이 성립했는지 여부입니다. 메인 스레드가 소비합니다.</summary>
    public bool bchargeImpact;
    /// <summary>현재 남아 있는 돌격 보너스 비율(0~1)입니다. 시간에 따라 0으로 감쇠합니다.</summary>
    public float chargeBonus;
    /// <summary>충돌 당시의 충격 세기(0~1)입니다. 감쇠의 시작값이 됩니다.</summary>
    public float chargeImpactPower;

    /// <summary>
    /// 이번 틱에 '몸으로 부딪히는' 충돌 공격이 성립했는지 여부입니다.
    ///
    /// 대형 유닛과 기병은 무기뿐 아니라 몸으로도 피해를 줍니다.
    /// 전속력으로 달려든 말은 부딪히는 것만으로 사람을 쓰러뜨립니다.
    /// 메인 스레드가 소비해 피해와 넉백을 적용합니다.
    /// </summary>
    public bool bcollisionAttack;
    /// <summary>돌격 보너스 감쇠를 재는 타이머입니다.</summary>
    public Timer timer_Charge;
    /// <summary>공격 속도 타이머입니다.</summary>
    public Timer timer_AttackSpeed;
    /// <summary>공격 딜레이 타이머입니다.</summary>
    public Timer timer_AttackDelay;
    /// <summary>유닛의 전투 상태입니다.</summary>
    public E_Unit_Fight e_Unit_Fight;
    /// <summary>유닛의 공격 타입입니다 (근접, 원거리).</summary>
    public E_Unit_AttackType e_Unit_AttackType;

    /// <summary>
    /// 남은 탄약입니다. 사격할 때마다 1씩 줄어듭니다.
    /// 0이 되면 더 이상 쏘지 못하고 근접으로만 싸웁니다.
    /// 부대 스탯의 ammunition이 0 이하이면 무한이므로 이 값은 무시됩니다.
    /// </summary>
    public int ammunition;

    /// <summary>
    /// 이번 틱에 원거리 발사가 성립했는지 여부입니다.
    /// 메인 스레드가 소비해 투사체 궤적을 그리고 다시 내립니다.
    /// (Burst Job 안에서는 오브젝트를 만들 수 없으므로 플래그로 넘깁니다)
    /// </summary>
    public bool bfiredThisTick;

    // Public methods
    /// <summary>Unit_Data 구조체의 생성자입니다.</summary>
    public Unit_Data(Unit unit, int num, in Army_Data armyData, int armyIndex)
    {
        this.num = num;
        this.armyIndex = armyIndex;
        bPlayer = armyData.bplayer;

        position = unit.transform.position;
        rotation = unit.transform.rotation;

        HP = armyData.GetHP();

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

        bdead = false;
        bdeadHandled = false;
        killerNum = int.MaxValue;

        bgetDamage = false;
        damageVector = new Vector3();

        benemyContact = false;
        enemyContactNormal = new Vector3();

        bcharging = false;
        bchargeImpact = false;
        chargeBonus = 0.0f;
        chargeImpactPower = 0.0f;
        bcollisionAttack = false;
        timer_Charge = new Timer(Constant.time_Charge_Bonus);

        timer_AttackSpeed = new Timer(armyData.GetMeleeAttackSpeed());
        timer_AttackDelay = new Timer(armyData.GetAttackDelay());

        e_Unit_Fight = E_Unit_Fight.Attack_Able;
        e_Unit_AttackType = E_Unit_AttackType.Melee;

        ammunition = armyData.GetAmmunition();
        bfiredThisTick = false;
    }

    /// <summary>유닛 데이터를 업데이트합니다.</summary>
    public void _Update(in Army_Data armyData)
    {
        _Update_Move(armyData);
        _Update_Fight(armyData);
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

/// <summary>
/// 병종 분류입니다. 상성 보너스의 대상을 결정합니다.
///
/// 토탈워의 가위바위보는 '무엇에 강한가'를 스탯으로 표현합니다.
/// 창병은 대형(기병)에 강하고, 기병은 궁병을 짓밟고, 궁병은 보병을 갉아먹습니다.
/// </summary>
public enum E_Unit_Class
{
    /// <summary>일반 보병입니다. 무난하지만 특출난 상성이 없습니다.</summary>
    Infantry,

    /// <summary>창병입니다. 대형 상대 보너스를 갖고 돌격을 받아냅니다.</summary>
    Spear,

    /// <summary>기병입니다. 대형으로 취급되어 창병에게 약합니다.</summary>
    Cavalry,

    /// <summary>궁병입니다. 근접에 약하지만 원거리에서 갉아먹습니다.</summary>
    Archer,

    /// <summary>대형 유닛입니다. 기병과 마찬가지로 대형 판정을 받습니다.</summary>
    Large
}

/// <summary>병종 분류에 대한 판정 도우미입니다. Burst에서 쓰이므로 순수 정적 함수입니다.</summary>
public static class Unit_Class_Util
{
    /// <summary>
    /// 이 병종이 '대형'으로 취급되는지 여부입니다.
    /// 창병의 대형 보너스와 anti-large 판정이 이 기준을 씁니다.
    /// </summary>
    public static bool IsLarge(E_Unit_Class unitClass)
    {
        return unitClass == E_Unit_Class.Cavalry
            || unitClass == E_Unit_Class.Large;
    }
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
