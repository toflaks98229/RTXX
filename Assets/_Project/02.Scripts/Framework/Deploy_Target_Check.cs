using UnityEngine;

/// <summary>
/// 배치 단계에서 순간이동한 유닛이 '제자리를 목적지로' 들고 있는지 재는 검사입니다.
///
/// 왜 런타임에 두는가:
/// 같은 검사를 두 곳에서 씁니다.
///   Deploy_Target_Probe   에디터 메뉴 (사람이 눌러서 확인)
///   Mass_Battle_Probe     배치모드 -deployCheck (자동 검증)
/// 두 곳에 따로 적으면 한쪽만 낡아, 통과하는데 실제로는 깨진 상태가 됩니다.
/// 판정을 여기 한 곳에만 둡니다.
///
/// 무엇을 재는가:
/// 배치 중에는 시뮬레이션 틱이 멈춰 있어 화면상 아무 문제가 없어 보입니다.
/// 그런데 순간이동(Unit.Place_At)이 위치만 옮기고 unit_Data.location을
/// 그대로 두면, 전투가 시작되는 첫 틱에 전원이 옛 목표를 향해 걸어갑니다.
/// 증상은 "진형이 한 번 뭉개졌다 펴진다"로 나타납니다.
/// </summary>
public static class Deploy_Target_Check
{
    /// <summary>검사 결과입니다.</summary>
    public struct Result
    {
        /// <summary>검사한 유닛 수입니다.</summary>
        public int counted;

        /// <summary>
        /// 놓인 자리와 가야 할 자리의 최대 거리(m)입니다.
        /// 0이 아니면 첫 틱에 그만큼 걸어갑니다.
        /// </summary>
        public float worstGap;

        /// <summary>이동 상태로 남은 유닛 수입니다. 전투 시작과 함께 움직입니다.</summary>
        public int stillMoving;

        /// <summary>슬롯 배정이 남은 유닛 수입니다. 옛 슬롯으로 끌려갑니다.</summary>
        public int stillAssigned;

        /// <summary>
        /// 유닛 무리의 중심과 진형 슬롯 무리의 중심 사이의 거리(m)입니다.
        ///
        /// 순간이동이 끝났으면 유닛은 슬롯 위에 서 있어야 하므로 0에
        /// 가까워야 합니다. 이 값이 크면 슬롯이 부대와 따로 놀고 있다는
        /// 뜻이고, 그 상태로 재정비가 걸리면 부대가 통째로 끌려갑니다.
        ///
        /// 기준점(formation_Move_Transform)과 비교하지 않는 이유:
        /// Formation_Job은 슬롯을 기준점에서 **뒤로** 쌓습니다.
        /// (add_vertical = -forward * interval)
        /// 즉 기준점은 대열의 '앞줄 중앙'이고 유닛 평균은 그보다 깊이의
        /// 절반만큼 뒤에 있습니다. 둘의 거리는 설계상 0이 아니므로
        /// 그것을 결함으로 세면 언제나 실패합니다.
        /// </summary>
        public float slotGap;

        /// <summary>모든 항목이 허용치 안인지 여부입니다.</summary>
        public bool IsClean =>
            worstGap < gapTolerance
            && stillMoving == 0
            && stillAssigned == 0
            && slotGap < slotTolerance;
    }

    /// <summary>
    /// 목표 이격 허용치(m)입니다.
    /// 지면 스냅으로 생기는 미세한 차이만 감안한 값입니다.
    /// </summary>
    public const float gapTolerance = 0.05f;

    /// <summary>
    /// 유닛 무리와 슬롯 무리의 중심 이격 허용치(m)입니다.
    ///
    /// 전사 등으로 인원과 슬롯 수가 어긋나면 두 중심이 조금 벌어집니다.
    /// 그 정도는 허용하되, 대열 하나만큼 벌어지는 것은 잡아야 합니다.
    /// </summary>
    public const float slotTolerance = 1.0f;

    /// <summary>부대 하나의 목표 정합을 잽니다.</summary>
    /// <param name="army">검사할 부대입니다.</param>
    /// <returns>측정 결과입니다.</returns>
    public static Result Measure(Army army)
    {
        Result result = new Result();

        if (army == null || army.units == null) return result;

        for (int i = 0; i < army.units.Count; i++)
        {
            Unit unit = army.units[i];
            if (unit == null) continue;

            result.counted++;

            Vector3 gap = unit.unit_Data.location - unit.unit_Data.position;
            gap.y = 0.0f;

            float distance = gap.magnitude;
            if (distance > result.worstGap) result.worstGap = distance;

            if (unit.unit_Data.e_Unit_Move != E_Unit_Move.Idle) result.stillMoving++;
            if (unit.targetSlotIndex >= 0) result.stillAssigned++;
        }

        // 슬롯 무리의 중심과 유닛 무리의 중심을 견줍니다.
        //
        // 순간이동이 끝났으면 유닛은 슬롯 위에 서 있어야 합니다.
        // 배치 중 이동 명령에서 기준점만 출발지에 남으면 이 값이 곧바로
        // '출발지와 목적지 사이의 거리'가 되어 드러납니다.
        int slotCount = army.SlotCount;

        if (slotCount > 0 && result.counted > 0)
        {
            Vector3 slotSum = Vector3.zero;
            for (int i = 0; i < slotCount; i++) slotSum += army.Get_Slot_World(i);

            Vector3 slotCenter = slotSum / slotCount;

            Vector3 gap = slotCenter - army.GetPosition();
            gap.y = 0.0f;
            result.slotGap = gap.magnitude;
        }

        return result;
    }

    /// <summary>결과를 사람이 읽을 수 있는 여러 줄 문자열로 만듭니다.</summary>
    /// <param name="result">측정 결과입니다.</param>
    /// <param name="label">어떤 상황을 검사했는지 표시할 이름입니다.</param>
    /// <returns>보고 문자열입니다.</returns>
    public static string Describe(in Result result, string label)
    {
        return $"[배치목표] {label}  유닛 {result.counted}명\n" +
               $"    목표 이격 최대 {result.worstGap:F3} m " +
               $"(허용 {gapTolerance:F2})\n" +
               $"    이동 상태로 남은 유닛 {result.stillMoving}명 / " +
               $"슬롯 배정이 남은 유닛 {result.stillAssigned}명\n" +
               $"    슬롯 중심과 부대 중심의 거리 {result.slotGap:F3} m " +
               $"(허용 {slotTolerance:F2})\n" +
               $"    {(result.IsClean ? "[통과]" : "[실패] 전투 시작 첫 틱에 진형이 움직입니다")}";
    }
}
