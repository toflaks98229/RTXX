using System;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct Unit_Job : IJobParallelFor
{
    // Public member variables
    /// <summary>유닛 데이터 배열입니다. (병렬 처리 시 제한 있음)</summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<Unit_Data> unit_Datas;
    /// <summary>레이캐스트 충돌 결과 배열입니다. (읽기 전용)</summary>
    [ReadOnly]
    public NativeArray<RaycastHit> raycastHits;
    /// <summary>유닛 데이터를 조회하기 위한 해시맵입니다. (읽기 전용)</summary>
    [ReadOnly]
    public NativeHashMap<EntityId, Unit_Data> unitDataMap;
    /// <summary>이 부대의 스탯입니다. 소속 유닛 전체가 공유합니다.</summary>
    [ReadOnly]
    public Army_Data armyData;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        Unit_Data myData = unit_Datas[index];
        RaycastHit hit = raycastHits[index];

        // 전방에 이미 교전 중인 유닛이 있으면 멈춰 서서
        // 난전 덩어리를 뒤에서 밀고 들어가지 않도록 합니다.
        //
        // 단, 돌격 중에는 적용하지 않습니다.
        // 레이캐스트 거리가 5m라서 돌격이 적 전열에 닿기 직전에 걸리는데,
        // Move_Cancel은 상태를 Idle로 바꿔 버립니다. 그러면 Accelerate가
        // 아예 호출되지 않아 돌격 가속 로직이 통째로 배제되고
        // currentMoveSpeed가 0으로 굳어 충돌 직전에 급정거합니다.
        if (!myData.bcharging)
        {
            // Unity 6부터 InstanceID 계열 API가 EntityId로 대체되었습니다.
            EntityId hitColliderID = hit.colliderEntityId;

            if (hitColliderID != EntityId.None)
            {
                if (unitDataMap.TryGetValue(hitColliderID, out Unit_Data hitUnitData))
                {
                    if (hitUnitData.e_Unit_Fight != E_Unit_Fight.Attack_Able
                        && myData.e_Unit_Move == E_Unit_Move.Move)
                    {
                        myData.Move_Cancel();
                    }
                }
            }
        }

        myData._Update(armyData);
        unit_Datas[index] = myData;
    }
}


[BurstCompile]
public struct Unit_Fight_Job : IJobParallelFor
{
    // Public member variables
    /// <summary>아군(자기 부대) 유닛 데이터 배열입니다. index로 접근하는 대상입니다.</summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<Unit_Data> unit_Datas;
    /// <summary>적 부대의 유닛 데이터 배열입니다. 타겟 탐색과 피격 판정의 원본입니다. (읽기 전용)</summary>
    [ReadOnly]
    public NativeArray<Unit_Data> target_Unit_Datas;
    /// <summary>
    /// 이번에 처리 중인 '내 유닛'입니다. 피해를 받는 쪽이므로 방어자입니다.
    ///
    /// 이름 주의: 예전에는 이 필드도 unit_Data였고, 피해 계산 메서드의
    /// 파라미터(공격자)도 unit_Data였습니다. 파라미터가 필드를 가려
    /// 같은 이름이 문맥에 따라 방어자/공격자를 뜻했습니다.
    /// 실제로 아래 한 줄이 두 의미를 동시에 씁니다.
    ///   Quaternion.Angle(this.unit_Data.rotation, unit_Data.rotation)
    /// 방어/공격을 뒤집어도 컴파일되므로, 이름으로 갈라 두는 편이 안전합니다.
    /// </summary>
    Unit_Data defender;
    /// <summary>내 부대의 스탯입니다. (방어 측 계산에 사용)</summary>
    [ReadOnly]
    public Army_Data armyData;
    /// <summary>적 부대의 스탯입니다. (공격 측 계산에 사용)</summary>
    [ReadOnly]
    public Army_Data targetArmyData;
    /// <summary>
    /// 적 유닛의 공간 격자입니다. 해시 키 -> target_Unit_Datas 인덱스.
    /// 사거리 안의 적만 추려내기 위해 사용합니다. (읽기 전용)
    /// </summary>
    [ReadOnly]
    public NativeParallelMultiHashMap<int, int> targetGrid;
    /// <summary>격자 셀 크기입니다. 반드시 최대 상호작용 사거리 이상이어야 합니다.</summary>
    public float cellSize;
    /// <summary>
    /// 이번 틱의 난수 시드입니다. 유닛 인덱스와 섞어 '유닛마다 다른' 난수를 만듭니다.
    ///
    /// 예전에는 int 하나를 메인 스레드에서 뽑아 부대 전원이 공유했습니다.
    /// 그러면 300명이 같은 주사위를 굴리는 셈이라 명중이 전원 동시에 일어나고,
    /// 방어구 랜덤 감산 같은 확률 요소가 아예 성립하지 않습니다.
    /// </summary>
    public uint randomSeed;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        defender = unit_Datas[index];

        // 사망한 유닛은 타겟을 잡지도, 피해를 받지도 않습니다.
        if (defender.bdead) return;

        // 후보를 '전부 훑어본 뒤' 가장 좋은 하나를 고릅니다.
        // 예전에는 사거리 안에서 처음 만난 적을 그냥 잡았는데,
        // 격자 순회 순서가 해시 버킷 순서라 사실상 무작위였습니다.
        // 그 결과 옆이나 뒤의 적을 붙잡고 자기 대열을 가로질러 이동했습니다.
        int bestIndex = -1;
        float bestScore = float.MaxValue;

        /// 동점일 때 표적을 고르는 기준입니다. 자세한 이유는 아래 2번 주석을 보십시오.
        int bestNum = int.MaxValue;

        bool btargetStillValid = false;

        // 이 유닛 전용 난수입니다. 명중 판정과 방어구 굴림이 유닛마다 달라야
        // 전투가 확률적으로 흐릅니다. (전원 공유 시드로는 성립하지 않습니다)
        Unity.Mathematics.Random random = Make_Random(index);

        Spatial_Grid.ToCell(defender.position, cellSize, out int cellX, out int cellZ);

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int hash = Spatial_Grid.Hash(cellX + dx, cellZ + dz);

                if (!targetGrid.TryGetFirstValue(hash, out int enemyIndex, out var it))
                    continue;

                do
                {
                    Unit_Data enemy = target_Unit_Datas[enemyIndex];

                    if (enemy.bdead) continue;

                    // 1. 지금 잡고 있는 타겟이면 위치를 갱신합니다.
                    if (defender.btarget && enemy.num == defender.unit_Target_Data.num)
                    {
                        if (defender.Refresh_Target(enemy, armyData))
                            btargetStillValid = true;
                    }

                    // 2. 더 좋은 표적인지 평가합니다. (정면 우선)
                    //
                    // 동점 처리 주의:
                    // 격자 순회 순서는 NativeParallelMultiHashMap의 버킷 순서이고,
                    // 그 순서는 '어느 워커 스레드가 먼저 넣었는가'에 좌우됩니다.
                    // 즉 실행할 때마다 달라집니다.
                    //
                    // 그래서 점수가 같을 때 '먼저 만난 쪽'을 고르면 표적이
                    // 실행마다 갈리고, 그 차이가 전투 전체로 번집니다.
                    // 유닛 고유 번호가 작은 쪽으로 고정하면 순회 순서와
                    // 무관하게 언제나 같은 표적이 선택됩니다.
                    float score = defender.Get_Target_Score(enemy, armyData);

                    bool bbetter = score < bestScore
                                || (score == bestScore && enemy.num < bestNum);

                    if (bbetter)
                    {
                        bestScore = score;
                        bestIndex = enemyIndex;
                        bestNum = enemy.num;
                    }

                    // 3. 저 적이 나를 노리고 이번 틱에 타격을 성립시켰다면 피해를 받습니다.
                    //
                    // 죽어도 여기서 빠져나가지 않습니다.
                    //
                    // 예전에는 사망 즉시 return했습니다. 그러면 같은 틱에 나를 친
                    // 나머지 공격들이 '누가 먼저 순회되었는가'에 따라 버려집니다.
                    // 격자 순회 순서는 실행마다 달라지므로 그 자체가 비결정론입니다.
                    //
                    // 이제는 모든 공격을 끝까지 받습니다. GetDamage는 bdead면
                    // 즉시 반환하므로 HP가 더 깎이지는 않고, 킬 기여자도
                    // 첫 치명타를 낸 쪽으로 이미 확정되어 바뀌지 않습니다.
                    // 결과는 같으면서 순서 의존성만 사라집니다.
                    if (enemy.bhitTarget && enemy.unit_Target_Data.num == defender.num)
                    {
                        // 난수는 '이 공격 한 건'에 종속된 독립 스트림에서 뽑습니다.
                        //
                        // 왜 공용 random을 쓰면 안 되는가:
                        // 하나의 스트림을 이어 쓰면 '몇 번째로 처리된 공격인가'가
                        // 뽑히는 값을 바꿉니다. 순회 순서는 실행마다 달라지므로
                        // 같은 공격이 어떤 실행에서는 명중하고 다른 실행에서는
                        // 빗나갑니다.
                        //
                        // 공격자와 방어자의 고유 번호로 시드를 만들면, 그 공격의
                        // 판정은 순서와 무관하게 언제나 같은 결과가 됩니다.
                        var attackRandom = Make_Attack_Random(enemy.num);
                        GetDamage(enemy, ref attackRandom);
                    }
                }
                while (targetGrid.TryGetNextValue(out enemyIndex, ref it));
            }
        }

        // 기존 타겟이 여전히 유효하면 유지합니다. (매 틱 표적이 바뀌면 전투가 산만해집니다)
        if (!btargetStillValid)
        {
            if (bestIndex >= 0)
            {
                defender.Set_Target(target_Unit_Datas[bestIndex], armyData);
            }
            else if (defender.btarget)
            {
                // 사거리 안에 아무도 없습니다.
                defender.Lose_Target();
            }
        }

        unit_Datas[index] = defender;
    }

    /// <summary>
    /// 이 유닛 전용 난수 생성기를 만듭니다.
    ///
    /// 시드에 유닛 고유 번호를 섞어야 같은 틱에도 유닛마다 다른 값이 나옵니다.
    /// Unity.Mathematics.Random은 시드가 0이면 예외를 던지므로 반드시 걸러냅니다.
    /// </summary>
    private Unity.Mathematics.Random Make_Random(int index)
    {
        uint seed = randomSeed ^ (uint)((index + 1) * 2654435761u);
        if (seed == 0u) seed = 1u;

        return new Unity.Mathematics.Random(seed);
    }

    /// <summary>
    /// 공격 한 건에 대한 독립 난수 생성기를 만듭니다.
    ///
    /// 시드가 (틱, 부대, 방어자, 공격자)의 함수이므로, 이 공격의 명중과
    /// 방어구 굴림은 순회 순서와 무관하게 언제나 같은 결과를 냅니다.
    /// 하나의 스트림을 이어 쓰면 '몇 번째로 처리되었는가'가 결과를 바꿉니다.
    /// </summary>
    /// <param name="attackerNum">공격자의 전역 고유 번호입니다.</param>
    private Unity.Mathematics.Random Make_Attack_Random(int attackerNum)
    {
        unchecked
        {
            uint seed = randomSeed;

            seed ^= (uint)(defender.num + 1) * 2654435761u;
            seed ^= (uint)(attackerNum + 1) * 2246822519u;

            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;

            return new Unity.Mathematics.Random(seed != 0u ? seed : 1u);
        }
    }

    /// <summary>
    /// 나(defender)가 공격자에게 맞았을 때의 피해를 처리합니다.
    /// 공격 타입은 '공격자가 무엇으로 쳤는가'이므로 attacker에서 읽습니다.
    /// </summary>
    private void GetDamage(in Unit_Data attacker, ref Unity.Mathematics.Random random)
    {
        switch (attacker.e_Unit_AttackType)
        {
            case E_Unit_AttackType.Melee:
                GetDamage_Melee(attacker, ref random);
                break;
            case E_Unit_AttackType.Range:
                GetDamage_Range(attacker, ref random);
                break;
        }
    }

    /// <summary>
    /// 방어구에 의한 기본 피해 감산율(0~1)을 굴립니다.
    ///
    /// 고정 감산이 아니라 랜덤인 이유:
    /// 고정이면 방어구가 공격력을 넘는 순간 항상 최소 피해로 바닥을 쳐서
    /// 교전이 교착에 빠집니다. 랜덤이면 매 타격에 변동이 생겨 전투가 흐릅니다.
    /// </summary>
    private static float Roll_Armour_Reduction(float armour, ref Unity.Mathematics.Random random)
    {
        if (armour <= 0.0f) return 0.0f;

        float roll = random.NextFloat(armour * Constant.armour_Roll_Min_Rate, armour);

        float reduction = roll / Constant.armour_Full_Block;
        if (reduction < 0.0f) reduction = 0.0f;
        if (reduction > 1.0f) reduction = 1.0f;

        return reduction;
    }

    /// <summary>
    /// 공격이 내 정면에서 들어왔는지 판정합니다.
    /// 돌격 저항은 정면에서만 성립합니다. 뒤에서 찔린 창벽은 아무것도 막지 못합니다.
    /// </summary>
    private bool Is_Front_Attack(in Unit_Data attacker)
    {
        Vector3 toAttacker = attacker.position - defender.position;
        toAttacker.y = 0.0f;

        if (toAttacker.sqrMagnitude < 0.000001f) return true;

        Vector3 forward = defender.rotation * Vector3.forward;

        return Vector3.Dot(forward.normalized, toAttacker.normalized)
               >= Constant.stance_Front_Dot;
    }

    /// <summary>명중 확률을 상하한으로 클램프합니다. 절대 무적과 확정타를 모두 막습니다.</summary>
    private static float Clamp_Hit_Chance(float hitChance)
    {
        if (hitChance < Constant.hit_Chance_Min) return Constant.hit_Chance_Min;
        if (hitChance > Constant.hit_Chance_Max) return Constant.hit_Chance_Max;
        return hitChance;
    }

    /// <summary>
    /// 근접 공격에 의한 피해를 계산하고 적용합니다.
    ///
    /// 방향 주의: armyData가 방어자(나)의 부대, targetArmyData가 공격자의 부대입니다.
    /// 이 Job은 '내가 맞는 쪽'을 계산하므로 두 이름의 의미가 뒤집혀 보입니다.
    /// </summary>
    /// <param name="attacker">나를 친 적 유닛입니다.</param>
    private void GetDamage_Melee(in Unit_Data attacker, ref Unity.Mathematics.Random random)
    {
        // 피로도: 지친 쪽은 덜 맞히고 덜 아프게 때립니다.
        // 공격 측은 적 부대(targetArmyData), 방어 측은 내 부대(armyData) 기준입니다.
        float attackerFatigue = targetArmyData.GetFatigueRate();
        float defenderFatigue = armyData.GetFatigueRate();

        // 돌격 보너스: 충돌 직후 최대이며 시간에 따라 사라집니다.
        // 지친 부대의 돌격은 위력이 떨어집니다.
        float chargeBonus = attacker.chargeBonus
                            * targetArmyData.GetMeleeChargeBonus()
                            * attackerFatigue;

        // 돌격 저항: 방패벽/창벽은 정면으로 들어온 돌격을 받아냅니다.
        //
        // 창을 세운 대열에 말을 몰아넣으면 돌격 보너스가 통째로 사라집니다.
        // 이것이 '기병은 창병 정면을 피해야 한다'는 규칙의 실체입니다.
        // 단, 측후방으로 돌아 들어온 돌격은 창벽으로도 막지 못합니다.
        if (chargeBonus > 0.0f)
        {
            float resist = armyData.GetChargeResistance();

            if (resist > 0.0f && Is_Front_Attack(attacker))
            {
                chargeBonus *= (1.0f - resist);
                if (chargeBonus < 0.0f) chargeBonus = 0.0f;
            }
        }

        // 상성 보너스: 공격자가 '내 병종'을 상대로 갖는 보너스입니다.
        // 창병이 기병을 막고, 기병이 보병을 휩쓰는 가위바위보가 여기서 나옵니다.
        // (여기서 targetArmyData는 공격자, armyData는 방어자인 나입니다)
        float bonusVsTarget = targetArmyData.GetBonusVsTarget(armyData);

        // 고지에서 내려치는 쪽이 유리합니다. (공격자 기준 보정)
        float attack = (targetArmyData.GetMeleeAttack()
                        + chargeBonus
                        + bonusVsTarget
                        + targetArmyData.GetHighGroundAttack())
                       * attackerFatigue;

        // 지치면 방어 자세도 무너집니다.
        // 창벽은 창이 앞을 가려 근접 방어가 올라갑니다.
        float defence = (armyData.GetMeleeDiffense() + armyData.GetStanceMeleeDefence())
                        * defenderFatigue;
        float armor = armyData.GetArmor();
        float shieldArmor = armyData.GetShieldArmor();

        // 방어자와 공격자의 정면이 이루는 각도로 피격 방향을 판정합니다.
        // 두 유닛이 마주 볼수록(180도에 가까울수록) 정면 교전입니다.
        float angle = Quaternion.Angle(defender.rotation, attacker.rotation);

        if (angle > Constant.hit_Angle_Front)
        {
            // 정면: 방어력과 방패를 온전히 활용합니다.
        }
        else if (angle < Constant.hit_Angle_Back)
        {
            // 후방: 방어 자세를 갖추지 못해 방어력과 방패가 모두 무력화됩니다.
            defence = 0.0f;
            shieldArmor = 0.0f;
        }
        else
        {
            // 측면: 방어력이 깎이고 방패는 소용이 없습니다.
            defence = defence * Constant.hit_Side_Defence_Rate;
            shieldArmor = 0.0f;
        }

        // 명중 판정: 공격력이 높고 방어력이 낮을수록 잘 맞습니다.
        // (원래는 defence + base - attack 이어서 방어력이 높을수록 더 맞는 역전 구조였습니다)
        //
        // 상하한을 두는 이유: 방어력이 아무리 높아도 절대 무적이 되지 않고,
        // 공격력이 아무리 높아도 확정타가 되지 않습니다. 극단적인 스탯 조합에서도
        // 전투가 성립하도록 보장하는 장치입니다.
        float hitChance = Clamp_Hit_Chance(Constant.hit_Chance_Base + attack - defence);

        if (hitChance <= random.NextFloat(0.0f, 100.0f)) return;

        // 피해량은 두 갈래로 나뉩니다.
        //   기본 피해 : 방어구가 감산합니다. 감산율은 랜덤입니다.
        //   AP 피해   : 방어구를 무시하고 그대로 들어갑니다.
        //
        // 이 분리가 병종 상성의 근간입니다. 중장갑을 뚫으려면 AP가 필요하므로
        // 도끼/망치가 검보다 중장갑에 강해지는 구조가 자연히 나옵니다.
        //
        // 피로는 '명중률과 방어 자세'에만 적용하고 피해량에는 적용하지 않습니다.
        // 방어구 감산 앞에서 피해를 깎으면 효과가 증폭되어,
        // 양쪽이 지치는 순간 서로 아무도 죽일 수 없는 교착에 빠집니다.
        float reduction = Roll_Armour_Reduction(armor + shieldArmor, ref random);

        float baseDamage = (targetArmyData.GetMeleeDamage() + chargeBonus) * (1.0f - reduction);
        float apDamage = targetArmyData.GetMeleeDamageAP();

        float damage = baseDamage + apDamage;
        if (damage < Constant.damage_Min) damage = Constant.damage_Min;

        defender.GetDamage(damage, attacker.rotation * Vector3.forward,
                           attacker.num, attacker.armyIndex);
    }

    /// <summary>
    /// 원거리 공격에 의한 피해를 계산하고 적용합니다.
    /// armyData가 방어자(나), targetArmyData가 공격자의 부대입니다.
    /// </summary>
    /// <param name="attacker">나를 쏜 적 유닛입니다.</param>
    private void GetDamage_Range(in Unit_Data attacker, ref Unity.Mathematics.Random random)
    {
        float attackerFatigue = targetArmyData.GetFatigueRate();

        float bonusVsTarget = targetArmyData.GetBonusVsTarget(armyData);

        float attack = (targetArmyData.GetRangeAccuracy()
                        + bonusVsTarget
                        + targetArmyData.GetHighGroundAttack())
                       * attackerFatigue;

        // 방패벽은 화살을 막아내고, 창벽은 밀집해 있어 오히려 잘 맞습니다.
        float defence = armyData.GetRangeDiffense() + armyData.GetStanceRangeDefence();

        float hitChance = Clamp_Hit_Chance(Constant.hit_Chance_Base + attack - defence);

        if (hitChance <= random.NextFloat(0.0f, 100.0f)) return;

        // 원거리는 방패로도 막습니다. 방패는 화살을 받아내는 물건이기 때문입니다.
        // 석궁이나 총처럼 갑옷을 뚫는 무기는 rangeDamageAP로 표현합니다.
        float reduction = Roll_Armour_Reduction(
            armyData.GetArmor() + armyData.GetShieldArmor(), ref random);

        float baseDamage = targetArmyData.GetRangeDamage() * (1.0f - reduction);
        float apDamage = targetArmyData.GetRangeDamageAP();

        float damage = baseDamage + apDamage;
        if (damage < Constant.damage_Min) damage = Constant.damage_Min;

        defender.GetDamage(damage, attacker.rotation * Vector3.forward,
                           attacker.num, attacker.armyIndex);
    }
}


/// <summary>
/// 애니메이션이 필요로 하는 유닛 자세만 담은 최소 구조체입니다.
///
/// 왜 Unit_Data를 통째로 넘기지 않는가:
/// 예전에는 Unit_Animation_Job이 unit_Datas 배열 전체를 받았습니다.
/// 그런데 실제로 읽는 것은 position과 rotation 두 개뿐이고, 쓰지는
/// 않습니다. 그럼에도 같은 배열을 건드린다는 이유로 Unity의 Job 안전
/// 시스템이 Unit_Fight_Job과의 병렬 실행을 막았습니다.
///
/// 그 결과 한 부대 안에서 Job 5개가 직렬로 이어졌고, 메인 스레드가
/// 그 체인을 기다리는 시간이 틱의 46%(14.70ms)를 차지했습니다.
///
/// 읽는 것만 따로 떼어 내면 두 Job이 서로 다른 메모리를 보게 되어
/// 겹쳐 실행할 수 있습니다. 264바이트 대신 28바이트만 복사하므로
/// 캐시 효율도 함께 좋아집니다.
/// </summary>
public struct Unit_Pose
{
    public Vector3 position;
    public Quaternion rotation;
}

/// <summary>
/// 유닛 자세를 애니메이션용 배열로 추려내는 잡입니다.
///
/// Unit_Job이 끝난 직후에 돌며, 이후 애니메이션 계산은 이 배열만 보므로
/// 전투 Job과 독립적으로 병렬 실행됩니다.
/// </summary>
[BurstCompile]
public struct Unit_Pose_Extract_Job : IJobParallelFor
{
    [ReadOnly] public NativeArray<Unit_Data> unit_Datas;
    [WriteOnly] public NativeArray<Unit_Pose> poses;

    public void Execute(int index)
    {
        Unit_Data data = unit_Datas[index];

        poses[index] = new Unit_Pose
        {
            position = data.position,
            rotation = data.rotation
        };
    }
}

[BurstCompile]
public struct Unit_Animation_Job : IJobParallelFor
{
    // Public member variables
    /// <summary>
    /// 유닛 자세 배열입니다. (읽기 전용)
    ///
    /// unit_Datas가 아니라 이 배열을 받는 것이 핵심입니다.
    /// 전투 Job이 unit_Datas에 쓰는 동안에도 이 Job은 함께 돌 수 있습니다.
    /// </summary>
    [ReadOnly]
    public NativeArray<Unit_Pose> poses;
    /// <summary>유닛 애니메이션 데이터 배열입니다.</summary>
    public NativeArray<Unit_Animation_Data> unit_Animation_Datas;
    /// <summary>카메라의 위치입니다.</summary>
    public Vector3 cam_Position;
    /// <summary>카메라의 회전값입니다.</summary>
    public Quaternion cam_Rotation;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        Unit_Pose pose = poses[index];
        Unit_Animation_Data data = unit_Animation_Datas[index];

        data.position = pose.position;
        data.rotation = pose.rotation;

        data.cam_Position = cam_Position;
        data.cam_Rotation = cam_Rotation;

        data._Update();

        // 예전에는 여기서 unit_Datas[index]에 '바뀌지도 않은' 값을 되썼습니다.
        // 그 쓰기 하나 때문에 Job 안전 시스템이 이 Job을 전투 Job과
        // 직렬화했습니다. 읽기만 하므로 되쓸 이유가 없습니다.
        unit_Animation_Datas[index] = data;
    }
}
