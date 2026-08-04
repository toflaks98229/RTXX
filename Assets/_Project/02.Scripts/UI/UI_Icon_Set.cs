using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD가 쓰는 아이콘들을 씬에 실어 나르는 컴포넌트입니다.
///
/// 왜 필요한가:
/// UI_Icons는 이름으로 스프라이트를 찾는데, 에디터에서는 AssetDatabase로
/// 찾을 수 있지만 빌드에는 그 API가 없습니다. Resources 폴더를 쓰면
/// 타일 5,000장이 통째로 빌드에 들어가고 시작이 느려집니다.
///
/// 그래서 '실제로 쓰는 아이콘만' 이 컴포넌트가 참조로 들고 있다가
/// 시작 시 UI_Icons에 넘깁니다. 참조된 것만 빌드에 포함되므로
/// 필요한 몇 장만 딸려 갑니다.
///
/// 목록 채우기: 인스펙터에서 직접 넣거나,
/// 'RTXX/HUD 아이콘 채우기' 메뉴로 자동 수집할 수 있습니다.
/// </summary>
public class UI_Icon_Set : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// HUD가 쓰는 아이콘 스프라이트들입니다.
    ///
    /// 이름으로 조회하므로 순서는 중요하지 않습니다.
    /// 여기 참조된 것만 빌드에 포함됩니다.
    /// </summary>
    [Tooltip("HUD가 쓰는 아이콘입니다. 'RTXX/HUD 아이콘 채우기'로 자동 수집할 수 있습니다.")]
    public List<Sprite> icons = new List<Sprite>();

    // 비공개 멤버 변수
    /// <summary>이름 -> 스프라이트 조회표입니다. Awake에서 한 번만 만듭니다.</summary>
    private Dictionary<string, Sprite> lookup;

    // Unity 이벤트 함수
    /// <summary>조회표를 만들고 자기를 아이콘 공급원으로 등록합니다.</summary>
    private void Awake()
    {
        Build_Lookup();
        UI_Icons.Register(this);
    }

    /// <summary>
    /// 등록을 해제합니다.
    ///
    /// 정적 참조이므로 씬을 옮겨도 살아남습니다. 파괴된 컴포넌트를
    /// 가리킨 채로 두면 다음 세션이 빈 목록을 보게 됩니다.
    /// </summary>
    private void OnDestroy()
    {
        UI_Icons.Clear();
    }

    // 공개 메서드
    /// <summary>
    /// 이름으로 아이콘을 찾습니다.
    /// </summary>
    /// <param name="name">찾을 스프라이트 이름입니다.</param>
    /// <returns>찾은 스프라이트이며, 없으면 null입니다.</returns>
    public Sprite Find(string name)
    {
        if (lookup == null) Build_Lookup();

        return lookup.TryGetValue(name, out Sprite sprite) ? sprite : null;
    }

    // 비공개 메서드
    /// <summary>목록에서 이름 조회표를 만듭니다.</summary>
    private void Build_Lookup()
    {
        lookup = new Dictionary<string, Sprite>(icons.Count);

        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null) continue;

            // 같은 이름이 둘이면 앞의 것을 씁니다.
            // 목록에 중복이 들어가도 조용히 넘어가게 합니다.
            if (lookup.ContainsKey(icons[i].name)) continue;

            lookup[icons[i].name] = icons[i];
        }
    }
}
