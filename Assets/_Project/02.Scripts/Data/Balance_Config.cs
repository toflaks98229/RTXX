using UnityEngine;

/// <summary>
/// 밸런스 수치를 인스펙터에서 편집하기 위한 에셋입니다.
///
/// 사용법:
/// 1. Project 창에서 Create > RTXX > Balance Config 로 에셋을 만듭니다.
/// 2. Controller의 balance_Config 칸에 그 에셋을 넣습니다.
/// 3. 값을 수정하면 플레이 중에도 즉시 반영됩니다. (재컴파일 불필요)
///
/// 비워 두면 Balance_Data.Default()가 쓰이며, 이는 리팩토링 이전 Constant 값과
/// 완전히 같습니다. 즉 이 에셋을 만들지 않아도 기존 동작이 그대로 유지됩니다.
///
/// 왜 값이 struct 하나로 묶여 있는가:
/// Burst Job에 넘기려면 blittable 구조체여야 합니다. ScriptableObject 자체는
/// 관리 객체라 Job이 볼 수 없으므로, 안에 담긴 struct만 Balance로 복사해 씁니다.
/// </summary>
[CreateAssetMenu(fileName = "Balance_Config", menuName = "RTXX/Balance Config")]
public class Balance_Config : ScriptableObject
{
    /// <summary>이 에셋이 들고 있는 밸런스 수치입니다. 인스펙터에서 편집합니다.</summary>
    [Tooltip("게임 밸런스 수치 전체입니다. 항목별 설명은 Balance_Data.cs를 참고하십시오.")]
    public Balance_Data balance = Balance_Data.Default();

    /// <summary>이 에셋의 수치를 전역 밸런스에 적용합니다.</summary>
    public void Apply()
    {
        Balance.Apply(balance);
    }

    /// <summary>
    /// 인스펙터에서 값이 바뀔 때마다 즉시 반영합니다.
    /// 플레이 중 슬라이더를 움직이면 다음 틱부터 새 수치로 전투가 진행됩니다.
    /// 이것이 이 리팩토링의 핵심 목적입니다.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) Apply();
    }

    /// <summary>모든 수치를 원본 Constant 기본값으로 되돌립니다.</summary>
    [ContextMenu("기본값으로 되돌리기")]
    public void Reset_To_Default()
    {
        balance = Balance_Data.Default();
        if (Application.isPlaying) Apply();
    }
}
