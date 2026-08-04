using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 보정표가 실제로 쓰이는 몸통을 빠짐없이 덮는지 점검하는 도구입니다.
///
/// 왜 필요한가:
/// 보정값은 몸통 스프라이트 이름으로 찾습니다. 그런데 부대에 배선된
/// 몸통이 표에 없으면 조용히 0이 적용되어, 그 병사만 무기가 어긋납니다.
/// 로그 경고는 실행 중에야 뜨므로 미리 잡아 두는 편이 낫습니다.
///
/// 점검 범위는 세 곳입니다.
///   1) 씬의 부대에 배선된 몸통 (실제로 쓰이는 것)
///   2) 타일 폴더의 몸통 후보 (앞으로 쓸 수 있는 것)
///   3) 표에는 있는데 어디에도 없는 항목 (죽은 값)
/// </summary>
public static class Hand_Coverage_Probe
{
    /// <summary>타일 폴더입니다.</summary>
    private const string tileFolder = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>몸통으로 쓰일 수 있는 파일 이름의 접두사입니다.</summary>
    private static readonly string[] bodyPrefixes = { "human_", "orc_", "base_human" };

    /// <summary>보정표 적용 범위를 점검합니다.</summary>
    [MenuItem("RTXX/검증: 손 위치 보정 적용 범위")]
    public static void Verify()
    {
        // --- 1. 씬에서 실제로 쓰이는 몸통 ---
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        HashSet<string> inUse = new HashSet<string>();

        for (int i = 0; i < armies.Length; i++)
        {
            List<Sprite> list = armies[i].images_Unit;
            if (list == null) continue;

            for (int j = 0; j < list.Count; j++)
            {
                if (list[j] != null) inUse.Add(list[j].name);
            }
        }

        int missingInUse = 0;

        Debug.Log($"[범위] 씬의 부대가 쓰는 몸통 {inUse.Count}종");

        foreach (string name in inUse)
        {
            bool bcovered = Unit_Hand_Offset.Has(name);

            if (bcovered)
            {
                Debug.Log($"    [있음] {name}");
            }
            else
            {
                missingInUse++;
                Debug.LogError($"    [없음] {name}  <- 이 병사들은 무기가 어긋납니다");
            }
        }

        // --- 2. 폴더에 있는 몸통 후보 ---
        //
        // 아직 안 쓰더라도 나중에 배선하면 바로 문제가 되므로 함께 봅니다.
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { tileFolder });

        List<string> candidates = new List<string>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (!Is_Body_Candidate(name)) continue;
            if (inUse.Contains(name)) continue;

            candidates.Add(name);
        }

        candidates.Sort();

        int missingCandidate = 0;

        Debug.Log($"[범위] 아직 안 쓰는 몸통 후보 {candidates.Count}종");

        for (int i = 0; i < candidates.Count; i++)
        {
            if (Unit_Hand_Offset.Has(candidates[i])) continue;

            missingCandidate++;
            Debug.LogWarning($"    [미등록] {candidates[i]}");
        }

        // --- 3. 표에만 있고 실물이 없는 항목 ---
        //
        // 파일 이름이 바뀌었는데 표를 안 고쳤을 때 여기서 걸립니다.
        int orphan = 0;

        foreach (string key in Unit_Hand_Offset.Keys)
        {
            if (Load(key) != null) continue;

            orphan++;
            Debug.LogWarning($"    [실물없음] 표에 '{key}'가 있는데 스프라이트를 찾지 못했습니다.");
        }

        Debug.Log(
            "[범위] 요약\n" +
            $"  사용 중 몸통 {inUse.Count}종 / 미등록 {missingInUse}종\n" +
            $"  후보 몸통 {candidates.Count}종 / 미등록 {missingCandidate}종\n" +
            $"  표에만 있는 항목 {orphan}종\n" +
            "  사용 중 미등록이 0이면 지금 화면에는 문제가 없습니다.");
    }

    /// <summary>몸통으로 쓰일 만한 이름인지 확인합니다.</summary>
    /// <param name="name">스프라이트 이름입니다.</param>
    /// <returns>몸통 후보이면 true입니다.</returns>
    private static bool Is_Body_Candidate(string name)
    {
        for (int i = 0; i < bodyPrefixes.Length; i++)
        {
            if (name.StartsWith(bodyPrefixes[i])) return true;
        }

        return false;
    }

    /// <summary>이름으로 스프라이트를 찾습니다.</summary>
    /// <param name="name">스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    private static Sprite Load(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { tileFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    /// <summary>배치모드에서 점검하고 종료합니다.</summary>
    public static void Verify_From_CLI()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene1.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        Verify();
        EditorApplication.Exit(0);
    }
}
