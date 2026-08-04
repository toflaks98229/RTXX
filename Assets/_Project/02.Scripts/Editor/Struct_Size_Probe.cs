using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시뮬레이션 구조체의 실제 메모리 크기를 재는 도구입니다.
///
/// 왜 필요한가:
/// "Unit_Data가 264바이트"라는 추정으로 최적화를 논해 왔는데,
/// 그 값은 필드 목록을 눈으로 세어 계산한 것이라 정렬(padding)과
/// bool 패킹을 정확히 반영하지 못합니다.
///
/// SoA 전환 같은 큰 구조 변경을 결정하려면 실제 크기와 '어느 필드가
/// 얼마를 차지하는가'를 정확히 알아야 합니다. 예를 들어 Timer 3개가
/// 절반을 차지한다면, 전면 전환보다 그 부분만 떼어내는 편이 낫습니다.
/// </summary>
public static class Struct_Size_Probe
{
    /// <summary>주요 구조체의 크기를 재어 로그로 남깁니다.</summary>
    [MenuItem("RTXX/구조체 크기 조사")]
    public static void Run()
    {
        Debug.Log(Build_Report());
    }

    /// <summary>배치모드에서 구조체 크기 측정을 실행하고 종료합니다.</summary>
    public static void Run_From_CLI()
    {
        Debug.Log(Build_Report());
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 복사 자체의 비용과 '관리 객체를 거치는' 비용을 갈라 잽니다.
    ///
    /// 왜 이 구분이 중요한가:
    /// A_UnitUpdate가 2.97ms인데, 그것이
    ///   (a) 280바이트를 9,600번 복사해서인지
    ///   (b) 관리 객체(Unit)를 9,600번 역참조해서인지
    /// 에 따라 해법이 완전히 달라집니다.
    ///
    /// (a)라면 구조체를 줄여야 하고, (b)라면 SoA로 옮겨야 합니다.
    /// 둘 다 큰 작업이므로 어느 쪽인지 먼저 확정해야 합니다.
    /// </summary>
    private static string Measure_Copy_Cost(int units, int unitDataSize)
    {
        StringBuilder sb = new StringBuilder(512);

        var watch = new System.Diagnostics.Stopwatch();
        const int rounds = 200;

        // 1. 순수 배열 간 복사 (관리 객체 없음)
        var src = new Unit_Data[units];
        var dst = new Unit_Data[units];

        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++) dst[i] = src[i];
        }
        watch.Stop();
        double arrayMs = watch.Elapsed.TotalMilliseconds / rounds;

        // 2. 관리 객체를 거친 복사 (현재 구조와 동일)
        //
        // 실제 Unit 컴포넌트를 만들 수는 없으므로, 같은 크기의 구조체를
        // 필드로 가진 관리 클래스를 세워 역참조 비용만 재현합니다.
        var holders = new Holder[units];
        for (int i = 0; i < units; i++) holders[i] = new Holder();

        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++) holders[i].data = src[i];
        }
        watch.Stop();
        double holderMs = watch.Elapsed.TotalMilliseconds / rounds;

        // 3. 필요한 필드만 옮기는 경우 (SoA 상한 추정)
        var poses = new Unit_Pose[units];

        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++)
            {
                poses[i] = new Unit_Pose
                {
                    position = src[i].position,
                    rotation = src[i].rotation
                };
            }
        }
        watch.Stop();
        double poseMs = watch.Elapsed.TotalMilliseconds / rounds;

        sb.AppendLine("--- 복사 비용 실측 (9,600개 x 200회 평균) ---");
        sb.AppendLine($"배열 -> 배열        : {arrayMs,6:F3} ms  (순수 복사)");
        sb.AppendLine($"배열 -> 관리객체    : {holderMs,6:F3} ms  (현재 구조)");
        sb.AppendLine($"배열 -> Pose(28B)   : {poseMs,6:F3} ms  (필요분만)");
        sb.AppendLine();
        sb.AppendLine($"역참조 추가 비용    : {(holderMs - arrayMs),6:F3} ms");
        sb.AppendLine($"구조체 크기 영향    : {(arrayMs - poseMs),6:F3} ms");

        return sb.ToString();
    }

    /// <summary>역참조 비용 재현용 관리 객체입니다.</summary>
    private class Holder
    {
        /// <summary>크기 측정 대상 구조체입니다. 필드 하나만 두어 순수 크기를 재기 위함입니다.</summary>
        public Unit_Data data;
    }

    /// <summary>
    /// 진형 슬롯을 Transform으로 두는 비용과 배열로 두는 비용을 비교합니다.
    ///
    /// 왜 재는가:
    /// 유닛 후처리를 Job으로 옮기려는 시도가 세 번 실패했는데, 원인은
    /// Job에 넘길 슬롯 위치를 메인 스레드가 Transform에서 읽어야 한다는
    /// 점이었습니다. 슬롯을 순수 데이터로 바꾸면 그 수집이 사라집니다.
    ///
    /// 다만 그 전환은 진형 시스템 전체를 건드리는 일이라, 착수 전에
    /// '얼마나 이득인가'를 숫자로 알아야 합니다.
    /// </summary>
    [MenuItem("RTXX/진형 슬롯 비용 비교")]
    public static void Measure_Slot_Cost()
    {
        Debug.Log(Build_Slot_Report());
    }

    /// <summary>배치모드에서 진형 슬롯 읽기 비용을 측정하고 종료합니다.</summary>
    public static void Measure_Slot_Cost_From_CLI()
    {
        Debug.Log(Build_Slot_Report());
        EditorApplication.Exit(0);
    }

    /// <summary>슬롯 읽기 비용 측정 결과를 표로 만듭니다.</summary>
    /// <returns>사람이 읽을 수 있는 결과 문자열입니다.</returns>
    private static string Build_Slot_Report()
    {
        const int units = 9600;
        const int rounds = 100;

        var sb = new StringBuilder(512);
        var watch = new System.Diagnostics.Stopwatch();

        // 1. Transform 슬롯 (현재 구조)
        //    부모 하나 아래에 자식 9,600개를 만듭니다.
        var root = new GameObject("SlotProbe_Root").transform;
        var slots = new Transform[units];

        for (int i = 0; i < units; i++)
        {
            var go = new GameObject("slot");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(i % 100, 0.0f, i / 100);
            slots[i] = go.transform;
        }

        var readBuffer = new Vector3[units];

        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++) readBuffer[i] = slots[i].position;
        }
        watch.Stop();
        double transformReadMs = watch.Elapsed.TotalMilliseconds / rounds;

        // 2. 배열 슬롯 (전환 후 예상)
        var arraySlots = new Vector3[units];
        for (int i = 0; i < units; i++) arraySlots[i] = new Vector3(i % 100, 0.0f, i / 100);

        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++) readBuffer[i] = arraySlots[i];
        }
        watch.Stop();
        double arrayReadMs = watch.Elapsed.TotalMilliseconds / rounds;

        // 3. 슬롯 위치 쓰기 비교 (명령 시 1회 발생)
        watch.Restart();
        for (int r = 0; r < rounds; r++)
        {
            for (int i = 0; i < units; i++) slots[i].position = readBuffer[i];
        }
        watch.Stop();
        double transformWriteMs = watch.Elapsed.TotalMilliseconds / rounds;

        Object.DestroyImmediate(root.gameObject);

        sb.AppendLine("========== 진형 슬롯 비용 비교 ==========");
        sb.AppendLine($"유닛 수: {units}, {rounds}회 평균");
        sb.AppendLine();
        sb.AppendLine($"Transform 읽기 : {transformReadMs,7:F3} ms  <- 매 틱 발생 (Job 입력 수집)");
        sb.AppendLine($"배열 읽기      : {arrayReadMs,7:F3} ms  <- 전환 후");
        sb.AppendLine($"절감 추정      : {(transformReadMs - arrayReadMs),7:F3} ms / 틱");
        sb.AppendLine();
        sb.AppendLine($"Transform 쓰기 : {transformWriteMs,7:F3} ms  (명령 시에만)");
        sb.AppendLine("========================================");

        return sb.ToString();
    }

    /// <summary>구조체 크기 측정 결과를 표로 만듭니다.</summary>
    /// <returns>사람이 읽을 수 있는 결과 문자열입니다.</returns>
    private static string Build_Report()
    {
        StringBuilder sb = new StringBuilder(1024);

        sb.AppendLine("========== 구조체 크기 조사 ==========");

        int unitDataSize = Marshal.SizeOf<Unit_Data>();
        int armyDataSize = Marshal.SizeOf<Army_Data>();
        int poseSize = Marshal.SizeOf<Unit_Pose>();
        int bodySize = Marshal.SizeOf<Collision_Body>();
        int animSize = Marshal.SizeOf<Unit_Animation_Data>();
        int timerSize = Marshal.SizeOf<Timer>();
        int targetSize = Marshal.SizeOf<Unit_target_Data>();
        int statSize = Marshal.SizeOf<Unit_Stat>();

        sb.AppendLine($"Unit_Data            : {unitDataSize,6} B");
        sb.AppendLine($"  Unit_target_Data   : {targetSize,6} B");
        sb.AppendLine($"  Timer (x3)         : {timerSize,6} B x 3 = {timerSize * 3} B");
        sb.AppendLine($"Army_Data            : {armyDataSize,6} B");
        sb.AppendLine($"  Unit_Stat          : {statSize,6} B");
        sb.AppendLine($"Unit_Pose            : {poseSize,6} B");
        sb.AppendLine($"Collision_Body       : {bodySize,6} B");
        sb.AppendLine($"Unit_Animation_Data  : {animSize,6} B");

        sb.AppendLine();
        sb.AppendLine("--- 9,600 유닛 기준 틱당 복사량 ---");

        const int units = 9600;

        // Apply 단계: unit_Datas -> Unit.unit_Data 전량 복사
        double applyMB = units * (double)unitDataSize / (1024.0 * 1024.0);

        // Prepare 단계: Unit.unit_Data -> unit_Datas 전량 복사
        double prepareMB = applyMB;

        // Snapshot: 해시맵에 전량 등록
        double snapshotMB = applyMB;

        sb.AppendLine($"Prepare  (읽어 담기) : {prepareMB,6:F2} MB");
        sb.AppendLine($"Apply    (되돌려쓰기): {applyMB,6:F2} MB");
        sb.AppendLine($"Snapshot (해시맵)    : {snapshotMB,6:F2} MB");
        sb.AppendLine($"합계                 : {(prepareMB + applyMB + snapshotMB),6:F2} MB / 틱");
        sb.AppendLine($"60틱 기준            : {(prepareMB + applyMB + snapshotMB) * 60 / 1024.0,6:F2} GB/s");

        sb.AppendLine();
        sb.AppendLine("--- 비교: 필요한 만큼만 옮긴다면 ---");
        sb.AppendLine($"Unit_Pose만 (28B 가정): {units * (double)poseSize / (1024.0 * 1024.0),6:F2} MB");
        sb.AppendLine($"절감 비율             : {(1.0 - (double)poseSize / unitDataSize) * 100.0,6:F1} %");

        sb.AppendLine();
        sb.AppendLine(Measure_Copy_Cost(units, unitDataSize));
        sb.AppendLine("======================================");

        return sb.ToString();
    }
}
