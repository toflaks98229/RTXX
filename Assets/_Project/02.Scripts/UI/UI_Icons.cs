using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 병종·태세·상태를 나타내는 아이콘을 조회하는 정적 계층입니다.
///
/// 왜 필요한가:
/// 지금까지 HUD는 병종과 태세를 전부 한글 텍스트로 적었습니다.
/// 카드가 글자로 가득 차 난전 중에 눈으로 훑기 어렵습니다.
/// 아이콘이면 한 번에 읽힙니다.
///
/// 왜 이 계층을 따로 두는가:
/// 아이콘을 쓰는 곳이 여럿(부대 카드, 부대 배너, 전과 보고서)이고,
/// 각자 Resources.Load를 부르면 같은 것을 여러 번 찾게 됩니다.
/// 여기서 한 번만 찾아 캐시해 두고 나눠 씁니다.
///
/// ---------------------------------------------------------------------
/// IMGUI에서 아틀라스 스프라이트를 그리는 방법
/// ---------------------------------------------------------------------
/// GUI.DrawTexture는 Texture2D만 받습니다. 그런데 아틀라스에 묶인
/// 스프라이트는 큰 텍스처의 '일부'이므로, 그냥 그리면 아틀라스 전체가
/// 나옵니다.
///
/// GUI.DrawTextureWithTexCoords에 UV 사각형을 함께 넘겨야 그 부분만
/// 잘라 그릴 수 있습니다. Sprite.textureRect가 픽셀 단위 영역을 주므로,
/// 텍스처 크기로 나눠 0~1 UV로 바꿉니다. (Get_UV가 그 일을 합니다)
///
/// 주의: 아틀라스 바인딩은 플레이 모드에서만 일어납니다.
/// 편집 모드에서는 sprite.texture가 원본 PNG를 가리키므로,
/// 이 계층은 어느 쪽이든 동작하도록 sprite에서 직접 값을 읽습니다.
/// </summary>
public static class UI_Icons
{
    /// <summary>아이콘 스프라이트가 들어 있는 폴더입니다.</summary>
    private const string iconFolder = "DCSS_Tiles";

    /// <summary>이름 -> 스프라이트 캐시입니다. 한 번 찾은 것은 다시 찾지 않습니다.</summary>
    private static readonly Dictionary<string, Sprite> cache =
        new Dictionary<string, Sprite>(32);

    /// <summary>아이콘을 찾을 수 없을 때 로그를 한 번만 내기 위한 표시입니다.</summary>
    private static readonly HashSet<string> reported = new HashSet<string>();

    // =====================================================================
    // 대응표
    //
    // 파일명은 DCSS_Tile_Importer가 붙인 규칙을 따릅니다.
    //   원본 gui/skills/polearms.png -> gui_skills_polearms
    // =====================================================================

    /// <summary>
    /// 병종에 대응하는 아이콘 이름을 반환합니다.
    ///
    /// DCSS의 무기 숙련 아이콘을 그대로 씁니다. 창병은 폴암,
    /// 궁병은 활처럼 무기가 곧 병종을 뜻하므로 직관적으로 읽힙니다.
    /// </summary>
    /// <param name="unitClass">아이콘을 구할 병종입니다.</param>
    /// <returns>아이콘 스프라이트 이름입니다.</returns>
    public static string Get_Class_Icon(E_Unit_Class unitClass)
    {
        switch (unitClass)
        {
            case E_Unit_Class.Spear: return "gui_skills_polearms";
            case E_Unit_Class.Archer: return "gui_skills_bows";

            // 기병은 전용 아이콘이 없어 근접 무기로 대신합니다.
            case E_Unit_Class.Cavalry: return "gui_skills_maces_flails";

            case E_Unit_Class.Large: return "gui_skills_fighting";

            default: return "gui_skills_axes";
        }
    }

    /// <summary>
    /// 태세에 대응하는 아이콘 이름을 반환합니다. **부대 카드용**입니다.
    ///
    /// 기본 태세(Line)는 빈 문자열이며 호출부가 그리지 않습니다.
    /// 카드에 전부 아이콘을 달면 '특별한 상태'가 눈에 띄지 않기 때문입니다.
    ///
    /// 버튼에는 이 함수를 쓰지 마십시오. 버튼은 비어 있으면 안 되므로
    /// <see cref="Get_Stance_Button_Icon"/>를 씁니다.
    /// </summary>
    /// <param name="stance">아이콘을 구할 태세입니다.</param>
    /// <returns>아이콘 이름이며, 표시할 것이 없으면 빈 문자열입니다.</returns>
    public static string Get_Stance_Icon(E_Army_Stance stance)
    {
        // Line은 기본 태세이므로 카드에 표시하지 않습니다.
        return stance == E_Army_Stance.Line
            ? string.Empty
            : Get_Stance_Button_Icon(stance);
    }

    /// <summary>
    /// 태세 버튼에 쓸 아이콘 이름입니다. **모든 태세가 값을 갖습니다.**
    ///
    /// 카드용과 나눈 이유:
    /// 카드는 '평상시와 다른 상태'만 아이콘으로 알리는 것이 목적이라
    /// 기본 태세를 비워 둡니다. 그런데 버튼은 다섯 개가 나란히 놓이므로,
    /// 하나만 비면 그 자리에 글자('전')가 들어가 혼자 튀어 보입니다.
    /// 실제로 그렇게 보인다는 지적이 있어 분리했습니다.
    /// </summary>
    /// <param name="stance">아이콘을 구할 태세입니다.</param>
    /// <returns>아이콘 스프라이트 이름입니다.</returns>
    public static string Get_Stance_Button_Icon(E_Army_Stance stance)
    {
        switch (stance)
        {
            case E_Army_Stance.ShieldWall: return "gui_skills_shields";
            case E_Army_Stance.SpearWall: return "gui_skills_polearms";
            case E_Army_Stance.Skirmish: return "gui_skills_stealth";
            case E_Army_Stance.Loose: return "gui_skills_dodging";

            // 전열 유지 = 갑옷.
            // 대열을 지키며 버틴다는 뜻에 가장 가깝고, 다른 태세나
            // 병종 아이콘과 겹치지 않습니다.
            default: return "gui_skills_armour";
        }
    }

    /// <summary>
    /// 이름으로 아이콘 스프라이트를 찾습니다.
    ///
    /// 처음 호출할 때만 실제로 찾고, 이후에는 캐시에서 꺼냅니다.
    /// 찾지 못하면 null이며, 경고를 한 번만 남깁니다.
    /// (매 프레임 그리는 경로라 반복 로그가 프레임을 잡아먹습니다)
    /// </summary>
    /// <param name="name">아이콘 스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (cache.TryGetValue(name, out Sprite cached)) return cached;

        Sprite sprite = Load(name);

        // 못 찾은 것도 캐시에 넣습니다.
        // 그러지 않으면 없는 아이콘을 매 프레임 다시 찾게 됩니다.
        cache[name] = sprite;

        if (sprite == null && reported.Add(name))
        {
            Debug.LogWarning($"[UI_Icons] 아이콘을 찾지 못했습니다: {name}");
        }

        return sprite;
    }

    /// <summary>
    /// 런타임에 아이콘을 공급하는 등록소입니다.
    ///
    /// 씬에 UI_Icon_Set 컴포넌트가 있으면 자기를 여기에 꽂습니다.
    /// 빌드에서는 이 경로만 동작합니다. (AssetDatabase는 에디터 전용입니다)
    /// </summary>
    private static UI_Icon_Set registry;

    /// <summary>
    /// 아이콘 공급원을 등록합니다. UI_Icon_Set이 자기 Awake에서 호출합니다.
    /// </summary>
    /// <param name="set">아이콘을 들고 있는 에셋입니다.</param>
    public static void Register(UI_Icon_Set set)
    {
        registry = set;

        // 공급원이 바뀌면 이전 캐시는 낡은 것을 가리킵니다.
        Clear();
    }

    /// <summary>
    /// 스프라이트를 실제로 불러옵니다.
    ///
    /// Resources 폴더를 쓰지 않는 이유:
    /// 5,000장이 넘는 타일을 Resources에 두면 빌드에 전부 포함되고
    /// 시작 시 색인이 만들어져 로딩이 길어집니다.
    ///
    /// 우선순위:
    ///   1) 등록소(UI_Icon_Set)  — 에디터와 빌드 양쪽에서 동작
    ///   2) AssetDatabase        — 에디터 전용 폴백
    ///
    /// 등록소를 먼저 보는 이유: 빌드에서 유일하게 동작하는 경로이므로,
    /// 에디터에서도 같은 경로를 타야 '에디터에서는 되는데 빌드에서는
    /// 안 되는' 상황을 미리 잡을 수 있습니다.
    /// </summary>
    /// <param name="name">찾을 스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    private static Sprite Load(string name)
    {
        if (registry != null)
        {
            Sprite found = registry.Find(name);
            if (found != null) return found;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets(
            $"{name} t:Sprite",
            new[] { "Assets/_Project/04.Art/01.Images/" + iconFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);

            // FindAssets는 부분 일치도 돌려주므로 이름이 정확한지 확인합니다.
            if (System.IO.Path.GetFileNameWithoutExtension(path) != name) continue;

            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
#endif
        return null;
    }

    /// <summary>
    /// 스프라이트의 UV 사각형을 구합니다.
    ///
    /// GUI.DrawTextureWithTexCoords에 넘겨 아틀라스에서 그 부분만
    /// 잘라 그리기 위한 값입니다. 자세한 배경은 클래스 요약을 보십시오.
    /// </summary>
    /// <param name="sprite">UV를 구할 스프라이트입니다.</param>
    /// <returns>0~1 범위의 UV 사각형입니다.</returns>
    public static Rect Get_UV(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return new Rect(0, 0, 1, 1);

        Rect r = sprite.textureRect;
        float w = sprite.texture.width;
        float h = sprite.texture.height;

        return new Rect(r.x / w, r.y / h, r.width / w, r.height / h);
    }

    /// <summary>
    /// 아이콘 하나를 지정한 영역에 그립니다.
    ///
    /// 스프라이트가 없으면 아무것도 그리지 않고 false를 돌려줍니다.
    /// 호출부는 그 경우 텍스트로 대신할 수 있습니다.
    /// </summary>
    /// <param name="rect">그릴 화면 영역입니다.</param>
    /// <param name="name">아이콘 이름입니다.</param>
    /// <param name="tint">덧입힐 색입니다.</param>
    /// <returns>실제로 그렸으면 true입니다.</returns>
    public static bool Draw(Rect rect, string name, Color tint)
    {
        Sprite sprite = Get(name);
        if (sprite == null || sprite.texture == null) return false;

        Color previous = GUI.color;
        GUI.color = tint;

        GUI.DrawTextureWithTexCoords(rect, sprite.texture, Get_UV(sprite), true);

        GUI.color = previous;
        return true;
    }

    /// <summary>캐시를 비웁니다. 씬을 다시 시작할 때 호출합니다.</summary>
    public static void Clear()
    {
        cache.Clear();
        reported.Clear();
    }
}
