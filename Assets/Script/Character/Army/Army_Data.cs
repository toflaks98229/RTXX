using System;

using System.Collections.Generic;
using UnityEngine;

// 열거형 (Enums)
/// <summary>
/// 부대의 이동 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Move
{
    Move,       // 일반 이동
    MoveToTarget, // 타겟으로 이동
    MoveEscape,   // 도주
    MoveCharge,   // 돌격
    Idle        // 대기
}

/// <summary>
/// 부대의 진형 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Formation
{
    Formation,      // 진형 상태
    NonFormation  // 비진형 상태
}

/// <summary>
/// 부대의 재정비 태세입니다.
///
/// 이 값은 '전열 재정비를 어떻게 할 것인가'만 결정합니다.
/// 어느 쪽이든 재정비가 적을 감지해 각도를 바꾸지는 않습니다.
/// 전열 각도는 언제나 플레이어가 명령으로 잡아 둔 방향을 따릅니다.
/// </summary>
public enum E_Army_Stance
{
    /// <summary>
    /// 방어 태세: 명령으로 설정한 전열을 그대로 유지하며 재정비합니다.
    /// 대열의 폭과 각도가 보존되므로 방어선이 흐트러지지 않습니다.
    /// </summary>
    Line,

    /// <summary>
    /// 공격 태세: 전열을 엄격히 지키지 않고, 앞쪽에 생긴 빈자리만 메웁니다.
    /// 추격이나 난전에서 병력이 앞으로 뭉치는 형태가 됩니다.
    /// </summary>
    Loose,

    /// <summary>
    /// 방패벽: 밀집해 방패를 맞대고 버팁니다.
    /// 원거리 방어와 돌격 저항이 크게 오르지만 걷기 속도로 제한됩니다.
    /// </summary>
    ShieldWall,

    /// <summary>
    /// 창벽: 2열의 창이 1열 사이로 나옵니다.
    /// 돌격 저항이 극대화되고, 정지 상태에서는 돌격 반사가 발동합니다.
    /// 대신 원거리 방어가 떨어지고 걷기 속도로 제한됩니다.
    /// </summary>
    SpearWall,

    /// <summary>
    /// 산개: 적이 다가오면 자동으로 물러나며 거리를 유지합니다.
    /// 원거리 부대가 근접에 휘말리지 않도록 하는 태세입니다.
    /// </summary>
    Skirmish
}

/// <summary>
/// 부대의 피로 단계입니다. 토탈워의 Fresh~Exhausted에 대응합니다.
/// </summary>
public enum E_Army_Fatigue
{
    Fresh,      // 원기왕성
    Active,     // 활동적
    Winded,     // 숨참
    Tired,      // 지침
    VeryTired,  // 매우 지침
    Exhausted   // 탈진
}

/// <summary>
/// 부대의 사기 상태를 나타내는 열거형입니다.
/// 토탈워에서 부대는 전멸이 아니라 이 상태가 무너지면서 패배합니다.
/// </summary>
public enum E_Army_Morale
{
    Steady,   // 견고 - 정상 교전
    Wavering, // 동요 - 곧 무너질 수 있음
    Broken,   // 붕괴 - 전투를 거부하고 패주 (재결집 가능)
    Shattered // 와해 - 너무 많이 잃어 재결집이 불가능한 상태
}

/// <summary>
/// 부대의 전투 상태를 나타내는 열거형입니다.
/// </summary>
public enum E_Army_Fight
{
    Melee, // 근접 전투
    Range, // 원거리 전투
    Non    // 비전투
}

// 클래스
/// <summary>
/// 부대의 진형에 대한 데이터를 담고 있는 클래스입니다.
/// </summary>
[Serializable]
public class Formation_Data
{
    // 공개 멤버 변수
    /// <summary>
    /// 각 유닛의 목표 위치 리스트입니다.
    /// </summary>
    public List<Vector3> formation;
    /// <summary>
    /// 진형의 전방 방향입니다.
    /// </summary>
    public Vector3 direction;
    /// <summary>
    /// 진형의 중심 위치입니다.
    /// </summary>
    public Vector3 position;
    /// <summary>
    /// 진형의 너비를 결정하는 유닛 수입니다.
    /// </summary>
    public int num;

    // 생성자
    /// <summary>
    /// 다른 Formation_Data 인스턴스를 복사하여 새로운 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="formation_Data">복사할 Formation_Data 인스턴스입니다.</param>
    public Formation_Data(Formation_Data formation_Data)
    {
        position = formation_Data.position;
        direction = formation_Data.direction;
        num = formation_Data.num;
        formation = formation_Data.formation;
    }

    /// <summary>
    /// 모든 진형 데이터를 인수로 받아 새로운 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="num">진형의 너비입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <param name="formation">유닛들의 진형 위치 리스트입니다.</param>
    public Formation_Data(int num, Vector3 direction, Vector3 position, List<Vector3> formation)
    {
        this.num = num;
        this.direction = direction;
        this.position = position;
        this.formation = formation;
    }

    // 공개 메서드
    /// <summary>
    /// 현재 Formation_Data 인스턴스를 반환합니다.
    /// </summary>
    /// <returns>Formation_Data 인스턴스입니다.</returns>
    public Formation_Data GetFormation_Data()
    {
        return this;
    }

    /// <summary>
    /// 진형 데이터를 설정합니다.
    /// </summary>
    /// <param name="num">진형의 너비입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <param name="formation">유닛들의 진형 위치 리스트입니다.</param>
    public void Set_Formation_Data(int num, Vector3 direction, Vector3 position, List<Vector3> formation)
    {
        this.num = num;
        this.direction = direction;
        this.position = position;
        this.formation = formation;
    }

    /// <summary>
    /// 다른 Formation_Data 인스턴스의 데이터로 현재 인스턴스를 덮어씁니다.
    /// </summary>
    /// <param name="formation_Data">덮어쓸 Formation_Data 인스턴스입니다.</param>
    public void Set_Formation_Data(Formation_Data formation_Data)
    {
        position = formation_Data.position;
        direction = formation_Data.direction;
        num = formation_Data.num;
        formation = formation_Data.formation;
    }

    /// <summary>
    /// 진형의 방향을 반환합니다.
    /// </summary>
    /// <returns>진형의 방향 벡터입니다.</returns>
    public Vector3 GetDirection()
    {
        return direction;
    }

    /// <summary>
    /// 진형의 위치를 반환합니다.
    /// </summary>
    /// <returns>진형의 위치 벡터입니다.</returns>
    public Vector3 GetPosition()
    {
        return position;
    }

    /// <summary>
    /// 진형의 너비를 반환합니다.
    /// </summary>
    /// <returns>진형의 너비입니다.</returns>
    public int GetNum()
    {
        return num;
    }
}

// 구조체
/// <summary>
/// 부대의 전반적인 데이터를 담고 있는 구조체입니다.
/// </summary>
[Serializable]
public struct Army_Data
{
    // 공개 멤버 변수
    /// <summary>
    /// 부대의 현재 위치입니다.
    /// </summary>
    public Vector3 position;
    /// <summary>
    /// 부대의 현재 회전 값입니다.
    /// </summary>
    public Quaternion rotation;
    /// <summary>
    /// 부대가 플레이어 소유인지 여부입니다.
    /// </summary>
    public bool bplayer;
    /// <summary>
    /// 명령 상태인지 여부입니다.
    /// </summary>
    public bool bonCommand;
    /// <summary>
    /// 부대의 유닛 통계 데이터입니다.
    /// </summary>
    public Unit_Stat unit_Stat;
    /// <summary>
    /// 부대 전체의 총 HP입니다.
    /// </summary>
    public float HP_All;
    /// <summary>
    /// 현재 부대의 유닛 수입니다.
    /// </summary>
    public int unit_Num;
    /// <summary>
    /// 부대가 가질 수 있는 최대 유닛 수입니다.
    /// </summary>
    public int unit_Num_Max;
    /// <summary>
    /// 방어 상태인지 여부입니다.
    /// </summary>
    public bool bdefense;
    /// <summary>
    /// 부대의 전투 상태입니다.
    /// </summary>
    public E_Army_Fight e_Army_Fight;
    /// <summary>
    /// 부대의 이동 상태입니다.
    /// </summary>
    public E_Army_Move e_Army_Move;
    /// <summary>
    /// 부대의 진형 상태입니다.
    /// </summary>
    public E_Army_Formation e_Army_Formation;
    /// <summary>
    /// 부대의 재정비 태세입니다. 기본값은 전열을 지키는 방어 태세입니다.
    /// </summary>
    public E_Army_Stance e_Army_Stance;
    /// <summary>
    /// 명령 상태 지속 시간을 측정하는 타이머입니다.
    /// </summary>
    public Timer timer_On_Command;
    /// <summary>
    /// 재편성 상태를 나타내는 타이머입니다.
    /// </summary>
    public Timer timer_Reformation;
    /// <summary>
    /// 재편성이 필요한지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool breformation;

    // --- 사기 ---
    /// <summary>현재 사기입니다. 0이 되면 부대가 붕괴합니다.</summary>
    public float morale;
    /// <summary>
    /// 이번 틱의 목표 사기입니다. 메인 스레드에서 상황(손실/포위/깃발)을 반영해 계산하고,
    /// 실제 morale은 이 값을 향해 서서히 이동합니다.
    /// </summary>
    public float morale_Target;
    /// <summary>부대의 사기 상태입니다.</summary>
    public E_Army_Morale e_Army_Morale;
    /// <summary>패주 후 재결집까지의 대기 시간을 재는 타이머입니다.</summary>
    public Timer timer_Rally;
    /// <summary>이번 틱에 붕괴가 발생했는지 여부입니다. 이벤트 발행용 1회성 플래그입니다.</summary>
    public bool broutedThisTick;
    /// <summary>이번 틱에 재결집했는지 여부입니다. 이벤트 발행용 1회성 플래그입니다.</summary>
    public bool bralliedThisTick;
    /// <summary>패주 시 달아날 방향입니다. 메인 스레드가 적 반대 방향으로 설정합니다.</summary>
    public Vector3 escapeDirection;
    /// <summary>
    /// 돌격 등으로 받은 순간적인 사기 충격입니다.
    /// 목표 사기에서 차감되며 시간이 지나면 회복됩니다.
    /// </summary>
    public float morale_Shock;

    /// <summary>
    /// 이번 틱의 사기 모디파이어 내역입니다.
    /// morale_Target은 이 항목들의 합으로 산출되며,
    /// UI는 여기서 "왜 사기가 떨어지는가"를 항목별로 읽어 갑니다.
    /// </summary>
    public Morale_Modifiers morale_Modifiers;

    /// <summary>이번 틱에 와해(재결집 불가)가 발생했는지 여부입니다. 이벤트 발행용입니다.</summary>
    public bool bshatteredThisTick;

    // --- 피로도 ---
    /// <summary>현재 피로도입니다. 높을수록 모든 능력이 떨어집니다.</summary>
    public float fatigue;
    /// <summary>피로 단계입니다. UI 표시와 가독성을 위한 값입니다.</summary>
    public E_Army_Fatigue e_Army_Fatigue;

    /// <summary>
    /// 부대 기준점의 실측 이동 속도(초당)입니다.
    /// 돌격 충격량이 '얼마나 빠르게 달려와 부딪혔는가'에 비례하도록 만들기 위해
    /// 매 틱 위치 변화로부터 계산합니다.
    /// </summary>
    public float currentSpeed;

    // 공개 메서드
    /// <summary>
    /// 부대 데이터를 초기화합니다.
    /// </summary>
    public void _Start()
    {
        e_Army_Move = E_Army_Move.Idle;
        e_Army_Stance = E_Army_Stance.Line;
        timer_Reformation = new Timer(Constant.time_Reformation);

        // 사기 초기화
        morale = Constant.morale_Max;
        morale_Target = Constant.morale_Max;
        e_Army_Morale = E_Army_Morale.Steady;
        timer_Rally = new Timer(Constant.time_Rally);
        broutedThisTick = false;
        bralliedThisTick = false;
        escapeDirection = Vector3.zero;
        morale_Shock = 0.0f;
        morale_Modifiers = new Morale_Modifiers();
        bshatteredThisTick = false;

        fatigue = 0.0f;
        e_Army_Fatigue = E_Army_Fatigue.Fresh;
        currentSpeed = 0.0f;

        // 손실률 계산의 기준이 되는 초기 인원입니다.
        if (unit_Num_Max < unit_Num) unit_Num_Max = unit_Num;
    }

    /// <summary>
    /// 부대 데이터를 업데이트합니다.
    /// </summary>
    public void _Update()
    {
        _Update_Fatigue();
        _Update_Morale();

        switch (e_Army_Move)
        {
            case E_Army_Move.Idle:
                timer_Reformation._Update();
                if (timer_Reformation.IsOverTime())
                {
                    breformation = true;
                    timer_Reformation.ReSetTimer();
                }
                break;
            case E_Army_Move.Move:
                timer_Reformation.ReSetTimer();
                breformation = false;
                break;
            case E_Army_Move.MoveToTarget:
                timer_Reformation.ReSetTimer();
                breformation = false;
                break;
        }
    }

    /// <summary>
    /// 현재 행동에 따라 피로도를 누적하거나 회복합니다.
    /// 돌격이 가장 힘들고, 제자리에서 쉬면 회복됩니다.
    /// </summary>
    public void _Update_Fatigue()
    {
        float gain;

        switch (e_Army_Move)
        {
            case E_Army_Move.MoveCharge:
                gain = Constant.fatigue_Gain_Charge;
                break;

            case E_Army_Move.MoveEscape:
                gain = Constant.fatigue_Gain_Rout;
                break;

            case E_Army_Move.Move:
            case E_Army_Move.MoveToTarget:
                gain = Constant.fatigue_Gain_Move;
                break;

            default:
                // 제자리에 있어도 교전 중이면 계속 지칩니다.
                gain = e_Army_Fight != E_Army_Fight.Non
                    ? Constant.fatigue_Gain_Melee
                    : -Constant.fatigue_Recover;
                break;
        }

        fatigue += gain * Constant.deltaTime;

        if (fatigue < 0.0f) fatigue = 0.0f;
        if (fatigue > Constant.fatigue_Max) fatigue = Constant.fatigue_Max;

        if (fatigue >= Constant.fatigue_Exhausted) e_Army_Fatigue = E_Army_Fatigue.Exhausted;
        else if (fatigue >= Constant.fatigue_VeryTired) e_Army_Fatigue = E_Army_Fatigue.VeryTired;
        else if (fatigue >= Constant.fatigue_Tired) e_Army_Fatigue = E_Army_Fatigue.Tired;
        else if (fatigue >= Constant.fatigue_Winded) e_Army_Fatigue = E_Army_Fatigue.Winded;
        else if (fatigue >= Constant.fatigue_Active) e_Army_Fatigue = E_Army_Fatigue.Active;
        else e_Army_Fatigue = E_Army_Fatigue.Fresh;
    }

    /// <summary>
    /// 피로에 따른 성능 배율입니다. 1.0(생생함)에서 fatigue_Min_Rate(탈진)까지 떨어집니다.
    /// 명중, 피해, 이동 속도에 곱해 씁니다.
    /// </summary>
    public float GetFatigueRate()
    {
        float rate = fatigue / Constant.fatigue_Max;
        if (rate < 0.0f) rate = 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        return 1.0f - rate * (1.0f - Constant.fatigue_Min_Rate);
    }

    /// <summary>
    /// 돌격 운동량 비율(0~1)입니다.
    /// 현재 속도를 돌격 속도로 나눈 값이며, 제자리에서 붙으면 0에 가깝습니다.
    /// 충격량(피해 보너스와 사기 충격)이 여기에 비례합니다.
    /// </summary>
    public float GetChargeMomentumRate()
    {
        float chargeSpeed = GetMeleeChargeSpeed();
        if (chargeSpeed <= 0.0f) chargeSpeed = GetMoveSpeed();
        if (chargeSpeed <= 0.0f) return 0.0f;

        float rate = currentSpeed / chargeSpeed;
        if (rate < 0.0f) rate = 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        return rate;
    }

    /// <summary>피로 때문에 목표 사기에서 깎이는 양입니다.</summary>
    public float GetFatigueMoralePenalty()
    {
        float rate = fatigue / Constant.fatigue_Max;
        if (rate < 0.0f) rate = 0.0f;
        if (rate > 1.0f) rate = 1.0f;

        return rate * Constant.fatigue_Morale_Penalty;
    }

    /// <summary>
    /// 사기를 갱신하고 붕괴/재결집 상태 전이를 처리합니다.
    /// morale_Target은 메인 스레드에서 미리 계산해 둡니다.
    /// </summary>
    public void _Update_Morale()
    {
        broutedThisTick = false;
        bralliedThisTick = false;
        bshatteredThisTick = false;

        // 돌격 충격은 시간이 지나며 회복됩니다.
        if (morale_Shock > 0.0f)
        {
            morale_Shock -= Constant.morale_Shock_Recover * Constant.deltaTime;
            if (morale_Shock < 0.0f) morale_Shock = 0.0f;
        }

        // 충격을 반영한 실효 목표 사기입니다.
        float effectiveTarget = morale_Target - morale_Shock;
        if (effectiveTarget < 0.0f) effectiveTarget = 0.0f;

        // 목표 사기를 향해 서서히 이동합니다.
        // 즉시 대입하지 않는 이유는, 한 번의 큰 피해로 부대가 급사하듯
        // 무너지는 것을 막고 '버티다 무너지는' 흐름을 만들기 위함입니다.
        float delta = Constant.morale_Drift_Rate * Constant.deltaTime;
        if (morale < effectiveTarget)
        {
            morale += delta;
            if (morale > effectiveTarget) morale = effectiveTarget;
        }
        else if (morale > effectiveTarget)
        {
            morale -= delta;
            if (morale < effectiveTarget) morale = effectiveTarget;
        }

        if (morale > Constant.morale_Max) morale = Constant.morale_Max;
        if (morale < 0.0f) morale = 0.0f;

        switch (e_Army_Morale)
        {
            case E_Army_Morale.Steady:
            case E_Army_Morale.Wavering:
                if (morale <= Constant.morale_Rout_Threshold)
                {
                    // 붕괴: 전투를 거부하고 달아납니다.
                    //
                    // 이미 너무 많이 잃은 부대는 다시 싸우지 못합니다. (와해)
                    // 재결집 판정 자체를 건너뛰므로 영구히 전장을 떠납니다.
                    e_Army_Morale = IsShatterable()
                        ? E_Army_Morale.Shattered
                        : E_Army_Morale.Broken;

                    e_Army_Move = E_Army_Move.MoveEscape;
                    timer_Rally.ReSetTimer();

                    broutedThisTick = true;
                    if (e_Army_Morale == E_Army_Morale.Shattered) bshatteredThisTick = true;
                }
                else
                {
                    e_Army_Morale = morale <= Constant.morale_Waver_Threshold
                        ? E_Army_Morale.Wavering
                        : E_Army_Morale.Steady;
                }
                break;

            case E_Army_Morale.Broken:
                // 패주 중에도 손실이 계속 쌓이면 끝내 와해됩니다.
                if (IsShatterable())
                {
                    e_Army_Morale = E_Army_Morale.Shattered;
                    bshatteredThisTick = true;
                    break;
                }

                // 일정 시간이 지나고 사기를 회복했으면 재결집합니다.
                timer_Rally._Update();

                if (timer_Rally.IsOverTime() && morale >= Constant.morale_Rally_Threshold)
                {
                    timer_Rally.ReSetTimer();
                    e_Army_Morale = E_Army_Morale.Wavering;
                    e_Army_Move = E_Army_Move.Idle;
                    bralliedThisTick = true;
                }
                break;

            case E_Army_Morale.Shattered:
                // 와해된 부대는 어떤 경우에도 돌아오지 않습니다.
                e_Army_Move = E_Army_Move.MoveEscape;
                break;
        }
    }

    /// <summary>
    /// 손실이 재결집 불가 수준에 도달했는지 판정합니다.
    /// 이 상태에서 무너지면 Broken이 아니라 Shattered가 됩니다.
    /// </summary>
    private bool IsShatterable()
    {
        if (unit_Num_Max <= 0) return false;

        float lossRate = 1.0f - ((float)unit_Num / unit_Num_Max);
        return lossRate >= Constant.morale_Shatter_Loss_Rate;
    }

    /// <summary>부대가 와해되어 다시 싸울 수 없는 상태인지 여부입니다.</summary>
    public bool IsShattered()
    {
        return e_Army_Morale == E_Army_Morale.Shattered;
    }

    // ---------------------------------------------------------------------
    // 태세 (Stance)
    // ---------------------------------------------------------------------

    /// <summary>밀집 태세(방패벽/창벽)인지 여부입니다. 기동이 제한됩니다.</summary>
    public bool IsWallStance()
    {
        return e_Army_Stance == E_Army_Stance.ShieldWall
            || e_Army_Stance == E_Army_Stance.SpearWall;
    }

    /// <summary>
    /// 태세에 따른 이동 속도 배율입니다.
    /// 밀집 대열은 발을 맞춰야 하므로 뛸 수 없습니다.
    /// </summary>
    public float GetStanceSpeedRate()
    {
        return IsWallStance() ? Constant.stance_Wall_Speed_Rate : 1.0f;
    }

    /// <summary>태세에 따른 근접 방어 보정입니다.</summary>
    public float GetStanceMeleeDefence()
    {
        return e_Army_Stance == E_Army_Stance.SpearWall
            ? Constant.stance_SpearWall_Melee_Defence
            : 0.0f;
    }

    /// <summary>
    /// 태세에 따른 원거리 방어 보정입니다.
    /// 방패벽은 화살을 막아내고, 창벽은 밀집해 있어 오히려 취약합니다.
    /// </summary>
    public float GetStanceRangeDefence()
    {
        switch (e_Army_Stance)
        {
            case E_Army_Stance.ShieldWall:
                return Constant.stance_ShieldWall_Range_Defence;

            case E_Army_Stance.SpearWall:
                return -Constant.stance_SpearWall_Range_Penalty;

            default:
                return 0.0f;
        }
    }

    /// <summary>
    /// 정면에서 받는 돌격을 얼마나 무효화하는지의 비율(0~1)입니다.
    /// 1.0이면 돌격 보너스가 통째로 사라집니다.
    ///
    /// 측후방 판정은 호출부(Unit_Fight_Job)에서 각도를 보고 결정합니다.
    /// 뒤에서 찔린 창벽은 아무것도 막지 못합니다.
    /// </summary>
    public float GetChargeResistance()
    {
        switch (e_Army_Stance)
        {
            case E_Army_Stance.ShieldWall:
                return Constant.stance_ShieldWall_Charge_Resist;

            case E_Army_Stance.SpearWall:
                return Constant.stance_SpearWall_Charge_Resist;

            default:
                return 0.0f;
        }
    }

    /// <summary>
    /// 정지한 창벽이 돌격을 되받아치는지 여부입니다.
    /// 움직이는 창벽은 창을 세우지 못하므로 반사가 성립하지 않습니다.
    /// </summary>
    public bool IsChargeReflecting()
    {
        return e_Army_Stance == E_Army_Stance.SpearWall
            && e_Army_Move == E_Army_Move.Idle;
    }

    /// <summary>
    /// 부대가 무너져 패주 중인지 여부입니다.
    /// 와해(Shattered)도 붕괴의 일종이므로 함께 참입니다.
    /// 전투 중단 판정은 모두 이 함수를 거치므로 반드시 둘 다 포함해야 합니다.
    /// </summary>
    public bool IsBroken()
    {
        return e_Army_Morale == E_Army_Morale.Broken
            || e_Army_Morale == E_Army_Morale.Shattered;
    }

    /// <summary>
    /// 부대 전체의 총 HP를 반환합니다.
    /// </summary>
    /// <returns>총 HP입니다.</returns>
    public float GetAllHP()
    {
        if (HP_All < 0) return 0;
        return HP_All;
    }

    /// <summary>
    /// 부대 전체의 총 HP를 증가시킵니다.
    /// </summary>
    /// <param name="addFloat">추가할 HP 값입니다.</param>
    public void AddAllHP(float addFloat)
    {
        HP_All += addFloat;
    }

    // ---------------------------------------------------------------------
    // 스탯 접근자
    // 원본은 Get/Add 쌍마다 4~5줄씩 반복되어 약 300줄이었고,
    // GetSize/GetInterval만 '<= 0'이고 나머지는 '< 0', GetRadius/GetHeight는
    // 클램프가 아예 없는 등 규칙이 제각각이었습니다.
    // 여기서는 NonNegative 하나로 규칙을 통일했습니다.
    // (메서드 이름은 그대로 두어 호출부는 영향을 받지 않습니다)
    // ---------------------------------------------------------------------

    /// <summary>음수 스탯을 0으로 보정합니다. 모든 수치 스탯이 이 규칙을 따릅니다.</summary>
    private static float NonNegative(float value) => value < 0.0f ? 0.0f : value;

    // 이동
    public float GetMoveSpeed() => NonNegative(unit_Stat.moveSpeed);
    public void AddMoveSpeed(float v) => unit_Stat.moveSpeed += v;

    public float GetRotationSpeed() => NonNegative(unit_Stat.rotationSpeed);
    public void AddRotationSpeed(float v) => unit_Stat.rotationSpeed += v;

    public float GetAcceleration() => NonNegative(unit_Stat.acceleration);
    public void AddAccelerationd(float v) => unit_Stat.acceleration += v;

    public float GetMass() => NonNegative(unit_Stat.mass);
    public void AddMass(float v) => unit_Stat.mass += v;

    public float GetDrag() => NonNegative(unit_Stat.drag);
    public void AddDrag(float v) => unit_Stat.drag += v;

    // 근접 전투
    public float GetMeleeDamage() => NonNegative(unit_Stat.meleeDamage);
    public void AddMeleeDamage(float v) => unit_Stat.meleeDamage += v;

    /// <summary>근접 방어구 관통(AP) 피해량입니다. 방어구 감산을 받지 않습니다.</summary>
    public float GetMeleeDamageAP() => NonNegative(unit_Stat.meleeDamageAP);
    public void AddMeleeDamageAP(float v) => unit_Stat.meleeDamageAP += v;

    public float GetMeleeAttack() => NonNegative(unit_Stat.meleeAttack);
    public void AddMeleeAttack(float v) => unit_Stat.meleeAttack += v;

    public float GetMeleeAttackSpeed() => NonNegative(unit_Stat.meleeAttackSpeed);
    public void AddMeleeAttackSpeed(float v) => unit_Stat.meleeAttackSpeed += v;

    public float GetMeleeDiffense() => NonNegative(unit_Stat.meleeDiffense);
    public void AddMeleeDiffense(float v) => unit_Stat.meleeDiffense += v;

    public float GetMeleeRange() => NonNegative(unit_Stat.meleeRange);
    public void AddMeleeRange(float v) => unit_Stat.meleeRange += v;

    public float GetMeleeChargeSpeed() => NonNegative(unit_Stat.meleeChargeSpeed);
    public void AddMeleeChargeSpeed(float v) => unit_Stat.meleeChargeSpeed += v;

    public float GetMeleeChargeRange() => NonNegative(unit_Stat.meleeChargeRange);
    public void AddMeleeChargeRange(float v) => unit_Stat.meleeChargeRange += v;

    public float GetMeleeChargeBonus() => NonNegative(unit_Stat.meleeChargeBonus);
    public void AddMeleeChargeBonus(float v) => unit_Stat.meleeChargeBonus += v;

    // 방어구
    public float GetArmor() => NonNegative(unit_Stat.armor);
    public void AddArmor(float v) => unit_Stat.armor += v;

    public float GetShieldArmor() => NonNegative(unit_Stat.shieldArmor);
    public void AddShieldArmor(float v) => unit_Stat.shieldArmor += v;

    // 원거리 전투
    public bool IsRangeAttackAble() => unit_Stat.brangeAttackAble;
    public void SetRangeAttackAble(bool value) => unit_Stat.brangeAttackAble = value;

    public float GetRangeDamage() => NonNegative(unit_Stat.rangeDamage);
    public void AddRangeDamage(float v) => unit_Stat.rangeDamage += v;

    /// <summary>원거리 방어구 관통(AP) 피해량입니다. 방어구 감산을 받지 않습니다.</summary>
    public float GetRangeDamageAP() => NonNegative(unit_Stat.rangeDamageAP);
    public void AddRangeDamageAP(float v) => unit_Stat.rangeDamageAP += v;

    public float GetRangeAttackSpeed() => NonNegative(unit_Stat.rangeAttackSpeed);
    public void AddRangeAttackSpeed(float v) => unit_Stat.rangeAttackSpeed += v;

    public float GetRangeDiffense() => NonNegative(unit_Stat.rangeDiffense);
    public void AddRangeDiffense(float v) => unit_Stat.rangeDiffense += v;

    public float GetRangeAccuracy() => NonNegative(unit_Stat.rangeAccuracy);
    public void AddRangeAccuracy(float v) => unit_Stat.rangeAccuracy += v;

    public float GetRangeRange() => NonNegative(unit_Stat.rangeRange);
    public void AddRangeRange(float v) => unit_Stat.rangeRange += v;

    /// <summary>유닛 1인의 총 탄약 수입니다. 0 이하이면 무한입니다.</summary>
    public int GetAmmunition() => unit_Stat.ammunition;

    /// <summary>탄약이 유한한지 여부입니다. 0 이하로 설정하면 무한 사격입니다.</summary>
    public bool IsAmmunitionLimited() => unit_Stat.ammunition > 0;

    // 공격 지연
    public float GetAttackDelay() => NonNegative(unit_Stat.attackDelay);
    public void AddAttackDelay(float v) => unit_Stat.attackDelay += v;

    // 크기 / 충돌
    public float GetSize() => NonNegative(unit_Stat.size);
    public void AddSize(float v) => unit_Stat.size += v;

    public float GetInterval() => NonNegative(unit_Stat.interval);
    public void AddInterval(float v) => unit_Stat.interval += v;

    public float GetRadius() => NonNegative(unit_Stat.radius);
    public float GetHeight() => NonNegative(unit_Stat.height);

    // 체력
    public float GetHP() => NonNegative(unit_Stat.HP);
    public void AddHP(float v) => unit_Stat.HP += v;

    // 공격 타입
    public E_Unit_AttackType GetE_Unit_AttackType() => unit_Stat.e_Unit_AttackType;

    // 병종 / 상성
    /// <summary>이 부대의 병종 분류입니다.</summary>
    public E_Unit_Class GetE_Unit_Class() => unit_Stat.e_Unit_Class;

    /// <summary>이 부대가 대형으로 취급되는지 여부입니다.</summary>
    public bool IsLarge() => Unit_Class_Util.IsLarge(unit_Stat.e_Unit_Class);

    public float GetBonusVsLarge() => NonNegative(unit_Stat.bonusVsLarge);
    public void AddBonusVsLarge(float v) => unit_Stat.bonusVsLarge += v;

    public float GetBonusVsInfantry() => NonNegative(unit_Stat.bonusVsInfantry);
    public void AddBonusVsInfantry(float v) => unit_Stat.bonusVsInfantry += v;

    /// <summary>
    /// 상대 부대를 향한 상성 명중 보너스입니다.
    /// 명중 공식의 bonus_vs_target 항에 들어갑니다.
    /// </summary>
    public float GetBonusVsTarget(in Army_Data target)
    {
        return target.IsLarge()
            ? GetBonusVsLarge()
            : GetBonusVsInfantry();
    }
}
