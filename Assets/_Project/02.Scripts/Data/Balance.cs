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

    /// <summary>초기화 여부입니다. 관리 측에서만 확인하므로 일반 static으로 충분합니다.</summary>
    private static bool binitialized;

    /// <summary>
    /// 현재 적용된 밸런스 수치입니다.
    ///
    /// 최초 접근 시 자동으로 기본값이 채워집니다. 따라서 Balance_Config를
    /// 씬에 두지 않아도 리팩토링 이전과 완전히 동일하게 동작합니다.
    ///
    /// 주의: Burst Job 안에서는 이 프로퍼티가 아니라 Data를 통해 접근해야 합니다.
    /// (프로퍼티 자체는 Burst에서도 인라인되므로 실제로는 양쪽 모두 안전합니다)
    /// </summary>
    public static ref Balance_Data Data
    {
        get
        {
            // Burst 코드에서는 이 분기가 실행되지 않지만,
            // 관리 코드가 먼저 한 번은 반드시 거치므로 초기화가 보장됩니다.
            if (!binitialized) Reset_To_Default();
            return ref store.Data;
        }
    }

    /// <summary>
    /// 밸런스 수치를 통째로 교체합니다. 플레이 중에 호출해도 즉시 반영됩니다.
    /// </summary>
    public static void Apply(in Balance_Data data)
    {
        store.Data = data;
        binitialized = true;
    }

    /// <summary>
    /// 원본 Constant와 동일한 기본값으로 되돌립니다.
    /// Balance_Config가 지정되지 않은 씬에서 자동으로 호출됩니다.
    /// </summary>
    public static void Reset_To_Default()
    {
        store.Data = Balance_Data.Default();
        binitialized = true;
    }
}
