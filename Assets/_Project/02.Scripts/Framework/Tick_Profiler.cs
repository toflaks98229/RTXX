using System.Diagnostics;
using System.Text;
using UnityEngine;

/// <summary>
/// 시뮬레이션 틱의 단계별 비용을 재는 계측기입니다.
///
/// 왜 필요한가:
/// "평균 틱 32ms"라는 숫자만으로는 무엇을 고쳐야 할지 알 수 없습니다.
/// 부대 갱신이 문제인지, 충돌이 문제인지, Transform 쓰기가 문제인지
/// 모르는 채로 최적화하면 엉뚱한 곳을 건드리게 됩니다.
///
/// 이 프로젝트의 기존 최적화들은 전부 실측 근거를 갖고 있습니다.
/// (주석의 11.5ms, 6.12ms, 88ms 같은 수치들) 그 방식을 이어갑니다.
///
/// 비용:
/// Stopwatch는 호출당 수십 나노초입니다. 단계가 10개 남짓이므로
/// 측정 자체가 결과를 왜곡하지 않습니다. 그래도 릴리스에서는
/// benabled를 꺼 두는 편이 안전합니다.
/// </summary>
public static class Tick_Profiler
{
    /// <summary>측정할 단계입니다.</summary>
    public enum Phase
    {
        Snapshot,       // 유닛 스냅샷 만들기
        ArmyJob,        // 부대 상태 머신 Job
        Prepare,        // 부대 전처리 (이동, 탐지, 지형)
        Schedule,       // Job 스케줄
        Complete,       // Job 완료 대기
        Apply,          // 결과 반영
        Collision,      // 충돌 해소
        GroundSync,     // 지면 높이 동기화
        TransformWrite, // Transform 일괄 쓰기
        Contact,        // 부대 접촉 집계
        Count
    }

    /// <summary>계측을 켤지 여부입니다.</summary>
    public static bool benabled;

    private static readonly double[] totals = new double[(int)Phase.Count];
    private static readonly double[] worsts = new double[(int)Phase.Count];
    private static readonly Stopwatch watch = new Stopwatch();

    private static Phase current;
    private static int samples;

    /// <summary>한 단계의 측정을 시작합니다.</summary>
    public static void Begin(Phase phase)
    {
        if (!benabled) return;

        current = phase;
        watch.Restart();
    }

    /// <summary>측정 중인 단계를 끝내고 누적합니다.</summary>
    public static void End()
    {
        if (!benabled) return;

        watch.Stop();

        double ms = watch.Elapsed.TotalMilliseconds;
        int i = (int)current;

        totals[i] += ms;
        if (ms > worsts[i]) worsts[i] = ms;
    }

    /// <summary>한 틱이 끝났음을 알립니다. 평균 계산의 분모가 됩니다.</summary>
    public static void End_Tick()
    {
        if (!benabled) return;
        samples++;
    }

    /// <summary>누적된 측정값을 사람이 읽을 수 있는 표로 만듭니다.</summary>
    public static string Report()
    {
        if (samples == 0) return "측정된 틱이 없습니다.";

        StringBuilder sb = new StringBuilder(512);

        sb.AppendLine("---------- 틱 단계별 비용 ----------");
        sb.AppendLine($"측정 틱: {samples}");
        sb.AppendLine($"{"단계",-16}{"평균(ms)",10}{"최악(ms)",10}{"비중",8}");

        double grandTotal = 0.0;
        for (int i = 0; i < (int)Phase.Count; i++) grandTotal += totals[i];

        if (grandTotal <= 0.0) return "측정값이 0입니다.";

        for (int i = 0; i < (int)Phase.Count; i++)
        {
            double avg = totals[i] / samples;
            double share = totals[i] / grandTotal * 100.0;

            sb.AppendLine($"{(Phase)i,-16}{avg,10:F2}{worsts[i],10:F2}{share,7:F1}%");
        }

        sb.AppendLine($"{"합계",-16}{grandTotal / samples,10:F2}");
        sb.AppendLine("------------------------------------");

        return sb.ToString();
    }

    /// <summary>누적값을 지웁니다.</summary>
    public static void Reset()
    {
        for (int i = 0; i < (int)Phase.Count; i++)
        {
            totals[i] = 0.0;
            worsts[i] = 0.0;
        }

        samples = 0;
    }
}
