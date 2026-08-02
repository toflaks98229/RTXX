using System;
using UnityEngine;

/// <summary>
/// 부대 사기에 영향을 주는 개별 요인들입니다.
///
/// 왜 통합값이 아니라 개별 항목으로 두는가:
/// 토탈워에서 플레이어는 사기 툴팁으로 "-12 포위됨 / -8 사상자 / +10 장군 근처"를
/// 읽고 다음 수를 정합니다. 합계만 들고 있으면 그 화면을 만들 수 없고,
/// 그러면 플레이어는 부대가 왜 무너지는지 끝내 알 수 없습니다.
///
/// 각 항목은 목표 사기에 더해지는 값이며, 음수가 페널티입니다.
/// Burst Job에서 쓰이므로 참조 타입을 담지 않는 순수 값 타입이어야 합니다.
/// </summary>
[Serializable]
public struct Morale_Modifiers
{
    /// <summary>사상자 손실률에 의한 감소입니다. 보통 가장 지배적인 요인입니다.</summary>
    public float casualties;

    /// <summary>둘 이상의 적 부대와 교전 중일 때의 포위 감소입니다.</summary>
    public float surrounded;

    /// <summary>수적 열세에 의한 감소입니다. 교전 상대와의 인원 비로 계산합니다.</summary>
    public float outnumbered;

    /// <summary>피로에 의한 감소입니다. 지친 병사가 먼저 무너집니다.</summary>
    public float fatigue;

    /// <summary>깃발을 든 유닛이 살아 있을 때의 보너스입니다.</summary>
    public float flag;

    /// <summary>인접 아군 부대가 붕괴했을 때의 연쇄 감소입니다.</summary>
    public float alliedRouting;

    /// <summary>국지적으로 우세할 때의 보너스입니다. 이기고 있으면 사기가 오릅니다.</summary>
    public float winning;

    /// <summary>돌격을 받아 발생한 순간 충격입니다. 시간이 지나면 회복됩니다.</summary>
    public float shock;

    /// <summary>고지를 점했을 때의 보너스입니다. 낮은 곳이면 음수입니다.</summary>
    public float terrain;

    /// <summary>모든 항목의 합입니다. 목표 사기는 최대치에 이 값을 더해 구합니다.</summary>
    public float Sum()
    {
        return casualties
             + surrounded
             + outnumbered
             + fatigue
             + flag
             + alliedRouting
             + winning
             + shock
             + terrain;
    }

    /// <summary>매 틱 다시 계산하기 전에 모든 항목을 지웁니다.</summary>
    public void Clear()
    {
        casualties = 0.0f;
        surrounded = 0.0f;
        outnumbered = 0.0f;
        fatigue = 0.0f;
        flag = 0.0f;
        alliedRouting = 0.0f;
        winning = 0.0f;
        shock = 0.0f;
        terrain = 0.0f;
    }
}
