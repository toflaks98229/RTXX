using System.Collections;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 대규모 전투 씬을 배치모드(CLI)에서 실제로 돌려 검증하는 러너입니다.
///
/// 왜 필요한가:
/// 컴파일 성공은 문법이 맞다는 뜻일 뿐입니다. 이번 리팩토링은
///   - 물리 충돌 경로를 통째로 삭제하고
///   - 위치의 주인을 Transform에서 unit_Data로 일원화하고
///   - 부대 조회를 전수 순회에서 격자로 바꿨습니다
/// 전부 '예외 없이 조용히 틀릴 수 있는' 종류의 변경입니다.
///
/// 그래서 실제로 시뮬레이션을 돌려 봐야 합니다.
/// 이 러너는 에디터를 띄우지 않고 플레이 모드를 실행해,
/// 사람이 보지 않아도 결과가 로그에 남게 합니다.
///
/// 사용법:
///   Unity.exe -batchmode -projectPath . -executeMethod Mass_Battle_Runner.Run_From_CLI
///
/// 주의: -quit을 함께 주면 플레이 모드가 시작되기 전에 에디터가 종료됩니다.
///       이 러너가 스스로 종료하므로 -quit은 빼야 합니다.
/// </summary>
public static class Mass_Battle_Runner
{
    /// <summary>실행할 대규모 전투 씬의 경로입니다.</summary>
    private const string scenePath = "Assets/_Project/01.Scenes/Scene_MassBattle.unity";

    /// <summary>측정할 틱 수입니다. 60틱 = 시뮬레이션 1초입니다.</summary>
    private static int runTicks = 600;

    /// <summary>결정론 검증용 시드입니다.</summary>
    private static uint seed = 12345;

    /// <summary>결과를 적을 파일 경로입니다.</summary>
    private static string outputPath = "massbattle_result.txt";

    /// <summary>
    /// 배치모드에서 씬을 열고 플레이 모드로 진입해 검증을 시작합니다.
    ///
    /// 주의: -quit을 함께 주면 플레이 모드가 시작되기 전에 에디터가 종료됩니다.
    /// 종료는 Mass_Battle_Probe가 스스로 처리하므로 -quit을 빼야 합니다.
    /// </summary>
    public static void Run_From_CLI()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-runTicks") int.TryParse(args[i + 1], out runTicks);
            if (args[i] == "-seed") uint.TryParse(args[i + 1], out seed);
            if (args[i] == "-out") outputPath = args[i + 1];
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 시드를 덮어씁니다. 같은 시드로 두 번 돌려 결과를 비교하기 위함입니다.
        Controller controller = Object.FindAnyObjectByType<Controller>();
        if (controller != null)
        {
            controller.simulationSeed = seed;

            // 대조 실험: -noGroundSync를 주면 지면 동기화를 끕니다.
            // 이 계층이 실제로 일을 하는지 확인하기 위한 것입니다.
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-noGroundSync") controller.bdisableGroundSync = true;
            }

            // 패주 검증 플래그는 프로브가 런타임에 처리합니다.
            //
            // 여기서 사기를 낮춰도 소용이 없습니다. 플레이 모드 진입 후
            // Army._Start()가 사기를 최댓값으로 되돌리기 때문입니다.
            // 따라서 시뮬레이션이 시작된 뒤에 낮춰야 합니다.
        }

        // 배치 단계를 건너뛰고 바로 전투를 시작하게 합니다.
        // 배치모드에는 키 입력이 없으므로 Space를 누를 수 없습니다.
        Battle_Manager manager = Object.FindAnyObjectByType<Battle_Manager>();
        if (manager != null) manager.buseDeployment = false;

        // 결과 기록과 종료는 Mass_Battle_Probe가 맡습니다.
        //
        // 왜 러너가 하지 않는가:
        // 배치모드에서는 플레이 중에 EditorApplication.update가 확실히
        // 돌지 않습니다. 실제로 시뮬레이션이 600틱을 넘겼는데도 러너의
        // 콜백이 그 사실을 보지 못해 영원히 대기했습니다.
        // 반면 FixedUpdate는 반드시 돌므로, 프로브가 직접 끝내는 편이 확실합니다.
        EditorApplication.EnterPlaymode();
    }

    /// <summary>플레이 모드 진입을 기다린 프레임 수입니다. 무한 대기를 막습니다.</summary>
    private static int framesWaited;
    /// <summary>플레이 모드에 한 번이라도 진입했는지 여부입니다.</summary>
    private static bool bstarted;

    /// <summary>
    /// 플레이 모드가 시작되기를 기다렸다가, 정해진 틱만큼 돈 뒤 결과를 남깁니다.
    ///
    /// 왜 코루틴이 아닌 EditorApplication.update인가:
    /// 플레이 모드 진입은 도메인 리로드를 동반하므로 코루틴이 끊깁니다.
    /// 에디터 콜백은 그 경계를 넘어 살아남습니다.
    /// </summary>
    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            // 아직 진입 전이면 기다립니다.
            // 진입 자체가 실패하는 경우를 대비해 상한을 둡니다.
            if (!bstarted)
            {
                if (framesWaited++ < 1200) return;

                Debug.LogError("[MassBattleRunner] 플레이 모드 진입에 실패했습니다.");
                Finish(1);
                return;
            }

            // 시작했다가 멈췄으면 끝난 것입니다.
            Finish(bwrote ? 0 : 1);
            return;
        }

        bstarted = true;

        Controller controller = Object.FindAnyObjectByType<Controller>();
        if (controller == null) return;

        // 목표 틱에 도달하면 결과를 남기고 플레이를 멈춥니다.
        //
        // 안전장치: 틱이 아예 진행되지 않는 경우(시뮬레이션이 멈춘 경우)에도
        // 영원히 기다리지 않도록 프레임 수에도 상한을 둡니다.
        playFrames++;

        bool bdone = Simulation_Clock.tick >= (uint)runTicks;
        bool btimeout = playFrames > runTicks * 10 + 3000;

        if (bdone || btimeout)
        {
            if (btimeout)
            {
                Debug.LogError($"[MassBattleRunner] 시간 초과. " +
                               $"틱 {Simulation_Clock.tick}/{runTicks}에서 멈췄습니다.");
            }

            Write_Result(controller);
            bwrote = true;
            EditorApplication.isPlaying = false;
        }
    }

    /// <summary>플레이 모드에서 지난 프레임 수입니다. 무한 대기를 막습니다.</summary>
    private static int playFrames;

    /// <summary>결과 파일을 실제로 남겼는지 여부입니다.</summary>
    private static bool bwrote;

    /// <summary>
    /// 시뮬레이션 상태의 지문(fingerprint)을 남깁니다.
    ///
    /// 왜 해시인가:
    /// 결정론을 확인하려면 두 실행의 결과가 '완전히 같은지'를 봐야 합니다.
    /// 유닛 9,600명의 좌표를 전부 비교하는 대신, 그 값들을 하나의 수로
    /// 압축해 비교합니다. 한 명의 위치가 조금이라도 다르면 해시가 달라집니다.
    ///
    /// 부동소수점 주의:
    /// 위치를 그대로 해시하면 아주 미세한 차이도 잡히는데, 그게 목적입니다.
    /// 결정론이 성립한다면 비트 단위로 같아야 하기 때문입니다.
    /// </summary>
    private static void Write_Result(Controller controller)
    {
        ulong hash = 1469598103934665603UL; // FNV-1a 오프셋
        int alive = 0;
        int totalHp = 0;

        var units = controller.units;
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null) continue;
                if (u.IsDead()) continue;

                alive++;
                totalHp += Mathf.RoundToInt(u.unit_Data.HP);

                hash = Mix(hash, u.unit_Data.position.x);
                hash = Mix(hash, u.unit_Data.position.z);
                hash = Mix(hash, u.unit_Data.HP);
            }
        }

        int moraleSum = 0;
        int armies = 0;

        // 전과 합계입니다. 킬 귀속이 실제로 동작하는지 배치모드에서 확인하는
        // 유일한 수단이므로 함께 기록합니다.
        //
        // 검증 포인트: playerKills는 enemyLosses와, enemyKills는 playerLosses와
        // 같아야 합니다. 어긋나면 킬 귀속이 새고 있다는 뜻입니다.
        // (전멸한 부대는 파괴되어 목록에서 빠지므로 완전히 일치하지 않을 수
        //  있습니다. 그 경우 kills <= losses 관계만 성립합니다)
        int playerKills = 0, enemyKills = 0;
        int playerLosses = 0, enemyLosses = 0;

        if (controller.armies != null)
        {
            for (int i = 0; i < controller.armies.Count; i++)
            {
                Army a = controller.armies[i];
                if (a == null) continue;
                armies++;
                moraleSum += Mathf.RoundToInt(a.army_Data.morale);

                if (a.army_Data.bplayer)
                {
                    playerKills += a.killCount;
                    playerLosses += a.lossCount;
                }
                else
                {
                    enemyKills += a.killCount;
                    enemyLosses += a.lossCount;
                }
            }
        }

        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine($"seed={seed}");
        sb.AppendLine($"ticks={Simulation_Clock.tick}");
        sb.AppendLine($"armies={armies}");
        sb.AppendLine($"alive={alive}");
        sb.AppendLine($"totalHp={totalHp}");
        sb.AppendLine($"moraleSum={moraleSum}");
        sb.AppendLine($"playerKills={playerKills}");
        sb.AppendLine($"enemyKills={enemyKills}");
        sb.AppendLine($"playerLosses={playerLosses}");
        sb.AppendLine($"enemyLosses={enemyLosses}");
        sb.AppendLine($"stateHash={hash:X16}");

        System.IO.File.WriteAllText(outputPath, sb.ToString());

        Debug.Log($"[MassBattleRunner] 결과 기록: {outputPath}\n{sb}");
    }

    /// <summary>부동소수점의 비트 표현을 FNV-1a 해시에 섞습니다.</summary>
    /// <param name="hash">지금까지 누적된 해시입니다.</param>
    /// <param name="value">섞어 넣을 값입니다.</param>
    /// <returns>갱신된 해시입니다.</returns>
    private static ulong Mix(ulong hash, float value)
    {
        unchecked
        {
            // 부동소수점의 비트 표현을 그대로 섞습니다.
            uint bits = (uint)System.BitConverter.SingleToInt32Bits(value);

            for (int i = 0; i < 4; i++)
            {
                hash ^= (byte)(bits >> (i * 8));
                hash *= 1099511628211UL; // FNV-1a 소수
            }

            return hash;
        }
    }

    /// <summary>에디터 콜백을 해제하고 지정한 코드로 종료합니다.</summary>
    /// <param name="exitCode">프로세스 종료 코드입니다. 0이 정상입니다.</param>
    private static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        EditorApplication.Exit(exitCode);
    }
}
