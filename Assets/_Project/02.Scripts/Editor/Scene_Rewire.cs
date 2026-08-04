using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 삭제된 DCSS 시트를 참조하던 스프라이트들을 새 타일로 다시 배선합니다.
///
/// 왜 필요한가:
/// 기존 프로젝트는 합쳐진 시트(main.png, player.png, icons.png)를 잘라
/// 쓰고 있었습니다. 그 파일들이 삭제되면서 Scene1의 참조 5,195개가
/// 통째로 끊겼습니다. Unity는 이런 경우 조용히 빈 스프라이트를 쓰므로,
/// 실행하면 병사가 아예 보이지 않거나 흰 사각형으로 나옵니다.
///
/// 무엇을 다시 잇는가:
///   1) 부대의 몸통/무기/방패 목록 (Army.images_*)
///   2) 유닛 발밑 선택 링       (Unit_UI)
///   3) 진형 배치 마커           (Formation_UI 프리팹)
///
/// 2번과 3번은 원래 DCSS 시트의 '캐릭터 그림 조각'을 잘라 쓰고 있었습니다.
/// 마커로 만들어진 자산이 아니라 눈에 덜 거슬리는 조각을 고른 것이었고,
/// crawl 저장소에도 이 용도의 자산이 없습니다. (DCSS는 커서를 코드로 그립니다)
/// 그래서 Marker_Sprite_Builder가 만든 전용 마커로 바꿉니다.
/// </summary>
public static class Scene_Rewire
{
    /// <summary>새 타일이 있는 폴더입니다.</summary>
    private const string tileFolder = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>생성한 마커가 있는 폴더입니다.</summary>
    private const string markerFolder = "Assets/_Project/04.Art/01.Images/Markers";

    /// <summary>
    /// 현재 열린 씬의 부대와 마커를 새 자산으로 다시 배선합니다.
    /// </summary>
    [MenuItem("RTXX/씬 스프라이트 재배선")]
    public static void Rewire()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        // --- 1. 마커 프리팹 ---
        //
        // 프리팹을 먼저 고칩니다. 씬의 유닛은 프리팹 인스턴스이므로,
        // 프리팹을 고치면 씬의 모든 인스턴스가 함께 따라옵니다.
        Rewire_Marker("Assets/_Project/05.Prefabs/Character/Unit.prefab",
                      "Unit_UI", "marker_select");

        Rewire_Marker("Assets/_Project/05.Prefabs/UI/Formation_UI.prefab",
                      null, "marker_slot");

        // --- 2. 부대 스프라이트 목록 ---
        List<Sprite> humans = Load_Tiles("human_");
        List<Sprite> orcs = Load_Tiles("orc_");
        List<Sprite> weapons = Load_Tiles("weapon_");
        List<Sprite> shields = Load_Tiles("shield_");

        if (humans.Count == 0 || orcs.Count == 0)
        {
            Debug.LogError(
                $"[Rewire] {tileFolder}에 타일이 없습니다.\n" +
                "먼저 'RTXX/DCSS 타일 전체 가져오기'를 실행하십시오.");
            return;
        }

        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsSortMode.None);

        int rewired = 0;

        for (int i = 0; i < armies.Length; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            // 진영으로 몸통을 가릅니다.
            // 아군은 인간, 적군은 오크로 두면 난전에서 피아 식별이 됩니다.
            army.images_Unit = new List<Sprite>(
                army.army_Data.bplayer ? humans : orcs);

            army.images_Weapon = new List<Sprite>(weapons);

            // 궁병은 방패를 들지 않습니다. 활을 두 손으로 잡기 때문입니다.
            army.images_Shield = army.army_Data.GetE_Unit_Class() == E_Unit_Class.Archer
                ? new List<Sprite>()
                : new List<Sprite>(shields);

            EditorUtility.SetDirty(army);
            rewired++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            $"[Rewire] '{scene.name}' 재배선 완료.\n" +
            $"  부대 {rewired}개\n" +
            $"  인간 {humans.Count} / 오크 {orcs.Count} / " +
            $"무기 {weapons.Count} / 방패 {shields.Count}");
    }

    /// <summary>
    /// 프리팹의 SpriteRenderer를 지정한 마커로 바꿉니다.
    /// </summary>
    /// <param name="prefabPath">고칠 프리팹 경로입니다.</param>
    /// <param name="childName">대상 자식 이름입니다. null이면 루트를 봅니다.</param>
    /// <param name="markerName">쓸 마커 스프라이트 이름입니다.</param>
    private static void Rewire_Marker(string prefabPath, string childName, string markerName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogWarning($"[Rewire] 프리팹을 찾지 못했습니다: {prefabPath}");
            return;
        }

        Sprite marker = Find_Sprite(markerFolder, markerName);

        if (marker == null)
        {
            Debug.LogWarning(
                $"[Rewire] 마커를 찾지 못했습니다: {markerName}\n" +
                "먼저 'RTXX/발밑 마커 생성'을 실행하십시오.");
            return;
        }

        // 프리팹을 열어 수정합니다.
        //
        // LoadAssetAtPath로 얻은 것을 직접 고치면 저장되지 않습니다.
        // LoadPrefabContents로 사본을 열고 저장한 뒤 닫아야 합니다.
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Transform target = childName == null
                ? root.transform
                : Find_Child(root.transform, childName);

            if (target == null)
            {
                Debug.LogWarning($"[Rewire] '{childName}' 자식을 찾지 못했습니다: {prefabPath}");
                return;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();

            if (renderer == null)
            {
                Debug.LogWarning($"[Rewire] SpriteRenderer가 없습니다: {prefabPath}/{childName}");
                return;
            }

            renderer.sprite = marker;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"[Rewire] {System.IO.Path.GetFileName(prefabPath)} " +
                      $"-> {markerName} 적용");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>이름이 일치하는 자식을 재귀로 찾습니다.</summary>
    /// <param name="parent">탐색을 시작할 부모입니다.</param>
    /// <param name="name">찾을 자식 이름입니다.</param>
    /// <returns>찾은 Transform이며, 없으면 null입니다.</returns>
    private static Transform Find_Child(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = Find_Child(parent.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>접두사로 시작하는 스프라이트를 모아 이름순으로 돌려줍니다.</summary>
    /// <param name="prefix">파일명 접두사입니다.</param>
    /// <returns>찾은 스프라이트 목록입니다.</returns>
    private static List<Sprite> Load_Tiles(string prefix)
    {
        List<Sprite> result = new List<Sprite>();

        if (!AssetDatabase.IsValidFolder(tileFolder)) return result;

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { tileFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!System.IO.Path.GetFileName(path).StartsWith(prefix)) continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) result.Add(sprite);
        }

        // 정렬해 두어야 씬을 다시 배선할 때 같은 순서가 나옵니다.
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        return result;
    }

    /// <summary>폴더에서 이름이 정확히 일치하는 스프라이트를 찾습니다.</summary>
    /// <param name="folder">찾을 폴더입니다.</param>
    /// <param name="name">스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    private static Sprite Find_Sprite(string folder, string name)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;

        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { folder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    /// <summary>배치모드에서 재배선하고 종료합니다.</summary>
    public static void Rewire_From_CLI()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        string scenePath = "Assets/_Project/01.Scenes/Scene1.unity";

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-rewireScene") scenePath = args[i + 1];
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Rewire();

        EditorApplication.Exit(0);
    }
}
