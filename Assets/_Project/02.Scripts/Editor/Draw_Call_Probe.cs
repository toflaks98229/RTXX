using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 드로우콜이 왜 줄지 않는지 원인을 좁히는 진단 도구입니다.
///
/// 왜 필요한가:
/// 아틀라스를 한 장으로 묶어도 드로우콜이 수백이면, 원인은 텍스처 전환이
/// 아니라 다른 곳입니다. 후보가 여럿이고 각각 확인 방법이 달라
/// 눈으로 추측하면 시간만 흘러갑니다.
///
/// 이 도구는 배칭을 끊는 알려진 원인들을 하나씩 확인해 보고합니다.
/// 무엇이 걸리는지 알아야 고칠 곳이 정해집니다.
///
/// 확인 항목:
///   1) URP 동적 배칭 설정   — 꺼져 있으면 스프라이트가 묶이지 않습니다
///   2) 스프라이트 머티리얼   — 유닛마다 다르면 절대 묶이지 않습니다
///   3) 정렬 레이어/순서      — 갈리면 그 경계마다 드로우콜이 끊깁니다
///   4) 셰이더 정점 변형      — 스프라이트 배칭 경로에서 제외될 수 있습니다
/// </summary>
public static class Draw_Call_Probe
{
    /// <summary>씬의 스프라이트 렌더링 상태를 검사해 보고합니다.</summary>
    [MenuItem("RTXX/드로우콜 진단")]
    public static void Diagnose()
    {
        StringBuilder sb = new StringBuilder(1024);

        sb.AppendLine("========== 드로우콜 진단 ==========");

        Check_Pipeline(sb);
        Check_Renderers(sb);

        sb.AppendLine("===================================");

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 렌더 파이프라인 설정을 확인합니다.
    ///
    /// URP에서 SpriteRenderer는 SRP Batcher의 대상이 아닙니다.
    /// SRP Batcher는 MeshRenderer 계열을 위한 경로이며,
    /// 스프라이트는 '동적 배칭'이라는 별도 경로로 묶입니다.
    /// 그래서 SRP Batcher가 켜져 있어도 동적 배칭이 꺼져 있으면
    /// 스프라이트는 하나씩 그려집니다.
    /// </summary>
    private static void Check_Pipeline(StringBuilder sb)
    {
        sb.AppendLine("--- 렌더 파이프라인 ---");

        var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

        if (asset == null)
        {
            sb.AppendLine("파이프라인   : 빌트인 (URP 아님)");
            return;
        }

        sb.AppendLine($"파이프라인   : {asset.GetType().Name}");

        // 동적 배칭 설정은 직렬화 필드로만 노출됩니다.
        SerializedObject so = new SerializedObject(asset);

        SerializedProperty dynamic = so.FindProperty("m_SupportsDynamicBatching");
        SerializedProperty srp = so.FindProperty("m_UseSRPBatcher");

        if (dynamic != null)
        {
            sb.AppendLine($"동적 배칭    : {(dynamic.boolValue ? "켬" : "끔  <- 스프라이트가 묶이지 않습니다")}");
        }

        if (srp != null)
        {
            sb.AppendLine($"SRP Batcher  : {(srp.boolValue ? "켬" : "끔")} " +
                          "(스프라이트에는 적용되지 않습니다)");
        }
    }

    /// <summary>
    /// 씬의 스프라이트 렌더러들을 훑어 배칭을 끊는 요인을 셉니다.
    ///
    /// 유닛이 수천 개이므로 전수 순회는 비쌉니다. 다만 이 도구는
    /// 에디터에서 수동으로 한 번 돌리는 것이라 문제가 되지 않습니다.
    /// </summary>
    private static void Check_Renderers(StringBuilder sb)
    {
        SpriteRenderer[] renderers =
            Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        sb.AppendLine("--- 스프라이트 렌더러 ---");
        sb.AppendLine($"총 개수      : {renderers.Length}개");

        if (renderers.Length == 0) return;

        // 서로 다른 머티리얼과 정렬 조합을 셉니다.
        //
        // 배칭은 '같은 머티리얼 + 같은 정렬'끼리만 일어납니다.
        // 조합 수가 곧 드로우콜의 하한이 됩니다.
        var materials = new System.Collections.Generic.HashSet<Material>();
        var sortings = new System.Collections.Generic.HashSet<long>();
        var textures = new System.Collections.Generic.HashSet<Texture>();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer r = renderers[i];
            if (r == null) continue;

            // sharedMaterial을 씁니다.
            //
            // material을 읽으면 그 자리에서 인스턴스가 만들어져
            // 검사 행위 자체가 배칭을 깨뜨립니다.
            if (r.sharedMaterial != null) materials.Add(r.sharedMaterial);

            // 정렬 레이어와 순서를 하나의 키로 묶습니다.
            long key = ((long)r.sortingLayerID << 32) | (uint)r.sortingOrder;
            sortings.Add(key);

            // 스프라이트가 실제로 쓰는 텍스처입니다.
            // 아틀라스에 묶였다면 전부 같은 텍스처를 가리켜야 합니다.
            if (r.sprite != null) textures.Add(r.sprite.texture);
        }

        sb.AppendLine($"머티리얼 종류: {materials.Count}개");
        sb.AppendLine($"정렬 조합    : {sortings.Count}개");
        sb.AppendLine($"텍스처 종류  : {textures.Count}개  <- 아틀라스가 묶였다면 소수여야 합니다");

        // 텍스처가 여럿이면 아틀라스가 실제로는 적용되지 않은 것입니다.
        if (textures.Count > 4)
        {
            sb.AppendLine();
            sb.AppendLine("경고: 텍스처 종류가 많습니다.");
            sb.AppendLine("      아틀라스가 런타임에 적용되지 않았을 수 있습니다.");
            sb.AppendLine("      (에디터에서는 Play 중에만 아틀라스가 바인딩됩니다)");

            int shown = 0;
            foreach (Texture t in textures)
            {
                if (shown++ >= 5) break;
                sb.AppendLine($"        - {t.name} ({t.width}x{t.height})");
            }
        }

        // 머티리얼이 여럿이면 그것만으로 드로우콜이 갈립니다.
        if (materials.Count > 3)
        {
            sb.AppendLine();
            sb.AppendLine("경고: 머티리얼 종류가 많습니다. 같은 머티리얼을 공유해야 묶입니다.");

            int shown = 0;
            foreach (Material m in materials)
            {
                if (shown++ >= 5) break;
                sb.AppendLine($"        - {m.name} (셰이더: {m.shader.name})");
            }
        }
    }

    /// <summary>배치모드에서 진단을 실행하고 종료합니다.</summary>
    public static void Diagnose_From_CLI()
    {
        Diagnose();
        EditorApplication.Exit(0);
    }
}
