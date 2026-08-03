using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대 목록과 armyIndex 색인표를 '함께' 소유하는 등록소입니다.
///
/// 왜 필요한가:
/// 예전에는 두 가지가 서로 다른 곳에서 따로 정해졌습니다.
///
///   1) Army.armyIndexTable  : Controller.Start에서 Set_Army_Index_Table(armies)
///   2) Unit_Data.armyIndex  : 그 아래의 별도 루프에서 armies를 다시 순회하며 부여
///
/// 두 루프가 같은 리스트를 같은 순서로 도는 한 값은 일치했지만, 그것을 보장하는
/// 장치는 주석뿐이었습니다. 부대를 런타임에 추가하거나(증원) 순서를 바꾸면
/// 색인표만 낡은 상태가 되는데, 이 어긋남은 컴파일 오류도 런타임 예외도 내지
/// 않습니다. 그저 킬이 엉뚱한 부대에 기록되고 아무도 눈치채지 못합니다.
///
/// 이 클래스는 그 어긋남이 '가능한 경로 자체'를 없앱니다.
/// 색인표와 유닛의 armyIndex는 오직 Rebuild_Indices() 한 곳에서 함께 정해지므로,
/// 둘이 따로 놀 방법이 없습니다.
///
/// 사용 규약:
///   Register/Unregister로 목록을 바꾸면 dirty로 표시됩니다.
///   시뮬레이션이 색인을 읽기 전에 반드시 Rebuild_Indices()를 호출하십시오.
///   (Controller가 Start와 매 틱 앞에서 호출합니다. 깨끗하면 즉시 반환합니다)
/// </summary>
public class Army_Registry
{
    /// <summary>등록된 부대들입니다. 인덱스가 곧 armyIndex입니다.</summary>
    private readonly List<Army> armies = new List<Army>();

    /// <summary>
    /// armyIndex -> Army 조회 배열입니다.
    ///
    /// armies 리스트를 직접 인덱싱해도 되지만, 배열로 스냅샷을 떠 두면
    /// 조회 경로가 리스트 변경과 분리되어 더 안전합니다.
    /// </summary>
    private Army[] indexTable;

    /// <summary>목록이 바뀌어 색인을 다시 만들어야 하는지 여부입니다.</summary>
    private bool bdirty = true;

    /// <summary>등록된 부대 목록입니다. 외부에서 순서를 바꾸지 못하도록 읽기 전용입니다.</summary>
    public IReadOnlyList<Army> Armies => armies;

    /// <summary>등록된 부대 수입니다.</summary>
    public int Count => armies.Count;

    /// <summary>
    /// 부대를 등록합니다. 이미 등록되어 있으면 아무 일도 하지 않습니다.
    /// 색인은 즉시 갱신되지 않고 다음 Rebuild_Indices()에서 함께 정해집니다.
    /// </summary>
    public void Register(Army army)
    {
        if (army == null) return;
        if (armies.Contains(army)) return;

        armies.Add(army);
        bdirty = true;
    }

    /// <summary>
    /// 부대를 등록 해제합니다.
    ///
    /// 주의: 제거하면 뒤쪽 부대들의 인덱스가 한 칸씩 당겨집니다.
    /// 그래서 반드시 Rebuild_Indices()로 전 유닛의 armyIndex를 다시 부여해야
    /// 합니다. 이 함수는 dirty 표시만 하고, 실제 재부여는 그쪽에서 일어납니다.
    /// </summary>
    public void Unregister(Army army)
    {
        if (army == null) return;
        if (!armies.Remove(army)) return;

        bdirty = true;
    }

    /// <summary>
    /// 여러 부대를 한 번에 등록합니다. 씬 시작 시 인스펙터 목록을 넘길 때 씁니다.
    /// </summary>
    public void Register_Range(List<Army> source)
    {
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            Register(source[i]);
        }
    }

    /// <summary>
    /// 색인표와 전 유닛의 armyIndex를 '함께' 다시 만듭니다.
    ///
    /// 이 함수가 이 클래스의 존재 이유입니다.
    /// 두 값이 한 루프에서 같은 i로 정해지므로 어긋날 수가 없습니다.
    ///
    /// 깨끗한 상태에서는 즉시 반환하므로 매 틱 호출해도 비용이 없습니다.
    /// </summary>
    /// <param name="bforce">깨끗해도 강제로 다시 만들지 여부입니다.</param>
    public void Rebuild_Indices(bool bforce = false)
    {
        if (!bdirty && !bforce) return;

        // 파괴된 부대(Unity의 == 오버로드로 null 판정)를 먼저 걷어냅니다.
        // 남겨 두면 색인표에 구멍이 생겨 Get()이 null을 돌려줍니다.
        for (int i = armies.Count - 1; i >= 0; i--)
        {
            if (armies[i] == null) armies.RemoveAt(i);
        }

        indexTable = armies.ToArray();

        // 색인표와 '같은 루프'에서 유닛의 armyIndex를 부여합니다.
        // 이 한 줄이 예전의 별도 루프를 대체하며, 그것이 정합성의 근거입니다.
        for (int i = 0; i < armies.Count; i++)
        {
            armies[i].Assign_Army_Index(i);
        }

        // Army의 정적 조회 경로도 같은 스냅샷으로 맞춥니다.
        // (Get_Army_By_Index가 이 표를 씁니다)
        Army.Set_Army_Index_Table(indexTable);

        bdirty = false;
    }

    /// <summary>
    /// armyIndex로 부대를 찾습니다. 범위를 벗어나면 null입니다.
    /// </summary>
    public Army Get(int armyIndex)
    {
        if (indexTable == null) return null;
        if (armyIndex < 0 || armyIndex >= indexTable.Length) return null;

        return indexTable[armyIndex];
    }

    /// <summary>
    /// 색인을 다시 만들어야 하는 상태인지 여부입니다.
    /// 검증 코드가 "읽기 전에 재구축했는가"를 확인할 때 씁니다.
    /// </summary>
    public bool IsDirty => bdirty;

    /// <summary>
    /// 등록소를 비웁니다. 씬을 다시 시작할 때 호출합니다.
    /// </summary>
    public void Clear()
    {
        armies.Clear();
        indexTable = null;
        bdirty = true;
    }

    // =====================================================================
    // 검증 (에디터 전용)
    //
    // 아래 검사들은 빌드에서 통째로 사라집니다.
    // 인덱스 계약이 깨지는 순간은 조용하므로, 개발 중에는 시끄러워야 합니다.
    // =====================================================================

    /// <summary>
    /// 색인표와 유닛들의 armyIndex가 실제로 일치하는지 검사합니다.
    ///
    /// 이 검사가 실패한다는 것은 곧 킬 귀속이 틀리고 있다는 뜻입니다.
    /// 조용히 넘어가면 전투 통계가 통째로 신뢰할 수 없게 되므로 크게 알립니다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void Assert_Integrity()
    {
        if (bdirty)
        {
            Debug.LogError("Army_Registry: 색인이 dirty인 상태로 조회되었습니다. " +
                           "시뮬레이션 틱 전에 Rebuild_Indices()를 호출해야 합니다.");
            return;
        }

        if (indexTable == null)
        {
            Debug.LogError("Army_Registry: 색인표가 만들어지지 않았습니다.");
            return;
        }

        if (indexTable.Length != armies.Count)
        {
            Debug.LogError($"Army_Registry: 색인표 길이({indexTable.Length})와 " +
                           $"부대 수({armies.Count})가 다릅니다.");
            return;
        }

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            if (indexTable[i] != army)
            {
                Debug.LogError($"Army_Registry: 색인표[{i}]가 실제 부대와 다릅니다.", army);
                continue;
            }

            // 이 부대의 유닛들이 전부 i를 들고 있어야 합니다.
            List<Unit> units = army.units;
            if (units == null) continue;

            for (int u = 0; u < units.Count; u++)
            {
                if (units[u] == null) continue;

                if (units[u].unit_Data.armyIndex != i)
                {
                    Debug.LogError(
                        $"Army_Registry: {army.name}의 유닛 armyIndex가 " +
                        $"{units[u].unit_Data.armyIndex}인데 실제 색인은 {i}입니다. " +
                        "킬 귀속이 엉뚱한 부대로 기록됩니다.", units[u]);

                    // 한 부대에서 하나만 알려도 원인 파악에는 충분합니다.
                    break;
                }
            }
        }
    }
}
