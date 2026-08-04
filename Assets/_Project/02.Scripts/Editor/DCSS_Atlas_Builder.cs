using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// DCSS 타일용 스프라이트 아틀라스를 만들고 설정하는 도구입니다.
///
/// 왜 아틀라스가 필요한가:
/// 스프라이트 하나하나가 별도 텍스처면 Unity는 스프라이트마다 텍스처를
/// 바꿔 가며 그려야 합니다. 텍스처 전환은 드로우콜을 끊으므로,
/// 9,600명이 서로 다른 타일을 쓰면 드로우콜이 그만큼 늘어납니다.
///
/// 아틀라스는 여러 타일을 한 장의 큰 텍스처에 모읍니다. 같은 텍스처를
/// 쓰는 스프라이트끼리는 한 번의 드로우콜로 묶이므로,
/// 이론상 아틀라스 한 장 = 드로우콜 한 번이 됩니다.
///
/// ---------------------------------------------------------------------
/// 픽셀 아트 아틀라스의 함정 — 이 파일이 존재하는 이유
/// ---------------------------------------------------------------------
/// 기존 'DCSS Atlas'는 다음 설정을 쓰고 있었습니다.
///
///   enableRotation: 1      회전 패킹 허용
///   enableTightPacking: 1  외곽선을 따라 빈틈없이 채움
///   padding: 4             타일 사이 여백 4px
///
/// 셋 다 '텍스처 공간을 아끼는' 설정이며, 이 프로젝트에는 전부 해롭습니다.
///
///   1) 회전 패킹: 스프라이트를 90도 돌려 넣습니다. 렌더러가 그릴 때
///      되돌려야 하므로 정점 처리가 달라지고, 배칭이 끊길 수 있습니다.
///
///   2) 타이트 패킹: 사각형이 아니라 실루엣을 따라 채웁니다. 32x32
///      균일 격자에서는 아낄 공간이 거의 없으면서 메시만 복잡해집니다.
///      (4,091장이 32x32라 애초에 빈틈이 없습니다)
///
///   3) 여백 4px: 32px 타일에 4px 여백은 면적의 27%입니다. 밉맵을 끄고
///      Point 필터를 쓰면 이웃 픽셀이 새어 나오지 않으므로 2px면 충분합니다.
///
/// 이 도구는 그 셋을 픽셀 아트에 맞게 되돌립니다.
///
/// 용량 계산 (실측 기준):
///   32x32 타일 4,208장 = 4,308,992 px
///   2048 한 장  = 4,194,304 px  -> 두 장 필요 (드로우콜 2회)
///   4096 한 장  = 16,777,216 px -> 한 장에 수용 (드로우콜 1회)
///
/// 그래서 maxTextureSize를 4096으로 올립니다. 요즘 GPU는 8192까지
/// 문제없이 다루므로 4096은 안전한 값입니다.
/// </summary>
public static class DCSS_Atlas_Builder
{
    /// <summary>타일이 들어 있는 폴더입니다. 이 폴더를 통째로 아틀라스에 담습니다.</summary>
    private const string tileFolder = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>만들 아틀라스 에셋의 경로입니다.</summary>
    private const string atlasPath = "Assets/_Project/04.Art/01.Images/DCSS_Tiles.spriteatlas";

    /// <summary>
    /// 아틀라스 한 장의 최대 크기입니다.
    ///
    /// 4096으로 두면 32x32 타일 16,384장이 한 장에 들어갑니다.
    /// 즉 전체 타일(8,351장)도 한 장에 담기므로 드로우콜이 1회로 수렴합니다.
    /// </summary>
    private const int maxTextureSize = 4096;

    /// <summary>
    /// 타일 사이 여백(픽셀)입니다.
    ///
    /// 밉맵을 끄고 Point 필터를 쓰므로 이웃 픽셀이 번지지 않습니다.
    /// 2px면 부동소수점 UV 오차로 인한 가장자리 새어 나옴만 막으면 충분합니다.
    /// </summary>
    private const int padding = 2;

    /// <summary>
    /// 타일 폴더를 담는 스프라이트 아틀라스를 만들거나 갱신합니다.
    ///
    /// 이미 있으면 설정만 다시 적용합니다. 폴더를 통째로 담으므로
    /// 타일을 추가한 뒤 다시 실행할 필요는 없습니다. (Unity가 폴더를 추적합니다)
    /// </summary>
    [MenuItem("RTXX/DCSS 아틀라스 만들기")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(tileFolder))
        {
            Debug.LogError(
                $"[DCSS] 타일 폴더가 없습니다: {tileFolder}\n" +
                "먼저 'RTXX/DCSS 타일 가져오기'를 실행하십시오.");
            return;
        }

        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

        bool bcreated = atlas == null;

        if (bcreated)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }

        Apply_Settings(atlas);

        // 폴더를 통째로 담습니다.
        //
        // 파일을 하나씩 등록하지 않는 이유: 타일을 추가할 때마다 이 도구를
        // 다시 돌려야 합니다. 폴더로 담으면 Unity가 폴더 내용을 추적하므로
        // 새 타일이 자동으로 포함됩니다.
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(tileFolder);

        if (folder != null)
        {
            // 기존 등록분을 걷어내고 다시 담습니다.
            // 중복 등록하면 같은 스프라이트가 두 번 들어가 경고가 납니다.
            Object[] existing = atlas.GetPackables();
            if (existing != null && existing.Length > 0) atlas.Remove(existing);

            atlas.Add(new[] { folder });
        }

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DCSS] 아틀라스를 {(bcreated ? "만들었습니다" : "갱신했습니다")}: {atlasPath}\n" +
                  $"최대 크기 {maxTextureSize}, 여백 {padding}px, " +
                  "회전/타이트 패킹 끔 (픽셀 아트 배칭 유지)");
    }

    /// <summary>
    /// 아틀라스 설정을 픽셀 아트에 맞게 적용합니다.
    ///
    /// 각 값의 근거는 클래스 요약을 참고하십시오.
    /// </summary>
    /// <param name="atlas">설정을 적용할 아틀라스입니다.</param>
    private static void Apply_Settings(SpriteAtlas atlas)
    {
        // --- 패킹 ---
        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();

        // 회전 패킹을 끕니다. 돌려 넣은 스프라이트는 그릴 때 되돌려야 하므로
        // 정점 처리가 달라지고 배칭이 끊길 수 있습니다.
        packing.enableRotation = false;

        // 타이트 패킹을 끕니다. 32x32 균일 격자에서는 아낄 공간이 없으면서
        // 메시만 복잡해집니다.
        packing.enableTightPacking = false;

        packing.padding = padding;

        atlas.SetPackingSettings(packing);

        // --- 텍스처 ---
        SpriteAtlasTextureSettings texture = atlas.GetTextureSettings();

        // 픽셀 아트이므로 보간을 끕니다. 켜면 32px 타일이 뭉개집니다.
        texture.filterMode = FilterMode.Point;

        // 밉맵을 끕니다.
        //
        // 2D 스프라이트는 축소 렌더링이 없으므로 밉맵이 필요 없고,
        // 켜 두면 메모리가 33% 늘고 축소 시 타일이 흐려집니다.
        texture.generateMipMaps = false;

        // sRGB로 다룹니다. 색 공간이 어긋나면 타일이 어둡거나 밝게 보입니다.
        texture.sRGB = true;

        atlas.SetTextureSettings(texture);

        // --- 플랫폼 ---
        TextureImporterPlatformSettings platform = atlas.GetPlatformSettings("DefaultTexturePlatform");

        platform.maxTextureSize = maxTextureSize;

        // 압축을 끕니다.
        //
        // 32px 타일은 블록 압축(DXT/BC)에서 4x4 블록 단위로 뭉개집니다.
        // 픽셀 하나하나가 그림인 아트에서는 압축 아티팩트가 바로 보입니다.
        // 무압축 4096 아틀라스는 64MB이며, 요즘 환경에서 감당할 수 있습니다.
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.format = TextureImporterFormat.Automatic;
        platform.overridden = true;

        atlas.SetPlatformSettings(platform);

        // 런타임에 스프라이트가 아틀라스를 자동으로 참조하게 합니다.
        //
        // 끄면 코드가 SpriteAtlas.GetSprite()로 직접 꺼내야 하고,
        // 개별 스프라이트를 참조하는 기존 코드가 전부 원본 텍스처를 씁니다.
        // 그러면 아틀라스를 만든 의미가 사라집니다.
        atlas.SetIncludeInBuild(true);
    }

    /// <summary>배치모드에서 아틀라스를 만들고 종료합니다.</summary>
    public static void Build_From_CLI()
    {
        Build();
        EditorApplication.Exit(0);
    }
}
