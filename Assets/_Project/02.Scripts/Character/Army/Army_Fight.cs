
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 부대의 "교전 판단" 책임을 담당하는 부분 클래스입니다.
///
/// 담는 것: 표적 부대 선정, 근접/원거리 교전 상태 전이, 유닛 단위 전투 Job 스케줄.
/// 공통점은 전부 '누구와 어떻게 싸울지를 정하는' 일이라는 점입니다.
/// 감지(Army_Perception)가 채워 둔 army_Detected를 소비하며,
/// 이동(Army_Move)이나 사기 산출(Army_Morale)과는 관심사가 다릅니다.
///
/// 역할 분담 주의:
///   여기(메인 스레드) : 참조 타입(army_Detected, units)을 순회해 표적을 고르고
///                       Job에 넘길 입력을 꾸립니다. Burst로 옮길 수 없습니다.
///   Unit_Fight_Job    : 그 표적을 상대로 유닛마다 명중과 피해를 정산합니다.
///                       값 타입만 다루므로 병렬 실행됩니다.
///
/// 스케줄/완료 분리 주의:
/// _Schedule_Target()은 Job을 걸어 두기만 하고 기다리지 않습니다.
/// 전 부대가 스케줄을 끝낸 뒤 Controller가 _Complete_Target()으로 일괄
/// 대기하므로, 부대 하나가 다음 부대의 스케줄을 막지 않습니다.
/// </summary>
partial class Army
{
    // 비공개 멤버 변수 (Army.cs에서 관리)
    // Unity 이벤트 함수 (Army.cs에서 관리)

    // 공개 메서드
    /// <summary>
    /// 탐지된 적 중에서 이번 교전의 표적 부대를 고릅니다.
    ///
    /// 선정 규칙은 접촉 우선입니다. 몸이 맞닿은 적이 있으면 그중 가장 많이
    /// 닿은 부대를 고르고 난전(Melee)으로 넘어갑니다. 아무도 닿지 않았다면
    /// 시야 안의 가장 가까운 적을 골라 원거리 교전(Range)으로 처리합니다.
    ///
    /// 이미 난전 중이면 표적을 바꾸지 않습니다. 다만 원거리 교전은 예외로
    /// 재선정을 허용합니다. 쏘던 중에 적이 달려와 맞닿으면 난전으로 넘어가야
    /// 하는데, 여기서 막으면 영원히 Range 상태로 굳어 근접 판정이 서지 않습니다.
    /// </summary>
    /// <returns>표적을 새로 지정했으면 true, 그러지 못했으면 false를 반환합니다.</returns>
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
    /// 이번 틱에 나와 피해를 주고받을 수 있는 적 부대들입니다. 매 틱 재사용합니다.
    /// </summary>
    private readonly List<Army> fightArmies = new List<Army>();

    /// <summary>
    /// 교전 상대 목록을 모읍니다. 표적 부대와 '이미 탐지된 모든 적 부대'입니다.
    ///
    /// 왜 표적 하나로는 부족한가:
    /// 피해는 이 목록에 담긴 부대에서만 들어옵니다. 예전에는 targetArmy
    /// 하나뿐이어서, 나를 때리는 부대가 내 표적이 아니면 그 타격이
    /// 통째로 버려졌습니다. 명중 판정까지 끝난 뒤 아무도 읽지 않고 사라집니다.
    ///
    /// 실측(9,600명 / 701틱): 명중 1,426회 중 실제 피해는 228회(16%)뿐이었고,
    /// 몸을 맞대고도 피해를 못 주는 부대가 틱당 2.76개였습니다.
    /// 사기는 포위를 정확히 세는데(Morale_Modifiers.surrounded) 체력은
    /// 그만큼 줄지 않는 상태였습니다.
    ///
    /// army_Detected를 쓰는 이유:
    /// 이 목록은 접촉(Add_Contact)과 시야(_Update_Detection)로 이미 채워져
    /// 있습니다. 즉 '나와 상호작용할 수 있는 거리에 있는 적'이 그대로
    /// 들어 있으므로, 새 탐색을 돌릴 필요가 없습니다.
    /// </summary>
    private void Collect_Fight_Armies()
    {
        fightArmies.Clear();

        // 표적 부대는 아직 접촉 전이어도 반드시 포함합니다.
        // (원거리 교전은 닿지 않은 채로 성립합니다)
        if (Is_Damage_Source(targetArmy))
        {
            fightArmies.Add(targetArmy);
        }

        if (army_Detected == null) return;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            Army other = army_Detected[i].army;

            if (!Is_Damage_Source(other)) continue;
            if (fightArmies.Contains(other)) continue;

            fightArmies.Add(other);
        }
    }

    /// <summary>
    /// 이 부대가 나에게 피해를 줄 수 있는 상대인지 판정합니다.
    ///
    /// 이 함수가 피해 경로의 '유일한 기준'입니다.
    /// <see cref="Collect_Fight_Armies"/>가 피해원을 모을 때와
    /// <see cref="Measure_Damage_Path"/>가 누락을 검사할 때 같은 것을 봐야,
    /// 계측이 실제 동작을 감시하는 의미가 있습니다. 두 곳에 조건을 따로
    /// 적으면 한쪽만 좁아져도 지표가 0으로 남아 아무도 눈치채지 못합니다.
    /// </summary>
    /// <param name="other">검사할 상대 부대입니다.</param>
    /// <returns>피해를 주고받을 수 있으면 true입니다.</returns>
    private bool Is_Damage_Source(Army other)
    {
        if (other == null) return false;
        if (other == this) return false;
        if (other.units.Count == 0) return false;
        if (other.army_Data.bplayer == army_Data.bplayer) return false;

        return true;
    }

    /// <summary>
    /// 교전 상대들의 유닛을 상대로 유닛 단위 전투 Job을 스케줄합니다.
    ///
    /// 적 유닛을 공간 격자에 색인한 뒤 전투 Job을 겁니다. 격자가 있어야
    /// 내 유닛이 적 '전부'가 아니라 인접 셀만 검사하므로, 교전 비용이
    /// 인원 수의 곱이 아니라 접촉면에 비례하게 됩니다.
    ///
    /// 여기서 Complete()하지 않습니다. 대기는 _Complete_Target()이 합니다.
    /// </summary>
    /// <remarks>
    /// 이 Job은 '내 유닛'에만 씁니다. 적 배열은 읽기 전용으로 참조하며,
    /// 적이 받는 피해는 적 부대가 자기 _Update_Target_Unit에서 스스로 적용합니다.
    /// 그래서 이 함수는 표적이 없어도, 심지어 부대가 무너져 있어도 반드시
    /// 호출되어야 합니다. 여기가 '내가 맞는 피해'를 정산하는 유일한 자리입니다.
    /// </remarks>
    /// <param name="bcanAcquireTarget">
    /// 이 부대가 표적을 새로 잡을 수 있는지 여부입니다.
    /// 무너진 부대는 false입니다. 그래도 피해는 그대로 받습니다.
    /// </param>
    public void _Update_Target_Unit(bool bcanAcquireTarget = true)
    {
        if (units.Count == 0) return;

        Collect_Fight_Armies();
        if (fightArmies.Count == 0) return;

        // 교전 상대 전원의 인원을 미리 세어 버퍼를 확보합니다.
        int capacity = 0;
        for (int a = 0; a < fightArmies.Count; a++)
        {
            capacity += fightArmies[a].units.Count;
        }

        if (capacity == 0) return;

        Ensure_Capacity(ref target_Unit_Datas, capacity);

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.S_TargetCopy);

        // 여러 부대의 유닛을 하나의 배열로 모읍니다.
        //
        // 공격자의 부대 스탯은 유닛이 들고 있는 armyIndex로 찾으므로,
        // 섞여 있어도 각자의 공격력·피로·고지 보정이 정확히 적용됩니다.
        //
        // 셀 크기는 '여기 담긴 부대 전부'의 최대 사거리여야 합니다.
        // 하나라도 짧게 잡으면 그 부대의 공격이 3x3 탐색에서 누락됩니다.
        int targetCount = 0;
        float maxReach = Spatial_Grid.GetReach(army_Data);

        for (int a = 0; a < fightArmies.Count; a++)
        {
            Army foe = fightArmies[a];
            List<Unit> foeUnits = foe.units;

            // 갱신을 건너뛴 부대(LOD)는 목록에 죽은 자리가 남아 있을 수 있습니다.
            for (int i = 0; i < foeUnits.Count; i++)
            {
                if (foeUnits[i] == null) continue;
                target_Unit_Datas[targetCount++] = foeUnits[i].unit_Data;
            }

            float reach = Spatial_Grid.GetReach(foe.army_Data);
            if (reach > maxReach) maxReach = reach;
        }

        Tick_Profiler.End_Sub();

        if (targetCount == 0) return;

        // 버퍼는 실제 인원보다 클 수 있으므로 앞의 targetCount칸만 잘라 씁니다.
        // 이 구분이 없으면 남는 뒤쪽 칸에 남은 '지난 틱의 적'이 격자에 색인되어
        // 이미 죽었거나 다른 부대의 유닛을 공격 대상으로 잡습니다.
        var targetDatas = target_Unit_Datas.GetSubArray(0, targetCount);

        // 적 유닛을 공간 격자에 색인합니다.
        // 이렇게 하면 내 유닛이 적 '전부'가 아니라 인접 셀만 검사하면 됩니다.
        float cellSize = Mathf.Max(maxReach, Spatial_Grid.minCellSize);

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

        // 공격자의 부대 스탯은 유닛의 armyIndex로 찾습니다.
        // 여러 부대가 섞인 배열을 상대하므로 고정된 '적 부대' 하나를 넘길 수 없습니다.
        unit_Fight_Job.armyDatas = scheduleArmyDatas;
        unit_Fight_Job.bcanAcquireTarget = bcanAcquireTarget;
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
        // 이 Job은 unit_Datas에 씁니다. 같은 배열에 '쓰는' Unit_Job이나
        // 같은 배열을 '읽는' 자세 추출 Job이 아직 돌고 있으면 Unity의 Job
        // 안전 시스템이 예외를 던집니다. (충돌은 병렬로 둘 수 없습니다)
        //
        // 그래서 애니메이션 갈래의 첫 단계인 추출까지 기다린 뒤 씁니다.
        // 추출은 자세만 복사하는 아주 짧은 Job이라 이 대기는 저렴하고,
        // 정작 무거운 애니메이션 계산은 그 뒤에서 계속 병렬로 돕니다.
        JobHandle dependency = JobHandle.CombineDependencies(
            buildHandle, unitJobHandle, poseExtractHandle);

        fightJobHandle = unit_Fight_Job.Schedule(units.Count, Constant.jobBatchCount, dependency);
        bfightJobScheduled = true;

        // 여기서 Complete()하지 않습니다. 전 부대가 스케줄을 끝낸 뒤
        // Controller가 일괄로 기다립니다.
        // target_Unit_Datas는 재사용 버퍼이므로 해제하지 않습니다. (OnDestroy에서 반납)
    }

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
    /// 스케줄된 Job이 없으면 아무 일도 하지 않습니다.
    /// </summary>
    void _Complete_Target()
    {
        if (!bfightJobScheduled) return;

        fightJobHandle.Complete();
        bfightJobScheduled = false;

        // 격자는 재사용하므로 여기서 해제하지 않습니다. (OnDestroy에서 반납)
    }

    /// <summary>
    /// 이번 틱의 교전 상태를 갱신합니다. 표적 부대를 정한 뒤 유닛 전투를 겁니다.
    ///
    /// 표적 '부대' 재지정은 타이머로 묶어 두고, 유닛 단위 전투는 매 틱 돌립니다.
    /// 부대 표적이 매 틱 갈리면 전투가 불안정해지지만, 유닛 표적까지 묶어 두면
    /// 눈앞의 적이 죽어도 한동안 허공을 치기 때문입니다.
    ///
    /// 붕괴한 부대는 이 단계 전체를 건너뜁니다. 무너진 부대는 싸우지 않습니다.
    /// </summary>
    void _Update_Target()
    {
        // 붕괴한 부대는 표적을 고르지 않습니다. 오직 달아납니다.
        //
        // 주의: 여기서 반환하면 안 됩니다.
        //
        // 예전에는 이 자리에서 곧바로 return했습니다. 그런데 아래
        // _Update_Target_Unit이 거는 Job은 '내가 때리는' 계산이 아니라
        // **'내가 맞는' 계산**입니다. 그래서 조기 반환은 곧
        // "무너진 부대는 아무에게도 맞지 않는다"는 뜻이 되었습니다.
        //
        // 패주하는 적을 베는 것은 이 장르에서 사상자가 가장 많이 나는
        // 국면입니다. 그 국면이 통째로 무효였습니다.
        bool bbroken = army_Data.IsBroken();

        if (bbroken)
        {
            targetArmy = null;
            army_Data.e_Army_Fight = E_Army_Fight.Non;
        }
        else if (army_Detected.Count > 0)
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

        // 타겟 부대가 전멸했으면 교전을 종료합니다.
        if (targetArmy != null && targetArmy.units.Count == 0)
        {
            targetArmy = null;
            army_Data.e_Army_Fight = E_Army_Fight.Non;
        }

        // 표적이 없어도 전투 Job은 반드시 겁니다.
        //
        // 이 Job은 '내가 맞는 피해'를 정산하는 곳이기도 하기 때문입니다.
        // 표적이 없다고 건너뛰면, 나를 때리는 적이 있어도 그 타격이
        // 계산만 되고 사라집니다. 실제로 몸을 맞대고도 피해를 주지 못하는
        // 부대가 틱당 2.76개였습니다.
        //
        // 무너진 부대는 표적을 새로 잡지 못하게 막되(false), 맞는 것은
        // 그대로 둡니다. 패주 중에는 반격하지 않지만 베이기는 합니다.
        _Update_Target_Unit(!bbroken);
    }
}
