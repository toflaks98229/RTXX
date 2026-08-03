
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

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
    /// 표적을 정하고 전투 Job을 스케줄합니다. 완료 대기는 _Complete_Target()이 합니다.
    /// </summary>
    void _Schedule_Target()
    {
        _Update_Target();
    }

    /// <summary>
    /// 전투 Job이 끝나기를 기다리고 임시 자원을 반납합니다.
    /// </summary>
    void _Complete_Target()
    {
        if (!bfightJobScheduled) return;

        fightJobHandle.Complete();
        bfightJobScheduled = false;

        // 격자는 재사용하므로 여기서 해제하지 않습니다. (OnDestroy에서 반납)
    }

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
                    // 근접 교전에 들어갈 때만 이동을 멈춥니다.
                    //
                    // 원거리 교전은 멀리서 성립하므로, 여기서 멈추면
                    // 진격하던 부대가 적을 '보기만 해도' 얼어붙습니다.
                    // 사격은 제자리 여부와 무관하게 Unit_Fight_Job이 처리합니다.
                    if (army_Data.e_Army_Fight == E_Army_Fight.Melee)
                    {
                        Move_Cancel(); // 교전 시작 시 이동 취소
                    }
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

        // 이미 근접 교전 중이면 표적을 바꾸지 않습니다.
        //
        // 원거리(Range) 교전은 예외입니다. 멀리서 쏘던 중에 적이 달려와
        // 맞닿으면 난전으로 넘어가야 하는데, 여기서 막아 버리면
        // 영원히 Range 상태로 굳어 근접 판정이 성립하지 않습니다.
        if (targetArmy != null && army_Data.e_Army_Fight == E_Army_Fight.Melee)
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
            // 접촉한 적이 있으면 그중 가장 많이 맞닿은 부대를 고릅니다.
            // 아무도 닿지 않았다면 시야 안의 적을 고릅니다. (원거리 교전)
            Army bestArmy = null;
            int bestNum = 0;

            Army bestSighted = null;
            float bestSightedSqr = float.MaxValue;
            Vector3 myPosition = GetPosition();

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

                if (detected.bsighted)
                {
                    Vector3 to = detected.army.GetPosition() - myPosition;
                    to.y = 0.0f;

                    float sqr = to.sqrMagnitude;
                    if (sqr < bestSightedSqr)
                    {
                        bestSightedSqr = sqr;
                        bestSighted = detected.army;
                    }
                }
            }

            // 접촉이 없으면 시야 표적으로 대체합니다.
            bool bmelee = bestArmy != null;
            if (!bmelee) bestArmy = bestSighted;

            // 유효한 적을 찾지 못했으면 타겟을 지정하지 않습니다.
            if (bestArmy == null)
            {
                bfindTarget = false;
            }
            else
            {
                targetArmy = bestArmy;

                // 맞닿았으면 난전, 아직 떨어져 있으면 원거리 교전입니다.
                army_Data.e_Army_Fight = bmelee
                    ? E_Army_Fight.Melee
                    : E_Army_Fight.Range;

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

        // 타겟 유닛 데이터를 재사용 버퍼에 담습니다.
        //
        // 버퍼는 실제 적 인원보다 클 수 있으므로 앞의 targetCount칸만 잘라 씁니다.
        // 이 구분이 없으면 남는 뒤쪽 칸에 남은 '지난 틱의 적'이 격자에 색인되어
        // 이미 죽었거나 다른 부대의 유닛을 공격 대상으로 잡습니다.
        int targetCount = targetUnits.Count;

        Ensure_Capacity(ref target_Unit_Datas, targetCount);
        var targetDatas = target_Unit_Datas.GetSubArray(0, targetCount);

        for (int i = 0; i < targetCount; i++)
        {
            targetDatas[i] = targetUnits[i].unit_Data;
        }

        // 적 유닛을 공간 격자에 색인합니다.
        // 이렇게 하면 내 유닛이 적 '전부'가 아니라 인접 셀만 검사하면 됩니다.
        float cellSize = Spatial_Grid.GetCellSize(army_Data, targetArmy.army_Data);

        // 격자는 매 틱 새로 만들지 않고 재사용합니다.
        // 부대마다 할당/해제하면 교전이 붙은 틱에 그 비용이 부대 수만큼 쌓입니다.
        if (!fightGrid.IsCreated || fightGrid.Capacity < targetCount)
        {
            if (fightGrid.IsCreated) fightGrid.Dispose();
            fightGrid = new NativeParallelMultiHashMap<int, int>(
                Mathf.Max(targetCount, 64), Allocator.Persistent);
        }
        else
        {
            fightGrid.Clear();
        }

        Spatial_Grid_Build_Job buildJob = new Spatial_Grid_Build_Job();
        buildJob.unit_Datas = targetDatas;
        buildJob.cellSize = cellSize;
        buildJob.grid = fightGrid.AsParallelWriter();

        // 반드시 targetCount만큼만 색인합니다. (버퍼 .Length가 아닙니다)
        JobHandle buildHandle = buildJob.Schedule(targetCount, Constant.jobBatchCount);

        // Unit_Fight_Job을 생성하고 실행합니다. (격자 구축이 끝난 뒤 실행되도록 의존성 연결)
        Unit_Fight_Job unit_Fight_Job = new Unit_Fight_Job();
        unit_Fight_Job.unit_Datas = unit_Datas;
        unit_Fight_Job.target_Unit_Datas = targetDatas;
        unit_Fight_Job.targetGrid = fightGrid;
        unit_Fight_Job.cellSize = cellSize;
        unit_Fight_Job.armyData = army_Data;
        unit_Fight_Job.targetArmyData = targetArmy.army_Data;
        // 시드는 '틱 번호와 부대 인덱스의 함수'입니다.
        //
        // UnityEngine.Random을 쓰면 전역 난수 상태를 소비하므로,
        // 이펙트나 UI가 난수를 몇 번 뽑았는지에 따라 전투 결과가 달라집니다.
        // 같은 명령을 같은 순서로 내려도 재현되지 않습니다.
        //
        // 좌표의 함수로 만들면 호출 횟수와 순서에 영향받지 않으므로
        // 리플레이와 회귀 테스트가 성립합니다.
        // Job 안에서 다시 유닛 인덱스와 섞여 '유닛마다 다른' 난수가 됩니다.
        unit_Fight_Job.randomSeed =
            Simulation_Random.Seed_For(Simulation_Clock.tick, armyIndex);

        // 의존성 주의:
        // 이 Job은 unit_Datas에 씁니다. 같은 배열에 쓰는 Unit_Job이 아직
        // 끝나지 않았을 수 있으므로 반드시 그 핸들 뒤에 이어 붙여야 합니다.
        // (안 그러면 두 Job이 같은 메모리를 동시에 써 결과가 뒤엉킵니다)
        JobHandle dependency = JobHandle.CombineDependencies(buildHandle, unitJobHandle);

        fightJobHandle = unit_Fight_Job.Schedule(units.Count, Constant.jobBatchCount, dependency);
        bfightJobScheduled = true;

        // 여기서 Complete()하지 않습니다. 전 부대가 스케줄을 끝낸 뒤
        // Controller가 일괄로 기다립니다.
        // target_Unit_Datas는 재사용 버퍼이므로 해제하지 않습니다. (OnDestroy에서 반납)
    }
}
