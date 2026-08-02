
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

using Random = UnityEngine.Random;
using System.Linq;
using System;

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
        // 붕괴한 부대는 싸우지 않습니다. 오직 달아납니다.
        if (army_Data.IsBroken())
        {
            targetArmy = null;
            army_Data.e_Army_Fight = E_Army_Fight.Non;
            return;
        }

        if (army_Detected.Count > 0)
        {
            // 타겟 '부대' 재지정은 타이머로 제한합니다. (매 틱 교체되면 전투가 불안정해집니다)
            timer_ReTarget._Update();
            if (timer_ReTarget.IsOverTime())
            {
                timer_ReTarget.ReSetTimer();

                if (_Update_Target_Army())
                {
                    Move_Cancel(); // 교전 시작 시 이동 취소
                }
            }
        }
        else
        {
            // 탐지된 대상이 없으면 타이머와 타겟을 초기화합니다.
            timer_ReTarget.ReSetTimer();
            targetArmy = null;
            army_Data.e_Army_Fight = E_Army_Fight.Non;
        }

        // 타겟 부대가 살아있는 한, 유닛 단위 전투는 '매 틱' 수행되어야 합니다.
        if (targetArmy != null)
        {
            if (targetArmy.units.Count == 0)
            {
                // 타겟 부대가 전멸했으면 교전을 종료합니다.
                targetArmy = null;
                army_Data.e_Army_Fight = E_Army_Fight.Non;
            }
            else
            {
                _Update_Target_Unit();
            }
        }
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
            // 가장 많은 인원을 접촉한 '적' 부대를 찾습니다.
            Army bestArmy = null;
            int bestNum = 0;

            foreach (var detected in army_Detected)
            {
                if (detected.army == null) continue;
                if (detected.army == this) continue;
                if (detected.army.army_Data.bplayer == army_Data.bplayer) continue;
                if (detected.army.units.Count == 0) continue;

                if (detected.num > bestNum)
                {
                    bestNum = detected.num;
                    bestArmy = detected.army;
                }
            }

            // 유효한 적을 찾지 못했으면 타겟을 지정하지 않습니다.
            if (bestArmy == null)
            {
                bfindTarget = false;
            }
            else
            {
                targetArmy = bestArmy;
                army_Data.e_Army_Fight = E_Army_Fight.Melee;

                GameEvents.RaiseArmyEngaged(this, targetArmy);
            }
        }

        return bfindTarget;
    }

    /// <summary>
    /// 타겟 부대 내의 유닛들을 대상으로 전투 관련 데이터를 업데이트합니다.
    /// </summary>
    /// <remarks>
    /// 이 Job은 '내 유닛'만 씁니다. 적 배열은 읽기 전용으로만 참조하며,
    /// 적이 받는 피해는 적 부대가 자기 _Update_Target_Unit에서 스스로 적용합니다.
    /// 따라서 양측이 서로를 타겟으로 잡고 있으면 피해가 대칭적으로 정산됩니다.
    /// </remarks>
    public void _Update_Target_Unit()
    {
        if (targetArmy == null) return;

        List<Unit> targetUnits = targetArmy.GetUnits();
        if (targetUnits.Count == 0) return;
        if (units.Count == 0) return;

        // 타겟 유닛 데이터를 NativeArray에 할당합니다.
        target_Unit_Datas = new NativeArray<Unit_Data>(targetUnits.Count, Allocator.TempJob);
        for (int i = 0; i < targetUnits.Count; i++)
        {
            target_Unit_Datas[i] = targetUnits[i].unit_Data;
        }

        // 적 유닛을 공간 격자에 색인합니다.
        // 이렇게 하면 내 유닛이 적 '전부'가 아니라 인접 셀만 검사하면 됩니다.
        float cellSize = Spatial_Grid.GetCellSize(army_Data, targetArmy.army_Data);

        var targetGrid = new NativeParallelMultiHashMap<int, int>(targetUnits.Count, Allocator.TempJob);

        Spatial_Grid_Build_Job buildJob = new Spatial_Grid_Build_Job();
        buildJob.unit_Datas = target_Unit_Datas;
        buildJob.cellSize = cellSize;
        buildJob.grid = targetGrid.AsParallelWriter();

        JobHandle buildHandle = buildJob.Schedule(target_Unit_Datas.Length, 32);

        // Unit_Fight_Job을 생성하고 실행합니다. (격자 구축이 끝난 뒤 실행되도록 의존성 연결)
        Unit_Fight_Job unit_Fight_Job = new Unit_Fight_Job();
        unit_Fight_Job.unit_Datas = unit_Datas;
        unit_Fight_Job.target_Unit_Datas = target_Unit_Datas;
        unit_Fight_Job.targetGrid = targetGrid;
        unit_Fight_Job.cellSize = cellSize;
        unit_Fight_Job.armyData = army_Data;
        unit_Fight_Job.targetArmyData = targetArmy.army_Data;
        // 시드는 틱마다 달라야 하고, Job 안에서 유닛 인덱스와 섞여
        // '유닛마다 다른' 난수가 됩니다. 0은 Random 생성자가 거부하므로 피합니다.
        unit_Fight_Job.randomSeed = (uint)Random.Range(1, int.MaxValue);

        JobHandle jobHandle = unit_Fight_Job.Schedule(units.Count, 32, buildHandle);
        jobHandle.Complete();

        targetGrid.Dispose();
        target_Unit_Datas.Dispose(); // NativeArray 메모리 해제
    }
}
