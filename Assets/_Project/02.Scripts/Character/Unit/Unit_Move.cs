
using System;
using System.Linq;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛의 "이동" 책임을 담당하는 부분 구조체입니다.
///
/// 담는 것: 이동 상태 머신, 회전, 가속, 전투 중 발놀림, 관통 방지.
/// 공통점은 전부 '유닛 하나가 이번 틱에 어디로 얼마나 움직이는가'를
/// 정하는 일이라는 점입니다. 어디로 갈지(진형 슬롯)는 상위에서 넘어옵니다.
///
/// 값 타입인 이유:
/// Unit_Job 안에서 병렬로 갱신되므로 참조 타입을 담을 수 없습니다.
/// 부대 스탯은 메서드마다 in Army_Data로 넘겨받습니다.
/// (in은 복사 없는 읽기 전용 전달입니다)
///
/// 발놀림(footwork) 개념:
/// 교전 중 근거리에서는 몸을 돌리지 않고 적을 마주 본 채 움직입니다.
/// 뒷걸음과 옆걸음은 정면 행군보다 느리며, 그래서 접전에서 물러나는 것이
/// 실제로 대가를 치릅니다. 다만 멀리 재배치할 때까지 이 자세를 유지하면
/// 한없이 느려지므로 거리로 조건을 겁니다. (Is_Combat_Footwork)
///
/// 관통 방지 주의:
/// 어느 경로로 이동량이 정해졌든 마지막에 Clamp_Penetration이 상한을 겁니다.
/// Discrete 충돌 감지에서 한 틱에 콜라이더를 뛰어넘는 사고를 막기 위함입니다.
/// </summary>
partial struct Unit_Data
{
    // Public methods
    /// <summary>
    /// 이동을 시작합니다. 붙어 있던 속도를 버리고 Move 상태로 전환합니다.
    /// 목적지는 호출 전에 이미 정해져 있어야 합니다.
    /// </summary>
    public void Move_Start()
    {
        Lose_Speed();
        e_Unit_Move = E_Unit_Move.Move;
    }

    /// <summary>
    /// 특정 트랜스폼을 추격 목표로 삼아 이동을 시작합니다.
    /// 목표가 움직이는 대상이므로 btargetMoveTo를 세워 매 틱 위치를 다시 읽게 합니다.
    /// </summary>
    /// <param name="transform">추격할 목표 트랜스폼입니다.</param>
    public void Move_Start(Transform transform)
    {
        location = transform.position;
        btargetMoveTo = true;
        Move_Start();
    }

    /// <summary>
    /// 고정된 위치와 방향으로 이동을 시작합니다. 진형 이동에 사용합니다.
    /// </summary>
    /// <param name="vector3">이동할 목적지 좌표입니다.</param>
    /// <param name="quaternion">도착 후 바라볼 방향입니다.</param>
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

    /// <summary>유닛의 현재 이동 벡터를 반환합니다.</summary>
    /// <returns>이번 틱에 적용될 이동 벡터입니다.</returns>
    public Vector3 GetMovementVector()
    {
        Vector3 vector3 = movementVector;

        return vector3;
    }

    /// <summary>유닛의 현재 이동 속도를 반환합니다.</summary>
    /// <returns>현재 이동 속도입니다. 정지 상태면 0입니다.</returns>
    public float GetCurrentMoveSpeed()
    {
        float currentMoveSpeed = this.currentMoveSpeed;

        return currentMoveSpeed;
    }

    /// <summary>
    /// 유닛이 목적지에 도착했는지 확인합니다. 판정 반경은 부대의 유닛 반지름입니다.
    /// </summary>
    /// <param name="armyData">소속 부대의 스탯입니다. 도착 판정 반경을 여기서 읽습니다.</param>
    /// <returns>목적지 반경 안에 있으면 true를 반환합니다.</returns>
    public bool IsOnPosition(in Army_Data armyData)
    {
        Vector3 distenceVector;

        distenceVector = location - position;
        distenceVector.y = 0;

        float radius = armyData.GetRadius();
        return distenceVector.sqrMagnitude < radius * radius;
    }

    /// <summary>
    /// 유닛이 진형 목표 지점에 도달했는지 확인합니다.
    /// IsOnPosition과 달리 판정 기준이 유닛 크기(GetSize)입니다.
    /// </summary>
    /// <param name="armyData">소속 부대의 스탯입니다. 판정 크기를 여기서 읽습니다.</param>
    /// <returns>목표 지점 범위 안에 있으면 true를 반환합니다.</returns>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 속도와 반지름을 여기서 읽습니다.</param>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 붕괴 여부 판정에 사용합니다.</param>
    /// <returns>적을 마주 본 채 발놀림으로 움직여야 하면 true를 반환합니다.</returns>
    private bool Is_Combat_Footwork(in Army_Data armyData)
    {
        // 무너진 부대는 발놀림을 쓰지 않습니다.
        //
        // 패주는 '적을 마주한 채 물러나는 것'이 아니라 등을 보이고 달아나는 것입니다.
        // 이 검사가 없으면 붕괴한 부대가 뒷걸음(속도 45%)으로 도망쳐
        // 패주 속도 배율(160%)이 상쇄되고, 전열을 유지한 채 후퇴하는 것처럼 보입니다.
        //
        // 표적 해제(Lose_Target)만으로는 막을 수 없습니다.
        // Army.On_Rout이 표적을 지워도 같은 틱의 Unit_Fight_Job이 다시 표적을 잡아
        // btarget이 곧바로 되살아나기 때문입니다. 부대 상태를 직접 봐야 확실합니다.
        if (armyData.IsBroken()) return false;

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
    /// <param name="moveDirection">이동하려는 방향입니다.</param>
    /// <returns>속도 배율입니다. 정면이면 1.0, 옆걸음과 뒷걸음은 그보다 낮습니다.</returns>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 발놀림 판정에 사용합니다.</param>
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
        if (Is_Combat_Footwork(armyData))
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
    /// <param name="armyData">소속 부대의 스탯입니다. 속도와 사거리를 여기서 읽습니다.</param>
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

        if (Is_Combat_Footwork(armyData))
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

            Block_Into_Enemy(armyData);
            return;
        }

        movementVector = unitForwardVector.normalized + directionVector.normalized;
        movementVector = movementVector * 0.5f;
        movementVector.y = 0;

        movementSpeed = movementSpeed * GetCurrentMoveSpeed();
        movementSpeed = movementSpeed * Constant.deltaTime;

        movementVector = movementVector * movementSpeed;

        // 적 대열에 막혔으면 뚫고 들어가지 못합니다.
        Block_Into_Enemy(armyData);
    }

    /// <summary>
    /// 교전 중 목표를 향한 이동을 처리합니다.
    ///
    /// 토탈워식 전열 유지의 핵심 규칙:
    ///   1) 사거리 안이면 '제자리에서' 싸운다. 계속 전진하면 두 대열이 서로를 관통합니다.
    ///   2) 진형 슬롯에서 일정 거리 이상 벗어나면 더 쫓지 않는다. (전열 이탈 방지)
    ///   3) 사거리 밖일 때만 한 걸음 다가간다.
    /// </summary>
    /// <param name="armyData">소속 부대의 스탯입니다. 사거리와 방어 태세를 여기서 읽습니다.</param>
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

        Block_Into_Enemy(armyData);
    }

    /// <summary>유닛이 유휴 상태일 때의 행동을 처리합니다.</summary>
    /// <param name="armyData">소속 부대의 스탯입니다.</param>
    private void Idle(in Army_Data armyData)
    {
        // 무너진 부대는 표적을 붙들고 있어도 교전 행동을 하지 않습니다.
        //
        // btarget은 부대가 붕괴한 뒤에도 참일 수 있습니다.
        // Army.On_Rout이 표적을 지워도 같은 틱의 Unit_Fight_Job이 사거리 안의 적을
        // 다시 잡기 때문입니다. 그 상태로 여기 들어오면 패주 중인 병사가
        // 적을 마주 본 채 제자리 교전을 하거나 대열을 메우려 듭니다.
        // 실제 이동은 부대의 Move_Escape가 담당하므로 여기서는 아무것도 하지 않습니다.
        if (armyData.IsBroken())
        {
            movementVector = new Vector3();
            return;
        }

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
                // 무너진 부대는 예외입니다. 달아나는 병사는 발을 맞추지 않습니다.
                if (Is_Combat_Footwork(armyData)) movementSpeed *= Get_Footwork_Speed_Rate(step);

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
    /// <param name="armyData">소속 부대의 스탯입니다. 돌격 속도와 접촉 거리 계산에 사용합니다.</param>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 자기 반지름을 여기서 읽습니다.</param>
    /// <returns>접촉이 성립하는 거리입니다.</returns>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 상한 계산에 반지름을 사용합니다.</param>
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
    /// <param name="armyData">소속 부대의 스탯입니다. 접촉 거리 판정에 사용합니다.</param>
    private void Block_Into_Enemy(in Army_Data armyData)
    {
        // 돌격 중에는 진입 성분을 제거하지 않습니다. 부딪히는 것이 목적입니다.
        //
        // 다만 이제 Move_Target의 돌격 분기가 접촉면까지의 남은 거리로
        // 이동량을 클램프하므로, 여기서 막지 않아도 관통하지 않습니다.
        if (bcharging) return;

        if (!benemyContact) return;
        if (enemyContactNormal.sqrMagnitude < 0.0001f) return;

        float into = Vector3.Dot(movementVector, enemyContactNormal);
        if (into <= 0.0f) return;

        // 대형 유닛(기병 포함)은 완전히 막히지 않고 조금씩 밀고 들어갑니다.
        //
        // 이것이 없으면 기병이 보병 대열 앞에서 그대로 굳어 버려
        // '돌파'라는 개념이 성립하지 않습니다.
        // 다만 완전히 자유롭게 통과하지도 못하므로 접촉면은 유지됩니다.
        float remain = armyData.IsLarge() ? Constant.large_Push_Through_Rate : 0.0f;

        movementVector -= enemyContactNormal * into * (1.0f - remain);
    }

    /// <summary>
    /// 교전 중 자기 진형 자리로 조금씩 붙습니다. (전열 재정비)
    ///
    /// 토탈워에서 전열은 사상자가 나면 옆 병사가 빈자리를 메우며 유지됩니다.
    /// 여기서는 적을 바라본 채(발놀림) 천천히 이동하므로 등을 보이지 않습니다.
    /// </summary>
    /// <param name="armyData">소속 부대의 스탯입니다. 이동 속도를 여기서 읽습니다.</param>
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
        Block_Into_Enemy(armyData);
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
    /// <param name="armyData">소속 부대의 스탯입니다. 최고 속도와 가속도를 여기서 읽습니다.</param>
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
        // 오르막을 오를 때도 느려집니다.
        max_moveSpeed = armyData.GetMoveSpeed()
                        * armyData.GetFatigueRate()
                        * armyData.GetStanceSpeedRate()
                        * armyData.GetSlopeSpeedRate();

        if (btargetMoveTo)
        {
            Vector3 dirationVecotr = targetVector - position;
            dirationVecotr.y = 0.0f;

            // 목표 자리에 이미 서 있으면 방향을 잴 수 없습니다.
            //
            // 영벡터를 LookRotation에 넘기면 Unity가
            // "Look rotation viewing vector is zero" 오류를 내고 항등 회전을
            // 돌려줍니다. 그러면 엉뚱한 각도로 판정되어 가속 여부가 뒤집힙니다.
            //
            // 2,000틱 실행에서 121회 발생했습니다. 유닛이 슬롯에 도착할수록
            // 잦아지므로, 전투가 성립해 대열이 자리를 잡기 시작하면서
            // 비로소 눈에 띄었습니다.
            //
            // 도착한 유닛은 더 가속할 이유가 없으므로 그대로 둡니다.
            // (아래 램프도 거리가 0이면 배율이 1이라 영향이 없습니다)
            if (dirationVecotr.sqrMagnitude > 0.000001f)
            {
                // 목표를 향하고 있을 때만 가속합니다.
                // 몸이 돌아가 있으면 먼저 방향을 잡고 나서 붙습니다.
                float angle = Quaternion.Angle(
                    rotation, Quaternion.LookRotation(dirationVecotr));

                if (angle <= 30.0f)
                {
                    currentMoveSpeed += armyData.GetAcceleration() * Constant.deltaTime;
                }
            }

            // 멀리 갈수록 최고 속도를 올립니다. 가속도는 그대로입니다.
            //
            // 상한만 올리므로 유닛은 평소와 같은 비율로 속도를 붙이되,
            // 더 높은 속도까지 계속 붙습니다. 그래서 '순간이동하듯 튄다'가
            // 아니라 '멀리 갈 일이 있으니 점점 빨라진다'로 보입니다.
            //
            // 거리를 그대로 t로 넘기지 않는 이유:
            // Mathf.Lerp는 t를 0~1로 자릅니다. 예전 코드는 거리(미터)를
            // 그대로 넘겨서, 1m만 벗어나도 곧바로 최대 배율이 걸렸습니다.
            // 완만한 램프처럼 보였지만 실제로는 계단 함수였고, 그것이
            // 부대 이동이 지나치게 빨라 보이던 원인입니다.
            float full = Constant.distance_Speed_Target_Full;

            // 0으로 나누는 것을 막습니다. 거리 설정이 0이면 램프가 없는
            // 것으로 보고 배율을 걸지 않습니다.
            float ramp = full > 0.0f
                ? Mathf.Clamp01(dirationVecotr.magnitude / full)
                : 0.0f;

            max_moveSpeed *= Mathf.Lerp(1.0f, Constant.move_Speed_Target, ramp);
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

    // SetResistance는 제거되었습니다.
    //
    // 물리 충돌 진입 시점에 속도를 한 번 깎던 함수인데, 그 역할은 이미
    // Accelerate()의 benemyContact 분기(blocked_Accelerate_Rate)와
    // Block_Into_Enemy()의 진입 성분 제거가 매 틱 수행하고 있습니다.
    // 질량 우위에 따른 밀어붙임도 Collision_Resolve_Job이 반영합니다.
}

// 유닛 클래스
partial class Unit
{
    // Public methods
    /// <summary>유닛의 이동을 시작합니다.</summary>
    public void Move_Start()
    {
        // 경로탐색을 쓰지 않으므로 목표만 정하면 됩니다.
        unit_Data.Move_Start();
    }

    /// <summary>지정된 목표 트랜스폼으로 유닛 이동을 시작합니다.</summary>
    // Move_Start(Transform)은 제거되었습니다.
    // 진형 슬롯이 Transform이 아니게 되어 호출부가 사라졌습니다.
    // 슬롯으로 이동하려면 Move_Start(int, Vector3)을 쓰십시오.

    /// <summary>
    /// 배정받은 진형 슬롯 인덱스로 이동을 시작합니다.
    ///
    /// Transform 대신 인덱스를 받는 경로입니다. 위치는 매 틱 부대의
    /// 슬롯 배열에서 꺼내므로, 여기서는 '어느 자리인가'만 정합니다.
    /// </summary>
    /// <param name="slotIndex">진형 슬롯 인덱스입니다.</param>
    /// <param name="slotWorld">그 슬롯의 현재 월드 좌표입니다.</param>
    public void Move_Start(int slotIndex, Vector3 slotWorld)
    {
        targetSlotIndex = slotIndex;

        unit_Data.location = slotWorld;
        unit_Data.btargetMoveTo = true;
        unit_Data.Move_Start();
    }

    /// <summary>
    /// 지정된 좌표로 이동을 시작합니다. 바라볼 방향은 부대의 진형 방향에서 구합니다.
    /// 슬롯이 아니라 좌표를 직접 받는 경로이므로 슬롯 배정은 해제됩니다.
    /// </summary>
    /// <param name="location">이동할 목적지 좌표입니다.</param>
    public void Move_Start(Vector3 location)
    {
        Quaternion direction =
            Formation_Util.Rotation_From_Line(army.GetFormation_Direction());

        // 이 경로는 좌표를 직접 받으므로 슬롯 배정이 없습니다.
        // 지난 배정을 남겨 두면 다음 틱에 엉뚱한 슬롯을 따라갈 수 있으므로 지웁니다.
        // (unit_Data.Move_Start(Vector3, Quaternion)이 btargetMoveTo를 false로
        //  두므로 실제 읽기 경로는 타지 않지만, 상태를 남겨 둘 이유가 없습니다)
        targetSlotIndex = -1;

        unit_Data.Move_Start(location, direction);
    }

    /// <summary>
    /// 진형 재배치 이동을 처리합니다. 교전 중에도 자기 자리로 붙게 하는 경로입니다.
    /// </summary>
    /// <param name="location">새로 배정된 진형 슬롯의 좌표입니다.</param>
    /// <param name="slotIndex">
    /// 새로 배정된 슬롯의 인덱스입니다. -1이면 슬롯 없이 좌표만 지정합니다.
    ///
    /// 왜 인덱스를 함께 받는가:
    /// 예전에는 좌표만 받아 Move_Start(Vector3)를 탔고, 그 경로가
    /// targetSlotIndex를 -1로 지웠습니다. 즉 **재정비 한 번이면 슬롯 배정이
    /// 끊겼습니다.** 실측에서 표본으로 잡힌 유닛이 가능 표본의 절반뿐이었고,
    /// 슬롯 이탈 평균이 간격의 2.1배(3.12m / interval 1.5m)까지 벌어졌습니다.
    ///
    /// 배정이 끊기면 '이 슬롯에 선 유닛이 누구인가'를 물을 수 없습니다.
    /// 선두 지정은 그 방향이 지속적으로 유지되어야 성립하므로, 재정비가
    /// 슬롯 번호를 보존하도록 바꿉니다.
    ///
    /// 호출부는 전부 이미 매칭 결과(match[i])를 손에 들고 있으므로
    /// 새로 계산할 것이 없습니다.
    /// </param>
    public void Move_Reformation(Vector3 location, int slotIndex = -1)
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

        // 슬롯 배정을 복원합니다. **반드시 위 분기 뒤여야 합니다.**
        //
        // Move_Start(Vector3)가 targetSlotIndex를 -1로 지우기 때문입니다.
        // 그래서 예전에는 재정비 한 번이면 배정이 끊겼고, '이 슬롯에 선
        // 유닛이 누구인가'를 물을 수 없었습니다.
        // (실측: 표본으로 잡힌 유닛이 가능 표본의 절반뿐)
        //
        // 배정과 이동은 서로 다른 것입니다.
        //   배정 = 이 유닛이 대열의 어느 자리를 맡았는가 (신원)
        //   이동 = 이번에 어디로 갈 것인가 (재정비가 방금 계산한 좌표)
        //
        // 여기서 배정만 되살리고 이동 방식은 그대로 둡니다.
        // btargetMoveTo를 건드리지 않는 것이 중요합니다. 그 플래그를 켜면
        // Unit._Update_Move가 매 틱 location을 슬롯 배열 값으로 덮어쓰는데,
        // 배열은 마지막 Set_Formation_Move 시점의 값이라 재정비가 방금
        // 계산한 좌표와 다를 수 있습니다.
        if (slotIndex >= 0) targetSlotIndex = slotIndex;
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

        Apply_Move(step);

        // 진형 목표도 함께 끌고 가야 다음 재정비에서 되돌아가지 않습니다.
        unit_Data.location = unit_Data.position;
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

        Apply_Move(escapeVector);

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
    /// <summary>
    /// 유닛의 이동을 업데이트합니다.
    ///
    /// 회전은 여기서 하지 않습니다. unit_Data.rotation은 시뮬레이션
    /// (Unit_Data.Rotation)이 이미 정했고, Transform 반영은 틱 마지막의
    /// Write_Transform_Job이 위치와 함께 한 번에 처리합니다.
    /// </summary>
    private void _Update_Move()
    {
        Move();
    }

    /// <summary>유닛의 이동을 처리합니다.</summary>
    private void Move()
    {
        // Transform을 만지지 않습니다.
        //
        // unit_Data.position이 이번 틱의 진짜 위치이고, Controller가
        // 마지막에 Write_Transform_Job으로 전 유닛을 한 번에 반영합니다.
        // 유닛마다 대입하면 네이티브 왕복이 인원수만큼 발생해
        // 9,600명 기준 6 ms가 낭비됩니다. (Job으로 옮기면 0.6 ms)
        Vector3 delta = unit_Data.GetMovementVector();
        Vector3 position = unit_Data.position;

        if (delta.sqrMagnitude >= 0.0000001f)
        {
            position += delta;
        }

        // 조향 목표는 '가야 할 자리'입니다.
        //
        // 예전에는 navMeshAgent.steeringTarget을 읽었습니다. 그러려면 매 틱
        // SetDestination()으로 경로를 다시 뽑아야 했고, 그 두 호출이
        // 600명 기준 틱당 88ms로 프레임 예산(16.6ms)의 5배를 먹고 있었습니다.
        //
        // 유닛은 스스로 길을 찾지 않습니다. 부대 기준점이 NavMesh를 따라 움직이고
        // 유닛은 배정받은 진형 슬롯으로 갈 뿐이므로, 목표 지점을 그대로 쓰면
        // 결과가 같으면서 경로탐색 비용이 사라집니다.
        unit_Data.steeringTarget = unit_Data.location;
        unit_Data.position = position;

        switch (unit_Data.e_Unit_Move)
        {
            case E_Unit_Move.Move:
                if (unit_Data.btargetMoveTo && targetSlotIndex >= 0)
                {
                    // 갈 자리를 정합니다.
                    //
                    // 선두를 따르는 중이면 Army._Update_Leader_Follow가 이미
                    // unit_Data.location에 목표를 넣어 두었으므로 그것을 씁니다.
                    // 여기서 슬롯 배열로 덮어쓰면 선두 추종이 무효가 됩니다.
                    //
                    // 아니면 슬롯 위치를 배열에서 꺼냅니다.
                    // 예전에는 targetMoveTo.position(Transform)을 읽었습니다.
                    // C# -> 네이티브 왕복이라 호출 횟수 자체가 비용이고,
                    // 9,600명이면 틱당 2.955 ms였습니다.
                    // 배열은 부대가 틱당 한 번만 월드 좌표로 펼쳐 두므로
                    // 여기서는 단순 인덱싱입니다. (실측 0.029 ms)
                    Vector3 target = bfollowLeader
                        ? unit_Data.location
                        : army.Get_Slot_World(targetSlotIndex);

                    unit_Data.location = target;
                    unit_Data.targetVector = target;
                    unit_Data.steeringTarget = target;
                }
                break;
            case E_Unit_Move.Idle:
                if (unit_Data.btarget)
                {
                    // 교전 중에는 표적 쪽으로 향합니다.
                    unit_Data.steeringTarget = unit_Data.unit_Target_Data.position;
                }
                break;
        }

        if (unit_Data.bstop)
            Move_Stop();
    }

    /// <summary>
    /// 유닛을 실제로 옮깁니다.
    ///
    /// 시뮬레이션 위치만 갱신하고 Transform은 건드리지 않습니다.
    /// 반영은 Controller가 틱 마지막에 Job으로 한 번에 합니다.
    /// 유닛 간 겹침은 Collision_Resolve_Job이 처리합니다.
    /// </summary>
    /// <param name="delta">이번 틱에 적용할 이동량입니다.</param>
    private void Apply_Move(Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f) return;

        unit_Data.position += delta;
    }

    /// <summary>유닛의 이동을 중지합니다.</summary>
    private void Move_Stop()
    {
        unit_Data.bstop = false;
    }

    // Move_Collision_Enter / Move_Collision_Stay는 제거되었습니다.
    // 물리 콜백에서만 호출되던 함수들이며, 유닛 간 밀어내기는
    // Collision_Resolve_Job이 질량비까지 반영해 처리합니다.
}
