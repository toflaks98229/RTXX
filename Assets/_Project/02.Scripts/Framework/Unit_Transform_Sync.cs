using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

/// <summary>
/// 전 유닛의 Transform을 Job으로 일괄 읽고 쓰는 동기화 계층입니다.
///
/// 왜 필요한가:
/// Transform 접근은 C# -> 네이티브 왕복이라 호출 하나하나에 비용이 있습니다.
/// 9,600 유닛 기준으로 재 보면
///   transform.position 전수 읽기 : 6.12 ms
///   transform.rotation 전수 쓰기 : 5.97 ms
///   리스트 순회 + null 검사만    : 0.09 ms
/// 로직이 무거운 게 아니라 '만지는 행위' 자체가 134배입니다.
///
/// TransformAccessArray를 쓰면 Unity가 Transform을 내부 배치 단위로 묶어
/// 워커 스레드에서 병렬 처리합니다. 실측 4.6배(4.45 ms -> 0.97 ms).
///
/// 사용 규약:
///   틱 시작 : Read_Positions()로 현재 위치를 배열에 담습니다.
///   틱 중간 : 시뮬레이션은 배열만 읽고 씁니다. Transform을 직접 만지지 않습니다.
///   틱 마지막: Write_Transforms()로 위치/회전을 한 번에 되돌려 씁니다.
/// </summary>
public class Unit_Transform_Sync
{
    /// <summary>동기화 대상 Transform들입니다.</summary>
    private TransformAccessArray transforms;

    /// <summary>유닛 본체 위치입니다. Read_Positions가 채웁니다.</summary>
    public NativeArray<Vector3> positions;
    /// <summary>유닛 본체 회전입니다. Write_Transforms가 소비합니다.</summary>
    public NativeArray<Quaternion> rotations;

    /// <summary>현재 등록된 유닛 수입니다.</summary>
    public int Count => transforms.isCreated ? transforms.length : 0;

    /// <summary>배열이 준비되었는지 여부입니다.</summary>
    public bool IsCreated => transforms.isCreated && positions.IsCreated;

    /// <summary>
    /// 유닛 목록으로 동기화 배열을 다시 만듭니다.
    ///
    /// 중요: 인덱스는 units 리스트와 1:1로 맞춥니다.
    /// 죽어서 null이 된 자리도 건너뛰지 않고 유지해야
    /// Controller의 bodies[i]와 positions[i]가 같은 유닛을 가리킵니다.
    /// 파괴된 Transform은 TransformAccessArray가 자동으로 건너뜁니다.
    /// </summary>
    public void Rebuild(System.Collections.Generic.List<Unit> units)
    {
        Dispose();

        int n = units.Count;
        if (n == 0) return;

        var array = new Transform[n];
        for (int i = 0; i < n; i++)
        {
            // null 유닛은 null Transform으로 둡니다.
            // TransformAccessArray는 null 항목의 Execute를 호출하지 않습니다.
            array[i] = units[i] != null ? units[i].transform : null;
        }

        transforms = new TransformAccessArray(array);
        positions = new NativeArray<Vector3>(n, Allocator.Persistent);
        rotations = new NativeArray<Quaternion>(n, Allocator.Persistent);
    }

    /// <summary>Transform에서 현재 위치를 일괄로 읽어 옵니다.</summary>
    public void Read_Positions()
    {
        if (!IsCreated) return;

        new Read_Position_Job { positions = positions }
            .Schedule(transforms).Complete();
    }

    /// <summary>계산된 위치와 회전을 Transform에 일괄로 씁니다.</summary>
    public void Write_Transforms()
    {
        if (!IsCreated) return;

        new Write_Transform_Job { positions = positions, rotations = rotations }
            .Schedule(transforms).Complete();
    }

    /// <summary>네이티브 자원을 반납합니다.</summary>
    public void Dispose()
    {
        if (transforms.isCreated) transforms.Dispose();
        if (positions.IsCreated) positions.Dispose();
        if (rotations.IsCreated) rotations.Dispose();
    }
}

/// <summary>Transform 위치를 배열로 읽어 오는 잡입니다.</summary>
[BurstCompile]
public struct Read_Position_Job : IJobParallelForTransform
{
    [WriteOnly] public NativeArray<Vector3> positions;

    public void Execute(int index, TransformAccess transform)
    {
        positions[index] = transform.position;
    }
}

/// <summary>배열의 위치와 회전을 Transform에 쓰는 잡입니다.</summary>
[BurstCompile]
public struct Write_Transform_Job : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<Quaternion> rotations;

    public void Execute(int index, TransformAccess transform)
    {
        transform.position = positions[index];
        transform.rotation = rotations[index];
    }
}
