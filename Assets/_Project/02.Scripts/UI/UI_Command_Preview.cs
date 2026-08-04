using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우클릭 드래그로 진형을 그리는 동안 그 결과를 미리 보여 줍니다.
///
/// 왜 필요한가:
/// 지금까지 드래그 중에 보이는 것은 깃발 두 개(Flag1/Flag2)뿐이었습니다.
/// 그 사이에 부대가 어떻게 늘어설지, 어느 쪽을 보게 될지,
/// 여러 부대를 선택했다면 누가 어느 구간을 맡을지는 전혀 보이지 않습니다.
///
/// 이 게임에서 전열의 '방향'은 전투 결과를 직접 바꿉니다.
/// 측후방을 내주면 방어력과 방패가 무력화되기 때문입니다.
/// 그런데 명령을 내리는 순간 그 방향이 안 보인다면, 플레이어는
/// 결과를 보고 나서야 자기가 무엇을 시켰는지 알게 됩니다.
///
/// 구현 방식:
/// 화면 좌표로 선을 그립니다. 월드에 메시를 만들지 않는 이유는
/// 드래그 중에만 쓰는 표현에 오브젝트 생성/파괴 비용을 들일 이유가
/// 없기 때문입니다. 카메라로 월드->화면 변환만 하면 됩니다.
/// </summary>
public class UI_Command_Preview : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>드래그 상태를 읽어 올 컨트롤러입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    /// <summary>전열선의 두께(픽셀)입니다.</summary>
    [Header("표시")]
    [Tooltip("전열선의 두께입니다.")]
    public float lineThickness = 2.0f;

    /// <summary>정면 방향 화살표의 길이(픽셀)입니다.</summary>
    [Tooltip("정면 방향 화살표의 길이입니다.")]
    public float arrowLength = 26.0f;

    /// <summary>정면 화살표를 몇 개 그릴지입니다.</summary>
    [Tooltip("전열선을 따라 그릴 정면 화살표 개수입니다.")]
    public int arrowCount = 5;

    // 비공개 멤버 변수
    /// <summary>전열선 색입니다.</summary>
    private static readonly Color color_Line = new Color(0.98f, 0.90f, 0.50f, 0.95f);

    /// <summary>정면 화살표 색입니다. 선보다 옅게 두어 선이 주인공이 되게 합니다.</summary>
    private static readonly Color color_Arrow = new Color(0.98f, 0.90f, 0.50f, 0.60f);

    /// <summary>부대 구간을 가르는 눈금 색입니다.</summary>
    private static readonly Color color_Tick = new Color(1.0f, 1.0f, 1.0f, 0.75f);

    /// <summary>안내 문구 색입니다.</summary>
    private static readonly Color color_Text = new Color(0.90f, 0.86f, 0.76f, 1.0f);

    /// <summary>사각형을 채우는 데 쓰는 1x1 텍스처입니다.</summary>
    private Texture2D pixel;

    /// <summary>안내 문구 스타일입니다.</summary>
    private GUIStyle style;

    // Unity 이벤트 함수
    /// <summary>사각형 채우기에 쓸 1x1 텍스처를 만듭니다.</summary>
    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>동적으로 만든 텍스처를 반납합니다.</summary>
    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }

    /// <summary>비어 있는 참조를 씬에서 찾아 채웁니다.</summary>
    private void Start()
    {
        if (controller == null) controller = FindAnyObjectByType<Controller>();
    }

    /// <summary>드래그 중이면 진형 미리보기를 그립니다.</summary>
    private void OnGUI()
    {
        if (controller == null) return;

        // 진형선으로 인정될 만큼 끌었을 때만 그립니다.
        // 짧은 드래그는 '단순 클릭 이동'이라 전열 방향이 정해지지 않습니다.
        if (!controller.bdrag || !controller.bformation) return;

        Camera cam = Main_Camera.Get();
        if (cam == null) return;

        Ensure_Style();

        Vector3 startWorld = controller.formation_Start;
        Vector3 endWorld = controller.formation_End;

        Vector3 axis = endWorld - startWorld;
        axis.y = 0.0f;

        if (axis.sqrMagnitude < 0.0001f) return;

        Vector2 a = World_To_Screen(cam, startWorld);
        Vector2 b = World_To_Screen(cam, endWorld);

        // 카메라 뒤에 있으면 화면 좌표가 뒤집혀 엉뚱한 선이 그려집니다.
        if (!Is_In_Front(cam, startWorld) || !Is_In_Front(cam, endWorld)) return;

        // 1) 전열축 — 부대가 늘어설 선입니다.
        Draw_Line(a, b, color_Line, lineThickness);

        // 2) 정면 방향 — 이 게임에서 가장 중요한 정보입니다.
        Draw_Facing_Arrows(cam, startWorld, endWorld, axis);

        // 3) 부대별 구간 — 여러 부대를 선택했을 때 누가 어디에 서는지
        Draw_Army_Ticks(cam, startWorld, axis);

        // 4) 요약 — 길이와 부대 수
        Draw_Summary(a, b, axis.magnitude);
    }

    // 비공개 메서드
    /// <summary>안내 문구 스타일을 처음 한 번만 만듭니다.</summary>
    private void Ensure_Style()
    {
        if (style != null) return;

        style = new GUIStyle(GUI.skin.label);
        style.fontSize = 11;
        style.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>
    /// 전열선을 따라 정면 방향 화살표를 그립니다.
    ///
    /// 정면은 전열축에 수직입니다. 그 규약은 Formation_Util이 정하며,
    /// 여기서도 같은 함수를 써야 실제 배치와 미리보기가 어긋나지 않습니다.
    /// </summary>
    /// <param name="cam">화면 변환에 쓸 카메라입니다.</param>
    /// <param name="startWorld">전열 시작 월드 좌표입니다.</param>
    /// <param name="endWorld">전열 끝 월드 좌표입니다.</param>
    /// <param name="axis">전열축 벡터입니다.</param>
    private void Draw_Facing_Arrows(Camera cam, Vector3 startWorld,
                                    Vector3 endWorld, Vector3 axis)
    {
        // 실제 배치와 같은 규약으로 정면을 구합니다.
        Vector3 facing = Formation_Util.Facing_From_Line(axis);

        int count = Mathf.Max(2, arrowCount);

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0.5f;

            Vector3 onLine = Vector3.Lerp(startWorld, endWorld, t);

            if (!Is_In_Front(cam, onLine)) continue;

            Vector2 from = World_To_Screen(cam, onLine);

            // 월드에서 조금 앞으로 나간 지점을 화면으로 옮겨 방향을 구합니다.
            //
            // 화면 좌표에서 바로 수직을 잡지 않는 이유:
            // 카메라가 기울어져 있으면 화면상의 수직과 월드의 수직이
            // 다릅니다. 월드에서 계산해야 실제 방향과 일치합니다.
            Vector2 to = World_To_Screen(cam, onLine + facing * 3.0f);

            Vector2 dir = (to - from).normalized;

            Draw_Line(from, from + dir * arrowLength, color_Arrow, lineThickness);

            // 화살촉을 두 선으로 표현합니다.
            Vector2 tip = from + dir * arrowLength;
            Vector2 side = new Vector2(-dir.y, dir.x) * 4.0f;

            Draw_Line(tip, tip - dir * 6.0f + side, color_Arrow, 1.5f);
            Draw_Line(tip, tip - dir * 6.0f - side, color_Arrow, 1.5f);
        }
    }

    /// <summary>
    /// 선택된 부대들이 각각 맡을 구간의 경계를 눈금으로 표시합니다.
    ///
    /// 여러 부대를 함께 선택하면 드래그한 선을 나눠 서게 되는데,
    /// 그 경계가 보이지 않으면 어느 부대가 어디에 설지 알 수 없습니다.
    /// </summary>
    /// <param name="cam">화면 변환에 쓸 카메라입니다.</param>
    /// <param name="startWorld">전열 시작 월드 좌표입니다.</param>
    /// <param name="axis">전열축 벡터입니다.</param>
    private void Draw_Army_Ticks(Camera cam, Vector3 startWorld, Vector3 axis)
    {
        List<Army> selected = controller.armies_Selected;
        if (selected == null || selected.Count <= 1) return;

        Vector3 dir = axis.normalized;
        float offset = 0.0f;

        for (int i = 0; i < selected.Count - 1; i++)
        {
            Army army = selected[i];
            if (army == null) continue;

            offset += army.GetFormation_Length();

            // 드래그한 길이를 넘어가면 그 부대는 자리를 못 받습니다.
            if (offset > axis.magnitude) break;

            Vector3 world = startWorld + dir * offset;
            if (!Is_In_Front(cam, world)) continue;

            Vector2 p = World_To_Screen(cam, world);

            // 선에 수직인 짧은 눈금입니다.
            Vector2 screenDir = (World_To_Screen(cam, world + dir) - p).normalized;
            Vector2 perp = new Vector2(-screenDir.y, screenDir.x) * 5.0f;

            Draw_Line(p - perp, p + perp, color_Tick, 1.5f);
        }
    }

    /// <summary>
    /// 전열 길이와 배치될 부대 수를 선 가운데에 표시합니다.
    /// </summary>
    /// <param name="a">전열 시작 화면 좌표입니다.</param>
    /// <param name="b">전열 끝 화면 좌표입니다.</param>
    /// <param name="length">전열 길이(월드 단위)입니다.</param>
    private void Draw_Summary(Vector2 a, Vector2 b, float length)
    {
        List<Army> selected = controller.armies_Selected;
        int count = selected != null ? selected.Count : 0;

        Vector2 mid = (a + b) * 0.5f;

        string text = count > 1
            ? $"{length:F0} m  ·  부대 {count}개"
            : $"{length:F0} m";

        Rect rect = new Rect(mid.x - 60.0f, mid.y - 26.0f, 120.0f, 16.0f);

        // 글자가 전장에 묻히지 않도록 어두운 바탕을 깝니다.
        Draw_Rect(new Rect(rect.x, rect.y, rect.width, rect.height),
                  new Color(0.0f, 0.0f, 0.0f, 0.55f));

        Color previous = GUI.color;
        GUI.color = color_Text;
        GUI.Label(rect, text, style);
        GUI.color = previous;
    }

    /// <summary>월드 좌표를 GUI 화면 좌표로 옮깁니다.</summary>
    /// <param name="cam">변환에 쓸 카메라입니다.</param>
    /// <param name="world">변환할 월드 좌표입니다.</param>
    /// <returns>GUI 좌표계(좌상단 원점)의 화면 좌표입니다.</returns>
    private static Vector2 World_To_Screen(Camera cam, Vector3 world)
    {
        Vector3 p = cam.WorldToScreenPoint(world);

        // Unity의 화면 좌표는 좌하단이 원점이고 GUI는 좌상단이 원점입니다.
        return new Vector2(p.x, Screen.height - p.y);
    }

    /// <summary>
    /// 월드 지점이 카메라 앞에 있는지 확인합니다.
    ///
    /// 뒤에 있으면 WorldToScreenPoint가 뒤집힌 좌표를 돌려주어
    /// 화면 반대편에 엉뚱한 선이 그려집니다.
    /// </summary>
    /// <param name="cam">기준 카메라입니다.</param>
    /// <param name="world">확인할 월드 좌표입니다.</param>
    /// <returns>카메라 앞에 있으면 true입니다.</returns>
    private static bool Is_In_Front(Camera cam, Vector3 world)
    {
        return cam.WorldToScreenPoint(world).z > 0.0f;
    }

    /// <summary>
    /// 두 화면 좌표를 잇는 선을 그립니다.
    ///
    /// GUI에는 선 그리기가 없어 사각형을 회전시켜 대신합니다.
    /// GUIUtility.RotateAroundPivot으로 좌표계를 돌린 뒤 가로 사각형을
    /// 그리고 되돌리는 방식입니다.
    /// </summary>
    /// <param name="from">시작 화면 좌표입니다.</param>
    /// <param name="to">끝 화면 좌표입니다.</param>
    /// <param name="color">선 색입니다.</param>
    /// <param name="thickness">선 두께(픽셀)입니다.</param>
    private void Draw_Line(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;

        if (length < 0.5f) return;

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        Matrix4x4 saved = GUI.matrix;
        Color previousColor = GUI.color;

        GUIUtility.RotateAroundPivot(angle, from);
        GUI.color = color;

        GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness),
                        pixel);

        GUI.color = previousColor;
        GUI.matrix = saved;
    }

    /// <summary>단색 사각형을 채웁니다.</summary>
    /// <param name="rect">채울 영역입니다.</param>
    /// <param name="color">채울 색입니다.</param>
    private void Draw_Rect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(rect, pixel);

        GUI.color = previous;
    }
}
