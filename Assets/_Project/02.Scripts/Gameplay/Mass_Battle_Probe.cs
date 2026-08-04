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
    /// <summary>측정할 틱 수입니다. 이만큼 지나면 결과를 남기고 끝냅니다.</summary>
    [Header("측정")]
    [Tooltip("이 틱 수만큼 측정한 뒤 결과를 남깁니다. 0이면 계속 측정합니다.")]
    public int measureTicks = 600;

    /// <summary>이 시간을 넘긴 틱을 스파이크로 셉니다. 60fps 예산은 16.67ms입니다.</summary>
    [Tooltip("이 시간(ms)을 넘으면 스파이크로 셉니다. 60fps 예산은 16.6ms입니다.")]
    public float spikeThresholdMs = 20.0f;

    /// <summary>측정이 끝나면 애플리케이션을 종료할지 여부입니다. -out 인자를 주면 켜집니다.</summary>
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

    /// <summary>화면에 실시간 지표를 그릴지 여부입니다.</summary>
    [Header("표시")]
    [Tooltip("화면에 실시간 지표를 그립니다.")]
    public bool bdrawGUI = true;

    [Tooltip("단계별 비용과 스파이크 귀속을 함께 계측합니다.\n" +
             "배치모드에서는 -profile 인자로도 켤 수 있습니다.\n" +
             "계측 자체에 약간의 비용이 있으므로 평소에는 꺼 두십시오.")]
    /// <summary>단계별 비용과 스파이크 귀속을 함께 계측할지 여부입니다.</summary>
    public bool bprofile = true;

    /// <summary>검사 대상 컨트롤러입니다. Start에서 씬에서 찾습니다.</summary>
    private Controller controller;

    // --- 틱 시간 통계 ---
    /// <summary>지금까지 지난 틱 수입니다.</summary>
    private int tickCount;
    /// <summary>틱 시간의 누적 합(밀리초)입니다. 평균 계산에 씁니다.</summary>
    private double totalMs;
    /// <summary>관측된 가장 느린 틱(밀리초)입니다.</summary>
    private float worstMs;
    /// <summary>임계를 넘긴 틱 수입니다.</summary>
    private int spikeCount;
    /// <summary>틱 간격을 재는 스톱워치입니다.</summary>
    private System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

    // --- 전투 진행 통계 ---
    /// <summary>이번 실행에서 발생한 총 사망 수입니다.</summary>
    private int deaths;
    /// <summary>이번 실행에서 발생한 사기 붕괴 횟수입니다.</summary>
    private int routs;
    /// <summary>이번 실행에서 발생한 와해 횟수입니다.</summary>
    private int shatters;
    /// <summary>이번 실행에서 발생한 돌격 횟수입니다.</summary>
    private int charges;

    // --- 검증 결과 ---
    /// <summary>검출된 문제 목록입니다. 같은 종류는 한 번만 쌓입니다.</summary>
    private readonly List<string> problems = new List<string>();
    /// <summary>결과를 이미 보고했는지 여부입니다. 중복 보고를 막습니다.</summary>
    private bool breported;

    /// <summary>실시간 지표를 그리는 데 쓰는 스타일입니다.</summary>
    private GUIStyle style;

    /// <summary>배치모드 인자를 읽고 이벤트를 구독해 측정을 준비합니다.</summary>
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

        // 단계별 계측은 명시적으로 요청했을 때만 켭니다.
        //
        // 에디터에서 그냥 플레이하면 CLI 인자가 없으므로, 인스펙터 토글로도
        // 켤 수 있어야 합니다. 이것이 없어 에디터 실행에서는 단계별 표와
        // 스파이크 귀속이 통째로 비어 있었습니다.
        if (bprofile) Tick_Profiler.benabled = true;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-profile") Tick_Profiler.benabled = true;
            if (args[i] == "-forceRout") bforceRout = true;

            // 줌 스윕 측정 중에는 이 프로브가 애플리케이션을 끝내면 안 됩니다.
            //
            // 스윕은 줌 단계마다 수십~수백 프레임을 써야 하므로 600틱보다
            // 오래 걸립니다. 이쪽이 먼저 Quit하면 스윕이 중간에 잘려
            // 뒤쪽 줌 단계(넓게 보이는 구간)가 통째로 측정되지 않습니다.
            // 그 구간이 정확히 알고 싶은 부분이므로 치명적입니다.
            if (args[i] == "-zoomSweep") bzoomSweepMode = true;
        }

        Tick_Profiler.Reset();

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

        // 전투가 자리를 잡은 뒤 강제 패주를 겁니다.
        // 시작하자마자 걸면 부대가 아직 배치 중이라 도주 방향이 정해지지 않습니다.
        if (bforceRout && !broutForced && tickCount > 120)
        {
            broutForced = true;
            Apply_Force_Rout();
        }

        // 주기적으로 시뮬레이션 상태를 검증합니다.
        // 매 틱 전수 검사하면 그 비용이 측정값을 오염시키므로 간격을 둡니다.
        if (tickCount % 60 == 0) Validate();

        // 패주 검사는 더 자주 합니다.
        //
        // 패주는 몇 초 만에 끝나는 짧은 상태입니다. 60틱(1초) 간격으로
        // 보면 그 사이에 시작해 끝난 패주를 통째로 놓칩니다.
        // 실제로 첫 시도에서 붕괴가 7회 일어났는데 표본은 0회였습니다.
        if (tickCount % 10 == 0) Check_Rout_Ground_Alignment();

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

        // 진형 데이터와 마커 Transform도 검사합니다.
        //
        // 왜 추가했는가:
        // 이 검사가 없던 시절, 프로브는 "좌표/인덱스 이상 없음"을 보고하는데
        // Unity는 같은 실행에서 'Invalid localAABB. Object transform is corrupt.'와
        // 'IsFinite(distanceForSort)' 어서션을 쏟아냈습니다.
        //
        // 원인은 프로브가 '살아 있는 유닛의 unit_Data.position'만 봤기 때문입니다.
        // 손상된 것은 유닛이 아니라 진형 마커(UI_Unit)의 회전이었고,
        // 그 근원은 영벡터가 된 진형 방향이었습니다.
        Validate_Formation();

        Record(nanCount > 0, $"틱 {tickCount}: 좌표가 NaN인 유닛 {nanCount}명");
        Record(outOfRange > 0, $"틱 {tickCount}: 좌표가 전장을 벗어난 유닛 {outOfRange}명");
        Record(simIndexMismatch > 0,
               $"틱 {tickCount}: simIndex 불일치 {simIndexMismatch}건 " +
               "(Transform 동기화가 엉뚱한 자리를 건드립니다)");
        Record(armyIndexMismatch > 0,
               $"틱 {tickCount}: armyIndex 불일치 {armyIndexMismatch}건 " +
               "(킬 귀속이 엉뚱한 부대로 기록됩니다)");
    }

    /// <summary>
    /// 진형 방향과 마커 Transform이 손상되지 않았는지 검사합니다.
    ///
    /// 여기서 걸리는 값들이 Unity 렌더러의 'Invalid localAABB' /
    /// 'IsFinite(distanceForSort)' 어서션의 직접적인 원인입니다.
    /// 그 어서션은 렌더 스레드에서 나므로 스택이 게임 코드를 가리키지 않아,
    /// 이렇게 시뮬레이션 쪽에서 짚어 주지 않으면 원인 추적이 매우 어렵습니다.
    /// </summary>
    private void Validate_Formation()
    {
        List<Army> armies = controller != null ? controller.armies : Army.allArmies;
        if (armies == null) return;

        int zeroDirection = 0;
        int nanDirection = 0;
        int badPivot = 0;
        int badMarker = 0;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            // 1. 진형 방향 (모든 회전 계산의 근원)
            Formation_Data fd = army.GetFormation_Data();
            if (fd != null)
            {
                Vector3 d = fd.direction;

                if (Is_Bad(d)) nanDirection++;
                else if (d.sqrMagnitude < 0.0000001f) zeroDirection++;
            }

            // 2. 부대 기준점 Transform
            if (army.formation_Move_Transform != null)
            {
                if (Is_Bad(army.formation_Move_Transform.position)) badPivot++;
            }

            // 3. 진형 마커 (실제로 렌더링되는 대상)
            //    전량을 매번 보면 비싸므로 부대당 몇 개만 표본으로 확인합니다.
            //    손상은 부대 단위로 한꺼번에 생기므로 표본으로 충분합니다.
            List<UI_Unit> markers = army.uI_Units;
            if (markers == null) continue;

            int step = Mathf.Max(1, markers.Count / 4);

            for (int m = 0; m < markers.Count; m += step)
            {
                if (markers[m] == null) continue;

                Transform t = markers[m].transform;

                if (Is_Bad(t.position) || Is_Bad_Rotation(t.rotation))
                {
                    badMarker++;
                    break;
                }
            }
        }

        Record(nanDirection > 0,
               $"틱 {tickCount}: 진형 방향이 NaN/무한대인 부대 {nanDirection}개 " +
               "(마커 회전이 손상되어 렌더러 어서션이 납니다)");

        Record(zeroDirection > 0,
               $"틱 {tickCount}: 진형 방향이 영벡터인 부대 {zeroDirection}개 " +
               "(interval이 0이거나 direction이 영벡터로 들어왔습니다)");

        Record(badPivot > 0,
               $"틱 {tickCount}: 기준점 좌표가 손상된 부대 {badPivot}개");

        Record(badMarker > 0,
               $"틱 {tickCount}: 진형 마커 Transform이 손상된 부대 {badMarker}개 " +
               "(Invalid localAABB의 직접 원인입니다)");

        Validate_Targets();
    }

    /// <summary>
    /// 표적 좌표가 무한대인 채로 사용되고 있지 않은지 검사합니다.
    ///
    /// 왜 필요한가:
    /// Unit_target_Data.RemoveTarget()은 표적 좌표를 Vector3.positiveInfinity로
    /// 둡니다. 그 값이 투사체 궤적(Projectile_Renderer)이나 조향 목표로
    /// 흘러가면 NaN이 되어 렌더러가 'IsFinite(distanceForSort)' 어서션을 냅니다.
    ///
    /// 이 검사가 없어서, 프로브가 "좌표/인덱스 이상 없음"을 보고하는 동안
    /// Unity는 같은 실행에서 어서션을 쏟아내고 있었습니다.
    /// 유닛의 position만 봤을 뿐 '표적' 좌표는 보지 않았기 때문입니다.
    /// </summary>
    private void Validate_Targets()
    {
        List<Unit> units = controller != null ? controller.units : null;
        if (units == null) return;

        int badTarget = 0;
        int badSteering = 0;

        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;
            if (u.IsDead()) continue;

            // 표적이 있다고 표시된 유닛의 표적 좌표는 반드시 유한해야 합니다.
            if (u.unit_Data.btarget && Is_Bad(u.unit_Data.unit_Target_Data.position))
            {
                badTarget++;
            }

            if (Is_Bad(u.unit_Data.steeringTarget)) badSteering++;
        }

        Record(badTarget > 0,
               $"틱 {tickCount}: 표적 좌표가 무한대/NaN인 유닛 {badTarget}명 " +
               "(투사체 궤적이 손상되어 렌더러 어서션이 납니다)");

        Record(badSteering > 0,
               $"틱 {tickCount}: 조향 목표가 무한대/NaN인 유닛 {badSteering}명");
    }

    /// <summary>좌표가 NaN이거나 무한대인지 확인합니다.</summary>
    private static bool Is_Bad(Vector3 v)
    {
        return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
            || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
    }

    /// <summary>회전이 손상되었는지 확인합니다. 정규화된 쿼터니언은 크기가 1입니다.</summary>
    private static bool Is_Bad_Rotation(Quaternion q)
    {
        if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w))
            return true;

        float lengthSqr = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

        // 영쿼터니언(LookRotation이 영벡터를 받았을 때)이나 비정규 값을 잡습니다.
        return lengthSqr < 0.5f || lengthSqr > 2.0f;
    }

    /// <summary>
    /// 적군을 강제로 패주시킬지 여부입니다. (-forceRout)
    ///
    /// 왜 필요한가:
    /// 패주는 진형을 버리고 달아나는 별도 경로(Army.Move_Escape ->
    /// Unit.Move_Escape)를 탑니다. 그 경로가 일반 이동과 같은 처리를
    /// 받는지 확인하려면 실제로 패주가 일어나야 하는데,
    /// 평상시 700틱 실행에서는 붕괴가 0회입니다.
    /// </summary>
    private bool bforceRout;

    /// <summary>강제 패주를 이미 적용했는지 여부입니다.</summary>
    private bool broutForced;

    /// <summary>
    /// 적군 부대의 사기를 붕괴 직전으로 낮춥니다.
    ///
    /// 상태를 직접 Broken으로 대입하지 않는 이유:
    /// 그러면 시뮬레이션이 스스로 붕괴를 판정하는 경로(_Update_Morale)를
    /// 건너뛰게 되어, 실제 패주와 다른 상태가 만들어질 수 있습니다.
    /// 사기만 낮추고 판정은 시뮬레이션에 맡깁니다.
    /// </summary>
    private void Apply_Force_Rout()
    {
        if (controller == null || controller.armies == null) return;

        int affected = 0;

        for (int i = 0; i < controller.armies.Count; i++)
        {
            Army army = controller.armies[i];
            if (army == null) continue;

            // 적군만 무너뜨립니다. 양쪽이 다 도망가면 전투가 성립하지 않습니다.
            if (army.army_Data.bplayer) continue;

            army.army_Data.morale = 1.0f;
            army.army_Data.morale_Target = 0.0f;

            affected++;
        }

        Debug.Log($"[MassBattle] 패주 검증: 적군 {affected}개 부대의 사기를 낮췄습니다.");
    }

    /// <summary>패주 중인 유닛의 최대 지면 이격입니다.</summary>
    private float worstRoutGap;

    /// <summary>패주 중인 유닛을 실제로 표본에서 본 횟수입니다.</summary>
    private int routSamples;

    /// <summary>
    /// 패주 중인 부대의 유닛만 따로 지면 밀착을 검사합니다.
    ///
    /// 왜 따로 보는가:
    /// 전체 표본 검사는 9,600명에서 64명만 뽑으므로, 패주 부대가 소수일 때
    /// 그 유닛이 표본에 잡히지 않을 수 있습니다. 그러면 패주 경로에
    /// 문제가 있어도 "이탈 0%"로 통과해 버립니다.
    ///
    /// 패주는 진형을 버리고 달아나는 별도 경로(Move_Escape)를 타므로,
    /// 일반 이동과 같은 처리를 받는지 명시적으로 확인해야 합니다.
    /// </summary>
    private void Check_Rout_Ground_Alignment()
    {
        if (controller == null || controller.armies == null) return;

        const float tolerance = 1.0f;

        for (int a = 0; a < controller.armies.Count; a++)
        {
            Army army = controller.armies[a];
            if (army == null) continue;
            if (!army.army_Data.IsBroken()) continue;

            List<Unit> armyUnits = army.units;
            if (armyUnits == null) continue;

            // 부대마다 몇 명만 봅니다. 전수로 보면 레이캐스트가 폭증합니다.
            int stride = Mathf.Max(1, armyUnits.Count / 8);

            for (int i = 0; i < armyUnits.Count; i += stride)
            {
                Unit u = armyUnits[i];
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

                routSamples++;

                float gap = Mathf.Abs(p.y - hit.point.y);
                if (gap > worstRoutGap) worstRoutGap = gap;

                Record(gap > tolerance,
                       $"틱 {tickCount}: 패주 중인 유닛이 지면에서 {gap:F1}m 벗어났습니다. " +
                       "패주 경로가 지형 높이를 반영하지 않습니다.");
            }

            // 부대 기준점도 함께 봅니다.
            //
            // 기준점은 유닛과 전혀 다른 경로로 움직입니다.
            // (NavMeshAgent.Move, 지면 동기화 대상이 아님)
            // 진형 슬롯이 이 지점을 기준으로 생성되므로, 기준점이 뜨면
            // 슬롯도 함께 떠서 유닛이 매 틱 위아래로 끌려다닙니다.
            Vector3 pivot = army.formation_Move_Transform.position;
            Vector3 pivotOrigin = pivot + Vector3.up * Unit_Ground_Sync.rayStartHeight;

            if (Physics.Raycast(pivotOrigin, Vector3.down, out RaycastHit pivotHit,
                                Unit_Ground_Sync.rayDistance,
                                Unit_Ground_Sync.Ground_Mask))
            {
                float pivotGap = Mathf.Abs(pivot.y - pivotHit.point.y);
                if (pivotGap > worstRoutPivotGap) worstRoutPivotGap = pivotGap;

                Record(pivotGap > tolerance,
                       $"틱 {tickCount}: 패주 부대의 기준점이 지면에서 " +
                       $"{pivotGap:F1}m 벗어났습니다.");
            }
        }
    }

    /// <summary>패주 부대 기준점의 최대 지면 이격입니다.</summary>
    private float worstRoutPivotGap;

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

        // 이번 표본의 값입니다. 누적하면 한 번 어긋난 뒤로 영원히 보고합니다.
        transformOffCount = 0;

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

            // 실제 Transform도 함께 봅니다.
            //
            // 왜 필요한가:
            // 위 검사는 unit_Data.position을 지면과 비교합니다. 그런데 화면에
            // 보이는 것은 Transform이고, 둘은 매 틱 Write_Transform_Job이
            // 맞춰 줍니다. 그 동기화가 어긋나면 시뮬레이션은 지면에 붙어 있는데
            // 눈에는 파묻혀 보입니다.
            //
            // 실제로 프로브가 "지면 이격 0.00m"를 보고하는 동안 화면에서는
            // 유닛이 반쯤 잠겨 이동하는 현상이 보고되었습니다.
            // 시뮬레이션만 검사해서는 그 어긋남을 볼 수 없습니다.
            float drift = (u.transform.position - p).magnitude;

            if (drift > worstTransformDrift) worstTransformDrift = drift;
            if (drift > tolerance) transformOffCount++;
        }

        if (checkedCount == 0) return;

        if (worst > worstGroundGap) worstGroundGap = worst;
        groundOffRate = (float)offCount / checkedCount;

        Record(offCount > checkedCount / 2,
               $"틱 {tickCount}: 표본의 {offCount}/{checkedCount}이 지면에서 " +
               $"{tolerance}m 넘게 벗어났습니다 (최대 {worst:F1}m). " +
               "지형 높이가 반영되지 않고 있습니다.");

        // Transform 어긋남은 소수만 있어도 보고합니다.
        // 화면에 직접 드러나는 문제이므로 절반 기준을 쓰면 놓칩니다.
        Record(transformOffCount > 0,
               $"틱 {tickCount}: 표본 {transformOffCount}/{checkedCount}의 Transform이 " +
               $"시뮬레이션 위치와 {tolerance}m 넘게 어긋났습니다 " +
               $"(최대 {worstTransformDrift:F2}m). " +
               "화면에 보이는 위치가 실제 위치와 다릅니다.");
    }

    /// <summary>Transform과 시뮬레이션 위치의 최대 어긋남입니다.</summary>
    private float worstTransformDrift;

    /// <summary>이번 표본에서 Transform이 어긋난 유닛 수입니다.</summary>
    private int transformOffCount;

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
        sb.AppendLine($"패주 이격   : {worstRoutGap:F2} m (표본 {routSamples}회)");
        sb.AppendLine($"패주 기준점 : {worstRoutPivotGap:F2} m");
        sb.AppendLine($"Transform차 : {worstTransformDrift:F2} m (시뮬 위치와의 어긋남)");
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

        if (Tick_Profiler.benabled)
        {
            sb.AppendLine(Tick_Profiler.Report());
        }

        Debug.Log(sb.ToString());

        Write_Result_File();

        // 줌 스윕 중에는 종료를 그쪽에 맡깁니다. (위 -zoomSweep 주석 참고)
        if (bquitWhenDone && !bzoomSweepMode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>줌 스윕 측정 중인지 여부입니다. 참이면 이 프로브가 종료하지 않습니다.</summary>
    private bool bzoomSweepMode;

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

        // 프레임 타이밍을 함께 남깁니다.
        //
        // 왜 필요한가:
        // 이 값들이 없으면 빌드 실행 결과를 에디터 실행과 비교할 수 없습니다.
        // 지금 풀어야 할 질문이 정확히 그것입니다.
        //   "스파이크의 95%가 시뮬레이션 바깥인데, 에디터 탓인가 렌더링인가"
        // 빌드에는 Scene view도 Inspector도 없으므로, 두 실행의 차이가
        // 곧 에디터 오버헤드의 크기입니다.
        int measured = Mathf.Max(1, tickCount - 1);

        sb.AppendLine($"avgTickMs={totalMs / measured:F3}");
        sb.AppendLine($"worstTickMs={worstMs:F3}");
        sb.AppendLine($"spikes={spikeCount}");
        sb.AppendLine($"spikeRate={(double)spikeCount / measured * 100.0:F2}");
        sb.AppendLine($"measuredTicks={measured}");

        // 시뮬레이션 내부 비용도 함께 남겨야 '바깥'을 뺄셈으로 구할 수 있습니다.
        if (Tick_Profiler.benabled)
        {
            sb.AppendLine($"simTotalMs={Tick_Profiler.Average_Total_Ms():F3}");
            sb.AppendLine($"simSpikes={Tick_Profiler.SpikeSamples}");
        }

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

    /// <summary>측정 중인 지표를 화면에 실시간으로 그립니다.</summary>
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
