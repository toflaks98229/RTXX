using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 부대 데이터를 업데이트하는 잡입니다.
/// </summary>
[BurstCompile]
public struct Army_Job : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Army_Data> army_Datas;

    /// <summary>
    /// 잡을 실행합니다.
    /// </summary>
    /// <param name="index">현재 실행 중인 인덱스입니다.</param>
    public void Execute(int index)
    {
        Army_Data army_Data = army_Datas[index];
        army_Data._Update();
        army_Datas[index] = army_Data;
    }
}

/// <summary>
/// 유닛의 통계 데이터를 업데이트하는 잡입니다.
/// </summary>
[BurstCompile]
public struct Unit_Stat_Job : IJobParallelFor
{
    public NativeArray<Unit_Data> unit_Datas;
    public Army_Data army_Data;

    /// <summary>
    /// 잡을 실행합니다.
    /// </summary>
    /// <param name="index">현재 실행 중인 인덱스입니다.</param>
    public void Execute(int index)
    {
        Unit_Data unit_Data = unit_Datas[index];
        unit_Data.army_Data = army_Data;
        unit_Datas[index] = unit_Data;
    }
}

/// <summary>
/// 진형 위치를 계산하는 잡입니다.
/// </summary>
[BurstCompile]
public struct Formation_Job : IJobParallelFor
{
    public NativeArray<Vector3> locationMoveTo;
    public int num_width;
    public Vector3 add_width;
    public Vector3 add_vertical;
    public Vector3 formation_Start;

    /// <summary>
    /// 잡을 실행하여 각 유닛의 진형 위치를 계산합니다.
    /// </summary>
    /// <param name="index">현재 실행 중인 인덱스입니다.</param>
    public void Execute(int index)
    {
        Vector3 vector3 = formation_Start;
        vector3 += add_vertical * Mathf.Floor(index / num_width);
        float num = index % num_width;

        if (num_width % 2 == 1)
        {
            if (num % 2 == 1)
            {
                vector3 += add_width * (num + 1) * 0.5f;
            }
            else if (num % 2 == 0)
            {
                vector3 -= add_width * num * 0.5f;
            }
        }
        else
        {
            if (num % 2 == 1)
            {
                vector3 += add_width * num * 0.5f;
            }
            else if (num % 2 == 0)
            {
                vector3 -= add_width * (num + 1) * 0.5f;
            }
        }

        locationMoveTo[index] = vector3;
    }
}
