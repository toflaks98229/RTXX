using UnityEditor;
using UnityEngine;

/// <summary>
/// 진형 슬롯 인덱스 규약과 슬롯 이탈을 확인하는 도구입니다.
///
/// 선두 지정 시스템의 1단계 검증입니다. 이 단계에서는 조회 API만
/// 추가했고 이동 로직은 그대로이므로, **동작이 바뀌지 않았는지**와
/// **규약이 실제 좌표와 맞는지**를 확인하는 것이 목적입니다.
///
/// 확인하는 것:
///   1) 두 유닛이 같은 슬롯을 들고 있지 않은가
///   2) file/rank 유도가 실제 슬롯 좌표와 맞는가
///      (Formation_Job의 배치 순서와 Formation_Slots의 역변환이 같은가)
///   3) 선두 조회가 성립하는가
///   4) 유닛이 자기 슬롯에서 얼마나 벗어나 있는가 (전환 판단 근거)
///
/// 4번이 다음 단계의 판단 근거입니다.
/// 이탈이 크면 슬롯은 사실상 느슨한 안내선이므로 선두 추종으로 바꿔도
/// 잃을 것이 적습니다. 작으면 강체 결합에 의존하는 동작(돌격 접촉면,
/// 벽 태세 밀집도)이 있다는 뜻이라 그쪽부터 확인해야 합니다.
///
/// 플레이 중에 실행해야 합니다. 유닛과 슬롯은 실행 시 만들어집니다.
/// 부대에 이동 명령을 한 번 내린 뒤 실행하면 더 의미 있는 값이 나옵니다.
/// (명령 전에는 슬롯 배정이 없어 표본이 잡히지 않습니다)
/// </summary>
public static class Formation_Index_Probe
{
    /// <summary>씬의 모든 부대에 대해 진형 인덱스 규약을 검사합니다.</summary>
    [MenuItem("RTXX/검증: 진형 인덱스 규약과 슬롯 이탈")]
    public static void Verify()
    {
        Army[] armies = Object.FindObjectsByType<Army>(FindObjectsInactive.Include);

        if (armies.Length == 0)
        {
            Debug.LogError("[진형인덱스] 씬에 부대가 없습니다.");
            return;
        }

        int checkedArmies = 0;
        int failed = 0;

        double deviationSum = 0.0;
        int samples = 0;
        float worst = 0.0f;

        for (int i = 0; i < armies.Length; i++)
        {
            Formation_Index_Check.Result result =
                Formation_Index_Check.Measure(armies[i]);

            if (!result.bvalid) continue;

            checkedArmies++;

            deviationSum += result.deviationSum;
            samples += result.samples;
            if (result.worstDeviation > worst) worst = result.worstDeviation;

            if (result.IsClean) continue;

            failed++;

            // 처음 몇 개만 자세히 남깁니다. 60개가 전부 실패하면 로그가 넘칩니다.
            if (failed <= 3)
            {
                Debug.LogError(Formation_Index_Check.Describe(result, armies[i].name));
            }
        }

        if (checkedArmies == 0)
        {
            Debug.LogError("[진형인덱스] 진형이 만들어진 부대가 없습니다. " +
                           "플레이 중에, 이동 명령을 한 번 내린 뒤 실행하십시오.");
            return;
        }

        string summary =
            $"[진형인덱스] 검사 부대 {checkedArmies}개 / 실패 {failed}개\n" +
            $"    슬롯 이탈 평균 " +
            $"{(samples > 0 ? deviationSum / samples : 0.0):F2} m / " +
            $"최대 {worst:F2} m (표본 {samples}회)";

        if (failed == 0)
        {
            Debug.Log(summary + "\n    [통과] 인덱스 규약이 슬롯 좌표와 일치합니다.");
        }
        else
        {
            Debug.LogError(summary +
                           "\n    [실패] Formation_Job의 배치 순서와 " +
                           "Formation_Slots의 역변환을 대조하십시오.");
        }
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
