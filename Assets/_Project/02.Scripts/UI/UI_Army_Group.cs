using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대 그룹(제어 그룹)을 저장하고 호출하는 계층입니다.
///
/// 왜 필요한가:
/// 이 게임은 부대 수십 개를 동시에 지휘합니다. 그런데 지금 부대를 고르는
/// 방법은 화면에서 직접 드래그하는 것뿐입니다. 즉 **화면 밖에 있는 부대는
/// 다시 고를 방법이 없습니다.** 좌익이 무너지는 것을 미니맵으로 보고도,
/// 예비대를 부르려면 카메라를 그쪽으로 돌려 찾아 드래그해야 합니다.
///
/// 그 몇 초가 대규모 전투에서는 부대 하나 값입니다.
/// 그룹은 '카메라와 무관하게 부대를 지목하는 수단'입니다.
///
/// ---------------------------------------------------------------------
/// 왜 Ctrl+숫자가 아니라 F1~F8인가
/// ---------------------------------------------------------------------
/// RTS의 관례는 Ctrl+숫자입니다. 그런데 이 프로젝트에서는 둘 다 이미
/// 다른 뜻을 갖고 있습니다.
///
///   숫자 1~5 : 태세 전환 (Controller.keyCode_Stance_*)
///   Ctrl     : 선택 추가 (Controller.keyCode_disable_clear)
///
/// 즉 Ctrl+1은 '선택을 유지한 채 전열 태세로'라는 조합으로 이미 읽힙니다.
/// 여기에 그룹 저장을 겹치면 태세 단축키가 Ctrl을 누른 채로는 동작하지
/// 않게 되어, 사용자가 원인을 알 수 없는 방식으로 기능이 사라집니다.
///
/// 관례를 따르려다 이미 있는 기능을 망가뜨리는 것보다,
/// 비어 있는 F1~F8을 쓰는 편이 낫습니다. 단축키는 인스펙터에서 바꿀 수
/// 있으므로 태세를 다른 키로 옮기면 언제든 관례로 되돌릴 수 있습니다.
///
/// ---------------------------------------------------------------------
/// 죽은 부대 처리
/// ---------------------------------------------------------------------
/// 그룹에 담긴 부대는 전투 중에 전멸해 파괴될 수 있습니다.
/// 저장 시점에 검사해 봐야 소용없고, **호출할 때마다** 걸러야 합니다.
/// (Unity의 파괴된 오브젝트는 == null 이 true가 되므로 그것으로 거릅니다)
/// </summary>
public class UI_Army_Group : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>선택 목록을 읽고 쓸 컨트롤러입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    /// <summary>그룹 저장 조합에 쓰는 보조 키입니다.</summary>
    [Header("입력")]
    [Tooltip("이 키를 누른 채 그룹 키를 누르면 현재 선택을 저장합니다.")]
    public KeyCode keyCode_Assign = KeyCode.LeftShift;

    /// <summary>
    /// 그룹 호출/저장에 쓰는 키들입니다. 배열 순서가 곧 그룹 번호입니다.
    ///
    /// 숫자키를 피한 이유는 클래스 요약을 보십시오.
    /// </summary>
    [Tooltip("순서대로 1번 그룹부터 대응합니다.")]
    public KeyCode[] keyCode_Groups =
    {
        KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4,
        KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8,
    };

    /// <summary>그룹 표시줄을 화면에 그릴지 여부입니다.</summary>
    [Header("표시")]
    [Tooltip("끄면 단축키만 동작하고 화면에는 그리지 않습니다.")]
    public bool bdrawBar = true;

    /// <summary>화면 왼쪽 가장자리와의 여백(픽셀)입니다.</summary>
    [Tooltip("화면 왼쪽 가장자리와의 여백입니다.")]
    public float margin = 10.0f;

    /// <summary>
    /// 미니맵을 피해 위로 띄우는 높이(픽셀)입니다.
    ///
    /// 기본값 302는 미니맵 위에 얹히도록 계산한 값입니다.
    ///   UI_Minimap.bottomOffset(106) + mapSize(190) + 여백(6)
    /// 미니맵 크기나 위치를 바꾸면 이 값도 함께 옮겨야 겹치지 않습니다.
    /// </summary>
    [Tooltip("미니맵을 피해 위로 띄우는 높이입니다. 미니맵 크기를 바꾸면 함께 조정하십시오.")]
    public float bottomOffset = 302.0f;

    // 비공개 멤버 변수
    /// <summary>칸 하나의 크기(픽셀)입니다.</summary>
    private const float slotSize = 26.0f;

    /// <summary>칸 사이 간격(픽셀)입니다.</summary>
    private const float slotGap = 3.0f;

    /// <summary>바탕색입니다.</summary>
    private static readonly Color color_Panel = new Color(0.07f, 0.06f, 0.05f, 0.85f);

    /// <summary>비어 있는 칸의 색입니다.</summary>
    private static readonly Color color_Empty = new Color(0.16f, 0.15f, 0.13f, 0.75f);

    /// <summary>부대가 담긴 칸의 색입니다.</summary>
    private static readonly Color color_Filled = new Color(0.22f, 0.20f, 0.13f, 0.95f);

    /// <summary>테두리색입니다.</summary>
    private static readonly Color color_Edge = new Color(0.55f, 0.45f, 0.25f, 0.80f);

    /// <summary>글자색입니다.</summary>
    private static readonly Color color_Text = new Color(0.88f, 0.84f, 0.74f, 1.0f);

    /// <summary>비어 있는 칸의 글자색입니다.</summary>
    private static readonly Color color_Text_Dim = new Color(0.45f, 0.42f, 0.37f, 1.0f);

    /// <summary>
    /// 그룹별 부대 목록입니다. keyCode_Groups와 길이가 같습니다.
    ///
    /// 리스트를 새로 만들지 않고 Clear 후 채우는 이유:
    /// 그룹 저장은 전투 중에 반복되는 조작이라, 매번 새 리스트를 만들면
    /// 그만큼 GC가 쌓입니다. 칸은 시작할 때 한 번만 만듭니다.
    /// </summary>
    private List<Army>[] groups;

    /// <summary>그룹 번호 스타일입니다.</summary>
    private GUIStyle style_Number;

    /// <summary>부대 수 스타일입니다.</summary>
    private GUIStyle style_Count;

    /// <summary>사각형을 채우는 데 쓰는 1x1 텍스처입니다.</summary>
    private Texture2D pixel;

    // Unity 이벤트 함수
    /// <summary>그룹 저장소와 사각형 채우기용 텍스처를 준비합니다.</summary>
    private void Awake()
    {
        groups = new List<Army>[keyCode_Groups.Length];

        for (int i = 0; i < groups.Length; i++) groups[i] = new List<Army>(8);

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

    /// <summary>그룹 단축키를 처리합니다.</summary>
    private void Update()
    {
        if (controller == null) return;

        bool bassign = Input.GetKey(keyCode_Assign);

        for (int i = 0; i < keyCode_Groups.Length; i++)
        {
            if (!Input.GetKeyDown(keyCode_Groups[i])) continue;

            if (bassign) Assign(i);
            else Recall(i);

            // 같은 프레임에 두 그룹이 눌리는 일은 없다고 봅니다.
            break;
        }
    }

    /// <summary>그룹 표시줄을 그립니다.</summary>
    private void OnGUI()
    {
        if (!bdrawBar || groups == null) return;

        Ensure_Styles();

        float w = groups.Length * slotSize + (groups.Length - 1) * slotGap + 8.0f;
        float h = slotSize + 8.0f;

        float x = margin;
        float y = Screen.height - h - bottomOffset;

        Rect panel = new Rect(x, y, w, h);

        Draw_Rect(panel, color_Panel);

        // 그룹 바는 아직 클릭 조작이 없지만, 위를 눌렀을 때 뒤의 전장이
        // 반응하면 안 됩니다.
        UI_Input_Guard.Register(panel);

        float sx = x + 4.0f;
        float sy = y + 4.0f;

        for (int i = 0; i < groups.Length; i++)
        {
            Draw_Slot(new Rect(sx, sy, slotSize, slotSize), i);
            sx += slotSize + slotGap;
        }
    }

    // 공개 메서드
    /// <summary>
    /// 현재 선택을 지정한 그룹에 저장합니다.
    ///
    /// 선택이 비어 있으면 그룹을 비웁니다. 이것은 실수가 아니라 의도입니다.
    /// '빈 곳을 클릭한 뒤 저장'이 그룹을 지우는 유일한 방법이 됩니다.
    /// </summary>
    /// <param name="index">저장할 그룹 번호(0부터)입니다.</param>
    public void Assign(int index)
    {
        if (groups == null || index < 0 || index >= groups.Length) return;

        List<Army> selected = controller.armies_Selected;

        groups[index].Clear();

        if (selected == null) return;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null) continue;
            groups[index].Add(selected[i]);
        }
    }

    /// <summary>
    /// 지정한 그룹을 선택 상태로 되돌립니다.
    ///
    /// 기존 선택은 해제합니다. RTS의 관례이며, 그러지 않으면 그룹을
    /// 부를수록 선택이 누적되어 의도한 부대만 지휘할 수 없게 됩니다.
    /// </summary>
    /// <param name="index">호출할 그룹 번호(0부터)입니다.</param>
    public void Recall(int index)
    {
        if (groups == null || index < 0 || index >= groups.Length) return;

        List<Army> group = groups[index];

        // 죽은 부대를 먼저 걷어냅니다.
        // 전멸한 부대가 남아 있으면 아래에서 null 참조가 됩니다.
        Prune(group);

        // 빈 그룹을 부르면 아무 일도 하지 않습니다.
        // 선택만 풀어 버리면 '실수로 눌렀는데 지휘를 잃는' 일이 생깁니다.
        if (group.Count == 0) return;

        List<Army> selected = controller.armies_Selected;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null) continue;
            selected[i].UnSelected();
        }

        selected.Clear();

        for (int i = 0; i < group.Count; i++)
        {
            group[i].Selected();
            selected.Add(group[i]);
        }
    }

    // 비공개 메서드
    /// <summary>GUI 스타일을 처음 한 번만 만듭니다.</summary>
    private void Ensure_Styles()
    {
        if (style_Number != null) return;

        style_Number = new GUIStyle(GUI.skin.label);
        style_Number.fontSize = 10;
        style_Number.alignment = TextAnchor.UpperLeft;

        style_Count = new GUIStyle(GUI.skin.label);
        style_Count.fontSize = 9;
        style_Count.alignment = TextAnchor.LowerRight;
    }

    /// <summary>
    /// 그룹 칸 하나를 그립니다.
    /// </summary>
    /// <param name="rect">칸의 영역입니다.</param>
    /// <param name="index">그릴 그룹 번호(0부터)입니다.</param>
    private void Draw_Slot(Rect rect, int index)
    {
        // 그리는 김에 죽은 부대를 걷어냅니다.
        // 표시된 부대 수가 실제와 달라 보이면 그룹을 믿을 수 없게 됩니다.
        Prune(groups[index]);

        int count = groups[index].Count;
        bool bfilled = count > 0;

        Draw_Rect(rect, bfilled ? color_Filled : color_Empty);
        Draw_Border(rect, color_Edge, 1.0f);

        Color previous = GUI.color;

        GUI.color = bfilled ? color_Text : color_Text_Dim;
        GUI.Label(new Rect(rect.x + 3.0f, rect.y, rect.width, rect.height),
                  Get_Group_Label(index), style_Number);

        if (bfilled)
        {
            GUI.color = color_Text;
            GUI.Label(new Rect(rect.x, rect.y, rect.width - 3.0f, rect.height - 1.0f),
                      count.ToString(), style_Count);
        }

        GUI.color = previous;
    }

    /// <summary>
    /// 칸에 적을 짧은 이름을 만듭니다.
    ///
    /// KeyCode.F1은 "F1"로 그대로 읽히므로 키 이름을 그대로 씁니다.
    /// 단축키를 인스펙터에서 바꿔도 표시가 따라옵니다.
    /// </summary>
    /// <param name="index">이름을 구할 그룹 번호(0부터)입니다.</param>
    /// <returns>표시용 문자열입니다.</returns>
    private string Get_Group_Label(int index)
    {
        if (index < 0 || index >= keyCode_Groups.Length) return string.Empty;

        string name = keyCode_Groups[index].ToString();

        // Alpha1 같은 이름은 화면에서 읽히지 않으므로 숫자만 남깁니다.
        if (name.StartsWith("Alpha")) return name.Substring(5);
        if (name.StartsWith("Keypad")) return name.Substring(6);

        return name;
    }

    /// <summary>파괴되었거나 비어 버린 부대를 목록에서 제거합니다.</summary>
    /// <param name="group">정리할 그룹 목록입니다.</param>
    private static void Prune(List<Army> group)
    {
        // 뒤에서부터 지워야 인덱스가 밀리지 않습니다.
        for (int i = group.Count - 1; i >= 0; i--)
        {
            if (group[i] == null) group.RemoveAt(i);
        }
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

    /// <summary>사각형의 네 변에 테두리를 그립니다.</summary>
    /// <param name="rect">테두리를 두를 영역입니다.</param>
    /// <param name="color">테두리 색입니다.</param>
    /// <param name="thickness">테두리 두께(픽셀)입니다.</param>
    private void Draw_Border(Rect rect, Color color, float thickness)
    {
        Draw_Rect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Draw_Rect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
