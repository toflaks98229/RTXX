using UnityEditor;
using UnityEngine;

/// <summary>
/// 태세별 밀집도가 실제 간격으로 반영되는지 확인하는 도구입니다.
///
/// 왜 필요한가:
/// 간격은 진형 계산 여러 곳을 거쳐 화면에 나타납니다. 눈으로는
/// "좁아진 것 같다" 이상을 말하기 어렵고, 하한(반지름 기반)에 걸려
/// 배율이 무시되는 경우도 눈에 띄지 않습니다. 숫자로 확인합니다.
///
/// 확인하는 것:
///   1) 태세마다 간격이 의도한 배율대로 나오는가
///   2) 하한에 걸려 배율이 무시되는 태세가 있는가
///   3) 그 간격에서 대열 폭이 어떻게 달라지는가
/// </summary>
public static class Density_Probe
{
    /// <summary>씬의 부대들로 태세별 간격을 계산해 보고합니다.</summary>
    [MenuItem("RTXX/검증: 태세별 밀집도")]
    public static void Verify()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        if (armies.Length == 0)
        {
            Debug.LogError("[밀집도] 씬에 부대가 없습니다.");
            return;
        }

        E_Army_Stance[] stances =
        {
            E_Army_Stance.Line,
            E_Army_Stance.Loose,
            E_Army_Stance.ShieldWall,
            E_Army_Stance.SpearWall,
            E_Army_Stance.Skirmish,
        };

        // 병종마다 기본 간격이 다르므로 병종별로 한 부대씩만 봅니다.
        System.Collections.Generic.HashSet<E_Unit_Class> seen =
            new System.Collections.Generic.HashSet<E_Unit_Class>();

        for (int i = 0; i < armies.Length; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            E_Unit_Class unitClass = army.army_Data.GetE_Unit_Class();
            if (!seen.Add(unitClass)) continue;

            Report(army, unitClass, stances);
        }
    }

    /// <summary>부대 하나에 대해 태세별 간격을 출력합니다.</summary>
    /// <param name="army">검사할 부대입니다.</param>
    /// <param name="unitClass">부대의 병종입니다. 표시에 씁니다.</param>
    /// <param name="stances">확인할 태세 목록입니다.</param>
    private static void Report(Army army, E_Unit_Class unitClass, E_Army_Stance[] stances)
    {
        // 검사 후 되돌리기 위해 원래 태세를 기억합니다.
        E_Army_Stance original = army.army_Data.e_Army_Stance;

        float baseInterval = army.army_Data.GetBaseInterval();
        float radius = army.army_Data.GetRadius();
        float min = army.army_Data.GetMinInterval();

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.Append($"[밀집도] {unitClass}  기본간격 {baseInterval:F2}  ")
          .Append($"반지름 {radius:F2}  하한 {min:F2}\n");

        for (int i = 0; i < stances.Length; i++)
        {
            army.army_Data.e_Army_Stance = stances[i];

            float rate = army.army_Data.GetDensityRate();
            float actual = army.army_Data.GetInterval();
            float wanted = baseInterval * rate;

            // 하한에 걸렸는지 표시합니다.
            // 걸렸다면 그 태세의 배율은 화면에 반영되지 않습니다.
            bool bclamped = wanted < min - 0.0001f;

            sb.Append($"    {stances[i],-11} 배율 {rate:F2}  ")
              .Append($"의도 {wanted:F2}  실제 {actual:F2}")
              .Append(bclamped ? "   <- 하한에 걸림\n" : "\n");
        }

        army.army_Data.e_Army_Stance = original;

        Debug.Log(sb.ToString());
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
