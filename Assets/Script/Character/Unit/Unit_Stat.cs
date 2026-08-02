using System;

using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Unit_Stat
{
    // Public member variables
    // Speed
    /// <summary>유닛의 이동 속도입니다.</summary>
    public float moveSpeed;
    /// <summary>유닛의 회전 속도입니다.</summary>
    public float rotationSpeed;
    /// <summary>유닛의 가속도입니다.</summary>
    public float acceleration;
    /// <summary>유닛의 질량입니다.</summary>
    public float mass;
    /// <summary>유닛의 저항력(드래그)입니다.</summary>
    public float drag;

    // Melee
    /// <summary>
    /// 유닛의 근접 기본 피해량입니다. 방어구가 이 피해를 감산합니다.
    /// </summary>
    public float meleeDamage;
    /// <summary>
    /// 유닛의 근접 방어구 관통(AP) 피해량입니다.
    /// 방어구를 완전히 무시하고 그대로 들어갑니다.
    /// 중장갑 상대에게는 이 수치가 실질 피해를 결정합니다.
    /// </summary>
    public float meleeDamageAP;
    /// <summary>유닛의 근접 공격 수치입니다.</summary>
    public float meleeAttack;
    /// <summary>유닛의 근접 공격 속도입니다.</summary>
    public float meleeAttackSpeed;
    /// <summary>유닛의 근접 방어력입니다.</summary>
    public float meleeDiffense;
    /// <summary>유닛의 근접 공격 사거리입니다.</summary>
    public float meleeRange;
    /// <summary>근접 돌격 속도입니다.</summary>
    public float meleeChargeSpeed;
    /// <summary>근접 돌격 사거리입니다.</summary>
    public float meleeChargeRange;
    /// <summary>
    /// 돌격 충돌 순간에 더해지는 공격력/피해량 보너스입니다.
    /// 충돌 직후 최대이고 time_Charge_Bonus 동안 선형으로 사라집니다.
    /// </summary>
    public float meleeChargeBonus;

    // Armor
    /// <summary>유닛의 갑옷 방어력입니다.</summary>
    public float armor;
    /// <summary>유닛의 방패 방어력입니다.</summary>
    public float shieldArmor;

    // Range
    /// <summary>원거리 공격 가능 여부입니다.</summary>
    public bool brangeAttackAble;
    /// <summary>
    /// 유닛의 원거리 기본 피해량입니다. 방어구가 이 피해를 감산합니다.
    /// </summary>
    public float rangeDamage;
    /// <summary>
    /// 유닛의 원거리 방어구 관통(AP) 피해량입니다.
    /// 석궁이나 총처럼 갑옷을 뚫는 무기가 이 수치를 갖습니다.
    /// </summary>
    public float rangeDamageAP;
    /// <summary>유닛의 원거리 공격 속도입니다.</summary>
    public float rangeAttackSpeed;
    /// <summary>유닛의 원거리 방어력입니다.</summary>
    public float rangeDiffense;
    /// <summary>유닛의 원거리 공격 정확도입니다.</summary>
    public float rangeAccuracy;
    /// <summary>유닛의 원거리 공격 사거리입니다.</summary>
    public float rangeRange;

    // Attack
    /// <summary>공격 딜레이 시간입니다.</summary>
    public float attackDelay;

    // Size
    /// <summary>유닛의 크기입니다.</summary>
    public float size;
    /// <summary>유닛 간의 간격입니다.</summary>
    public float interval;

    // Collision
    /// <summary>유닛 콜라이더의 반지름입니다.</summary>
    public float radius;
    /// <summary>유닛 콜라이더의 높이입니다.</summary>
    public float height;

    // Health
    /// <summary>유닛의 최대 HP입니다.</summary>
    public float HP;

    /// <summary>유닛의 기본 공격 타입입니다.</summary>
    public E_Unit_AttackType e_Unit_AttackType;
}
