using Unity.VisualScripting;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

// 유닛 데이터 구조체
partial struct Unit_Data
{
    // Public methods
    /// <summary>유닛 이동 시작을 처리합니다.</summary>
    public void Move_Start()
    {
        Lose_Speed();
        e_Unit_Move = E_Unit_Move.Move;
    }

    /// <summary>특정 목표 트랜스폼으로 유닛 이동을 시작합니다.</summary>
    public void Move_Start(Transform transform)
    {
        location = transform.position;
        btargetMoveTo = true;
        Move_Start();
    }

    /// <summary>지정된 위치로 유닛 이동을 시작합니다.</summary>
    public void Move_Start(Vector3 vector3, Quaternion quaternion)
    {
        btargetMoveTo = false;
        location = vector3;
        direction = quaternion;
        Move_Start();
    }

    /// <summary>특정 위치로 이동을 시작합니다.</summary>
    public void Move_Start_Location()
    {
        btargetMoveTo = false;
        Move_Start();
    }

    /// <summary>유닛의 목표 위치를 업데이트합니다.</summary>
    public void Move_UpdateTarget(Vector3 steeringTarget)
    {
        this.steeringTarget = steeringTarget;
    }

    /// <summary>유닛의 조종 목표 및 목적지 위치를 업데이트합니다.</summary>
    public void Move_UpdateTarget(Vector3 steeringTarget, Vector3 location)
    {
        this.steeringTarget = steeringTarget;
        this.location = location;
    }

    /// <summary>유닛의 이동을 취소합니다.</summary>
    public void Move_Cancel()
    {
        e_Unit_Move = E_Unit_Move.Idle;
        currentMoveSpeed = 0.0f;
        location = position;
        bstop = true;
    }

    /// <summary>유닛 충돌 진입을 업데이트합니다.</summary>
    public void _Update_Collision_Enter(Unit_Data unit_Data)
    {
        if (e_Unit_Move == E_Unit_Move.Move)
            SetResistance(unit_Data);
    }

    /// <summary>유닛의 현재 이동 벡터를 반환합니다.</summary>
    public Vector3 GetMovementVector()
    {
        Vector3 vector3 = movementVector;

        return vector3;
    }

    /// <summary>유닛의 현재 이동 속도를 반환합니다.</summary>
    public float GetCurrentMoveSpeed()
    {
        float currentMoveSpeed = this.currentMoveSpeed;

        return currentMoveSpeed;
    }

    /// <summary>유닛이 지정된 위치에 있는지 확인합니다.</summary>
    public bool IsOnPosition()
    {
        Vector3 distenceVector;

        distenceVector = location - position;
        distenceVector.y = 0;

        if (distenceVector.magnitude < army_Data.GetRadius())
            return true;
        else
            return false;
    }

    /// <summary>유닛이 목표 위치에 있는지 확인합니다.</summary>
    public bool IsOnTarget()
    {
        Vector3 distenceVector;

        distenceVector = targetVector - position;
        distenceVector.y = 0;

        if (distenceVector.magnitude < army_Data.GetSize())
            return true;
        else
            return false;
    }

    // Private methods
    /// <summary>유닛의 이동과 회전을 업데이트합니다.</summary>
    private void _Update_Move()
    {
        Rotation();

        switch (e_Unit_Move)
        {
            case E_Unit_Move.Move:
                Move();
                Accelerate();
                break;
            case E_Unit_Move.Idle:
                Idle();
                break;
            default:
                break;
        }
    }

    /// <summary>유닛의 회전 상태를 업데이트합니다.</summary>
    private void Rotation()
    {
        Vector3 direction;
        Quaternion lookRotation;

        switch (e_Unit_Move)
        {
            case E_Unit_Move.Move:
                if (btargetMoveTo && Vector3.Distance(targetVector, position) < army_Data.GetSize())
                {
                    lookRotation = army_Data.rotation;

                    if (Quaternion.Angle(rotation, lookRotation) > army_Data.GetRotationSpeed() * Constant.deltaTime)
                    {
                        rotation = Quaternion.RotateTowards(rotation, lookRotation, army_Data.GetRotationSpeed() * Constant.deltaTime);
                    }

                    return;
                }
                else
                {
                    direction = (steeringTarget - position).normalized;
                    direction.y = 0;
                }
                break;

            case E_Unit_Move.Idle:
                if (btarget)
                {
                    direction = (targetVector - position).normalized;
                    direction.y = 0;
                }
                else
                {
                    direction = this.direction * Vector3.forward;
                    direction.y = 0;
                }
                break;

            default:
                direction = new Vector3();
                break;
        }

        if (direction == Vector3.zero) return;

        lookRotation = Quaternion.LookRotation(direction);

        if (Quaternion.Angle(rotation, lookRotation) > army_Data.GetRotationSpeed() * Constant.deltaTime)
        {
            rotation = Quaternion.RotateTowards(rotation, lookRotation, army_Data.GetRotationSpeed() * Constant.deltaTime);
        }

        else
        {
            rotation = lookRotation;
        }
    }

    /// <summary>유닛 이동 중 회전을 처리합니다.</summary>
    private void Rotation_Move()
    {
        Vector3 direction;
        Quaternion lookRotation;

        direction = (steeringTarget - position).normalized;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        lookRotation = Quaternion.LookRotation(direction);

        if (Quaternion.Angle(rotation, lookRotation) > army_Data.GetRotationSpeed() * Constant.deltaTime)
        {
            rotation = Quaternion.RotateTowards(rotation, lookRotation, army_Data.GetRotationSpeed() * Constant.deltaTime);
        }
        else
        {
            rotation = lookRotation;
        }
    }

    /// <summary>유닛이 유휴 상태일 때의 회전을 처리합니다.</summary>
    private void Rotation_Idle()
    {
        if (Quaternion.Angle(rotation, direction) > army_Data.GetRotationSpeed() * Constant.deltaTime)
        {
            rotation = Quaternion.RotateTowards(rotation, direction, army_Data.GetRotationSpeed() * Constant.deltaTime);
        }

        else
        {
            rotation = direction;
        }
    }

    /// <summary>유닛의 이동을 처리합니다.</summary>
    private void Move()
    {
        Vector3 directionVector;
        Vector3 unitForwardVector;

        Vector3 vector;
        float movementSpeed;

        movementVector = new Vector3();

        directionVector = steeringTarget - position;

        unitForwardVector = rotation * Vector3.forward;

        movementSpeed = 1.0f;

        if (btargetMoveTo)
        {
            vector = targetVector - position;
            vector.y = 0;
        }
        else
        {
            vector = location - position;
            vector.y = 0;

            if (vector.magnitude < Constant.distance_Stop)
                Move_Stop();
        }

        movementVector = unitForwardVector.normalized + directionVector.normalized;
        movementVector = movementVector * 0.5f;
        movementVector.y = 0;


        movementSpeed = movementSpeed * GetCurrentMoveSpeed();
        movementSpeed = movementSpeed * Constant.deltaTime;

        movementVector = movementVector * movementSpeed;
    }

    /// <summary>타겟을 향한 이동을 처리합니다.</summary>
    private void Move_Target()
    {
        Vector3 directionVector;
        Vector3 unitForwardVector;

        Vector3 vector;
        float movementSpeed;

        movementVector = new Vector3();

        directionVector = steeringTarget - position;

        unitForwardVector = rotation * Vector3.forward;

        movementSpeed = 1.0f;

        vector = unit_Target_Data.position - position;
        vector.y = 0;

        if (vector.magnitude > Constant.distance_Move_FowardVector)
            movementSpeed = movementSpeed * Constant.move_Speed_Target;

        if (army_Data.bdefense)
        {
            return;
        }
        else
        {
            if (Vector3.Distance(position, location) > Constant.distance_Attack_Move)
            {
                return;
            }
            else
            {
                // 추가적인 이동 로직
            }
        }

        if (vector.magnitude < Constant.distance_Move_FowardVector)
        {
            movementVector = directionVector.normalized;
        }
        else
        {
            movementVector = unitForwardVector.normalized;
        }

        movementSpeed = movementSpeed * GetCurrentMoveSpeed();

        movementVector = movementVector.normalized * movementSpeed;
    }

    /// <summary>유닛이 유휴 상태일 때의 행동을 처리합니다.</summary>
    private void Idle()
    {
        if (btarget)
        {
            Move_Target();
        }
        else
        {
            Vector3 directionVector;
            directionVector = location - position;
            directionVector.y = 0;

            if (directionVector.magnitude > army_Data.GetRadius())
            {
                float movementSpeed;
                movementSpeed = Constant.speed_Walk;
                movementSpeed = movementSpeed * Constant.deltaTime;

                movementVector = directionVector.normalized * movementSpeed;
            }
            else
            {
                movementVector = directionVector.normalized * directionVector.magnitude * Constant.deltaTime;
            }
        }
    }

    /// <summary>유닛의 이동을 중지합니다.</summary>
    private void Move_Stop()
    {
        e_Unit_Move = E_Unit_Move.Idle;
        currentMoveSpeed = 0.0f;
        bstop = true;
    }

    /// <summary>유닛의 속도를 감소시킵니다.</summary>
    private void Lose_Speed()
    {
        Vector3 dirationVecotr = location - position;
        dirationVecotr.y = 0.0f;

        float angle;
        angle = Quaternion.Angle(rotation, direction);

        if (angle > 30.0f)
        {
            currentMoveSpeed = currentMoveSpeed * (1 - angle / 180.0f);
        }

        if (e_Unit_Move == E_Unit_Move.Idle)
            currentMoveSpeed = 0.0f;
    }

    /// <summary>유닛의 가속도를 업데이트합니다.</summary>
    private void Accelerate()
    {
        float max_moveSpeed;

        max_moveSpeed = army_Data.GetMoveSpeed();

        if (btargetMoveTo)
        {
            Vector3 dirationVecotr = targetVector - position;
            dirationVecotr.y = 0.0f;

            float angle;
            angle = Quaternion.Angle(rotation, Quaternion.LookRotation(dirationVecotr));

            if (angle > 30.0f)
            {

            }
            else
            {
                currentMoveSpeed = currentMoveSpeed + army_Data.GetAcceleration() * Constant.deltaTime;
            }

            max_moveSpeed = max_moveSpeed * Mathf.Lerp(1.0f, Constant.move_Speed_Target, dirationVecotr.magnitude);
        }
        else
        {
            currentMoveSpeed = currentMoveSpeed + army_Data.GetAcceleration() * Constant.deltaTime;
        }

        if (currentMoveSpeed >= max_moveSpeed)
            currentMoveSpeed = max_moveSpeed;

        if (currentMoveSpeed <= 0)
            currentMoveSpeed = 0;
    }

    /// <summary>충돌 시 유닛의 저항력을 설정합니다.</summary>
    private void SetResistance(Unit_Data unit_Data)
    {
        Vector3 dirationVecotr = unit_Data.position - position;
        dirationVecotr.y = 0.0f;

        float angle = Quaternion.Angle(rotation, Quaternion.LookRotation(dirationVecotr));
        if (angle > 15.0f)
        {
            return;
        }

        if (unit_Data.bPlayer == bPlayer)
        {
            if (unit_Data.e_Unit_Move == E_Unit_Move.Idle)
            {
                Move_Cancel();
                return;
            }
        }
        else
        {
            float myMass = army_Data.GetMass();
            float otherMass = unit_Data.army_Data.GetMass();

            float speedMultiplier = 1.0f - (otherMass / (myMass + 0.0001f));

            if (speedMultiplier < 0)
            {
                speedMultiplier = 0;
            }

            currentMoveSpeed *= speedMultiplier;

            if (currentMoveSpeed < 0.01f)
            {
                Move_Cancel();
            }
        }
    }
}

// 유닛 클래스
partial class Unit
{
    // Public methods
    /// <summary>유닛의 이동을 시작합니다.</summary>
    public void Move_Start()
    {
        navMeshAgent.SetDestination(unit_Data.location);
        unit_Data.Move_Start();
    }

    /// <summary>지정된 목표 트랜스폼으로 유닛 이동을 시작합니다.</summary>
    public void Move_Start(Transform targetMoveTo)
    {
        navMeshAgent.SetDestination(targetMoveTo.position);
        this.targetMoveTo = targetMoveTo;
        unit_Data.Move_Start(targetMoveTo);
    }

    /// <summary>지정된 위치로 유닛 이동을 시작합니다.</summary>
    public void Move_Start(Vector3 location)
    {
        navMeshAgent.SetDestination(location);
        Quaternion direction = Quaternion.LookRotation(army.GetFormation_Direction(), Vector3.up);
        direction = direction * Quaternion.Euler(new Vector3(0, -90.0f, 0));

        unit_Data.Move_Start(location, direction);
    }

    /// <summary>재배치 이동을 처리합니다.</summary>
    public void Move_Reformation(Vector3 location)
    {
        unit_Data.location = location;

        if (unit_Data.btarget)
        {

        }
        else if (unit_Data.IsOnPosition())
        {

        }
        else
        {
            Move_Start(location);
        }
    }

    /// <summary>유닛의 이동을 취소합니다.</summary>
    public void Move_Cancel()
    {
        unit_Data.Move_Cancel();
    }

    // Private methods
    /// <summary>유닛의 이동과 회전을 업데이트합니다.</summary>
    private void _Update_Move()
    {
        Move();
        Rotation();
    }

    /// <summary>유닛의 회전을 업데이트합니다.</summary>
    private void Rotation()
    {
        transform.rotation = unit_Data.rotation;
    }

    /// <summary>유닛의 이동을 처리합니다.</summary>
    private void Move()
    {
        navMeshAgent.Move(unit_Data.GetMovementVector());

        unit_Data.steeringTarget = navMeshAgent.steeringTarget;
        unit_Data.position = transform.position;

        switch (unit_Data.e_Unit_Move)
        {
            case E_Unit_Move.Move:
                if (unit_Data.btargetMoveTo)
                {
                    navMeshAgent.SetDestination(targetMoveTo.position);
                    unit_Data.targetVector = targetMoveTo.position;
                }
                break;
            case E_Unit_Move.Idle:
                if (unit_Data.btarget)
                {
                    navMeshAgent.SetDestination(unit_Data.unit_Target_Data.position);
                }
                break;
        }

        if (unit_Data.bstop)
            Move_Stop();
    }

    /// <summary>유닛의 이동을 중지합니다.</summary>
    private void Move_Stop()
    {
        unit_Data.bstop = false;
    }

    /// <summary>다른 유닛과의 충돌 진입을 처리합니다.</summary>
    private void Move_Collision_Enter(Unit unit_collision)
    {
        unit_Data._Update_Collision_Enter(unit_collision.GetUnit_Data());
    }

    /// <summary>다른 유닛과의 충돌이 지속되는 동안의 움직임을 처리합니다.</summary>
    private void Move_Collision_Stay(Unit unit_collision)
    {
        if (this.unit_Data.e_Unit_Move == E_Unit_Move.Idle)
        {
            return;
        }

        if (unit_Data.bPlayer != unit_collision.unit_Data.bPlayer)
        {
            return;
        }

        Vector3 pushVector = unit_Data.GetMovementVector();
        Vector3 directionVector = transform.position - unit_collision.transform.position;
        Vector3 cross = Vector3.Cross(pushVector, directionVector);
        float Dot = Vector3.Dot(cross, Vector3.up);

        if (Dot > 0.0f)
        {
            pushVector = new Vector3(pushVector.z, pushVector.y, -pushVector.x);
        }
        else if (Dot < 0.0f)
        {
            pushVector = new Vector3(-pushVector.z, pushVector.y, pushVector.x);
        }

        pushVector = pushVector.normalized * Constant.speed_Walk * Constant.deltaTime;
        navMeshAgent.Move(pushVector);
    }
}
