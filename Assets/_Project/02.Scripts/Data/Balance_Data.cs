using System;

/// <summary>
/// 게임 밸런스 수치 전체를 담는 순수 값 구조체입니다.
///
/// 왜 struct인가:
/// 이 값들은 Burst Job 안(Unit_Fight_Job, Army_Data._Update 등)에서 읽힙니다.
/// Burst는 관리 객체(class, ScriptableObject)에 접근할 수 없으므로,
/// 반드시 참조 타입을 담지 않는 blittable 구조체여야 합니다.
///
/// 왜 Constant에서 분리했는가:
/// 기존에는 전부 const였습니다. const는 컴파일 타임에 호출부로 인라인되므로
/// 수치 하나를 바꾸려면 재컴파일(+ Burst 재컴파일)이 필요했습니다.
/// 대규모 전투 게임의 개발 시간 대부분이 이 수치를 만지는 데 들어가는데,
/// 가장 자주 하는 일이 가장 느린 경로에 막혀 있었습니다.
///
/// 여기로 옮긴 값은 Balance_Config(ScriptableObject)에서 편집하고
/// Balance.Apply()로 주입하면 플레이 중에도 즉시 반영됩니다.
///
/// 무엇이 여기 없는가:
/// deltaTime, targetFrameRate처럼 '시뮬레이션 사양'에 해당하는 값은
/// Constant에 const로 남아 있습니다. 그 값들은 밸런싱 대상이 아니며
/// 런타임에 바뀌면 시간 척도가 어긋나 시뮬레이션이 깨집니다.
/// </summary>
[Serializable]
public struct Balance_Data
{
    // ---------------------------------------------------------------------
    // 전투 판정
    // ---------------------------------------------------------------------
    /// <summary>명중 확률의 기본값(%)입니다. 공격력과 방어력이 같으면 이 값이 그대로 명중률입니다.</summary>
    public float hit_Chance_Base;
    /// <summary>명중 확률의 하한(%)입니다. 절대 무적을 막는 장치입니다.</summary>
    public float hit_Chance_Min;
    /// <summary>명중 확률의 상한(%)입니다. 확정타를 막는 장치입니다.</summary>
    public float hit_Chance_Max;
    /// <summary>방어구가 공격력을 상회해도 보장되는 최소 피해량입니다.</summary>
    public float damage_Min;

    // ---------------------------------------------------------------------
    // 방어구
    // ---------------------------------------------------------------------
    /// <summary>기본 피해를 100% 막아내는 방어구 수치입니다.</summary>
    public float armour_Full_Block;
    /// <summary>방어구 굴림의 하한 배율입니다. rand(방어구 x 이 값, 방어구)로 굴립니다.</summary>
    public float armour_Roll_Min_Rate;

    // ---------------------------------------------------------------------
    // 피격 시 밀림(넉백)
    // ---------------------------------------------------------------------
    /// <summary>피해량 1당 가해지는 충격량입니다.</summary>
    public float knockback_Per_Damage;
    /// <summary>단일 타격이 가할 수 있는 최대 충격량입니다.</summary>
    public float knockback_Impulse_Max;

    // ---------------------------------------------------------------------
    // 전열 유지
    // ---------------------------------------------------------------------
    /// <summary>교전 중인 유닛이 진형 슬롯에서 벗어날 수 있는 최대 거리입니다.</summary>
    public float distance_Engage_Leash;
    /// <summary>적과 접촉했을 때 남는 전진 속도 비율입니다.</summary>
    public float engage_Block_Rate;
    /// <summary>교전 중 아군을 옆으로 밀어내는 힘의 비율입니다.</summary>
    public float engage_Push_Rate;
    /// <summary>타겟 선정 시 '정면'을 얼마나 우선할지 결정하는 가중치입니다.</summary>
    public float target_Front_Bias;

    // ---------------------------------------------------------------------
    // 교전 중 보행
    // ---------------------------------------------------------------------
    /// <summary>교전 중 '적을 바라본 채' 이동할 최대 거리입니다.</summary>
    public float distance_Combat_Footwork;
    /// <summary>정면 보행으로 판정하는 내적 하한입니다. (약 45도 이내)</summary>
    public float dot_Forward_Walk;
    /// <summary>옆걸음으로 판정하는 내적 하한입니다. 이보다 낮으면 뒷걸음입니다.</summary>
    public float dot_Side_Walk;
    /// <summary>옆걸음 속도 배율입니다.</summary>
    public float speed_Rate_Sidestep;
    /// <summary>뒷걸음 속도 배율입니다. 가장 느립니다.</summary>
    public float speed_Rate_Backpedal;
    /// <summary>교전 중 대열을 메우러 이동할 때의 속도 배율입니다.</summary>
    public float reform_Speed_Rate;
    /// <summary>이 비율 이상의 유닛이 막히면 부대 기준점도 전진을 멈춥니다.</summary>
    public float army_Blocked_Rate;
    /// <summary>유닛이 전방의 교전을 감지하는 레이캐스트 거리입니다.</summary>
    public float distance_Front_Block_Ray;
    /// <summary>적과 접촉한 상태에서 남는 가속 비율입니다.</summary>
    public float blocked_Accelerate_Rate;

    // ---------------------------------------------------------------------
    // 돌격
    // ---------------------------------------------------------------------
    /// <summary>돌격 보너스가 완전히 사라지기까지의 시간(초)입니다.</summary>
    public float time_Charge_Bonus;
    /// <summary>돌격 충격이 인정되는 최소 속도 비율입니다.</summary>
    public float charge_Min_Speed_Rate;
    /// <summary>돌격이 성립하려면 이동 방향이 적 쪽이어야 하는 내적 하한입니다.</summary>
    public float charge_Facing_Dot;
    /// <summary>돌격에 필요한 최소 거리입니다. (돌격 사거리에 대한 비율)</summary>
    public float charge_Min_Distance_Rate;
    /// <summary>돌격이 자동으로 시작되는 거리 배율입니다.</summary>
    public float charge_Trigger_Rate;
    /// <summary>돌격이 멈춰야 할 접촉면까지의 여유 거리입니다.</summary>
    public float charge_Contact_Skin;
    /// <summary>한 틱에 허용되는 최대 진입 깊이(상대 반지름에 대한 비율)입니다.</summary>
    public float charge_Max_Penetration_Rate;

    // ---------------------------------------------------------------------
    // 충돌 공격
    // ---------------------------------------------------------------------
    /// <summary>돌격 충돌 순간 몸으로 가하는 기본 피해입니다.</summary>
    public float collision_Damage_Base;
    /// <summary>충돌 피해가 최대에 도달하는 질량비입니다.</summary>
    public float collision_Mass_Full_Ratio;
    /// <summary>충돌로 밀려나는 충격량입니다.</summary>
    public float collision_Knockback_Impulse;
    /// <summary>대형 유닛이 접촉만으로 밀어붙일 수 있는 속도 비율입니다.</summary>
    public float large_Push_Through_Rate;

    // ---------------------------------------------------------------------
    // 지휘 (장군)
    // ---------------------------------------------------------------------
    /// <summary>장군 오라가 미치는 반경입니다.</summary>
    public float general_Aura_Radius;
    /// <summary>장군 근처에서 받는 최대 사기 보너스입니다.</summary>
    public float general_Aura_Morale;
    /// <summary>장군이 전사했을 때 아군 전체가 받는 사기 충격입니다.</summary>
    public float general_Death_Shock;

    // ---------------------------------------------------------------------
    // 지형
    // ---------------------------------------------------------------------
    /// <summary>고지 우위가 최대에 도달하는 높이 차(m)입니다.</summary>
    public float terrain_Height_Full;
    /// <summary>고지에서 얻는 최대 명중 보너스입니다.</summary>
    public float terrain_High_Ground_Attack;
    /// <summary>고지에서 얻는 최대 사기 보너스입니다.</summary>
    public float terrain_High_Ground_Morale;
    /// <summary>원거리 사거리에 대한 고지 보정 배율입니다.</summary>
    public float terrain_High_Ground_Range_Rate;
    /// <summary>오르막을 오를 때 최대로 깎이는 이동 속도 비율입니다.</summary>
    public float terrain_Slope_Speed_Penalty;
    /// <summary>속도 페널티가 최대에 도달하는 경사(도)입니다.</summary>
    public float terrain_Slope_Full_Angle;

    // ---------------------------------------------------------------------
    // 태세
    // ---------------------------------------------------------------------
    /// <summary>방패벽/창벽 태세에서의 이동 속도 배율입니다.</summary>
    public float stance_Wall_Speed_Rate;
    /// <summary>방패벽의 원거리 방어 보너스입니다.</summary>
    public float stance_ShieldWall_Range_Defence;
    /// <summary>방패벽의 돌격 저항입니다.</summary>
    public float stance_ShieldWall_Charge_Resist;
    /// <summary>창벽의 근접 방어 보너스입니다.</summary>
    public float stance_SpearWall_Melee_Defence;
    /// <summary>창벽의 돌격 저항입니다. 1.0이면 정면 돌격 보너스를 완전 무효화합니다.</summary>
    public float stance_SpearWall_Charge_Resist;
    /// <summary>창벽의 원거리 취약 페널티입니다.</summary>
    public float stance_SpearWall_Range_Penalty;
    /// <summary>돌격 저항이 정면으로 인정되는 내적 하한입니다.</summary>
    public float stance_Front_Dot;
    /// <summary>창벽이 돌격을 반사할 때 돌격한 쪽이 받는 사기 충격입니다.</summary>
    public float stance_SpearWall_Reflect_Shock;
    /// <summary>산개 태세가 후퇴를 시작하는 거리입니다.</summary>
    public float stance_Skirmish_Flee_Distance;
    /// <summary>산개 후퇴 속도 배율입니다.</summary>
    public float stance_Skirmish_Flee_Rate;

    // ---------------------------------------------------------------------
    // 태세별 밀집도 (병사 간격 배율)
    //
    // 왜 필요한가:
    // 이 게임은 이미 태세에 따라 원거리 방어·돌격 저항·이동 속도를
    // 다르게 계산하고 있었습니다. 그런데 **화면에서는 늘 같은 간격**으로
    // 서 있었습니다. 방패벽을 세워도 그림이 그대로라 플레이어는 무엇이
    // 달라졌는지 볼 수 없었습니다.
    //
    // 토탈워(삼국지 포함)의 설계를 조사해 보면 밀집도가 그 거래의
    // 몸통입니다.
    //
    //   밀집  : 화살에 잘 맞음 / 돌격에 잘 버팀 / 느림
    //   산개  : 화살을 잘 피함 / 돌격에 무너짐 / 빠름
    //
    // Three Kingdoms의 Loose는 '유닛의 물리적 점유 면적을 넓혀' 원거리
    // 회피를 얻는 대신 돌격 저항을 크게 잃습니다. 방패벽(Turtle)은
    // 반대로 방패를 겹쳐 틈을 없앱니다.
    //
    // 아래 값은 그 관계를 간격에 반영합니다. 수치 효과는 이미 있으므로
    // 여기서는 '보이는 것'만 맞춥니다.
    // ---------------------------------------------------------------------
    /// <summary>전열(Line) 태세의 간격 배율입니다. 기준값이므로 1.0입니다.</summary>
    public float density_Line;
    /// <summary>느슨한 대열(Loose)의 간격 배율입니다. 넓게 벌어집니다.</summary>
    public float density_Loose;
    /// <summary>방패벽의 간격 배율입니다. 가장 조밀합니다.</summary>
    public float density_ShieldWall;
    /// <summary>창벽의 간격 배율입니다. 방패벽보다는 덜 조입니다.</summary>
    public float density_SpearWall;
    /// <summary>산개(Skirmish)의 간격 배율입니다. 가장 넓게 흩어집니다.</summary>
    public float density_Skirmish;

    /// <summary>
    /// 간격 하한을 정하는 반지름 배수입니다.
    ///
    /// 간격이 유닛 지름보다 좁아지면 병사들이 겹쳐 서고, 충돌 해소가
    /// 매 틱 밀어내 대열이 진동합니다. 그래서 아무리 조밀한 태세라도
    /// 이 하한 아래로는 내려가지 않습니다.
    ///   하한 = 반지름 * 2 * 이 값
    /// </summary>
    public float density_Min_Radius_Rate;

    // ---------------------------------------------------------------------
    // 적진 관통 방지
    //
    // 문제:
    // 지금 충돌 해소는 아군과 적군을 **똑같이** 처리합니다. 적/아군
    // 구분은 접촉 기록(benemyContact)에만 쓰이고 밀어내는 힘은 같습니다.
    // 그래서 전진 명령이 한 틱 분리량(maxSeparationPerTick)을 넘어서면
    // 병사가 적 사이를 비집고 들어갑니다.
    //
    // 토탈워도 같은 문제를 겪었습니다. Total War: Arena는 패치 10 이전에
    // 클릭 연타로 적진을 '뚫고 지나가' 교전에서 이탈할 수 있었고, CA는
    // **소프트 충돌(soft collision)** 을 넣어 막았습니다.
    //
    // 다만 완전 금지는 아닙니다. 질량이 큰 유닛은 대열을 뚫고 들어가
    // 후방에서 날뛸 수 있어야 합니다. 그것이 돌격의 의미입니다.
    // 그래서 아래 값들은 '막되, 질량 우위에는 길을 열어 주는' 구조입니다.
    //
    // 콜라이더 레이어를 적/아군으로 나누지 않는 이유:
    // 그러면 공간 그리드를 두 번 순회해야 합니다. 9,600 유닛에서 비용이
    // 두 배가 됩니다. 같은 순회 안에서 계수만 다르게 두면 비용이
    // 늘지 않으면서 같은 효과를 냅니다.
    // ---------------------------------------------------------------------
    /// <summary>
    /// 적을 밀어낼 때 분리 강도에 곱하는 배수입니다.
    /// 1.0이면 아군과 같고, 크면 적을 더 세게 튕겨냅니다.
    /// </summary>
    public float collide_Enemy_Separation_Rate;

    /// <summary>
    /// 적과의 접촉 판정에 쓰는 반지름 배수입니다.
    ///
    /// 1.0보다 크면 실제로 닿기 전에 먼저 밀리기 시작합니다.
    /// '적과 아군이 걸리는 충돌체를 다르게' 만드는 실질적인 수단입니다.
    /// </summary>
    public float collide_Enemy_Radius_Rate;

    // ---------------------------------------------------------------------
    // 발밑 마커가 지면 위로 뜨는 높이
    //
    // 왜 거리에 비례해야 하는가 — 지형 LOD 때문입니다:
    // 마커 높이는 지형에 레이캐스트해서 정합니다. 즉 **실제 높이맵**
    // 기준입니다. 그런데 유니티 터레인은 멀어질수록 메시를 단순화하고
    // (Scene1의 heightmapPixelError = 5), 그 단순화된 면은 원래 높이맵과
    // 어긋납니다. 볼록한 곳에서는 면이 위로 솟아 마커를 덮습니다.
    // 멀리 있는 마커만 지형에 잘려 보이던 것이 이것입니다.
    //
    // 그래서 기본 높이에 카메라 거리를 곱한 값을 더합니다. 가까이서는
    // 지면에 딱 붙어 보이고, 멀리서는 LOD 오차를 넘길 만큼 떠오릅니다.
    //
    // 깊이 정밀도 문제가 아닙니다. 이 게임의 카메라는 직교 투영이라
    // 깊이가 선형이고, 거리에 따라 정밀도가 나빠지지 않습니다.
    //
    // 깊이 테스트를 끄는 방법(ZTest Always)은 쓰지 않습니다. 그러면
    // 언덕 뒤에 있는 마커까지 비쳐 보여 지형을 읽을 수 없게 됩니다.
    //
    // 근본 해결은 heightmapPixelError를 낮추는 것이지만, 그만큼 지형
    // 삼각형이 늘어 9,600 유닛과 예산을 다투게 됩니다. 마커만 띄우는
    // 편이 훨씬 쌉니다.
    // ---------------------------------------------------------------------
    /// <summary>발밑 마커를 지면 위로 띄우는 기본 높이(m)입니다.</summary>
    public float marker_Ground_Lift;

    /// <summary>
    /// 카메라 거리 1m당 추가로 띄우는 높이입니다.
    /// 깊이 정밀도 손실을 상쇄하는 값이므로 아주 작아야 합니다.
    /// </summary>
    public float marker_Ground_Lift_Rate;

    /// <summary>
    /// 이 질량비를 넘으면 적진을 파고들 수 있습니다.
    ///
    /// (내 질량 / 상대 질량)이 이 값 이상이면 차단이 약해집니다.
    /// 기병이 보병 대열을 헤집는 것이 이 예외로 표현됩니다.
    /// </summary>
    public float collide_Push_Through_Mass_Rate;

    /// <summary>
    /// 질량 우위일 때 남기는 차단 비율입니다.
    /// 0이면 완전히 뚫고, 1이면 질량과 무관하게 막힙니다.
    /// </summary>
    public float collide_Push_Through_Block_Rate;

    // ---------------------------------------------------------------------
    // 근접 피격 방향 판정
    //
    // 이 게임의 핵심 전술 규칙(측면을 치면 유리하다)의 실체입니다.
    // 기존에는 Unit_Job.cs에 각도가 직접 박혀 있어 조정할 수 없었습니다.
    // ---------------------------------------------------------------------
    /// <summary>이 각도를 넘으면 정면 교전으로 봅니다. 방어력과 방패를 온전히 씁니다.</summary>
    public float hit_Angle_Front;
    /// <summary>이 각도 미만이면 후방 피격입니다. 방어력과 방패가 모두 무력화됩니다.</summary>
    public float hit_Angle_Back;
    /// <summary>측면 피격 시 남는 방어력 비율입니다. 방패는 이때도 소용이 없습니다.</summary>
    public float hit_Side_Defence_Rate;

    // ---------------------------------------------------------------------
    // 사기 충격
    // ---------------------------------------------------------------------
    /// <summary>돌격 충격이 상대 부대에 주는 사기 충격량입니다.</summary>
    public float morale_Shock_Charge;
    /// <summary>측후방에서 돌격당했을 때 사기 충격 배율입니다.</summary>
    public float morale_Shock_Flank_Rate;
    /// <summary>사기 충격이 초당 회복되는 양입니다.</summary>
    public float morale_Shock_Recover;

    // ---------------------------------------------------------------------
    // 피격 시각 피드백
    // ---------------------------------------------------------------------
    /// <summary>일반 피격 점멸이 사라지는 데 걸리는 시간(초)입니다.</summary>
    public float time_Hit_Flash;
    /// <summary>돌격 충돌 점멸이 사라지는 데 걸리는 시간(초)입니다.</summary>
    public float time_Charge_Flash;
    /// <summary>이 피해량에서 점멸 강도가 최대가 됩니다.</summary>
    public float hit_Flash_Full_Damage;
    /// <summary>점멸이 최대일 때 스프라이트가 커지는 비율입니다.</summary>
    public float hit_Flash_Punch_Scale;
    /// <summary>돌격 충돌 시 반동 배율입니다.</summary>
    public float charge_Flash_Punch_Rate;
    /// <summary>돌격 피격 점멸이 퍼지는 반경입니다. (진형 간격에 대한 배율)</summary>
    public float charge_Flash_Radius_Rate;

    // ---------------------------------------------------------------------
    // 공격 모션
    // ---------------------------------------------------------------------
    /// <summary>공격 시 표적 쪽으로 내지르는 거리입니다.</summary>
    public float attack_Lunge_Distance;
    /// <summary>내지르기가 끝나고 원위치로 돌아오기까지의 시간(초)입니다.</summary>
    public float attack_Lunge_Time;

    // ---------------------------------------------------------------------
    // 피로도
    // ---------------------------------------------------------------------
    /// <summary>피로도의 최댓값입니다.</summary>
    public float fatigue_Max;
    /// <summary>돌격 중 초당 누적되는 피로도입니다.</summary>
    public float fatigue_Gain_Charge;
    /// <summary>패주 중 초당 누적되는 피로도입니다.</summary>
    public float fatigue_Gain_Rout;
    /// <summary>근접 교전 중 초당 누적되는 피로도입니다.</summary>
    public float fatigue_Gain_Melee;
    /// <summary>일반 행군 중 초당 누적되는 피로도입니다.</summary>
    public float fatigue_Gain_Move;
    /// <summary>제자리에서 쉴 때 초당 회복되는 피로도입니다.</summary>
    public float fatigue_Recover;
    /// <summary>완전히 지쳤을 때 남는 전투/이동 성능 비율입니다.</summary>
    public float fatigue_Min_Rate;
    /// <summary>완전히 지쳤을 때 목표 사기에서 차감되는 양입니다.</summary>
    public float fatigue_Morale_Penalty;

    // 피로 단계 판정 기준
    //
    // 누적 피로도가 이 값들을 넘을 때마다 단계가 한 칸씩 올라갑니다.
    // 단계 자체는 성능에 직접 쓰이지 않고(그건 fatigue_Min_Rate가 합니다),
    // UI 표시와 가독성을 위한 구간 나눔입니다.
    /// <summary>이 피로도를 넘으면 '활발' 단계가 됩니다.</summary>
    public float fatigue_Active;
    /// <summary>이 피로도를 넘으면 '숨참' 단계가 됩니다.</summary>
    public float fatigue_Winded;
    /// <summary>이 피로도를 넘으면 '지침' 단계가 됩니다.</summary>
    public float fatigue_Tired;
    /// <summary>이 피로도를 넘으면 '매우 지침' 단계가 됩니다.</summary>
    public float fatigue_VeryTired;
    /// <summary>이 피로도를 넘으면 '탈진' 단계가 됩니다.</summary>
    public float fatigue_Exhausted;

    // ---------------------------------------------------------------------
    // 사기
    // ---------------------------------------------------------------------
    /// <summary>사기의 최댓값이자 초기값입니다.</summary>
    public float morale_Max;
    /// <summary>현재 사기가 목표 사기를 향해 움직이는 속도(초당)입니다.</summary>
    public float morale_Drift_Rate;
    /// <summary>전멸(손실률 100%) 시 적용되는 최대 사기 감소량입니다.</summary>
    public float morale_Penalty_Casualty;
    /// <summary>교전 중인 적 부대가 하나 늘어날 때마다 받는 포위 사기 감소량입니다.</summary>
    public float morale_Penalty_Surrounded;
    /// <summary>깃발을 든 유닛이 살아 있을 때 받는 사기 보너스입니다.</summary>
    public float morale_Bonus_Flag;
    /// <summary>수적 열세가 최대일 때의 사기 감소량입니다.</summary>
    public float morale_Penalty_Outnumbered;
    /// <summary>수적 열세 페널티가 최대에 도달하는 인원 비입니다.</summary>
    public float morale_Outnumbered_Full_Ratio;
    /// <summary>인접 아군 부대 하나가 붕괴할 때마다 받는 연쇄 사기 충격입니다.</summary>
    public float morale_Shock_Allied_Rout;
    /// <summary>연쇄 붕괴가 전파되는 반경입니다.</summary>
    public float morale_Allied_Rout_Radius;
    /// <summary>국지적으로 우세할 때 받는 최대 사기 보너스입니다.</summary>
    public float morale_Bonus_Winning;
    /// <summary>이 손실률을 넘긴 상태에서 붕괴하면 재결집이 불가능해집니다.</summary>
    public float morale_Shatter_Loss_Rate;
    /// <summary>이 값 이하가 되면 부대가 무너져 패주합니다.</summary>
    public float morale_Rout_Threshold;
    /// <summary>흔들림(Wavering) 상태로 판정되는 기준입니다.</summary>
    public float morale_Waver_Threshold;
    /// <summary>패주 부대가 재결집하기 위해 회복해야 하는 사기입니다.</summary>
    public float morale_Rally_Threshold;
    /// <summary>패주 후 재결집을 시도할 수 있게 되기까지의 시간(초)입니다.</summary>
    public float time_Rally;
    /// <summary>패주 중인 부대의 이동 속도 배율입니다.</summary>
    public float rout_Speed_Rate;

    // ---------------------------------------------------------------------
    // 이동 / 진형 일반
    // ---------------------------------------------------------------------
    /// <summary>걷기 속도 배율입니다. 밀집 태세처럼 뛸 수 없는 상태에서 곱해집니다.</summary>
    public float speed_Walk;
    /// <summary>목적지에 이만큼 가까워지면 도착으로 보고 멈춥니다.</summary>
    public float distance_Stop;
    /// <summary>진형 슬롯에서 이만큼 벗어난 유닛을 '대열 이탈'로 셉니다.</summary>
    public float distance_ReFormation;
    /// <summary>전방 판정에 쓰는 기준 거리입니다.</summary>
    public float distance_Move_FowardVector;
    /// <summary>접촉 없이 적을 시야로 탐지하는 거리입니다.</summary>
    public float distance_Detect;
    /// <summary>우클릭 드래그가 이 거리를 넘어야 진형선으로 인정합니다.</summary>
    public float distance_Formation;
    /// <summary>교전 중 표적을 향해 다가갈 수 있는 최대 거리입니다.</summary>
    public float distance_Attack_Move;
    /// <summary>대기 상태가 이 시간을 넘기면 자동 재정비를 발동합니다.</summary>
    public float time_Reformation;
    /// <summary>표적 부대를 다시 고르는 간격(초)입니다. 짧으면 표적이 산만해집니다.</summary>
    public float time_ReTarget;
    /// <summary>
    /// 표적을 향해 멀리 이동할 때의 최고 속도 배율입니다.
    ///
    /// 가속도는 건드리지 않습니다. 상한만 올리므로, 유닛은 평소와 같은
    /// 비율로 붙다가 더 높은 속도까지 계속 붙습니다.
    /// </summary>
    public float move_Speed_Target;

    /// <summary>
    /// 위 배율이 완전히 적용되는 거리(미터)입니다.
    ///
    /// 목표까지의 거리를 이 값으로 나눠 배율을 보간합니다.
    /// 즉 가까우면 1배, 이 거리 이상이면 move_Speed_Target 배입니다.
    ///
    /// 왜 이 값이 필요한가:
    /// 예전에는 거리(미터)를 Lerp의 t에 그대로 넘겼습니다. Lerp는 t를
    /// 0~1로 자르므로, **1m만 떨어져도 곧바로 최대 배율**이 걸렸습니다.
    /// 완만한 가속처럼 보이던 것이 사실은 계단 함수였고, 부대를 옮기라는
    /// 명령을 내리는 순간 전원이 두 배 속도로 튀어 나갔습니다.
    /// 이 값으로 나누어야 의도했던 '멀수록 빨리'가 실제로 동작합니다.
    /// </summary>
    public float distance_Speed_Target_Full;
    /// <summary>정지 판정에 쓰는 속도 임계입니다. 이보다 느리면 멈춘 것으로 봅니다.</summary>
    public float move_Speed_Stop;
    /// <summary>피격 후 반응이 이어지는 시간(초)입니다.</summary>
    public float time_beingAttackedDelay;
    /// <summary>부대 기준점의 기본 이동 속도 배율입니다.</summary>
    public float Army_Move_Rate;
    /// <summary>대열이 흐트러졌을 때 부대가 느려지는 배율입니다.</summary>
    public float Army_Move_Slow_Rate;
    /// <summary>재정비 중 이동에 곱해지는 속도 배율입니다.</summary>
    public float Army_ReFormation_Rate;

    /// <summary>
    /// 원본 Constant에 있던 기본값입니다.
    /// Balance_Config를 지정하지 않아도 기존과 완전히 동일하게 동작하도록,
    /// 이 값들은 리팩토링 이전 const 값을 그대로 옮겨 놓은 것입니다.
    /// </summary>
    public static Balance_Data Default()
    {
        Balance_Data b = new Balance_Data();

        // 전투 판정
        b.hit_Chance_Base = 30.0f;
        b.hit_Chance_Min = 8.0f;
        b.hit_Chance_Max = 90.0f;
        b.damage_Min = 1.0f;

        // 방어구
        b.armour_Full_Block = 200.0f;
        b.armour_Roll_Min_Rate = 0.5f;

        // 넉백
        b.knockback_Per_Damage = 1.0f;
        b.knockback_Impulse_Max = 40.0f;

        // 전열 유지
        b.distance_Engage_Leash = 0.8f;
        b.engage_Block_Rate = 0.05f;
        b.engage_Push_Rate = 0.15f;
        b.target_Front_Bias = 1.5f;

        // 교전 중 보행
        b.distance_Combat_Footwork = 15.0f;
        b.dot_Forward_Walk = 0.7f;
        b.dot_Side_Walk = -0.3f;
        b.speed_Rate_Sidestep = 0.65f;
        b.speed_Rate_Backpedal = 0.45f;
        b.reform_Speed_Rate = 0.5f;
        b.army_Blocked_Rate = 0.25f;
        b.distance_Front_Block_Ray = 5.0f;
        b.blocked_Accelerate_Rate = 0.0f;

        // 돌격
        b.time_Charge_Bonus = 8.0f;
        b.charge_Min_Speed_Rate = 0.15f;
        b.charge_Facing_Dot = 0.5f;
        b.charge_Min_Distance_Rate = 0.3f;
        b.charge_Trigger_Rate = 1.0f;
        b.charge_Contact_Skin = 0.05f;
        b.charge_Max_Penetration_Rate = 0.5f;

        // 충돌 공격
        b.collision_Damage_Base = 20.0f;
        b.collision_Mass_Full_Ratio = 3.0f;
        b.collision_Knockback_Impulse = 120.0f;
        b.large_Push_Through_Rate = 0.35f;

        // 지휘
        b.general_Aura_Radius = 30.0f;
        b.general_Aura_Morale = 15.0f;
        b.general_Death_Shock = 30.0f;

        // 지형
        b.terrain_Height_Full = 8.0f;
        b.terrain_High_Ground_Attack = 12.0f;
        b.terrain_High_Ground_Morale = 8.0f;
        b.terrain_High_Ground_Range_Rate = 0.25f;
        b.terrain_Slope_Speed_Penalty = 0.4f;
        b.terrain_Slope_Full_Angle = 35.0f;

        // 태세
        b.stance_Wall_Speed_Rate = 0.5f;
        b.stance_ShieldWall_Range_Defence = 25.0f;
        b.stance_ShieldWall_Charge_Resist = 0.5f;
        b.stance_SpearWall_Melee_Defence = 15.0f;
        b.stance_SpearWall_Charge_Resist = 1.0f;
        b.stance_SpearWall_Range_Penalty = 10.0f;
        b.stance_Front_Dot = 0.3f;
        b.stance_SpearWall_Reflect_Shock = 20.0f;
        b.stance_Skirmish_Flee_Distance = 8.0f;
        b.stance_Skirmish_Flee_Rate = 1.1f;

        // 태세별 밀집도
        //
        // 배율로 두는 이유: 병종마다 interval이 다릅니다(기병이 보병보다
        // 넓습니다). 절대값을 쓰면 그 차이가 사라집니다.
        b.density_Line = 1.0f;
        b.density_Loose = 1.35f;       // 토탈워 Loose: 면적을 넓혀 화살 회피
        b.density_ShieldWall = 0.75f;  // 방패를 겹치는 가장 조밀한 대열
        b.density_SpearWall = 0.85f;   // 창을 겨누되 방패벽만큼 붙지는 않음
        b.density_Skirmish = 1.5f;     // 궁병용. 가장 넓게 흩어짐
        b.density_Min_Radius_Rate = 1.05f;

        // 적진 관통 방지
        b.collide_Enemy_Separation_Rate = 2.0f;
        b.collide_Enemy_Radius_Rate = 1.25f;
        b.collide_Push_Through_Mass_Rate = 2.0f;
        b.collide_Push_Through_Block_Rate = 0.5f;

        // 발밑 마커 띄우기
        //
        // 0.002는 100m에서 0.2m가 더해지는 값입니다. 지형 굴곡에 비하면
        // 무시할 만하면서 깊이 정밀도 손실을 덮기에는 충분합니다.
        b.marker_Ground_Lift = 0.05f;
        b.marker_Ground_Lift_Rate = 0.002f;

        // 근접 피격 방향 (기존 Unit_Job.cs 하드코딩 값)
        b.hit_Angle_Front = 135.0f;
        b.hit_Angle_Back = 45.0f;
        b.hit_Side_Defence_Rate = 0.5f;

        // 사기 충격
        b.morale_Shock_Charge = 25.0f;
        b.morale_Shock_Flank_Rate = 2.0f;
        b.morale_Shock_Recover = 8.0f;

        // 시각 피드백
        b.time_Hit_Flash = 0.22f;
        b.time_Charge_Flash = 0.45f;
        b.hit_Flash_Full_Damage = 25.0f;
        b.hit_Flash_Punch_Scale = 0.35f;
        b.charge_Flash_Punch_Rate = 2.0f;
        b.charge_Flash_Radius_Rate = 4.0f;   // 기존 Army.cs 하드코딩 값

        // 공격 모션
        b.attack_Lunge_Distance = 0.35f;
        b.attack_Lunge_Time = 0.18f;

        // 피로도
        b.fatigue_Max = 100.0f;
        b.fatigue_Gain_Charge = 5.0f;
        b.fatigue_Gain_Rout = 3.5f;
        b.fatigue_Gain_Melee = 1.2f;
        b.fatigue_Gain_Move = 0.8f;
        b.fatigue_Recover = 2.0f;
        b.fatigue_Min_Rate = 0.5f;
        b.fatigue_Morale_Penalty = 25.0f;
        b.fatigue_Active = 15.0f;
        b.fatigue_Winded = 35.0f;
        b.fatigue_Tired = 55.0f;
        b.fatigue_VeryTired = 75.0f;
        b.fatigue_Exhausted = 90.0f;

        // 사기
        b.morale_Max = 100.0f;
        b.morale_Drift_Rate = 12.0f;
        b.morale_Penalty_Casualty = 65.0f;
        b.morale_Penalty_Surrounded = 20.0f;
        b.morale_Bonus_Flag = 10.0f;
        b.morale_Penalty_Outnumbered = 18.0f;
        b.morale_Outnumbered_Full_Ratio = 3.0f;
        b.morale_Shock_Allied_Rout = 15.0f;
        b.morale_Allied_Rout_Radius = 25.0f;
        b.morale_Bonus_Winning = 12.0f;
        b.morale_Shatter_Loss_Rate = 0.7f;
        b.morale_Rout_Threshold = 0.0f;
        b.morale_Waver_Threshold = 30.0f;
        b.morale_Rally_Threshold = 35.0f;
        b.time_Rally = 10.0f;
        b.rout_Speed_Rate = 1.6f;

        // 이동 / 진형 일반
        b.speed_Walk = 1.0f;
        b.distance_Stop = 1.0f;
        b.distance_ReFormation = 0.5f;
        b.distance_Move_FowardVector = 0.5f;
        b.distance_Detect = 20.0f;
        b.distance_Formation = 1.0f;
        b.distance_Attack_Move = 1.0f;
        b.time_Reformation = 3.0f;
        b.time_ReTarget = 1.0f;
        // 최고 속도는 오히려 올립니다. 대신 아래 거리로 완만하게 붙습니다.
        //
        // 예전 값 2.0은 1m만 벗어나도 즉시 걸렸습니다(Lerp의 t 클램프).
        // 그래서 '빠르다'가 아니라 '튄다'로 느껴졌습니다.
        // 이제는 25m를 가야 2.4배에 도달하므로, 먼 거리를 옮길 때는
        // 예전보다 빠르고 짧은 거리에서는 오히려 얌전합니다.
        b.move_Speed_Target = 2.4f;
        b.distance_Speed_Target_Full = 25.0f;
        b.move_Speed_Stop = 0.3f;
        b.time_beingAttackedDelay = 1.5f;
        b.Army_Move_Rate = 0.3f;
        b.Army_Move_Slow_Rate = 0.3f;
        b.Army_ReFormation_Rate = 0.8f;

        return b;
    }
}
