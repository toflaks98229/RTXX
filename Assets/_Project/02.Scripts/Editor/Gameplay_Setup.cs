using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전투 진행 계층을 씬에 배선하는 도구입니다.
///
/// 왜 필요한가:
/// 이 프로젝트에는 전투의 시작과 끝을 관리하는 계층이 이미 구현되어
/// 있는데, 어느 씬에도 붙어 있지 않았습니다. 조사 결과:
///
///   Battle_Manager   : Scene1 0개, Scene_MassBattle 0개
///   Campaign_Manager : 양쪽 모두 0개
///   UI_Army_Banner   : 양쪽 모두 0개
///
/// 즉 배치 단계도, 승패 판정도, 전과 보고서도, 캠페인 지속성도
/// 코드로만 존재하고 실제로는 한 번도 동작한 적이 없습니다.
/// 전투는 '시작도 끝도 없이' 그냥 돌아가고 있었습니다.
///
/// 이 도구는 그 계층들을 씬에 세우고 서로 연결합니다.
/// 이미 있으면 건드리지 않으므로 여러 번 실행해도 안전합니다.
/// </summary>
public static class Gameplay_Setup
{
    /// <summary>플레이용 씬입니다.</summary>
    private const string playScenePath = "Assets/_Project/01.Scenes/Scene1.unity";

    /// <summary>
    /// 현재 열린 씬에 전투 진행 계층을 배선합니다.
    ///
    /// 붙이는 것:
    ///   Battle_Manager   배치 -> 교전 -> 종료 단계 관리, 승패 판정
    ///   Campaign_Manager 전투 결과를 다음 전투로 이어받기
    ///   UI_Army_Banner   부대 위 사기/상태 배너
    ///
    /// UI_Command_Bar는 이미 두 씬에 있으므로 참조만 채웁니다.
    /// </summary>
    [MenuItem("RTXX/전투 진행 계층 배선")]
    public static void Setup()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        Controller controller = Object.FindAnyObjectByType<Controller>();

        if (controller == null)
        {
            Debug.LogError("[Setup] 씬에서 Controller를 찾지 못했습니다. " +
                           "전투 씬을 연 뒤 실행하십시오.");
            return;
        }

        // 관리 계층을 한 오브젝트에 모읍니다.
        //
        // 흩어 두면 하이어라키에서 찾기 어렵고, 씬을 옮길 때
        // 일부만 빠뜨리기 쉽습니다.
        GameObject host = GameObject.Find("Gameplay");

        if (host == null)
        {
            host = new GameObject("Gameplay");
            Undo.RegisterCreatedObjectUndo(host, "Create Gameplay host");
        }

        Battle_Manager battle = Ensure<Battle_Manager>(host);
        Campaign_Manager campaign = Ensure<Campaign_Manager>(host);

        // --- 서로 연결 ---
        battle.controller = controller;
        campaign.battle_Manager = battle;

        // Controller가 배치 단계를 보게 합니다.
        //
        // 이 참조가 없으면 Controller.FixedUpdate가 단계를 확인하지 못해
        // 배치 중에도 시뮬레이션이 돌아 부대가 제멋대로 움직입니다.
        controller.battle_Manager = battle;

        // HUD가 전투 단계와 결과를 읽게 합니다.
        UI_Command_Bar bar = Object.FindAnyObjectByType<UI_Command_Bar>();

        if (bar != null)
        {
            bar.controller = controller;
            bar.battle_Manager = battle;
            EditorUtility.SetDirty(bar);
        }

        // 적군 AI를 세웁니다.
        //
        // 왜 필요한가:
        // Scene1에는 부대 12개(아군 6, 적군 6)가 있는데 Battle_AI가
        // 하나도 없었습니다. 즉 적군이 명령을 받지 못해 제자리에 서 있고,
        // 플레이어가 다가가 때릴 때까지 아무 일도 일어나지 않습니다.
        //
        // 아군은 사람이 지휘하므로 AI를 붙이지 않습니다.
        // (bcontrolPlayerSide = false가 적군을 뜻합니다)
        Ensure_Enemy_AI(host, controller, battle);

        // 부대 배너를 붙입니다.
        //
        // 사기가 이 게임의 승부를 가르는데, 그 값이 화면에 나오지 않으면
        // 플레이어는 부대가 왜 무너지는지 알 수 없습니다.
        UI_Army_Banner banner = Object.FindAnyObjectByType<UI_Army_Banner>();

        if (banner == null)
        {
            banner = host.AddComponent<UI_Army_Banner>();
        }

        banner.controller = controller;
        EditorUtility.SetDirty(banner);

        // 전투 HUD를 붙입니다.
        //
        // 왜 필요한가:
        // 카드 바와 배너는 '지금 보이는 것'만 알려 줍니다. 그런데 이 게임은
        // 측후방 공격이 방어를 무력화하므로, 우회는 화면 밖에서 일어납니다.
        // 미니맵이 없으면 그 전술이 플레이어가 당하는 쪽으로만 작동합니다.
        //
        // 네 가지가 각각 메우는 구멍:
        //   UI_Minimap         화면 밖 전황을 볼 수단
        //   UI_Command_Preview 드래그가 어떤 진형을 뜻하는지
        //   UI_Command_Panel   태세가 존재한다는 사실 자체
        //   UI_Army_Group      화면 밖 부대를 다시 지목할 수단
        Ensure_HUD(host, controller);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(battle);
        EditorUtility.SetDirty(campaign);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            $"[Setup] '{scene.name}'에 전투 진행 계층을 배선했습니다.\n" +
            $"  Battle_Manager   : 배치({battle.keyCode_Start_Battle} 키로 시작) -> 교전 -> 종료\n" +
            $"  Campaign_Manager : 저장 위치 {campaign.SavePath}\n" +
            $"  UI_Army_Banner   : 부대 위 사기/상태 표시\n" +
            $"  전투 HUD         : 미니맵 / 명령 미리보기 / 태세 패널 / 부대 그룹(F1~F8)");
    }

    /// <summary>플레이용 씬(Scene1)을 열어 배선합니다.</summary>
    [MenuItem("RTXX/전투 진행 계층 배선 (Scene1)")]
    public static void Setup_Play_Scene()
    {
        EditorSceneManager.OpenScene(playScenePath, OpenSceneMode.Single);
        Setup();
    }

    /// <summary>
    /// 적군을 지휘할 AI를 확보합니다. 이미 있으면 참조만 채웁니다.
    ///
    /// 아군용 AI는 만들지 않습니다. 아군은 플레이어가 지휘하는 것이
    /// 이 게임의 전제이며, 둘 다 AI가 잡으면 플레이어가 할 일이 없습니다.
    /// (대규모 검증 씬에서는 사람 없이 돌려야 하므로 예외적으로 양쪽에 붙입니다)
    /// </summary>
    /// <param name="host">AI를 붙일 오브젝트입니다.</param>
    /// <param name="controller">부대 목록을 읽어 올 컨트롤러입니다.</param>
    /// <param name="battle">전투 단계를 볼 배틀 매니저입니다.</param>
    private static void Ensure_Enemy_AI(GameObject host, Controller controller,
                                        Battle_Manager battle)
    {
        Battle_AI[] existing = Object.FindObjectsByType<Battle_AI>(FindObjectsSortMode.None);

        Battle_AI enemyAI = null;

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null) continue;

            // 적군 담당 AI를 찾습니다.
            if (!existing[i].bcontrolPlayerSide) { enemyAI = existing[i]; break; }
        }

        if (enemyAI == null)
        {
            GameObject go = new GameObject("Battle_AI_Enemy");
            go.transform.SetParent(host.transform, false);

            enemyAI = go.AddComponent<Battle_AI>();
            enemyAI.bcontrolPlayerSide = false;
        }

        enemyAI.controller = controller;
        enemyAI.battle_Manager = battle;

        EditorUtility.SetDirty(enemyAI);
    }

    /// <summary>
    /// 전투 HUD 네 가지를 확보하고 컨트롤러 참조를 채웁니다.
    ///
    /// 넷 다 controller 하나만 있으면 동작하므로 개별 배선이 필요 없습니다.
    /// 나머지 값(패널 위치, 단축키)은 기본값이 곧 의도한 배치이며,
    /// 바꾸고 싶으면 인스펙터에서 조정합니다.
    ///
    /// 여기서 참조를 미리 채우는 이유:
    /// 각 컴포넌트의 Start가 FindAnyObjectByType으로 스스로 찾기는 하지만,
    /// 씬에 저장해 두면 그 탐색이 아예 일어나지 않고 배선이 눈에 보입니다.
    /// </summary>
    /// <param name="host">HUD를 붙일 오브젝트입니다.</param>
    /// <param name="controller">HUD가 읽어 갈 컨트롤러입니다.</param>
    private static void Ensure_HUD(GameObject host, Controller controller)
    {
        UI_Minimap minimap = Ensure<UI_Minimap>(host);
        UI_Command_Preview preview = Ensure<UI_Command_Preview>(host);
        UI_Command_Panel panel = Ensure<UI_Command_Panel>(host);
        UI_Army_Group group = Ensure<UI_Army_Group>(host);

        minimap.controller = controller;
        preview.controller = controller;
        panel.controller = controller;
        group.controller = controller;

        EditorUtility.SetDirty(minimap);
        EditorUtility.SetDirty(preview);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(group);
    }

    /// <summary>
    /// 컴포넌트가 없으면 붙이고, 있으면 그대로 돌려줍니다.
    /// </summary>
    /// <typeparam name="T">확보할 컴포넌트 형식입니다.</typeparam>
    /// <param name="host">컴포넌트를 붙일 오브젝트입니다.</param>
    /// <returns>씬에 존재하는 컴포넌트입니다.</returns>
    private static T Ensure<T>(GameObject host) where T : Component
    {
        T found = Object.FindAnyObjectByType<T>();
        if (found != null) return found;

        return host.AddComponent<T>();
    }

    /// <summary>배치모드에서 배선하고 종료합니다.</summary>
    public static void Setup_From_CLI()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string scenePath = playScenePath;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-setupScene") scenePath = args[i + 1];
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Setup();

        EditorApplication.Exit(0);
    }
}
