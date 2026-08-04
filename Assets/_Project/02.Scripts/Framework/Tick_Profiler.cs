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

        // Collision 내부
        C_Gather,       // Collision_Body 채우기 (유닛별 루프)
        C_Writeback,    // 결과 반영 루프 (유닛별)

        // 길찾기
        //
        // 이 프로젝트는 부대 기준점만 NavMesh를 쓰고, 유닛은 진형 슬롯을
        // 향한 벡터 연산만 합니다. 즉 '군집 기반 길찾기'가 이미 구현되어
        // 있습니다. 그 비용이 실제로 얼마인지 재기 위한 항목입니다.
        P_SetDestination, // 경로 재계산 (명령 시에만)
        P_AgentMove,      // NavMeshAgent.Move (틱당, 부대 단위)

        /// <summary>
        /// 부대 AI 판단입니다. (Battle_AI._Update_Decisions)
        ///
        /// '유닛 1만 개가 아니라 부대 수십 개만 판단한다'는 주장이
        /// 실제로 성립하는지 재기 위한 항목입니다.
        /// </summary>
        AI_Decision,

        /// <summary>Unit._Update_Move (진형 슬롯 Transform 읽기 포함)</summary>
        U_Move,
        /// <summary>Unit._Update_Fight (사건 없으면 즉시 반환)</summary>
        U_Fight,

        /// <summary>
        /// 진형 재정비의 유닛-슬롯 매칭입니다. (Formation_Matcher / 헝가리안)
        ///
        /// 스파이크 용의자입니다. 3초마다 부대별로 발동하며,
        /// 메인 스레드에서 동기 실행되는 O(n*s^2) 계산입니다.
        /// 부대마다 타이머가 따로라 몰리지 않지만, 걸린 틱은 크게 튑니다.
        /// </summary>
        A_Match,

        Count
    }

    /// <summary>계측을 켤지 여부입니다.</summary>
    public static bool benabled;

    /// <summary>단계별 누적 시간(밀리초)입니다. 평균 계산에 씁니다.</summary>
    private static readonly double[] totals = new double[(int)Phase.Count];

    /// <summary>단계별 최악 시간(밀리초)입니다. 스파이크의 크기를 봅니다.</summary>
    private static readonly double[] worsts = new double[(int)Phase.Count];
    /// <summary>상위 단계를 재는 주 스톱워치입니다.</summary>
    private static readonly Stopwatch watch = new Stopwatch();

    /// <summary>현재 측정 중인 상위 단계입니다.</summary>
    private static Phase current;
    /// <summary>측정된 틱 수입니다. 평균 계산의 분모입니다.</summary>
    private static int samples;

    // =====================================================================
    // 스파이크 귀속
    //
    // 왜 필요한가:
    // 평균만 보면 이 시뮬레이션은 예산 안에 있습니다(11.6 / 16.67 ms).
    // 그런데 실제 실행에서는 틱의 17%가 20ms를 넘고 최악은 94ms입니다.
    // 즉 비용이 고르게 퍼진 것이 아니라 '가끔 몰리는' 형태입니다.
    //
    // 평균값 표는 이 현상을 설명하지 못합니다. 어느 단계가 그 순간에
    // 튀는지 알아야 고칠 수 있으므로, 스파이크 틱에서만 단계별 비용을
    // 따로 누적합니다.
    // =====================================================================

    /// <summary>이 시간을 넘긴 틱을 스파이크로 봅니다.</summary>
    public const double spikeThresholdMs = 20.0;

    /// <summary>이번 틱에 각 단계가 쓴 시간입니다. 틱마다 초기화됩니다.</summary>
    private static readonly double[] tickCosts = new double[(int)Phase.Count];

    /// <summary>스파이크 틱에서만 누적한 단계별 비용입니다.</summary>
    private static readonly double[] spikeTotals = new double[(int)Phase.Count];

    /// <summary>스파이크로 판정된 틱 수입니다.</summary>
    private static int spikeSamples;

    /// <summary>스파이크 틱에서 가장 비쌌던 단계가 각각 몇 번 1위였는지입니다.</summary>
    private static readonly int[] spikeCulprit = new int[(int)Phase.Count];

    /// <summary>스파이크로 판정된 틱 수입니다. 결과 파일에 남깁니다.</summary>
    public static int SpikeSamples => spikeSamples;

    /// <summary>
    /// 시뮬레이션 한 틱의 평균 총비용입니다. (세부 단계는 중복이라 제외)
    ///
    /// 프로브가 재는 '틱 간격 전체'에서 이 값을 빼면 시뮬레이션 바깥의
    /// 비용(렌더링, GC, 에디터 오버헤드)이 나옵니다.
    /// </summary>
    public static double Average_Total_Ms()
    {
        if (samples == 0) return 0.0;

        double total = 0.0;
        for (int i = 0; i < (int)Phase.Count; i++)
        {
            if (Is_Sub((Phase)i)) continue;
            total += totals[i];
        }

        return total / samples;
    }

    /// <summary>
    /// 중첩 측정용 보조 스톱워치입니다.
    ///
    /// 주 스톱워치(watch)는 상위 단계가 쓰고 있으므로, 그 안에서 다시
    /// Begin/End를 부르면 상위 측정이 망가집니다. 세부 단계는 별도
    /// 스톱워치로 재고 합계에서는 제외합니다. (이중 계산 방지)
    /// </summary>
    private static readonly Stopwatch subWatch = new Stopwatch();
    /// <summary>현재 측정 중인 세부 단계입니다.</summary>
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
        tickCosts[i] += ms;
        if (ms > worsts[i]) worsts[i] = ms;
    }

    /// <summary>이 단계가 합계에서 제외되는 세부 단계인지 여부입니다.</summary>
    private static bool Is_Sub(Phase phase)
    {
        return phase >= Phase.A_Animation;
    }

    /// <summary>
    /// 경로 재계산(SetDestination) 요청 횟수입니다.
    ///
    /// 이 값이 '틱 수 x 부대 수'에 가까우면 매 틱 길을 다시 찾는 것이고,
    /// 명령 횟수 수준이면 군집 기반 길찾기가 성립한 것입니다.
    /// 시간만 재서는 그 차이를 구분할 수 없어 횟수를 따로 셉니다.
    /// </summary>
    public static int pathRequests { get; private set; }

    /// <summary>경로 재계산 한 건을 셉니다.</summary>
    public static void Count_Path_Request()
    {
        if (!benabled) return;
        pathRequests++;
    }

    /// <summary>
    /// 부대 AI 판단 횟수입니다.
    ///
    /// 유닛 수와 비교해야 '부대 단위 AI'가 성립하는지 알 수 있습니다.
    /// 개체별 AI라면 이 값이 유닛 수 x 틱 수에 비례해야 합니다.
    /// </summary>
    public static int aiDecisions { get; private set; }

    /// <summary>부대 AI 판단 한 건을 셉니다.</summary>
    public static void Count_AI_Decision()
    {
        if (!benabled) return;
        aiDecisions++;
    }

    /// <summary>
    /// 계산 주기 조절(LOD)로 이번 틱에 갱신을 건너뛴 부대의 누적 수입니다.
    ///
    /// '모든 부대를 매 틱 계산하지 않고 여러 틱에 분산한다'는 주장이
    /// 실제로 성립하는지 확인하기 위한 값입니다.
    /// 0이면 분산이 전혀 일어나지 않고 있다는 뜻입니다.
    /// </summary>
    public static int armiesSkipped { get; private set; }

    /// <summary>갱신을 건너뛴 부대 한 개를 셉니다.</summary>
    public static void Count_Army_Skipped()
    {
        if (!benabled) return;
        armiesSkipped++;
    }

    /// <summary>
    /// 진형 매칭(헝가리안) 호출 횟수입니다.
    ///
    /// 스파이크 수와 이 값을 비교해야 '재정비가 범인인가'를 판단할 수 있습니다.
    /// 시간만 봐서는 자주 조금씩인지 가끔 크게인지 구분되지 않습니다.
    /// </summary>
    public static int matchCalls { get; private set; }

    /// <summary>진형 매칭 한 건을 셉니다.</summary>
    public static void Count_Match()
    {
        if (!benabled) return;
        matchCalls++;
    }

    /// <summary>
    /// 사기가 '포위'로 계산한 부대의 누적 수입니다. (몸을 맞댄 적 부대가 2개 이상)
    /// </summary>
    public static int surroundedArmies { get; private set; }

    /// <summary>
    /// 몸을 맞댔는데도 피해를 줄 수 없는 적 부대의 누적 수입니다.
    ///
    /// 이 값은 **언제나 0이어야 합니다.** 회귀 감시용 지표입니다.
    ///
    /// 왜 두는가:
    /// 예전에는 피해가 '자기 부대가 고른 targetArmy' 하나에서만 들어왔습니다.
    /// 그래서 나를 때리는 부대가 내 표적이 아니면 그 타격은 명중 판정까지
    /// 끝난 뒤 아무도 읽지 않고 사라졌습니다. 사기는 포위를 정확히 세는데
    /// (Morale_Modifiers.surrounded) 체력만 줄지 않는 상태였습니다.
    ///
    /// 지금은 Army_Fight.Collect_Fight_Armies가 접촉·탐지한 적 부대를 모두
    /// 피해원으로 삼으므로 이 값이 0으로 유지됩니다. 0이 아니게 되는 순간
    /// 그 경로가 어딘가에서 다시 좁아졌다는 뜻입니다.
    /// </summary>
    public static int blockedAttackers { get; private set; }

    /// <summary>
    /// 부대 하나의 교전 실태를 셉니다. 틱마다 전 부대에 대해 호출합니다.
    /// </summary>
    /// <param name="contactEnemyArmies">몸이 맞닿은 적 부대 수입니다.</param>
    /// <param name="damageSourceArmies">그중 실제로 나에게 피해를 줄 수 있는 부대 수입니다.</param>
    public static void Count_Engagement(int contactEnemyArmies, int damageSourceArmies)
    {
        if (!benabled) return;

        if (contactEnemyArmies >= 2) surroundedArmies++;

        int blocked = contactEnemyArmies - damageSourceArmies;
        if (blocked > 0) blockedAttackers += blocked;
    }

    /// <summary>
    /// 이번 실행에서 '명중이 성립한' 타격의 누적 수입니다.
    ///
    /// Unit_Data.Attack_End가 사거리를 다시 재고 bhitTarget을 세운 횟수이며,
    /// 곧 "때리는 데까지는 성공한" 공격의 수입니다.
    /// </summary>
    public static int attacksLanded { get; private set; }

    /// <summary>
    /// 이번 틱에 실제로 HP가 깎인 '유닛 수'의 누적입니다.
    ///
    /// 주의: 피해 '건수'가 아닙니다.
    /// Unit_Data.bgetDamage는 bool 하나이고 Unit._Update_Fight가 틱마다
    /// 한 번만 소비하므로, 한 유닛이 같은 틱에 세 명에게 맞아도 1로 셉니다.
    /// 난전이 격해질수록 실제 타격 수보다 더 크게 과소 집계됩니다.
    ///
    /// 그래서 attacksLanded와의 비율을 '피해 전달률'로 읽으면 안 됩니다.
    /// 그 비율에는 명중 굴림(hit_Chance)과 위 합산 손실이 함께 섞여 있습니다.
    /// 피해가 실제로 전달되는지는 <see cref="blockedAttackers"/>(0이어야 함)와
    /// 사망자 수로 판단하십시오.
    /// </summary>
    public static int damageApplied { get; private set; }

    /// <summary>명중이 성립한 타격 하나를 셉니다.</summary>
    public static void Count_Attack_Landed()
    {
        if (!benabled) return;
        attacksLanded++;
    }

    /// <summary>실제로 HP를 깎은 피해 하나를 셉니다.</summary>
    public static void Count_Damage_Applied()
    {
        if (!benabled) return;
        damageApplied++;
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
        tickCosts[i] += ms;
        if (ms > worsts[i]) worsts[i] = ms;
    }

    /// <summary>
    /// 한 틱이 끝났음을 알립니다. 평균 계산의 분모가 됩니다.
    ///
    /// 여기서 이번 틱이 스파이크였는지 판정하고, 그렇다면 단계별 비용을
    /// 따로 누적합니다. 평균 표만으로는 '가끔 몰리는' 비용을 볼 수 없습니다.
    /// </summary>
    public static void End_Tick()
    {
        if (!benabled) return;

        samples++;

        // 이번 틱의 시뮬레이션 총비용을 구합니다. (세부 단계는 중복이라 제외)
        double tickTotal = 0.0;
        for (int i = 0; i < (int)Phase.Count; i++)
        {
            if (Is_Sub((Phase)i)) continue;
            tickTotal += tickCosts[i];
        }

        if (tickTotal > spikeThresholdMs)
        {
            spikeSamples++;

            // 이 틱에서 가장 비쌌던 '상위' 단계를 범인으로 기록합니다.
            int worstPhase = -1;
            double worstCost = 0.0;

            for (int i = 0; i < (int)Phase.Count; i++)
            {
                spikeTotals[i] += tickCosts[i];

                if (Is_Sub((Phase)i)) continue;

                if (tickCosts[i] > worstCost)
                {
                    worstCost = tickCosts[i];
                    worstPhase = i;
                }
            }

            if (worstPhase >= 0) spikeCulprit[worstPhase]++;
        }

        System.Array.Clear(tickCosts, 0, tickCosts.Length);
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

        // 스파이크 귀속을 보여 줍니다.
        //
        // 평균이 예산 안이어도 실제 플레이는 끊길 수 있습니다.
        // 실측에서 평균 11.6ms인데 틱의 17%가 20ms를 넘고 최악이 94ms였습니다.
        // 어느 단계가 그 순간에 튀는지 알아야 고칠 수 있습니다.
        if (spikeSamples > 0)
        {
            sb.AppendLine($"--- 스파이크 ({spikeThresholdMs:F0}ms 초과) ---");
            sb.AppendLine($"스파이크 틱   : {spikeSamples} / {samples} " +
                          $"({(double)spikeSamples / samples * 100.0:F1}%)");
            sb.AppendLine($"{"단계",-16}{"스파이크평균",12}{"1위횟수",9}");

            for (int i = 0; i < (int)Phase.Count; i++)
            {
                if (Is_Sub((Phase)i)) continue;
                if (spikeTotals[i] <= 0.0) continue;

                sb.AppendLine($"{(Phase)i,-16}{spikeTotals[i] / spikeSamples,12:F2}" +
                              $"{spikeCulprit[i],9}");
            }

            // 세부 단계도 스파이크 때 얼마였는지 봅니다.
            for (int i = 0; i < (int)Phase.Count; i++)
            {
                if (!Is_Sub((Phase)i)) continue;
                if (spikeTotals[i] <= 0.0) continue;

                sb.AppendLine($"  └ {(Phase)i,-13}{spikeTotals[i] / spikeSamples,12:F2}");
            }
        }

        // 길찾기 실태를 함께 보여 줍니다.
        //
        // 시간만 봐서는 '군집 기반인지 개체별인지'를 알 수 없습니다.
        // 요청 횟수를 틱 수와 비교해야 판단이 섭니다.
        sb.AppendLine("--- 길찾기 / AI ---");
        sb.AppendLine($"진형 매칭     : {matchCalls}회 " +
                      $"(틱당 {(double)matchCalls / samples:F3}회)");
        sb.AppendLine($"경로 재계산   : {pathRequests}회 " +
                      $"(틱당 {(double)pathRequests / samples:F3}회)");
        sb.AppendLine($"AI 판단       : {aiDecisions}회 " +
                      $"(틱당 {(double)aiDecisions / samples:F3}회)");
        sb.AppendLine($"갱신 건너뜀   : {armiesSkipped}개 " +
                      $"(틱당 {(double)armiesSkipped / samples:F2}개 부대)");

        // 피해 전달 실태를 함께 보여 줍니다.
        //
        // 사기는 접촉한 적 부대 수를 세어 포위 페널티를 매기는데,
        // 피해는 targetArmy 하나에서만 들어옵니다. 두 계산의 기준이 다르므로
        // 그 차이가 실제로 얼마나 벌어지는지 숫자로 확인해야 합니다.
        sb.AppendLine("--- 피해 전달 ---");
        sb.AppendLine($"포위 판정 부대 : {surroundedArmies}개 " +
                      $"(틱당 {(double)surroundedArmies / samples:F2}개)");
        sb.AppendLine($"피해원 누락   : {blockedAttackers}개 " +
                      $"{(blockedAttackers == 0 ? "(정상: 접촉한 적은 모두 피해를 줄 수 있음)" : "<- 피해 경로가 좁아졌습니다")}");
        sb.AppendLine($"명중 성립     : {attacksLanded}회 " +
                      $"(틱당 {(double)attacksLanded / samples:F1}회)");
        sb.AppendLine($"피격 유닛     : {damageApplied}명 " +
                      $"(틱당 {(double)damageApplied / samples:F1}명, 같은 틱 중복 피격은 1로 셈)");

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
        pathRequests = 0;
        aiDecisions = 0;
        armiesSkipped = 0;
        matchCalls = 0;
        surroundedArmies = 0;
        blockedAttackers = 0;
        attacksLanded = 0;
        damageApplied = 0;
    }
}
