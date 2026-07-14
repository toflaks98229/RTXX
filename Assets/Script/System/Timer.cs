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
    /// 초과 시 타이머를 재설정하고 true를 반환합니다.
    /// </summary>
    /// <returns>시간을 초과했으면 true, 아니면 false를 반환합니다.</returns>
    public bool IsOverTime()
    {
        if (time_Current > time_Limit)
        {
            ReSetTimer();
            return true;
        }
        return false;
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
