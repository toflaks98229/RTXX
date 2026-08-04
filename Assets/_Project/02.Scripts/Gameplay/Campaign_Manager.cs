using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캠페인 상태를 저장하고, 전투 결과를 그 상태에 반영하는 계층입니다.
///
/// 설계 방침:
/// 시뮬레이션을 일절 건드리지 않습니다. Battle_Manager가 전투 종료 시점에
/// 떠 두는 전과 보고서(Battle_Report_Entry)만 읽어 갑니다. 즉 이 클래스가
/// 없어도 전투는 그대로 돌아가고, 있으면 그 결과가 다음 전투로 이어집니다.
///
/// 이것이 2순위 작업(킬 통계 완결)에서 열어 둔 경로입니다. 그때 만든
/// Battle_Report_Entry가 survivors / kills / startCount를 값으로 들고 있어
/// 여기서 그대로 쓸 수 있습니다.
///
/// 저장 위치:
/// Application.persistentDataPath에 JSON으로 남깁니다. 에디터와 빌드가
/// 각각 다른 경로를 쓰므로, 개발 중 저장이 빌드 실행에 섞이지 않습니다.
/// </summary>
public class Campaign_Manager : MonoBehaviour
{
    /// <summary>전투 결과를 읽어 올 배틀 매니저입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Battle_Manager battle_Manager;

    /// <summary>저장 파일 이름입니다. persistentDataPath 아래에 만들어집니다.</summary>
    [Header("저장")]
    [Tooltip("저장 파일 이름입니다.")]
    public string saveFileName = "campaign.json";

    /// <summary>전투가 끝나면 자동으로 저장할지 여부입니다.</summary>
    [Tooltip("전투가 끝나면 자동으로 저장합니다.")]
    public bool bautoSave = true;

    /// <summary>씬 시작 시 저장된 캠페인을 불러올지 여부입니다.</summary>
    [Tooltip("씬 시작 시 저장된 캠페인을 불러옵니다.")]
    public bool bautoLoad = true;

    /// <summary>저장이 없을 때 새로 편성할 부대 수입니다.</summary>
    [Header("신규 캠페인")]
    [Tooltip("저장이 없을 때 편성할 부대 수입니다.")]
    public int startingArmies = 6;

    /// <summary>새로 편성하는 부대 하나의 정원입니다.</summary>
    [Tooltip("부대 하나의 정원입니다.")]
    public int startingStrength = 160;

    /// <summary>현재 캠페인 상태입니다.</summary>
    public Campaign_State state { get; private set; }

    /// <summary>이번 전투의 결과를 이미 반영했는지 여부입니다.</summary>
    private bool bresultApplied;

    /// <summary>저장 파일의 전체 경로입니다.</summary>
    public string SavePath => System.IO.Path.Combine(
        Application.persistentDataPath, saveFileName);

    /// <summary>
    /// 저장된 캠페인을 불러오거나, 없으면 새 캠페인을 시작합니다.
    ///
    /// Start가 아닌 Awake인 이유: 다른 컴포넌트가 Start에서 이 상태를 읽을 수
    /// 있으므로, 그보다 먼저 준비되어야 합니다.
    /// </summary>
    private void Awake()
    {
        if (battle_Manager == null) battle_Manager = FindAnyObjectByType<Battle_Manager>();

        if (bautoLoad && Load()) return;

        Start_New_Campaign();
    }

    /// <summary>
    /// 전투가 끝나는 순간을 감지해 결과를 캠페인에 한 번만 반영합니다.
    /// </summary>
    private void Update()
    {
        // 전투가 끝나면 한 번만 결과를 반영합니다.
        //
        // Battle_Manager.phase가 Finished로 바뀌는 순간을 잡습니다.
        // 매 프레임 검사하지만 bresultApplied가 재진입을 막습니다.
        if (bresultApplied) return;
        if (battle_Manager == null) return;
        if (battle_Manager.phase != E_Battle_Phase.Finished) return;

        Apply_Battle_Result();
    }

    /// <summary>
    /// 새 캠페인을 시작합니다. 저장된 상태를 덮어씁니다.
    /// </summary>
    public void Start_New_Campaign()
    {
        state = new Campaign_State();

        // 병종을 골고루 섞어 편성합니다.
        // 한 병종만 있으면 상성 시스템이 의미를 잃습니다.
        E_Unit_Class[] roster =
        {
            E_Unit_Class.Infantry,
            E_Unit_Class.Spear,
            E_Unit_Class.Archer,
            E_Unit_Class.Cavalry
        };

        for (int i = 0; i < startingArmies; i++)
        {
            E_Unit_Class unitClass = roster[i % roster.Length];
            bool bgeneral = i == 0;

            string name = bgeneral
                ? "장군 " + Get_Class_Name(unitClass)
                : Get_Class_Name(unitClass) + " " + (i + 1);

            state.Recruit(name, unitClass, startingStrength, bgeneral);
        }

        Debug.Log($"[Campaign] 새 캠페인 시작: 부대 {state.armies.Count}개, " +
                  $"병력 {state.Get_Total_Strength()}명");
    }

    /// <summary>
    /// 전투 결과를 캠페인 상태에 반영합니다.
    ///
    /// 부대 대응은 '순서'로 맞춥니다.
    /// 보고서는 아군 먼저 정렬되어 있고, 씬의 부대도 캠페인 순서대로
    /// 생성되는 것이 전제입니다. 실제 캠페인 씬을 붙일 때는 Army에
    /// campaignId를 실어 보내 확실하게 대응시키는 편이 안전합니다.
    /// </summary>
    private void Apply_Battle_Result()
    {
        bresultApplied = true;

        IReadOnlyList<Battle_Report_Entry> entries = battle_Manager.ReportEntries;
        if (entries == null) return;

        state.battlesFought++;

        if (Is_Victory(battle_Manager.result)) state.battlesWon++;

        // 아군 항목만 골라 순서대로 대응시킵니다.
        //
        // 보고서는 '아군 먼저, 그 안에서 전과순'으로 정렬되어 있습니다.
        // 따라서 아군만 걸러 내면 순서가 캠페인 목록과 맞아떨어집니다.
        //
        // 한계: 이 대응은 씬의 부대가 캠페인 순서대로 생성된다는 전제에
        // 기대고 있습니다. 실제 캠페인 씬을 붙일 때는 Army에 campaignId를
        // 실어 보내 확실하게 짝지어야 합니다.
        int index = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            Battle_Report_Entry entry = entries[i];
            if (!entry.bplayer) continue;

            if (index >= state.armies.Count) break;

            Campaign_Army army = state.armies[index];
            index++;

            if (army == null) continue;

            army.Apply_Battle_Result(entry);
        }

        // 살아남은 부대에 보충을 넣습니다.
        // 전멸/와해 판정은 Apply_Battle_Result가 이미 끝냈으므로,
        // 여기서는 그 부대들이 자동으로 제외됩니다.
        state.Reinforce_All();

        Debug.Log($"[Campaign] 전투 {state.battlesFought}회차 반영: " +
                  $"{battle_Manager.result}, " +
                  $"생존 부대 {state.Get_Alive_Count()}개, " +
                  $"병력 {state.Get_Total_Strength()}명");

        if (bautoSave) Save();
    }

    /// <summary>결과 등급이 승리에 해당하는지 판정합니다.</summary>
    /// <param name="result">판정할 전투 결과 등급입니다.</param>
    /// <returns>승리(압승/승리/신승)이면 true입니다.</returns>
    private static bool Is_Victory(E_Battle_Result result)
    {
        return result == E_Battle_Result.CrushingVictory
            || result == E_Battle_Result.ClearVictory
            || result == E_Battle_Result.CloseVictory;
    }

    /// <summary>캠페인 상태를 파일로 저장합니다.</summary>
    public void Save()
    {
        if (state == null) return;

        try
        {
            string json = JsonUtility.ToJson(state, true);
            System.IO.File.WriteAllText(SavePath, json);

            Debug.Log($"[Campaign] 저장: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Campaign] 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 저장된 캠페인을 불러옵니다.
    /// </summary>
    /// <returns>불러오기에 성공했으면 true입니다.</returns>
    public bool Load()
    {
        if (!System.IO.File.Exists(SavePath)) return false;

        try
        {
            string json = System.IO.File.ReadAllText(SavePath);
            Campaign_State loaded = JsonUtility.FromJson<Campaign_State>(json);

            if (loaded == null || loaded.armies == null) return false;

            state = loaded;

            Debug.Log($"[Campaign] 불러오기: 전투 {state.battlesFought}회, " +
                      $"부대 {state.Get_Alive_Count()}개, " +
                      $"병력 {state.Get_Total_Strength()}명");

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Campaign] 불러오기 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>저장 파일을 지웁니다.</summary>
    public void Delete_Save()
    {
        if (!System.IO.File.Exists(SavePath)) return;

        try
        {
            System.IO.File.Delete(SavePath);
            Debug.Log($"[Campaign] 저장 삭제: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Campaign] 저장 삭제 실패: {e.Message}");
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
}
