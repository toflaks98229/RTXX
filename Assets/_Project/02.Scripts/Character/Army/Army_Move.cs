using System;
using System.Linq;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 부대의 이동 관련 로직을 담당하는 부분 클래스입니다.
/// </summary>
partial class Army
{
    // 비공개 멤버 변수 (Army.cs에서 관리)
    // 공개 메서드 (Army.cs에서 관리)
    // Unity 이벤트 함수 (Army.cs에서 관리)

    // 비공개 메서드
    /// <summary>
    /// 부대의 이동 상태에 따라 동작을 수행합니다.
    /// </summary>
    void _Update_Move()
    {
        switch (army_Data.e_Army_Move)
        {
            case E_Army_Move.Idle:
                // 산개 태세는 적이 다가오면 자동으로 물러납니다.
                // 이 처리가 먼저 와야 궁병이 근접에 휘말리기 전에 빠집니다.
                if (Try_Skirmish_Withdraw()) break;

                // 부대 기준점은 '명령으로 설정된 전열 방향'을 유지합니다.
                // 적을 쫓아 회전하면 도망치는 적을 따라 전열이 계속 틀어집니다.
                Rotation_To_Formation();

                if (army_Data.breformation)
                {
                    // 교전 중에도 대열을 유지해야 합니다.
                    // 사상자로 벌어진 자리를 그대로 두면 그 틈으로 적이 파고듭니다.
                    if (targetArmy != null)
                    {
                        Move_Reformation_Combat();
                    }
                    else
                    {
                        Move_Reformation();
                    }
                }
                break;
            case E_Army_Move.Move:
                Move();
                break;
            case E_Army_Move.MoveToTarget:
                break;
            case E_Army_Move.MoveCharge:
                Move_Charge();
                break;
            case E_Army_Move.MoveEscape:
                Move_Escape();
                break;
        }
    }

    /// <summary>
    /// 산개(Skirmish) 태세의 자동 후퇴입니다.
    ///
    /// 원거리 부대가 근접에 휘말리면 아무것도 못 하고 죽습니다.
    /// 적이 일정 거리 안으로 들어오면 사격을 이어 가면서 물러나
    /// 거리를 유지합니다. 이것이 궁병을 운용 가능하게 만드는 장치입니다.
    /// </summary>
    /// <returns>후퇴를 수행했으면 true입니다.</returns>
    bool Try_Skirmish_Withdraw()
    {
        if (army_Data.e_Army_Stance != E_Army_Stance.Skirmish) return false;
        if (army_Data.IsBroken()) return false;
        if (units.Count == 0) return false;

        // 가장 가까운 적이 물러날 거리 안에 있는지 봅니다.
        Army nearest = Find_Nearest_Enemy_Army(out float distance);
        if (nearest == null) return false;
        if (distance > Constant.stance_Skirmish_Flee_Distance) return false;

        Vector3 away = GetPosition() - nearest.GetPosition();
        away.y = 0.0f;

        if (away.sqrMagnitude < 0.0001f) return false;

        away = away.normalized;

        float speed = army_Data.GetMoveSpeed() * Constant.stance_Skirmish_Flee_Rate;
        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_AgentMove);
        navMeshAgent.Move(away * speed * Time.fixedDeltaTime);
        Tick_Profiler.End_Sub();

        // 물러나면서도 적을 향한 자세는 유지합니다. 계속 쏘아야 하기 때문입니다.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].Move_Skirmish(away, speed);
        }

        return true;
    }

    /// <summary>
    /// 사기가 붕괴한 부대의 패주 이동입니다.
    /// 진형을 버리고 적 반대 방향으로 달아납니다.
    /// </summary>
    void Move_Escape()
    {
        Vector3 direction = army_Data.escapeDirection;

        // 달아날 방향이 정해지지 않았으면 현재 바라보는 반대쪽으로 물러납니다.
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -formation_Move_Transform.forward;
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            direction = direction.normalized;
        }

        // 공포에 질려 평소보다 빠르게 달립니다.
        // 패주는 공포로 달리므로 피로 감쇠를 덜 받습니다.
        float speed = army_Data.GetMoveSpeed() * Constant.rout_Speed_Rate;
        Vector3 movementVector = direction * speed * Time.fixedDeltaTime;

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_AgentMove);
        navMeshAgent.Move(movementVector);
        Tick_Profiler.End_Sub();

        // 기준점이 NavMesh 위에 남아 있도록 붙잡습니다.
        //
        // 왜 필요한가:
        // 패주는 '적 반대 방향'으로 무작정 달리는 유일한 경로입니다.
        // 다른 이동은 전부 명령받은 목적지가 있어 NavMesh 안에 머물지만,
        // 패주는 전장 가장자리에 닿아도 계속 같은 방향으로 밀어붙입니다.
        //
        // NavMeshAgent.Move()는 에이전트가 NavMesh 위에 있을 때만 표면에
        // 붙여 줍니다. 일단 벗어나면 그 보정이 사라져 기준점이 지형과
        // 무관한 높이로 떠 버립니다. 실측에서 패주 부대의 기준점이
        // 지면에서 43.9m까지 벌어졌고, 그 값이 계속 커졌습니다.
        //
        // 기준점이 뜨면 진형 슬롯도 함께 뜨고, 유닛은 닿을 수 없는 자리를
        // 향해 달리게 됩니다. (유닛 자체는 지면 동기화로 붙어 있습니다)
        Clamp_Pivot_To_NavMesh();

        // 기준점을 지면 높이에 맞춥니다.
        //
        // 위의 Clamp_Pivot_To_NavMesh는 '내브메시를 벗어났는가'만 봅니다.
        // 그런데 내브메시 표면은 지형과 높이가 다를 수 있어(경사·단차),
        // 내브메시 위에 잘 있어도 기준점이 지형에서 떠 있을 수 있습니다.
        //
        // 기준점이 뜨면 진형 슬롯도 함께 뜨고, 유닛은 닿을 수 없는 자리를
        // 향해 달립니다. Move_Stop이 같은 이유로 이미 지면 스냅을 합니다.
        //
        // 왜 이제야 드러났는가:
        // 패주는 사기가 무너져야 일어나는데, 전투가 성립하지 않던 동안에는
        // 붕괴가 한 번도 발생하지 않아 이 경로 자체가 실행된 적이 없었습니다.
        // 전투가 돌기 시작하자(2,000틱에서 붕괴 44회) 검증기가 8.7~9.7m
        // 이격을 잡아냈습니다.
        formation_Move_Transform.position =
            Snap_To_Ground(formation_Move_Transform.position);

        // 달아나는 쪽을 바라보게 합니다.
        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
        formation_Move_Transform.rotation = Quaternion.RotateTowards(
            formation_Move_Transform.rotation,
            lookRotation,
            army_Data.GetRotationSpeed() * Time.fixedDeltaTime);

        // 유닛들도 진형 슬롯을 버리고 부대 기준점을 따라 흩어져 도주합니다.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].Move_Escape(direction);
        }
    }

    /// <summary>
    /// 부대 기준점을 NavMesh 위로 되돌립니다.
    ///
    /// 이미 NavMesh 위에 있으면 아무 일도 하지 않습니다.
    /// 벗어났을 때만 가장 가까운 지점으로 끌어당깁니다.
    ///
    /// 왜 SamplePosition을 쓰는가:
    /// agent.Warp()는 에이전트를 강제로 옮기지만 NavMesh 밖이면 실패합니다.
    /// SamplePosition으로 유효한 지점을 먼저 찾은 뒤 옮겨야 확실합니다.
    /// </summary>
    void Clamp_Pivot_To_NavMesh()
    {
        if (navMeshAgent == null) return;
        if (!navMeshAgent.isActiveAndEnabled) return;

        // NavMesh 위에 잘 있으면 건드릴 이유가 없습니다.
        if (navMeshAgent.isOnNavMesh) return;

        Vector3 position = formation_Move_Transform.position;

        // 반경은 넉넉히 잡습니다. 한 틱에 벗어나는 거리보다 훨씬 커야
        // 가장자리에서 튕겨 나간 뒤에도 되돌아올 수 있습니다.
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 50.0f, NavMesh.AllAreas))
        {
            navMeshAgent.Warp(hit.position);
        }
    }

    /// <summary>
    /// 부대를 목표 방향으로 회전시킵니다.
    ///
    /// ---------------------------------------------------------------------
    /// navMeshAgent.steeringTarget을 쓰면 안 되는 이유
    /// ---------------------------------------------------------------------
    /// 이 부대의 에이전트는 _Start()에서 isStopped = true로 세워 둡니다.
    /// 경로를 따라 걷게 하지 않고 Move()가 직접 밀기 때문입니다.
    /// 그런데 멈춰 선 에이전트의 steeringTarget은 '다음 경로 지점'이 아니라
    /// **자기 위치 그대로**입니다. 따라서 방향이 언제나 영벡터가 됩니다.
    ///
    /// 그 결과 두 가지가 벌어졌습니다.
    ///   1) Quaternion.LookRotation이 "viewing vector is zero" 오류를 냅니다.
    ///      실측에서 701틱 동안 20,493회, 즉 이동 중인 부대 전부가 매 틱입니다.
    ///   2) 오류 시 항등 회전이 돌아오므로, RotateTowards가 부대를 월드
    ///      정면(+Z)으로 서서히 돌립니다. Move()는 그 forward 방향으로
    ///      전진하므로, **부대가 명령한 곳이 아니라 +Z로 행군합니다.**
    ///
    /// 이것이 60개 부대가 서로 만나지 못하고 701틱 동안 사망자가 2명뿐이던
    /// 원인입니다. 전투가 성립하지 않으니 밸런스 수치도 검증된 적이 없었습니다.
    ///
    /// 방향은 '가야 할 곳'에서 직접 구합니다. locationMoveTo는 명령이
    /// 확정한 목적지이므로 에이전트 상태와 무관하게 언제나 유효합니다.
    /// </summary>
    void Rotation()
    {
        Vector3 direction = locationMoveTo - formation_Move_Transform.position;
        direction.y = 0.0f;

        // 이미 도착했으면 방향을 바꿀 이유가 없습니다.
        // (여기서 막지 않으면 위 1)번 오류가 그대로 재발합니다)
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        formation_Move_Transform.rotation = Quaternion.RotateTowards(
            formation_Move_Transform.rotation,
            lookRotation,
            army_Data.GetRotationSpeed() * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 부대의 현재 평균 위치를 반환합니다.
    ///
    /// 이 값은 매 틱 _Upadate_Data()에서 한 번만 계산되어 캐시됩니다.
    ///
    /// 왜 캐시하는가:
    /// 원래는 호출할 때마다 전 유닛의 transform.position을 더했습니다.
    /// transform.position 접근은 네이티브 마샬링 비용이 있는데,
    /// 이 함수는 한 틱에 다음 위치에서 반복 호출됩니다.
    ///   - _Update_Detection : 자기 1회 + allArmies 순회마다 other.GetPosition()
    ///   - Find_Nearest_Enemy_Army / Find_Enemy_Army_Near : allArmies 전체 순회
    ///   - Battle_AI의 Decide / Find_Soft_Target / Is_Cavalry_Near
    ///   - 사기, 돌격 충격, 충돌 공격 정산
    /// 부대 20개 x 각 300명이면 _Update_Detection 하나만으로도
    /// 틱당 12만 회의 transform.position 접근이 발생했습니다.
    /// 부대 수의 '제곱'으로 악화되는 구조라 인원이 늘수록 급격히 나빠집니다.
    /// </summary>
    /// <returns>부대의 평균 위치입니다.</returns>
    public Vector3 GetPosition()
    {
        // 아직 이번 틱의 값이 준비되지 않았다면 즉시 계산합니다.
        // (_Start 직후나 배치 단계처럼 시뮬레이션 틱 밖에서 호출되는 경우입니다)
        if (!bcenterPositionValid) Update_Center_Position();

        return centerPosition;
    }

    /// <summary>
    /// 부대 평균 위치를 실제로 계산해 캐시에 담습니다.
    /// 매 틱 _Upadate_Data()에서 한 번만 호출됩니다.
    /// </summary>
    private void Update_Center_Position()
    {
        Vector3 position = new Vector3();
        int alive = 0;

        // unit_Data.position이 이번 틱의 진짜 위치입니다.
        // Transform을 읽으면 네이티브 왕복이 인원수만큼 발생하는데,
        // 9,600명 기준 그것만으로 6 ms입니다. 데이터를 읽으면 사실상 공짜입니다.
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit == null) continue;
            if (unit.IsDead()) continue;

            position += unit.unit_Data.position;
            alive++;
        }

        // 생존 인원으로 나눕니다.
        //
        // 기존에는 army_Data.unit_Num으로 나눴는데, 이 값은 틱 마지막
        // (_Update_Dead)에서만 갱신되므로 합계에 포함된 인원 수와
        // 어긋날 수 있었습니다. 실제로 더한 수로 나누는 것이 정확합니다.
        centerPosition = alive > 0 ? position / alive : Vector3.zero;
        bcenterPositionValid = true;
    }

    /// <summary>
    /// 캐시된 평균 위치를 무효화합니다.
    /// 유닛이 크게 움직인 뒤(생성, 재배치) 다음 조회에서 다시 계산하게 합니다.
    /// </summary>
    public void Invalidate_Center_Position()
    {
        bcenterPositionValid = false;
    }

    /// <summary>
    /// 부대 이동의 기준이 되는 위치를 설정합니다.
    /// </summary>
    void Set_Army_Move_Position()
    {
        Vector3 position = GetPosition();
        Vector3 direction = locationMoveTo - GetPosition();

        // 이미 목적지에 서 있으면 방향을 정할 수 없습니다.
        //
        // 아래 두 분기가 LookAt을 부르는데, 영벡터를 넘기면 Unity가
        // "Look rotation viewing vector is zero" 오류를 내고 회전이
        // 망가집니다. 제자리 재편성처럼 목적지가 곧 현재 위치인 경우가
        // 실제로 있으므로 여기서 걸러야 합니다.
        // (기준점 높이만 지면에 맞추고 방향은 그대로 둡니다)
        if (direction.sqrMagnitude < 0.0001f)
        {
            formation_Move_Transform.position = Snap_To_Ground(position);
            return;
        }

        if (Vector3.Angle(direction, formation_Move_Transform.forward) < 45.0f)
        {
            position = formation_Move_Transform.position;
        }
        else if (Vector3.Angle(direction, formation_Move_Transform.forward) < 135.0f)
        {
            position = position + direction.normalized * GetFormation_Length() * 0.5f;
            formation_Move_Transform.LookAt(locationMoveTo, Vector3.up);
        }
        else
        {
            // GetFormation_Num()이 0이면 정수 나눗셈에서 DivideByZeroException이 납니다.
            int formationNum = Mathf.Max(1, GetFormation_Num());
            position = position + direction.normalized * army_Data.GetInterval() * army_Data.unit_Num / formationNum * 0.5f;
            formation_Move_Transform.LookAt(locationMoveTo, Vector3.up);
        }

        // 기준점은 언제나 지면 위에 있어야 합니다.
        //
        // position은 부대 평균 위치(GetPosition)나 기존 기준점에서 유도되므로
        // 그 Y가 이미 어긋나 있으면 그대로 물려받습니다. 한 번 잠기면
        // 명령을 내릴 때마다 계속 잠긴 채 이어집니다.
        formation_Move_Transform.position = Snap_To_Ground(position);
    }

    // 공개 메서드
    /// <summary>
    /// 기본 설정을 사용하여 이동을 시작합니다.
    /// </summary>
    public void Move_Start()
    {
        Move_Start(GetFormation_Length(), GetFormation_Direction(), GetFormation_Position());
    }

    /// <summary>
    /// 방향과 위치를 기반으로 이동을 시작합니다.
    /// </summary>
    /// <param name="direction">이동 방향입니다.</param>
    /// <param name="position">이동 위치입니다.</param>
    public void Move_Start(Vector3 direction, Vector3 position)
    {
        Move_Start(GetFormation_Num(), direction, position);
    }

    /// <summary>
    /// 진형 길이, 방향, 위치를 기반으로 이동을 시작합니다.
    /// </summary>
    /// <param name="length">진형 길이입니다.</param>
    /// <param name="direction">이동 방향입니다.</param>
    /// <param name="position">이동 위치입니다.</param>
    public void Move_Start(float length, Vector3 direction, Vector3 position)
    {
        Move_Start_Internal(Set_Formation(length, direction, position), direction);
    }

    /// <summary>
    /// 진형 유닛 수를 기반으로 이동을 시작합니다.
    /// </summary>
    /// <param name="num">진형 유닛 수입니다.</param>
    /// <param name="direction">이동 방향입니다.</param>
    /// <param name="position">이동 위치입니다.</param>
    public void Move_Start(int num, Vector3 direction, Vector3 position)
    {
        Move_Start_Internal(Set_Formation(num, direction, position), direction);
    }

    /// <summary>
    /// 이동 시작의 공통 구현입니다.
    ///
    /// 두 공개 오버로드는 진형 폭을 '길이(m)'로 주느냐 '인원 수'로 주느냐만
    /// 다릅니다. 그 차이는 Set_Formation 호출 한 줄에서 이미 흡수되므로,
    /// 그 뒤의 절차는 완전히 같습니다.
    ///
    /// 예전에는 이 35줄이 두 오버로드에 그대로 복사되어 있었습니다.
    /// 한쪽만 고치면 플레이어 명령과 AI 명령이 다르게 동작하는 구조였고,
    /// 실제로 마커 방향이 그렇게 갈렸습니다.
    /// </summary>
    /// <param name="formationData">이미 계산된 진형 데이터입니다.</param>
    /// <param name="lineDirection">전열축 방향입니다. 마커 정면 계산에 씁니다.</param>
    private void Move_Start_Internal(Formation_Data formationData, Vector3 lineDirection)
    {
        // 전멸했거나 진형 계산이 실패하면 배치할 것이 없습니다.
        // (formation[0] 접근이 IndexOutOfRange를 내는 것을 막습니다)
        if (formationData == null || formationData.formation.Count == 0) return;

        bformation_Move = false;
        army_Data.e_Army_Move = E_Army_Move.Move;

        // 행군 목적지는 '대열의 한가운데'입니다.
        //
        // 예전에는 formation[0](첫 슬롯)을 썼습니다. 그때는 저장된 position이
        // 곧 첫 슬롯이라 사실상 같은 값이었지만, 규약이 중심으로 바뀐 지금은
        // 짝수 폭에서 간격의 절반만큼 어긋납니다. 중심을 직접 쓰는 편이
        // 뜻이 분명하고 폭의 홀짝에 흔들리지 않습니다.
        locationMoveTo = formationData.position;
        Set_Army_Move_Position();
        Set_Formation_Data(formationData);
        Set_Formation_Move();

        int[] _matchX = Match_Units_To_Slots(Get_Formation_Move_Positions());
        if (_matchX == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            int slot = _matchX[i];

            units[i].Move_Start(slot, Get_Slot_World(slot));

            if (slot == 0)
            {
                unit_Bearing_Flag = units[i];
            }
        }

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_SetDestination);
        navMeshAgent.SetDestination(locationMoveTo);
        Tick_Profiler.End_Sub();
        Tick_Profiler.Count_Path_Request();

        // 마커는 유닛이 실제로 설 방향(전열의 정면)을 가리켜야 합니다.
        //
        // 예전에는 전열축(direction)을 그대로 넘겨, 마커가 대열이 늘어선
        // 방향을 보고 있었습니다. Set_Formation이 세워 둔 방향과도 달라
        // 명령 직후 마커가 한 번 홱 돌아갔습니다.
        Vector3 markerFacing = Formation_Util.Facing_From_Line(lineDirection);

        // 이미 만들어진 마커만 갱신합니다. (없으면 건너뜁니다)
        Update_Markers(formationData.formation, formationData.formation.Count, markerFacing);

        // 배치 단계에서는 걸어가지 않고 즉시 자리를 잡습니다.
        //
        // 왜 필요한가:
        // 배치 중에는 Controller가 시뮬레이션 틱을 통째로 멈춥니다.
        // 위에서 Move_Start로 목적지를 정해도 그것을 실행할 주체가
        // 없어서, 명령을 내려도 병사들이 제자리에 그대로 서 있습니다.
        //
        // 배치는 '전투 전에 진형을 잡는' 단계이므로 결과가 즉시 보여야
        // 합니다. 걸어가는 모습을 보려고 배치를 하는 것이 아닙니다.
        if (Battle_Manager.bdeploying)
        {
            // 기준점을 목적지로 옮기고 그 자리에서 슬롯을 다시 만듭니다.
            //
            // 왜 필요한가:
            // Set_Army_Move_Position은 기준점을 '부대의 현재 위치'에 둡니다.
            // 행군하면서 기준점이 목적지로 옮겨 가는 것을 전제한 구조인데,
            // 배치 중에는 그 행군이 일어나지 않습니다.
            //
            // 그대로 두면 유닛만 목적지로 순간이동하고 슬롯은 출발지에
            // 남습니다. 그러면 전투가 시작된 뒤 재정비가 걸리는 순간
            // 부대 전체가 출발지로 되돌아갑니다.
            //
            // Move_Stop이 기준점을 locationMoveTo로 옮기고 전열 방향으로
            // 세우며, 이동 상태도 대기로 되돌립니다.
            // (그러지 않으면 전투 시작 시 '아직 이동 중'으로 판정되어
            //  이미 도착한 자리를 향해 다시 걸어가려 합니다)
            Move_Stop();

            // 목적지 기준으로 슬롯을 다시 만든 뒤 순간이동해야
            // 유닛이 선 자리와 슬롯이 일치합니다.
            Set_Formation_Move();

            Snap_Units_To_Slots();
        }
    }

    /// <summary>
    /// 포메이션 내 이동을 시작합니다.
    /// </summary>
    void Move_Formation()
    {
        bformation_Move = true;
        int[] _matchX = Match_Units_To_Slots(formation_Data.formation);
        if (_matchX == null) return;

        for (int i = 0; i < _matchX.Length; i++)
        {
            units[i].Move_Start(formation_Data.formation[_matchX[i]]);
        }
    }

    /// <summary>
    /// 부대를 이동시킵니다.
    /// </summary>
    void Move()
    {
        Vector3 movementVector;
        movementVector = formation_Move_Transform.forward.normalized;
        movementVector = movementVector * Time.fixedDeltaTime;

        if (!bformation_Move)
        {
            int army_InPosition_Num = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].GetUnit_Data().IsOnTarget(army_Data))
                {
                    army_InPosition_Num += 1;
                }
            }

            movementVector = movementVector * army_Data.GetMoveSpeed() * army_Data.GetFatigueRate();

            // unit_Num이 0이면 0으로 나누어 NaN이 이동 벡터로 퍼집니다.
            float inPositionRate = army_Data.unit_Num > 0
                ? (float)army_InPosition_Num / army_Data.unit_Num
                : 1.0f;
            movementVector = movementVector * Mathf.Lerp(0.2f, 1.0f, inPositionRate);

            if (army_InPosition_Num < army_Data.unit_Num * 0.5f)
            {
                // 로직 추가 가능
            }

            if (Vector3.Distance(formation_Move_Transform.position, locationMoveTo) < GetFormation_Length() * 0.5f)
            {
                Move_Formation();
            }

            // 사거리 안에 적이 들어왔으면 돌격으로 전환합니다.
            if (Try_Start_Charge()) return;

            // 상당수의 유닛이 적 대열에 막혀 있으면 부대 기준점도 멈춥니다.
            // 기준점이 계속 전진하면 진형 슬롯이 적진 너머에 생기고,
            // 유닛들은 닿지도 못할 자리를 향해 계속 적을 밀게 됩니다.
            if (!Is_Blocked_By_Enemy())
            {
                Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_AgentMove);
                navMeshAgent.Move(movementVector);
                Tick_Profiler.End_Sub();
            }

            Rotation();
        }
        else
        {
            // 진형 이동 단계입니다. 기준점이 목적지에 닿을 때까지 나아갑니다.
            //
            // steeringTarget / remainingDistance를 쓰지 않는 이유는
            // Rotation()의 주석과 같습니다. 멈춰 세운 에이전트에서는
            // steeringTarget이 자기 위치라 이동 벡터가 영벡터가 되고,
            // remainingDistance는 0으로 굳어 **진형 이동에 들어가는 순간
            // 곧바로 Move_Stop()이 걸렸습니다.**
            //
            // 목적지까지의 실제 거리로 판정하면 두 문제가 함께 사라집니다.
            Vector3 toDestination = locationMoveTo - formation_Move_Transform.position;
            toDestination.y = 0.0f;

            if (toDestination.sqrMagnitude > Constant.distance_Stop * Constant.distance_Stop)
            {
                movementVector = toDestination.normalized
                                 * army_Data.GetMoveSpeed()
                                 * army_Data.GetFatigueRate()
                                 * Time.fixedDeltaTime;

                Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_AgentMove);
                navMeshAgent.Move(movementVector);
                Tick_Profiler.End_Sub();
            }
            else
            {
                Move_Stop();
            }
        }
    }

    /// <summary>
    /// 돌격을 시작할 수 있으면 상태를 전환하고 true를 반환합니다.
    /// 붕괴한 부대는 돌격하지 않습니다.
    /// </summary>
    bool Try_Start_Charge()
    {
        if (army_Data.IsBroken()) return false;

        float chargeRange = army_Data.GetMeleeChargeRange();
        if (chargeRange <= 0.0f) return false;

        // 이미 적 대열에 맞닿아 있으면 돌격이 성립하지 않습니다.
        // 붙어 있는 상대에게는 가속할 거리가 없습니다.
        if (Is_Blocked_By_Enemy()) return false;

        // 지금 '진격해 가고 있는 목적지'에 가장 가까운 적 부대를 돌격 대상으로 봅니다.
        // 단순 최근접을 쓰면 명령과 무관한 옆쪽 부대로 뛰쳐나갑니다.
        Army enemy = Find_Enemy_Army_Near(locationMoveTo);
        if (enemy == null) return false;

        Vector3 myPosition = formation_Move_Transform.position;

        // 그 부대까지의 실제 거리로 발동을 판정합니다.
        Vector3 toEnemy = enemy.GetPosition() - myPosition;
        toEnemy.y = 0.0f;

        float distance = toEnemy.magnitude;
        if (distance > chargeRange * Constant.charge_Trigger_Rate) return false;

        // 달려들 거리가 남아 있어야 돌격입니다.
        if (distance < chargeRange * Constant.charge_Min_Distance_Rate) return false;

        // 명령받은 이동 방향이 실제로 적을 향해야 합니다.
        //
        // 이 검사가 없으면 교전 중 후퇴 명령도 돌격으로 바뀝니다.
        // (교전 중에는 거리가 이미 0에 가까워 위 거리 조건을 항상 통과하기 때문입니다)
        Vector3 moveDirection = locationMoveTo - myPosition;
        moveDirection.y = 0.0f;

        if (moveDirection.sqrMagnitude < 0.0001f) return false;

        if (Vector3.Dot(moveDirection.normalized, toEnemy.normalized) < Constant.charge_Facing_Dot)
            return false;

        targetArmy = enemy;
        army_Data.e_Army_Move = E_Army_Move.MoveCharge;

        // 소속 유닛을 모두 돌격 상태로 만듭니다.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            units[i].unit_Data.bcharging = true;
        }

        GameEvents.RaiseArmyCharged(this, enemy);
        return true;
    }

    /// <summary>
    /// 돌격 이동입니다. 적을 향해 최고 속도로 돌진하며,
    /// 접촉하는 순간 난전(Idle)으로 전환됩니다.
    /// </summary>
    void Move_Charge()
    {
        // 대상이 사라졌거나 부대가 무너졌으면 돌격을 중단합니다.
        if (targetArmy == null || targetArmy.units.Count == 0 || army_Data.IsBroken())
        {
            Stop_Charge();
            return;
        }

        // 접촉하면 그 순간 돌격은 끝나고 난전이 시작됩니다.
        if (Is_Blocked_By_Enemy())
        {
            Stop_Charge();
            return;
        }

        Vector3 direction = targetArmy.GetPosition() - formation_Move_Transform.position;
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Stop_Charge();
            return;
        }

        float distanceToEnemy = direction.magnitude;
        direction = direction / distanceToEnemy;

        float speed = army_Data.GetMeleeChargeSpeed();
        if (speed <= 0.0f) speed = army_Data.GetMoveSpeed();

        // 지친 부대의 돌격은 느립니다.
        speed *= army_Data.GetFatigueRate();

        // 부대 기준점도 적 대열을 지나쳐서는 안 됩니다.
        //
        // 기준점이 적진 너머로 가면 진형 슬롯이 적 뒤에 생기고,
        // 유닛들은 '닿을 수 없는 자리'를 향해 계속 적을 밀게 됩니다.
        // 접촉면(근접 사거리)까지만 전진하도록 남은 거리로 클램프합니다.
        float stopDistance = army_Data.GetMeleeRange();
        float remaining = distanceToEnemy - stopDistance;

        if (remaining > 0.0f)
        {
            float step = speed * Time.fixedDeltaTime;
            if (step > remaining) step = remaining;

            Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.P_AgentMove);
            navMeshAgent.Move(direction * step);
            Tick_Profiler.End_Sub();
        }

        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
        formation_Move_Transform.rotation = Quaternion.RotateTowards(
            formation_Move_Transform.rotation,
            lookRotation,
            army_Data.GetRotationSpeed() * Time.fixedDeltaTime);

        locationMoveTo = formation_Move_Transform.position;
    }

    /// <summary>
    /// 부대 차원의 돌격을 종료하고 난전 상태로 되돌립니다.
    ///
    /// 주의: 여기서 유닛의 bcharging을 직접 끄면 안 됩니다.
    /// 이 함수는 _Update_Move에서 '잡 실행 이전'에 호출되므로,
    /// 플래그를 미리 꺼 버리면 정작 이번 틱에 부딪힌 앞열이
    /// 충돌 판정(_Update_Charge)을 놓쳐 돌격이 통째로 무효가 됩니다.
    /// 각 유닛은 스스로 충돌을 기록하거나, 부대가 돌격을 끝냈는데
    /// 접촉하지 못했음을 확인하고 해제합니다.
    /// </summary>
    void Stop_Charge()
    {
        army_Data.e_Army_Move = E_Army_Move.Idle;
    }

    /// <summary>
    /// 적 대열에 가로막혀 부대가 더 전진할 수 없는 상태인지 판정합니다.
    /// </summary>
    bool Is_Blocked_By_Enemy()
    {
        if (units.Count == 0) return false;

        int blocked = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            if (units[i].unit_Data.benemyContact) blocked++;
        }

        return blocked >= units.Count * Constant.army_Blocked_Rate;
    }

    /// <summary>
    /// 부대의 이동을 중지합니다.
    /// </summary>
    void Move_Stop()
    {
        army_Data.e_Army_Move = E_Army_Move.Idle;

        // 기준점을 지면 높이에 맞춰 내려놓습니다.
        //
        // 왜 필요한가:
        // locationMoveTo는 진형 좌표(formation[0])에서 오고, 그 Y는 명령 시점의
        // 기준점 Y를 그대로 물려받습니다. 즉 한 번 어긋나면 계속 어긋난 채
        // 이어집니다. 기준점이 지면 아래로 내려가면 진형 슬롯도 함께 잠기고,
        // 유닛은 '땅속의 자리'를 향해 걷게 됩니다.
        //
        // 유닛 자체는 Unit_Ground_Sync가 지면에 붙여 주지만, 기준점은
        // 그 대상이 아니었습니다. 여기가 그 구멍이었습니다.
        formation_Move_Transform.position = Snap_To_Ground(locationMoveTo);

        formation_Move_Transform.rotation =
            Formation_Util.Rotation_From_Line(GetFormation_Direction());
    }

    /// <summary>
    /// 부대 이동을 취소하고 유닛들의 이동을 중지시킵니다.
    /// </summary>
    void Move_Cancel()
    {
        army_Data.e_Army_Move = E_Army_Move.Idle;
        for (int i = 0; i < units.Count; i++)
        {
            units[i].Move_Cancel();
        }
    }

    /// <summary>
    /// 전열의 기준 '깊이'(적 방향 전후 위치)를 구합니다.
    ///
    /// 돌격 직후에는 앞열이 부대 평균보다 앞에 나가 있습니다.
    /// 평균을 기준으로 삼으면 0열 슬롯이 뒤에 생겨 교전 중인 앞열이 물러나므로,
    /// 접촉 중인 유닛들의 위치를 기준으로 잡습니다.
    /// </summary>
    /// <param name="armyCenter">부대 평균 위치입니다.</param>
    /// <param name="facing">전열이 바라보는 방향(정규화)입니다.</param>
    Vector3 Get_Contact_Line_Center(Vector3 armyCenter, Vector3 facing)
    {
        Vector3 contactSum = Vector3.zero;
        int contactCount = 0;

        float bestProjection = float.MinValue;
        Vector3 frontMost = armyCenter;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;

            Vector3 position = units[i].transform.position;

            if (units[i].unit_Data.benemyContact)
            {
                contactSum += position;
                contactCount++;
            }

            float projection = Vector3.Dot(position, facing);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                frontMost = position;
            }
        }

        // 좌우 위치는 부대 중심을 따르고, 전후 성분만 접촉면에 맞춥니다.
        // 그래야 일부만 접촉했을 때 전열이 그쪽으로 쏠리지 않습니다.
        Vector3 reference = contactCount > 0 ? (contactSum / contactCount) : frontMost;

        float depth = Vector3.Dot(reference - armyCenter, facing);
        return armyCenter + facing * depth;
    }

    /// <summary>
    /// 부대 기준점을 '명령으로 설정된 전열 방향'에 맞춥니다.
    ///
    /// 적을 추적해 회전하지 않는 이유:
    /// 적이 도망가면 그 방향을 쫓아 전열 각도가 계속 틀어집니다.
    /// 전열의 각도는 플레이어가 잡아 둔 것이어야 하며,
    /// 개별 유닛이 자기 표적을 바라보는 것과는 별개입니다.
    /// </summary>
    void Rotation_To_Formation()
    {
        Vector3 lineDirection = GetFormation_Direction();
        if (lineDirection.sqrMagnitude < 0.0001f) return;

        // Move_Stop과 동일한 규약입니다. (정면 = 전열축을 -90도 회전)
        Quaternion lookRotation = Formation_Util.Rotation_From_Line(lineDirection);

        formation_Move_Transform.rotation = Quaternion.RotateTowards(
            formation_Move_Transform.rotation,
            lookRotation,
            army_Data.GetRotationSpeed() * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 교전 중 대열 재정비입니다. 태세에 따라 방식이 달라집니다.
    ///
    /// 공통 원칙: 재정비는 적 위치를 보고 각도를 새로 정하지 않습니다.
    /// 언제나 명령으로 설정된 전열 방향(formation_Data.direction)을 기준으로 합니다.
    /// </summary>
    void Move_Reformation_Combat()
    {
        army_Data.breformation = false;

        if (units.Count == 0) return;

        if (army_Data.e_Army_Stance == E_Army_Stance.Loose)
        {
            Move_Reformation_Loose();
        }
        else
        {
            Move_Reformation_Line();
        }
    }

    /// <summary>
    /// 방어 태세 재정비입니다.
    /// 명령으로 잡아 둔 전열의 방향과 폭을 그대로 유지한 채 대열을 다시 정돈합니다.
    /// </summary>
    void Move_Reformation_Line()
    {
        Vector3 lineDirection = GetFormation_Direction();
        if (lineDirection.sqrMagnitude < 0.0001f) return;

        lineDirection = lineDirection.normalized;

        // 전열이 바라보는 방향은 전열축에서 유도합니다. (적 위치와 무관)
        Vector3 facing = Formation_Util.Facing_From_Line(lineDirection);

        Vector3 armyCenter = GetPosition();
        Vector3 center = Get_Contact_Line_Center(armyCenter, facing);

        // 진형 좌표 규약이 '중심'이므로 왼쪽 끝으로 밀어 줄 필요가 없습니다.
        // (예전에는 여기서 center - lineDirection * 절반폭을 넘겼습니다)
        Formation_Data reformed = Set_Formation(GetFormation_Num(), lineDirection, center);
        if (reformed.formation.Count == 0) return;

        Set_Formation_Data(reformed);

        int[] match = Match_Units_To_Slots(reformed.formation);
        if (match == null) return;

        for (int i = 0; i < units.Count && i < match.Length; i++)
        {
            if (units[i] == null) continue;
            units[i].Move_Reformation(reformed.formation[match[i]]);
        }
    }

    /// <summary>
    /// 공격 태세 재정비입니다.
    /// 전열을 다시 계산하지 않고, 기존 진형의 '앞쪽 자리'만 채웁니다.
    /// 사상자가 생겨 빈 앞자리가 나면 뒷열이 올라오는 정도의 정비입니다.
    /// </summary>
    void Move_Reformation_Loose()
    {
        List<Vector3> slots = formation_Data.formation;
        if (slots == null || slots.Count == 0) return;

        // 진형 좌표는 앞열부터 순서대로 채워져 있으므로,
        // 앞에서부터 생존 인원 수만큼만 사용하면 자연히 앞으로 뭉칩니다.
        int count = Mathf.Min(units.Count, slots.Count);
        if (count == 0) return;

        List<Vector3> frontSlots = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            frontSlots.Add(slots[i]);
        }

        int[] match = Formation_Matcher.Match(Get_Unit_Positions(), frontSlots);
        if (match == null) return;

        for (int i = 0; i < units.Count && i < match.Length; i++)
        {
            if (units[i] == null) continue;
            units[i].Move_Reformation(frontSlots[match[i]]);
        }
    }

    /// <summary>생존 유닛들의 현재 위치를 모읍니다.</summary>
    List<Vector3> Get_Unit_Positions()
    {
        List<Vector3> positions = new List<Vector3>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            positions.Add(units[i] != null ? units[i].transform.position : Vector3.zero);
        }
        return positions;
    }

    /// <summary>
    /// 재편성 이동을 시작합니다.
    /// </summary>
    void Move_Reformation()
    {
        army_Data.breformation = false;
        if (army_Data.e_Army_Formation == E_Army_Formation.Formation)
        {
            return;
        }

        int[] _matchX = Match_Units_To_Slots(formation_Data.formation);
        if (_matchX == null) return;

        for (int i = 0; i < _matchX.Length; i++)
        {
            units[i].Move_Reformation(formation_Data.formation[_matchX[i]]);
        }
    }

    /// <summary>
    /// 진형 슬롯들의 현재 월드 좌표를 리스트로 모읍니다.
    ///
    /// 예전에는 formation_Moves(Transform)를 하나씩 읽었습니다.
    /// 이제 배열이 슬롯의 주인이므로 월드 캐시에서 그대로 꺼냅니다.
    /// </summary>
    private List<Vector3> Get_Formation_Move_Positions()
    {
        // 슬롯 배열은 생성 시점 인원만큼이고 전사해도 줄지 않습니다.
        // 매칭에는 현재 인원까지만 넘깁니다.
        int count = Mathf.Min(SlotCount, Mathf.Max(units.Count, army_Data.unit_Num));

        List<Vector3> positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            positions.Add(Get_Slot_World(i));
        }
        return positions;
    }

    /// <summary>
    /// 생존 유닛들을 진형 슬롯에 배정합니다.
    /// 인원이 많으면 분대 단위 계층 매칭으로 자동 전환되어 프레임 스파이크를 막습니다.
    /// </summary>
    /// <param name="slots">배정할 슬롯 위치들입니다.</param>
    /// <returns>result[유닛 인덱스] = 슬롯 인덱스. 실패 시 null입니다.</returns>
    private int[] Match_Units_To_Slots(List<Vector3> slots)
    {
        if (slots == null || units.Count == 0) return null;
        if (slots.Count < units.Count) return null;

        // 스파이크 용의자라 따로 잽니다.
        // 3초마다 부대별로 발동하는 O(n*s^2) 동기 계산입니다.
        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.A_Match);
        Tick_Profiler.Count_Match();

        int[] result = Formation_Matcher.Match(Get_Unit_Positions(), slots);

        Tick_Profiler.End_Sub();

        return result;
    }
}
