using UnityEditor;
using UnityEngine;

/// <summary>
/// 태세 전환이 실제로 대열 재정비를 일으키는지 확인하는 검증 도구입니다.
///
/// 왜 필요한가:
/// "버튼으로 태세를 바꿔도 대열을 다시 잡지 않는다"는 증상이 보고되었고,
/// 원인은 Army.Set_Stance가 열거형만 바꾸고 끝났기 때문이었습니다.
/// 실제 재정비는 Army_Data._Update의 Idle 분기에서 timer_Reformation이
/// 만료될 때(기본 3초)만 발동했습니다.
///
/// 이 도구는 그 수정이 유지되는지를 코드로 확인합니다. 눈으로 보는 검증은
/// "3초 기다렸더니 되더라"와 "즉시 되더라"를 구분하기 어렵기 때문입니다.
///
/// 확인하는 것:
///   1) 태세를 바꾸면 breformation이 그 자리에서 켜지는가
///   2) 같은 태세를 다시 지시하면 재발동하지 않는가 (대열 들썩임 방지)
///   3) 이동 중에는 재정비가 걸리지 않는가 (제자리 맴돌기 방지)
/// </summary>
public static class Stance_Reform_Probe
{
    /// <summary>태세 전환이 재정비를 일으키는지 검사합니다.</summary>
    [MenuItem("RTXX/검증: 태세 전환 재정비")]
    public static void Verify()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsSortMode.None);

        if (armies.Length == 0)
        {
            Debug.LogError("[Stance] 씬에 부대가 없습니다. 전투 씬을 연 뒤 실행하십시오.");
            return;
        }

        Army army = armies[0];

        int pass = 0;
        int fail = 0;

        // --- 1. 태세를 바꾸면 즉시 재정비가 예약되는가 ---
        //
        // 현재와 다른 태세를 골라야 의미가 있습니다.
        E_Army_Stance from = army.GetStance();
        E_Army_Stance to = from == E_Army_Stance.Line
            ? E_Army_Stance.ShieldWall
            : E_Army_Stance.Line;

        army.army_Data.e_Army_Move = E_Army_Move.Idle;
        army.army_Data.breformation = false;

        army.Set_Stance(to);

        if (army.army_Data.breformation) { pass++; Report(true, "태세 전환 시 즉시 재정비 예약"); }
        else { fail++; Report(false, "태세 전환 시 즉시 재정비 예약"); }

        // --- 2. 같은 태세를 다시 지시하면 재발동하지 않는가 ---
        //
        // AI는 판단 주기마다(기본 2초) 같은 태세를 다시 지시합니다.
        // 그때마다 재정비가 걸리면 대열이 자리를 잡지 못하고 들썩입니다.
        army.army_Data.breformation = false;
        army.Set_Stance(to);

        if (!army.army_Data.breformation) { pass++; Report(true, "동일 태세 재지시는 무시"); }
        else { fail++; Report(false, "동일 태세 재지시는 무시"); }

        // --- 3. 대기가 아닌 모든 상태에서 재정비가 억제되는가 ---
        //
        // 이동 중에 대열을 다시 잡으면 목적지로 가던 유닛이 제자리에서
        // 맴돕니다. 돌격·패주도 마찬가지입니다.
        //
        // 네 가지를 모두 확인하는 이유:
        // 예전에는 Move와 MoveToTarget만 처리하고 MoveCharge/MoveEscape는
        // 빠져 있어서, 돌격·패주 중에는 breformation을 아무도 내려 주지
        // 않았습니다. 하나만 검사하면 이 구멍을 놓칩니다.
        E_Army_Move[] moving =
        {
            E_Army_Move.Move,
            E_Army_Move.MoveToTarget,
            E_Army_Move.MoveCharge,
            E_Army_Move.MoveEscape,
        };

        for (int i = 0; i < moving.Length; i++)
        {
            army.army_Data.breformation = true;
            army.army_Data.e_Army_Move = moving[i];

            army.army_Data._Update();

            // _Update_Morale이 상태를 바꿀 수 있으므로 실제로 무엇이
            // 검사되었는지 함께 적습니다. 대기로 바뀌었다면 이 항목은
            // 판정 대상이 아닙니다.
            E_Army_Move actual = army.army_Data.e_Army_Move;

            if (actual == E_Army_Move.Idle)
            {
                Debug.Log($"  [건너뜀] {moving[i]} -> 사기 판정이 Idle로 되돌림");
                continue;
            }

            if (!army.army_Data.breformation)
            {
                pass++; Report(true, $"{actual} 중에는 재정비 억제");
            }
            else
            {
                fail++; Report(false, $"{actual} 중에는 재정비 억제");
            }
        }

        // 검사로 바꾼 상태를 되돌립니다.
        // 씬을 저장하지 않더라도 에디터에 남아 혼란을 주지 않도록 합니다.
        army.army_Data.e_Army_Move = E_Army_Move.Idle;
        army.army_Data.e_Army_Stance = from;
        army.army_Data.breformation = false;

        Debug.Log($"[Stance] 태세 재정비 검증: 통과 {pass} / 실패 {fail}");
    }

    /// <summary>검사 결과 한 줄을 남깁니다.</summary>
    /// <param name="bok">통과 여부입니다.</param>
    /// <param name="label">검사 이름입니다.</param>
    private static void Report(bool bok, string label)
    {
        if (bok) Debug.Log($"  [통과] {label}");
        else Debug.LogError($"  [실패] {label}");
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
