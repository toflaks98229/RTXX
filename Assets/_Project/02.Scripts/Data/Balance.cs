using Unity.Burst;

/// <summary>
/// 밸런스 수치의 전역 접근점입니다.
///
/// 왜 SharedStatic인가:
/// 밸런스 값은 Burst Job(Unit_Fight_Job, Army_Data._Update 등) 안에서 읽힙니다.
/// Burst 코드는 일반 C# static 필드에 접근할 수 없습니다. 컴파일된 네이티브 코드가
/// 관리 힙을 볼 수 없기 때문입니다.
///
/// SharedStatic&lt;T&gt;는 이 문제를 위해 Burst가 제공하는 장치로,
/// 관리 코드와 Burst 코드가 '같은 네이티브 메모리'를 공유합니다.
/// 덕분에 메인 스레드에서 Apply()로 값을 바꾸면 Job에서도 즉시 보입니다.
///
/// 대안을 쓰지 않은 이유:
/// Army_Data / Unit_Data의 수십 개 메서드(GetFatigueRate, GetChargeResistance 등)는
/// 인자를 받지 않고 Constant를 직접 읽습니다. 설정 구조체를 인자로 넘기려면
/// 그 시그니처를 전부 바꾸고 호출부 수백 곳을 고쳐야 합니다.
/// SharedStatic을 쓰면 호출부를 한 줄도 건드리지 않고 같은 결과를 얻습니다.
/// </summary>
public static class Balance
{
    /// <summary>
    /// Burst와 관리 코드가 공유하는 밸런스 데이터 저장소입니다.
    ///
    /// 제네릭 인자 두 개는 이 저장소를 전역에서 식별하는 키일 뿐입니다.
    /// (정적 클래스는 형식 인수로 쓸 수 없으므로 전용 태그 타입을 둡니다)
    /// </summary>
    private static readonly SharedStatic<Balance_Data> store =
        SharedStatic<Balance_Data>.GetOrCreate<Balance_Context, Balance_Data_Key>();

    /// <summary>SharedStatic 식별용 태그 타입입니다. 인스턴스를 만들지 않습니다.</summary>
    private class Balance_Context { }
    /// <summary>SharedStatic 식별용 태그 타입입니다. 인스턴스를 만들지 않습니다.</summary>
    private class Balance_Data_Key { }

    /// <summary>
    /// 현재 적용된 밸런스 수치입니다.
    ///
    /// Burst 주의:
    /// 예전에는 여기서 binitialized(일반 static bool)를 검사해 지연 초기화를
    /// 했습니다. 그런데 Burst는 readonly가 아닌 static 필드를 읽지 못합니다.
    ///
    ///   Burst error BC1040: Loading from a non-readonly static field
    ///   `Balance.binitialized` is not supported
    ///
    /// 이 프로퍼티는 Unit_Fight_Job과 Army_Data._Update 등 거의 모든 Burst
    /// Job이 읽으므로, 그 Job들이 통째로 Burst 컴파일에 실패해 느린 관리
    /// 코드로 실행되고 있었습니다. 최적화의 근간이 조용히 무력화된 셈입니다.
    ///
    /// 초기화는 정적 생성자로 옮겼습니다. 정적 생성자는 관리 측에서 타입에
    /// 처음 접근할 때 한 번 돌고, Burst 코드는 이미 초기화된 네이티브
    /// 메모리만 보므로 양쪽 모두 안전합니다.
    /// </summary>
    public static ref Balance_Data Data => ref store.Data;

    /// <summary>
    /// 기본값을 채워 둡니다.
    ///
    /// 정적 생성자는 이 타입에 처음 접근하는 시점에 한 번만 실행됩니다.
    /// Controller.Awake가 Balance_Config를 적용하기 전에도 유효한 값이
    /// 들어 있게 하는 것이 목적입니다.
    /// </summary>
    static Balance()
    {
        store.Data = Balance_Data.Default();
    }

    /// <summary>
    /// 밸런스 수치를 통째로 교체합니다. 플레이 중에 호출해도 즉시 반영됩니다.
    /// </summary>
    public static void Apply(in Balance_Data data)
    {
        store.Data = data;
    }

    /// <summary>
    /// 원본 Constant와 동일한 기본값으로 되돌립니다.
    /// Balance_Config가 지정되지 않은 씬에서 자동으로 호출됩니다.
    /// </summary>
    public static void Reset_To_Default()
    {
        store.Data = Balance_Data.Default();
    }
}
