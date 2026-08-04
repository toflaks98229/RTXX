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
    /// unit_Data를 참조로 돌려줍니다.
    ///
    /// 왜 필요한가:
    /// Unit_Data는 264바이트 구조체입니다. u.unit_Data.field = x 를
    /// 여러 번 쓰면 그때마다 필드 오프셋 계산이 새로 붙고, 읽기는
    /// 구조체 전체를 복사할 수도 있습니다.
    ///
    /// 한 곳에서 여러 필드를 연달아 만질 때는 ref로 한 번만 잡아 두는
    /// 편이 훨씬 쌉니다. 충돌 결과 반영 루프처럼 9,600번 도는 자리에서
    /// 그 차이가 드러납니다.
    ///
    /// 주의: 참조를 오래 들고 있지 마십시오. 이 구조체는 매 틱
    /// Job 배열과 오가므로, 보관하면 낡은 값을 가리키게 됩니다.
    /// </summary>
    public ref Unit_Data unit_DataRef => ref unit_Data;
    /// <summary>
    /// 콜라이더의 EntityId를 캐시한 값입니다.
    /// 매 틱 GetComponent를 호출하지 않기 위해 _Start에서 한 번만 구합니다.
    /// </summary>
    public EntityId colliderEntityId;
    // targetMoveTo(진형 슬롯 Transform)는 제거되었습니다.
    // 유닛은 이제 슬롯 '인덱스'만 들고, 위치는 부대의 배열에서 꺼냅니다.

    /// <summary>
    /// 배정받은 진형 슬롯의 인덱스입니다. -1이면 배정 없음입니다.
    ///
    /// 왜 Transform이 아니라 인덱스인가:
    /// Transform을 들고 있으면 위치를 읽을 때마다 네이티브 왕복이 발생합니다.
    /// 9,600명이면 그것만으로 틱당 2.955 ms입니다. 인덱스로 두면 부대가
    /// 소유한 배열에서 꺼내므로 0.029 ms로 끝나고, 무엇보다 그 배열을
    /// Burst Job에 통째로 넘길 수 있습니다. (Transform은 넘길 수 없습니다)
    /// </summary>
    public int targetSlotIndex = -1;

    /// <summary>
    /// 이번 틱에 '자기 오의 선두'를 따라가는 중인지 여부입니다.
    ///
    /// Army._Update_Leader_Follow가 매 틱 정합니다. 참이면 unit_Data.location에
    /// 선두 기준으로 계산된 목표가 이미 들어 있으므로, 아래 이동 처리가
    /// 그것을 슬롯 배열 값으로 덮어쓰면 안 됩니다.
    ///
    /// 슬롯 배정(targetSlotIndex)은 그대로 유지됩니다. 그것은 '이 유닛이
    /// 대열의 어느 자리를 맡았는가'라는 신원이고, 이 플래그는 '이번에
    /// 어디를 보고 걷는가'라는 별개의 것입니다.
    /// </summary>
    public bool bfollowLeader;
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

    // Rigidbody / CapsuleCollider / NavMeshAgent 참조는 제거되었습니다.
    //
    // 유닛은 물리 엔진도 경로탐색도 쓰지 않습니다.
    //   겹침   : Collision_Resolve_Job (격자, 질량비 반영)
    //   이동   : unit_Data.position을 시뮬레이션이 직접 계산
    //   반영   : Write_Transform_Job이 틱 마지막에 일괄 쓰기
    // 프리팹에서도 이 컴포넌트들은 이미 제거되어 있어 전부 null이었습니다.
    //
    // bbatchedTransform 스위치도 함께 사라졌습니다.
    // 이제 '시뮬레이션이 위치의 유일한 주인'이라는 규칙에 예외가 없습니다.

    /// <summary>유닛 위에 표시되는 UI를 제어하는 컴포넌트입니다.</summary>
    private UI_Unit uI_Unit;

    // Unity event functions
    /// <summary>유닛이 생성된 후 초기화하는 함수입니다.</summary>
    public void _Start(int num, int armyIndex)
    {
        enabled = false;

        unit_Data = new Unit_Data(this, num, army.army_Data, armyIndex);

        // 이 게임오브젝트의 ID를 전방 레이캐스트 조회 키로 씁니다.
        //
        // unitDataMap(EntityId -> Unit_Data)의 키로만 쓰이므로 유닛마다
        // 고유하기만 하면 되며, 콜라이더가 있을 필요는 없습니다.
        colliderEntityId = gameObject.GetEntityId();

        uI_Unit = GetComponentInChildren<UI_Unit>();
        uI_Unit._Start(army.army_Data);

        unit_Animation = GetComponentInChildren<Unit_Animation>();
        unit_Animation._Start(army, sprite_Weapon, sprite_Shield);

        sprite_Unit.sprite = army.images_Unit[Random.Range(0, army.images_Unit.Count)];

        if (army.images_Weapon.Count > 0)
            sprite_Weapon.sprite = army.images_Weapon[Random.Range(0, army.images_Weapon.Count)];
        if (army.images_Shield.Count > 0)
            sprite_Shield.sprite = army.images_Shield[Random.Range(0, army.images_Shield.Count)];

        // 몸통에 맞춰 무기·방패 위치를 보정합니다.
        //
        // 몸통을 고른 뒤에 해야 합니다. DCSS는 몸통 타일마다 손 위치가
        // 달라서 장비를 픽셀 단위로 밀어 줘야 손에 들립니다.
        // 여기서는 몸통을 무작위로 고르므로 병사마다 값이 달라집니다.
        //
        // 몸통과 크기를 함께 넘기는 이유:
        // Unit_Animation이 몸통에 크기 배율과 들어올림을 겁니다. 그런데
        // 무기와 방패가 몸통 아래에 매달렸는지 형제인지에 따라 그것을
        // 물려받는 정도가 다릅니다. 계산이 갈리므로 둘 다 필요합니다.
        // (자세한 내용은 Unit_Hand_Offset.Apply의 주석 참조)
        Unit_Hand_Offset.Apply(sprite_Unit.sprite, sprite_Unit,
                               sprite_Weapon, sprite_Shield,
                               army.army_Data.GetSize());
    }

    /// <summary>매 프레임마다 호출되어 유닛의 상태를 업데이트합니다.</summary>
    public void _Update()
    {
        _Update_Move();
        _Update_Fight();

        // 이번 틱의 접촉 정보를 소비했으므로 해제합니다.
        //
        // 물리 기반일 때: 아직 닿아 있다면 다음 물리 프레임의 OnCollisionStay가 다시 세웁니다.
        // 자체 충돌일 때: 이 직후 Controller._Update_Collision이 계산값으로 채웁니다.
        // 어느 쪽이든 여기서 한 번 비우는 것이 맞습니다.
        unit_Data.benemyContact = false;
    }

    // 물리 충돌 콜백(OnCollisionEnter/Stay/Exit)은 제거되었습니다.
    //
    // 유닛 겹침과 적 접촉 판정은 Controller._Update_Collision의 격자 계산
    // (Collision_Resolve_Job)이 전부 담당합니다. 그쪽이 benemyContact와
    // enemyContactNormal, 그리고 접촉 부대(contactArmyIndex)까지 채웁니다.
    //
    // 프리팹에서 Rigidbody와 CapsuleCollider를 뗀 이상 이 콜백들은 애초에
    // 호출되지도 않았습니다. 남겨 두면 '두 벌의 접촉 로직'이 되어
    // 신규 기능마다 양쪽을 모두 손봐야 하므로 지웁니다.

    // Public methods
    /// <summary>
    /// 유닛을 지정 위치에 배치합니다.
    ///
    /// Transform과 시뮬레이션 위치를 함께 맞춰야 합니다.
    /// Transform만 옮기면 다음 틱에 시뮬레이션이 옛 자리로 되돌리고,
    /// unit_Data만 옮기면 첫 일괄 쓰기 전까지 화면에 반영되지 않습니다.
    /// </summary>
    public void Place_At(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        // 시뮬레이션 쪽 위치도 함께 맞춥니다.
        //
        // 일괄 처리 모드에서는 unit_Data.position이 위치의 유일한 주인입니다.
        // 여기서 Transform만 옮기면 다음 틱에 시뮬레이션이 옛 자리로 되돌립니다.
        unit_Data.position = position;
        unit_Data.rotation = rotation;

        // 옮겨 놓은 자리를 '가야 할 자리'로도 삼습니다.
        //
        // 왜 필요한가:
        // 이 함수는 유닛을 순간이동시킵니다. 그런데 위치만 옮기고
        // unit_Data.location(목적지)을 그대로 두면, 시뮬레이션이 도는
        // 첫 틱에 유닛이 **옛 목표를 향해 걸어갑니다.**
        //
        // 배치 단계에서 이 증상이 특히 눈에 띕니다. 틱이 멈춰 있는 동안에는
        // 대열이 멀쩡해 보이다가, 전투가 시작되는 순간 전원이 직전 대열로
        // 되돌아가면서 진형이 한 번 뭉개졌다 펴집니다.
        // (태세를 넓히면 그 되돌아감이 '좁아지는' 모습으로 보입니다)
        //
        // 슬롯 배정도 함께 지웁니다. 남겨 두면 다음 틱에 부대 기준점 기준으로
        // 계산된 옛 슬롯을 따라가므로 같은 일이 벌어집니다.
        unit_Data.direction = rotation;
        unit_Data.btargetMoveTo = false;
        targetSlotIndex = -1;

        // location = position, 속도 0, Idle로 되돌립니다.
        // (position을 먼저 대입했으므로 여기서 제자리가 목적지가 됩니다)
        unit_Data.Move_Cancel();
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
    /// <param name="attackerArmyIndex">가해자가 속한 부대의 인덱스입니다.</param>
    public void Take_Collision_Hit(float damage, Vector3 direction, float impulse,
                                   int attackerNum, int attackerArmyIndex)
    {
        if (bDead) return;
        if (unit_Data.bdead) return;

        unit_Data.GetDamage(damage, direction, attackerNum, attackerArmyIndex);

        Apply_Knockback(direction, impulse);

        // 치인 쪽도 크게 번쩍여 무슨 일이 일어났는지 보이게 합니다.
        if (unit_Animation != null) unit_Animation.Flash_Charge_Impact(1.0f);
    }

    /// <summary>
    /// 넉백을 적용합니다.
    ///
    /// 속도 변화(impulse / mass)를 한 틱 분 이동으로 환산합니다.
    /// ForceMode.Impulse가 내부적으로 질량으로 나누는 것과 같은 계산이며,
    /// '무거울수록 덜 밀린다'는 규칙이 그대로 성립합니다.
    ///
    /// Transform이 아니라 unit_Data.position을 옮깁니다.
    /// 시뮬레이션이 위치의 유일한 주인이므로, 여기서 Transform을 건드리면
    /// 다음 틱에 되읽어야만 그 변화를 알 수 있습니다.
    /// (되읽기는 9,600유닛 기준 1.8 ms입니다)
    /// </summary>
    public void Apply_Knockback(Vector3 direction, float impulse)
    {
        if (impulse <= 0.0f) return;
        if (direction.sqrMagnitude < 0.000001f) return;

        float mass = army != null ? army.army_Data.GetMass() : 1.0f;
        if (mass <= 0.0f) mass = 1.0f;

        float step = impulse / mass * Constant.deltaTime;

        // 한 방에 날아가지 않도록 상한을 둡니다.
        if (step > Unit_Collision.maxSeparationPerTick)
            step = Unit_Collision.maxSeparationPerTick;

        unit_Data.position += direction.normalized * step;
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

        // 충돌/탐지에서 빠지는 것은 bdead 플래그 하나로 충분합니다.
        //   Collision_Grid_Build_Job  : 죽은 유닛을 격자에 넣지 않음
        //   Spatial_Grid_Build_Job    : 마찬가지
        //   Collision_Resolve_Job     : bdead면 즉시 반환
        // 접촉 카운트도 매 틱 Clear_Contact_Counts()로 다시 세워지므로
        // 사망 시점에 따로 정리할 것이 없습니다.
    }

}
