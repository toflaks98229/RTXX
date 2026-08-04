using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 진형 슬롯 인덱스 규약이 실제 기하와 맞는지 확인하고, 유닛이 자기
/// 슬롯에서 얼마나 벗어나 있는지 재는 검사입니다.
///
/// 왜 런타임에 두는가:
/// 같은 검사를 두 곳에서 씁니다.
///   Formation_Index_Probe  에디터 메뉴 (플레이 중에 눌러서 확인)
///   Mass_Battle_Probe      배치모드 정기 검증
/// 두 곳에 따로 적으면 한쪽만 낡아, 통과하는데 실제로는 깨진 상태가 됩니다.
///
/// 무엇을 확인하는가:
///
/// 1) 슬롯 배정의 유일성
///    두 유닛이 같은 슬롯을 들고 있으면 선두 지정이 엉뚱한 유닛을 가리킵니다.
///
/// 2) file/rank 유도가 실제 슬롯 좌표와 맞는가
///    이것이 핵심입니다. 규약은 Formation_Job이 좌표를 펼치는 순서
///    (index / width = 열, index % width = 오)에서 유도한 것인데,
///    그 관계가 코드 두 곳에 나뉘어 있습니다. 한쪽이 바뀌어도 예외는
///    나지 않고 대열만 이상해지므로 좌표로 대조해야 합니다.
///
/// 3) 유닛이 자기 슬롯에서 얼마나 벗어나 있는가
///    선두 지정 전환의 판단 근거입니다. 이탈이 이미 크다면 슬롯은
///    사실상 느슨한 안내선이므로 선두 추종으로 바꿔도 잃을 것이 적습니다.
///    작다면 강체 결합에 의존하는 동작(돌격 접촉면, 벽 태세 밀집도)이
///    있다는 뜻이라 그쪽부터 봐야 합니다.
///
/// 이 검사는 읽기 전용입니다. 어떤 동작도 바꾸지 않습니다.
/// </summary>
public static class Formation_Index_Check
{
    /// <summary>부대 하나에 대한 검사 결과입니다.</summary>
    public struct Result
    {
        /// <summary>진형이 있어 검사가 성립했는지 여부입니다.</summary>
        public bool bvalid;

        /// <summary>진형의 가로 인원 수입니다.</summary>
        public int width;
        /// <summary>슬롯 수입니다.</summary>
        public int slotCount;

        /// <summary>이탈을 잰 표본 수입니다.</summary>
        public int samples;
        /// <summary>이탈 거리의 합입니다. 평균을 내는 데 씁니다.</summary>
        public double deviationSum;
        /// <summary>관측된 최대 이탈 거리입니다.</summary>
        public float worstDeviation;

        /// <summary>두 유닛이 같은 슬롯을 든 횟수입니다.</summary>
        public int slotCollision;

        /// <summary>file/rank 유도가 실제 슬롯 좌표와 어긋났는지 여부입니다.</summary>
        public bool bfileRankMismatch;

        /// <summary>선두를 찾지 못했는지 여부입니다.</summary>
        public bool bleaderMissing;

        /// <summary>평균 이탈 거리입니다.</summary>
        public float AverageDeviation =>
            samples > 0 ? (float)(deviationSum / samples) : 0.0f;

        /// <summary>규약이 깨진 곳이 없는지 여부입니다. (이탈 거리는 판정에 넣지 않습니다)</summary>
        public bool IsClean =>
            slotCollision == 0 && !bfileRankMismatch && !bleaderMissing;
    }

    /// <summary>
    /// 부대 하나의 진형 인덱스 규약과 슬롯 이탈을 잽니다.
    /// </summary>
    /// <param name="army">검사할 부대입니다.</param>
    /// <param name="maxSamples">
    /// 이탈을 잴 최대 표본 수입니다. 0 이하면 전수 검사합니다.
    /// 배정이 깨지면 부대 단위로 한꺼번에 깨지므로 표본으로 충분합니다.
    /// </param>
    /// <returns>측정 결과입니다.</returns>
    public static Result Measure(Army army, int maxSamples = 16)
    {
        Result result = new Result();

        if (army == null) return result;

        List<Unit> units = army.units;
        if (units == null || units.Count == 0) return result;

        int width = army.GetFormation_Num();
        int slotCount = army.SlotCount;

        if (width <= 0 || slotCount <= 0) return result;

        result.bvalid = true;
        result.width = width;
        result.slotCount = slotCount;

        // 소유자 색인을 지금 다시 만듭니다.
        //
        // 검사기가 낡은 캐시를 보면 '배정이 깨졌다'고 잘못 보고합니다.
        // 슬롯 배정은 틱 도중에도 바뀌므로(Battle_AI의 FixedUpdate 등)
        // 지연 재구축에 맡길 수 없습니다.
        army.Refresh_Slot_Owners();

        // --- 1. 슬롯 배정의 유일성과 이탈 거리 ---
        int step = maxSamples > 0
            ? Mathf.Max(1, units.Count / maxSamples)
            : 1;

        for (int i = 0; i < units.Count; i += step)
        {
            Unit unit = units[i];
            if (unit == null) continue;

            int slot = unit.targetSlotIndex;
            if (slot < 0 || slot >= slotCount) continue;

            // 이 슬롯의 주인이 정말 이 유닛인가.
            // 아니라면 다른 유닛이 같은 슬롯을 들고 있다는 뜻입니다.
            if (army.Get_Slot_Owner(slot) != unit) result.slotCollision++;

            Vector3 gap = army.Get_Slot_World(slot) - unit.unit_Data.position;
            gap.y = 0.0f;

            float distance = gap.magnitude;

            result.deviationSum += distance;
            result.samples++;
            if (distance > result.worstDeviation) result.worstDeviation = distance;
        }

        // --- 2. file/rank 유도가 실제 좌표와 맞는가 ---
        //
        // 같은 오의 인접한 두 열은 정확히 간격 하나만큼 떨어져 있어야 합니다.
        // (Formation_Job이 add_vertical로 뒤로 쌓으므로)
        float interval = army.army_Data.GetInterval();

        if (interval > 0.0f)
        {
            int front = Formation_Slots.Index_Of(0, 0, width);
            int second = Formation_Slots.Index_Of(0, 1, width);

            if (front >= 0 && second >= 0 && second < slotCount)
            {
                float actual = Vector3.Distance(
                    army.Get_Slot_World(front), army.Get_Slot_World(second));

                // 지면 스냅으로 생기는 오차만 감안합니다.
                if (Mathf.Abs(actual - interval) > interval * 0.5f)
                {
                    result.bfileRankMismatch = true;
                }
            }
        }

        // --- 3. 선두 조회가 성립하는가 ---
        //
        // 살아 있는 유닛이 있는 부대라면 0번 오의 선두를 찾을 수 있어야 합니다.
        // 못 찾으면 소유자 색인이나 승격 규칙이 깨진 것입니다.
        //
        // 단, 아직 아무도 슬롯을 배정받지 않았으면(명령 전) 정상입니다.
        if (result.samples > 0 && army.Get_File_Leader(0) == null)
        {
            result.bleaderMissing = true;
        }

        return result;
    }

    /// <summary>결과를 사람이 읽을 수 있는 여러 줄 문자열로 만듭니다.</summary>
    /// <param name="result">측정 결과입니다.</param>
    /// <param name="label">부대 이름 등 표시할 이름입니다.</param>
    /// <returns>보고 문자열입니다.</returns>
    public static string Describe(in Result result, string label)
    {
        if (!result.bvalid)
        {
            return $"[진형인덱스] {label}  진형이 아직 만들어지지 않았습니다.";
        }

        return $"[진형인덱스] {label}  폭 {result.width} / 슬롯 {result.slotCount}개\n" +
               $"    슬롯 이탈 평균 {result.AverageDeviation:F2} m / " +
               $"최대 {result.worstDeviation:F2} m (표본 {result.samples}회)\n" +
               $"    슬롯 충돌 {result.slotCollision}건 / " +
               $"규약 불일치 {(result.bfileRankMismatch ? "있음" : "없음")} / " +
               $"선두 {(result.bleaderMissing ? "실패" : "정상")}\n" +
               $"    {(result.IsClean ? "[통과]" : "[실패] 인덱스 규약이 좌표와 어긋났습니다")}";
    }
}
