using System;
using UnityEngine;

/// <summary>
/// 게임 내에서 시간을 측정하고 관리하는 데 사용되는 타이머 구조체입니다.
/// </summary>
[Serializable]
public struct Timer
{
    // 공개 멤버 변수
    /// <summary>
    /// 타이머의 현재 경과 시간입니다.
    /// </summary>
    public float time_Current;

    // 비공개 멤버 변수
    /// <summary>
    /// 타이머의 제한 시간입니다.
    /// </summary>
    private float time_Limit;

    // 생성자
    /// <summary>
    /// 지정된 제한 시간으로 새로운 타이머를 생성합니다.
    /// </summary>
    /// <param name="time_Limit">설정할 제한 시간입니다.</param>
    public Timer(float time_Limit)
    {
        this.time_Limit = time_Limit;
        time_Current = 0;
    }

    // 공개 메서드
    /// <summary>
    /// 타이머의 현재 시간을 업데이트합니다.
    /// Constant.deltaTime을 사용하여 프레임 독립적으로 시간을 증가시킵니다.
    /// </summary>
    public void _Update()
    {
        time_Current += Constant.deltaTime;
    }

    /// <summary>
    /// 타이머의 현재 시간이 제한 시간을 초과했는지 확인합니다.
    /// 이 메서드는 상태를 변경하지 않는 순수 질의입니다.
    /// 초과분을 소비하려면 ReSetTimer() 또는 TryConsume()을 사용하십시오.
    /// </summary>
    /// <returns>시간을 초과했으면 true, 아니면 false를 반환합니다.</returns>
    public bool IsOverTime()
    {
        return time_Current > time_Limit;
    }

    /// <summary>
    /// 제한 시간을 초과했으면 타이머를 재설정하고 true를 반환합니다.
    /// 확인과 소비를 한 번에 처리해야 할 때 사용합니다.
    /// </summary>
    /// <returns>시간을 초과하여 소비했으면 true, 아니면 false를 반환합니다.</returns>
    public bool TryConsume()
    {
        if (time_Current > time_Limit)
        {
            ReSetTimer();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 경과 비율(0~1)을 반환합니다. 제한 시간이 0 이하이면 1을 반환합니다.
    /// 돌격 보너스처럼 시간에 따라 선형으로 감쇠하는 값에 사용합니다.
    /// </summary>
    public float GetRate()
    {
        if (time_Limit <= 0.0f) return 1.0f;

        float rate = time_Current / time_Limit;
        if (rate < 0.0f) return 0.0f;
        if (rate > 1.0f) return 1.0f;
        return rate;
    }

    /// <summary>
    /// 타이머를 0으로 재설정합니다.
    /// </summary>
    public void ReSetTimer()
    {
        time_Current = 0.0f;
    }

    /// <summary>
    /// 타이머의 제한 시간을 설정합니다.
    /// </summary>
    /// <param name="InTimeLimit">새로운 제한 시간입니다.</param>
    public void SetTimeLimit(float InTimeLimit)
    {
        time_Limit = InTimeLimit;
    }
}
