using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// HUD가 쓰는 아이콘을 모아 UI_Icon_Set에 채워 넣는 도구입니다.
///
/// 왜 자동화하는가:
/// 아이콘 이름은 UI_Icons의 대응표가 정합니다. 인스펙터에서 손으로
/// 넣으면 대응표를 고칠 때마다 씬도 함께 고쳐야 하고, 빠뜨리면
/// 런타임에 아이콘이 안 나옵니다. 대응표를 읽어 자동으로 채우면
/// 그 어긋남이 생기지 않습니다.
/// </summary>
public static class UI_Icon_Setup
{
    /// <summary>아이콘을 찾을 폴더입니다.</summary>
    private const string iconFolder = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>
    /// 대응표에 적힌 아이콘을 모아 씬의 UI_Icon_Set에 채웁니다.
    /// 컴포넌트가 없으면 Gameplay 오브젝트에 붙입니다.
    /// </summary>
    [MenuItem("RTXX/HUD 아이콘 채우기")]
    public static void Populate()
    {
        // 대응표가 실제로 쓰는 이름을 전부 모읍니다.
        //
        // 하드코딩하지 않고 열거형을 돌며 물어보는 이유:
        // 병종이나 태세가 추가되면 대응표만 고치면 되고,
        // 이 도구는 자동으로 그 새 아이콘까지 가져옵니다.
        HashSet<string> names = new HashSet<string>();

        foreach (E_Unit_Class c in System.Enum.GetValues(typeof(E_Unit_Class)))
        {
            names.Add(UI_Icons.Get_Class_Icon(c));
        }

        foreach (E_Army_Stance s in System.Enum.GetValues(typeof(E_Army_Stance)))
        {
            string icon = UI_Icons.Get_Stance_Icon(s);
            if (!string.IsNullOrEmpty(icon)) names.Add(icon);
        }

        // 이름으로 실제 스프라이트를 찾습니다.
        List<Sprite> found = new List<Sprite>();
        List<string> missing = new List<string>();

        foreach (string name in names)
        {
            Sprite sprite = Find_Sprite(name);

            if (sprite != null) found.Add(sprite);
            else missing.Add(name);
        }

        // 씬의 컴포넌트에 채웁니다.
        UI_Icon_Set set = Object.FindAnyObjectByType<UI_Icon_Set>();

        if (set == null)
        {
            GameObject host = GameObject.Find("Gameplay");
            if (host == null) host = new GameObject("Gameplay");

            set = host.AddComponent<UI_Icon_Set>();
        }

        set.icons = found;
        EditorUtility.SetDirty(set);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[UI_Icons] 아이콘 {found.Count}개를 채웠습니다." +
                  (missing.Count > 0
                      ? $"\n찾지 못함 {missing.Count}개: {string.Join(", ", missing)}"
                      : ""));
    }

    /// <summary>이름이 정확히 일치하는 스프라이트를 찾습니다.</summary>
    /// <param name="name">찾을 스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    private static Sprite Find_Sprite(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { iconFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            // FindAssets는 부분 일치도 돌려주므로 정확한 이름만 받습니다.
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    /// <summary>배치모드에서 아이콘을 채우고 종료합니다.</summary>
    public static void Populate_From_CLI()
    {
        EditorSceneManager.OpenScene("Assets/_Project/01.Scenes/Scene1.unity", OpenSceneMode.Single);
        Populate();
        EditorApplication.Exit(0);
    }
}
