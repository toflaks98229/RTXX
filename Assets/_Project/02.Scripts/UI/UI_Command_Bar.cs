using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 화면 하단의 부대 카드 바입니다. 토탈워 HUD의 핵심 요소입니다.
///
/// 왜 이것이 필요한가:
/// 토탈워에서 플레이어는 전장을 내려다보는 동시에, 하단 바에서 '내 군대 전체'를
/// 한눈에 봅니다. 어느 부대가 지쳤는지, 어디가 무너지려는지, 예비대가 남았는지를
/// 카메라를 돌리지 않고도 읽을 수 있어야 지휘가 성립합니다.
///
/// 지금까지 이 프로젝트는 사기/피로/태세/상성을 전부 계산하면서도
/// 그 정보가 화면에 거의 나오지 않았습니다. 부대가 왜 무너지는지 플레이어는
/// 알 수 없었습니다. 이 파일은 '이미 계산된 것을 보여주는' 역할만 합니다.
///
/// 구현 방식:
/// IMGUI(OnGUI)입니다. Canvas 프리팹 배선이 필요 없어 씬에 컴포넌트 하나만
/// 올리면 동작합니다. 부대는 많아야 수십 개라 비용도 문제되지 않습니다.
/// (유닛 단위였다면 절대 이렇게 하면 안 됩니다)
/// </summary>
public class UI_Command_Bar : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    [Tooltip("전투 단계를 읽어 배치/종료 상태를 표시합니다. 비워도 됩니다.")]
    public Battle_Manager battle_Manager;

    [Header("표시")]
    [Tooltip("카드 하나의 너비입니다.")]
    public float cardWidth = 88.0f;

    [Tooltip("카드 하나의 높이입니다.")]
    public float cardHeight = 62.0f;

    [Tooltip("바 전체의 아래 여백입니다.")]
    public float bottomMargin = 10.0f;

    [Tooltip("적 부대 카드도 표시합니다. 보통은 끕니다.")]
    public bool bshowEnemyCards = false;

    // ---------------------------------------------------------------------
    // 팔레트
    //
    // 토탈워 HUD는 어두운 양피지/금속 바탕에 낮은 채도의 금색 테두리입니다.
    // 그 위에서 사기 게이지 같은 '상태 색'만 선명하게 튀어야 정보가 읽힙니다.
    // 바탕이 화려하면 정작 봐야 할 것이 묻힙니다.
    // ---------------------------------------------------------------------
    private static readonly Color color_Panel = new Color(0.07f, 0.06f, 0.05f, 0.88f);
    private static readonly Color color_Card = new Color(0.14f, 0.12f, 0.10f, 0.95f);
    private static readonly Color color_Card_Selected = new Color(0.30f, 0.26f, 0.16f, 0.98f);
    private static readonly Color color_Edge = new Color(0.55f, 0.45f, 0.25f, 0.85f);
    private static readonly Color color_Edge_Selected = new Color(0.95f, 0.82f, 0.45f, 1.00f);
    private static readonly Color color_Text = new Color(0.90f, 0.86f, 0.76f, 1.00f);
    private static readonly Color color_Text_Dim = new Color(0.62f, 0.58f, 0.50f, 1.00f);
    private static readonly Color color_General = new Color(0.98f, 0.84f, 0.40f, 1.00f);

    private GUIStyle style_Name;
    private GUIStyle style_Small;
    private GUIStyle style_Center;
    private GUIStyle style_Banner;
    private GUIStyle style_Hint;

    private Texture2D pixel;
    private readonly StringBuilder builder = new StringBuilder(128);

    /// <summary>이번 프레임에 그린 카드들입니다. 클릭 판정에 씁니다.</summary>
    private readonly List<Army> cardArmies = new List<Army>();

    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }

    private void Start()
    {
        if (controller == null) controller = FindAnyObjectByType<Controller>();
        if (battle_Manager == null) battle_Manager = FindAnyObjectByType<Battle_Manager>();
    }

    private void Ensure_Styles()
    {
        if (style_Name != null) return;

        style_Name = new GUIStyle(GUI.skin.label);
        style_Name.fontSize = 11;
        style_Name.fontStyle = FontStyle.Bold;
        style_Name.alignment = TextAnchor.MiddleLeft;
        style_Name.padding = new RectOffset(0, 0, 0, 0);

        style_Small = new GUIStyle(GUI.skin.label);
        style_Small.fontSize = 9;
        style_Small.alignment = TextAnchor.MiddleLeft;
        style_Small.padding = new RectOffset(0, 0, 0, 0);

        style_Center = new GUIStyle(GUI.skin.label);
        style_Center.fontSize = 9;
        style_Center.alignment = TextAnchor.MiddleCenter;
        style_Center.padding = new RectOffset(0, 0, 0, 0);

        style_Banner = new GUIStyle(GUI.skin.label);
        style_Banner.fontSize = 26;
        style_Banner.fontStyle = FontStyle.Bold;
        style_Banner.alignment = TextAnchor.MiddleCenter;

        style_Hint = new GUIStyle(GUI.skin.label);
        style_Hint.fontSize = 11;
        style_Hint.alignment = TextAnchor.MiddleCenter;
    }

    private void OnGUI()
    {
        Ensure_Styles();

        List<Army> armies = controller != null ? controller.armies : Army.allArmies;
        if (armies == null) return;

        Draw_Top_Strip(armies);
        Draw_Card_Bar(armies);
        Draw_Phase_Banner();
    }

    // =====================================================================
    // 상단 전력 표시줄
    //
    // 토탈워는 화면 위쪽에 양측 전력비를 항상 띄웁니다.
    // "지금 이기고 있는가"는 개별 부대를 다 확인하지 않고도 알아야 하는 정보입니다.
    // =====================================================================
    private void Draw_Top_Strip(List<Army> armies)
    {
        int playerAlive = 0, playerMax = 0;
        int enemyAlive = 0, enemyMax = 0;
        int playerBroken = 0, enemyBroken = 0;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            ref Army_Data d = ref army.army_Data;

            if (d.bplayer)
            {
                playerAlive += d.unit_Num;
                playerMax += d.unit_Num_Max;
                if (d.IsBroken()) playerBroken++;
            }
            else
            {
                enemyAlive += d.unit_Num;
                enemyMax += d.unit_Num_Max;
                if (d.IsBroken()) enemyBroken++;
            }
        }

        const float height = 24.0f;
        float width = Screen.width;

        Draw_Rect(new Rect(0.0f, 0.0f, width, height), color_Panel);
        Draw_Rect(new Rect(0.0f, height - 1.0f, width, 1.0f), color_Edge);

        // 전력비 게이지: 왼쪽이 아군, 오른쪽이 적군입니다.
        int total = playerAlive + enemyAlive;
        float playerRate = total > 0 ? (float)playerAlive / total : 0.5f;

        const float barY = 4.0f;
        const float barH = 4.0f;
        float barW = Mathf.Min(420.0f, width * 0.4f);
        float barX = (width - barW) * 0.5f;

        Draw_Rect(new Rect(barX, barY, barW, barH), new Color(0.5f, 0.22f, 0.18f, 0.95f));
        Draw_Rect(new Rect(barX, barY, barW * playerRate, barH), new Color(0.35f, 0.55f, 0.80f, 0.95f));

        // 좌: 아군 / 우: 적군 수치
        builder.Clear();
        builder.Append("아군 ").Append(playerAlive).Append(" / ").Append(playerMax);
        if (playerBroken > 0) builder.Append("   붕괴 ").Append(playerBroken);
        Draw_Label(new Rect(10.0f, barY + barH, 280.0f, 14.0f), builder.ToString(),
                   style_Small, new Color(0.72f, 0.84f, 1.0f, 1.0f));

        builder.Clear();
        builder.Append("적군 ").Append(enemyAlive).Append(" / ").Append(enemyMax);
        if (enemyBroken > 0) builder.Append("   붕괴 ").Append(enemyBroken);

        GUIStyle right = new GUIStyle(style_Small);
        right.alignment = TextAnchor.MiddleRight;
        Draw_Label(new Rect(width - 290.0f, barY + barH, 280.0f, 14.0f), builder.ToString(),
                   right, new Color(1.0f, 0.78f, 0.72f, 1.0f));
    }

    // =====================================================================
    // 하단 부대 카드 바
    // =====================================================================
    private void Draw_Card_Bar(List<Army> armies)
    {
        cardArmies.Clear();
        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;                 // 전멸한 부대는 카드에서 뺍니다
            if (!bshowEnemyCards && !army.army_Data.bplayer) continue;

            cardArmies.Add(army);
        }

        if (cardArmies.Count == 0) return;

        const float gap = 4.0f;
        const float padding = 8.0f;

        float totalWidth = cardArmies.Count * cardWidth + (cardArmies.Count - 1) * gap;
        float panelWidth = totalWidth + padding * 2.0f;
        float panelHeight = cardHeight + padding * 2.0f;

        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Screen.height - panelHeight - bottomMargin;

        // 카드가 화면을 넘으면 좌측 정렬로 바꿔 잘리지 않게 합니다.
        if (panelX < 0.0f) panelX = 0.0f;

        Rect panel = new Rect(panelX, panelY, Mathf.Min(panelWidth, Screen.width), panelHeight);
        Draw_Panel(panel);

        float x = panelX + padding;
        float y = panelY + padding;

        for (int i = 0; i < cardArmies.Count; i++)
        {
            Rect card = new Rect(x, y, cardWidth, cardHeight);

            // 화면 밖으로 밀려나면 그리지 않습니다.
            if (card.xMax <= Screen.width) Draw_Card(card, cardArmies[i], i);

            x += cardWidth + gap;
        }

        Draw_Selection_Hint(panelX, panelY);
    }

    /// <summary>부대 카드 하나입니다. 클릭하면 그 부대가 선택됩니다.</summary>
    private void Draw_Card(Rect rect, Army army, int index)
    {
        ref Army_Data data = ref army.army_Data;

        bool bselected = army.IsSelected();
        bool bbroken = data.IsBroken();

        // 바탕
        Draw_Rect(rect, bselected ? color_Card_Selected : color_Card);

        // 무너진 부대는 붉게 덮어 한눈에 구분되게 합니다.
        if (bbroken)
        {
            Draw_Rect(rect, new Color(0.45f, 0.10f, 0.08f, 0.45f));
        }

        Draw_Border(rect, bselected ? color_Edge_Selected : color_Edge, bselected ? 2.0f : 1.0f);

        float pad = 4.0f;
        float innerW = rect.width - pad * 2.0f;
        float cx = rect.x + pad;
        float cy = rect.y + pad;

        // 1행: 병종 (장군이면 ★)
        builder.Clear();
        if (data.bgeneral) builder.Append("★ ");
        builder.Append(Get_Class_Name(data.GetE_Unit_Class()));

        Draw_Label(new Rect(cx, cy, innerW, 13.0f), builder.ToString(), style_Name,
                   data.bgeneral ? color_General : color_Text);
        cy += 13.0f;

        // 2행: 인원 + 피로
        builder.Clear();
        builder.Append(data.unit_Num).Append('/').Append(data.unit_Num_Max);

        Draw_Label(new Rect(cx, cy, innerW * 0.5f, 11.0f), builder.ToString(),
                   style_Small, color_Text);

        Draw_Label(new Rect(cx + innerW * 0.5f, cy, innerW * 0.5f, 11.0f),
                   Get_Fatigue_Name(data.e_Army_Fatigue), style_Small,
                   Get_Fatigue_Color(data.e_Army_Fatigue));
        cy += 12.0f;

        // 3행: 사기 게이지 (가장 중요한 정보이므로 가장 넓게)
        Rect morale = new Rect(cx, cy, innerW, 6.0f);
        Draw_Morale_Bar(morale, army);
        cy += 8.0f;

        // 4행: 인원 게이지 (얼마나 갈려 나갔는가)
        Rect strength = new Rect(cx, cy, innerW, 3.0f);
        float sRate = data.unit_Num_Max > 0 ? (float)data.unit_Num / data.unit_Num_Max : 0.0f;
        Draw_Rect(strength, new Color(0.0f, 0.0f, 0.0f, 0.6f));
        Draw_Rect(new Rect(strength.x, strength.y, strength.width * sRate, strength.height),
                  new Color(0.65f, 0.62f, 0.55f, 0.9f));
        cy += 5.0f;

        // 5행: 상태 / 태세
        string state = Get_State_Text(army);
        if (!string.IsNullOrEmpty(state))
        {
            Draw_Label(new Rect(cx, cy, innerW, 11.0f), state, style_Center,
                       Get_Morale_Color(data.e_Army_Morale));
        }
        else if (data.e_Army_Stance != E_Army_Stance.Line)
        {
            Draw_Label(new Rect(cx, cy, innerW, 11.0f),
                       Get_Stance_Name(data.e_Army_Stance), style_Center, color_Text_Dim);
        }

        Handle_Card_Click(rect, army);
    }

    /// <summary>
    /// 카드 클릭 처리입니다.
    ///
    /// 토탈워와 동일하게: 그냥 클릭하면 그 부대만 선택,
    /// Ctrl을 누른 채 클릭하면 선택에 추가합니다.
    /// </summary>
    private void Handle_Card_Click(Rect rect, Army army)
    {
        Event e = Event.current;
        if (e == null) return;
        if (e.type != EventType.MouseDown) return;
        if (e.button != 0) return;
        if (!rect.Contains(e.mousePosition)) return;

        if (controller == null) return;

        bool badditive = e.control || e.shift;

        if (!badditive)
        {
            for (int i = 0; i < controller.armies_Selected.Count; i++)
            {
                if (controller.armies_Selected[i] != null)
                    controller.armies_Selected[i].UnSelected();
            }
            controller.armies_Selected.Clear();
        }

        if (army.IsSelected())
        {
            // 이미 선택된 카드를 Ctrl+클릭하면 선택 해제입니다.
            if (badditive)
            {
                army.UnSelected();
                controller.armies_Selected.Remove(army);
            }
        }
        else
        {
            army.Selected();
            if (!controller.armies_Selected.Contains(army))
                controller.armies_Selected.Add(army);
        }

        // 이 클릭이 뒤의 전장으로 전달되면 선택이 곧바로 풀립니다.
        e.Use();
    }

    /// <summary>선택 상태와 조작 힌트를 카드 바 위에 한 줄로 띄웁니다.</summary>
    private void Draw_Selection_Hint(float panelX, float panelY)
    {
        if (controller == null) return;

        int count = controller.armies_Selected.Count;
        if (count == 0) return;

        builder.Clear();
        builder.Append(count).Append("개 부대 선택   ");
        builder.Append("1 전열  2 산병  3 방패벽  4 창벽  5 산개   우클릭 드래그로 진형");

        float w = 640.0f;
        float x = (Screen.width - w) * 0.5f;
        float y = panelY - 20.0f;

        Draw_Rect(new Rect(x, y, w, 18.0f), color_Panel);
        Draw_Label(new Rect(x, y, w, 18.0f), builder.ToString(), style_Hint, color_Text_Dim);
    }

    // =====================================================================
    // 전투 단계 배너
    // =====================================================================
    private void Draw_Phase_Banner()
    {
        if (battle_Manager == null) return;

        string title = null;
        string sub = null;

        switch (battle_Manager.phase)
        {
            case E_Battle_Phase.Deployment:
                title = "배치 단계";
                sub = $"부대를 배치한 뒤 {battle_Manager.keyCode_Start_Battle} 키로 전투를 시작합니다";
                break;

            case E_Battle_Phase.Finished:
                title = Get_Result_Text(battle_Manager.result);
                sub = null;
                break;
        }

        if (title == null) return;

        float w = Screen.width;
        float y = battle_Manager.phase == E_Battle_Phase.Deployment
            ? 44.0f
            : Screen.height * 0.34f;

        Draw_Rect(new Rect(0.0f, y - 8.0f, w, sub != null ? 62.0f : 46.0f), color_Panel);
        Draw_Label(new Rect(0.0f, y, w, 34.0f), title, style_Banner, color_General);

        if (sub != null)
        {
            Draw_Label(new Rect(0.0f, y + 34.0f, w, 16.0f), sub, style_Hint, color_Text_Dim);
        }
    }

    // =====================================================================
    // 그리기 도우미
    // =====================================================================
    private void Draw_Morale_Bar(Rect rect, Army army)
    {
        ref Army_Data data = ref army.army_Data;

        float max = Constant.morale_Max;
        float rate = max > 0.0f ? data.morale / max : 0.0f;
        if (rate < 0.0f) rate = 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        Draw_Rect(rect, new Color(0.0f, 0.0f, 0.0f, 0.65f));

        Color color = Get_Morale_Color(data.e_Army_Morale);

        // 동요 중에는 점멸시켜 '곧 무너진다'를 알립니다.
        if (data.e_Army_Morale == E_Army_Morale.Wavering)
        {
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6.0f));
            color.a *= pulse;
        }

        Draw_Rect(new Rect(rect.x, rect.y, rect.width * rate, rect.height), color);

        // 패주 임계선 표시: 여기에 닿으면 무너집니다.
        float waver = max > 0.0f ? Constant.morale_Waver_Threshold / max : 0.0f;
        if (waver > 0.0f && waver < 1.0f)
        {
            Draw_Rect(new Rect(rect.x + rect.width * waver, rect.y, 1.0f, rect.height),
                      new Color(1.0f, 1.0f, 1.0f, 0.35f));
        }
    }

    private void Draw_Panel(Rect rect)
    {
        Draw_Rect(rect, color_Panel);
        Draw_Border(rect, color_Edge, 1.0f);
    }

    private void Draw_Border(Rect rect, Color color, float thickness)
    {
        Draw_Rect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Draw_Rect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private void Draw_Rect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixel);
        GUI.color = previous;
    }

    /// <summary>그림자를 깔고 글자를 그립니다. 어떤 배경에서도 읽히게 하기 위함입니다.</summary>
    private void Draw_Label(Rect rect, string text, GUIStyle style, Color color)
    {
        Color previous = style.normal.textColor;

        style.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 0.9f);
        GUI.Label(new Rect(rect.x + 1.0f, rect.y + 1.0f, rect.width, rect.height), text, style);

        style.normal.textColor = color;
        GUI.Label(rect, text, style);

        style.normal.textColor = previous;
    }

    // =====================================================================
    // 문자열 / 색 변환
    // =====================================================================
    private static string Get_State_Text(Army army)
    {
        ref Army_Data d = ref army.army_Data;

        if (d.e_Army_Morale == E_Army_Morale.Shattered) return "와해";
        if (d.e_Army_Morale == E_Army_Morale.Broken) return "패주";
        if (d.e_Army_Move == E_Army_Move.MoveCharge) return "돌격";
        if (d.e_Army_Morale == E_Army_Morale.Wavering) return "동요";
        if (d.e_Army_Fight != E_Army_Fight.Non) return "교전";

        return null;
    }

    private static Color Get_Morale_Color(E_Army_Morale morale)
    {
        switch (morale)
        {
            case E_Army_Morale.Steady: return new Color(0.40f, 0.82f, 0.38f, 0.95f);
            case E_Army_Morale.Wavering: return new Color(0.95f, 0.78f, 0.22f, 0.95f);
            case E_Army_Morale.Broken: return new Color(0.88f, 0.32f, 0.20f, 0.95f);
            default: return new Color(0.50f, 0.12f, 0.12f, 0.95f);
        }
    }

    private static Color Get_Fatigue_Color(E_Army_Fatigue fatigue)
    {
        switch (fatigue)
        {
            case E_Army_Fatigue.Fresh:
            case E_Army_Fatigue.Active:
                return new Color(0.62f, 0.58f, 0.50f, 1.0f);
            case E_Army_Fatigue.Winded:
            case E_Army_Fatigue.Tired:
                return new Color(0.90f, 0.78f, 0.40f, 1.0f);
            default:
                return new Color(0.92f, 0.48f, 0.32f, 1.0f);
        }
    }

    private static string Get_Class_Name(E_Unit_Class unitClass)
    {
        switch (unitClass)
        {
            case E_Unit_Class.Spear: return "창병";
            case E_Unit_Class.Cavalry: return "기병";
            case E_Unit_Class.Archer: return "궁병";
            case E_Unit_Class.Large: return "대형";
            default: return "보병";
        }
    }

    private static string Get_Stance_Name(E_Army_Stance stance)
    {
        switch (stance)
        {
            case E_Army_Stance.Loose: return "산병";
            case E_Army_Stance.ShieldWall: return "방패벽";
            case E_Army_Stance.SpearWall: return "창벽";
            case E_Army_Stance.Skirmish: return "산개";
            default: return "전열";
        }
    }

    private static string Get_Fatigue_Name(E_Army_Fatigue fatigue)
    {
        switch (fatigue)
        {
            case E_Army_Fatigue.Active: return "활발";
            case E_Army_Fatigue.Winded: return "숨참";
            case E_Army_Fatigue.Tired: return "지침";
            case E_Army_Fatigue.VeryTired: return "매우지침";
            case E_Army_Fatigue.Exhausted: return "탈진";
            default: return "생생";
        }
    }

    private static string Get_Result_Text(E_Battle_Result result)
    {
        switch (result)
        {
            case E_Battle_Result.CrushingVictory: return "압승";
            case E_Battle_Result.ClearVictory: return "승리";
            case E_Battle_Result.CloseVictory: return "신승";
            case E_Battle_Result.CloseDefeat: return "석패";
            case E_Battle_Result.ClearDefeat: return "패배";
            case E_Battle_Result.CrushingDefeat: return "참패";
            default: return "";
        }
    }
}
