using UnityEditor;
using UnityEngine;

/// <summary>
/// 스프라이트의 실제 사각형과 피벗을 읽어 보고하는 도구입니다.
///
/// 왜 필요한가:
/// 2px 정도의 오차가 남았는데, 그 크기는 타일 보정값과 같은 규모라
/// 원인을 값으로 특정해야 합니다.
///
/// 확인해야 할 것은 '스프라이트의 원점이 어디인가'입니다.
/// 피벗을 하단 중앙으로 잡아 두었어도, 스프라이트의 rect가 원본
/// 32x32가 아니면(트리밍이 걸렸다면) 원점이 그림마다 달라집니다.
///
///   rect          아틀라스/텍스처 안에서 이 스프라이트가 차지하는 영역
///   pivot         rect 안에서의 원점 (픽셀)
///   bounds        월드 단위로 그려지는 범위
///
/// rect.height가 32가 아니면 트리밍된 것이고, 그만큼 발밑이 뜹니다.
/// </summary>
public static class Sprite_Rect_Probe
{
    /// <summary>타일 폴더입니다.</summary>
    private const string folder = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>주요 타일의 사각형과 피벗을 보고합니다.</summary>
    [MenuItem("RTXX/검증: 스프라이트 사각형·피벗")]
    public static void Report()
    {
        string[] names =
        {
            "human_human", "orc_orc", "orc_orc_warlord",
            "weapon_long_sword_slant", "weapon_bow", "weapon_dagger", "weapon_spear",
            "shield_buckler_round", "shield_kite_shield_kite1",
        };

        Debug.Log("[사각형] 스프라이트별 rect / pivot / bounds\n" +
                  "  rect.height가 32가 아니면 트리밍된 것입니다.");

        for (int i = 0; i < names.Length; i++)
        {
            Sprite s = Load(names[i]);

            if (s == null)
            {
                Debug.LogWarning($"  {names[i],-28} 찾지 못함");
                continue;
            }

            Rect r = s.rect;
            Vector2 p = s.pivot;

            // 피벗을 rect 기준 비율로도 보여 줍니다.
            // 하단 중앙이면 (0.5, 0)이어야 합니다.
            float px = r.width > 0.0f ? p.x / r.width : 0.0f;
            float py = r.height > 0.0f ? p.y / r.height : 0.0f;

            // 원본 32x32 기준으로 발밑이 얼마나 떠 있는지입니다.
            // 트리밍이 없으면 0이어야 합니다.
            float bottomGap = 32.0f - r.height - Offset_From_Texture(s);

            Debug.Log(
                $"  {names[i],-28}\n" +
                $"      rect   = ({r.x,4},{r.y,4})  {r.width}x{r.height}\n" +
                $"      pivot  = ({p.x:F1},{p.y:F1}) px  ->  비율 ({px:F2},{py:F2})\n" +
                $"      bounds = {s.bounds.center} size {s.bounds.size}\n" +
                $"      PPU    = {s.pixelsPerUnit}");
        }
    }

    /// <summary>텍스처 안에서 rect의 아래쪽 여백을 구합니다.</summary>
    /// <param name="s">조사할 스프라이트입니다.</param>
    /// <returns>텍스처 하단으로부터의 거리(px)입니다.</returns>
    private static float Offset_From_Texture(Sprite s)
    {
        return s.rect.y;
    }

    /// <summary>이름으로 스프라이트를 불러옵니다.</summary>
    /// <param name="name">스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    private static Sprite Load(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { folder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    /// <summary>배치모드에서 보고하고 종료합니다.</summary>
    public static void Report_From_CLI()
    {
        Report();
        EditorApplication.Exit(0);
    }
}
