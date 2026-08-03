using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// 전 유닛을 지면 높이에 맞추는 계층입니다.
///
/// 왜 필요한가:
/// 이 시뮬레이션의 이동 계산은 전부 XZ 평면에서만 이루어집니다.
/// movementVector를 만드는 모든 경로가 마지막에 .y = 0을 넣기 때문입니다.
/// (Move, Move_Target, Move_Hold_Line, Idle, Move_Charge_Step ...)
///
/// 그 자체는 옳은 설계입니다. 전술 판정(사거리, 측후방, 진형)은 평면에서
/// 이루어져야 하고, 경사면에서 사거리가 늘었다 줄었다 하면 안 됩니다.
///
/// 문제는 '그러면 Y는 누가 정하는가'가 비어 있었다는 점입니다.
/// 유닛은 생성 시점의 Y를 그대로 들고 다녔고, 결과적으로 언덕을
/// 뚫고 지나가거나 공중에 떠서 걸었습니다.
///
/// 예전에는 Rigidbody 중력과 NavMeshAgent가 이 구멍을 부분적으로
/// 가려 주었지만, 둘 다 제거되면서 그대로 드러났습니다.
/// (물리를 되살리는 것은 답이 아닙니다. 4800명 기준 틱당 11.5ms였습니다)
///
/// 해결:
/// 유닛 머리 위에서 아래로 레이캐스트를 쏴 지면 높이를 얻습니다.
/// 유닛마다 Physics.Raycast를 부르면 9,600명 기준 수십 ms가 들지만,
/// RaycastCommand.ScheduleBatch로 묶으면 워커 스레드에서 병렬 처리됩니다.
/// 이미 Army._Schedule_Unit이 전방 차단 검사에 쓰고 있는 것과 같은 방식입니다.
///
/// 갱신 빈도:
/// 지면 높이는 유닛이 움직인 만큼만 달라지므로 매 틱 갱신할 필요가 없습니다.
/// 여러 틱에 나눠 처리하고, 그 사이에는 이전 높이를 유지합니다.
/// 경사를 걸어 올라갈 때 미세하게 늦게 따라오지만 눈에 띄지 않습니다.
/// </summary>
public class Unit_Ground_Sync
{
    /// <summary>레이를 쏘기 시작하는 높이입니다. 유닛 머리 위에서 시작합니다.</summary>
    public const float rayStartHeight = 50.0f;

    /// <summary>레이의 최대 길이입니다. 시작 높이보다 충분히 길어야 합니다.</summary>
    public const float rayDistance = 200.0f;

    /// <summary>레이캐스트 명령 버퍼입니다. 매 틱 재사용합니다.</summary>
    private NativeArray<RaycastCommand> commands;

    /// <summary>레이캐스트 결과 버퍼입니다.</summary>
    private NativeArray<RaycastHit> results;

    /// <summary>이번에 처리한 유닛 수입니다.</summary>
    private int scheduledCount;

    /// <summary>진행 중인 Job 핸들입니다.</summary>
    private JobHandle handle;

    /// <summary>Job을 실제로 걸었는지 여부입니다.</summary>
    private bool bscheduled;

    /// <summary>지면 레이어 마스크 캐시입니다.</summary>
    private static int groundMaskCache = -1;

    /// <summary>
    /// 지면 레이어 마스크입니다.
    ///
    /// "Ground" 레이어가 없으면 모든 레이어를 봅니다. 그래야 레이어 설정이
    /// 되어 있지 않은 프로젝트에서도 조용히 실패하지 않습니다.
    /// 다만 유닛끼리 서로를 지면으로 인식하면 안 되므로, 유닛에 콜라이더가
    /// 없는 현재 구조에서만 안전합니다. (지금은 콜라이더가 없습니다)
    /// </summary>
    public static int Ground_Mask
    {
        get
        {
            if (groundMaskCache == -1)
            {
                groundMaskCache = LayerMask.GetMask("Ground");
                if (groundMaskCache == 0) groundMaskCache = ~0;
            }

            return groundMaskCache;
        }
    }

    /// <summary>
    /// 지면 높이 조회를 스케줄합니다. 여기서 기다리지 않습니다.
    ///
    /// 부분 갱신:
    /// offset부터 count개만 처리합니다. 전 유닛을 매 틱 쏘지 않고
    /// 여러 틱에 나눠 도는 방식이며, 호출부가 구간을 정합니다.
    /// </summary>
    /// <param name="units">전체 유닛 목록입니다.</param>
    /// <param name="offset">이번에 처리할 시작 인덱스입니다.</param>
    /// <param name="count">이번에 처리할 개수입니다.</param>
    public void Schedule(System.Collections.Generic.List<Unit> units, int offset, int count)
    {
        if (units == null || count <= 0) return;

        Ensure_Capacity(ref commands, count);
        Ensure_Capacity(ref results, count);

        var cmd = commands.GetSubArray(0, count);
        var res = results.GetSubArray(0, count);

        int mask = Ground_Mask;

        // QueryParameters를 쓰는 신형 생성자입니다.
        // 구형 생성자는 Unity 6에서 사용 중단 경고가 납니다.
        var parameters = new QueryParameters(mask, false, QueryTriggerInteraction.Ignore, false);

        for (int i = 0; i < count; i++)
        {
            int index = offset + i;

            Unit u = (index < units.Count) ? units[index] : null;

            if (u == null)
            {
                // 빈 자리는 아주 먼 곳을 쏘게 두어 결과가 무시되게 합니다.
                // (길이 0 명령을 넣으면 ScheduleBatch가 예외를 냅니다)
                cmd[i] = new RaycastCommand(
                    new Vector3(0.0f, -100000.0f, 0.0f), Vector3.down, parameters, 1.0f);
                continue;
            }

            Vector3 origin = u.unit_Data.position;
            origin.y += rayStartHeight;

            cmd[i] = new RaycastCommand(origin, Vector3.down, parameters, rayDistance);
        }

        scheduledCount = count;
        handle = RaycastCommand.ScheduleBatch(cmd, res, Constant.jobBatchCount);
        bscheduled = true;
    }

    /// <summary>
    /// 조회 결과를 유닛 높이에 반영합니다.
    ///
    /// 지면을 찾지 못한 유닛은 건드리지 않습니다. 전장 밖으로 나갔거나
    /// 지형이 없는 구역인데, 그 경우 0으로 떨어뜨리면 오히려 이상해집니다.
    /// </summary>
    /// <param name="units">전체 유닛 목록입니다.</param>
    /// <param name="offset">Schedule에 넘긴 것과 같은 시작 인덱스여야 합니다.</param>
    public void Complete_And_Apply(System.Collections.Generic.List<Unit> units, int offset)
    {
        if (!bscheduled) return;

        handle.Complete();
        bscheduled = false;

        if (units == null) return;

        var res = results.GetSubArray(0, scheduledCount);

        for (int i = 0; i < scheduledCount; i++)
        {
            int index = offset + i;
            if (index >= units.Count) break;

            Unit u = units[index];
            if (u == null) continue;

            // RaycastCommand는 '맞지 않음'을 colliderEntityId가 None인 것으로 알립니다.
            // distance만 보면 0에서 맞은 경우와 구분되지 않습니다.
            if (res[i].colliderEntityId == EntityId.None) continue;

            Vector3 p = u.unit_Data.position;
            p.y = res[i].point.y;
            u.unit_Data.position = p;
        }
    }

    /// <summary>버퍼가 요청한 크기 이상이 되도록 보장합니다.</summary>
    private static void Ensure_Capacity<T>(ref NativeArray<T> buffer, int required)
        where T : struct
    {
        if (buffer.IsCreated && buffer.Length >= required) return;

        if (buffer.IsCreated) buffer.Dispose();

        buffer = new NativeArray<T>(Mathf.Max(required, 256), Allocator.Persistent);
    }

    /// <summary>네이티브 자원을 반납합니다.</summary>
    public void Dispose()
    {
        // 진행 중인 Job이 참조하는 버퍼를 해제하면 즉시 크래시가 납니다.
        if (bscheduled)
        {
            handle.Complete();
            bscheduled = false;
        }

        if (commands.IsCreated) commands.Dispose();
        if (results.IsCreated) results.Dispose();
    }
}
