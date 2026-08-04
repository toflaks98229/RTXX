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
    /// <summary>부대 목록을 읽어 올 컨트롤러입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    /// <summary>전투 단계와 결과를 읽어 올 배틀 매니저입니다. 비워도 동작합니다.</summary>
    [Tooltip("전투 단계를 읽어 배치/종료 상태를 표시합니다. 비워도 됩니다.")]
    public Battle_Manager battle_Manager;

    /// <summary>부대 카드 하나의 너비(픽셀)입니다.</summary>
    [Header("표시")]
    [Tooltip("카드 하나의 너비입니다.")]
    public float cardWidth = 88.0f;

    [Tooltip("카드 하나의 높이입니다.")]
    // 전과 행(11px)이 추가되어 기존 62에서 늘렸습니다.
    // 이 값이 모자라면 마지막 행(상태/태세)이 카드 밖으로 밀려납니다.
    /// <summary>부대 카드 하나의 높이(픽셀)입니다. 표시 행 수에 맞춰야 합니다.</summary>
    public float cardHeight = 74.0f;

    /// <summary>카드 바와 화면 아래 끝 사이의 여백(픽셀)입니다.</summary>
    [Tooltip("바 전체의 아래 여백입니다.")]
    public float bottomMargin = 10.0f;

    /// <summary>적 부대 카드도 표시할지 여부입니다. 디버그용이며 보통은 끕니다.</summary>
    [Tooltip("적 부대 카드도 표시합니다. 보통은 끕니다.")]
    public bool bshowEnemyCards = false;

    // ---------------------------------------------------------------------
    // 팔레트
    //
    // 토탈워 HUD는 어두운 양피지/금속 바탕에 낮은 채도의 금색 테두리입니다.
    // 그 위에서 사기 게이지 같은 '상태 색'만 선명하게 튀어야 정보가 읽힙니다.
    // 바탕이 화려하면 정작 봐야 할 것이 묻힙니다.
    // ---------------------------------------------------------------------
    /// <summary>패널 바탕색입니다. 어두운 양피지/금속 톤입니다.</summary>
    private static readonly Color color_Panel = new Color(0.07f, 0.06f, 0.05f, 0.88f);
    /// <summary>부대 카드의 기본 바탕색입니다.</summary>
    private static readonly Color color_Card = new Color(0.14f, 0.12f, 0.10f, 0.95f);
    /// <summary>선택된 부대 카드의 바탕색입니다.</summary>
    private static readonly Color color_Card_Selected = new Color(0.30f, 0.26f, 0.16f, 0.98f);
    /// <summary>패널과 카드의 기본 테두리색입니다.</summary>
    private static readonly Color color_Edge = new Color(0.55f, 0.45f, 0.25f, 0.85f);
    /// <summary>선택된 카드의 테두리색입니다. 눈에 띄게 밝습니다.</summary>
    private static readonly Color color_Edge_Selected = new Color(0.95f, 0.82f, 0.45f, 1.00f);
    /// <summary>기본 글자색입니다.</summary>
    private static readonly Color color_Text = new Color(0.90f, 0.86f, 0.76f, 1.00f);
    /// <summary>보조 정보용 흐린 글자색입니다.</summary>
    private static readonly Color color_Text_Dim = new Color(0.62f, 0.58f, 0.50f, 1.00f);
    /// <summary>장군과 강조 수치에 쓰는 금색입니다.</summary>
    private static readonly Color color_General = new Color(0.98f, 0.84f, 0.40f, 1.00f);

    /// <summary>전과(킬) 표시색입니다. 피를 연상시키되 경고색과는 구분되어야 합니다.</summary>
    private static readonly Color color_Kill = new Color(0.86f, 0.62f, 0.52f, 1.00f);

    /// <summary>카드의 병종 이름에 쓰는 스타일입니다.</summary>
    private GUIStyle style_Name;
    /// <summary>카드의 작은 수치(인원, 피로)에 쓰는 스타일입니다.</summary>
    private GUIStyle style_Small;
    /// <summary>가운데 정렬이 필요한 짧은 문구에 쓰는 스타일입니다.</summary>
    private GUIStyle style_Center;
    /// <summary>전투 단계/결과 배너에 쓰는 큰 스타일입니다.</summary>
    private GUIStyle style_Banner;
    /// <summary>조작 힌트와 부제에 쓰는 스타일입니다.</summary>
    private GUIStyle style_Hint;

    /// <summary>
    /// 사각형을 채우는 데 쓰는 1x1 흰색 텍스처입니다.
    ///
    /// GUI.color를 바꿔 가며 이 하나를 늘려 그리면 색마다 텍스처를
    /// 만들 필요가 없습니다.
    /// </summary>
    private Texture2D pixel;
    /// <summary>
    /// 문자열을 조립할 때 재사용하는 버퍼입니다.
    /// OnGUI는 매 프레임 여러 번 불리므로 문자열 결합을 그대로 두면 GC가 늡니다.
    /// </summary>
    private readonly StringBuilder builder = new StringBuilder(128);

    /// <summary>이번 프레임에 그린 카드들입니다. 클릭 판정에 씁니다.</summary>
    private readonly List<Army> cardArmies = new List<Army>();

    /// <summary>사각형 채우기에 쓸 1x1 텍스처를 만듭니다.</summary>
    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>동적으로 만든 텍스처를 반납합니다. 두지 않으면 누수됩니다.</summary>
    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }

    /// <summary>비어 있는 참조를 씬에서 찾아 채웁니다.</summary>
    private void Start()
    {
        if (controller == null) controller = FindAnyObjectByType<Controller>();
        if (battle_Manager == null) battle_Manager = FindAnyObjectByType<Battle_Manager>();
    }

    /// <summary>
    /// GUI 스타일을 처음 한 번만 만듭니다.
    ///
    /// GUI.skin은 OnGUI 안에서만 접근할 수 있어 Awake에서 만들 수 없습니다.
    /// </summary>
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

    /// <summary>
    /// 전장 HUD 또는 전과 보고서를 그립니다.
    ///
    /// 전투가 끝났으면 카드 바 대신 보고서만 그립니다. 둘을 함께 그리면
    /// 카드가 보고서를 가리고, 끝난 전투에서 부대가 선택되어 조작이 어긋납니다.
    /// </summary>
    private void OnGUI()
    {
        Ensure_Styles();

        List<Army> armies = controller != null ? controller.armies : Army.allArmies;
        if (armies == null) return;

        // 전투가 끝났으면 전장 HUD 대신 전과 보고서를 띄웁니다.
        //
        // 둘을 함께 그리면 카드 바가 보고서를 가리고, 이미 끝난 전투에서
        // 부대를 선택하게 되어 조작이 어긋납니다.
        if (battle_Manager != null && battle_Manager.phase == E_Battle_Phase.Finished)
        {
            Draw_After_Action_Report();
            return;
        }

        Draw_Top_Strip(armies);
        Draw_Card_Bar(armies);
        Draw_Phase_Banner();
    }

    // =====================================================================
    // 전과 보고서 (After Action Report)
    //
    // 토탈워가 전투 후 반드시 보여주는 화면입니다.
    // "이겼다"만으로는 부족하고, 어느 부대가 무엇을 했는지 알아야
    // 다음 전투에서 편성과 운용을 바꿀 수 있습니다.
    //
    // 이 화면은 Battle_Manager가 종료 시점에 떠 둔 스냅샷만 읽습니다.
    // 전멸한 부대도 기록에 남아 있으므로, 값비싼 대가를 치른 부대가
    // 목록에서 조용히 사라지지 않습니다.
    // =====================================================================
    /// <summary>전투 종료 후 부대별 전과 보고서를 그립니다.</summary>
    private void Draw_After_Action_Report()
    {
        var entries = battle_Manager.ReportEntries;

        float w = Mathf.Min(700.0f, Screen.width - 40.0f);
        float x = (Screen.width - w) * 0.5f;

        const float rowH = 18.0f;
        // 캠페인 한 줄이 추가되어 96에서 늘렸습니다.
        const float headH = 114.0f;

        int rows = entries != null ? entries.Count : 0;
        float h = Mathf.Min(headH + rows * rowH + 16.0f, Screen.height - 40.0f);
        float y = (Screen.height - h) * 0.5f;

        Rect panel = new Rect(x, y, w, h);
        Draw_Panel(panel);

        // 제목
        Draw_Label(new Rect(x, y + 10.0f, w, 34.0f),
                   Get_Result_Text(battle_Manager.result), style_Banner, color_General);

        // 양측 총 손실
        builder.Clear();
        builder.Append("아군 손실 ").Append(battle_Manager.PlayerLosses)
               .Append(" / ").Append(battle_Manager.PlayerStart)
               .Append("      적군 손실 ").Append(battle_Manager.EnemyLosses)
               .Append(" / ").Append(battle_Manager.EnemyStart);

        Draw_Label(new Rect(x, y + 46.0f, w, 16.0f), builder.ToString(),
                   style_Hint, color_Text);

        // 캠페인이 붙어 있으면 누적 전황도 함께 보여 줍니다.
        //
        // 이 한 줄이 전투에 지속성을 부여합니다. "이겼다"만으로는
        // 이번 승리가 값쌌는지 비쌌는지 알 수 없습니다.
        Draw_Campaign_Line(x, y + 62.0f, w);

        // 표 머리글
        float ty = y + 88.0f;
        Draw_Rect(new Rect(x + 8.0f, ty + rowH - 1.0f, w - 16.0f, 1.0f), color_Edge);
        Draw_Report_Row(x, ty, w, "부대", "전과", "손실", "생존", "교환비", color_Text_Dim);

        if (entries == null) return;

        ty += rowH + 2.0f;

        for (int i = 0; i < entries.Count; i++)
        {
            // 패널 밖으로 넘치면 그리지 않습니다.
            if (ty + rowH > y + h - 6.0f) break;

            Battle_Report_Entry e = entries[i];

            // 진영 구분: 아군은 푸른 기, 적군은 붉은 기를 옅게 깝니다.
            Draw_Rect(new Rect(x + 8.0f, ty, w - 16.0f, rowH),
                      e.bplayer ? new Color(0.20f, 0.28f, 0.42f, 0.35f)
                                : new Color(0.42f, 0.20f, 0.18f, 0.35f));

            builder.Clear();
            if (e.bgeneral) builder.Append("★ ");
            builder.Append(Get_Class_Name(e.unitClass));

            // 전멸한 부대는 이름 옆에 표시합니다. 가장 값비싼 대가이므로 묻히면 안 됩니다.
            if (e.survivors == 0) builder.Append(" (전멸)");
            else if (e.morale == E_Army_Morale.Shattered) builder.Append(" (와해)");
            else if (e.morale == E_Army_Morale.Broken) builder.Append(" (패주)");

            Color rowColor = e.survivors == 0 ? color_Text_Dim : color_Text;

            Draw_Report_Row(
                x, ty, w,
                builder.ToString(),
                e.kills.ToString(),
                e.losses.ToString(),
                $"{e.survivors}/{e.startCount}",
                e.KillRatio.ToString("0.0"),
                rowColor,
                e.KillRatio >= 1.0f ? color_General : color_Text_Dim);

            ty += rowH;
        }
    }

    /// <summary>
    /// 캠페인 누적 전황을 한 줄로 그립니다.
    ///
    /// Campaign_Manager가 씬에 없으면 아무것도 그리지 않습니다.
    /// 캠페인은 선택 기능이며, 없어도 전투는 그대로 성립합니다.
    /// </summary>
    private void Draw_Campaign_Line(float x, float y, float w)
    {
        if (campaign == null)
        {
            campaign = FindAnyObjectByType<Campaign_Manager>();
            if (campaign == null) return;
        }

        Campaign_State s = campaign.state;
        if (s == null) return;

        builder.Clear();
        builder.Append("전역 ").Append(s.battlesWon).Append('승')
               .Append(' ').Append(s.battlesFought - s.battlesWon).Append('패')
               .Append("      잔존 부대 ").Append(s.Get_Alive_Count())
               .Append('/').Append(s.armies.Count)
               .Append("      병력 ").Append(s.Get_Total_Strength());

        Draw_Label(new Rect(x, y, w, 16.0f), builder.ToString(),
                   style_Hint, color_General);
    }

    /// <summary>캠페인 관리자입니다. 없으면 캠페인 표시를 건너뜁니다.</summary>
    private Campaign_Manager campaign;

    /// <summary>전과 보고서의 한 행을 그립니다. 열 너비를 한 곳에서 정합니다.</summary>
    private void Draw_Report_Row(float x, float y, float w,
                                 string name, string kills, string losses,
                                 string survivors, string ratio,
                                 Color color, Color? ratioColor = null)
    {
        const float pad = 14.0f;
        float inner = w - pad * 2.0f;

        // 이름이 가장 넓고, 수치 열은 균등하게 나눕니다.
        float nameW = inner * 0.36f;
        float colW = (inner - nameW) * 0.25f;

        float cx = x + pad;

        Draw_Label(new Rect(cx, y, nameW, 16.0f), name, style_Small, color);
        cx += nameW;

        GUIStyle right = style_Center;

        Draw_Label(new Rect(cx, y, colW, 16.0f), kills, right, color);
        cx += colW;
        Draw_Label(new Rect(cx, y, colW, 16.0f), losses, right, color);
        cx += colW;
        Draw_Label(new Rect(cx, y, colW, 16.0f), survivors, right, color);
        cx += colW;
        Draw_Label(new Rect(cx, y, colW, 16.0f), ratio, right, ratioColor ?? color);
    }

    // =====================================================================
    // 상단 전력 표시줄
    //
    // 토탈워는 화면 위쪽에 양측 전력비를 항상 띄웁니다.
    // "지금 이기고 있는가"는 개별 부대를 다 확인하지 않고도 알아야 하는 정보입니다.
    // =====================================================================
    /// <summary>화면 상단에 양측 전력비와 병력 수를 그립니다.</summary>
    /// <param name="armies">집계할 전체 부대 목록입니다.</param>
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
    /// <summary>화면 하단에 아군 부대 카드 바를 그립니다.</summary>
    /// <param name="armies">카드로 만들 후보 부대 목록입니다.</param>
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

        // 이 영역 위의 클릭은 전장으로 넘기지 않습니다.
        // 그러지 않으면 카드를 눌러 선택한 직후 Controller가 '빈 땅 클릭'
        // 으로 판정해 선택을 도로 풀어 버립니다.
        UI_Input_Guard.Register(panel);

        float x = panelX + padding;
        float y = panelY + padding;

        // 마우스가 올라간 카드를 기억해 뒀다가 나중에 툴팁을 그립니다.
        //
        // 카드를 그리는 도중에 툴팁을 그리면 뒤에 오는 카드가 툴팁을
        // 덮어씁니다. IMGUI는 나중에 그린 것이 위에 오기 때문입니다.
        Army hovered = null;
        Rect hoveredCard = new Rect();

        for (int i = 0; i < cardArmies.Count; i++)
        {
            Rect card = new Rect(x, y, cardWidth, cardHeight);

            // 화면 밖으로 밀려나면 그리지 않습니다.
            if (card.xMax <= Screen.width)
            {
                Draw_Card(card, cardArmies[i], i);

                if (card.Contains(Event.current.mousePosition))
                {
                    hovered = cardArmies[i];
                    hoveredCard = card;
                }
            }

            x += cardWidth + gap;
        }

        Draw_Selection_Hint(panelX, panelY);

        // 툴팁은 마지막에 그려 모든 카드 위에 오게 합니다.
        if (hovered != null) Draw_Morale_Tooltip(hovered, hoveredCard);
    }

    // =====================================================================
    // 사기 툴팁
    //
    // 이 게임에서 부대는 전멸이 아니라 사기가 무너져 패배합니다.
    // 그런데 지금까지 화면에는 '합계 게이지'만 나왔습니다.
    //
    // Morale_Modifiers는 casualties / surrounded / outnumbered / fatigue /
    // flag / alliedRouting / winning / shock / terrain / general 열 항목을
    // 이미 개별로 들고 있습니다. 값은 있는데 보여 주지 않았을 뿐입니다.
    //
    // 그 화면이 없으면 플레이어는 부대가 왜 무너지는지 끝내 알 수 없고,
    // 정교하게 만든 사기 시스템이 '가끔 도망가는 랜덤'으로 체감됩니다.
    // 여기서는 읽어서 그리기만 합니다. 시뮬레이션은 건드리지 않습니다.
    // =====================================================================

    /// <summary>툴팁 한 줄의 높이입니다.</summary>
    private const float tooltipRowHeight = 14.0f;

    /// <summary>
    /// 마우스가 올라간 부대의 사기 내역을 항목별로 그립니다.
    /// </summary>
    /// <param name="army">내역을 보여 줄 부대입니다.</param>
    /// <param name="card">기준이 되는 카드 영역입니다. 이 위에 띄웁니다.</param>
    private void Draw_Morale_Tooltip(Army army, Rect card)
    {
        if (army == null) return;

        ref Army_Data data = ref army.army_Data;
        Morale_Modifiers mods = data.morale_Modifiers;

        // 값이 0인 항목은 건너뜁니다.
        //
        // 열 줄을 항상 띄우면 정작 봐야 할 큰 값이 묻힙니다.
        // 지금 작용 중인 것만 보여야 판단에 쓰입니다.
        int count = 0;

        if (Mathf.Abs(mods.casualties) > 0.5f) count++;
        if (Mathf.Abs(mods.surrounded) > 0.5f) count++;
        if (Mathf.Abs(mods.outnumbered) > 0.5f) count++;
        if (Mathf.Abs(mods.fatigue) > 0.5f) count++;
        if (Mathf.Abs(mods.flag) > 0.5f) count++;
        if (Mathf.Abs(mods.alliedRouting) > 0.5f) count++;
        if (Mathf.Abs(mods.winning) > 0.5f) count++;
        if (Mathf.Abs(mods.shock) > 0.5f) count++;
        if (Mathf.Abs(mods.terrain) > 0.5f) count++;
        if (Mathf.Abs(mods.general) > 0.5f) count++;

        // 제목 + 항목들 + 구분선 + 합계
        float w = 190.0f;
        float h = tooltipRowHeight * (count + 3) + 10.0f;

        // 카드 위쪽에 띄우되 화면 밖으로 나가지 않게 합니다.
        float x = Mathf.Min(card.x, Screen.width - w - 4.0f);
        float y = card.y - h - 6.0f;

        if (y < 4.0f) y = card.yMax + 6.0f;

        Rect panel = new Rect(x, y, w, h);
        Draw_Panel(panel);

        float ty = y + 5.0f;

        // 제목: 현재 사기와 상태
        builder.Clear();
        builder.Append("사기 ").Append(Mathf.RoundToInt(data.morale))
               .Append(" / ").Append(Mathf.RoundToInt(Constant.morale_Max));

        Draw_Label(new Rect(x + 8.0f, ty, w - 16.0f, tooltipRowHeight),
                   builder.ToString(), style_Small, Get_Morale_Color(data.e_Army_Morale));
        ty += tooltipRowHeight;

        // 항목들
        ty = Draw_Modifier(x, ty, w, "사상자", mods.casualties);
        ty = Draw_Modifier(x, ty, w, "포위됨", mods.surrounded);
        ty = Draw_Modifier(x, ty, w, "수적 열세", mods.outnumbered);
        ty = Draw_Modifier(x, ty, w, "피로", mods.fatigue);
        ty = Draw_Modifier(x, ty, w, "돌격 충격", mods.shock);
        ty = Draw_Modifier(x, ty, w, "아군 붕괴", mods.alliedRouting);
        ty = Draw_Modifier(x, ty, w, "깃발", mods.flag);
        ty = Draw_Modifier(x, ty, w, "우세", mods.winning);
        ty = Draw_Modifier(x, ty, w, "지형", mods.terrain);
        ty = Draw_Modifier(x, ty, w, "장군 근처", mods.general);

        // 구분선과 합계
        Draw_Rect(new Rect(x + 8.0f, ty + 2.0f, w - 16.0f, 1.0f), color_Edge);
        ty += 5.0f;

        builder.Clear();
        builder.Append("= ").Append(Mathf.RoundToInt(data.morale_Target))
               .Append("  (").Append(Get_Morale_Name(data.e_Army_Morale)).Append(')');

        Draw_Label(new Rect(x + 8.0f, ty, w - 16.0f, tooltipRowHeight),
                   builder.ToString(), style_Small, color_Text);
    }

    /// <summary>
    /// 사기 모디파이어 한 줄을 그립니다. 값이 0에 가까우면 건너뜁니다.
    /// </summary>
    /// <param name="x">패널의 왼쪽 좌표입니다.</param>
    /// <param name="y">이 줄을 그릴 세로 좌표입니다.</param>
    /// <param name="w">패널 너비입니다.</param>
    /// <param name="label">항목 이름입니다.</param>
    /// <param name="value">그 항목의 사기 증감입니다.</param>
    /// <returns>다음 줄의 세로 좌표입니다.</returns>
    private float Draw_Modifier(float x, float y, float w, string label, float value)
    {
        if (Mathf.Abs(value) <= 0.5f) return y;

        // 감소는 붉게, 증가는 푸르게. 부호만으로는 빠르게 읽히지 않습니다.
        Color color = value < 0.0f
            ? new Color(0.90f, 0.55f, 0.50f, 1.0f)
            : new Color(0.60f, 0.85f, 0.65f, 1.0f);

        builder.Clear();
        builder.Append(value > 0.0f ? "+" : "").Append(Mathf.RoundToInt(value));

        Draw_Label(new Rect(x + 8.0f, y, 34.0f, tooltipRowHeight),
                   builder.ToString(), style_Small, color);

        Draw_Label(new Rect(x + 46.0f, y, w - 54.0f, tooltipRowHeight),
                   label, style_Small, color_Text_Dim);

        return y + tooltipRowHeight;
    }

    /// <summary>사기 상태의 한글 이름을 반환합니다.</summary>
    /// <param name="morale">이름을 구할 사기 상태입니다.</param>
    /// <returns>표시용 상태 이름입니다.</returns>
    private static string Get_Morale_Name(E_Army_Morale morale)
    {
        switch (morale)
        {
            case E_Army_Morale.Wavering: return "동요";
            case E_Army_Morale.Broken: return "붕괴";
            case E_Army_Morale.Shattered: return "와해";
            default: return "견고";
        }
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

        // 1행: 병종 아이콘 + 이름 (장군이면 ★), 오른쪽 끝에 태세 아이콘
        //
        // 아이콘만 두지 않고 이름을 함께 남기는 이유:
        // 처음 보는 플레이어는 아이콘의 뜻을 모릅니다. 아이콘은 익숙해진
        // 뒤 빠르게 훑기 위한 것이고, 이름은 배우기 위한 것입니다.
        const float iconSize = 12.0f;

        // 병종 아이콘. 없으면 이름만 그리고 자리를 비우지 않습니다.
        bool bclassIcon = UI_Icons.Draw(
            new Rect(cx, cy, iconSize, iconSize),
            UI_Icons.Get_Class_Icon(data.GetE_Unit_Class()),
            data.bgeneral ? color_General : color_Text);

        float nameX = bclassIcon ? cx + iconSize + 2.0f : cx;

        builder.Clear();
        if (data.bgeneral) builder.Append("★ ");
        builder.Append(Get_Class_Name(data.GetE_Unit_Class()));

        Draw_Label(new Rect(nameX, cy, innerW - (nameX - cx) - iconSize - 2.0f, 13.0f),
                   builder.ToString(), style_Name,
                   data.bgeneral ? color_General : color_Text);

        // 태세 아이콘은 오른쪽 끝에 둡니다.
        // 기본 태세(Line)는 아이콘이 없어 아무것도 그리지 않습니다.
        UI_Icons.Draw(
            new Rect(cx + innerW - iconSize, cy, iconSize, iconSize),
            UI_Icons.Get_Stance_Icon(data.e_Army_Stance),
            color_Text);

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

        // 2.5행: 전과
        //
        // 토탈워는 카드에 킬 수를 띄웁니다. 어느 부대가 실제로 일하고 있는지는
        // 인원이나 사기만 봐서는 알 수 없습니다. 손실 없이 버티는 부대와
        // 적을 갈아 넣고 있는 부대는 전혀 다른 판단을 요구합니다.
        if (army.killCount > 0 || army.lossCount > 0)
        {
            builder.Clear();
            builder.Append("⚔ ").Append(army.killCount);

            Draw_Label(new Rect(cx, cy, innerW * 0.5f, 11.0f), builder.ToString(),
                       style_Small, color_Kill);

            // 교환비는 1.0을 기준으로 색이 갈립니다. 이득이면 금색, 손해면 붉은색.
            float ratio = army.GetKillRatio();

            builder.Clear();
            builder.Append(ratio.ToString("0.0")).Append(':').Append('1');

            Draw_Label(new Rect(cx + innerW * 0.5f, cy, innerW * 0.5f, 11.0f),
                       builder.ToString(), style_Small,
                       ratio >= 1.0f ? color_General : color_Text_Dim);

            cy += 11.0f;
        }

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
    /// <summary>배치 단계나 전투 결과를 화면 중앙 배너로 그립니다.</summary>
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
    /// <summary>
    /// 사기 게이지를 그립니다. 동요 중에는 점멸시켜 붕괴가 임박했음을 알립니다.
    /// </summary>
    /// <param name="rect">게이지를 그릴 영역입니다.</param>
    /// <param name="army">사기를 읽어 올 부대입니다.</param>
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

    /// <summary>바탕과 테두리로 이루어진 패널을 그립니다.</summary>
    /// <param name="rect">패널 영역입니다.</param>
    private void Draw_Panel(Rect rect)
    {
        Draw_Rect(rect, color_Panel);
        Draw_Border(rect, color_Edge, 1.0f);
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
    /// <summary>부대의 현재 상태를 한 단어로 요약합니다.</summary>
    /// <param name="army">상태를 읽어 올 부대입니다.</param>
    /// <returns>와해/패주/돌격/동요/교전 중 하나이며, 해당 없으면 null입니다.</returns>
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

    /// <summary>사기 상태에 대응하는 표시색을 반환합니다.</summary>
    /// <param name="morale">사기 상태입니다.</param>
    /// <returns>그 상태를 나타내는 색입니다.</returns>
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

    /// <summary>피로 단계에 대응하는 표시색을 반환합니다.</summary>
    /// <param name="fatigue">피로 단계입니다.</param>
    /// <returns>그 단계를 나타내는 색입니다.</returns>
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

    /// <summary>병종의 한글 표시 이름을 반환합니다.</summary>
    /// <param name="unitClass">이름을 구할 병종입니다.</param>
    /// <returns>표시용 병종 이름입니다.</returns>
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

    /// <summary>태세의 한글 표시 이름을 반환합니다.</summary>
    /// <param name="stance">이름을 구할 태세입니다.</param>
    /// <returns>표시용 태세 이름입니다.</returns>
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

    /// <summary>피로 단계의 한글 표시 이름을 반환합니다.</summary>
    /// <param name="fatigue">이름을 구할 피로 단계입니다.</param>
    /// <returns>표시용 피로 단계 이름입니다.</returns>
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

    /// <summary>전투 결과 등급의 한글 표시 이름을 반환합니다.</summary>
    /// <param name="result">이름을 구할 결과 등급입니다.</param>
    /// <returns>표시용 결과 이름입니다. None이면 빈 문자열입니다.</returns>
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
