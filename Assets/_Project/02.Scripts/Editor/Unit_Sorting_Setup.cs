using UnityEditor;
using UnityEngine;

/// <summary>
/// 유닛 스프라이트의 렌더링 정렬을 확정하는 도구입니다.
///
/// 왜 필요한가:
/// 프리팹 실측 결과 몸통·무기·방패가 **전부 같은 정렬 레이어(Unit)에
/// 같은 순서(0)** 로 놓여 있었습니다.
///
///   Unit_Sprite  layer=Unit  order=0
///   Unit_Weapon  layer=Unit  order=0
///   Unit_Shield  layer=Unit  order=0
///
/// 정렬 레이어와 순서가 모두 같으면 Unity는 그 다음 기준(카메라 축
/// 거리)으로 순서를 정합니다. 그런데 이 세 스프라이트는 z가 사실상
/// 같으므로, 어느 것이 앞에 그려질지가 **매 프레임 뒤집힐 수
/// 있습니다.** 무기가 몸 뒤로 숨었다 나왔다 하는 깜빡임이 그것입니다.
///
/// 위치 보정을 아무리 정확히 해도 이 문제는 남습니다. 원인이 좌표가
/// 아니라 그리는 순서이기 때문입니다.
///
/// ---------------------------------------------------------------------
/// 왜 'Unit_Equipment' 레이어를 쓰는가
/// ---------------------------------------------------------------------
/// 프로젝트에는 이미 다음 네 레이어가 정의되어 있습니다.
///
///   Default / Unit / Unit_Equipment / Unit_UI
///
/// 그런데 Unit_Equipment를 **아무도 쓰고 있지 않았습니다.** 장비를
/// 몸통과 분리해 그리려고 만들어 둔 레이어가 놀고 있었던 것입니다.
/// 이 도구가 그 원래 의도대로 배선합니다.
///
/// 순서는 다음과 같이 잡습니다.
///
///   Unit           방패(-1) < 몸통(0)      방패는 몸 뒤(반대편 손)
///   Unit_Equipment 무기(0)                 무기는 몸 앞
///   Unit_UI        발밑 마커
///
/// 방패를 몸 뒤에 두는 이유: DCSS 타일은 캐릭터가 오른쪽을 보고 있고
/// 방패는 왼팔(먼 쪽 팔)에 듭니다. 몸 앞에 그리면 가슴을 덮습니다.
/// </summary>
public static class Unit_Sorting_Setup
{
    /// <summary>유닛 프리팹 경로입니다.</summary>
    private const string prefabPath = "Assets/_Project/05.Prefabs/Character/Unit.prefab";

    /// <summary>몸통이 놓일 정렬 레이어입니다.</summary>
    private const string layer_Unit = "Unit";

    /// <summary>무기가 놓일 정렬 레이어입니다.</summary>
    private const string layer_Equipment = "Unit_Equipment";

    /// <summary>정렬을 프리팹에 적용합니다.</summary>
    [MenuItem("RTXX/유닛 렌더링 정렬 설정")]
    public static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError($"[정렬] 프리팹을 열지 못했습니다: {prefabPath}");
            return;
        }

        try
        {
            Unit unit = root.GetComponent<Unit>();

            if (unit == null)
            {
                Debug.LogError("[정렬] 프리팹에 Unit 컴포넌트가 없습니다.");
                return;
            }

            // 몸통: 기준입니다. Unit 레이어의 0번.
            Apply(unit.sprite_Unit, layer_Unit, 0, "몸통");

            // 방패: 몸통보다 뒤. 같은 레이어에서 순서만 낮춥니다.
            //
            // 레이어를 나누지 않는 이유: 방패는 몸통과 한 덩어리로
            // 읽혀야 하므로, 다른 부대의 몸통 사이에 끼어들면 안 됩니다.
            Apply(unit.sprite_Shield, layer_Unit, -1, "방패");

            // 무기: 몸통보다 앞. 레이어를 올려 확실히 분리합니다.
            //
            // 순서(order)만 올리지 않고 레이어를 쓰는 이유:
            // 유닛 9,600명이 서로 겹치는 난전에서는 같은 레이어 안의
            // 순서만으로는 '내 무기가 남의 몸통 앞'이 보장되지 않습니다.
            // 레이어를 분리하면 장비는 언제나 몸통 무리 위에 옵니다.
            Apply(unit.sprite_Weapon, layer_Equipment, 0, "무기");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log(
                "[정렬] 유닛 렌더링 정렬을 확정했습니다.\n" +
                "  방패  Unit(-1)            몸 뒤\n" +
                "  몸통  Unit(0)             기준\n" +
                "  무기  Unit_Equipment(0)   몸 앞\n" +
                "  -> 같은 레이어·같은 순서로 인한 깜빡임이 사라집니다.");
        }
        finally
        {
            // 예외가 나도 반드시 닫아야 합니다.
            // 열어 둔 채로 두면 다음 로드에서 경고가 납니다.
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>렌더러 하나의 정렬 레이어와 순서를 설정합니다.</summary>
    /// <param name="renderer">설정할 렌더러입니다.</param>
    /// <param name="layer">정렬 레이어 이름입니다.</param>
    /// <param name="order">레이어 안에서의 순서입니다.</param>
    /// <param name="label">로그에 쓸 이름입니다.</param>
    private static void Apply(SpriteRenderer renderer, string layer, int order, string label)
    {
        if (renderer == null)
        {
            Debug.LogWarning($"[정렬] {label} 렌더러가 배선되지 않았습니다.");
            return;
        }

        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;

        EditorUtility.SetDirty(renderer);
    }

    /// <summary>배치모드에서 설정하고 종료합니다.</summary>
    public static void Setup_From_CLI()
    {
        Setup();
        EditorApplication.Exit(0);
    }
}
