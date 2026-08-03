using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 검증 씬의 지형이 실제로 기복을 갖는지 확인하는 도구입니다.
///
/// 왜 필요한가:
/// 지형 밀착 검사가 "이격 0.00m"로 통과했을 때, 그것이
///   (a) 높이 동기화가 제대로 동작한 것인지
///   (b) 애초에 지형이 평평해서 아무 일도 안 해도 통과한 것인지
/// 를 구분할 수 없습니다. 후자라면 그 통과는 아무것도 증명하지 못합니다.
///
/// 그래서 전장 범위의 지면 높이를 직접 표본 조사해 기복을 잽니다.
/// </summary>
public static class Terrain_Probe
{
    public static void Run_From_CLI()
    {
        EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene_MassBattle.unity", OpenSceneMode.Single);

        Controller controller = Object.FindAnyObjectByType<Controller>();
        if (controller == null || controller.armies == null || controller.armies.Count == 0)
        {
            Debug.LogError("[TerrainProbe] 부대를 찾지 못했습니다.");
            EditorApplication.Exit(1);
            return;
        }

        // 부대들이 실제로 서 있는 범위를 구합니다.
        Vector3 min = new Vector3(float.MaxValue, 0.0f, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, 0.0f, float.MinValue);

        for (int i = 0; i < controller.armies.Count; i++)
        {
            Army a = controller.armies[i];
            if (a == null) continue;

            Vector3 p = a.transform.position;
            if (p.x < min.x) min.x = p.x;
            if (p.z < min.z) min.z = p.z;
            if (p.x > max.x) max.x = p.x;
            if (p.z > max.z) max.z = p.z;
        }

        int mask = LayerMask.GetMask("Ground");
        if (mask == 0) mask = ~0;

        float lowest = float.MaxValue;
        float highest = float.MinValue;
        int hits = 0;
        int misses = 0;

        const int steps = 24;

        for (int ix = 0; ix <= steps; ix++)
        {
            for (int iz = 0; iz <= steps; iz++)
            {
                float x = Mathf.Lerp(min.x, max.x, ix / (float)steps);
                float z = Mathf.Lerp(min.z, max.z, iz / (float)steps);

                Vector3 origin = new Vector3(x, 500.0f, z);

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000.0f, mask))
                {
                    hits++;
                    if (hit.point.y < lowest) lowest = hit.point.y;
                    if (hit.point.y > highest) highest = hit.point.y;
                }
                else
                {
                    misses++;
                }
            }
        }

        if (hits == 0)
        {
            Debug.LogError("[TerrainProbe] 전장 범위에서 지면을 전혀 찾지 못했습니다. " +
                           "지형이 없거나 Ground 레이어가 아닙니다.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(
            $"[TerrainProbe] 전장 범위 X[{min.x:F1}~{max.x:F1}] Z[{min.z:F1}~{max.z:F1}]\n" +
            $"  지면 적중 {hits} / 미적중 {misses}\n" +
            $"  최저 {lowest:F2} m / 최고 {highest:F2} m\n" +
            $"  높이차 {(highest - lowest):F2} m  " +
            $"(0에 가까우면 평지이며, 지형 밀착 검사가 의미를 갖지 못합니다)");

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 검증용으로 지형에 완만한 언덕을 만듭니다.
    ///
    /// 왜 필요한가:
    /// 기본 지형이 완전한 평지(높이차 0.00m)라서, 지형 밀착 검사가
    /// 아무것도 하지 않아도 통과합니다. 그런 통과는 높이 동기화가
    /// 동작한다는 증거가 되지 못합니다.
    ///
    /// 전장을 가로지르는 능선을 만들어, 고지 점령과 경사 감속 같은
    /// 규칙이 실제로 발동하는지도 함께 볼 수 있게 합니다.
    ///
    /// 주의: 지형 에셋을 직접 수정합니다. 원본을 보존하려면 먼저 백업하십시오.
    /// </summary>
    public static void Raise_Hills_From_CLI()
    {
        EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene_MassBattle.unity", OpenSceneMode.Single);

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length == 0)
        {
            Debug.LogError("[TerrainProbe] 지형을 찾지 못했습니다.");
            EditorApplication.Exit(1);
            return;
        }

        for (int t = 0; t < terrains.Length; t++)
        {
            Terrain terrain = terrains[t];
            TerrainData data = terrain.terrainData;
            if (data == null) continue;

            int res = data.heightmapResolution;
            float[,] heights = new float[res, res];

            Vector3 terrainPos = terrain.transform.position;
            Vector3 size = data.size;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // 지형 로컬 좌표를 월드 좌표로 환산합니다.
                    float wx = terrainPos.x + (x / (float)(res - 1)) * size.x;
                    float wz = terrainPos.z + (y / (float)(res - 1)) * size.z;

                    // 완만한 능선 + 잔물결입니다.
                    //
                    // 값은 0~1 정규화 높이이고 실제 높이는 size.y(수백 m)가
                    // 곱해지므로, 아주 작은 계수를 써야 '언덕'이 됩니다.
                    // 전장 전체 고저차를 15~20m 정도로 잡습니다.
                    // (첫 시도에서 0.55를 썼다가 267m 절벽이 나왔습니다)
                    float scale = 20.0f / Mathf.Max(1.0f, size.y);

                    float ridge = Mathf.Exp(-Mathf.Pow((wz - 50.0f) / 45.0f, 2.0f)) * 0.7f;
                    float ripple = Mathf.Sin(wx * 0.04f) * Mathf.Cos(wz * 0.035f) * 0.3f;

                    float h = (ridge + ripple) * scale;

                    heights[y, x] = Mathf.Clamp01(h);
                }
            }

            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[TerrainProbe] 지형 {terrains.Length}개에 언덕을 생성했습니다.");
        EditorApplication.Exit(0);
    }
}
