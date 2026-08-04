using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DCSS(Dungeon Crawl Stone Soup) 개별 타일을 가져와 임포트 설정을 맞추는 도구입니다.
///
/// 왜 이 방식인가:
/// 기존 프로젝트는 main.png(307분할) 같은 '합쳐진 시트'를 스프라이트 에디터로
/// 잘라 쓰고 있었습니다. 그런데 crawl 저장소를 열어 보니 원본이 이미
/// **개별 PNG 8,351개**로 존재합니다. (crawl-ref/source/rltiles/)
/// 합쳐진 시트는 빌드 산출물이고, 원본은 낱장입니다.
///
/// 즉 잘라 낼 필요가 없습니다. 필요한 낱장만 골라 가져오면 됩니다.
/// 그 편이 정확하고(경계가 어긋날 일이 없음), 피벗도 파일마다 정확히 잡힙니다.
///
/// ---------------------------------------------------------------------
/// 피벗을 하단 중앙으로 두는 이유 — 이번 작업의 핵심
/// ---------------------------------------------------------------------
/// 기존 스프라이트는 전부 중앙 피벗(0.5, 0.5)이었습니다.
///
///   32px 타일 / PPU 32 = 1.0 유닛 높이
///   중앙 피벗 -> 피벗 기준 위아래 0.5씩
///   Unit_Sprite 로컬 y = 0.5 -> 스프라이트 하단이 정확히 y=0 (여유 0)
///   여기에 셰이더 상하 진동이 얹히면 하단이 지면 아래로 내려갑니다.
///
/// 실측으로 확인한 것: crawl 타일은 캐릭터의 발이 타일 하단(y=31)에
/// 정확히 닿아 있습니다. (아래 여백 0px)
/// 따라서 피벗을 (0.5, 0)으로 두면 **피벗이 곧 발바닥**이 되고,
/// Unit_Sprite 로컬 y를 0으로 두면 발이 지면에 정확히 붙습니다.
/// 셰이더 진동은 위로만 작용하므로 다시는 잠기지 않습니다.
///
/// 사용법:
///   메뉴 > RTXX > DCSS 타일 가져오기
///   또는 CLI: -executeMethod DCSS_Tile_Importer.Import_From_CLI
/// </summary>
public static class DCSS_Tile_Importer
{
    /// <summary>crawl 저장소의 타일 원본 폴더입니다. 프로젝트 루트 기준입니다.</summary>
    private const string sourceRoot = "External/crawl/crawl-ref/source/rltiles";

    /// <summary>가져온 타일을 둘 폴더입니다.</summary>
    private const string targetRoot = "Assets/_Project/04.Art/01.Images/DCSS_Tiles";

    /// <summary>
    /// 이 프로젝트가 쓰는 타일 목록입니다.
    ///
    /// 8,351개를 전부 가져오면 Unity 임포트가 길어지고 아틀라스도 비대해집니다.
    /// 지금 필요한 것만 골라 씁니다. 필요하면 여기에 추가하십시오.
    ///
    /// ---------------------------------------------------------------------
    /// 무기/방패 위치 — 캔버스 정렬만으로는 부족합니다
    /// ---------------------------------------------------------------------
    /// 실측으로 확인한 것입니다.
    ///
    ///   player/base/human_m.png       32x32, 몸이 x=5..26  y=2..31
    ///   player/hand1/dagger.png       32x32, 무기가 x=2..8 y=5..20
    ///   player/hand1/long_sword_slant 32x32, 무기가 x=0..8 y=0..20
    ///
    /// 무기 PNG는 '무기만 잘라낸 이미지'가 아니라 **32x32 캔버스에 미리
    /// 배치된** 이미지입니다. 따라서 같은 크기·같은 피벗으로 겹치는 것이
    /// 출발점입니다. (그래서 아래 Apply_Import_Settings가 Full Rect를
    /// 강제합니다. Tight로 자르면 이 전제가 깨집니다)
    ///
    /// 다만 **그것만으로는 손에 정확히 들리지 않습니다.**
    ///
    /// crawl은 몸통 타일마다 무기·방패를 픽셀 단위로 밀어 주는 표를
    /// 코드에 갖고 있습니다. (tilemcache.cc)
    ///
    ///   get_weapon_offset()  무기 337종 (그중 312종이 0이 아님)
    ///   get_shield_offset()  방패 160종
    ///
    ///   HUMAN       무기 (-1,-2)  방패 (0,0)
    ///   ORC         무기 (-2,-1)  방패 (1,0)
    ///   ORC_KNIGHT  무기 (-2,+1)  방패 (1,0)
    ///
    /// 즉 몸통마다 손 위치가 다르므로, 몸통을 무작위로 고르는 지금 구조
    /// (Unit._Start)에서는 같은 부대 안에서도 맞는 병사와 어긋난 병사가
    /// 섞입니다.
    ///
    /// 이 오프셋은 '플레이어 인형(doll)' 경로에서는 0이지만, 우리는
    /// mon/ 폴더의 **몬스터 타일**을 몸통으로 쓰므로 해당됩니다.
    /// 자세한 근거와 적용 방안은 Docs/DCSS_Hand_Offset.md를 보십시오.
    /// </summary>
    private static readonly string[] tilePaths =
    {
        // --- 몸통: 몬스터 타일 ---
        // 인간 계열 — 아군 병사로 씁니다.
        "mon/humanoids/humans/human.png",
        "mon/humanoids/humans/human2.png",
        "mon/humanoids/humans/human3.png",

        // 오크 계열 — 적군 병사로 씁니다.
        "mon/humanoids/orcs/orc.png",
        "mon/humanoids/orcs/orc_warrior.png",
        "mon/humanoids/orcs/orc_knight.png",
        "mon/humanoids/orcs/orc_warlord.png",
        "mon/humanoids/orcs/orc_priest.png",

        // --- 몸통: 플레이어 베이스 ---
        // 몬스터 타일보다 자세가 중립적이라 무기 합성에 더 잘 맞습니다.
        "player/base/human_m.png",
        "player/base/human_f.png",

        // --- 무기 (hand1) ---
        // 손 위치가 이미 반영되어 있으므로 그대로 겹치면 됩니다.
        "player/hand1/dagger.png",
        "player/hand1/short_sword_slant.png",
        "player/hand1/long_sword_slant.png",
        "player/hand1/hand_axe.png",
        "player/hand1/spear.png",
        "player/hand1/mace.png",
        "player/hand1/bow.png",

        // --- 방패 (hand2) ---
        "player/hand2/buckler_round.png",
        "player/hand2/kite_shield_bullseye.png",
        "player/hand2/kite_shield_kite1.png",
    };

    // =====================================================================
    // 전체 가져오기
    //
    // 목록(tilePaths)에 적힌 것만 가져오는 선별 방식과 달리, 폴더째
    // 통째로 들여옵니다. 병종을 늘리거나 아이템을 붙일 때마다 목록을
    // 손보지 않아도 되지만, 그만큼 임포트 시간과 아틀라스가 커집니다.
    //
    // 규모 (실측):
    //   mon     1,521장    player  1,133장
    //   item    1,111장    effect    443장
    //   dngn    2,220장    gui     1,003장
    //   총      8,351장
    //
    // 크기 분포: 4,091장이 32x32, 105장이 32x48(대형 몬스터), 나머지 소수.
    // 하단 중앙 피벗은 크기가 달라도 그대로 성립하므로 특별 처리가 없습니다.
    // =====================================================================

    /// <summary>
    /// 전체 가져오기 대상 폴더입니다.
    ///
    /// dngn(지형)과 gui는 제외했습니다. 이 게임은 지형에 Unity Terrain을
    /// 쓰고 UI는 IMGUI로 그리므로 쓸 일이 없고, 둘이 3,223장으로 전체의
    /// 40%를 차지해 아틀라스만 불립니다.
    /// </summary>
    private static readonly string[] bulkFolders =
    {
        "mon",
        "player",
        "item",
        "effect",

        // UI 아이콘입니다. (Docs/UI_Design_DCSS.md 참고)
        //
        // gui/skills, gui/abilities 등이 32x32 아이콘이라 병종·태세·상태
        // 표시에 그대로 쓸 수 있습니다. 같은 아틀라스에 들어가므로
        // 드로우콜은 늘지 않습니다.
        "gui",
    };

    /// <summary>
    /// crawl 타일 전체를 폴더째 가져옵니다.
    ///
    /// 선별 가져오기(Import)와 달리 목록을 관리하지 않아도 되지만,
    /// 임포트에 수 분이 걸리고 아틀라스가 커집니다. 쓰는 타일이
    /// 확정되기 전의 탐색 단계에서 쓰십시오.
    /// </summary>
    [MenuItem("RTXX/DCSS 타일 전체 가져오기")]
    public static void Import_All()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, sourceRoot);

        if (!Directory.Exists(source))
        {
            Debug.LogError($"[DCSS] 타일 원본을 찾지 못했습니다: {source}");
            return;
        }

        if (!Directory.Exists(targetRoot)) Directory.CreateDirectory(targetRoot);

        List<string> imported = new List<string>();

        for (int f = 0; f < bulkFolders.Length; f++)
        {
            string folder = Path.Combine(source, bulkFolders[f]);
            if (!Directory.Exists(folder)) continue;

            string[] files = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                // 진행 상황을 보여 줍니다.
                // 수천 장을 복사하는 동안 에디터가 멈춘 것처럼 보이기 때문입니다.
                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "DCSS 타일 가져오기",
                        $"{bulkFolders[f]} ({i}/{files.Length})",
                        (f + (float)i / files.Length) / bulkFolders.Length);
                }

                string relative = files[i].Substring(source.Length + 1).Replace('\\', '/');

                // 이름 충돌을 막기 위해 경로를 파일명에 녹입니다.
                //
                // 원본에는 서로 다른 폴더에 같은 이름이 흔합니다.
                // (mon/.../human.png 와 player/base/human_m.png 등)
                // 평평하게 펴면서 이름만 쓰면 나중 것이 앞의 것을 덮어씁니다.
                string fileName = Flatten_Name(relative);
                string to = Path.Combine(targetRoot, fileName);

                File.Copy(files[i], to, true);
                imported.Add(to.Replace('\\', '/'));
            }
        }

        EditorUtility.ClearProgressBar();

        AssetDatabase.Refresh();

        // 임포트 설정을 일괄 적용합니다.
        //
        // StartAssetEditing으로 묶는 이유: 파일마다 SaveAndReimport를 부르면
        // 그때마다 에셋 데이터베이스가 갱신되어 수천 장에서 매우 느려집니다.
        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < imported.Count; i++)
            {
                if (i % 200 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "DCSS 임포트 설정", $"{i}/{imported.Count}",
                        (float)i / imported.Count);
                }

                Apply_Import_Settings(imported[i]);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[DCSS] 타일 {imported.Count}장을 가져왔습니다.\n" +
                  $"위치: {targetRoot}\n" +
                  "이어서 'RTXX/DCSS 아틀라스 만들기'를 실행하십시오.");
    }

    /// <summary>
    /// 원본 상대 경로를 충돌하지 않는 평평한 파일명으로 바꿉니다.
    ///
    /// 예: mon/humanoids/orcs/orc.png -> mon_humanoids_orcs_orc.png
    ///
    /// 폴더 구조를 그대로 옮기지 않는 이유: 스프라이트 아틀라스는 폴더를
    /// 통째로 담을 수 있지만, 폴더가 깊으면 어느 계열인지 파일명만으로
    /// 알 수 없어 코드에서 고르기가 어렵습니다.
    /// </summary>
    /// <param name="relativePath">원본 기준 상대 경로입니다.</param>
    /// <returns>경로 구분자를 밑줄로 바꾼 파일명입니다.</returns>
    private static string Flatten_Name(string relativePath)
    {
        return relativePath.Replace('/', '_');
    }

    /// <summary>목록에 적힌 타일을 복사해 오고 임포트 설정을 적용합니다.</summary>
    [MenuItem("RTXX/DCSS 타일 가져오기 (선별)")]
    public static void Import()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, sourceRoot);

        if (!Directory.Exists(source))
        {
            Debug.LogError(
                $"[DCSS] 타일 원본을 찾지 못했습니다: {source}\n" +
                "다음 명령으로 먼저 클론하십시오:\n" +
                "  git clone --depth 1 --branch stone_soup-0.34 " +
                "https://github.com/crawl/crawl.git External/crawl");
            return;
        }

        if (!Directory.Exists(targetRoot)) Directory.CreateDirectory(targetRoot);

        List<string> imported = new List<string>();

        for (int i = 0; i < tilePaths.Length; i++)
        {
            string relative = tilePaths[i];
            string from = Path.Combine(source, relative.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(from))
            {
                Debug.LogWarning($"[DCSS] 원본 없음: {relative}");
                continue;
            }

            // 폴더 구조를 평평하게 폅니다.
            //
            // 원본 경로(mon/humanoids/orcs/...)를 그대로 옮기면 폴더가 깊어지고,
            // 파일명만으로는 어느 계열인지 알 수 없게 됩니다.
            // 계열을 접두사로 붙여 한 폴더에 모읍니다.
            string category = Get_Category(relative);
            string fileName = category + "_" + Path.GetFileName(relative);
            string to = Path.Combine(targetRoot, fileName);

            File.Copy(from, to, true);
            imported.Add(to.Replace('\\', '/'));
        }

        AssetDatabase.Refresh();

        // 복사 직후에는 아직 기본 설정(중앙 피벗)입니다. 여기서 바로잡습니다.
        for (int i = 0; i < imported.Count; i++)
        {
            Apply_Import_Settings(imported[i]);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[DCSS] 타일 {imported.Count}개를 가져와 하단 중앙 피벗으로 설정했습니다.\n" +
                  $"위치: {targetRoot}");
    }

    /// <summary>원본 경로에서 계열 이름을 뽑습니다. 파일명 접두사로 씁니다.</summary>
    private static string Get_Category(string relativePath)
    {
        // 손 슬롯을 먼저 봅니다.
        // 경로에 player가 함께 들어 있어 순서가 뒤바뀌면 전부 base로 묶입니다.
        if (relativePath.Contains("/hand1/")) return "weapon";
        if (relativePath.Contains("/hand2/")) return "shield";
        if (relativePath.Contains("/base/")) return "base";

        if (relativePath.Contains("/humans/")) return "human";
        if (relativePath.Contains("/orcs/")) return "orc";
        if (relativePath.Contains("/elves/")) return "elf";

        return "tile";
    }

    /// <summary>
    /// 스프라이트 임포트 설정을 이 프로젝트 규약에 맞춥니다.
    ///
    /// 핵심은 pivot입니다. 나머지는 픽셀 아트가 뭉개지지 않게 하는 설정입니다.
    /// </summary>
    /// <param name="assetPath">설정을 적용할 에셋 경로입니다.</param>
    /// <param name="breimport">
    /// 즉시 다시 임포트할지 여부입니다.
    ///
    /// 여러 장을 한꺼번에 처리할 때는 false로 두십시오.
    /// StartAssetEditing 구간 안에서 장마다 SaveAndReimport를 부르면
    /// 일괄 처리가 깨져 5,000장에 몇 분이 걸립니다.
    /// </param>
    private static void Apply_Import_Settings(string assetPath, bool breimport = true)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;

        // 낱장이므로 분할이 필요 없습니다.
        importer.spriteImportMode = SpriteImportMode.Single;

        // 32px 타일을 1유닛으로 씁니다. 기존 스프라이트와 같은 척도입니다.
        importer.spritePixelsPerUnit = 32.0f;

        // 픽셀 아트이므로 보간과 압축을 끕니다.
        // 켜 두면 32px 타일이 뭉개져 원본의 픽셀감이 사라집니다.
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        // === 피벗: 하단 중앙 ===
        //
        // 이 두 줄이 이번 작업의 목적입니다.
        // crawl 타일은 발이 타일 하단에 정확히 닿아 있으므로(실측 아래 여백 0px),
        // 하단 중앙 피벗은 곧 '발바닥이 원점'을 뜻합니다.
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spritePivot = new Vector2(0.5f, 0.0f);

        // === 메시: Full Rect (반드시 Tight가 아니어야 합니다) ===
        //
        // 이 한 줄이 없으면 무기와 방패가 몸통과 어긋납니다.
        //
        // Unity의 기본값은 Tight이고, Tight는 투명 픽셀을 잘라내 메시를
        // '실제로 그려진 부분'에만 맞춥니다. 그러면 피벗이 원본 32x32
        // 캔버스가 아니라 **잘려나간 사각형 기준**으로 계산됩니다.
        //
        // 결과가 왜 어긋나는가:
        // DCSS 타일은 무기·방패가 32x32 캔버스 위의 제자리에 이미 그려져
        // 있어서, 같은 캔버스에 겹치기만 하면 맞도록 만들어져 있습니다.
        // (그래서 오프셋 데이터가 따로 없습니다)
        // 그런데 Tight로 자르면 칼은 칼 그림의 하단 중앙이, 방패는 방패
        // 그림의 하단 중앙이 원점이 됩니다. 그림마다 여백이 다르므로
        // 타일마다 제각각 다른 양만큼 밀립니다.
        //
        // Full Rect는 원본 32x32를 그대로 유지하므로 '같은 캔버스에
        // 겹친다'는 전제가 지켜집니다.
        settings.spriteMeshType = SpriteMeshType.FullRect;

        importer.SetTextureSettings(settings);

        if (breimport)
        {
            importer.SaveAndReimport();
            return;
        }

        // 일괄 처리 중에는 .meta에 기록만 하고 임포트 요청만 큐에 넣습니다.
        //
        // SaveAndReimport는 그 자리에서 임포트를 강제하므로
        // StartAssetEditing 구간의 이점이 사라집니다. ImportAsset은
        // 구간 안에서는 예약만 되고 StopAssetEditing에서 한 번에 처리됩니다.
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>
    /// 이미 가져온 타일의 임포트 설정을 현재 규약으로 다시 맞춥니다.
    ///
    /// 왜 따로 필요한가:
    /// 임포트 설정은 .meta에 한 번 기록되면 그대로 남습니다. 즉 위의
    /// Apply_Import_Settings를 고쳐도 **이미 가져온 파일에는 적용되지
    /// 않습니다.** 규약이 바뀔 때마다 전부 지우고 다시 받는 것은
    /// 비싸므로, 설정만 다시 씌우는 경로를 둡니다.
    ///
    /// 실제로 이 함수가 필요했던 사건:
    /// 타일 5,231장이 전부 Tight 메시로 들어와 있었습니다(Unity 기본값).
    /// Tight는 투명 여백을 잘라내므로 피벗이 원본 캔버스가 아니라 잘린
    /// 사각형 기준이 되고, 그 결과 무기와 방패가 몸통과 어긋났습니다.
    /// 자세한 배경은 Apply_Import_Settings의 메시 항목을 보십시오.
    /// </summary>
    [MenuItem("RTXX/DCSS 타일 임포트 설정 재적용")]
    public static void Reapply_Settings()
    {
        if (!Directory.Exists(targetRoot))
        {
            Debug.LogError($"[DCSS] 타일 폴더가 없습니다: {targetRoot}");
            return;
        }

        string[] files = Directory.GetFiles(targetRoot, "*.png", SearchOption.AllDirectories);

        // 5,000장이 넘으므로 한 장씩 SaveAndReimport하면 매우 느립니다.
        // 일괄 구간으로 묶어 임포트를 한 번만 돌립니다.
        AssetDatabase.StartAssetEditing();

        int changed = 0;

        try
        {
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');

                EditorUtility.DisplayProgressBar(
                    "DCSS 타일 설정 재적용",
                    $"{i + 1} / {files.Length}",
                    (float)i / Mathf.Max(1, files.Length));

                if (Reapply_One(path)) changed++;
            }
        }
        finally
        {
            // 예외가 나도 반드시 풀어야 합니다.
            // 걸린 채로 남으면 에디터가 에셋 변경을 반영하지 않습니다.
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        Debug.Log($"[DCSS] 임포트 설정을 재적용했습니다.\n" +
                  $"  검사 {files.Length}장 / 수정 {changed}장\n" +
                  $"  메시를 Full Rect로 바꿔 무기·방패 피벗을 원본 캔버스에 맞췄습니다.");
    }

    /// <summary>
    /// 타일 한 장의 설정이 규약과 다르면 고칩니다.
    /// </summary>
    /// <param name="assetPath">검사할 에셋 경로입니다.</param>
    /// <returns>실제로 고쳤으면 true입니다.</returns>
    private static bool Reapply_One(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        // 이미 맞는 것은 건드리지 않습니다.
        //
        // 5,000장을 무조건 다시 임포트하면 몇 분이 걸립니다.
        // 실제로 틀어진 것만 골라야 재적용이 현실적인 비용이 됩니다.
        bool bok = settings.spriteMeshType == SpriteMeshType.FullRect
                   && settings.spriteAlignment == (int)SpriteAlignment.BottomCenter
                   && settings.spritePivot == new Vector2(0.5f, 0.0f);

        if (bok) return false;

        // 일괄 구간 안이므로 즉시 임포트하지 않습니다.
        Apply_Import_Settings(assetPath, false);
        return true;
    }

    /// <summary>배치모드 진입점입니다.</summary>
    public static void Import_From_CLI()
    {
        Import();
        EditorApplication.Exit(0);
    }

    /// <summary>배치모드에서 설정만 재적용하고 종료합니다.</summary>
    public static void Reapply_From_CLI()
    {
        Reapply_Settings();
        EditorApplication.Exit(0);
    }
}
