using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 열거형 (Enums)
/// <summary>
/// 부대의 이동 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Move
{
    Move,       // 일반 이동
    MoveToTarget, // 타겟으로 이동
    MoveEscape,   // 도주
    MoveCharge,   // 돌격
    Idle        // 대기
}

/// <summary>
/// 부대의 진형 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Formation
{
    Formation,      // 진형 상태
    NonFormation  // 비진형 상태
}

/// <summary>
/// 부대의 전투 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Fight
{
    Melee, // 근접 전투
    Range, // 원거리 전투
    Non    // 비전투
}

// 클래스
/// <summary>
/// 부대의 진형에 대한 데이터를 담고 있는 클래스입니다.
/// </summary>
[Serializable]
public class Formation_Data
{
    // 공개 멤버 변수
    /// <summary>
    /// 각 유닛의 목표 위치 리스트입니다.
    /// </summary>
    public List<Vector3> formation;
    /// <summary>
    /// 진형의 전방 방향입니다.
    /// </summary>
    public Vector3 direction;
    /// <summary>
    /// 진형의 중심 위치입니다.
    /// </summary>
    public Vector3 position;
    /// <summary>
    /// 진형의 너비를 결정하는 유닛 수입니다.
    /// </summary>
    public int num;

    // 생성자
    /// <summary>
    /// 다른 Formation_Data 인스턴스를 복사하여 새로운 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="formation_Data">복사할 Formation_Data 인스턴스입니다.</param>
    public Formation_Data(Formation_Data formation_Data)
    {
        position = formation_Data.position;
        direction = formation_Data.direction;
        num = formation_Data.num;
        formation = formation_Data.formation;
    }

    /// <summary>
    /// 모든 진형 데이터를 인수로 받아 새로운 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="num">진형의 너비입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <param name="formation">유닛들의 진형 위치 리스트입니다.</param>
    public Formation_Data(int num, Vector3 direction, Vector3 position, List<Vector3> formation)
    {
        this.num = num;
        this.direction = direction;
        this.position = position;
        this.formation = formation;
    }

    // 공개 메서드
    /// <summary>
    /// 현재 Formation_Data 인스턴스를 반환합니다.
    /// </summary>
    /// <returns>Formation_Data 인스턴스입니다.</returns>
    public Formation_Data GetFormation_Data()
    {
        return this;
    }

    /// <summary>
    /// 진형 데이터를 설정합니다.
    /// </summary>
    /// <param name="num">진형의 너비입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <param name="formation">유닛들의 진형 위치 리스트입니다.</param>
    public void Set_Formation_Data(int num, Vector3 direction, Vector3 position, List<Vector3> formation)
    {
        this.num = num;
        this.direction = direction;
        this.position = position;
        this.formation = formation;
    }

    /// <summary>
    /// 다른 Formation_Data 인스턴스의 데이터로 현재 인스턴스를 덮어씁니다.
    /// </summary>
    /// <param name="formation_Data">덮어쓸 Formation_Data 인스턴스입니다.</param>
    public void Set_Formation_Data(Formation_Data formation_Data)
    {
        position = formation_Data.position;
        direction = formation_Data.direction;
        num = formation_Data.num;
        formation = formation_Data.formation;
    }

    /// <summary>
    /// 진형의 방향을 반환합니다.
    /// </summary>
    /// <returns>진형의 방향 벡터입니다.</returns>
    public Vector3 GetDirection()
    {
        return direction;
    }

    /// <summary>
    /// 진형의 위치를 반환합니다.
    /// </summary>
    /// <returns>진형의 위치 벡터입니다.</returns>
    public Vector3 GetPosition()
    {
        return position;
    }

    /// <summary>
    /// 진형의 너비를 반환합니다.
    /// </summary>
    /// <returns>진형의 너비입니다.</returns>
    public int GetNum()
    {
        return num;
    }
}

// 구조체
/// <summary>
/// 부대의 전반적인 데이터를 담고 있는 구조체입니다.
/// </summary>
[Serializable]
public struct Army_Data
{
    // 공개 멤버 변수
    /// <summary>
    /// 부대의 현재 위치입니다.
    /// </summary>
    public Vector3 position;
    /// <summary>
    /// 부대의 현재 회전 값입니다.
    /// </summary>
    public Quaternion rotation;
    /// <summary>
    /// 부대가 플레이어 소유인지 여부입니다.
    /// </summary>
    public bool bplayer;
    /// <summary>
    /// 명령 상태인지 여부입니다.
    /// </summary>
    public bool bonCommand;
    /// <summary>
    /// 부대의 유닛 통계 데이터입니다.
    /// </summary>
    public Unit_Stat unit_Stat;
    /// <summary>
    /// 부대 전체의 총 HP입니다.
    /// </summary>
    public float HP_All;
    /// <summary>
    /// 현재 부대의 유닛 수입니다.
    /// </summary>
    public int unit_Num;
    /// <summary>
    /// 부대가 가질 수 있는 최대 유닛 수입니다.
    /// </summary>
    public int unit_Num_Max;
    /// <summary>
    /// 방어 상태인지 여부입니다.
    /// </summary>
    public bool bdefense;
    /// <summary>
    /// 부대의 전투 상태입니다.
    /// </summary>
    public E_Army_Fight e_Army_Fight;
    /// <summary>
    /// 부대의 이동 상태입니다.
    /// </summary>
    public E_Army_Move e_Army_Move;
    /// <summary>
    /// 부대의 진형 상태입니다.
    /// </summary>
    public E_Army_Formation e_Army_Formation;
    /// <summary>
    /// 명령 상태 지속 시간을 측정하는 타이머입니다.
    /// </summary>
    public Timer timer_On_Command;
    /// <summary>
    /// 재편성 상태를 나타내는 타이머입니다.
    /// </summary>
    public Timer timer_Reformation;
    /// <summary>
    /// 재편성이 필요한지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool breformation;

    // 공개 메서드
    /// <summary>
    /// 부대 데이터를 초기화합니다.
    /// </summary>
    public void _Start()
    {
        e_Army_Move = E_Army_Move.Idle;
        timer_Reformation = new Timer(Constant.time_Reformation);
    }

    /// <summary>
    /// 부대 데이터를 업데이트합니다.
    /// </summary>
    public void _Update()
    {
        switch (e_Army_Move)
        {
            case E_Army_Move.Idle:
                timer_Reformation._Update();
                if (timer_Reformation.IsOverTime())
                {
                    breformation = true;
                    timer_Reformation.ReSetTimer();
                }
                break;
            case E_Army_Move.Move:
                timer_Reformation.ReSetTimer();
                breformation = false;
                break;
            case E_Army_Move.MoveToTarget:
                timer_Reformation.ReSetTimer();
                breformation = false;
                break;
        }
    }

    /// <summary>
    /// 부대 전체의 총 HP를 반환합니다.
    /// </summary>
    /// <returns>총 HP입니다.</returns>
    public float GetAllHP()
    {
        if (HP_All < 0) return 0;
        return HP_All;
    }

    /// <summary>
    /// 부대 전체의 총 HP를 증가시킵니다.
    /// </summary>
    /// <param name="addFloat">추가할 HP 값입니다.</param>
    public void AddAllHP(float addFloat)
    {
        HP_All += addFloat;
    }

    // 통계 관련 메서드
    public float GetMoveSpeed()
    {
        if (unit_Stat.moveSpeed < 0) return 0;
        return unit_Stat.moveSpeed;
    }

    public void AddMoveSpeed(float addFloat)
    {
        unit_Stat.moveSpeed += addFloat;
    }

    public float GetRotationSpeed()
    {
        if (unit_Stat.rotationSpeed < 0) return 0;
        return unit_Stat.rotationSpeed;
    }

    public void AddRotationSpeed(float addFloat)
    {
        unit_Stat.rotationSpeed += addFloat;
    }

    public float GetAcceleration()
    {
        if (unit_Stat.acceleration < 0) return 0;
        return unit_Stat.acceleration;
    }

    public void AddAccelerationd(float addFloat)
    {
        unit_Stat.acceleration += addFloat;
    }

    public float GetMass()
    {
        if (unit_Stat.mass < 0) return 0;
        return unit_Stat.mass;
    }

    public void AddMass(float addFloat)
    {
        unit_Stat.mass += addFloat;
    }

    public float GetDrag()
    {
        if (unit_Stat.drag < 0) return 0;
        return unit_Stat.drag;
    }

    public void AddDrag(float addFloat)
    {
        unit_Stat.drag += addFloat;
    }

    // 근접 전투 관련 메서드
    public float GetMeleeDamage()
    {
        if (unit_Stat.meleeDamage < 0) return 0;
        return unit_Stat.meleeDamage;
    }

    public void AddMeleeDamage(float addFloat)
    {
        unit_Stat.meleeDamage += addFloat;
    }

    public float GetMeleeAttack()
    {
        if (unit_Stat.meleeAttack < 0) return 0;
        return unit_Stat.meleeAttack;
    }

    public void AddMeleeAttack(float addFloat)
    {
        unit_Stat.meleeAttack += addFloat;
    }

    public float GetMeleeAttackSpeed()
    {
        if (unit_Stat.meleeAttackSpeed < 0) return 0;
        return unit_Stat.meleeAttackSpeed;
    }

    public void AddMeleeAttackSpeed(float addFloat)
    {
        unit_Stat.meleeAttackSpeed += addFloat;
    }

    public float GetMeleeDiffense()
    {
        if (unit_Stat.meleeDiffense < 0) return 0;
        return unit_Stat.meleeDiffense;
    }

    public void AddMeleeDiffense(float addFloat)
    {
        unit_Stat.meleeDiffense += addFloat;
    }

    public float GetMeleeRange()
    {
        if (unit_Stat.meleeRange < 0) return 0;
        return unit_Stat.meleeRange;
    }

    public void AddMeleeRange(float addFloat)
    {
        unit_Stat.meleeRange += addFloat;
    }

    public float GetMeleeChargeSpeed()
    {
        if (unit_Stat.meleeChargeSpeed < 0) return 0;
        return unit_Stat.meleeChargeSpeed;
    }

    public void AddMeleeChargeSpeed(float addFloat)
    {
        unit_Stat.meleeChargeSpeed += addFloat;
    }

    public float GetMeleeChargeRange()
    {
        if (unit_Stat.meleeChargeRange < 0) return 0;
        return unit_Stat.meleeChargeRange;
    }

    public void AddMeleeChargeRange(float addFloat)
    {
        unit_Stat.meleeChargeRange += addFloat;
    }

    // 방어구 관련 메서드
    public float GetArmor()
    {
        if (unit_Stat.armor < 0) return 0;
        return unit_Stat.armor;
    }

    public void AddArmor(float addFloat)
    {
        unit_Stat.armor += addFloat;
    }

    public float GetShieldArmor()
    {
        if (unit_Stat.shieldArmor < 0) return 0;
        return unit_Stat.shieldArmor;
    }

    public void AddShieldArmor(float addFloat)
    {
        unit_Stat.shieldArmor += addFloat;
    }

    // 원거리 공격 관련 메서드
    public bool IsRangeAttackAble()
    {
        return unit_Stat.brangeAttackAble;
    }

    public void SetRangeAttackAble(bool brangeAttackAble)
    {
        unit_Stat.brangeAttackAble = brangeAttackAble;
    }

    public float GetRangeDamage()
    {
        if (unit_Stat.rangeDamage < 0) return 0;
        return unit_Stat.rangeDamage;
    }

    public void AddRangeDamage(float addFloat)
    {
        unit_Stat.rangeDamage += addFloat;
    }

    public float GetRangeAttackSpeed()
    {
        if (unit_Stat.rangeAttackSpeed < 0) return 0;
        return unit_Stat.rangeAttackSpeed;
    }

    public void AddRangeAttackSpeed(float addFloat)
    {
        unit_Stat.rangeAttackSpeed += addFloat;
    }

    public float GetRangeDiffense()
    {
        if (unit_Stat.rangeDiffense < 0) return 0;
        return unit_Stat.rangeDiffense;
    }

    public void AddRangeDiffense(float addFloat)
    {
        unit_Stat.rangeDiffense += addFloat;
    }

    public float GetRangeAccuracy()
    {
        if (unit_Stat.rangeAccuracy < 0) return 0;
        return unit_Stat.rangeAccuracy;
    }

    public void AddRangeAccuracy(float addFloat)
    {
        unit_Stat.rangeAccuracy += addFloat;
    }

    public float GetRangeRange()
    {
        if (unit_Stat.rangeRange < 0) return 0;
        return unit_Stat.rangeRange;
    }

    public void AddRangeRange(float addFloat)
    {
        unit_Stat.rangeRange += addFloat;
    }

    // 공격 지연 관련 메서드
    public float GetAttackDelay()
    {
        if (unit_Stat.attackDelay < 0) return 0;
        return unit_Stat.attackDelay;
    }

    public void AddAttackDelay(float addFloat)
    {
        unit_Stat.attackDelay += addFloat;
    }

    // 크기 관련 메서드
    public float GetSize()
    {
        if (unit_Stat.size <= 0) return 0f;
        return unit_Stat.size;
    }

    public void AddSize(float addFloat)
    {
        unit_Stat.size += addFloat;
    }

    public float GetInterval()
    {
        if (unit_Stat.interval <= 0) return 0f;
        return unit_Stat.interval;
    }

    public void AddInterval(float addFloat)
    {
        unit_Stat.interval += addFloat;
    }

    // 충돌 관련 메서드
    public float GetRadius()
    {
        return unit_Stat.radius;
    }

    public float GetHeight()
    {
        return unit_Stat.height;
    }

    // 체력 관련 메서드
    public float GetHP()
    {
        if (unit_Stat.HP < 0) return 0;
        return unit_Stat.HP;
    }

    public void AddHP(float addFloat)
    {
        unit_Stat.HP += addFloat;
    }

    // 공격 타입 관련 메서드
    public E_Unit_AttackType GetE_Unit_AttackType()
    {
        return unit_Stat.e_Unit_AttackType;
    }
}
