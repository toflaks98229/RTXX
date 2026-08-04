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

/// <summary>
/// 전투가 끝난 시점의 부대 하나에 대한 전과 기록입니다.
///
/// 왜 Army 참조가 아니라 값을 복사해 두는가:
/// 전멸한 부대는 GameObject가 파괴되어 Army가 null이 됩니다.
/// 결과 화면은 그 부대야말로 가장 보여줘야 할 대상이므로
/// (전멸했지만 적을 많이 잡았을 수 있습니다) 값으로 떠 둡니다.
/// </summary>
public struct Battle_Report_Entry
{
    /// <summary>부대 이름입니다.</summary>
    public string name;
    /// <summary>플레이어 소속 여부입니다.</summary>
    public bool bplayer;
    /// <summary>장군 부대 여부입니다.</summary>
    public bool bgeneral;
    /// <summary>병종입니다.</summary>
    public E_Unit_Class unitClass;

    /// <summary>쓰러뜨린 적 수입니다.</summary>
    public int kills;
    /// <summary>잃은 아군 수입니다.</summary>
    public int losses;
    /// <summary>생존 인원입니다.</summary>
    public int survivors;
    /// <summary>전투 시작 시점 인원입니다.</summary>
    public int startCount;

    /// <summary>전투 종료 시점의 사기 상태입니다.</summary>
    public E_Army_Morale morale;

    /// <summary>교환비입니다. 잃은 1명당 잡은 수입니다.</summary>
    public float KillRatio => losses > 0 ? (float)kills / losses : kills;

    /// <summary>생존율(0~1)입니다.</summary>
    public float SurvivalRate => startCount > 0 ? (float)survivors / startCount : 0.0f;
}

/// <summary>전투 결과 등급입니다. 사상자 비율로 결정됩니다.</summary>
public enum E_Battle_Result
{
    /// <summary>아직 판정되지 않았습니다. 전투가 끝나기 전의 값입니다.</summary>
    None,

    /// <summary>압승입니다. 거의 잃지 않고 적을 무너뜨렸습니다.</summary>
    CrushingVictory,

    /// <summary>승리입니다.</summary>
    ClearVictory,

    /// <summary>신승입니다. 큰 손실을 치르고 간신히 이겼습니다.</summary>
    CloseVictory,

    /// <summary>석패입니다. 졌지만 적에게도 큰 손실을 입혔습니다.</summary>
    CloseDefeat,

    /// <summary>패배입니다.</summary>
    ClearDefeat,

    /// <summary>참패입니다. 적에게 거의 손실을 입히지 못하고 무너졌습니다.</summary>
    CrushingDefeat
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
    /// <summary>부대 목록을 읽어 올 컨트롤러입니다. 비워 두면 전역 목록을 씁니다.</summary>
    [Header("연결")]
    public Controller controller;

    /// <summary>배치 단계를 쓸지 여부입니다. 끄면 시작하자마자 교전합니다.</summary>
    [Header("배치 단계")]
    [Tooltip("배치 단계를 사용할지 여부입니다. 끄면 시작하자마자 교전합니다.")]
    public bool buseDeployment = true;

    /// <summary>배치를 끝내고 전투를 시작하는 키입니다.</summary>
    [Tooltip("배치를 끝내고 전투를 시작하는 키입니다.")]
    public KeyCode keyCode_Start_Battle = KeyCode.Space;

    /// <summary>전투 종료 후 결과를 표시하는 시간(초)입니다. 0이면 계속 표시합니다.</summary>
    [Header("종료 판정")]
    [Tooltip("전투 종료 후 결과를 표시하는 시간(초)입니다. 0이면 계속 표시합니다.")]
    public float resultDisplayTime = 0.0f;

    [Header("표시")]
    [Tooltip("이 컴포넌트가 직접 단계/결과 문구를 그릴지 여부입니다.\n" +
             "UI_Command_Bar가 같은 내용을 더 나은 배치로 그리므로 기본값은 꺼짐입니다.\n" +
             "HUD 없이 이 스크립트만 쓸 때만 켜십시오.")]
    /// <summary>
    /// 이 컴포넌트가 직접 단계/결과 문구를 그릴지 여부입니다.
    /// UI_Command_Bar가 같은 내용을 더 나은 배치로 그리므로 기본값은 꺼짐입니다.
    /// </summary>
    public bool bdrawOwnGUI = false;

    /// <summary>현재 전투 단계입니다.</summary>
    public E_Battle_Phase phase
    {
        get => phaseValue;
        private set
        {
            phaseValue = value;

            // 정적 사본을 함께 갱신합니다. 아래 bdeploying 주석 참조.
            bdeploying = value == E_Battle_Phase.Deployment;
        }
    }

    /// <summary>phase의 실제 저장소입니다.</summary>
    private E_Battle_Phase phaseValue = E_Battle_Phase.Deployment;

    /// <summary>
    /// 지금이 배치 단계인지 나타내는 정적 사본입니다.
    ///
    /// 왜 정적으로 두는가:
    /// Army는 Battle_Manager를 참조하지 않습니다. 부대가 전투 진행
    /// 계층을 알 필요가 없다는 것이 이 프로젝트의 구조이고, 그 때문에
    /// Army에 참조를 새로 배선하면 프리팹과 씬을 전부 고쳐야 합니다.
    ///
    /// 그런데 '배치 중인가'는 부대가 알아야 할 일이 하나 있습니다.
    /// 배치 중에는 시뮬레이션이 멈추므로, 태세를 바꿔도 유닛을 움직여
    /// 줄 주체가 없어 즉시 옮겨야 합니다.
    ///
    /// bool 하나뿐이고 Battle_Manager가 유일한 주인이므로 정적 사본이
    /// 가장 싸고 안전합니다.
    ///
    /// 기본값이 false인 것에 주의하십시오. Battle_Manager가 없는
    /// 씬(대규모 검증 씬 등)에서는 배치 개념이 없습니다.
    /// </summary>
    public static bool bdeploying { get; private set; }

    /// <summary>전투 결과입니다. 종료 전에는 None입니다.</summary>
    public E_Battle_Result result { get; private set; } = E_Battle_Result.None;

    // 통계 (GameEvents 구독으로 누적)
    /// <summary>아군의 누적 손실 인원입니다. OnUnitKilled 구독으로 쌓입니다.</summary>
    private int playerLosses;
    /// <summary>적군의 누적 손실 인원입니다.</summary>
    private int enemyLosses;
    /// <summary>전투 시작 시점의 아군 병력입니다. 결과 등급의 기준입니다.</summary>
    private int playerStart;
    /// <summary>전투 시작 시점의 적군 병력입니다.</summary>
    private int enemyStart;

    /// <summary>전투가 끝난 뒤 산출된 부대별 전과입니다. 종료 전에는 비어 있습니다.</summary>
    private readonly List<Battle_Report_Entry> reportEntries = new List<Battle_Report_Entry>();

    /// <summary>
    /// 부대별 전과 목록입니다. 결과 화면이 읽어 갑니다.
    ///
    /// 전투가 끝난 시점에 한 번만 만들어집니다. 매 프레임 다시 모으면
    /// 전멸한 부대가 목록에서 빠져 기록이 사라지기 때문입니다.
    /// </summary>
    public IReadOnlyList<Battle_Report_Entry> ReportEntries => reportEntries;

    /// <summary>아군의 총 손실입니다.</summary>
    public int PlayerLosses => playerLosses;
    /// <summary>적군의 총 손실입니다.</summary>
    public int EnemyLosses => enemyLosses;
    /// <summary>아군의 전투 시작 시점 병력입니다.</summary>
    public int PlayerStart => playerStart;
    /// <summary>적군의 전투 시작 시점 병력입니다.</summary>
    public int EnemyStart => enemyStart;

    /// <summary>전투가 끝난 시각입니다. 결과 표시 시간을 재는 데 씁니다.</summary>
    private float finishedTime;
    /// <summary>결과 배너에 쓰는 큰 스타일입니다.</summary>
    private GUIStyle bannerStyle;
    /// <summary>손실 수치 등 부제에 쓰는 스타일입니다.</summary>
    private GUIStyle infoStyle;

    /// <summary>
    /// 전투 단계를 정합니다.
    ///
    /// Start가 아닌 Awake인 이유: Controller.FixedUpdate는 배치 단계면 시뮬레이션을
    /// 통째로 건너뜁니다. Start의 실행 순서는 컴포넌트 간에 보장되지 않으므로,
    /// Controller가 먼저 돌면 배치를 쓰지 않는 씬에서도 첫 틱이 멈춘 채 지나갑니다.
    /// </summary>
    private void Awake()
    {
        // 구독은 여기서 하지 않습니다.
        // Controller.Awake가 GameEvents.ClearAll을 부르므로 지워집니다.

        // 단계는 반드시 Awake에서 정합니다.
        //
        // 왜 Start가 아닌가:
        // Controller.FixedUpdate는 phase가 Deployment면 시뮬레이션을 통째로
        // 건너뜁니다. 그런데 Start()의 실행 순서는 컴포넌트 간에 보장되지
        // 않으므로, Controller가 먼저 돌면 배치를 쓰지 않는 씬에서도
        // 첫 몇 틱이(혹은 영원히) 멈춘 채 지나갑니다.
        //
        // 실제로 배치모드 검증에서 이 순서 문제로 600프레임 동안 시뮬레이션이
        // 한 틱도 돌지 않았습니다. Awake는 모든 Start보다 앞서므로,
        // 여기서 정하면 어떤 실행 순서에서도 첫 틱부터 올바릅니다.
        phase = buseDeployment ? E_Battle_Phase.Deployment : E_Battle_Phase.Fighting;
    }

    /// <summary>사망 이벤트를 구독하고 시작 병력을 기록합니다.</summary>
    private void Start()
    {
        GameEvents.OnUnitKilled += On_Unit_Killed;

        Record_Starting_Strength();
    }

    /// <summary>정적 이벤트 구독을 해지합니다. 두면 파괴된 객체가 호출됩니다.</summary>
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

    /// <summary>집계 대상 부대 목록을 반환합니다.</summary>
    /// <returns>컨트롤러가 있으면 그 목록, 없으면 전역 목록입니다.</returns>
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

    /// <summary>배치 단계에서는 시작 입력을, 교전 중에는 승패를 확인합니다.</summary>
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

    /// <summary>
    /// 부대별 전과를 한 번만 떠서 보관합니다.
    ///
    /// 전투 종료 시점에 호출합니다. 이후 부대가 파괴되어도 기록은 남습니다.
    /// 전과가 큰 순서로 정렬해, 결과 화면 위쪽에 활약한 부대가 오게 합니다.
    /// </summary>
    private void Build_Report()
    {
        reportEntries.Clear();

        List<Army> armies = Get_Armies();
        if (armies == null) return;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            ref Army_Data d = ref army.army_Data;

            reportEntries.Add(new Battle_Report_Entry
            {
                name = army.name,
                bplayer = d.bplayer,
                bgeneral = d.bgeneral,
                unitClass = d.GetE_Unit_Class(),

                kills = army.killCount,
                losses = army.lossCount,
                survivors = army.units.Count,
                startCount = d.unit_Num_Max,

                morale = d.e_Army_Morale
            });
        }

        // 아군 먼저, 그 안에서 전과가 많은 순입니다.
        reportEntries.Sort((a, b) =>
        {
            if (a.bplayer != b.bplayer) return a.bplayer ? -1 : 1;
            return b.kills.CompareTo(a.kills);
        });
    }

    /// <summary>전투를 종료하고 결과 등급을 산출합니다.</summary>
    private void Finish_Battle(bool bplayerFighting, bool benemyFighting)
    {
        phase = E_Battle_Phase.Finished;
        finishedTime = Time.time;

        // 부대가 파괴되기 전에 전과를 떠 둡니다.
        Build_Report();

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

    /// <summary>GUI 스타일을 처음 한 번만 만듭니다.</summary>
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

    /// <summary>단계와 결과 문구를 그립니다. bdrawOwnGUI가 꺼져 있으면 아무것도 그리지 않습니다.</summary>
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

    /// <summary>화면 가로 중앙에 제목과 부제를 그립니다.</summary>
    /// <param name="title">큰 글씨로 그릴 제목입니다.</param>
    /// <param name="info">작은 글씨로 그릴 부제입니다.</param>
    /// <param name="y">그리기 시작할 세로 위치입니다.</param>
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
