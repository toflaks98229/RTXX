using System;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public partial class Unit : MonoBehaviour
{
    // Public member variables
    /// <summary>유닛의 데이터가 담긴 구조체입니다.</summary>
    public Unit_Data unit_Data;
    /// <summary>
    /// 콜라이더의 EntityId를 캐시한 값입니다.
    /// 매 틱 GetComponent를 호출하지 않기 위해 _Start에서 한 번만 구합니다.
    /// </summary>
    public EntityId colliderEntityId;
    /// <summary>유닛이 이동할 목표 지점의 트랜스폼입니다.</summary>
    public Transform targetMoveTo;
    /// <summary>유닛이 사망했는지 여부를 나타냅니다.</summary>
    public bool bDead = false;
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
    /// <summary>유닛의 충돌체입니다.</summary>
    private CapsuleCollider capsuleCollider;
    /// <summary>유닛의 내비게이션 경로를 제어하는 컴포넌트입니다.</summary>
    private NavMeshAgent navMeshAgent;
    /// <summary>유닛 위에 표시되는 UI를 제어하는 컴포넌트입니다.</summary>
    private UI_Unit uI_Unit;

    // Unity event functions
    /// <summary>유닛이 생성된 후 초기화하는 함수입니다.</summary>
    public void _Start(int num, int armyIndex)
    {
        enabled = false;

        unit_Data = new Unit_Data(this, num, army.army_Data, armyIndex);

        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        capsuleCollider = GetComponent<CapsuleCollider>();
        colliderEntityId = capsuleCollider.GetEntityId();

        rigidbody = GetComponent<Rigidbody>();
        rigidbody.mass = army.army_Data.GetMass();
        rigidbody.linearDamping = army.army_Data.GetDrag();

        uI_Unit = GetComponentInChildren<UI_Unit>();
        uI_Unit._Start(army.army_Data);

        unit_Animation = GetComponentInChildren<Unit_Animation>();
        unit_Animation._Start(army, sprite_Weapon, sprite_Shield);

        sprite_Unit.sprite = army.images_Unit[Random.Range(0, army.images_Unit.Count)];

        if (army.images_Weapon.Count > 0)
            sprite_Weapon.sprite = army.images_Weapon[Random.Range(0, army.images_Weapon.Count)];
        if (army.images_Shield.Count > 0)
            sprite_Shield.sprite = army.images_Shield[Random.Range(0, army.images_Shield.Count)];

        capsuleCollider.radius = army.army_Data.GetRadius();
        capsuleCollider.height = army.army_Data.GetHeight();
    }

    /// <summary>매 프레임마다 호출되어 유닛의 상태를 업데이트합니다.</summary>
    public void _Update()
    {
        _Update_Move();
        _Update_Fight();

        // 이번 틱의 접촉 정보를 소비했으므로 해제합니다.
        // 아직 닿아 있다면 다음 물리 프레임의 OnCollisionStay가 다시 세웁니다.
        unit_Data.benemyContact = false;
    }

    /// <summary>다른 콜라이더와 충돌하기 시작할 때 호출됩니다.</summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (bDead) return;
        if (!collision.transform.CompareTag("Unit")) return;

        Unit unit_collision = collision.transform.GetComponent<Unit>();
        if (unit_collision == null) return;
        if (unit_collision.IsDead()) return;

        if (unit_Data.bPlayer != unit_collision.unit_Data.bPlayer)
        {
            Set_Enemy_Contact(unit_collision);
        }

        Move_Collision_Enter(unit_collision);

        if (army != null) army.Add_Army_Detected(unit_collision.GetArmy());
    }

    /// <summary>다른 콜라이더와 충돌이 지속되는 동안 호출됩니다.</summary>
    private void OnCollisionStay(Collision collision)
    {
        if (bDead) return;
        if (!collision.transform.CompareTag("Unit")) return;

        Unit unit_collision = collision.transform.GetComponent<Unit>();
        if (unit_collision == null) return;
        if (unit_collision.IsDead()) return;

        // 적과 맞닿아 있는 동안에는 매 프레임 접촉을 기록해 전진을 막습니다.
        // 이 처리가 없으면 OnCollisionEnter의 1회성 감속을 Accelerate가 즉시 되돌려
        // 적 대열을 그대로 통과해 버립니다.
        if (unit_Data.bPlayer != unit_collision.unit_Data.bPlayer)
        {
            Set_Enemy_Contact(unit_collision);
            return;
        }

        Move_Collision_Stay(unit_collision);
    }

    /// <summary>
    /// 적과 접촉했음을 기록합니다. 여러 적과 닿으면 방향을 누적해 평균을 냅니다.
    /// </summary>
    private void Set_Enemy_Contact(Unit enemy)
    {
        Vector3 toEnemy = enemy.transform.position - transform.position;
        toEnemy.y = 0.0f;

        if (toEnemy.sqrMagnitude < 0.000001f) return;

        Vector3 normal = toEnemy.normalized;

        if (unit_Data.benemyContact)
        {
            normal = (unit_Data.enemyContactNormal + normal);
            if (normal.sqrMagnitude < 0.000001f) return;
            normal = normal.normalized;
        }

        unit_Data.benemyContact = true;
        unit_Data.enemyContactNormal = normal;
    }

    /// <summary>다른 콜라이더와의 충돌이 끝날 때 호출됩니다.</summary>
    private void OnCollisionExit(Collision collision)
    {
        // 상대가 파괴되는 중이어도 내 탐지 카운트는 반드시 정리해야 합니다.
        if (collision.transform == null) return;
        if (!collision.transform.CompareTag("Unit")) return;

        Unit unit_collision = collision.transform.GetComponent<Unit>();
        if (unit_collision == null) return;

        if (army != null) army.Remove_Army_Detected(unit_collision.GetArmy());
    }

    // Public methods
    /// <summary>
    /// 유닛을 지정 위치에 '물리적으로도' 배치합니다.
    ///
    /// transform.position만 대입하면 안 되는 이유:
    /// 이 프리팹의 Rigidbody는 Interpolate가 켜진 비운동학 바디입니다.
    /// 그 경우 transform 대입이 물리 바디의 내부 위치에 즉시 반영되지 않아,
    /// 물리 엔진은 유닛들이 여전히 생성 지점에 겹쳐 있다고 판단하고
    /// 첫 시뮬레이션 스텝에서 서로를 폭발적으로 밀어냅니다.
    /// NavMeshAgent도 자체 위치를 들고 있어 Warp로 맞춰 주어야 합니다.
    /// </summary>
    public void Place_At(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        // NavMeshAgent는 반드시 Warp로 옮겨야 내부 위치가 동기화됩니다.
        NavMeshAgent agent = navMeshAgent != null ? navMeshAgent : GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
        }

        Rigidbody body = rigidbody != null ? rigidbody : GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = rotation;

            // 생성 직후 남아 있는 속도를 지워 튕겨 나가지 않도록 합니다.
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 대형 유닛의 충돌 공격을 받습니다. 피해와 함께 크게 밀려납니다.
    ///
    /// 일반 피격(넉백)과 달리 충격량이 훨씬 크고, 무기 명중 판정을 거치지 않습니다.
    /// 달려오는 말에 부딪히는 것은 '맞는' 게 아니라 '치이는' 것이기 때문입니다.
    /// </summary>
    /// <param name="damage">가해질 피해량입니다.</param>
    /// <param name="direction">밀려날 방향입니다.</param>
    /// <param name="impulse">가해질 충격량입니다.</param>
    /// <param name="attackerNum">가해자 유닛 번호입니다. 킬 기여자로 기록됩니다.</param>
    public void Take_Collision_Hit(float damage, Vector3 direction, float impulse, int attackerNum)
    {
        if (bDead) return;
        if (unit_Data.bdead) return;

        unit_Data.GetDamage(damage, direction, attackerNum);

        if (rigidbody != null && !rigidbody.isKinematic)
        {
            rigidbody.AddForce(direction.normalized * impulse, ForceMode.Impulse);
        }

        // 치인 쪽도 크게 번쩍여 무슨 일이 일어났는지 보이게 합니다.
        if (unit_Animation != null) unit_Animation.Flash_Charge_Impact(1.0f);
    }

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

    /// <summary>현재 유닛이 사망했는지 여부를 반환합니다.</summary>
    public bool IsDead()
    {
return bDead || unit_Data.bdead;
    }

    /// <summary>
    /// 유닛의 사망을 Unity 계층에 반영합니다.
    /// 실제 오브젝트 제거는 소속 부대(Army._Update_Dead)가 수행합니다.
    /// </summary>
    public void Dead()
    {
        if (bDead) return;

        bDead = true;
        unit_Data.bdead = true;

        // 선택 UI를 숨깁니다.
        if (uI_Unit != null) uI_Unit.Invisible();

        // 다른 유닛의 탐지/충돌 계산에서 즉시 빠지도록 콜라이더를 끕니다.
        if (capsuleCollider != null) capsuleCollider.enabled = false;

        // 상대편 부대의 탐지 카운트는 이 오브젝트가 파괴될 때
        // 상대 유닛의 OnCollisionExit이 호출되며 정리됩니다.

        // 물리 반응을 멈춥니다.
        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
        }

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.isStopped = true;
        }
    }

}
