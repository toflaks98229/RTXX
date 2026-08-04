using UnityEditor;
using UnityEngine;

/// <summary>
/// 유닛을 실제로 만들어 무기·방패가 최종적으로 어디에 놓이는지
/// 월드 좌표로 측정하는 도구입니다.
///
/// 왜 필요한가:
/// 지금까지는 프리팹 YAML과 코드를 읽고 '이렇게 될 것이다'를 추정해
/// 계산했습니다. 그런데 두 번 틀렸습니다. 프리팹의 편집 시점 값은
/// 런타임과 다르고(Unit_Animation이 몸통을 바꿉니다), 계층에 따라
/// 물려받는 변환도 달라지기 때문입니다.
///
/// 추정을 멈추고 실제 값을 읽습니다. 이 도구는 유닛을 하나 만들어
/// _Start를 태운 뒤, 각 렌더러의 **월드 좌표와 실제 그려지는 범위**를
/// 보고합니다. 스프라이트의 그림이 실제로 어디에 오는지가 중요하므로
/// bounds(렌더러가 차지하는 실제 사각형)까지 함께 봅니다.
/// </summary>
public static class Hand_Runtime_Probe
{
    /// <summary>씬의 첫 부대로 유닛을 만들어 위치를 측정합니다.</summary>
    [MenuItem("RTXX/검증: 무기·방패 실측")]
    public static void Measure()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        if (armies.Length == 0)
        {
            Debug.LogError("[실측] 씬에 부대가 없습니다.");
            return;
        }

        // 몸통이 실제로 배선된 부대를 고릅니다.
        Army army = null;

        for (int i = 0; i < armies.Length; i++)
        {
            if (armies[i].images_Unit != null && armies[i].images_Unit.Count > 0)
            {
                army = armies[i];
                break;
            }
        }

        if (army == null)
        {
            Debug.LogError("[실측] 몸통 스프라이트가 배선된 부대가 없습니다.");
            return;
        }

        // 유닛은 실행 중에 생성되므로 씬에 미리 놓여 있지 않습니다.
        // 부대가 들고 있는 프리팹에서 하나 만들어 같은 경로를 태웁니다.
        if (army.unit == null)
        {
            Debug.LogError("[실측] 부대에 유닛 프리팹(unit)이 배선되어 있지 않습니다.");
            return;
        }

        GameObject instance = Object.Instantiate(army.unit, army.transform);
        Unit source = instance.GetComponent<Unit>();

        if (source == null)
        {
            Debug.LogError("[실측] 유닛 프리팹에 Unit 컴포넌트가 없습니다.");
            Object.DestroyImmediate(instance);
            return;
        }

        float size = army.army_Data.GetSize();

        Debug.Log($"[실측] 부대 '{army.name}'  size = {size}\n" +
                  $"  몸통 들어올림 (size-1)*0.5 = {(size - 1.0f) * 0.5f}");

        Report("보정 적용 전 (프리팹 상태)", source);

        // 실제 생성 경로와 같은 순서로 초기화합니다.
        // Army.Create_Units가 SetArmy를 먼저 부르고 _Start를 태웁니다.
        //
        // 여기서 Unit_Animation._Start가 몸통을 옮기고,
        // 그 다음 Unit_Hand_Offset.Apply가 장비를 배치합니다.
        source.SetArmy(army);
        source._Start(0, 0);

        Report("보정 적용 후 (_Start 통과)", source);

        // 몸통 기준 상대 오차를 픽셀로 환산해 보고합니다.
        //
        // 눈으로 "2px쯤"이라고 느낀 것을 숫자로 확정하기 위해서입니다.
        // 32px 타일 / PPU 32 이므로 1px = 1/32 유닛입니다.
        Delta("무기", source.sprite_Unit, source.sprite_Weapon);
        Delta("방패", source.sprite_Unit, source.sprite_Shield);

        Debug.Log(
            "[실측] 판정 기준\n" +
            "  아래 '몸통 대비' 값이 표의 오프셋과 같아야 합니다.\n" +
            "  (좌우 반전 중이면 x 부호가 뒤집혀 보이는 것이 정상입니다)");
    }

    /// <summary>몸통 대비 장비의 위치 차이를 픽셀 단위로 보고합니다.</summary>
    /// <param name="label">표시 이름입니다.</param>
    /// <param name="body">기준이 되는 몸통 렌더러입니다.</param>
    /// <param name="equip">비교할 장비 렌더러입니다.</param>
    private static void Delta(string label, SpriteRenderer body, SpriteRenderer equip)
    {
        if (body == null || equip == null) return;

        // 실제로 그려지는 사각형의 중심으로 비교합니다.
        // transform 위치는 피벗 기준이라 그림이 어디 있는지와 다를 수 있습니다.
        Vector3 d = equip.bounds.center - body.bounds.center;

        const float pixel = 1.0f / 32.0f;

        Debug.Log($"    [{label} 몸통 대비] " +
                  $"x {d.x / pixel:F2} px, y {d.y / pixel:F2} px  (월드 {d})");
    }

    /// <summary>세 렌더러의 위치와 실제 그려지는 범위를 출력합니다.</summary>
    /// <param name="label">보고 구간 이름입니다.</param>
    /// <param name="unit">측정할 유닛입니다.</param>
    private static void Report(string label, Unit unit)
    {
        Debug.Log($"--- {label} ---");

        Line("몸통", unit.sprite_Unit);
        Line("무기", unit.sprite_Weapon);
        Line("방패", unit.sprite_Shield);
    }

    /// <summary>렌더러 하나의 좌표와 범위를 한 줄로 출력합니다.</summary>
    /// <param name="label">표시 이름입니다.</param>
    /// <param name="r">측정할 렌더러입니다.</param>
    private static void Line(string label, SpriteRenderer r)
    {
        if (r == null)
        {
            Debug.Log($"    {label}: 없음");
            return;
        }

        Transform t = r.transform;

        // lossyScale은 부모까지 곱해진 최종 배율입니다.
        // 이것이 몸통과 다르면 픽셀 크기가 어긋납니다.
        string spriteName = r.sprite != null ? r.sprite.name : "(스프라이트 없음)";

        Debug.Log(
            $"    {label,-4} {spriteName,-24}\n" +
            $"        로컬위치 {t.localPosition}  최종배율 {t.lossyScale}\n" +
            $"        월드위치 {t.position}\n" +
            $"        그려지는범위 center={r.bounds.center} size={r.bounds.size}");
    }

    /// <summary>배치모드에서 측정하고 종료합니다.</summary>
    public static void Measure_From_CLI()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene1.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        Measure();
        EditorApplication.Exit(0);
    }
}
