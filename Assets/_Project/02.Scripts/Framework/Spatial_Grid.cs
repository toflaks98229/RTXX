using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// 유닛 위치를 2D 격자에 색인해 "가까운 유닛만" 빠르게 찾도록 돕는 공간 분할 유틸리티입니다.
///
/// 배경:
/// 원래 Unit_Fight_Job은 내 유닛 하나마다 적 유닛 '전부'를 두 번 순회했습니다.
/// 300 대 300 교전이면 부대당 18만 번의 거리 계산이 매 틱 발생합니다. (O(N*M))
/// 격자를 쓰면 각 유닛은 자기 셀과 인접 8칸만 보면 되므로,
/// 밀도가 균일할 때 사실상 O(N * k) (k = 셀당 평균 유닛 수)로 떨어집니다.
///
/// 정확성 조건:
/// cellSize는 '상호작용 최대 거리' 이상이어야 합니다. 그래야 3x3 이웃 탐색이
/// 사거리 안의 모든 유닛을 빠짐없이 포함합니다. 이 조건이 깨지면 사거리 안인데도
/// 탐지되지 않는 유닛이 생깁니다.
/// </summary>
public static class Spatial_Grid
{
    /// <summary>셀 크기가 0에 수렴해 좌표가 폭주하는 것을 막는 하한입니다.</summary>
    public const float minCellSize = 0.5f;

    /// <summary>월드 좌표를 격자 좌표(x, z)로 변환합니다. y축은 무시합니다.</summary>
    public static void ToCell(Vector3 position, float cellSize, out int cellX, out int cellZ)
    {
        cellX = Mathf.FloorToInt(position.x / cellSize);
        cellZ = Mathf.FloorToInt(position.z / cellSize);
    }

    /// <summary>
    /// 격자 좌표를 해시 키로 변환합니다.
    /// 큰 소수를 곱해 XOR하는 표준적인 공간 해시 방식입니다.
    /// </summary>
    public static int Hash(int cellX, int cellZ)
    {
        unchecked
        {
            return (cellX * 73856093) ^ (cellZ * 19349663);
        }
    }

    /// <summary>월드 좌표를 바로 해시 키로 변환합니다.</summary>
    public static int Hash(Vector3 position, float cellSize)
    {
        ToCell(position, cellSize, out int cellX, out int cellZ);
        return Hash(cellX, cellZ);
    }

    /// <summary>
    /// 두 부대의 사거리를 모두 감안한 안전한 셀 크기를 계산합니다.
    /// 공격/피격 양방향을 모두 3x3 탐색으로 처리하려면 가장 긴 사거리가 기준이어야 합니다.
    /// </summary>
    public static float GetCellSize(in Army_Data a, in Army_Data b)
    {
        float maxRange = Mathf.Max(GetReach(a), GetReach(b));
        return Mathf.Max(maxRange, minCellSize);
    }

    /// <summary>
    /// 한 부대가 실제로 닿을 수 있는 최대 거리입니다.
    /// 원거리 사거리는 '실제로 원거리 공격이 가능한 부대'만 반영합니다.
    /// 근접 부대에까지 원거리 사거리를 적용하면 셀이 불필요하게 커져
    /// 격자를 쓰는 의미가 사라집니다. (_Update_Target의 판정 조건과 동일하게 맞춥니다)
    /// </summary>
    private static float GetReach(in Army_Data army)
    {
        float reach = army.GetMeleeRange();

        if (army.GetE_Unit_AttackType() == E_Unit_AttackType.Range || army.IsRangeAttackAble())
        {
            reach = Mathf.Max(reach, army.GetRangeRange());
        }

        return reach;
    }
}

/// <summary>
/// 유닛 배열을 공간 격자에 색인하는 잡입니다.
/// 사망한 유닛은 색인하지 않아 탐색 대상에서 자연스럽게 빠집니다.
/// </summary>
[BurstCompile]
public struct Spatial_Grid_Build_Job : IJobParallelFor
{
    /// <summary>색인할 유닛 데이터입니다.</summary>
    [ReadOnly]
    public NativeArray<Unit_Data> unit_Datas;
    /// <summary>격자 셀 크기입니다.</summary>
    public float cellSize;
    /// <summary>해시 키 -> 유닛 인덱스 맵입니다.</summary>
    public NativeParallelMultiHashMap<int, int>.ParallelWriter grid;

    public void Execute(int index)
    {
        Unit_Data unit_Data = unit_Datas[index];

        // 죽은 유닛은 색인에서 제외합니다.
        if (unit_Data.bdead) return;

        grid.Add(Spatial_Grid.Hash(unit_Data.position, cellSize), index);
    }
}
