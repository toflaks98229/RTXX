using System;
using System.Linq;
using System.Collections;
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
                if (army_Data.breformation && targetArmy == null)
                {
                    Move_Reformation();
                }
                break;
            case E_Army_Move.Move:
                Move();
                break;
            case E_Army_Move.MoveToTarget:
                break;
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
            position = position + direction.normalized * army_Data.GetInterval() * army_Data.unit_Num / GetFormation_Num() * 0.5f;
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

        List<Vector3> unit_Positions = new List<Vector3>();
        foreach (Unit unit in units)
        {
            unit_Positions.Add(unit.transform.position);
        }

        Hungarian hungarian = new Hungarian(units, formation_Moves);
        int[] _matchX = hungarian.Run();

        for (int i = 0; i < units.Count; i++)
        {
            units[i].Move_Start(formation_Moves[_matchX[i]]);
            if (_matchX[i] == 0)
            {
                unit_Bearing_Flag = units[i];
            }
        }

        navMeshAgent.SetDestination(locationMoveTo);
        for (int i = 0; i < army_Data.unit_Num; i++)
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

        List<Vector3> unit_Positions = new List<Vector3>();
        foreach (Unit unit in units)
        {
            unit_Positions.Add(unit.transform.position);
        }

        Hungarian hungarian = new Hungarian(units, formation_Moves);
        int[] _matchX = hungarian.Run();

        for (int i = 0; i < units.Count; i++)
        {
            units[i].Move_Start(formation_Moves[_matchX[i]]);
            if (_matchX[i] == 0)
            {
                unit_Bearing_Flag = units[i];
            }
        }

        navMeshAgent.SetDestination(locationMoveTo);
        for (int i = 0; i < army_Data.unit_Num; i++)
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
        Hungarian hungarian = new Hungarian(units, formation_Data.formation);
        int[] _matchX = hungarian.Run();
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
                if (units[i].GetUnit_Data().IsOnTarget())
                {
                    army_InPosition_Num += 1;
                }
            }

            movementVector = movementVector * army_Data.GetMoveSpeed();
            movementVector = movementVector * Mathf.Lerp(0.2f, 1.0f, (float)army_InPosition_Num / army_Data.unit_Num);

            if (army_InPosition_Num < army_Data.unit_Num * 0.5f)
            {
                // 로직 추가 가능
            }

            if (Vector3.Distance(formation_Move_Transform.position, locationMoveTo) < GetFormation_Length() * 0.5f)
            {
                Move_Formation();
            }

            navMeshAgent.Move(movementVector);
            Rotation();
        }
        else
        {
            movementVector = (navMeshAgent.steeringTarget - formation_Move_Transform.position).normalized;
            movementVector = movementVector * army_Data.GetMoveSpeed();
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
    /// 재편성 이동을 시작합니다.
    /// </summary>
    void Move_Reformation()
    {
        army_Data.breformation = false;
        if (army_Data.e_Army_Formation == E_Army_Formation.Formation)
        {
            return;
        }

        List<Vector3> unit_Positions = new List<Vector3>();
        foreach (Unit unit in units)
        {
            unit_Positions.Add(unit.transform.position);
        }

        Hungarian hungarian = new Hungarian(unit_Positions, formation_Data.formation.ToList());
        int[] _matchX = hungarian.Run();

        for (int i = 0; i < _matchX.Length; i++)
        {
            units[i].Move_Reformation(formation_Data.formation[_matchX[i]]);
        }
    }
}
