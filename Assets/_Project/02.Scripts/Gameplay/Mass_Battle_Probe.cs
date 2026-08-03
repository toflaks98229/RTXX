using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 대규모 전투에서 시뮬레이션이 '실제로 올바르게' 도는지 측정하고 검증합니다.
///
/// 왜 필요한가:
/// 컴파일이 통과했다는 것은 문법이 맞다는 뜻일 뿐입니다. 이번 리팩토링은
/// 물리 경로를 통째로 걷어내고, 인덱스 계약을 바꾸고, 부대 조회를 격자로
/// 옮겼습니다. 이런 변경은 '조용히 틀리는' 종류입니다. 예외도 없이,
/// 그저 병사들이 이상하게 움직일 뿐입니다.
///
/// 그래서 사람이 눈으로 보지 않아도 잡히는 지표를 남깁니다.
///
/// 측정하는 것:
///   - 틱 시간 분포 (평균/최악/스파이크 횟수)
///   - 유닛 위치의 유효성 (NaN, 지면 아래, 좌표 폭주)
///   - 인덱스 정합성 (simIndex, armyIndex)
///   - 전투가 실제로 진행되는가 (사망자, 교전, 사기 변동)
///
/// 마지막 항목이 특히 중요합니다. 성능만 재면 '아무 일도 일어나지 않는
/// 빠른 씬'을 통과시켜 버립니다. 전투가 돌지 않으면 그것부터 버그입니다.
/// </summary>
public class Mass_Battle_Probe : MonoBehaviour
{
    [Header("측정")]
    [Tooltip("이 틱 수만큼 측정한 뒤 결과를 남깁니다. 0이면 계속 측정합니다.")]
    public int measureTicks = 600;

    [Tooltip("이 시간(ms)을 넘으면 스파이크로 셉니다. 60fps 예산은 16.6ms입니다.")]
    public float spikeThresholdMs = 20.0f;

    [Tooltip("측정이 끝나면 자동으로 플레이를 종료합니다. 배치모드 검증용입니다.")]
    public bool bquitWhenDone;

    /// <summary>
    /// 결과 지문을 적을 파일 경로입니다. 비어 있으면 파일을 남기지 않습니다.
    ///
    /// 배치모드에서 명령줄(-out)로 지정합니다. 결과 기록을 러너가 아니라
    /// 이 컴포넌트가 맡는 이유는, 배치모드에서 EditorApplication.update가
    /// 플레이 중에 확실히 돌지 않기 때문입니다. FixedUpdate는 반드시 돕니다.
    /// </summary>
    [System.NonSerialized]
    public string resultPath;

    [Header("표시")]
    [Tooltip("화면에 실시간 지표를 그립니다.")]
    public bool bdrawGUI = true;

    private Controller controller;

    // --- 틱 시간 통계 ---
    private int tickCount;
    private double totalMs;
    private float worstMs;
    private int spikeCount;
    private System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

    // --- 전투 진행 통계 ---
    private int deaths;
    private int routs;
    private int shatters;
    private int charges;

    // --- 검증 결과 ---
    private readonly List<string> problems = new List<string>();
    private bool breported;

    private GUIStyle style;

    private void Start()
    {
        controller = FindAnyObjectByType<Controller>();

        // 배치모드 인자를 직접 읽습니다.
        //   -out       결과 파일 경로
        //   -runTicks  측정할 틱 수
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-out") { resultPath = args[i + 1]; bquitWhenDone = true; }
            if (args[i] == "-runTicks") int.TryParse(args[i + 1], out measureTicks);
        }

        GameEvents.OnUnitKilled += (u, v, k) => deaths++;
        GameEvents.OnArmyRouted += a => routs++;
        GameEvents.OnArmyShattered += a => shatters++;
        GameEvents.OnArmyCharged += (a, t) => charges++;
    }

    /// <summary>
    /// 틱 시간을 잽니다.
    ///
    /// Controller보다 먼저 도는 FixedUpdate에서 시작하고, 늦게 도는
    /// LateFixed 성격의 자리에서 멈춰야 정확하지만, Unity에는 그런 콜백이
    /// 없습니다. 대신 '이전 틱의 끝에서 이번 틱의 끝까지'를 재면
    /// 결과적으로 한 틱의 전체 비용이 나옵니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (watch.IsRunning)
        {
            watch.Stop();
            float ms = (float)watch.Elapsed.TotalMilliseconds;

            // 첫 틱은 초기화 비용이 섞여 있어 통계에서 제외합니다.
            if (tickCount > 0)
            {
                totalMs += ms;
                if (ms > worstMs) worstMs = ms;
                if (ms > spikeThresholdMs) spikeCount++;
            }

            tickCount++;
        }

        // 주기적으로 시뮬레이션 상태를 검증합니다.
        // 매 틱 전수 검사하면 그 비용이 측정값을 오염시키므로 간격을 둡니다.
        if (tickCount % 60 == 0) Validate();

        if (measureTicks > 0 && tickCount >= measureTicks && !breported)
        {
            Report();
        }

        watch.Restart();
    }

    /// <summary>
    /// 시뮬레이션 상태의 유효성을 검사합니다.
    ///
    /// 여기서 잡히는 것은 전부 '예외를 던지지 않는 버그'입니다.
    /// NaN 좌표, 어긋난 인덱스, 지면을 뚫고 내려간 유닛 같은 것들은
    /// 크래시 없이 조용히 진행되므로 명시적으로 확인해야 합니다.
    /// </summary>
    private void Validate()
    {
        if (controller == null) return;

        List<Unit> units = controller.units;
        if (units == null) return;

        int nanCount = 0;
        int outOfRange = 0;
        int simIndexMismatch = 0;
        int armyIndexMismatch = 0;
        int alive = 0;

        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;
            if (u.IsDead()) continue;

            alive++;

            Vector3 p = u.unit_Data.position;

            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)
                || float.IsInfinity(p.x) || float.IsInfinity(p.z))
            {
                nanCount++;
                continue;
            }

            // 전장은 수백 미터 규모입니다. 이보다 멀면 좌표가 폭주한 것입니다.
            if (Mathf.Abs(p.x) > 5000.0f || Mathf.Abs(p.z) > 5000.0f || p.y < -100.0f)
            {
                outOfRange++;
            }

            // 인덱스 계약 검증 (1순위 작업의 핵심)
            if (u.unit_Data.simIndex != i) simIndexMismatch++;

            Army owner = u.GetArmy();
            if (owner != null && owner.armyIndex >= 0
                && u.unit_Data.armyIndex != owner.armyIndex)
            {
                armyIndexMismatch++;
            }
        }

        // 지면 밀착도를 확인합니다.
        //
        // 유닛이 지면에서 얼마나 떠 있거나 파묻혀 있는지 재는 것이 목적입니다.
        // 이동 계산이 전부 XZ 평면에서 이루어지므로, Y를 채워 주는 계층이
        // 없으면 유닛이 생성 높이 그대로 언덕을 통과합니다.
        Check_Ground_Alignment(units);

        Record(nanCount > 0, $"틱 {tickCount}: 좌표가 NaN인 유닛 {nanCount}명");
        Record(outOfRange > 0, $"틱 {tickCount}: 좌표가 전장을 벗어난 유닛 {outOfRange}명");
        Record(simIndexMismatch > 0,
               $"틱 {tickCount}: simIndex 불일치 {simIndexMismatch}건 " +
               "(Transform 동기화가 엉뚱한 자리를 건드립니다)");
        Record(armyIndexMismatch > 0,
               $"틱 {tickCount}: armyIndex 불일치 {armyIndexMismatch}건 " +
               "(킬 귀속이 엉뚱한 부대로 기록됩니다)");
    }

    /// <summary>지면에서 떨어진 정도의 최댓값입니다. 검증 결과에 남깁니다.</summary>
    private float worstGroundGap;

    /// <summary>표본 중 지면에서 크게 벗어난 유닛의 비율입니다.</summary>
    private float groundOffRate;

    /// <summary>
    /// 유닛이 지면에 붙어 있는지 표본 검사합니다.
    ///
    /// 전수 검사하면 레이캐스트가 9,600번 나가 측정값 자체를 오염시킵니다.
    /// 고르게 뽑은 표본이면 '떠 있는 유닛이 있는가'를 판단하기에 충분합니다.
    /// </summary>
    private void Check_Ground_Alignment(List<Unit> units)
    {
        const int sampleCount = 64;
        const float tolerance = 1.0f; // 이보다 벗어나면 떠 있거나 파묻힌 것으로 봅니다.

        if (units.Count == 0) return;

        int stride = Mathf.Max(1, units.Count / sampleCount);

        int checkedCount = 0;
        int offCount = 0;
        float worst = 0.0f;

        for (int i = 0; i < units.Count; i += stride)
        {
            Unit u = units[i];
            if (u == null) continue;
            if (u.IsDead()) continue;

            Vector3 p = u.unit_Data.position;
            Vector3 origin = p + Vector3.up * Unit_Ground_Sync.rayStartHeight;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                 Unit_Ground_Sync.rayDistance,
                                 Unit_Ground_Sync.Ground_Mask))
            {
                continue;
            }

            checkedCount++;

            float gap = Mathf.Abs(p.y - hit.point.y);
            if (gap > worst) worst = gap;
            if (gap > tolerance) offCount++;
        }

        if (checkedCount == 0) return;

        if (worst > worstGroundGap) worstGroundGap = worst;
        groundOffRate = (float)offCount / checkedCount;

        Record(offCount > checkedCount / 2,
               $"틱 {tickCount}: 표본의 {offCount}/{checkedCount}이 지면에서 " +
               $"{tolerance}m 넘게 벗어났습니다 (최대 {worst:F1}m). " +
               "지형 높이가 반영되지 않고 있습니다.");
    }

    /// <summary>같은 문제를 반복해서 쌓지 않도록 한 번만 기록합니다.</summary>
    private void Record(bool bcondition, string message)
    {
        if (!bcondition) return;
        if (problems.Count > 20) return;

        // 같은 종류의 문제는 첫 번째만 남깁니다.
        string kind = message.Substring(message.IndexOf(':') + 1);
        for (int i = 0; i < problems.Count; i++)
        {
            if (problems[i].Contains(kind.Substring(0, Mathf.Min(20, kind.Length)))) return;
        }

        problems.Add(message);
        Debug.LogWarning($"[MassBattle] {message}");
    }

    /// <summary>측정 결과를 로그로 남깁니다.</summary>
    private void Report()
    {
        breported = true;

        int measured = Mathf.Max(1, tickCount - 1);
        double avg = totalMs / measured;

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("========== 대규모 전투 검증 결과 ==========");

        int armies = controller != null && controller.armies != null ? controller.armies.Count : 0;
        int units = controller != null && controller.units != null ? controller.units.Count : 0;

        sb.AppendLine($"규모        : 부대 {armies}개 / 유닛 {units}명");
        sb.AppendLine($"시뮬 틱     : {Simulation_Clock.tick} " +
                      $"(0이면 시뮬레이션이 돌지 않은 것입니다)");

        // 부대 상태 분포를 남깁니다.
        // 전투가 왜 시작되지 않는지 진단하려면 부대가 지금 무엇을 하고 있는지
        // 알아야 합니다. Idle만 잔뜩이면 명령이 안 내려간 것이고,
        // Move뿐이면 아직 접근 중입니다.
        if (controller != null && controller.armies != null)
        {
            int idle = 0, move = 0, charge = 0, escape = 0, engaged = 0, detected = 0;

            for (int i = 0; i < controller.armies.Count; i++)
            {
                Army a = controller.armies[i];
                if (a == null) continue;

                switch (a.army_Data.e_Army_Move)
                {
                    case E_Army_Move.Idle: idle++; break;
                    case E_Army_Move.MoveCharge: charge++; break;
                    case E_Army_Move.MoveEscape: escape++; break;
                    default: move++; break;
                }

                if (a.army_Data.e_Army_Fight != E_Army_Fight.Non) engaged++;
                if (a.army_Detected != null && a.army_Detected.Count > 0) detected++;
            }

            sb.AppendLine($"부대 상태   : Idle {idle} / Move {move} / " +
                          $"Charge {charge} / Escape {escape}");
            sb.AppendLine($"교전 중     : {engaged}개 / 적 탐지 : {detected}개");
        }
        sb.AppendLine($"측정 틱     : {measured}");
        sb.AppendLine($"평균 틱     : {avg:F2} ms");
        sb.AppendLine($"최악 틱     : {worstMs:F2} ms");
        sb.AppendLine($"스파이크    : {spikeCount}회 ({spikeThresholdMs}ms 초과)");
        sb.AppendLine($"예산 대비   : {(avg / 16.6 * 100.0):F0}% (60fps 기준)");
        sb.AppendLine("--- 전투 진행 ---");
        sb.AppendLine($"사망        : {deaths}명");
        sb.AppendLine($"돌격        : {charges}회");
        sb.AppendLine($"붕괴        : {routs}회");
        sb.AppendLine($"와해        : {shatters}회");
        sb.AppendLine("--- 지형 밀착 ---");
        sb.AppendLine($"최대 이격   : {worstGroundGap:F2} m");
        sb.AppendLine($"이탈 비율   : {groundOffRate * 100.0f:F0}% (표본 기준)");
        sb.AppendLine("--- 검증 ---");

        // 전투가 실제로 진행되었는지 확인합니다.
        // 성능만 좋고 아무 일도 안 일어나면 그건 통과가 아닙니다.
        if (deaths == 0)
        {
            sb.AppendLine("[실패] 사망자가 0명입니다. 전투가 진행되지 않았습니다.");
        }

        if (problems.Count == 0)
        {
            sb.AppendLine("[정상] 좌표/인덱스 이상 없음");
        }
        else
        {
            for (int i = 0; i < problems.Count; i++) sb.AppendLine($"[문제] {problems[i]}");
        }

        sb.AppendLine("==========================================");

        Debug.Log(sb.ToString());

        Write_Result_File();

        if (bquitWhenDone)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// 시뮬레이션 상태의 지문을 파일로 남깁니다.
    ///
    /// 결정론 검증의 핵심입니다. 같은 시드로 두 번 돌려 이 해시가 같으면
    /// 9,600명의 좌표와 HP가 비트 단위로 일치한다는 뜻입니다.
    /// 하나라도 어긋나면 해시가 완전히 달라집니다.
    /// </summary>
    private void Write_Result_File()
    {
        if (string.IsNullOrEmpty(resultPath)) return;
        if (controller == null) return;

        ulong hash = 1469598103934665603UL; // FNV-1a 오프셋
        int alive = 0;
        long hpSum = 0;

        var units = controller.units;
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null) continue;
                if (u.IsDead()) continue;

                alive++;
                hpSum += Mathf.RoundToInt(u.unit_Data.HP);

                hash = Mix(hash, u.unit_Data.position.x);
                hash = Mix(hash, u.unit_Data.position.z);
                hash = Mix(hash, u.unit_Data.HP);
            }
        }

        long moraleSum = 0;
        if (controller.armies != null)
        {
            for (int i = 0; i < controller.armies.Count; i++)
            {
                Army a = controller.armies[i];
                if (a == null) continue;
                moraleSum += Mathf.RoundToInt(a.army_Data.morale * 100.0f);
            }
        }

        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine($"ticks={Simulation_Clock.tick}");
        sb.AppendLine($"alive={alive}");
        sb.AppendLine($"deaths={deaths}");
        sb.AppendLine($"charges={charges}");
        sb.AppendLine($"hpSum={hpSum}");
        sb.AppendLine($"moraleSum={moraleSum}");
        sb.AppendLine($"stateHash={hash:X16}");

        try
        {
            System.IO.File.WriteAllText(resultPath, sb.ToString());
            Debug.Log($"[MassBattle] 결과 기록: {resultPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MassBattle] 결과 기록 실패: {e.Message}");
        }
    }

    /// <summary>부동소수점의 비트 표현을 해시에 섞습니다.</summary>
    private static ulong Mix(ulong hash, float value)
    {
        unchecked
        {
            uint bits = (uint)System.BitConverter.SingleToInt32Bits(value);

            for (int i = 0; i < 4; i++)
            {
                hash ^= (byte)(bits >> (i * 8));
                hash *= 1099511628211UL;
            }

            return hash;
        }
    }

    private void OnGUI()
    {
        if (!bdrawGUI) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = Color.yellow;
        }

        int measured = Mathf.Max(1, tickCount - 1);
        double avg = totalMs / measured;

        int units = controller != null && controller.units != null ? controller.units.Count : 0;

        string text =
            $"유닛 {units} / 틱 {tickCount}\n" +
            $"평균 {avg:F2}ms  최악 {worstMs:F2}ms  스파이크 {spikeCount}\n" +
            $"사망 {deaths}  돌격 {charges}  붕괴 {routs}";

        GUI.Label(new Rect(10, 60, 600, 80), text, style);
    }
}
