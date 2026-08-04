using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 스프라이트 아틀라스가 실제로 어떻게 묶였는지 보고하는 검사 도구입니다.
///
/// 왜 필요한가:
/// 아틀라스 설정을 바꿔도 '실제로 몇 장으로 묶였는지'는 에셋 파일만
/// 봐서는 알 수 없습니다. Unity가 패킹한 결과를 직접 물어봐야 합니다.
///
/// 이것이 드로우콜과 직결됩니다.
///   페이지 1장 -> 같은 텍스처이므로 한 번의 드로우콜로 묶임
///   페이지 2장 -> 텍스처가 갈리므로 최소 두 번
///
/// 즉 이 보고서의 '페이지 수'가 곧 스프라이트 렌더링의 드로우콜 하한입니다.
/// </summary>
public static class DCSS_Atlas_Report
{
    /// <summary>검사할 아틀라스 경로입니다.</summary>
    private const string atlasPath = "Assets/_Project/04.Art/01.Images/DCSS_Tiles.spriteatlas";

    /// <summary>
    /// 아틀라스를 강제로 패킹한 뒤 결과를 보고합니다.
    ///
    /// 패킹을 먼저 하는 이유: 에디터는 필요할 때까지 패킹을 미룹니다.
    /// 그 전에 물어보면 페이지가 0장으로 나와 잘못 판단하게 됩니다.
    /// </summary>
    [MenuItem("RTXX/DCSS 아틀라스 보고서")]
    public static void Report()
    {
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

        if (atlas == null)
        {
            Debug.LogError($"[DCSS] 아틀라스를 찾지 못했습니다: {atlasPath}");
            return;
        }

        // 현재 빌드 타깃 기준으로 패킹합니다.
        SpriteAtlasUtility.PackAtlases(
            new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

        StringBuilder sb = new StringBuilder(512);

        sb.AppendLine("========== DCSS 아틀라스 보고서 ==========");
        sb.AppendLine($"경로       : {atlasPath}");
        sb.AppendLine($"등록 스프라이트: {atlas.spriteCount}장");

        // --- 패킹 설정 ---
        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();

        sb.AppendLine("--- 패킹 설정 ---");
        sb.AppendLine($"회전 패킹   : {(packing.enableRotation ? "켬 (배칭 위험)" : "끔")}");
        sb.AppendLine($"타이트 패킹 : {(packing.enableTightPacking ? "켬 (메시 복잡)" : "끔")}");
        sb.AppendLine($"여백        : {packing.padding} px");

        // --- 텍스처 설정 ---
        SpriteAtlasTextureSettings texture = atlas.GetTextureSettings();
        TextureImporterPlatformSettings platform =
            atlas.GetPlatformSettings("DefaultTexturePlatform");

        sb.AppendLine("--- 텍스처 설정 ---");
        sb.AppendLine($"최대 크기   : {platform.maxTextureSize}");
        sb.AppendLine($"압축        : {platform.textureCompression}");
        sb.AppendLine($"필터        : {texture.filterMode}");
        sb.AppendLine($"밉맵        : {(texture.generateMipMaps ? "켬" : "끔")}");

        // --- 실제 패킹 결과 ---
        //
        // 이 부분이 핵심입니다. 페이지 수가 곧 드로우콜 하한입니다.
        Texture2D[] pages = GetPreviewTextures(atlas);

        sb.AppendLine("--- 패킹 결과 ---");

        if (pages == null || pages.Length == 0)
        {
            sb.AppendLine("페이지     : 확인 불가 (패킹이 아직 끝나지 않았을 수 있습니다)");
        }
        else
        {
            sb.AppendLine($"페이지     : {pages.Length}장  <- 스프라이트 드로우콜 하한");

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;

                sb.AppendLine($"  [{i}] {pages[i].width}x{pages[i].height} " +
                              $"({pages[i].format})");
            }

            if (pages.Length > 1)
            {
                sb.AppendLine();
                sb.AppendLine("경고: 페이지가 둘 이상입니다. 최대 크기를 올리면");
                sb.AppendLine("      한 장으로 합쳐져 드로우콜이 줄어듭니다.");
            }
        }

        sb.AppendLine("==========================================");

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 아틀라스가 실제로 만들어 낸 텍스처 페이지들을 가져옵니다.
    ///
    /// SpriteAtlasExtensions.GetPreviewTextures는 에디터 전용 내부 API라
    /// 리플렉션으로 호출합니다. 공개 API에는 페이지 수를 묻는 방법이 없습니다.
    /// Unity 버전이 바뀌어 이름이 달라지면 null을 돌려주고 넘어갑니다.
    /// </summary>
    /// <param name="atlas">검사할 아틀라스입니다.</param>
    /// <returns>패킹된 텍스처 페이지 배열이며, 확인할 수 없으면 null입니다.</returns>
    private static Texture2D[] GetPreviewTextures(SpriteAtlas atlas)
    {
        try
        {
            System.Type type = typeof(SpriteAtlasExtensions);

            System.Reflection.MethodInfo method = type.GetMethod(
                "GetPreviewTextures",
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);

            if (method == null) return null;

            return method.Invoke(null, new object[] { atlas }) as Texture2D[];
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DCSS] 페이지 수를 확인하지 못했습니다: {e.Message}");
            return null;
        }
    }

    /// <summary>배치모드에서 보고서를 출력하고 종료합니다.</summary>
    public static void Report_From_CLI()
    {
        Report();
        EditorApplication.Exit(0);
    }
}
