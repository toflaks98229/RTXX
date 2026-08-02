using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 부대 위에 사기/피로/인원/병종을 띄우는 화면 오버레이입니다.
///
/// 왜 필요한가:
/// 시뮬레이션은 사기, 피로, 측후방 판정, 상성까지 정밀하게 계산하고 있는데
/// 화면에는 아무것도 나오지 않았습니다. 플레이어 입장에서는 이유를 모른 채
/// 부대가 갑자기 도망칩니다. 계산한 것을 보여주는 것이 이 파일의 전부입니다.
///
/// 구현 방식:
/// IMGUI(OnGUI)로 그립니다. 프리팹이나 Canvas 배선이 필요 없어
/// 씬에 이 컴포넌트 하나만 올리면 바로 동작합니다.
/// 부대 수는 많아야 수십 개이므로 IMGUI 비용도 문제되지 않습니다.
/// (유닛 단위였다면 절대 이렇게 하면 안 됩니다)
/// </summary>
public class UI_Army_Banner : MonoBehaviour
{
    [Header("표시 대상")]
    [Tooltip("비워 두면 씬의 모든 부대를 표시합니다.")]
    public Controller controller;

    [Tooltip("적 부대의 배너도 표시할지 여부입니다.")]
    public bool bshowEnemy = true;

    [Header("배치")]
    [Tooltip("부대 머리 위로 띄울 높이입니다.")]
    public float heightOffset = 6.0f;

    [Tooltip("배너의 픽셀 너비입니다.")]
    public float bannerWidth = 132.0f;

    [Header("상세")]
    [Tooltip("선택된 부대에 사기 모디파이어 내역을 함께 표시합니다.")]
    public bool bshowModifierBreakdown = true;

    // 매 프레임 문자열을 새로 만들면 GC가 계속 돌므로 재사용합니다.
    private readonly StringBuilder builder = new StringBuilder(256);

    private GUIStyle labelStyle;
    private GUIStyle breakdownStyle;
    private Texture2D barTexture;

    /// <summary>단색 텍스처 하나를 색만 바꿔 가며 재사용합니다.</summary>
    private void Awake()
    {
        barTexture = new Texture2D(1, 1);
        barTexture.SetPixel(0, 0, Color.white);
        barTexture.Apply();
    }

    private void OnDestroy()
    {
        if (barTexture != null) Destroy(barTexture);
    }

    /// <summary>스타일은 OnGUI 안에서만 만들 수 있어 최초 1회 지연 생성합니다.</summary>
    private void Ensure_Styles()
    {
        if (labelStyle != null) return;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontSize = 11;
        labelStyle.normal.textColor = Color.white;

        breakdownStyle = new GUIStyle(GUI.skin.label);
        breakdownStyle.alignment = TextAnchor.UpperLeft;
        breakdownStyle.fontSize = 10;
        breakdownStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        Camera camera = Main_Camera.Get();
        if (camera == null) return;

        Ensure_Styles();

        List<Army> armies = controller != null ? controller.armies : Army.allArmies;
        if (armies == null) return;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;
            if (!bshowEnemy && !army.army_Data.bplayer) continue;

            Draw_Army(camera, army);
        }
    }

    /// <summary>부대 하나의 배너를 그립니다.</summary>
    private void Draw_Army(Camera camera, Army army)
    {
        Vector3 worldPosition = army.GetPosition();
        worldPosition.y += heightOffset;

        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);

        // 카메라 뒤쪽이면 그리지 않습니다. (그리면 화면에 뒤집혀 나타납니다)
        if (screenPosition.z <= 0.0f) return;

        // WorldToScreenPoint는 좌하단 원점, GUI는 좌상단 원점입니다.
        float x = screenPosition.x - bannerWidth * 0.5f;
        float y = Screen.height - screenPosition.y;

        if (x + bannerWidth < 0.0f || x > Screen.width) return;
        if (y < 0.0f || y > Screen.height) return;

        ref Army_Data data = ref army.army_Data;

        const float rowHeight = 13.0f;
        float cursorY = y;

        // 1행: 병종 · 인원 · 피로
        builder.Clear();
        builder.Append(Get_Class_Name(data.GetE_Unit_Class()));
        builder.Append("  ");
        builder.Append(data.unit_Num);
        builder.Append('/');
        builder.Append(data.unit_Num_Max);
        builder.Append("  ");
        builder.Append(Get_Fatigue_Name(data.e_Army_Fatigue));

        // 기본 태세(Line)는 굳이 표시하지 않습니다. 화면이 시끄러워집니다.
        if (data.e_Army_Stance != E_Army_Stance.Line)
        {
            builder.Append("  [");
            builder.Append(Get_Stance_Name(data.e_Army_Stance));
            builder.Append(']');
        }

        Draw_Shadowed_Label(new Rect(x, cursorY, bannerWidth, rowHeight),
                            builder.ToString(),
                            Get_Team_Color(data.bplayer));
        cursorY += rowHeight;

        // 2행: 사기 게이지
        Draw_Morale_Bar(new Rect(x, cursorY, bannerWidth, 6.0f), army);
        cursorY += 8.0f;

        // 3행: 상태 문구 (교전/패주/와해 등)
        string state = Get_State_Text(army);
        if (!string.IsNullOrEmpty(state))
        {
            Draw_Shadowed_Label(new Rect(x, cursorY, bannerWidth, rowHeight),
                                state,
                                Get_Morale_Color(data.e_Army_Morale));
            cursorY += rowHeight;
        }

        // 선택된 부대만 사기 내역을 펼칩니다. 전부 펼치면 화면이 가득 찹니다.
        if (bshowModifierBreakdown && army.IsSelected())
        {
            Draw_Modifier_Breakdown(x, cursorY, army);
        }
    }

    /// <summary>
    /// 사기 게이지입니다. 상태에 따라 색이 바뀌고, 동요 중에는 점멸합니다.
    /// 점멸은 '곧 무너진다'는 신호이므로 토탈워에서도 같은 방식을 씁니다.
    /// </summary>
    private void Draw_Morale_Bar(Rect rect, Army army)
    {
        ref Army_Data data = ref army.army_Data;

        float rate = Constant.morale_Max > 0.0f
            ? data.morale / Constant.morale_Max
            : 0.0f;

        if (rate < 0.0f) rate = 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        // 배경
        Draw_Rect(rect, new Color(0.0f, 0.0f, 0.0f, 0.55f));

        Color color = Get_Morale_Color(data.e_Army_Morale);

        // 동요 상태는 점멸시켜 위급함을 알립니다.
        if (data.e_Army_Morale == E_Army_Morale.Wavering)
        {
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6.0f));
            color.a *= pulse;
        }

        Rect fill = rect;
        fill.width = rect.width * rate;
        Draw_Rect(fill, color);
    }

    /// <summary>선택된 부대의 사기 모디파이어를 항목별로 나열합니다.</summary>
    private void Draw_Modifier_Breakdown(float x, float y, Army army)
    {
        Morale_Modifiers m = army.army_Data.morale_Modifiers;

        builder.Clear();
        Append_Modifier(builder, "사상자", m.casualties);
        Append_Modifier(builder, "포위", m.surrounded);
        Append_Modifier(builder, "수적열세", m.outnumbered);
        Append_Modifier(builder, "피로", m.fatigue);
        Append_Modifier(builder, "깃발", m.flag);
        Append_Modifier(builder, "아군붕괴", m.alliedRouting);
        Append_Modifier(builder, "우세", m.winning);
        Append_Modifier(builder, "충격", m.shock);
        Append_Modifier(builder, "고지", m.terrain);

        if (builder.Length == 0) return;

        string text = builder.ToString();

        // 글자 수에 맞춰 높이를 잡습니다.
        float height = breakdownStyle.CalcHeight(new GUIContent(text), bannerWidth);
        Rect rect = new Rect(x, y, bannerWidth, height);

        Draw_Rect(rect, new Color(0.0f, 0.0f, 0.0f, 0.5f));
        GUI.Label(rect, text, breakdownStyle);
    }

    /// <summary>0이 아닌 항목만 한 줄씩 덧붙입니다.</summary>
    private static void Append_Modifier(StringBuilder builder, string name, float value)
    {
        // 소수점 이하의 미세한 값은 표시해도 의미가 없습니다.
        if (value > -0.5f && value < 0.5f) return;

        if (builder.Length > 0) builder.Append('\n');

        builder.Append(value > 0.0f ? '+' : '-');
        builder.Append(Mathf.RoundToInt(Mathf.Abs(value)));
        builder.Append(' ');
        builder.Append(name);
    }

    /// <summary>상태 문구입니다. 지금 이 부대에 무슨 일이 일어나는지 한 줄로 알립니다.</summary>
    private static string Get_State_Text(Army army)
    {
        ref Army_Data data = ref army.army_Data;

        if (data.e_Army_Morale == E_Army_Morale.Shattered) return "와해";
        if (data.e_Army_Morale == E_Army_Morale.Broken) return "패주";
        if (data.e_Army_Move == E_Army_Move.MoveCharge) return "돌격";
        if (data.e_Army_Morale == E_Army_Morale.Wavering) return "동요";
        if (data.e_Army_Fight != E_Army_Fight.Non) return "교전";

        return null;
    }

    private static Color Get_Morale_Color(E_Army_Morale morale)
    {
        switch (morale)
        {
            case E_Army_Morale.Steady: return new Color(0.35f, 0.85f, 0.35f, 0.95f);
            case E_Army_Morale.Wavering: return new Color(0.95f, 0.80f, 0.20f, 0.95f);
            case E_Army_Morale.Broken: return new Color(0.90f, 0.30f, 0.20f, 0.95f);
            default: return new Color(0.45f, 0.10f, 0.10f, 0.95f); // Shattered
        }
    }

    private static Color Get_Team_Color(bool bplayer)
    {
        return bplayer
            ? new Color(0.70f, 0.85f, 1.00f, 1.0f)
            : new Color(1.00f, 0.75f, 0.70f, 1.0f);
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

    /// <summary>단색 사각형을 그립니다.</summary>
    private void Draw_Rect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, barTexture);
        GUI.color = previous;
    }

    /// <summary>
    /// 그림자를 깔고 글자를 그립니다.
    /// 전장 배경이 밝든 어둡든 읽히게 하려면 외곽선이 필요합니다.
    /// </summary>
    private void Draw_Shadowed_Label(Rect rect, string text, Color color)
    {
        Color previous = labelStyle.normal.textColor;

        labelStyle.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
        Rect shadow = rect;
        shadow.x += 1.0f;
        shadow.y += 1.0f;
        GUI.Label(shadow, text, labelStyle);

        labelStyle.normal.textColor = color;
        GUI.Label(rect, text, labelStyle);

        labelStyle.normal.textColor = previous;
    }
}
