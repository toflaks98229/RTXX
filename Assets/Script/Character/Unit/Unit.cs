using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public partial class Unit : MonoBehaviour
{
    // Public member variables
    /// <summary>유닛의 데이터가 담긴 구조체입니다.</summary>
    public Unit_Data unit_Data;
    /// <summary>유닛이 이동할 목표 지점의 트랜스폼입니다.</summary>
    public Transform targetMoveTo;
    /// <summary>유닛이 전투 중인지 여부를 나타냅니다.</summary>
    public bool bFight = false;
    /// <summary>유닛이 사망했는지 여부를 나타냅니다.</summary>
    public bool bDead = false;
    /// <summary>유닛이 공격받고 있는지 여부를 나타냅니다.</summary>
    public bool bBeAttacked = false;
    /// <summary>유닛이 감지한 다른 유닛들의 데이터 리스트입니다.</summary>
    public List<Unit_Data> unitCollisions;
    /// <summary>유닛의 애니메이션을 제어하는 컴포넌트입니다.</summary>
    public Unit_Animation unit_Animation;
    /// <summary>유닛의 본체 스프라이트 렌더러입니다.</summary>
    public SpriteRenderer sprite_Unit;
    /// <summary>유닛의 무기 스프라이트 렌더러입니다.</summary>
    public SpriteRenderer sprite_Weapon;
    /// <summary>유닛의 방패 스프라이트 렌더러입니다.</summary>
    public SpriteRenderer sprite_Shield;

    // Private member variables
    /// <summary>유닛이 속한 군대입니다.</summary>
    private Army army;
    /// <summary>유닛의 물리적 움직임을 제어하는 리지드바디입니다.</summary>
    private Rigidbody rigidbody;
    /// <summary>유닛의 내비게이션 경로를 제어하는 컴포넌트입니다.</summary>
    private NavMeshAgent navMeshAgent;
    /// <summary>유닛 위에 표시되는 UI를 제어하는 컴포넌트입니다.</summary>
    private UI_Unit uI_Unit;
    /// <summary>유닛의 공격 목표 유닛입니다.</summary>
    private Unit targetUnit;

    // Unity event functions
    /// <summary>유닛이 생성된 후 초기화하는 함수입니다.</summary>
    public void _Start(int num)
    {
        enabled = false;

        unit_Data = new Unit_Data(this, num);

        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        rigidbody = GetComponent<Rigidbody>();
        rigidbody.mass = unit_Data.army_Data.GetMass();
        rigidbody.drag = unit_Data.army_Data.GetDrag();

        uI_Unit = GetComponentInChildren<UI_Unit>();
        uI_Unit._Start(army.army_Data);

        unitCollisions = new List<Unit_Data>();

        unit_Animation = GetComponentInChildren<Unit_Animation>();
        unit_Animation._Start(army);

        sprite_Unit.sprite = army.images_Unit[Random.Range(0, army.images_Unit.Count)];

        if (army.images_Weapon.Count > 0)
            sprite_Weapon.sprite = army.images_Weapon[Random.Range(0, army.images_Weapon.Count)];
        if (army.images_Shield.Count > 0)
            sprite_Shield.sprite = army.images_Shield[Random.Range(0, army.images_Shield.Count)];

        GetComponent<CapsuleCollider>().radius = unit_Data.army_Data.GetRadius();
        GetComponent<CapsuleCollider>().height = unit_Data.army_Data.GetHeight();
    }

    /// <summary>매 프레임마다 호출되어 유닛의 상태를 업데이트합니다.</summary>
    public void _Update()
    {
        _Update_Move();
        _Update_Fight();
    }

    /// <summary>다른 콜라이더와 충돌하기 시작할 때 호출됩니다.</summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Unit"))
        {
            Unit unit_collision = collision.transform.GetComponent<Unit>();

            if (unit_collision != null)
                unitCollisions.Add(unit_collision.GetUnit_Data());

            Move_Collision_Enter(unit_collision);

            army.Add_Army_Detected(unit_collision.GetArmy());
        }
    }

    /// <summary>다른 콜라이더와 충돌이 지속되는 동안 호출됩니다.</summary>
    private void OnCollisionStay(Collision collision)
    {
        if (collision.transform.CompareTag("Unit"))
        {
            Unit unit_collision = collision.transform.GetComponent<Unit>();

            Move_Collision_Stay(unit_collision);
        }
    }

    /// <summary>다른 콜라이더와의 충돌이 끝날 때 호출됩니다.</summary>
    private void OnCollisionExit(Collision collision)
    {
        if (collision.transform.CompareTag("Unit"))
        {
            Unit unit_collision = collision.transform.GetComponent<Unit>();

            if (unit_collision != null)
                unitCollisions.Remove(unit_collision.GetUnit_Data());

            army.Remove_Army_Detected(unit_collision.GetArmy());
        }
    }

    // Public methods
    /// <summary>현재 유닛의 데이터를 반환합니다.</summary>
    public Unit_Data GetUnit_Data()
    {
        return unit_Data;
    }

    /// <summary>유닛이 속한 군대를 설정합니다.</summary>
    public void SetArmy(Army army)
    {
        this.army = army;
    }

    /// <summary>유닛이 속한 군대를 반환합니다.</summary>
    public Army GetArmy()
    {
        return army;
    }

    /// <summary>유닛이 속한 군대의 데이터를 반환합니다.</summary>
    public Army_Data GetArmy_Data()
    {
        return army.army_Data;
    }

    /// <summary>유닛의 선택 상태를 해제하고 UI를 숨깁니다.</summary>
    public void UnSelected()
    {
        uI_Unit.Invisible();
    }

    /// <summary>유닛을 선택 상태로 만들고 UI를 표시합니다.</summary>
    public void Selected()
    {
        uI_Unit.Visible();
    }

    /// <summary>현재 유닛이 공격받고 있는지 여부를 반환합니다.</summary>
    public bool IsBeAttacked()
    {
        return bBeAttacked;
    }

    /// <summary>현재 유닛이 사망했는지 여부를 반환합니다.</summary>
    public bool IsDead()
    {
        return bDead;
    }

    /// <summary>현재 유닛이 전투 중인지 여부를 반환합니다.</summary>
    public bool IsFight()
    {
        return bFight;
    }

    /// <summary>유닛이 속한 군대의 킬 카운트를 증가시킵니다.</summary>
    public void Kill()
    {
        army.AddKillCount();
    }
}
