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
    /// <summary>스프라이트(자식) Transform들입니다.</summary>
    private TransformAccessArray spriteTransforms;

    /// <summary>유닛 본체 위치입니다. Read_Positions가 채웁니다.</summary>
    public NativeArray<Vector3> positions;
    /// <summary>유닛 본체 회전입니다. Write_Transforms가 소비합니다.</summary>
    public NativeArray<Quaternion> rotations;

    /// <summary>스프라이트 회전입니다. (카메라를 향한 자세)</summary>
    public NativeArray<Quaternion> spriteRotations;
    /// <summary>스프라이트 스케일입니다. (피격 반동 포함)</summary>
    public NativeArray<Vector3> spriteScales;
    /// <summary>스프라이트 로컬 위치입니다. (공격 내지르기 포함)</summary>
    public NativeArray<Vector3> spriteLocalPositions;

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
    ///
    /// 비용 주의: 이 함수는 전 유닛을 다시 등록하므로 비쌉니다.
    /// 유닛이 죽을 때마다 부르면 전투 중 프레임이 크게 튑니다.
    /// (실측: 사망이 있는 틱이 40~60 ms까지 치솟았습니다)
    /// 그래서 Controller는 사망 자리만 Remove_At으로 떼어내고,
    /// 정말 필요할 때만 이 함수를 부릅니다.
    /// </summary>
    public void Rebuild(System.Collections.Generic.List<Unit> units)
    {
        Dispose();

        int n = units.Count;
        if (n == 0) return;

        // units와 같은 길이로 만듭니다.
        // 죽어서 null이 된 자리도 건너뛰지 않고 유지해야
        // Controller의 bodies[i]와 positions[i]가 같은 유닛을 가리킵니다.
        var array = new Transform[n];
        var sprites = new Transform[n];

        for (int i = 0; i < n; i++)
        {
            // null 유닛은 null Transform으로 둡니다.
            // TransformAccessArray는 null 항목의 Execute를 호출하지 않습니다.
            Unit u = units[i];
            array[i] = u != null ? u.transform : null;
            sprites[i] = (u != null && u.unit_Animation != null)
                ? u.unit_Animation.transform : null;
        }

        transforms = new TransformAccessArray(array);
        spriteTransforms = new TransformAccessArray(sprites);

        positions = new NativeArray<Vector3>(n, Allocator.Persistent);
        rotations = new NativeArray<Quaternion>(n, Allocator.Persistent);

        spriteRotations = new NativeArray<Quaternion>(n, Allocator.Persistent);
        spriteScales = new NativeArray<Vector3>(n, Allocator.Persistent);
        spriteLocalPositions = new NativeArray<Vector3>(n, Allocator.Persistent);
    }

    /// <summary>
    /// 한 자리의 Transform 참조만 비웁니다.
    ///
    /// 유닛이 죽으면 그 Transform이 파괴되어 Job이 예외를 던집니다.
    /// 전체를 다시 만드는 대신 그 칸만 null로 바꾸면 O(1)로 끝납니다.
    /// 배열 길이와 인덱스 정렬은 그대로 유지됩니다.
    /// </summary>
    public void Clear_At(int index)
    {
        if (!transforms.isCreated) return;
        if (index < 0 || index >= transforms.length) return;

        transforms[index] = null;

        if (spriteTransforms.isCreated && index < spriteTransforms.length)
            spriteTransforms[index] = null;
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

    /// <summary>
    /// 본체와 스프라이트를 한 번에 씁니다.
    ///
    /// 왜 합치는가:
    /// Job 하나를 걸고 기다릴 때마다 스케줄 등록과 워커 동기화 비용이 붙습니다.
    /// 9,600유닛 기준으로 재 보면 실제 계산보다 그 왕복이 더 컸습니다.
    ///   Write_Transforms 1.88 ms + Write_Sprites 1.39 ms = 3.27 ms
    /// 두 Job은 서로 다른 Transform 집합을 건드리므로 의존성이 없습니다.
    /// 따라서 둘 다 걸어 두고 마지막에 한 번만 기다리면 됩니다.
    /// </summary>
    public void Write_All()
    {
        if (!IsCreated) return;

        JobHandle a = new Write_Transform_Job
        {
            positions = positions,
            rotations = rotations
        }.Schedule(transforms);

        if (!spriteTransforms.isCreated || !spriteRotations.IsCreated)
        {
            a.Complete();
            return;
        }

        JobHandle b = new Write_Sprite_Job
        {
            rotations = spriteRotations,
            scales = spriteScales,
            localPositions = spriteLocalPositions
        }.Schedule(spriteTransforms);

        JobHandle.CombineDependencies(a, b).Complete();
    }

    /// <summary>스프라이트 회전/스케일/로컬위치를 Transform에 일괄로 씁니다.</summary>
    public void Write_Sprites()
    {
        if (!spriteTransforms.isCreated) return;
        if (!spriteRotations.IsCreated) return;

        new Write_Sprite_Job
        {
            rotations = spriteRotations,
            scales = spriteScales,
            localPositions = spriteLocalPositions
        }.Schedule(spriteTransforms).Complete();
    }

    /// <summary>네이티브 자원을 반납합니다.</summary>
    public void Dispose()
    {
        if (transforms.isCreated) transforms.Dispose();
        if (spriteTransforms.isCreated) spriteTransforms.Dispose();
        if (positions.IsCreated) positions.Dispose();
        if (rotations.IsCreated) rotations.Dispose();
        if (spriteRotations.IsCreated) spriteRotations.Dispose();
        if (spriteScales.IsCreated) spriteScales.Dispose();
        if (spriteLocalPositions.IsCreated) spriteLocalPositions.Dispose();
    }
}

/// <summary>Transform 위치를 배열로 읽어 오는 잡입니다.</summary>
[BurstCompile]
public struct Read_Position_Job : IJobParallelForTransform
{
    /// <summary>읽어 온 위치를 담을 배열입니다.</summary>
    [WriteOnly] public NativeArray<Vector3> positions;

    /// <summary>Transform 하나의 위치를 배열에 옮겨 담습니다.</summary>
    /// <param name="index">배열 인덱스입니다. units 리스트와 1:1로 대응합니다.</param>
    /// <param name="transform">읽어 올 Transform입니다.</param>
    public void Execute(int index, TransformAccess transform)
    {
        positions[index] = transform.position;
    }
}

/// <summary>배열의 위치와 회전을 Transform에 쓰는 잡입니다.</summary>
[BurstCompile]
public struct Write_Transform_Job : IJobParallelForTransform
{
    /// <summary>Transform에 쓸 위치 배열입니다.</summary>
    [ReadOnly] public NativeArray<Vector3> positions;

    /// <summary>Transform에 쓸 회전 배열입니다.</summary>
    [ReadOnly] public NativeArray<Quaternion> rotations;

    /// <summary>계산된 위치와 회전을 Transform 하나에 반영합니다.</summary>
    /// <param name="index">배열 인덱스입니다.</param>
    /// <param name="transform">값을 쓸 Transform입니다.</param>
    public void Execute(int index, TransformAccess transform)
    {
        transform.position = positions[index];
        transform.rotation = rotations[index];
    }
}

/// <summary>
/// 스프라이트(자식) Transform에 회전/스케일/로컬위치를 쓰는 잡입니다.
///
/// 스프라이트는 카메라를 향해 서고, 피격 시 커졌다 줄고, 공격할 때
/// 표적 쪽으로 짧게 내지릅니다. 이 세 가지를 유닛마다 메인 스레드에서
/// 대입하면 9,600명 기준 7.44 ms가 듭니다.
/// </summary>
[BurstCompile]
public struct Write_Sprite_Job : IJobParallelForTransform
{
    /// <summary>스프라이트가 카메라를 향하도록 하는 회전 배열입니다.</summary>
    [ReadOnly] public NativeArray<Quaternion> rotations;

    /// <summary>피격 반동과 좌우 반전이 반영된 스케일 배열입니다. X 부호가 반전을 뜻합니다.</summary>
    [ReadOnly] public NativeArray<Vector3> scales;

    /// <summary>공격 시 내지르기가 반영된 로컬 위치 배열입니다.</summary>
    [ReadOnly] public NativeArray<Vector3> localPositions;

    /// <summary>스프라이트 Transform 하나에 자세와 연출을 반영합니다.</summary>
    /// <param name="index">배열 인덱스입니다.</param>
    /// <param name="transform">값을 쓸 스프라이트 Transform입니다.</param>
    public void Execute(int index, TransformAccess transform)
    {
        transform.rotation = rotations[index];
        transform.localScale = scales[index];
        transform.localPosition = localPositions[index];
    }
}
