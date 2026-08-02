
using System;
using System.Linq;

using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>유닛의 이동을 취소합니다.</summary>
    public void Move_Cancel()
    {
        e_Unit_Move = E_Unit_Move.Idle;
        currentMoveSpeed = 0.0f;
        location = position;
        bstop = true;
    }

    /// <summary>유닛 충돌 진입을 업데이트합니다.</summary>
    public void _Update_Collision_Enter(in Army_Data armyData, in Unit_Data other, in Army_Data otherArmyData)
    {
        if (e_Unit_Move == E_Unit_Move.Move)
            SetResistance(armyData, other, otherArmyData);
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
    public bool IsOnPosition(in Army_Data armyData)
    {
        Vector3 distenceVector;

        distenceVector = location - position;
        distenceVector.y = 0;

        float radius = armyData.GetRadius();
        return distenceVector.sqrMagnitude < radius * radius;
    }

    /// <summary>유닛이 목표 위치에 있는지 확인합니다.</summary>
    public bool IsOnTarget(in Army_Data armyData)
    {
        Vector3 distenceVector;

        distenceVector = targetVector - position;
        distenceVector.y = 0;

        float size = armyData.GetSize();
        return distenceVector.sqrMagnitude < size * size;
    }

    // Private methods
    /// <summary>유닛의 이동과 회전을 업데이트합니다.</summary>
    private void _Update_Move(in Army_Data armyData)
    {
        Rotation(armyData);

        switch (e_Unit_Move)
        {
            case E_Unit_Move.Move:
                Move(armyData);
                Accelerate(armyData);
                break;
            case E_Unit_Move.Idle:
                Idle(armyData);
                break;
            default:
                break;
        }

        // 어느 경로로 이동량이 정해졌든, 마지막에 한 번 상한을 겁니다.
        // Discrete 충돌 감지에서 콜라이더를 통과하는 사고를 원천 차단합니다.
        Clamp_Penetration(armyData);
    }

    /// <summary>
    /// 교전 중 '적을 바라본 채' 발놀림으로 이동할 상황인지 판정합니다.
    /// 목적지가 가까울 때만 성립합니다. 멀리 재배치할 때까지 게걸음으로 가면
    /// 한없이 느려지므로, 그때는 몸을 돌려 정상 행군합니다.
    /// </summary>
    private bool Is_Combat_Footwork()
    {
        // 돌격 중에는 절대 발놀림으로 전환하지 않습니다.
        // 표적을 잡았다고 뒷걸음/옆걸음 속도로 떨어지면
        // 충돌 직전에 감속해 돌격 자체가 무의미해집니다.
        if (bcharging) return false;

        if (!btarget) return false;

        Vector3 toDestination = location - position;
        toDestination.y = 0.0f;

        return toDestination.sqrMagnitude
               <= Constant.distance_Combat_Footwork * Constant.distance_Combat_Footwork;
    }

    /// <summary>
    /// 이동 방향과 현재 정면이 이루는 각도에 따른 속도 배율입니다.
    /// 뒷걸음이 가장 느리고, 옆걸음이 그다음입니다.
    /// </summary>
    private float Get_Footwork_Speed_Rate(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < 0.000001f) return 1.0f;

        Vector3 forward = rotation * Vector3.forward;
        float dot = Vector3.Dot(forward, moveDirection.normalized);

        if (dot >= Constant.dot_Forward_Walk) return 1.0f;
        if (dot >= Constant.dot_Side_Walk) return Constant.speed_Rate_Sidestep;

        return Constant.speed_Rate_Backpedal;
    }

    /// <summary>유닛의 회전 상태를 업데이트합니다.</summary>
    private void Rotation(in Army_Data armyData)
    {
        Vector3 direction;
        Quaternion lookRotation;

        // 교전 중 근거리에서는 이동 방향과 무관하게 '자기 표적'을 바라봅니다.
        // 유닛마다 표적이 다르므로, 여러 부대와 동시에 맞붙으면
        // 각 유닛이 자기가 상대하는 방향을 향하게 됩니다.
        //
        // 멀리 재배치할 때까지 이 자세를 유지하면 게걸음으로 한없이 느려지므로,
        // 반드시 Is_Combat_Footwork()로 거리를 함께 봅니다.
        if (Is_Combat_Footwork())
        {
            // 주의: 예전에는 targetVector를 봤는데, 그 값은 진형 이동에서만 갱신되어
            //       전투 중에는 낡은 방향이었습니다. 실제 표적 위치를 써야 합니다.
            direction = unit_Target_Data.position - position;
            direction.y = 0.0f;

            if (direction.sqrMagnitude > 0.000001f)
            {
                lookRotation = Quaternion.LookRotation(direction.normalized);
                rotation = Quaternion.RotateTowards(
                    rotation, lookRotation, armyData.GetRotationSpeed() * Constant.deltaTime);
                return;
            }
        }

        switch (e_Unit_Move)
        {
            case E_Unit_Move.Move:
                if (btargetMoveTo && Vector3.Distance(targetVector, position) < armyData.GetSize())
                {
                    lookRotation = armyData.rotation;

                    if (Quaternion.Angle(rotation, lookRotation) > armyData.GetRotationSpeed() * Constant.deltaTime)
                    {
                        rotation = Quaternion.RotateTowards(rotation, lookRotation, armyData.GetRotationSpeed() * Constant.deltaTime);
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
                direction = this.direction * Vector3.forward;
                direction.y = 0;
                break;

            default:
                direction = new Vector3();
                break;
        }

        if (direction == Vector3.zero) return;

        lookRotation = Quaternion.LookRotation(direction);

        if (Quaternion.Angle(rotation, lookRotation) > armyData.GetRotationSpeed() * Constant.deltaTime)
        {
            rotation = Quaternion.RotateTowards(rotation, lookRotation, armyData.GetRotationSpeed() * Constant.deltaTime);
        }

        else
        {
            rotation = lookRotation;
        }
    }

    /// <summary>유닛의 이동을 처리합니다.</summary>
    private void Move(in Army_Data armyData)
    {
        Vector3 directionVector;
        Vector3 unitForwardVector;

        Vector3 vector;
        float movementSpeed;

        movementVector = new Vector3();

        directionVector = steeringTarget - position;

        unitForwardVector = rotation * Vector3.forward;

        movementSpeed = 1.0f;

        // 돌격 중이라면 여기가 실제로 유닛을 움직이는 경로입니다.
        //
        // 돌격 명령을 받은 유닛은 Move_Start로 E_Unit_Move.Move 상태가 되므로
        // Move_Target()이 아니라 이 함수를 지나갑니다.
        // 이 분기가 없으면 유닛은 steeringTarget을 향해 감속 없이 달려
        // 적 대열을 그대로 통과합니다. (실제로 그 버그가 있었습니다)
        if (bcharging && btarget)
        {
            Move_Charge_Step(armyData);
            return;
        }

        if (btargetMoveTo)
        {
            vector = targetVector - position;
            vector.y = 0;
        }
        else
        {
            vector = location - position;
            vector.y = 0;

            if (vector.sqrMagnitude < Constant.distance_Stop * Constant.distance_Stop)
                Move_Stop();
        }

        if (Is_Combat_Footwork())
        {
            // 교전 중 근거리 재배치: 몸을 돌리지 않고 목적지 쪽으로 곧장 발을 옮깁니다.
            // 정면 성분과 섞으면 표적을 향한 자세가 흐트러지므로 여기서는 섞지 않습니다.
            Vector3 toDestination = location - position;
            toDestination.y = 0.0f;

            if (toDestination.sqrMagnitude < 0.000001f)
            {
                movementVector = new Vector3();
                return;
            }

            movementVector = toDestination.normalized;

            // 뒷걸음/옆걸음은 정면 보행보다 느립니다.
            movementSpeed = movementSpeed
                            * GetCurrentMoveSpeed()
                            * Get_Footwork_Speed_Rate(movementVector)
                            * Constant.deltaTime;

            movementVector = movementVector * movementSpeed;

            Block_Into_Enemy();
            return;
        }

        movementVector = unitForwardVector.normalized + directionVector.normalized;
        movementVector = movementVector * 0.5f;
        movementVector.y = 0;

        movementSpeed = movementSpeed * GetCurrentMoveSpeed();
        movementSpeed = movementSpeed * Constant.deltaTime;

        movementVector = movementVector * movementSpeed;

        // 적 대열에 막혔으면 뚫고 들어가지 못합니다.
        Block_Into_Enemy();
    }

    /// <summary>
    /// 교전 중 목표를 향한 이동을 처리합니다.
    ///
    /// 토탈워식 전열 유지의 핵심 규칙:
    ///   1) 사거리 안이면 '제자리에서' 싸운다. 계속 전진하면 두 대열이 서로를 관통합니다.
    ///   2) 진형 슬롯에서 일정 거리 이상 벗어나면 더 쫓지 않는다. (전열 이탈 방지)
    ///   3) 사거리 밖일 때만 한 걸음 다가간다.
    /// </summary>
    private void Move_Target(in Army_Data armyData)
    {
        movementVector = new Vector3();

        // 방어 태세면 자리를 절대 뜨지 않습니다.
        if (armyData.bdefense) return;

        Vector3 toTarget = unit_Target_Data.position - position;
        toTarget.y = 0.0f;

        float meleeRange = armyData.GetMeleeRange();

        // 돌격 중이면 접촉면까지 최고 속도로 파고듭니다.
        if (bcharging)
        {
            Move_Charge_Step(armyData);
            return;
        }

        // 0. 지금 쏠 수 있는 상태라면 다가가지 않습니다.
        //    궁병이 사거리 안의 적을 향해 걸어 들어가면 근접에 휘말려
        //    원거리 부대의 존재 의의가 사라집니다.
        if (e_Unit_AttackType == E_Unit_AttackType.Range && Can_Shoot(armyData))
        {
            return;
        }

        // 1. 이미 닿는 거리면 전진하지 않고 그 자리에서 교전합니다.
        if (toTarget.sqrMagnitude <= meleeRange * meleeRange) return;

        // 2. 자기 진형 슬롯에서 너무 멀어졌으면 추격을 포기합니다.
        Vector3 fromSlot = position - location;
        fromSlot.y = 0.0f;

        if (fromSlot.sqrMagnitude > Constant.distance_Engage_Leash * Constant.distance_Engage_Leash)
            return;

        if (toTarget.sqrMagnitude < 0.0001f) return;

        // 3. 사거리 밖이면 목표 쪽으로 한 걸음만 좁힙니다.
        //    (주의: 여기에 deltaTime을 곱하지 않으면 '초당 속도'가 '틱당 이동량'이 되어
        //     약 60배로 튀어나가며 대열이 서로를 뚫고 지나갑니다. 실제로 그 버그가 있었습니다)
        Vector3 step = toTarget.normalized;

        // 표적을 향해 다가가는 것은 정면 보행이지만,
        // 자세가 아직 안 돌아갔다면 그만큼 느려집니다.
        float speed = GetCurrentMoveSpeed()
                      * Get_Footwork_Speed_Rate(step)
                      * Constant.deltaTime;

        movementVector = step * speed;

        Block_Into_Enemy();
    }

    /// <summary>유닛이 유휴 상태일 때의 행동을 처리합니다.</summary>
    private void Idle(in Army_Data armyData)
    {
        if (btarget)
        {
            Move_Target(armyData);

            // 표적과 맞붙어 제자리 교전 중이라면(이동량 0), 그 틈에 대열을 메웁니다.
            // 사상자가 생겨 벌어진 자리를 싸우면서 좁히는 움직임입니다.
            if (movementVector.sqrMagnitude < 0.000001f)
            {
                Move_Hold_Line(armyData);
            }
        }
        else
        {
            Vector3 directionVector;
            directionVector = location - position;
            directionVector.y = 0;

            float idleRadius = armyData.GetRadius();
            if (directionVector.sqrMagnitude > idleRadius * idleRadius)
            {
                Vector3 step = directionVector.normalized;

                float movementSpeed = Constant.speed_Walk;

                // 교전 중이라면 적을 바라본 채 걸음을 옮깁니다.
                if (btarget) movementSpeed *= Get_Footwork_Speed_Rate(step);

                movementSpeed = movementSpeed * Constant.deltaTime;

                movementVector = step * movementSpeed;
            }
            else
            {
                movementVector = directionVector.normalized * directionVector.magnitude * Constant.deltaTime;
            }
        }
    }

    /// <summary>
    /// 돌격 한 틱의 이동을 계산합니다. 표적과의 접촉면에서 정확히 멈춥니다.
    ///
    /// 이 함수가 존재하는 이유:
    /// 물리 충돌(benemyContact)은 물리 스텝에서 세워지므로 시뮬레이션이 읽는
    /// 시점에는 이미 한 틱 늦은 값입니다. 돌격 속도에서는 그 한 틱이
    /// 콜라이더 반지름의 절반을 넘습니다. (6m/s ÷ 60틱 = 0.1m, 반지름 0.3m)
    /// 여기에 넉백 충격량까지 겹치면 두 대열이 서로를 뚫고 들어가 뒤섞였습니다.
    ///
    /// 그래서 물리를 기다리지 않고 '남은 거리'를 직접 계산해 클램프합니다.
    /// 돌격 경로가 두 곳(Move / Move_Target)이므로 반드시 공용으로 써야 합니다.
    /// </summary>
    private void Move_Charge_Step(in Army_Data armyData)
    {
        Vector3 toTarget = unit_Target_Data.position - position;
        toTarget.y = 0.0f;

        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget < 0.0001f)
        {
            movementVector = new Vector3();
            return;
        }

        float chargeSpeed = armyData.GetMeleeChargeSpeed();
        if (chargeSpeed <= 0.0f) chargeSpeed = armyData.GetMoveSpeed();
        chargeSpeed *= armyData.GetFatigueRate();

        Vector3 direction = toTarget / distanceToTarget;

        // 콜라이더 두 개가 맞닿는 지점까지가 실제로 갈 수 있는 거리입니다.
        // 근접 사거리가 더 짧으면 그쪽이 기준입니다. (창병은 더 멀리서 멈춤)
        float contactDistance = Get_Contact_Distance(armyData);

        float meleeRange = armyData.GetMeleeRange();
        if (meleeRange > contactDistance) contactDistance = meleeRange;

        float remaining = distanceToTarget - contactDistance;

        // 이미 접촉면에 도달했으면 더 전진하지 않습니다.
        // (bcharging 해제는 _Update_Charge가 담당하므로 여기서는 건드리지 않습니다)
        if (remaining <= 0.0f)
        {
            movementVector = new Vector3();
            return;
        }

        float step = chargeSpeed * Constant.deltaTime;

        // 남은 거리보다 더 갈 수 없습니다. 이 클램프가 관통을 막는 핵심입니다.
        if (step > remaining) step = remaining;

        movementVector = direction * step;
    }

    /// <summary>
    /// 두 유닛의 콜라이더가 맞닿는 중심 간 거리입니다.
    ///
    /// 같은 부대끼리는 반지름이 같으므로 (반지름 x 2)이며,
    /// 여기에 약간의 여유(skin)를 더해 물리 엔진이 밀어내기를 시작하기 전에
    /// 시뮬레이션이 먼저 멈추도록 합니다.
    ///
    /// 상대 부대의 반지름을 쓰지 않는 이유:
    /// Job 안에서는 표적의 부대 스탯에 접근할 수 없습니다. 자기 반지름의 2배로
    /// 근사해도 접촉면 형성이라는 목적에는 충분하며, 오차는 skin이 흡수합니다.
    /// </summary>
    private float Get_Contact_Distance(in Army_Data armyData)
    {
        float radius = armyData.GetRadius();
        if (radius <= 0.0f) radius = 0.3f;

        return radius * 2.0f + Constant.charge_Contact_Skin;
    }

    /// <summary>
    /// 한 틱의 이동량이 상대를 통과할 만큼 크지 않도록 잘라냅니다.
    ///
    /// Unity의 Discrete 충돌 감지는 스텝 사이를 건너뛴 이동을 검사하지 않으므로,
    /// 고속 이동에 넉백 충격량까지 겹치면 콜라이더를 그대로 지나칩니다.
    /// 이동량 자체에 상한을 두는 것이 가장 확실한 방어입니다.
    /// </summary>
    private void Clamp_Penetration(in Army_Data armyData)
    {
        float radius = armyData.GetRadius();
        if (radius <= 0.0f) radius = 0.3f;

        float maxStep = radius * Constant.charge_Max_Penetration_Rate;
        if (maxStep <= 0.0f) return;

        float stepSqr = movementVector.sqrMagnitude;
        if (stepSqr <= maxStep * maxStep) return;

        movementVector = movementVector.normalized * maxStep;
    }

    /// <summary>
    /// 적과 접촉 중이면 '적 쪽으로 파고드는' 이동 성분만 제거합니다.
    /// 옆으로 비켜서거나 뒤로 물러나는 것은 그대로 허용해야
    /// 유닛이 벽에 낀 것처럼 굳지 않습니다.
    /// </summary>
    private void Block_Into_Enemy()
    {
        // 돌격 중에는 진입 성분을 제거하지 않습니다. 부딪히는 것이 목적입니다.
        //
        // 다만 이제 Move_Target의 돌격 분기가 접촉면까지의 남은 거리로
        // 이동량을 클램프하므로, 여기서 막지 않아도 관통하지 않습니다.
        if (bcharging) return;

        if (!benemyContact) return;
        if (enemyContactNormal.sqrMagnitude < 0.0001f) return;

        float into = Vector3.Dot(movementVector, enemyContactNormal);
        if (into > 0.0f)
        {
            movementVector -= enemyContactNormal * into;
        }
    }

    /// <summary>
    /// 교전 중 자기 진형 자리로 조금씩 붙습니다. (전열 재정비)
    ///
    /// 토탈워에서 전열은 사상자가 나면 옆 병사가 빈자리를 메우며 유지됩니다.
    /// 여기서는 적을 바라본 채(발놀림) 천천히 이동하므로 등을 보이지 않습니다.
    /// </summary>
    private void Move_Hold_Line(in Army_Data armyData)
    {
        Vector3 toSlot = location - position;
        toSlot.y = 0.0f;

        // 이미 제자리에 가까우면 움직이지 않습니다. (미세한 떨림 방지)
        float radius = armyData.GetRadius();
        if (radius <= 0.0f) radius = 0.3f;

        if (toSlot.sqrMagnitude < radius * radius) return;

        Vector3 step = toSlot.normalized;

        // 적과 맞닿아 싸우는 중이라면 '뒤로 빠지는' 정비 이동은 하지 않습니다.
        //
        // 대열 재정비는 빈자리를 메우라는 뜻이지 교전을 버리라는 뜻이 아닙니다.
        // 이 가드가 없으면 돌격으로 막 접촉한 앞열이
        // 재정비 슬롯을 따라 물러나 전열이 무너집니다.
        // 옆으로 자리를 좁히는 이동은 그대로 허용합니다.
        if (benemyContact && btarget)
        {
            Vector3 toTarget = unit_Target_Data.position - position;
            toTarget.y = 0.0f;

            if (toTarget.sqrMagnitude > 0.000001f
                && Vector3.Dot(step, toTarget.normalized) < 0.0f)
            {
                return;
            }
        }

        float speed = armyData.GetMoveSpeed()
                      * Constant.reform_Speed_Rate
                      * Get_Footwork_Speed_Rate(step)
                      * armyData.GetFatigueRate()
                      * Constant.deltaTime;

        movementVector = step * speed;

        // 대열을 메우려다 적을 밀고 들어가지는 않습니다.
        Block_Into_Enemy();
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
    private void Accelerate(in Army_Data armyData)
    {
        // 돌격 중에는 접촉 직전까지 계속 가속해 운동량을 확보합니다.
        // (충격량이 실제 속도에 비례하므로, 여기서 감속하면 돌격이 헛돕니다)
        if (bcharging)
        {
            float chargeSpeed = armyData.GetMeleeChargeSpeed();
            if (chargeSpeed <= 0.0f) chargeSpeed = armyData.GetMoveSpeed();
            chargeSpeed *= armyData.GetFatigueRate();

            currentMoveSpeed += armyData.GetAcceleration() * Constant.deltaTime;
            if (currentMoveSpeed > chargeSpeed) currentMoveSpeed = chargeSpeed;
            return;
        }

        // 적에게 막힌 동안에는 가속하지 않습니다.
        // (예전에는 충돌 시 SetResistance가 속도를 한 번 깎아도
        //  여기서 매 틱 즉시 복구해 버려서 결국 적 대열을 뚫고 지나갔습니다)
        if (benemyContact)
        {
            currentMoveSpeed *= Constant.blocked_Accelerate_Rate;
            if (currentMoveSpeed < 0.0f) currentMoveSpeed = 0.0f;
            return;
        }

        float max_moveSpeed;

        // 지치면 최고 속도 자체가 낮아집니다.
        // 밀집 태세(방패벽/창벽)는 발을 맞춰야 하므로 뛸 수 없습니다.
        max_moveSpeed = armyData.GetMoveSpeed()
                        * armyData.GetFatigueRate()
                        * armyData.GetStanceSpeedRate();

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
                currentMoveSpeed = currentMoveSpeed + armyData.GetAcceleration() * Constant.deltaTime;
            }

            max_moveSpeed = max_moveSpeed * Mathf.Lerp(1.0f, Constant.move_Speed_Target, dirationVecotr.magnitude);
        }
        else
        {
            currentMoveSpeed = currentMoveSpeed + armyData.GetAcceleration() * Constant.deltaTime;
        }

        if (currentMoveSpeed >= max_moveSpeed)
            currentMoveSpeed = max_moveSpeed;

        if (currentMoveSpeed <= 0)
            currentMoveSpeed = 0;
    }

    /// <summary>충돌 시 유닛의 저항력을 설정합니다.</summary>
    private void SetResistance(in Army_Data armyData, in Unit_Data unit_Data, in Army_Data otherArmyData)
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
            // 돌격 중에는 멈춰 선 아군에 막혀 서지 않습니다.
            // (앞서 교전에 들어간 아군이나 다른 부대와 스치면 돌격이 죽습니다)
            if (bcharging) return;

            if (unit_Data.e_Unit_Move == E_Unit_Move.Idle)
            {
                Move_Cancel();
                return;
            }
        }
        else if (bcharging)
        {
            // 돌격 충돌 순간입니다. 여기서 속도를 깎으면 충격량이 사라집니다.
            return;
        }
        else
        {
            // 적과 부딪혔습니다. 여기서 밀고 들어가면 두 대열이 뒤섞이므로
            // 전진을 사실상 차단해 '접촉면'을 형성합니다.
            float myMass = armyData.GetMass();
            float otherMass = otherArmyData.GetMass();

            // 질량이 크게 앞서면 조금은 밀어붙일 수 있습니다. (기병 돌파의 토대)
            float massAdvantage = myMass / (myMass + otherMass + 0.0001f); // 0.5면 동급
            float speedMultiplier = (massAdvantage - 0.5f) * 2.0f;         // 동급이면 0

            if (speedMultiplier < Constant.engage_Block_Rate)
                speedMultiplier = Constant.engage_Block_Rate;

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
            // 교전 중입니다. Move_Start를 부르면 표적을 놓고 행군을 시작해
            // 전열이 무너지므로, 목표 자리만 갱신하고 이동은
            // Move_Hold_Line이 발놀림으로 천천히 처리합니다.
        }
        else if (unit_Data.IsOnPosition(army.army_Data))
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

    /// <summary>
    /// 산개 태세의 후퇴 이동입니다.
    ///
    /// 패주(Move_Escape)와 다른 점: 표적을 버리지 않습니다.
    /// 물러나면서도 계속 쏘아야 하므로 자세는 적을 향한 채 유지합니다.
    /// </summary>
    /// <param name="direction">물러날 방향입니다.</param>
    /// <param name="speed">후퇴 속도입니다.</param>
    public void Move_Skirmish(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 step = direction.normalized * speed * Constant.deltaTime;

        navMeshAgent.Move(step);

        unit_Data.position = transform.position;

        // 진형 목표도 함께 끌고 가야 다음 재정비에서 되돌아가지 않습니다.
        unit_Data.location = transform.position;
    }

    /// <summary>
    /// 부대가 붕괴했을 때의 패주 이동입니다.
    /// 진형과 목표를 버리고 지정된 방향으로 달아납니다.
    /// </summary>
    /// <param name="direction">달아날 방향입니다.</param>
    public void Move_Escape(Vector3 direction)
    {
        unit_Data.Lose_Target();

        float speed = army.army_Data.GetMoveSpeed() * Constant.rout_Speed_Rate;
        Vector3 escapeVector = direction.normalized * speed * Constant.deltaTime;

        navMeshAgent.Move(escapeVector);

        unit_Data.position = transform.position;

        // 달아나는 쪽을 바라봅니다.
        if (direction.sqrMagnitude > 0.0001f)
        {
            unit_Data.rotation = Quaternion.RotateTowards(
                unit_Data.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                army.army_Data.GetRotationSpeed() * Constant.deltaTime);
        }
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
        Army otherArmy = unit_collision.GetArmy();
        if (otherArmy == null || army == null) return;

        unit_Data._Update_Collision_Enter(
            army.army_Data,
            unit_collision.GetUnit_Data(),
            otherArmy.army_Data);
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

        // 교전 중인 유닛은 옆으로 비켜서지 않습니다.
        // 비켜서면 대열에 구멍이 생기고 그 틈으로 적이 파고들어 뒤섞입니다.
        if (unit_Data.btarget || unit_collision.unit_Data.btarget)
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

        pushVector = pushVector.normalized * Constant.speed_Walk * Constant.engage_Push_Rate * Constant.deltaTime;
        navMeshAgent.Move(pushVector);
    }
}
