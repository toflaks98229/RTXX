using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대의 "사기 산출" 책임을 담당하는 부분 클래스입니다.
///
/// 담는 것: 목표 사기 계산(Morale_Modifiers 10개 항목)과 붕괴 처리.
///
/// 역할 분담 주의:
///   여기(메인 스레드) : '무엇이 사기를 흔드는가'를 상황에서 읽어 목표값을 냅니다.
///                       참조 타입(allArmies, units, 깃발)을 순회해야 하므로
///                       Burst로 옮길 수 없습니다.
///   Army_Data._Update : 그 목표값을 향해 실제 사기를 움직이고 상태를 전이합니다.
///                       값 타입만 다루므로 Burst Job에서 병렬 실행됩니다.
///
/// 항목을 합계가 아니라 개별로 남기는 이유는 Morale_Modifiers의 주석을
/// 참고하십시오. 요약하면 "왜 무너지는가"를 UI가 읽어 가야 하기 때문입니다.
/// </summary>
partial class Army
{
    /// <summary>
    /// 사기의 '목표값'을 계산합니다. 실제 사기 이동은 Burst Job(Army_Data._Update)이 수행합니다.
    /// 토탈워식으로 손실률, 포위, 지휘(깃발) 상황을 종합합니다.
    /// </summary>
    private void _Update_Morale_Input()
    {
        // 항목별로 계산해 남겨 둡니다. UI가 "왜 사기가 떨어지는가"를
        // 이 내역에서 그대로 읽어 갑니다. (합계만 두면 그 화면을 못 만듭니다)
        Morale_Modifiers modifiers = army_Data.morale_Modifiers;
        modifiers.Clear();

        // 1. 사상자: 손실률이 클수록 크게 떨어집니다. 가장 지배적인 요인입니다.
        if (army_Data.unit_Num_Max > 0)
        {
            float lossRate = 1.0f - ((float)army_Data.unit_Num / army_Data.unit_Num_Max);
            if (lossRate < 0.0f) lossRate = 0.0f;
            modifiers.casualties = -lossRate * Constant.morale_Penalty_Casualty;
        }

        // 2. 포위: 나를 상대로 교전 중인 적 부대가 둘 이상이면 급격히 흔들립니다.
        //    동시에 교전 중인 적의 총 인원도 세어 수적 열세 판정에 씁니다.
        int enemyArmies = 0;
        int enemyUnits = 0;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            Army other = army_Detected[i].army;
            if (other == null) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            enemyArmies++;
            enemyUnits += other.units.Count;
        }

        if (enemyArmies > 1)
        {
            modifiers.surrounded = -(enemyArmies - 1) * Constant.morale_Penalty_Surrounded;
        }

        // 3. 수적 열세 / 우세: 눈앞의 적이 나보다 많으면 흔들리고, 적으면 버팁니다.
        if (enemyUnits > 0 && army_Data.unit_Num > 0)
        {
            float ratio = (float)enemyUnits / army_Data.unit_Num;

            if (ratio > 1.0f)
            {
                // 열세: 정해진 비율에서 페널티가 최대에 도달합니다.
                float t = (ratio - 1.0f)
                          / Mathf.Max(0.0001f, Constant.morale_Outnumbered_Full_Ratio - 1.0f);
                if (t > 1.0f) t = 1.0f;

                modifiers.outnumbered = -t * Constant.morale_Penalty_Outnumbered;
            }
            else
            {
                // 우세: 이기고 있다는 실감이 부대를 버티게 합니다.
                float t = 1.0f - ratio;
                modifiers.winning = t * Constant.morale_Bonus_Winning;
            }
        }

        // 4. 피로: 지친 병사는 먼저 무너집니다.
        modifiers.fatigue = -army_Data.GetFatigueMoralePenalty();

        // 5. 지휘: 깃발을 든 유닛이 살아 있으면 부대가 결속을 유지합니다.
        if (unit_Bearing_Flag != null && !unit_Bearing_Flag.IsDead())
        {
            modifiers.flag = Constant.morale_Bonus_Flag;
        }

        // 6. 지형: 고지를 점하면 사기가 오르고, 올려다보는 쪽은 떨어집니다.
        modifiers.terrain = army_Data.GetHighGroundMorale();

        // 6-1. 지휘: 장군이 가까이 있으면 부대가 버팁니다.
        modifiers.general = Get_General_Aura();

        // 7. 연쇄 붕괴: 옆의 아군이 무너지면 이쪽도 흔들립니다.
        //    (GameEvents.OnArmyRouted 구독으로 누적된 값을 소비합니다)
        modifiers.alliedRouting = -alliedRoutPenalty;

        // 충격은 별도 필드로 관리되므로 표시용으로만 옮겨 담습니다.
        modifiers.shock = -army_Data.morale_Shock;

        army_Data.morale_Modifiers = modifiers;

        // 충격을 뺀 나머지 항목의 합이 목표 사기입니다.
        // (충격은 _Update_Morale에서 별도로 차감되므로 여기서 이중 적용하면 안 됩니다)
        float target = Constant.morale_Max + modifiers.Sum() - modifiers.shock;

        if (target > Constant.morale_Max) target = Constant.morale_Max;
        if (target < 0.0f) target = 0.0f;

        army_Data.morale_Target = target;

        // 5. 패주 방향: 교전 상대의 반대편으로 달아납니다.
        if (targetArmy != null)
        {
            Vector3 away = GetPosition() - targetArmy.GetPosition();
            away.y = 0.0f;
            if (away.sqrMagnitude > 0.0001f) army_Data.escapeDirection = away.normalized;
        }

        // 연쇄 붕괴 페널티는 사기 충격과 같은 속도로 사그라듭니다.
        // 옆 부대가 무너진 충격이 영원히 남으면 재결집이 불가능해집니다.
        if (alliedRoutPenalty > 0.0f)
        {
            alliedRoutPenalty -= Constant.morale_Shock_Recover * Constant.deltaTime;
            if (alliedRoutPenalty < 0.0f) alliedRoutPenalty = 0.0f;
        }

        // 6. 붕괴/재결집 알림 (Job이 세운 1회성 플래그를 소비합니다)
        if (army_Data.broutedThisTick)
        {
            army_Data.broutedThisTick = false;
            On_Rout();
        }

        if (army_Data.bshatteredThisTick)
        {
            army_Data.bshatteredThisTick = false;
            GameEvents.RaiseArmyShattered(this);
        }

        if (army_Data.bralliedThisTick)
        {
            army_Data.bralliedThisTick = false;
            GameEvents.RaiseArmyRallied(this);
        }
    }

    /// <summary>
    /// 부대가 붕괴했을 때의 처리입니다. 교전을 끊고 모든 유닛의 목표를 해제합니다.
    /// </summary>
    private void On_Rout()
    {
        targetArmy = null;
        army_Data.e_Army_Fight = E_Army_Fight.Non;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].unit_Data.Lose_Target();
        }

        GameEvents.RaiseArmyRouted(this);
    }
}
