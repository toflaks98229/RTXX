using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 단계에서 순간이동한 유닛이 '제자리를 목적지로' 들고 있는지 확인합니다.
///
/// 왜 필요한가:
/// 배치 중에는 시뮬레이션 틱이 멈춰 있어 화면상 아무 문제가 없어 보입니다.
/// 그런데 순간이동(Place_At)이 위치만 옮기고 unit_Data.location(가야 할 곳)을
/// 그대로 두면, 전투가 시작되는 첫 틱에 전원이 옛 목표를 향해 걸어갑니다.
///
/// 증상은 "진형이 한 번 뭉개졌다 펴진다"로 나타납니다. 태세를 넓힌 직후라면
/// 되돌아감이 '좁아지는' 모습이라 더 눈에 띕니다.
///
/// 기존 Deploy_Snap_Probe는 '유닛이 움직였는가'만 봅니다. 그래서 이 결함을
/// 그대로 통과시켰습니다. 여기서는 **옮겨 놓은 자리와 가야 할 자리가
/// 일치하는가**를 잽니다.
///
/// 확인하는 것:
///   1) 배치 중 태세를 바꾼 뒤 position 과 location 이 일치하는가
///   2) 슬롯 배정(targetSlotIndex)과 이동 상태가 남아 있지 않은가
///   3) 부대 기준점이 유닛들과 같은 자리에 있는가 (슬롯이 따로 놀지 않는가)
///
/// 플레이 중에 실행해야 합니다. 유닛은 실행 시 생성됩니다.
/// </summary>
public static class Deploy_Target_Probe
{
    /// <summary>순간이동 후 유닛의 목적지가 제자리인지 검사합니다.</summary>
    [MenuItem("RTXX/검증: 배치 후 유닛 목표 정합")]
    public static void Verify()
    {
        Army army = Find_Army();

        if (army == null)
        {
            Debug.LogError("[배치목표] 유닛이 생성된 부대가 없습니다. " +
                           "플레이 중에 실행하십시오.");
            return;
        }

        Debug.Log($"[배치목표] 부대 '{army.name}'  유닛 {army.units.Count}명  " +
                  $"배치단계 = {Battle_Manager.bdeploying}");

        E_Army_Stance original = army.GetStance();

        // 간격이 크게 달라지는 두 태세를 왕복합니다.
        // 되돌아감이 있다면 그 폭이 클수록 크게 드러납니다.
        E_Army_Stance other = original == E_Army_Stance.Skirmish
            ? E_Army_Stance.ShieldWall
            : E_Army_Stance.Skirmish;

        army.Set_Stance(other);

        Report(army, $"{original} -> {other}");

        army.Set_Stance(original);
    }

    /// <summary>한 부대의 목표 정합을 재고 결과를 남깁니다.</summary>
    /// <param name="army">검사할 부대입니다.</param>
    /// <param name="label">어떤 전환을 검사했는지 표시할 이름입니다.</param>
    private static void Report(Army army, string label)
    {
        // 판정은 Deploy_Target_Check 한 곳에만 있습니다.
        // 배치모드(-deployCheck)도 같은 함수를 쓰므로 두 경로가 어긋날 수 없습니다.
        Deploy_Target_Check.Result result = Deploy_Target_Check.Measure(army);

        if (result.counted == 0)
        {
            Debug.LogError("[배치목표] 검사할 유닛이 없습니다.");
            return;
        }

        string text = Deploy_Target_Check.Describe(result, $"태세 {label}");

        if (result.IsClean) Debug.Log(text);
        else Debug.LogError(text);
    }

    /// <summary>유닛이 생성된 부대를 찾습니다.</summary>
    /// <returns>찾은 부대이며, 없으면 null입니다.</returns>
    private static Army Find_Army()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        for (int i = 0; i < armies.Length; i++)
        {
            if (armies[i] == null) continue;
            if (armies[i].units == null) continue;
            if (armies[i].units.Count > 1) return armies[i];
        }

        return null;
    }
}
