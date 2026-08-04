using UnityEditor;
using UnityEngine;

/// <summary>
/// 태세를 바꿨을 때 진형이 올바르게 다시 만들어지는지 확인합니다.
///
/// 확인하는 것:
///   1) 간격이 태세를 따라 바뀌는가
///   2) **대열이 옆으로 밀려나지 않는가** (반복해서 눌러도 제자리인가)
///
/// 2번이 특히 중요합니다. Formation_Data.position은 이름과 달리 중심이
/// 아니라 첫 슬롯을 담고 있는데, Set_Formation은 넘어온 값을 중심처럼
/// 다룹니다. 그 값을 그대로 되먹이면 누를 때마다 절반 폭씩 밀려납니다.
/// 눈으로는 "왜 자꾸 옆으로 가지?" 정도로만 보이므로 숫자로 잡습니다.
/// </summary>
public static class Stance_Rebuild_Probe
{
    /// <summary>태세 전환 시 진형 재구성과 위치 유지를 검사합니다.</summary>
    [MenuItem("RTXX/검증: 태세 전환 시 진형 재구성")]
    public static void Verify()
    {
        Army army = Find_Army();

        if (army == null)
        {
            Debug.LogError("[재구성] 진형이 만들어진 부대가 없습니다. " +
                           "플레이 중에 실행하십시오.");
            return;
        }

        E_Army_Stance original = army.GetStance();

        Debug.Log($"[재구성] 부대 '{army.name}'  슬롯 {army.SlotCount}개");

        // --- 1. 태세별 간격 ---
        E_Army_Stance[] stances =
        {
            E_Army_Stance.Line,
            E_Army_Stance.ShieldWall,
            E_Army_Stance.Skirmish,
        };

        for (int i = 0; i < stances.Length; i++)
        {
            army.Set_Stance(stances[i]);

            float gap = First_Gap(army);
            float interval = army.army_Data.GetInterval();
            bool bok = Mathf.Abs(gap - interval) < 0.05f;

            Debug.Log($"    {stances[i],-11} 설정간격 {interval:F3}  " +
                      $"실제 슬롯간격 {gap:F3}  {(bok ? "일치" : "<- 어긋남")}");
        }

        // --- 2. 반복 전환 시 위치가 유지되는가 ---
        //
        // 같은 태세를 왕복해서 여러 번 누릅니다. 원래 자리로 돌아와야
        // 합니다. 밀려나는 버그가 있으면 왕복할수록 멀어집니다.
        army.Set_Stance(E_Army_Stance.Line);

        Vector3 before = Center(army);

        const int rounds = 5;

        for (int i = 0; i < rounds; i++)
        {
            army.Set_Stance(E_Army_Stance.ShieldWall);
            army.Set_Stance(E_Army_Stance.Line);
        }

        Vector3 after = Center(army);
        float drift = Vector3.Distance(before, after);

        Debug.Log($"    왕복 {rounds}회 후 중심 이동 {drift:F4}m  " +
                  $"({before} -> {after})");

        if (drift < 0.05f)
        {
            Debug.Log("    [통과] 대열이 제자리를 지킵니다.");
        }
        else
        {
            Debug.LogError($"    [실패] 대열이 {drift:F2}m 밀려났습니다. " +
                           "Rebuild_Formation_For_Stance의 기준점 계산을 확인하십시오.");
        }

        army.Set_Stance(original);
    }

    /// <summary>진형이 만들어진 부대를 찾습니다.</summary>
    /// <returns>찾은 부대이며, 없으면 null입니다.</returns>
    private static Army Find_Army()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        for (int i = 0; i < armies.Length; i++)
        {
            if (armies[i] != null && armies[i].SlotCount > 1) return armies[i];
        }

        return null;
    }

    /// <summary>진형 슬롯들의 평균 위치입니다.</summary>
    /// <param name="army">측정할 부대입니다.</param>
    /// <returns>대열의 중심입니다.</returns>
    private static Vector3 Center(Army army)
    {
        int count = army.SlotCount;
        if (count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < count; i++) sum += army.Get_Slot_World(i);

        return sum / count;
    }

    /// <summary>가장 앞줄 두 슬롯 사이의 거리를 잽니다.</summary>
    /// <param name="army">측정할 부대입니다.</param>
    /// <returns>슬롯 간격입니다. 잴 수 없으면 0입니다.</returns>
    private static float First_Gap(Army army)
    {
        if (army.SlotCount < 2) return 0.0f;

        return Vector3.Distance(army.Get_Slot_World(0), army.Get_Slot_World(1));
    }

    /// <summary>배치모드에서 검증하고 종료합니다.</summary>
    public static void Verify_From_CLI()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/_Project/01.Scenes/Scene1.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        Verify();
        EditorApplication.Exit(0);
    }
}
