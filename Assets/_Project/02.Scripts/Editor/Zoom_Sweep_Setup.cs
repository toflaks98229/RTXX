using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 대규모 전투 씬에 줌 스윕 프로브를 붙여 주는 에디터 도구입니다.
///
/// 왜 별도 도구인가:
/// Mass_Battle_Builder는 씬을 통째로 다시 만듭니다. 지금 필요한 것은
/// '이미 있는 씬에 컴포넌트 하나 추가'뿐이므로, 부대 배치를 새로 뽑아
/// 측정 조건을 바꿔 버리는 것은 오히려 해롭습니다.
///
/// 프로브는 -zoomSweep 인자가 있을 때만 동작하므로, 붙여 두어도
/// 평소 실행에는 아무 영향이 없습니다.
/// </summary>
public static class Zoom_Sweep_Setup
{
    // 비공개 멤버 변수
    /// <summary>프로브를 붙일 대상 씬의 경로입니다.</summary>
    private const string scenePath = "Assets/_Project/01.Scenes/Scene_MassBattle.unity";

    // 공개 메서드
    /// <summary>
    /// 대상 씬을 열고 줌 스윕 프로브를 붙인 뒤 저장합니다.
    ///
    /// 이미 붙어 있으면 아무 일도 하지 않습니다. 중복으로 붙이면
    /// 두 프로브가 같은 카메라를 서로 다른 줌으로 밀어 측정이 무의미해집니다.
    /// </summary>
    [MenuItem("RTXX/줌 스윕 프로브 붙이기")]
    public static void Attach()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        if (scene.path != scenePath)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        if (Object.FindAnyObjectByType<Zoom_Sweep_Probe>() != null)
        {
            Debug.Log("[ZoomSweep] 이미 붙어 있습니다.");
            return;
        }

        GameObject go = new GameObject("Zoom_Sweep_Probe");
        go.AddComponent<Zoom_Sweep_Probe>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log("[ZoomSweep] 프로브를 붙이고 씬을 저장했습니다.");
    }

    /// <summary>배치모드에서 호출하는 진입점입니다.</summary>
    public static void Attach_From_CLI()
    {
        Attach();
        EditorApplication.Exit(0);
    }
}
