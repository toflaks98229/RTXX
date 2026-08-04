using UnityEngine;

/// <summary>
/// 시뮬레이션 사양 상수와 밸런스 수치에 대한 접근점입니다.
///
/// 두 부류가 명확히 구분됩니다.
///
/// 1) 시뮬레이션 사양 (const로 유지)
///    deltaTime, targetFrameRate처럼 시뮬레이션의 시간 척도를 정의하는 값입니다.
///    런타임에 바뀌면 부대(메인 스레드)와 유닛(Job)의 시간이 어긋나 시뮬레이션이
///    깨지므로, 반드시 컴파일 타임 상수여야 합니다.
///
/// 2) 게임 밸런스 (Balance로 위임)
///    사기, 피로, 태세, 돌격, 지형 등 '조정 대상'인 수치입니다.
///    Balance_Data 구조체에 담겨 Balance(SharedStatic)를 통해 제공되며,
///    Balance_Config 에셋으로 플레이 중에도 수정할 수 있습니다.
///
/// 아래 밸런스 프로퍼티들은 기존 호출부(Constant.morale_Max 등)를 그대로 두기 위한
/// 얇은 전달자입니다. 전부 인라인되므로 const였을 때와 성능 차이는 실질적으로 없고,
/// Burst Job 안에서도 동일하게 동작합니다.
/// </summary>
static class Constant
{
    // =====================================================================
    // 1) 시뮬레이션 사양 — 진짜 상수. 밸런싱 대상이 아닙니다.
    // =====================================================================

    /// <summary>
    /// 시뮬레이션 고정 틱 간격입니다.
    /// Burst Job 내부에서는 Time.fixedDeltaTime에 접근할 수 없으므로 const로 유지하고,
    /// 대신 Controller.Awake()에서 Time.fixedDeltaTime을 이 값으로 맞춥니다.
    /// 두 값이 어긋나면 부대(메인 스레드)와 유닛(Job)의 시간 척도가 달라집니다.
    /// </summary>
    public const float deltaTime = 1.0f / 60.0f;

    /// <summary>목표 프레임 레이트입니다. deltaTime과 반드시 일치해야 합니다.</summary>
    public const int targetFrameRate = 60;

    /// <summary>
    /// IJobParallelFor의 배치 크기(innerloopBatchCount)입니다.
    ///
    /// 워커 스레드가 한 번에 가져가는 작업 단위입니다.
    /// 1이면 유닛 하나마다 스케줄링이 일어나 그 오버헤드가 실제 계산량을
    /// 넘어섭니다. 유닛당 작업이 짧은 이 프로젝트에서는 수십 단위로 묶는 편이
    /// 훨씬 유리합니다. (Unit_Fight_Job은 원래부터 32를 쓰고 있었습니다)
    ///
    /// 밸런스가 아니라 실행 성능에 관한 값이므로 Balance가 아닌 여기에 둡니다.
    /// </summary>
    public const int jobBatchCount = 32;

    // =====================================================================
    // 2) 게임 밸런스 — Balance_Config 에셋으로 조정합니다.
    //    각 항목의 의미와 기본값은 Balance_Data.cs에 문서화되어 있습니다.
    // =====================================================================

    // --- 이동 / 진형 일반 ---
    /// <inheritdoc cref="Balance_Data.speed_Walk"/>
    public static float speed_Walk => Balance.Data.speed_Walk;
    /// <inheritdoc cref="Balance_Data.distance_Stop"/>
    public static float distance_Stop => Balance.Data.distance_Stop;
    /// <inheritdoc cref="Balance_Data.distance_ReFormation"/>
    public static float distance_ReFormation => Balance.Data.distance_ReFormation;
    /// <inheritdoc cref="Balance_Data.distance_Move_FowardVector"/>
    public static float distance_Move_FowardVector => Balance.Data.distance_Move_FowardVector;
    /// <inheritdoc cref="Balance_Data.distance_Detect"/>
    public static float distance_Detect => Balance.Data.distance_Detect;
    /// <inheritdoc cref="Balance_Data.distance_Formation"/>
    public static float distance_Formation => Balance.Data.distance_Formation;
    /// <inheritdoc cref="Balance_Data.distance_Attack_Move"/>
    public static float distance_Attack_Move => Balance.Data.distance_Attack_Move;
    /// <inheritdoc cref="Balance_Data.time_Reformation"/>
    public static float time_Reformation => Balance.Data.time_Reformation;
    /// <inheritdoc cref="Balance_Data.time_ReTarget"/>
    public static float time_ReTarget => Balance.Data.time_ReTarget;
    /// <inheritdoc cref="Balance_Data.move_Speed_Target"/>
    public static float move_Speed_Target => Balance.Data.move_Speed_Target;
    /// <inheritdoc cref="Balance_Data.distance_Speed_Target_Full"/>
    public static float distance_Speed_Target_Full => Balance.Data.distance_Speed_Target_Full;
    /// <inheritdoc cref="Balance_Data.move_Speed_Stop"/>
    public static float move_Speed_Stop => Balance.Data.move_Speed_Stop;
    /// <inheritdoc cref="Balance_Data.time_beingAttackedDelay"/>
    public static float time_beingAttackedDelay => Balance.Data.time_beingAttackedDelay;
    /// <inheritdoc cref="Balance_Data.Army_Move_Rate"/>
    public static float Army_Move_Rate => Balance.Data.Army_Move_Rate;
    /// <inheritdoc cref="Balance_Data.Army_Move_Slow_Rate"/>
    public static float Army_Move_Slow_Rate => Balance.Data.Army_Move_Slow_Rate;
    /// <inheritdoc cref="Balance_Data.Army_ReFormation_Rate"/>
    public static float Army_ReFormation_Rate => Balance.Data.Army_ReFormation_Rate;

    // --- 전투 판정 ---
    /// <inheritdoc cref="Balance_Data.hit_Chance_Base"/>
    public static float hit_Chance_Base => Balance.Data.hit_Chance_Base;
    /// <inheritdoc cref="Balance_Data.hit_Chance_Min"/>
    public static float hit_Chance_Min => Balance.Data.hit_Chance_Min;
    /// <inheritdoc cref="Balance_Data.hit_Chance_Max"/>
    public static float hit_Chance_Max => Balance.Data.hit_Chance_Max;
    /// <inheritdoc cref="Balance_Data.damage_Min"/>
    public static float damage_Min => Balance.Data.damage_Min;

    // --- 방어구 ---
    /// <inheritdoc cref="Balance_Data.armour_Full_Block"/>
    public static float armour_Full_Block => Balance.Data.armour_Full_Block;
    /// <inheritdoc cref="Balance_Data.armour_Roll_Min_Rate"/>
    public static float armour_Roll_Min_Rate => Balance.Data.armour_Roll_Min_Rate;

    // --- 피격 시 밀림(넉백) ---
    /// <inheritdoc cref="Balance_Data.knockback_Per_Damage"/>
    public static float knockback_Per_Damage => Balance.Data.knockback_Per_Damage;
    /// <inheritdoc cref="Balance_Data.knockback_Impulse_Max"/>
    public static float knockback_Impulse_Max => Balance.Data.knockback_Impulse_Max;

    // --- 전열 유지 ---
    /// <inheritdoc cref="Balance_Data.distance_Engage_Leash"/>
    public static float distance_Engage_Leash => Balance.Data.distance_Engage_Leash;
    /// <inheritdoc cref="Balance_Data.engage_Block_Rate"/>
    public static float engage_Block_Rate => Balance.Data.engage_Block_Rate;
    /// <inheritdoc cref="Balance_Data.engage_Push_Rate"/>
    public static float engage_Push_Rate => Balance.Data.engage_Push_Rate;
    /// <inheritdoc cref="Balance_Data.target_Front_Bias"/>
    public static float target_Front_Bias => Balance.Data.target_Front_Bias;

    // --- 교전 중 보행 ---
    /// <inheritdoc cref="Balance_Data.distance_Combat_Footwork"/>
    public static float distance_Combat_Footwork => Balance.Data.distance_Combat_Footwork;
    /// <inheritdoc cref="Balance_Data.dot_Forward_Walk"/>
    public static float dot_Forward_Walk => Balance.Data.dot_Forward_Walk;
    /// <inheritdoc cref="Balance_Data.dot_Side_Walk"/>
    public static float dot_Side_Walk => Balance.Data.dot_Side_Walk;
    /// <inheritdoc cref="Balance_Data.speed_Rate_Sidestep"/>
    public static float speed_Rate_Sidestep => Balance.Data.speed_Rate_Sidestep;
    /// <inheritdoc cref="Balance_Data.speed_Rate_Backpedal"/>
    public static float speed_Rate_Backpedal => Balance.Data.speed_Rate_Backpedal;
    /// <inheritdoc cref="Balance_Data.reform_Speed_Rate"/>
    public static float reform_Speed_Rate => Balance.Data.reform_Speed_Rate;
    /// <inheritdoc cref="Balance_Data.army_Blocked_Rate"/>
    public static float army_Blocked_Rate => Balance.Data.army_Blocked_Rate;
    /// <inheritdoc cref="Balance_Data.distance_Front_Block_Ray"/>
    public static float distance_Front_Block_Ray => Balance.Data.distance_Front_Block_Ray;
    /// <inheritdoc cref="Balance_Data.blocked_Accelerate_Rate"/>
    public static float blocked_Accelerate_Rate => Balance.Data.blocked_Accelerate_Rate;

    // --- 돌격 ---
    /// <inheritdoc cref="Balance_Data.time_Charge_Bonus"/>
    public static float time_Charge_Bonus => Balance.Data.time_Charge_Bonus;
    /// <inheritdoc cref="Balance_Data.charge_Min_Speed_Rate"/>
    public static float charge_Min_Speed_Rate => Balance.Data.charge_Min_Speed_Rate;
    /// <inheritdoc cref="Balance_Data.charge_Facing_Dot"/>
    public static float charge_Facing_Dot => Balance.Data.charge_Facing_Dot;
    /// <inheritdoc cref="Balance_Data.charge_Min_Distance_Rate"/>
    public static float charge_Min_Distance_Rate => Balance.Data.charge_Min_Distance_Rate;
    /// <inheritdoc cref="Balance_Data.charge_Trigger_Rate"/>
    public static float charge_Trigger_Rate => Balance.Data.charge_Trigger_Rate;
    /// <inheritdoc cref="Balance_Data.charge_Contact_Skin"/>
    public static float charge_Contact_Skin => Balance.Data.charge_Contact_Skin;
    /// <inheritdoc cref="Balance_Data.charge_Max_Penetration_Rate"/>
    public static float charge_Max_Penetration_Rate => Balance.Data.charge_Max_Penetration_Rate;

    // --- 충돌 공격 ---
    /// <inheritdoc cref="Balance_Data.collision_Damage_Base"/>
    public static float collision_Damage_Base => Balance.Data.collision_Damage_Base;
    /// <inheritdoc cref="Balance_Data.collision_Mass_Full_Ratio"/>
    public static float collision_Mass_Full_Ratio => Balance.Data.collision_Mass_Full_Ratio;
    /// <inheritdoc cref="Balance_Data.collision_Knockback_Impulse"/>
    public static float collision_Knockback_Impulse => Balance.Data.collision_Knockback_Impulse;
    /// <inheritdoc cref="Balance_Data.large_Push_Through_Rate"/>
    public static float large_Push_Through_Rate => Balance.Data.large_Push_Through_Rate;

    // --- 지휘 (장군) ---
    /// <inheritdoc cref="Balance_Data.general_Aura_Radius"/>
    public static float general_Aura_Radius => Balance.Data.general_Aura_Radius;
    /// <inheritdoc cref="Balance_Data.general_Aura_Morale"/>
    public static float general_Aura_Morale => Balance.Data.general_Aura_Morale;
    /// <inheritdoc cref="Balance_Data.general_Death_Shock"/>
    public static float general_Death_Shock => Balance.Data.general_Death_Shock;

    // --- 지형 ---
    /// <inheritdoc cref="Balance_Data.terrain_Height_Full"/>
    public static float terrain_Height_Full => Balance.Data.terrain_Height_Full;
    /// <inheritdoc cref="Balance_Data.terrain_High_Ground_Attack"/>
    public static float terrain_High_Ground_Attack => Balance.Data.terrain_High_Ground_Attack;
    /// <inheritdoc cref="Balance_Data.terrain_High_Ground_Morale"/>
    public static float terrain_High_Ground_Morale => Balance.Data.terrain_High_Ground_Morale;
    /// <inheritdoc cref="Balance_Data.terrain_High_Ground_Range_Rate"/>
    public static float terrain_High_Ground_Range_Rate => Balance.Data.terrain_High_Ground_Range_Rate;
    /// <inheritdoc cref="Balance_Data.terrain_Slope_Speed_Penalty"/>
    public static float terrain_Slope_Speed_Penalty => Balance.Data.terrain_Slope_Speed_Penalty;
    /// <inheritdoc cref="Balance_Data.terrain_Slope_Full_Angle"/>
    public static float terrain_Slope_Full_Angle => Balance.Data.terrain_Slope_Full_Angle;

    // --- 태세 ---
    /// <inheritdoc cref="Balance_Data.stance_Wall_Speed_Rate"/>
    public static float stance_Wall_Speed_Rate => Balance.Data.stance_Wall_Speed_Rate;
    /// <inheritdoc cref="Balance_Data.stance_ShieldWall_Range_Defence"/>
    public static float stance_ShieldWall_Range_Defence => Balance.Data.stance_ShieldWall_Range_Defence;
    /// <inheritdoc cref="Balance_Data.stance_ShieldWall_Charge_Resist"/>
    public static float stance_ShieldWall_Charge_Resist => Balance.Data.stance_ShieldWall_Charge_Resist;
    /// <inheritdoc cref="Balance_Data.stance_SpearWall_Melee_Defence"/>
    public static float stance_SpearWall_Melee_Defence => Balance.Data.stance_SpearWall_Melee_Defence;
    /// <inheritdoc cref="Balance_Data.stance_SpearWall_Charge_Resist"/>
    public static float stance_SpearWall_Charge_Resist => Balance.Data.stance_SpearWall_Charge_Resist;
    /// <inheritdoc cref="Balance_Data.stance_SpearWall_Range_Penalty"/>
    public static float stance_SpearWall_Range_Penalty => Balance.Data.stance_SpearWall_Range_Penalty;
    /// <inheritdoc cref="Balance_Data.stance_Front_Dot"/>
    public static float stance_Front_Dot => Balance.Data.stance_Front_Dot;
    /// <inheritdoc cref="Balance_Data.stance_SpearWall_Reflect_Shock"/>
    public static float stance_SpearWall_Reflect_Shock => Balance.Data.stance_SpearWall_Reflect_Shock;
    /// <inheritdoc cref="Balance_Data.stance_Skirmish_Flee_Distance"/>
    public static float stance_Skirmish_Flee_Distance => Balance.Data.stance_Skirmish_Flee_Distance;
    /// <inheritdoc cref="Balance_Data.stance_Skirmish_Flee_Rate"/>
    public static float stance_Skirmish_Flee_Rate => Balance.Data.stance_Skirmish_Flee_Rate;

    // 태세별 밀집도
    /// <inheritdoc cref="Balance_Data.density_Line"/>
    public static float density_Line => Balance.Data.density_Line;
    /// <inheritdoc cref="Balance_Data.density_Loose"/>
    public static float density_Loose => Balance.Data.density_Loose;
    /// <inheritdoc cref="Balance_Data.density_ShieldWall"/>
    public static float density_ShieldWall => Balance.Data.density_ShieldWall;
    /// <inheritdoc cref="Balance_Data.density_SpearWall"/>
    public static float density_SpearWall => Balance.Data.density_SpearWall;
    /// <inheritdoc cref="Balance_Data.density_Skirmish"/>
    public static float density_Skirmish => Balance.Data.density_Skirmish;
    /// <inheritdoc cref="Balance_Data.density_Min_Radius_Rate"/>
    public static float density_Min_Radius_Rate => Balance.Data.density_Min_Radius_Rate;

    // 적진 관통 방지
    /// <inheritdoc cref="Balance_Data.collide_Enemy_Separation_Rate"/>
    public static float collide_Enemy_Separation_Rate => Balance.Data.collide_Enemy_Separation_Rate;
    /// <inheritdoc cref="Balance_Data.collide_Enemy_Radius_Rate"/>
    public static float collide_Enemy_Radius_Rate => Balance.Data.collide_Enemy_Radius_Rate;
    /// <inheritdoc cref="Balance_Data.collide_Push_Through_Mass_Rate"/>
    public static float collide_Push_Through_Mass_Rate => Balance.Data.collide_Push_Through_Mass_Rate;
    /// <inheritdoc cref="Balance_Data.collide_Push_Through_Block_Rate"/>
    public static float collide_Push_Through_Block_Rate => Balance.Data.collide_Push_Through_Block_Rate;

    // 발밑 마커
    /// <inheritdoc cref="Balance_Data.marker_Ground_Lift"/>
    public static float marker_Ground_Lift => Balance.Data.marker_Ground_Lift;
    /// <inheritdoc cref="Balance_Data.marker_Ground_Lift_Rate"/>
    public static float marker_Ground_Lift_Rate => Balance.Data.marker_Ground_Lift_Rate;

    // --- 근접 피격 방향 판정 ---
    // 이 게임의 핵심 전술 규칙(측면을 치면 유리하다)의 실체입니다.
    // 기존에는 Unit_Job.cs에 각도가 직접 박혀 조정할 수 없었습니다.
    /// <inheritdoc cref="Balance_Data.hit_Angle_Front"/>
    public static float hit_Angle_Front => Balance.Data.hit_Angle_Front;
    /// <inheritdoc cref="Balance_Data.hit_Angle_Back"/>
    public static float hit_Angle_Back => Balance.Data.hit_Angle_Back;
    /// <inheritdoc cref="Balance_Data.hit_Side_Defence_Rate"/>
    public static float hit_Side_Defence_Rate => Balance.Data.hit_Side_Defence_Rate;

    // --- 사기 충격 ---
    /// <inheritdoc cref="Balance_Data.morale_Shock_Charge"/>
    public static float morale_Shock_Charge => Balance.Data.morale_Shock_Charge;
    /// <inheritdoc cref="Balance_Data.morale_Shock_Flank_Rate"/>
    public static float morale_Shock_Flank_Rate => Balance.Data.morale_Shock_Flank_Rate;
    /// <inheritdoc cref="Balance_Data.morale_Shock_Recover"/>
    public static float morale_Shock_Recover => Balance.Data.morale_Shock_Recover;

    // --- 피격 시각 피드백 ---
    /// <inheritdoc cref="Balance_Data.time_Hit_Flash"/>
    public static float time_Hit_Flash => Balance.Data.time_Hit_Flash;
    /// <inheritdoc cref="Balance_Data.time_Charge_Flash"/>
    public static float time_Charge_Flash => Balance.Data.time_Charge_Flash;
    /// <inheritdoc cref="Balance_Data.hit_Flash_Full_Damage"/>
    public static float hit_Flash_Full_Damage => Balance.Data.hit_Flash_Full_Damage;
    /// <inheritdoc cref="Balance_Data.hit_Flash_Punch_Scale"/>
    public static float hit_Flash_Punch_Scale => Balance.Data.hit_Flash_Punch_Scale;
    /// <inheritdoc cref="Balance_Data.charge_Flash_Punch_Rate"/>
    public static float charge_Flash_Punch_Rate => Balance.Data.charge_Flash_Punch_Rate;
    /// <inheritdoc cref="Balance_Data.charge_Flash_Radius_Rate"/>
    public static float charge_Flash_Radius_Rate => Balance.Data.charge_Flash_Radius_Rate;

    // --- 공격 모션 ---
    /// <inheritdoc cref="Balance_Data.attack_Lunge_Distance"/>
    public static float attack_Lunge_Distance => Balance.Data.attack_Lunge_Distance;
    /// <inheritdoc cref="Balance_Data.attack_Lunge_Time"/>
    public static float attack_Lunge_Time => Balance.Data.attack_Lunge_Time;

    // --- 피로도 ---
    /// <inheritdoc cref="Balance_Data.fatigue_Max"/>
    public static float fatigue_Max => Balance.Data.fatigue_Max;
    /// <inheritdoc cref="Balance_Data.fatigue_Gain_Charge"/>
    public static float fatigue_Gain_Charge => Balance.Data.fatigue_Gain_Charge;
    /// <inheritdoc cref="Balance_Data.fatigue_Gain_Rout"/>
    public static float fatigue_Gain_Rout => Balance.Data.fatigue_Gain_Rout;
    /// <inheritdoc cref="Balance_Data.fatigue_Gain_Melee"/>
    public static float fatigue_Gain_Melee => Balance.Data.fatigue_Gain_Melee;
    /// <inheritdoc cref="Balance_Data.fatigue_Gain_Move"/>
    public static float fatigue_Gain_Move => Balance.Data.fatigue_Gain_Move;
    /// <inheritdoc cref="Balance_Data.fatigue_Recover"/>
    public static float fatigue_Recover => Balance.Data.fatigue_Recover;
    /// <inheritdoc cref="Balance_Data.fatigue_Min_Rate"/>
    public static float fatigue_Min_Rate => Balance.Data.fatigue_Min_Rate;
    /// <inheritdoc cref="Balance_Data.fatigue_Morale_Penalty"/>
    public static float fatigue_Morale_Penalty => Balance.Data.fatigue_Morale_Penalty;
    /// <inheritdoc cref="Balance_Data.fatigue_Active"/>
    public static float fatigue_Active => Balance.Data.fatigue_Active;
    /// <inheritdoc cref="Balance_Data.fatigue_Winded"/>
    public static float fatigue_Winded => Balance.Data.fatigue_Winded;
    /// <inheritdoc cref="Balance_Data.fatigue_Tired"/>
    public static float fatigue_Tired => Balance.Data.fatigue_Tired;
    /// <inheritdoc cref="Balance_Data.fatigue_VeryTired"/>
    public static float fatigue_VeryTired => Balance.Data.fatigue_VeryTired;
    /// <inheritdoc cref="Balance_Data.fatigue_Exhausted"/>
    public static float fatigue_Exhausted => Balance.Data.fatigue_Exhausted;

    // --- 사기 ---
    /// <inheritdoc cref="Balance_Data.morale_Max"/>
    public static float morale_Max => Balance.Data.morale_Max;
    /// <inheritdoc cref="Balance_Data.morale_Drift_Rate"/>
    public static float morale_Drift_Rate => Balance.Data.morale_Drift_Rate;
    /// <inheritdoc cref="Balance_Data.morale_Penalty_Casualty"/>
    public static float morale_Penalty_Casualty => Balance.Data.morale_Penalty_Casualty;
    /// <inheritdoc cref="Balance_Data.morale_Penalty_Surrounded"/>
    public static float morale_Penalty_Surrounded => Balance.Data.morale_Penalty_Surrounded;
    /// <inheritdoc cref="Balance_Data.morale_Bonus_Flag"/>
    public static float morale_Bonus_Flag => Balance.Data.morale_Bonus_Flag;
    /// <inheritdoc cref="Balance_Data.morale_Penalty_Outnumbered"/>
    public static float morale_Penalty_Outnumbered => Balance.Data.morale_Penalty_Outnumbered;
    /// <inheritdoc cref="Balance_Data.morale_Outnumbered_Full_Ratio"/>
    public static float morale_Outnumbered_Full_Ratio => Balance.Data.morale_Outnumbered_Full_Ratio;
    /// <inheritdoc cref="Balance_Data.morale_Shock_Allied_Rout"/>
    public static float morale_Shock_Allied_Rout => Balance.Data.morale_Shock_Allied_Rout;
    /// <inheritdoc cref="Balance_Data.morale_Allied_Rout_Radius"/>
    public static float morale_Allied_Rout_Radius => Balance.Data.morale_Allied_Rout_Radius;
    /// <inheritdoc cref="Balance_Data.morale_Bonus_Winning"/>
    public static float morale_Bonus_Winning => Balance.Data.morale_Bonus_Winning;
    /// <inheritdoc cref="Balance_Data.morale_Shatter_Loss_Rate"/>
    public static float morale_Shatter_Loss_Rate => Balance.Data.morale_Shatter_Loss_Rate;
    /// <inheritdoc cref="Balance_Data.morale_Rout_Threshold"/>
    public static float morale_Rout_Threshold => Balance.Data.morale_Rout_Threshold;
    /// <inheritdoc cref="Balance_Data.morale_Waver_Threshold"/>
    public static float morale_Waver_Threshold => Balance.Data.morale_Waver_Threshold;
    /// <inheritdoc cref="Balance_Data.morale_Rally_Threshold"/>
    public static float morale_Rally_Threshold => Balance.Data.morale_Rally_Threshold;
    /// <inheritdoc cref="Balance_Data.time_Rally"/>
    public static float time_Rally => Balance.Data.time_Rally;
    /// <inheritdoc cref="Balance_Data.rout_Speed_Rate"/>
    public static float rout_Speed_Rate => Balance.Data.rout_Speed_Rate;
}
