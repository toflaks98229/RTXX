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

        navMeshAgent.Move(movementVector);

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
    /// 부대를 목표 방향으로 회전시킵니다.
    /// </summary>
    void Rotation()
    {
        Vector3 direction = (navMeshAgent.steeringTarget - formation_Move_Transform.position);
        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
        formation_Move_Transform.rotation = Quaternion.RotateTowards(formation_Move_Transform.rotation, lookRotation, army_Data.GetRotationSpeed() * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 부대의 현재 평균 위치를 계산하여 반환합니다.
    /// </summary>
    /// <returns>부대의 평균 위치입니다.</returns>
    public Vector3 GetPosition()
    {
        Vector3 position = new Vector3();
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (unit.IsDead()) continue;
            position += unit.transform.position;
        }

        if (army_Data.unit_Num == 0) return Vector3.zero;
        return position / army_Data.unit_Num;
    }

    /// <summary>
    /// 부대 이동의 기준이 되는 위치를 설정합니다.
    /// </summary>
    void Set_Army_Move_Position()
    {
        Vector3 position = GetPosition();
        Vector3 direction = locationMoveTo - GetPosition();

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

        formation_Move_Transform.position = position;
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
        bformation_Move = false;
        army_Data.e_Army_Move = E_Army_Move.Move;
        Formation_Data formation_Data = Set_Formation(length, direction, position);
        locationMoveTo = formation_Data.formation[0];
        Set_Army_Move_Position();
        Set_Formation_Data(formation_Data);
        Set_Formation_Move();

        int[] _matchX = Match_Units_To_Slots(Get_Formation_Move_Positions());
        if (_matchX == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            units[i].Move_Start(formation_Moves[_matchX[i]]);
            if (_matchX[i] == 0)
            {
                unit_Bearing_Flag = units[i];
            }
        }

        navMeshAgent.SetDestination(locationMoveTo);

        // uI_Units는 생성 시점 인원, formation은 현재 생존 인원 기준이라 길이가 다릅니다.
        int uiCount = Mathf.Min(uI_Units.Count, formation_Data.formation.Count);
        for (int i = 0; i < uiCount; i++)
        {
            uI_Units[i]._Update(formation_Data.formation[i], direction);
        }
    }

    /// <summary>
    /// 진형 유닛 수를 기반으로 이동을 시작합니다.
    /// </summary>
    /// <param name="num">진형 유닛 수입니다.</param>
    /// <param name="direction">이동 방향입니다.</param>
    /// <param name="position">이동 위치입니다.</param>
    public void Move_Start(int num, Vector3 direction, Vector3 position)
    {
        bformation_Move = false;
        army_Data.e_Army_Move = E_Army_Move.Move;
        Formation_Data formation_Data = Set_Formation(num, direction, position);
        locationMoveTo = formation_Data.formation[0];
        Set_Army_Move_Position();
        Set_Formation_Data(formation_Data);
        Set_Formation_Move();

        int[] _matchX = Match_Units_To_Slots(Get_Formation_Move_Positions());
        if (_matchX == null) return;

        for (int i = 0; i < units.Count; i++)
        {
            units[i].Move_Start(formation_Moves[_matchX[i]]);
            if (_matchX[i] == 0)
            {
                unit_Bearing_Flag = units[i];
            }
        }

        navMeshAgent.SetDestination(locationMoveTo);

        // uI_Units는 생성 시점 인원, formation은 현재 생존 인원 기준이라 길이가 다릅니다.
        int uiCount = Mathf.Min(uI_Units.Count, formation_Data.formation.Count);
        for (int i = 0; i < uiCount; i++)
        {
            uI_Units[i]._Update(formation_Data.formation[i], direction);
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
                navMeshAgent.Move(movementVector);
            }

            Rotation();
        }
        else
        {
            movementVector = (navMeshAgent.steeringTarget - formation_Move_Transform.position).normalized;
            movementVector = movementVector * army_Data.GetMoveSpeed() * army_Data.GetFatigueRate();
            movementVector = movementVector * Time.fixedDeltaTime;

            if (navMeshAgent.remainingDistance > Constant.distance_Stop)
            {
                navMeshAgent.Move(movementVector);
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

            navMeshAgent.Move(direction * step);
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
        formation_Move_Transform.position = locationMoveTo;
        Quaternion direction = Quaternion.LookRotation(GetFormation_Direction(), Vector3.up);
        direction = direction * Quaternion.Euler(new Vector3(0, -90.0f, 0));
        formation_Move_Transform.rotation = direction;
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
        Quaternion lookRotation =
            Quaternion.LookRotation(lineDirection.normalized, Vector3.up)
            * Quaternion.Euler(new Vector3(0.0f, -90.0f, 0.0f));

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
        Vector3 facing = Quaternion.AngleAxis(-90.0f, Vector3.up) * lineDirection;

        Vector3 armyCenter = GetPosition();
        Vector3 center = Get_Contact_Line_Center(armyCenter, facing);

        Vector3 start = center - lineDirection * (GetFormation_Length() * 0.5f);

        Formation_Data reformed = Set_Formation(GetFormation_Num(), lineDirection, start);
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
    /// formation_Moves 트랜스폼들의 현재 위치를 리스트로 모읍니다.
    /// </summary>
    private List<Vector3> Get_Formation_Move_Positions()
    {
        // 전사로 formation_Moves가 실제 인원보다 길 수 있습니다.
        // 매칭에는 갱신된 슬롯(생존 인원 수)만 넘깁니다.
        int count = Mathf.Min(formation_Moves.Count, Mathf.Max(units.Count, army_Data.unit_Num));

        List<Vector3> positions = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            positions.Add(formation_Moves[i].position);
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

        return Formation_Matcher.Match(Get_Unit_Positions(), slots);
    }
}
