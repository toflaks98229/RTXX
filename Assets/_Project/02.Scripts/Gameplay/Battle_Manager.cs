using System.Collections.Generic;
using UnityEngine;

/// <summary>전투의 진행 단계입니다.</summary>
public enum E_Battle_Phase
{
    /// <summary>배치 단계입니다. 전투 시작 전 진형을 잡습니다.</summary>
    Deployment,

    /// <summary>교전 중입니다.</summary>
    Fighting,

    /// <summary>전투가 끝났습니다.</summary>
    Finished
}

/// <summary>전투 결과 등급입니다. 사상자 비율로 결정됩니다.</summary>
public enum E_Battle_Result
{
    None,
    CrushingVictory,  // 압승 - 거의 잃지 않고 전멸시킴
    ClearVictory,     // 승리
    CloseVictory,     // 신승 - 간신히 이김
    CloseDefeat,      // 석패
    ClearDefeat,      // 패배
    CrushingDefeat    // 참패
}

/// <summary>
/// 전투 한 판의 진행을 관리합니다. 배치 → 교전 → 종료.
///
/// 왜 필요한가:
/// 지금까지 전투는 '시작도 끝도 없이' 그냥 돌아갔습니다.
/// GameEvents.OnArmyWiped는 발행되고 있었지만 구독자가 아무도 없어
/// 전멸해도 아무 일도 일어나지 않았습니다.
///
/// 이 클래스는 이벤트 버스를 구독하기만 하고 시뮬레이션은 건드리지 않습니다.
/// T1-2에서 만들어 둔 이벤트들이 여기서 처음으로 값어치를 합니다.
/// </summary>
public class Battle_Manager : MonoBehaviour
{
    [Header("연결")]
    public Controller controller;

    [Header("배치 단계")]
    [Tooltip("배치 단계를 사용할지 여부입니다. 끄면 시작하자마자 교전합니다.")]
    public bool buseDeployment = true;

    [Tooltip("배치를 끝내고 전투를 시작하는 키입니다.")]
    public KeyCode keyCode_Start_Battle = KeyCode.Space;

    [Header("종료 판정")]
    [Tooltip("전투 종료 후 결과를 표시하는 시간(초)입니다. 0이면 계속 표시합니다.")]
    public float resultDisplayTime = 0.0f;

    [Header("표시")]
    [Tooltip("이 컴포넌트가 직접 단계/결과 문구를 그릴지 여부입니다.\n" +
             "UI_Command_Bar가 같은 내용을 더 나은 배치로 그리므로 기본값은 꺼짐입니다.\n" +
             "HUD 없이 이 스크립트만 쓸 때만 켜십시오.")]
    public bool bdrawOwnGUI = false;

    /// <summary>현재 전투 단계입니다.</summary>
    public E_Battle_Phase phase { get; private set; } = E_Battle_Phase.Deployment;

    /// <summary>전투 결과입니다. 종료 전에는 None입니다.</summary>
    public E_Battle_Result result { get; private set; } = E_Battle_Result.None;

    // 통계 (GameEvents 구독으로 누적)
    private int playerLosses;
    private int enemyLosses;
    private int playerStart;
    private int enemyStart;

    private float finishedTime;
    private GUIStyle bannerStyle;
    private GUIStyle infoStyle;

    private void Awake()
    {
        // Controller.Awake가 GameEvents.ClearAll을 부르므로
        // 구독은 반드시 Start에서 해야 합니다. (Awake에서 하면 지워집니다)
    }

    private void Start()
    {
        GameEvents.OnUnitKilled += On_Unit_Killed;

        Record_Starting_Strength();

        if (!buseDeployment)
        {
            Start_Battle();
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnUnitKilled -= On_Unit_Killed;
    }

    /// <summary>전투 시작 시점의 양측 병력을 기록합니다. 결과 판정의 기준입니다.</summary>
    private void Record_Starting_Strength()
    {
        playerStart = 0;
        enemyStart = 0;

        List<Army> armies = Get_Armies();
        if (armies == null) return;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            if (army.army_Data.bplayer) playerStart += army.units.Count;
            else enemyStart += army.units.Count;
        }
    }

    private List<Army> Get_Armies()
    {
        return controller != null ? controller.armies : Army.allArmies;
    }

    /// <summary>유닛이 죽을 때마다 양측 손실을 누적합니다.</summary>
    private void On_Unit_Killed(Unit unit, Army victimArmy, Army killerArmy)
    {
        if (victimArmy == null) return;

        if (victimArmy.army_Data.bplayer) playerLosses++;
        else enemyLosses++;
    }

    private void Update()
    {
        switch (phase)
        {
            case E_Battle_Phase.Deployment:
                if (Input.GetKeyDown(keyCode_Start_Battle)) Start_Battle();
                break;

            case E_Battle_Phase.Fighting:
                _Update_Victory_Check();
                break;
        }
    }

    /// <summary>
    /// 배치를 끝내고 교전을 시작합니다.
    /// 배치 중에는 시뮬레이션이 돌지 않으므로 여기서 다시 켭니다.
    /// </summary>
    public void Start_Battle()
    {
        if (phase != E_Battle_Phase.Deployment) return;

        phase = E_Battle_Phase.Fighting;

        // 배치 단계에서 인원이 바뀌었을 수 있으므로 다시 셉니다.
        Record_Starting_Strength();
    }

    /// <summary>
    /// 승패를 판정합니다.
    ///
    /// 토탈워의 종료 조건: 한쪽이 전멸하거나 전군이 무너지면 끝납니다.
    /// 여기서 '무너졌다'는 것은 전멸뿐 아니라 패주/와해도 포함합니다.
    /// 도망치는 부대만 남았다면 그 군대는 이미 진 것입니다.
    /// </summary>
    private void _Update_Victory_Check()
    {
        List<Army> armies = Get_Armies();
        if (armies == null) return;

        bool bplayerFighting = false;
        bool benemyFighting = false;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;

            // 무너진 부대는 더 이상 전력이 아닙니다.
            if (army.army_Data.IsBroken()) continue;

            if (army.army_Data.bplayer) bplayerFighting = true;
            else benemyFighting = true;
        }

        // 양측 모두 싸울 수 있으면 전투는 계속됩니다.
        if (bplayerFighting && benemyFighting) return;

        Finish_Battle(bplayerFighting, benemyFighting);
    }

    /// <summary>전투를 종료하고 결과 등급을 산출합니다.</summary>
    private void Finish_Battle(bool bplayerFighting, bool benemyFighting)
    {
        phase = E_Battle_Phase.Finished;
        finishedTime = Time.time;

        // 양측이 동시에 무너졌으면 방어측(적) 승리로 봅니다.
        // 토탈워도 무승부는 방어측 승리로 처리합니다.
        bool bwon = bplayerFighting && !benemyFighting;

        float myLossRate = playerStart > 0 ? (float)playerLosses / playerStart : 1.0f;

        if (bwon)
        {
            if (myLossRate < 0.15f) result = E_Battle_Result.CrushingVictory;
            else if (myLossRate < 0.45f) result = E_Battle_Result.ClearVictory;
            else result = E_Battle_Result.CloseVictory;
        }
        else
        {
            float enemyLossRate = enemyStart > 0 ? (float)enemyLosses / enemyStart : 0.0f;

            if (enemyLossRate > 0.55f) result = E_Battle_Result.CloseDefeat;
            else if (enemyLossRate > 0.25f) result = E_Battle_Result.ClearDefeat;
            else result = E_Battle_Result.CrushingDefeat;
        }
    }

    private void Ensure_Styles()
    {
        if (bannerStyle != null) return;

        bannerStyle = new GUIStyle(GUI.skin.label);
        bannerStyle.alignment = TextAnchor.MiddleCenter;
        bannerStyle.fontSize = 28;
        bannerStyle.fontStyle = FontStyle.Bold;
        bannerStyle.normal.textColor = Color.white;

        infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.alignment = TextAnchor.MiddleCenter;
        infoStyle.fontSize = 14;
        infoStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        // HUD(UI_Command_Bar)가 같은 내용을 그리므로 기본적으로 비워 둡니다.
        // 둘 다 그리면 배치/결과 문구가 겹쳐 보입니다.
        if (!bdrawOwnGUI) return;

        Ensure_Styles();

        switch (phase)
        {
            case E_Battle_Phase.Deployment:
                Draw_Center_Text("배치 단계",
                                 $"부대를 배치한 뒤 {keyCode_Start_Battle} 키로 전투를 시작합니다.",
                                 60.0f);
                break;

            case E_Battle_Phase.Finished:
                if (resultDisplayTime > 0.0f
                    && Time.time - finishedTime > resultDisplayTime) return;

                Draw_Center_Text(Get_Result_Text(result),
                                 $"아군 손실 {playerLosses}/{playerStart}    " +
                                 $"적군 손실 {enemyLosses}/{enemyStart}",
                                 Screen.height * 0.4f);
                break;
        }
    }

    private void Draw_Center_Text(string title, string info, float y)
    {
        float width = Screen.width;

        Rect back = new Rect(0.0f, y - 10.0f, width, 78.0f);
        Color previous = GUI.color;
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.55f);
        GUI.DrawTexture(back, Texture2D.whiteTexture);
        GUI.color = previous;

        GUI.Label(new Rect(0.0f, y, width, 40.0f), title, bannerStyle);
        GUI.Label(new Rect(0.0f, y + 40.0f, width, 24.0f), info, infoStyle);
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
