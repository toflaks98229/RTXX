using UnityEngine;

/// <summary>
/// 병종 스탯을 에셋으로 관리하기 위한 ScriptableObject입니다.
///
/// 설계 의도 (비파괴):
/// 기존 Army 프리팹들은 인스펙터에 직접 입력한 army_Data.unit_Stat 값을 갖고 있습니다.
/// 그 데이터를 잃지 않기 위해 이 에셋은 '선택적 덮어쓰기'로 동작합니다.
///   - Army.unit_Stat_Asset 이 비어 있으면  -> 기존 인스펙터 값을 그대로 사용
///   - 에셋이 지정되어 있으면              -> _Start 시점에 에셋 값으로 덮어씀
///
/// 덕분에 병종별 밸런싱을 에셋 하나로 관리하면서도,
/// 아직 전환하지 않은 프리팹은 예전과 똑같이 동작합니다.
///
/// 사용법: Project 창에서 Create > RTXX > Unit Stat
/// </summary>
[CreateAssetMenu(fileName = "UnitStat_", menuName = "RTXX/Unit Stat", order = 0)]
public class UnitStatSO : ScriptableObject
{
    [Header("병종 식별")]
    [Tooltip("에디터에서 알아보기 위한 이름입니다. 게임 로직에는 쓰이지 않습니다.")]
    public string displayName = "New Unit";

    [TextArea]
    [Tooltip("이 병종에 대한 메모입니다. 밸런싱 의도를 적어두면 좋습니다.")]
    public string description;

    [Header("스탯")]
    public Unit_Stat stat;

    /// <summary>
    /// 스탯 값의 유효성을 검사하고 문제를 경고로 알립니다.
    /// interval이 0 이하이면 진형 계산이 성립하지 않으므로 특히 중요합니다.
    /// </summary>
    private void OnValidate()
    {
        if (stat.interval <= 0.0f)
        {
            Debug.LogWarning($"{name}: interval이 0 이하입니다. 진형 폭 계산이 1로 고정됩니다.", this);
        }

        if (stat.HP <= 0.0f)
        {
            Debug.LogWarning($"{name}: HP가 0 이하입니다. 유닛이 생성 즉시 사망합니다.", this);
        }

        if (stat.size < 0.0f) stat.size = 0.0f;
        if (stat.radius < 0.0f) stat.radius = 0.0f;
        if (stat.height < 0.0f) stat.height = 0.0f;

        // 피해량은 기본 + AP 두 갈래이며 둘 다 0이면 아무도 죽지 않습니다.
        if (stat.meleeDamage < 0.0f) stat.meleeDamage = 0.0f;
        if (stat.meleeDamageAP < 0.0f) stat.meleeDamageAP = 0.0f;
        if (stat.rangeDamage < 0.0f) stat.rangeDamage = 0.0f;
        if (stat.rangeDamageAP < 0.0f) stat.rangeDamageAP = 0.0f;

        if (stat.meleeDamage <= 0.0f && stat.meleeDamageAP <= 0.0f)
        {
            Debug.LogWarning($"{name}: 근접 피해가 기본/AP 모두 0입니다. " +
                             "최소 피해로만 싸우게 됩니다.", this);
        }
    }
}
