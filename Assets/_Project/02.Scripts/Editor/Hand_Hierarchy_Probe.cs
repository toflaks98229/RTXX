using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기·방패가 실제로 어떤 계층 아래에 있고 어떤 변환을 물려받는지
/// 프리팹에서 직접 읽어 보고하는 도구입니다.
///
/// 왜 필요한가:
/// 손 위치 보정값을 계산하려면 '무엇을 물려받는가'를 알아야 합니다.
/// 그런데 이것을 코드만 읽고 추정하면 틀립니다. 실제로 한 번 틀렸습니다.
///
/// 프리팹의 편집 시점 값(전부 0, 스케일 1)은 런타임과 다릅니다.
/// Unit_Animation._Start가 몸통에 scale=size와 y=(size-1)*0.5를 겁니다.
/// 그래서 부모가 누구인지에 따라 같은 로컬 값이 다른 결과를 냅니다.
///
/// 이 도구는 추정 대신 계층을 그대로 출력합니다.
/// </summary>
public static class Hand_Hierarchy_Probe
{
    /// <summary>유닛 프리팹의 스프라이트 계층을 보고합니다.</summary>
    [MenuItem("RTXX/검증: 무기·방패 계층 구조")]
    public static void Report()
    {
        const string path = "Assets/_Project/05.Prefabs/Character/Unit.prefab";

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"[계층] 프리팹을 찾지 못했습니다: {path}");
            return;
        }

        Unit unit = prefab.GetComponent<Unit>();

        if (unit == null)
        {
            Debug.LogError("[계층] 프리팹에 Unit 컴포넌트가 없습니다.");
            return;
        }

        Debug.Log("[계층] 유닛 스프라이트 계층 구조");

        Describe("몸통(Unit_Sprite)", unit.sprite_Unit, prefab.transform);
        Describe("무기(Unit_Weapon)", unit.sprite_Weapon, prefab.transform);
        Describe("방패(Unit_Shield)", unit.sprite_Shield, prefab.transform);

        // 몸통에 애니메이션이 무엇을 거는지도 함께 적습니다.
        // 이 값을 모르면 위 계층 정보만으로는 결론을 낼 수 없습니다.
        Transform body = unit.sprite_Unit != null ? unit.sprite_Unit.transform : null;

        if (body != null)
        {
            Debug.Log(
                "\n[계층] 런타임에 Unit_Animation._Start가 몸통에 거는 것:\n" +
                "    scale           = size\n" +
                "    localPosition.y = (size - 1) * 0.5\n" +
                "  따라서 몸통의 '자식'은 이 둘을 물려받고,\n" +
                "  몸통의 '형제'는 둘 다 받지 않습니다.");
        }
    }

    /// <summary>렌더러 하나의 부모 사슬과 변환을 출력합니다.</summary>
    /// <param name="label">보고에 쓸 이름입니다.</param>
    /// <param name="renderer">조사할 렌더러입니다.</param>
    /// <param name="root">루트 트랜스폼입니다. 사슬을 여기서 끊습니다.</param>
    private static void Describe(string label, SpriteRenderer renderer, Transform root)
    {
        if (renderer == null)
        {
            Debug.LogWarning($"  {label}: 배선되지 않음");
            return;
        }

        Transform t = renderer.transform;

        // 루트까지의 부모 사슬을 만듭니다.
        string chain = t.name;
        Transform p = t.parent;

        while (p != null)
        {
            chain = p.name + " > " + chain;
            if (p == root) break;
            p = p.parent;
        }

        Debug.Log($"  {label}\n" +
                  $"    경로   : {chain}\n" +
                  $"    부모   : {(t.parent != null ? t.parent.name : "(없음)")}\n" +
                  $"    로컬위치: {t.localPosition}\n" +
                  $"    로컬스케일: {t.localScale}\n" +
                  $"    정렬순서: {renderer.sortingOrder}");
    }

    /// <summary>배치모드에서 보고하고 종료합니다.</summary>
    public static void Report_From_CLI()
    {
        Report();
        EditorApplication.Exit(0);
    }
}
