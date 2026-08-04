using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 카메라 줌 단계별로 '화면에 보이는 유닛 수 대 프레임 비용'을 재는 프로브입니다.
///
/// 왜 필요한가:
/// 지금까지의 측정은 전부 카메라가 고정된 상태였습니다. 그래서 "화면에 많이
/// 보이면 프레임이 떨어진다"는 현상이 측정에 아예 포함되지 않았습니다.
/// 시뮬레이션 비용은 화면과 무관하므로(전 유닛을 항상 계산합니다),
/// 보이는 수에 비례해 늘어나는 비용은 정의상 렌더링 쪽입니다.
///
/// 무엇을 재는가:
/// 줌을 여러 단계로 옮겨 가며 각 단계에서
///   1) 화면 안에 들어온 유닛 수 (절두체 판정)
///   2) 프레임 시간 (Time.unscaledDeltaTime)
///   3) 시뮬레이션 시간 (Tick_Profiler)
/// 를 모읍니다. 1과 2의 관계가 곧 답입니다.
///
/// 판독 방법:
///   - 보이는 수에 비례해 프레임이 늘면      -> 유닛당 렌더링 비용 (드로우콜/배칭)
///   - 보이는 수와 무관하게 평평하면          -> 렌더링이 아니라 다른 원인
///   - 특정 구간부터 급격히 꺾이면            -> 배칭이 깨지는 임계점
///
/// 왜 시뮬레이션 시간도 함께 재는가:
/// 줌을 당기면 프레임이 느려지는 것이 '렌더링 때문'인지 확인하려면,
/// 같은 구간에서 시뮬레이션이 변하지 않았음을 보여야 합니다.
/// 시뮬레이션도 함께 늘었다면 그건 줌 탓이 아니라 전투 상황 탓입니다.
///
/// 사용법:
///   RTXX_Test.exe -batchmode -zoomSweep -out result.txt
/// </summary>
public class Zoom_Sweep_Probe : MonoBehaviour
{
    /// <summary>각 줌 단계에서 측정할 프레임 수입니다. 많을수록 값이 안정됩니다.</summary>
    [Header("측정")]
    [Tooltip("각 줌 단계에서 측정할 프레임 수입니다. 앞쪽 몇 프레임은 버립니다.")]
    public int framesPerStep = 120;

    /// <summary>
    /// 줌을 바꾼 뒤 버릴 프레임 수입니다.
    ///
    /// 카메라 이동 직후에는 컬링 결과와 셰이더 워밍업이 안정되지 않아
    /// 그 구간을 측정에 넣으면 값이 부풀려집니다.
    /// </summary>
    [Tooltip("줌을 바꾼 뒤 안정화까지 버릴 프레임 수입니다.")]
    public int warmupFrames = 30;

    /// <summary>
    /// 측정할 orthographicSize 값들입니다. 클수록 넓게(멀리) 보입니다.
    ///
    /// 왜 카메라 위치가 아니라 orthographicSize인가:
    /// 이 프로젝트의 카메라는 직교(orthographic) 투영입니다. 직교 카메라는
    /// 뒤로 물러나도 보이는 범위가 전혀 넓어지지 않습니다. 실제 줌은
    /// Camera_Player가 orthographicSize를 조절해 구현합니다. (Camera_Player.cs:282)
    ///
    /// 첫 측정에서 이걸 놓쳐 카메라를 뒤로 물렸다가, 넓은 구간에서
    /// visible=0이 나오는 무효 데이터를 얻었습니다. (far clip 200에도 걸렸습니다)
    /// </summary>
    [Tooltip("측정할 orthographicSize 값들입니다. 클수록 넓게 보입니다.")]
    public float[] zoomSteps = { 5.0f, 10.0f, 20.0f, 30.0f, 45.0f, 60.0f, 80.0f, 100.0f };

    /// <summary>결과 파일 경로입니다. -zoomOut 인자로 지정합니다.</summary>
    [Header("출력")]
    [Tooltip("결과 파일 경로입니다. 비우면 로그로만 남깁니다.")]
    public string resultPath;

    /// <summary>측정이 끝나면 애플리케이션을 종료할지 여부입니다.</summary>
    [Tooltip("측정이 끝나면 애플리케이션을 종료합니다.")]
    public bool bquitWhenDone;

    /// <summary>이 프로브가 동작할지 여부입니다. -zoomSweep 인자로 켭니다.</summary>
    private bool bactive;

    /// <summary>유닛 목록을 읽어 올 컨트롤러입니다.</summary>
    private Controller controller;
    /// <summary>줌을 조절할 대상 카메라입니다.</summary>
    private Camera cam;
    /// <summary>카메라의 Transform입니다. 위치를 옮길 때 씁니다.</summary>
    private Transform camTransform;

    /// <summary>현재 측정 중인 줌 단계 인덱스입니다.</summary>
    private int stepIndex;

    /// <summary>이번 단계에서 지난 프레임 수입니다.</summary>
    private int stepFrames;

    /// <summary>이번 단계의 프레임 시간 누적입니다.</summary>
    private double stepFrameMs;

    /// <summary>이번 단계의 최악 프레임입니다.</summary>
    private float stepWorstMs;

    /// <summary>이번 단계의 화면 내 유닛 수 누적입니다.</summary>
    private long stepVisibleSum;

    /// <summary>이번 단계의 시뮬레이션 시간 누적 기준점입니다.</summary>
    private double stepSimStartMs;

    /// <summary>단계별 측정 결과 줄입니다. 마지막에 파일로 씁니다.</summary>
    private readonly List<string> rows = new List<string>();
    /// <summary>모든 단계를 마쳤는지 여부입니다. 중복 종료를 막습니다.</summary>
    private bool bdone;

    /// <summary>측정 인자를 읽고 카메라를 첫 줌 단계로 맞춥니다.</summary>
    private void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-zoomSweep") bactive = true;

            // 전용 인자를 씁니다.
            //
            // -out은 Mass_Battle_Probe가 전투 결과를 적는 경로입니다.
            // 같은 파일을 쓰면 나중에 끝나는 쪽이 상대의 결과를 지웁니다.
            if (i < args.Length - 1 && args[i] == "-zoomOut")
            {
                resultPath = args[i + 1];
                bquitWhenDone = true;
            }
        }

        if (!bactive)
        {
            enabled = false;
            return;
        }

        controller = FindAnyObjectByType<Controller>();
        cam = Main_Camera.Get();
        camTransform = Main_Camera.GetTransform();

        if (controller == null || cam == null)
        {
            Debug.LogError("[ZoomSweep] Controller 또는 카메라를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        // 계측이 꺼져 있으면 시뮬레이션 시간을 비교할 수 없습니다.
        Tick_Profiler.benabled = true;

        // 카메라 조작 스크립트를 끕니다.
        //
        // Camera_Player.Update()가 매 프레임 orthographicSize를 자기 값으로
        // 되돌리므로(Camera_Player.cs:282), 켜 둔 채로는 이 프로브가 설정한
        // 줌이 한 프레임도 유지되지 않습니다.
        Camera_Player player = FindAnyObjectByType<Camera_Player>();
        if (player != null) player.enabled = false;

        Apply_Zoom(zoomSteps[0]);

        Debug.Log($"[ZoomSweep] 시작. {zoomSteps.Length}단계 x {framesPerStep}프레임");
    }

    /// <summary>
    /// 카메라를 지정한 높이로 옮깁니다.
    ///
    /// Camera_Player의 줌 로직을 쓰지 않고 직접 옮기는 이유:
    /// 그쪽은 마우스 입력과 Lerp 보간에 묶여 있어 '정확히 이 거리'를
    /// 만들 수 없습니다. 측정은 단계가 정확히 재현되어야 의미가 있습니다.
    /// </summary>
    private void Apply_Zoom(float size)
    {
        if (camTransform == null || cam == null) return;

        // 카메라를 전장 중심 위로 옮깁니다.
        //
        // 위치는 모든 단계에서 동일하게 유지합니다. 직교 카메라이므로
        // 보이는 범위는 orthographicSize 하나로만 결정되고, 위치를 함께
        // 바꾸면 '무엇이 범위를 바꿨는지' 구분할 수 없게 됩니다.
        Vector3 center = Get_Battle_Center();

        // far clip이 200이므로 그 안에 들어오는 거리를 씁니다.
        // 45도 부감을 유지하되 거리는 고정입니다.
        const float rigDistance = 60.0f;
        Vector3 offset = new Vector3(0.0f, rigDistance, -rigDistance);

        camTransform.position = center + offset;
        camTransform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

        // 실제 줌입니다. 이 한 줄이 보이는 범위를 결정합니다.
        cam.orthographicSize = size;
    }

    /// <summary>전 부대의 평균 위치입니다. 카메라가 바라볼 지점입니다.</summary>
    private Vector3 Get_Battle_Center()
    {
        if (controller == null || controller.armies == null) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < controller.armies.Count; i++)
        {
            Army a = controller.armies[i];
            if (a == null) continue;
            if (a.units.Count == 0) continue;

            sum += a.GetPosition();
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    /// <summary>
    /// 카메라 절두체 안에 들어온 유닛 수를 셉니다.
    ///
    /// 실제 렌더링 대상과 정확히 같지는 않습니다. (Unity의 컬링은 바운딩
    /// 볼륨 기준이고 오클루전도 고려합니다) 다만 '보이는 수'의 대리 지표로
    /// 충분하며, 줌 단계 간 상대 비교가 목적이므로 절대값은 중요하지 않습니다.
    /// </summary>
    private int Count_Visible_Units()
    {
        if (controller == null || controller.units == null) return 0;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);

        List<Unit> units = controller.units;
        int visible = 0;

        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;
            if (u.IsDead()) continue;

            // 유닛을 작은 구로 근사합니다. 스프라이트 크기보다 넉넉하면 됩니다.
            Vector3 p = u.unit_Data.position;

            if (Is_Inside(planes, p, 1.0f)) visible++;
        }

        return visible;
    }

    /// <summary>점이 절두체 안에 있는지 판정합니다.</summary>
    private static bool Is_Inside(Plane[] planes, Vector3 point, float radius)
    {
        for (int i = 0; i < planes.Length; i++)
        {
            if (planes[i].GetDistanceToPoint(point) < -radius) return false;
        }

        return true;
    }

    /// <summary>
    /// 프레임을 세며 측정하고, 정해진 수를 채우면 다음 줌 단계로 넘어갑니다.
    /// </summary>
    private void Update()
    {
        if (bdone) return;

        stepFrames++;

        // 줌을 바꾼 직후 몇 프레임은 버립니다.
        // 카메라 이동 직후에는 컬링 결과와 셰이더 워밍업이 안정되지 않습니다.
        if (stepFrames <= warmupFrames)
        {
            if (stepFrames == warmupFrames)
            {
                // 본 측정 시작 시점의 시뮬레이션 누적을 기록해 둡니다.
                stepSimStartMs = Tick_Profiler.Average_Total_Ms();
            }
            return;
        }

        float ms = Time.unscaledDeltaTime * 1000.0f;

        stepFrameMs += ms;
        if (ms > stepWorstMs) stepWorstMs = ms;
        stepVisibleSum += Count_Visible_Units();

        if (stepFrames < warmupFrames + framesPerStep) return;

        Record_Step();
        Next_Step();
    }

    /// <summary>한 줌 단계의 측정 결과를 기록합니다.</summary>
    private void Record_Step()
    {
        int measured = framesPerStep;

        double avgMs = stepFrameMs / measured;
        double avgVisible = (double)stepVisibleSum / measured;
        double simMs = Tick_Profiler.Average_Total_Ms();

        float height = zoomSteps[stepIndex];

        rows.Add($"zoom={height:F0} visible={avgVisible:F0} " +
                 $"frameMs={avgMs:F3} worstMs={stepWorstMs:F3} simMs={simMs:F3}");

        Debug.Log($"[ZoomSweep] 높이 {height:F0}: " +
                  $"보이는 유닛 {avgVisible:F0}명, " +
                  $"프레임 {avgMs:F2} ms (최악 {stepWorstMs:F2}), " +
                  $"시뮬 {simMs:F2} ms");
    }

    /// <summary>다음 줌 단계로 넘어갑니다. 마지막이면 결과를 남깁니다.</summary>
    private void Next_Step()
    {
        stepIndex++;

        stepFrames = 0;
        stepFrameMs = 0.0;
        stepWorstMs = 0.0f;
        stepVisibleSum = 0;

        if (stepIndex >= zoomSteps.Length)
        {
            Finish();
            return;
        }

        Apply_Zoom(zoomSteps[stepIndex]);
    }

    /// <summary>측정을 끝내고 결과를 남깁니다.</summary>
    private void Finish()
    {
        bdone = true;

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("# 줌 단계별 렌더링 비용");
        sb.AppendLine($"# 유닛 {controller.units.Count}명 / 프레임당 {framesPerStep}회 측정");

        for (int i = 0; i < rows.Count; i++)
        {
            sb.AppendLine(rows[i]);
        }

        Debug.Log("[ZoomSweep] 완료\n" + sb);

        if (!string.IsNullOrEmpty(resultPath))
        {
            try
            {
                System.IO.File.WriteAllText(resultPath, sb.ToString());
                Debug.Log($"[ZoomSweep] 결과 기록: {resultPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ZoomSweep] 기록 실패: {e.Message}");
            }
        }

        if (bquitWhenDone)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }
    }
}
