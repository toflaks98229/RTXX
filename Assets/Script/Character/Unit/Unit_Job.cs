using System;
using System.Collections;
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
    public NativeHashMap<int, Unit_Data> unitDataMap;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        Unit_Data myData = unit_Datas[index];
        RaycastHit hit = raycastHits[index];

        if (hit.colliderInstanceID != 0)
        {
            if (unitDataMap.TryGetValue(hit.colliderInstanceID, out Unit_Data hitUnitData))
            {
                if (hitUnitData.e_Unit_Fight != E_Unit_Fight.Attack_Able && myData.e_Unit_Move == E_Unit_Move.Move)
                {
                    myData.Move_Cancel();
                }
            }
        }

        myData._Update();
        unit_Datas[index] = myData;
    }
}


[BurstCompile]
public struct Unit_Fight_Job : IJobParallelFor
{
    // Public member variables
    /// <summary>유닛 데이터 배열입니다. (병렬 처리 시 제한 있음)</summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<Unit_Data> unit_Datas;
    /// <summary>공격 목표 유닛 데이터 배열입니다.</summary>
    public NativeArray<Unit_Data> target_Unit_Datas;
    /// <summary>Job 실행 시 사용되는 유닛 데이터입니다.</summary>
    Unit_Data unit_Data;
    /// <summary>피해 계산에 사용되는 랜덤 값입니다.</summary>
    public int random;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        unit_Data = unit_Datas[index];

        Update_Target();

        for (int i = 0; i < unit_Datas.Length; i++)
        {
            if (index == i)
                continue;

            if (unit_Datas[i].unit_Target_Data.num == index
                && unit_Datas[i].bhitTarget)
            {
                GetDamage(unit_Datas[i]);
            }
        }

        unit_Datas[index] = unit_Data;
    }

    // Private methods
    /// <summary>유닛의 공격 목표를 업데이트합니다.</summary>
    private void Update_Target()
    {
        for (int i = 0; i < unit_Datas.Length; i++)
        {
            unit_Data._Update_Target(unit_Datas[i]);
        }
    }

    /// <summary>유닛이 피해를 입었을 때의 로직을 처리합니다.</summary>
    private void GetDamage(Unit_Data unit_Data)
    {
        switch (unit_Data.e_Unit_AttackType)
        {
            case E_Unit_AttackType.Melee:
                GetDamage_Melee(unit_Data);
                break;
            case E_Unit_AttackType.Range:
                GetDamage_Range(unit_Data);
                break;
        }
    }

    /// <summary>근접 공격에 의한 피해를 계산하고 적용합니다.</summary>
    private void GetDamage_Melee(Unit_Data unit_Data)
    {
        float damage = 0.0f;
        float attack = unit_Data.army_Data.GetMeleeAttack();
        float defence = this.unit_Data.army_Data.GetMeleeDiffense();
        float armor = this.unit_Data.army_Data.GetArmor();
        float shieldArmor = this.unit_Data.army_Data.GetShieldArmor();

        float angle = Quaternion.Angle(this.unit_Data.rotation, unit_Data.rotation);

        if (angle > 135.0f)
        {

        }
        else if (angle < 45.0f)
        {
            defence = defence * 0.0f;
            shieldArmor = shieldArmor * 0.0f;
        }
        else
        {
            defence = defence * 0.5f;
            shieldArmor = shieldArmor * 0.0f;
        }

        if (defence + Constant.defence - attack > random)
        {
            damage = damage + unit_Data.army_Data.GetMeleeDamage();
            damage = damage - armor;
            damage = damage - shieldArmor;

            this.unit_Data.GetDamage(damage, unit_Data.rotation * Vector3.forward);
        }
    }

    /// <summary>원거리 공격에 의한 피해를 계산하고 적용합니다.</summary>
    private void GetDamage_Range(Unit_Data unit_Data)
    {
        float damage = 0.0f;
        this.unit_Data.GetDamage(damage);
    }
}


[BurstCompile]
public struct Unit_Animation_Job : IJobParallelFor
{
    // Public member variables
    /// <summary>유닛 데이터 배열입니다.</summary>
    public NativeArray<Unit_Data> unit_Datas;
    /// <summary>유닛 애니메이션 데이터 배열입니다.</summary>
    public NativeArray<Unit_Animation_Data> unit_Animation_Datas;
    /// <summary>카메라의 위치입니다.</summary>
    public Vector3 cam_Position;
    /// <summary>카메라의 회전값입니다.</summary>
    public Quaternion cam_Rotation;

    // Private member variables
    /// <summary>Job 실행 시 사용되는 유닛 데이터입니다.</summary>
    private Unit_Data unit_Data;
    /// <summary>Job 실행 시 사용되는 유닛 애니메이션 데이터입니다.</summary>
    private Unit_Animation_Data unit_Animation_Data;

    // Public methods
    /// <summary>Job의 메인 실행 함수입니다. 각 유닛별로 병렬 실행됩니다.</summary>
    public void Execute(int index)
    {
        unit_Data = unit_Datas[index];
        unit_Animation_Data = unit_Animation_Datas[index];

        unit_Animation_Data.position = unit_Data.position;
        unit_Animation_Data.rotation = unit_Data.rotation;

        unit_Animation_Data.cam_Position = cam_Position;
        unit_Animation_Data.cam_Rotation = cam_Rotation;

        unit_Animation_Data._Update();

        unit_Datas[index] = unit_Data;
        unit_Animation_Datas[index] = unit_Animation_Data;
    }
}
