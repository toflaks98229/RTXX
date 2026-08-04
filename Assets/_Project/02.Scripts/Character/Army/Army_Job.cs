using System;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 부대 상태 머신을 병렬로 갱신하는 잡입니다.
///
/// 항목이 '부대' 단위(보통 수십 개)라 유닛 잡과 성격이 다릅니다.
/// 항목 수가 적고 항목당 작업량(사기, 피로, 이동 상태 전이)이 크므로,
/// Controller는 이 잡을 작은 배치(4)로 잘게 나눠 스케줄합니다.
/// </summary>
[BurstCompile]
public struct Army_Job : IJobParallelFor
{
    // 공개 멤버 변수
    /// <summary>
    /// 갱신할 부대 데이터 배열입니다. 같은 배열을 읽고 되쓰므로
    /// 병렬 제한을 해제합니다. 각 항목은 자기 인덱스만 건드립니다.
    /// </summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<Army_Data> army_Datas;

    // 공개 메서드
    /// <summary>
    /// 부대 하나의 상태를 갱신합니다. 부대마다 병렬 실행됩니다.
    /// </summary>
    /// <param name="index">갱신할 부대의 인덱스입니다.</param>
    public void Execute(int index)
    {
        Army_Data army_Data = army_Datas[index];
        army_Data._Update();
        army_Datas[index] = army_Data;
    }
}

/// <summary>
/// 각 유닛이 설 진형 슬롯 좌표를 병렬로 계산하는 잡입니다.
///
/// 배치 규칙:
/// 중앙에서 좌우로 번갈아 채웁니다. 앞에서부터 순서대로 채우면
/// 부대가 한쪽으로 쏠리므로, 짝수/홀수 인덱스를 좌우로 갈라
/// 중앙을 기준으로 대칭이 되게 합니다. 진형 폭이 홀수냐 짝수냐에 따라
/// 중앙 기준이 달라지므로 두 경우를 나눠 계산합니다.
/// </summary>
[BurstCompile]
public struct Formation_Job : IJobParallelFor
{
    // 공개 멤버 변수
    /// <summary>계산된 슬롯 월드 좌표를 담는 배열입니다. 인덱스가 곧 슬롯 번호입니다.</summary>
    public NativeArray<Vector3> locationMoveTo;

    /// <summary>진형의 가로 인원 수(열 폭)입니다. 이 값으로 행과 열을 나눕니다.</summary>
    public int num_width;

    /// <summary>슬롯 하나만큼 옆으로 이동하는 벡터입니다. (전열축 x 간격)</summary>
    public Vector3 add_width;

    /// <summary>다음 열로 넘어갈 때 뒤로 이동하는 벡터입니다.</summary>
    public Vector3 add_vertical;

    /// <summary>진형의 기준 좌표입니다. 여기서부터 좌우로 펼칩니다.</summary>
    public Vector3 formation_Start;

    // 공개 메서드
    /// <summary>
    /// 슬롯 하나의 좌표를 계산합니다. 유닛마다 병렬 실행됩니다.
    /// </summary>
    /// <param name="index">계산할 슬롯의 인덱스입니다.</param>
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
