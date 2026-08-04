using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 대규모 전투 씬을 실행 파일로 빌드하는 CLI 도구입니다.
///
/// 왜 필요한가:
/// 지금까지의 모든 측정은 에디터 안에서 이루어졌습니다. 그런데 에디터에는
/// 게임과 무관한 비용이 상시로 얹혀 있습니다.
///   - Scene view가 두 번째 카메라로 씬을 다시 그림
///   - Inspector / Hierarchy가 매 프레임 갱신
///   - 도메인 리로드 훅, 에셋 감시, 외부 브리지 플러그인
///
/// 실측에서 시뮬레이션은 틱당 11.57 ms인데 프로브가 재는 '틱 간격 전체'는
/// 스파이크가 109회(17%)에 최악 79.77 ms였습니다. 즉 스파이크의 95%가
/// 시뮬레이션 바깥입니다. 그것이 렌더링인지 에디터 오버헤드인지는
/// 에디터 안에서 측정하는 한 절대 구분되지 않습니다.
///
/// 빌드에는 Scene view도 Inspector도 없습니다. 따라서
///   (에디터 스파이크) - (빌드 스파이크) = 에디터 오버헤드
/// 가 되고, 빌드에도 스파이크가 남으면 그것이 진짜 렌더링 비용입니다.
///
/// 사용법:
///   Unity.exe -batchmode -quit -projectPath . \
///             -executeMethod Mass_Battle_Build.Build_From_CLI \
///             -buildOut "E:/GamePJ/RTXX/BuildTest/RTXX_Test.exe"
/// </summary>
public static class Mass_Battle_Build
{
    /// <summary>빌드에 담을 씬의 경로입니다.</summary>
    private const string scenePath = "Assets/_Project/01.Scenes/Scene_MassBattle.unity";

    /// <summary>기본 출력 경로입니다. -buildOut으로 덮어쓸 수 있습니다.</summary>
    private const string defaultOutput = "BuildTest/RTXX_Test.exe";

    /// <summary>
    /// 대규모 전투 씬만 담은 실행 파일을 만듭니다.
    ///
    /// 빌드 설정(EditorBuildSettings)을 영구히 바꾸지 않습니다.
    /// BuildPlayerOptions.scenes로 직접 넘기면 프로젝트 설정을 건드리지 않고도
    /// 원하는 씬만 담을 수 있습니다. 측정용 빌드가 프로젝트 상태를 오염시키면
    /// 다음 사람이 영문을 모른 채 그 설정을 물려받게 됩니다.
    /// </summary>
    public static void Build_From_CLI()
    {
        string output = defaultOutput;

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildOut") output = args[i + 1];
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,

            // 개발 빌드로 만듭니다.
            //
            // 왜 릴리스가 아닌가:
            // 이 빌드의 목적은 '측정'입니다. 개발 빌드여야 Debug.Log가 남고
            // 프로파일러를 붙일 수 있습니다. 다만 개발 빌드는 릴리스보다
            // 조금 느리므로, 여기서 나온 수치는 최종 성능이 아니라
            // '에디터와의 차이'를 보는 용도로만 읽어야 합니다.
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[MassBattleBuild] 결과: {summary.result} " +
                  $"({summary.totalSize / (1024 * 1024)} MB, " +
                  $"{summary.totalTime.TotalSeconds:F1}초)");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[MassBattleBuild] 빌드 실패: {summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }
}
