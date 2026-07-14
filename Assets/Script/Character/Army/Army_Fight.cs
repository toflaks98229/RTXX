using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System.Linq;
using System;
using Unity.VisualScripting;

/// <summary>
/// 부대의 전투 관련 로직을 담당하는 부분 클래스입니다.
/// </summary>
partial class Army
{
    // 비공개 멤버 변수 (Army.cs에서 관리)
    // 공개 메서드 (Army.cs에서 관리)
    // Unity 이벤트 함수 (Army.cs에서 관리)

    // 비공개 메서드
    /// <summary>
    /// 타겟 업데이트를 처리하는 함수입니다.
    /// </summary>
    void _Update_Target()
    {
        if (army_Detected.Count > 0)
        {
            // 타이머를 업데이트하고, 시간이 지났는지 확인
            timer_ReTarget._Update();
            if (timer_ReTarget.IsOverTime())
            {
                // 타겟 부대 지정에 성공하면
                if (_Update_Target_Army())
                {
                    _Update_Target_Unit(); // 타겟 유닛 업데이트
                    Move_Cancel(); // 이동 취소
                    Debug.Log("타겟 아미 지정 성공");
                }
            }
        }
        else
        {
            // 탐지된 대상이 없으면 타이머 초기화
            timer_ReTarget.ReSetTimer();
        }
    }

    /// <summary>
    /// 유닛의 통계 데이터를 업데이트합니다.
    /// </summary>
    void _Update_Stat()
    {
        Unit_Stat_Job unit_Stat_Job = new Unit_Stat_Job();
        unit_Stat_Job.unit_Datas = unit_Datas;
        unit_Stat_Job.army_Data = army_Data;

        JobHandle jobHandle = unit_Stat_Job.Schedule(units.Count, 1);
        jobHandle.Complete();

        army_Data = unit_Stat_Job.army_Data;
    }

    /// <summary>
    /// 탐지된 부대 중에서 가장 많은 접촉 횟수를 가진 부대를 타겟으로 지정합니다.
    /// </summary>
    /// <returns>타겟을 찾았으면 true, 아니면 false를 반환합니다.</returns>
    public bool _Update_Target_Army()
    {
        bool bfindTarget = false;

        // 이미 타겟이 있고 전투 중이면 추가적인 타겟을 찾지 않습니다.
        if (targetArmy != null && army_Data.e_Army_Fight != E_Army_Fight.Non)
        {
            return bfindTarget;
        }

        switch (army_Data.e_Army_Fight)
        {
            case E_Army_Fight.Range:
            case E_Army_Fight.Non:
                bfindTarget = true;
                break;
            case E_Army_Fight.Melee:
                break;
        }

        if (bfindTarget)
        {
            // 가장 많은 인원을 접촉한 대상을 찾습니다.
            Army_Count army_Count = new Army_Count(this, 0);
            foreach (var detected in army_Detected)
            {
                if (detected.num > army_Count.num && detected.army.army_Data.bplayer != army_Data.bplayer)
                {
                    army_Count = detected;
                }
            }

            // 찾은 대상을 targetArmy로 지정합니다.
            targetArmy = army_Count.army;
        }

        return bfindTarget;
    }

    /// <summary>
    /// 타겟 부대 내의 유닛들을 대상으로 전투 관련 데이터를 업데이트합니다.
    /// </summary>
    public void _Update_Target_Unit()
    {
        if (targetArmy == null) return;

        // 타겟 유닛 데이터를 NativeArray에 할당합니다.
        target_Unit_Datas = new NativeArray<Unit_Data>(targetArmy.GetUnits().Count, Allocator.TempJob);
        for (int i = 0; i < targetArmy.GetUnits().Count; i++)
        {
            target_Unit_Datas[i] = targetArmy.GetUnits()[i].unit_Data;
        }

        // Unit_Fight_Job을 생성하고 실행합니다.
        Unit_Fight_Job unit_Fight_Job = new Unit_Fight_Job();
        unit_Fight_Job.unit_Datas = unit_Datas;
        unit_Fight_Job.target_Unit_Datas = target_Unit_Datas;
        unit_Fight_Job.random = Random.Range(0, 100);

        JobHandle jobHandle = unit_Fight_Job.Schedule(units.Count, 1);
        jobHandle.Complete();

        target_Unit_Datas.Dispose(); // NativeArray 메모리 해제
    }
}
