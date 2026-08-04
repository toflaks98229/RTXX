using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 우하단의 태세 명령 패널입니다. DCSS 아이콘을 버튼으로 씁니다.
///
/// 왜 필요한가:
/// 태세는 이 게임에서 '기동을 포기하고 버틴다'는 거래입니다.
/// 창벽은 돌격을 되받아치고 방패벽은 화살을 막습니다.
/// 그런데 지금까지 그것을 바꾸는 방법은 숫자키 1~5뿐이었고,
/// 화면 어디에도 그런 명령이 있다는 표시가 없었습니다.
///
/// 즉 이 게임의 전술 절반이 '아는 사람만 쓰는 기능'이었습니다.
/// 버튼으로 내놓으면 처음 보는 사람도 태세가 있다는 것을 알고,
/// 단축키 숫자를 함께 적어 두면 자연히 단축키로 옮겨 갑니다.
///
/// 입력은 Controller의 기존 경로(Set_Stance)를 그대로 씁니다.
/// 버튼이 특별한 일을 하지 않아야 단축키와 동작이 갈리지 않습니다.
/// </summary>
public class UI_Command_Panel : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>선택된 부대를 읽어 올 컨트롤러입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    /// <summary>버튼 한 변의 크기(픽셀)입니다.</summary>
    [Header("표시")]
    [Tooltip("버튼 한 변의 크기입니다.")]
    public float buttonSize = 40.0f;

    /// <summary>화면 가장자리와의 여백(픽셀)입니다.</summary>
    [Tooltip("화면 가장자리와의 여백입니다.")]
    public float margin = 10.0f;

    /// <summary>
    /// 카드 바를 피해 위로 띄우는 높이(픽셀)입니다.
    ///
    /// 기본값 106은 카드 바 위에 얹히도록 계산한 값입니다.
    ///   UI_Command_Bar: bottomMargin(10) + cardHeight(74) + padding(8*2) = 100
    ///   여기에 여백 6을 더합니다.
    /// 카드 크기를 바꾸면 이 값도 함께 옮겨야 겹치지 않습니다.
    /// </summary>
    [Tooltip("카드 바를 피해 위로 띄우는 높이입니다. 카드 크기를 바꾸면 함께 조정하십시오.")]
    public float bottomOffset = 106.0f;

    // 비공개 멤버 변수
    /// <summary>패널 바탕색입니다.</summary>
    private static readonly Color color_Panel = new Color(0.07f, 0.06f, 0.05f, 0.88f);

    /// <summary>버튼 바탕색입니다.</summary>
    private static readonly Color color_Button = new Color(0.14f, 0.12f, 0.10f, 0.95f);

    /// <summary>현재 적용 중인 태세 버튼의 바탕색입니다.</summary>
    private static readonly Color color_Active = new Color(0.30f, 0.26f, 0.16f, 0.98f);

    /// <summary>기본 테두리색입니다.</summary>
    private static readonly Color color_Edge = new Color(0.55f, 0.45f, 0.25f, 0.85f);

    /// <summary>현재 태세 버튼의 테두리색입니다.</summary>
    private static readonly Color color_Edge_Active = new Color(0.95f, 0.82f, 0.45f, 1.0f);

    /// <summary>아이콘과 글자색입니다.</summary>
    private static readonly Color color_Text = new Color(0.90f, 0.86f, 0.76f, 1.0f);

    /// <summary>보조 글자색입니다. 단축키 숫자에 씁니다.</summary>
    private static readonly Color color_Dim = new Color(0.62f, 0.58f, 0.50f, 1.0f);

    /// <summary>사각형을 채우는 데 쓰는 1x1 텍스처입니다.</summary>
    private Texture2D pixel;

    /// <summary>단축키 숫자 스타일입니다.</summary>
    private GUIStyle style_Key;

    /// <summary>툴팁 스타일입니다.</summary>
    private GUIStyle style_Tip;

    /// <summary>
    /// 패널에 늘어놓을 태세들입니다.
    ///
    /// 순서가 곧 단축키 숫자입니다. Controller의 keyCode_Stance_* 순서와
    /// 반드시 같아야 버튼과 단축키가 같은 것을 가리킵니다.
    /// </summary>
    private static readonly E_Army_Stance[] stances =
    {
        E_Army_Stance.Line,
        E_Army_Stance.Loose,
        E_Army_Stance.ShieldWall,
        E_Army_Stance.SpearWall,
        E_Army_Stance.Skirmish,
    };

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

    /// <summary>선택된 부대가 있을 때만 명령 패널을 그립니다.</summary>
    private void OnGUI()
    {
        if (controller == null) return;

        List<Army> selected = controller.armies_Selected;

        // 선택이 없으면 그리지 않습니다.
        // 쓸 수 없는 버튼을 띄워 두면 화면만 좁아집니다.
        if (selected == null || selected.Count == 0) return;

        Ensure_Styles();

        const float gap = 4.0f;

        float w = stances.Length * buttonSize + (stances.Length - 1) * gap + 12.0f;
        float h = buttonSize + 12.0f;

        float x = Screen.width - w - margin;
        float y = Screen.height - h - bottomOffset;

        Rect panel = new Rect(x, y, w, h);

        Draw_Rect(panel, color_Panel);
        Draw_Border(panel, color_Edge, 1.0f);

        // 버튼 위의 클릭이 전장으로 새어 나가면, 태세를 바꾸는 순간
        // 선택이 풀려 명령이 아무 부대에도 적용되지 않습니다.
        UI_Input_Guard.Register(panel);

        // 선택된 부대들의 공통 태세를 찾습니다.
        // 서로 다르면 어느 것도 '현재'로 표시하지 않습니다.
        E_Army_Stance current = Get_Common_Stance(selected);
        bool bcommon = Has_Common_Stance(selected);

        string tooltip = null;

        float bx = x + 6.0f;
        float by = y + 6.0f;

        for (int i = 0; i < stances.Length; i++)
        {
            Rect button = new Rect(bx, by, buttonSize, buttonSize);

            bool bactive = bcommon && stances[i] == current;

            if (Draw_Button(button, stances[i], i + 1, bactive))
            {
                Apply_Stance(selected, stances[i]);
            }

            if (button.Contains(Event.current.mousePosition))
            {
                tooltip = Get_Stance_Tooltip(stances[i]);
            }

            bx += buttonSize + gap;
        }

        // 툴팁은 마지막에 그려 버튼 위에 오게 합니다.
        if (tooltip != null) Draw_Tooltip(panel, tooltip);
    }

    // 비공개 메서드
    /// <summary>GUI 스타일을 처음 한 번만 만듭니다.</summary>
    private void Ensure_Styles()
    {
        if (style_Key != null) return;

        style_Key = new GUIStyle(GUI.skin.label);
        style_Key.fontSize = 9;
        style_Key.alignment = TextAnchor.LowerRight;

        style_Tip = new GUIStyle(GUI.skin.label);
        style_Tip.fontSize = 11;
        style_Tip.alignment = TextAnchor.UpperLeft;
        style_Tip.wordWrap = true;
    }

    /// <summary>
    /// 태세 버튼 하나를 그리고 눌렸는지 돌려줍니다.
    /// </summary>
    /// <param name="rect">버튼 영역입니다.</param>
    /// <param name="stance">이 버튼이 적용할 태세입니다.</param>
    /// <param name="key">함께 표시할 단축키 숫자입니다.</param>
    /// <param name="bactive">현재 적용 중인 태세인지 여부입니다.</param>
    /// <returns>이번 프레임에 눌렸으면 true입니다.</returns>
    private bool Draw_Button(Rect rect, E_Army_Stance stance, int key, bool bactive)
    {
        bool bhover = rect.Contains(Event.current.mousePosition);

        Draw_Rect(rect, bactive ? color_Active : color_Button);
        Draw_Border(rect, bactive || bhover ? color_Edge_Active : color_Edge, 1.0f);

        // 아이콘을 가운데에 그립니다.
        //
        // Line(기본 태세)은 UI_Icons가 빈 이름을 돌려주므로 아이콘이
        // 없습니다. 그 경우 글자로 대신합니다.
        const float pad = 8.0f;

        Rect iconRect = new Rect(rect.x + pad, rect.y + pad,
                                 rect.width - pad * 2.0f, rect.height - pad * 2.0f);

        // 버튼 전용 대응표를 씁니다.
        // 카드용(Get_Stance_Icon)은 기본 태세를 비워 두므로, 그것을 쓰면
        // 전열 유지 버튼만 글자('전')로 나옵니다.
        string icon = UI_Icons.Get_Stance_Button_Icon(stance);

        if (!UI_Icons.Draw(iconRect, icon, color_Text))
        {
            // 아이콘이 없으면 태세 이름의 첫 글자를 씁니다.
            GUIStyle center = new GUIStyle(style_Key);
            center.alignment = TextAnchor.MiddleCenter;
            center.fontSize = 14;

            Color previous = GUI.color;
            GUI.color = color_Text;
            GUI.Label(rect, Get_Stance_Name(stance).Substring(0, 1), center);
            GUI.color = previous;
        }

        // 단축키 숫자를 오른쪽 아래에 작게 답니다.
        Color prev = GUI.color;
        GUI.color = color_Dim;
        GUI.Label(new Rect(rect.x, rect.y, rect.width - 3.0f, rect.height - 1.0f),
                  key.ToString(), style_Key);
        GUI.color = prev;

        return bhover
            && Event.current.type == EventType.MouseDown
            && Event.current.button == 0
            && Consume();
    }

    /// <summary>클릭 입력을 소비합니다. 전장 선택으로 새어 나가지 않게 합니다.</summary>
    /// <returns>언제나 true입니다. 조건식 안에서 쓰기 위한 형태입니다.</returns>
    private static bool Consume()
    {
        Event.current.Use();
        return true;
    }

    /// <summary>선택된 부대 전체에 태세를 적용합니다.</summary>
    /// <param name="selected">적용할 부대 목록입니다.</param>
    /// <param name="stance">적용할 태세입니다.</param>
    private static void Apply_Stance(List<Army> selected, E_Army_Stance stance)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null) continue;

            // 단축키와 같은 입구를 씁니다.
            // 여기서 army_Data를 직접 건드리면 두 경로가 갈립니다.
            selected[i].Set_Stance(stance);
        }
    }

    /// <summary>선택된 부대들의 공통 태세를 반환합니다.</summary>
    /// <param name="selected">확인할 부대 목록입니다.</param>
    /// <returns>첫 부대의 태세입니다.</returns>
    private static E_Army_Stance Get_Common_Stance(List<Army> selected)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] != null) return selected[i].GetStance();
        }

        return E_Army_Stance.Line;
    }

    /// <summary>선택된 부대들이 모두 같은 태세인지 확인합니다.</summary>
    /// <param name="selected">확인할 부대 목록입니다.</param>
    /// <returns>전부 같으면 true입니다.</returns>
    private static bool Has_Common_Stance(List<Army> selected)
    {
        bool bfirst = true;
        E_Army_Stance common = E_Army_Stance.Line;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null) continue;

            if (bfirst)
            {
                common = selected[i].GetStance();
                bfirst = false;
                continue;
            }

            if (selected[i].GetStance() != common) return false;
        }

        return !bfirst;
    }

    /// <summary>패널 위에 태세 설명을 띄웁니다.</summary>
    /// <param name="panel">기준이 되는 패널 영역입니다.</param>
    /// <param name="text">표시할 설명입니다.</param>
    private void Draw_Tooltip(Rect panel, string text)
    {
        const float w = 230.0f;
        const float h = 56.0f;

        Rect rect = new Rect(panel.xMax - w, panel.y - h - 4.0f, w, h);

        Draw_Rect(rect, color_Panel);
        Draw_Border(rect, color_Edge, 1.0f);

        Color previous = GUI.color;
        GUI.color = color_Text;

        GUI.Label(new Rect(rect.x + 6.0f, rect.y + 4.0f, rect.width - 12.0f, rect.height - 8.0f),
                  text, style_Tip);

        GUI.color = previous;
    }

    /// <summary>
    /// 태세의 설명을 반환합니다.
    ///
    /// 수치가 아니라 '무엇과 무엇을 맞바꾸는가'를 적습니다.
    /// 플레이어가 알아야 할 것은 정확한 배율이 아니라 거래의 성격입니다.
    /// </summary>
    /// <param name="stance">설명을 구할 태세입니다.</param>
    /// <returns>표시용 설명입니다.</returns>
    private static string Get_Stance_Tooltip(E_Army_Stance stance)
    {
        switch (stance)
        {
            case E_Army_Stance.Line:
                return "전열 유지\n명령받은 폭과 각도를 지키며 정비합니다.";

            case E_Army_Stance.Loose:
                return "느슨한 대열\n전열을 지키지 않고 앞쪽 빈자리를 메웁니다. 추격에 유리합니다.";

            case E_Army_Stance.ShieldWall:
                return "방패벽\n원거리 방어와 정면 돌격 저항이 크게 오릅니다. 걷기 속도로 제한됩니다.";

            case E_Army_Stance.SpearWall:
                return "창벽\n정지 상태에서 정면 돌격을 되받아칩니다. 원거리에는 취약합니다.";

            case E_Army_Stance.Skirmish:
                return "산개\n적이 다가오면 물러나며 거리를 유지합니다. 궁병용입니다.";

            default:
                return string.Empty;
        }
    }

    /// <summary>태세의 한글 이름을 반환합니다.</summary>
    /// <param name="stance">이름을 구할 태세입니다.</param>
    /// <returns>표시용 태세 이름입니다.</returns>
    private static string Get_Stance_Name(E_Army_Stance stance)
    {
        switch (stance)
        {
            case E_Army_Stance.Loose: return "공격";
            case E_Army_Stance.ShieldWall: return "방패";
            case E_Army_Stance.SpearWall: return "창벽";
            case E_Army_Stance.Skirmish: return "산개";
            default: return "전열";
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
