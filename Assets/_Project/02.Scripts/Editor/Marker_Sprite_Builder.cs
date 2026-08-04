using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 발밑 마커 스프라이트를 코드로 생성하는 도구입니다.
///
/// 왜 만들어 쓰는가:
/// 기존 프로젝트는 발밑 선택 표시와 진형 배치 마커에 DCSS 시트의
/// 조각(player_14, icons_*)을 잘라 쓰고 있었습니다. 그런데 확인해 보니
/// 그 조각은 링이나 화살표가 아니라 **캐릭터 그림의 일부**였습니다.
/// 마커로 쓰라고 만든 것이 아니라, 눈에 덜 거슬리는 조각을 골라 쓴 것입니다.
///
/// crawl 저장소에도 이 용도에 맞는 자산이 없습니다. DCSS는 커서와
/// 하이라이트를 이미지가 아니라 코드로 그리기 때문입니다.
///
/// 그래서 직접 만듭니다. 필요한 모양이 단순한 도형(링, 쐐기)이라
/// 코드로 그리는 편이 정확하고, 색과 두께를 언제든 바꿀 수 있습니다.
///
/// ---------------------------------------------------------------------
/// 이 마커들이 필요한 이유
/// ---------------------------------------------------------------------
/// 이 게임은 부대 단위로 지휘하는데, 화면에는 병사 9,600명이 뒤엉켜
/// 있습니다. "내가 지금 어느 부대를 선택했는가"와 "그 부대가 어디로
/// 가서 어느 쪽을 보게 되는가"가 보이지 않으면 지휘가 성립하지 않습니다.
///
///   선택 링   : 지금 선택된 부대의 병사들
///   방향 쐐기 : 그 병사가 바라보는 쪽
///   진형 마커 : 명령이 확정되면 병사가 설 자리
/// </summary>
public static class Marker_Sprite_Builder
{
    /// <summary>만든 마커를 둘 폴더입니다.</summary>
    private const string outputFolder = "Assets/_Project/04.Art/01.Images/Markers";

    /// <summary>
    /// 마커 텍스처 한 변의 크기(픽셀)입니다.
    ///
    /// 유닛 타일이 32px이므로 같은 척도로 맞춥니다.
    /// 더 크게 만들면 선명해지지만 발밑에서 유닛을 가립니다.
    /// </summary>
    private const int size = 32;

    /// <summary>모든 마커를 만들고 스프라이트로 임포트합니다.</summary>
    [MenuItem("RTXX/발밑 마커 생성")]
    public static void Build()
    {
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        // 선택 링: 부대를 선택했을 때 병사 발밑에 깔립니다.
        //
        // 속이 빈 원인 이유: 꽉 찬 원은 병사의 발을 가려 누가 서 있는지
        // 보이지 않습니다. 테두리만 있으면 위치는 알리면서 가리지 않습니다.
        Save(Make_Ring(0.86f, 0.16f), "marker_select");

        // 방향 쐐기: 병사가 바라보는 쪽을 가리킵니다.
        //
        // 링과 따로 두는 이유: 링은 회전이 필요 없지만 쐐기는 유닛 방향을
        // 따라 돌아야 합니다. 한 장으로 합치면 링까지 함께 돌아
        // 원이 찌그러져 보입니다.
        Save(Make_Wedge(), "marker_facing");

        // 진형 슬롯: 명령을 내릴 때 병사가 설 자리를 미리 보여 줍니다.
        //
        // 선택 링보다 얇고 흐리게 만듭니다. 슬롯은 '아직 아무도 없는 자리'라
        // 실제 병사보다 눈에 덜 띄어야 화면이 정리됩니다.
        Save(Make_Ring(0.72f, 0.10f), "marker_slot");

        AssetDatabase.Refresh();

        Debug.Log($"[Marker] 마커 3종을 만들었습니다: {outputFolder}\n" +
                  "  marker_select : 선택된 부대의 병사 발밑\n" +
                  "  marker_facing : 병사가 바라보는 방향\n" +
                  "  marker_slot   : 진형 배치 예정 자리");
    }

    /// <summary>
    /// 속이 빈 원(링)을 그립니다.
    /// </summary>
    /// <param name="outer">바깥 반지름입니다. 0~1 비율이며 1이면 텍스처에 꽉 찹니다.</param>
    /// <param name="thickness">테두리 두께입니다. 0~1 비율입니다.</param>
    /// <returns>만들어진 텍스처입니다.</returns>
    private static Texture2D Make_Ring(float outer, float thickness)
    {
        Texture2D tex = New_Texture();

        float half = size * 0.5f;
        float outerR = half * outer;
        float innerR = outerR - half * thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 픽셀 중심까지의 거리로 재야 원이 한쪽으로 치우치지 않습니다.
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 링의 안팎 경계에서 알파를 부드럽게 깎습니다.
                //
                // 픽셀 아트지만 원은 계단이 심하게 보이므로, 경계 1px만
                // 반투명으로 두어 형태가 읽히게 합니다.
                float a = 0.0f;

                if (d <= outerR && d >= innerR)
                {
                    float edgeOuter = Mathf.Clamp01(outerR - d);
                    float edgeInner = Mathf.Clamp01(d - innerR);
                    a = Mathf.Min(1.0f, Mathf.Min(edgeOuter, edgeInner) + 0.35f);
                }

                tex.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 위쪽을 가리키는 삼각 쐐기를 그립니다.
    ///
    /// 유닛의 정면 방향으로 회전시켜 쓰므로, 기준은 +Y(위)입니다.
    /// </summary>
    /// <returns>만들어진 텍스처입니다.</returns>
    private static Texture2D Make_Wedge()
    {
        Texture2D tex = New_Texture();

        float half = size * 0.5f;

        // 쐐기는 링 바깥쪽에 붙습니다. 링 안에 두면 병사를 가립니다.
        //
        // 크기 주의: 처음에 baseHalf를 0.16으로 잡았더니 32px 텍스처에서
        // 밑변이 5px밖에 안 되어 화면에서 점으로 보였습니다.
        // 발밑 마커는 카메라가 내려다보는 각도에서 납작해지므로,
        // 실제로 보이는 것보다 넉넉히 키워야 방향이 읽힙니다.
        const float tipY = 0.98f;    // 꼭짓점 높이 (비율)
        const float baseY = 0.30f;   // 밑변 높이
        const float baseHalf = 0.42f; // 밑변 절반 너비

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - half) / half;
                float ny = (y + 0.5f - half) / half;

                float a = 0.0f;

                if (ny >= baseY && ny <= tipY)
                {
                    // 위로 갈수록 좁아지는 삼각형입니다.
                    float t = (ny - baseY) / (tipY - baseY);
                    float halfWidth = baseHalf * (1.0f - t);

                    if (Mathf.Abs(nx) <= halfWidth) a = 1.0f;
                }

                tex.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>투명으로 초기화된 텍스처를 만듭니다.</summary>
    /// <returns>빈 텍스처입니다.</returns>
    private static Texture2D New_Texture()
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color clear = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++) tex.SetPixel(x, y, clear);
        }

        return tex;
    }

    /// <summary>
    /// 텍스처를 PNG로 저장하고 스프라이트 임포트 설정을 적용합니다.
    ///
    /// 피벗은 **중앙**입니다. 유닛 타일과 다릅니다.
    /// 마커는 발밑 지면에 눕혀 그리므로 회전의 중심이 한가운데여야
    /// 방향을 돌려도 제자리에서 돕니다. (타일은 발이 원점이라 하단 중앙)
    /// </summary>
    /// <param name="tex">저장할 텍스처입니다.</param>
    /// <param name="name">파일 이름(확장자 제외)입니다.</param>
    private static void Save(Texture2D tex, string name)
    {
        string path = Path.Combine(outputFolder, name + ".png");

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32.0f;

        // 마커는 원과 삼각형이라 보간이 있어도 뭉개지지 않고 오히려
        // 부드러워집니다. 유닛 타일과 달리 Point를 쓰지 않습니다.
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);

        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    /// <summary>배치모드에서 마커를 만들고 종료합니다.</summary>
    public static void Build_From_CLI()
    {
        Build();
        EditorApplication.Exit(0);
    }
}
