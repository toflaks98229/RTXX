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

        // Apply 내부 세부 단계
        // Apply가 30%를 차지하는데 그 안에서 무엇이 비싼지 몰라 추가했습니다.
        A_Animation,    // 애니메이션 반영
        A_Formation,    // 진형 유지 판정
        A_UnitUpdate,   // 유닛별 _Update (이동/전투 후처리)
        A_Charge,       // 돌격 충돌 정산
        A_Dead,         // 사망 처리
        A_Morale,       // 사기 입력 산출

        // Schedule 내부 세부 단계
        S_RaycastSetup, // 레이캐스트 명령 생성 (유닛별 루프)
        S_AnimInput,    // 애니메이션 입력 복사
        S_TargetCopy,   // 적 유닛 데이터 복사
        S_JobSchedule,  // 실제 Job 스케줄 호출

        Count
    }

    /// <summary>계측을 켤지 여부입니다.</summary>
    public static bool benabled;

    private static readonly double[] totals = new double[(int)Phase.Count];
    private static readonly double[] worsts = new double[(int)Phase.Count];
    private static readonly Stopwatch watch = new Stopwatch();

    private static Phase current;
    private static int samples;

    /// <summary>
    /// 중첩 측정용 보조 스톱워치입니다.
    ///
    /// 주 스톱워치(watch)는 상위 단계가 쓰고 있으므로, 그 안에서 다시
    /// Begin/End를 부르면 상위 측정이 망가집니다. 세부 단계는 별도
    /// 스톱워치로 재고 합계에서는 제외합니다. (이중 계산 방지)
    /// </summary>
    private static readonly Stopwatch subWatch = new Stopwatch();
    private static Phase subCurrent;

    /// <summary>세부 단계 측정을 시작합니다. 상위 측정과 겹쳐도 안전합니다.</summary>
    public static void Begin_Sub(Phase phase)
    {
        if (!benabled) return;

        subCurrent = phase;
        subWatch.Restart();
    }

    /// <summary>세부 단계 측정을 끝냅니다.</summary>
    public static void End_Sub()
    {
        if (!benabled) return;

        subWatch.Stop();

        double ms = subWatch.Elapsed.TotalMilliseconds;
        int i = (int)subCurrent;

        totals[i] += ms;
        if (ms > worsts[i]) worsts[i] = ms;
    }

    /// <summary>이 단계가 합계에서 제외되는 세부 단계인지 여부입니다.</summary>
    private static bool Is_Sub(Phase phase)
    {
        return phase >= Phase.A_Animation;
    }

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

        // 세부 단계는 상위 단계에 이미 포함되어 있으므로 합계에서 뺍니다.
        double grandTotal = 0.0;
        for (int i = 0; i < (int)Phase.Count; i++)
        {
            if (Is_Sub((Phase)i)) continue;
            grandTotal += totals[i];
        }

        if (grandTotal <= 0.0) return "측정값이 0입니다.";

        for (int i = 0; i < (int)Phase.Count; i++)
        {
            if (Is_Sub((Phase)i)) continue;

            double avg = totals[i] / samples;
            double share = totals[i] / grandTotal * 100.0;

            sb.AppendLine($"{(Phase)i,-16}{avg,10:F2}{worsts[i],10:F2}{share,7:F1}%");
        }

        sb.AppendLine($"{"합계",-16}{grandTotal / samples,10:F2}");

        // 세부 단계는 따로 보여 줍니다. (상위 단계에 포함된 값)
        bool bhasSub = false;
        for (int i = 0; i < (int)Phase.Count; i++)
        {
            if (!Is_Sub((Phase)i)) continue;
            if (totals[i] <= 0.0) continue;

            if (!bhasSub)
            {
                sb.AppendLine("--- 세부 (상위 단계에 포함) ---");
                bhasSub = true;
            }

            sb.AppendLine($"{(Phase)i,-16}{totals[i] / samples,10:F2}{worsts[i],10:F2}");
        }

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
