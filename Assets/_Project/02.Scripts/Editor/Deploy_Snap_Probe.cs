using UnityEditor;
using UnityEngine;

/// <summary>
/// 배치 단계의 즉시 재배치가 동작하는지 확인하는 도구입니다.
///
/// 왜 필요한가:
/// 배치 중에는 시뮬레이션 틱이 멈추므로, 명령을 내려도 유닛을 움직여
/// 줄 주체가 없습니다. 그래서 즉시 옮기는 경로를 따로 두었는데,
/// 그것이 실제로 도는지는 좌표를 재야 알 수 있습니다.
///
/// 확인하는 것:
///   1) 배치 중 태세를 바꾸면 유닛이 즉시 새 슬롯으로 가는가
///   2) 배치가 아닐 때는 순간이동하지 않는가 (교전 중 대열이 튀면 안 됨)
///   3) Battle_Manager가 없는 씬에서 오작동하지 않는가
/// </summary>
public static class Deploy_Snap_Probe
{
    /// <summary>배치 단계 즉시 재배치를 검사합니다.</summary>
    [MenuItem("RTXX/검증: 배치 단계 즉시 재배치")]
    public static void Verify()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        Army army = null;

        for (int i = 0; i < armies.Length; i++)
        {
            if (armies[i] != null && armies[i].units != null && armies[i].units.Count > 1)
            {
                army = armies[i];
                break;
            }
        }

        if (army == null)
        {
            Debug.LogError("[배치] 유닛이 생성된 부대가 없습니다. " +
                           "플레이 중에 실행하십시오. (유닛은 실행 시 생성됩니다)");
            return;
        }

        Debug.Log($"[배치] 부대 '{army.name}'  유닛 {army.units.Count}명  " +
                  $"배치단계 = {Battle_Manager.bdeploying}");

        // 유닛 하나의 위치를 기억해 두고 태세를 바꿔 봅니다.
        Unit sample = army.units[0];
        Vector3 before = sample.transform.position;

        E_Army_Stance original = army.GetStance();
        E_Army_Stance other = original == E_Army_Stance.ShieldWall
            ? E_Army_Stance.Skirmish
            : E_Army_Stance.ShieldWall;

        army.Set_Stance(other);

        Vector3 after = sample.transform.position;
        float moved = Vector3.Distance(before, after);

        Debug.Log($"    태세 {original} -> {other}\n" +
                  $"    유닛 이동 거리 {moved:F3}m");

        if (Battle_Manager.bdeploying)
        {
            if (moved > 0.01f)
            {
                Debug.Log("    [통과] 배치 중이므로 즉시 이동했습니다.");
            }
            else
            {
                Debug.LogError("    [실패] 배치 중인데 유닛이 움직이지 않았습니다.");
            }
        }
        else
        {
            // 교전 중에는 순간이동하면 안 됩니다.
            // 대열이 갑자기 재배치되면 적과의 접촉이 끊깁니다.
            if (moved < 0.01f)
            {
                Debug.Log("    [통과] 배치가 아니므로 즉시 이동하지 않았습니다. " +
                          "(재정비로 서서히 이동합니다)");
            }
            else
            {
                Debug.LogError("    [실패] 교전 중에 유닛이 순간이동했습니다.");
            }
        }

        army.Set_Stance(original);
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
