using UnityEngine;

static class Constant
{
    /// <summary>
    /// 시뮬레이션 고정 틱 간격입니다.
    /// Burst Job 내부에서는 Time.fixedDeltaTime에 접근할 수 없으므로 const로 유지하고,
    /// 대신 Controller.Awake()에서 Time.fixedDeltaTime을 이 값으로 맞춥니다.
    /// 두 값이 어긋나면 부대(메인 스레드)와 유닛(Job)의 시간 척도가 달라집니다.
    /// </summary>
    public const float deltaTime = 1.0f / 60.0f;

    /// <summary>목표 프레임 레이트입니다. deltaTime과 반드시 일치해야 합니다.</summary>
    public const int targetFrameRate = 60;

    public const float speed_Walk = 1.0f;

    public const float distance_Stop = 1.0f;
    public const float distance_ReFormation = 0.5f;
    public const float distance_Move_FowardVector = 0.5f;
    public const float distance_Detect = 20.0f;
    public const float distance_Formation = 1.0f;
    public const float distance_Attack_Move = 1.0f;

    public const float time_Reformation = 3.0f;

    public const float time_ReTarget = 1.0f;

    public const float move_Speed_Target = 2.0f;
    public const float move_Speed_Stop = 0.3f;

    public const float time_beingAttackedDelay = 1.5f;

    // ---------------------------------------------------------------------
    // 전투 판정
    // ---------------------------------------------------------------------
    /// <summary>
    /// 명중 확률의 기본값(%)입니다.
    /// 최종 확률 = base + 공격력 + 상성보너스 + 돌격보너스 - 방어력.
    /// 공격력과 방어력이 같으면 이 값이 그대로 명중 확률이 됩니다.
    /// </summary>
    public const float hit_Chance_Base = 30.0f;

    /// <summary>
    /// 명중 확률의 하한(%)입니다.
    /// 아무리 방어력이 높아도 이 확률로는 맞습니다. 절대 무적을 막는 장치입니다.
    /// </summary>
    public const float hit_Chance_Min = 8.0f;

    /// <summary>
    /// 명중 확률의 상한(%)입니다.
    /// 아무리 공격력이 높아도 이 확률을 넘지 못합니다. 확정타를 막는 장치입니다.
    /// </summary>
    public const float hit_Chance_Max = 90.0f;

    /// <summary>
    /// 방어구가 공격력을 완전히 상회해도 보장되는 최소 피해량입니다.
    /// 0이면 중장갑 유닛이 무적이 되어 교전이 영원히 끝나지 않습니다.
    /// </summary>
    public const float damage_Min = 1.0f;

    // ---------------------------------------------------------------------
    // 방어구 (Armour)
    //
    // 피해는 두 종류로 나뉩니다.
    //   기본 피해(base) : 방어구가 막습니다.
    //   관통 피해(AP)   : 방어구를 무시하고 그대로 들어갑니다.
    //
    // 이 분리가 병종 상성의 근간입니다. 중장갑을 뚫으려면 AP가 필요하므로
    // 도끼/망치/석궁이 검/창보다 중장갑에 강해지는 구조가 자연히 나옵니다.
    //
    // 방어구 감산은 '고정'이 아니라 '랜덤'입니다.
    // 고정 감산이면 방어구가 공격력을 넘는 순간 항상 최소 피해로 바닥을 쳐서
    // 교전이 교착에 빠집니다. 랜덤이면 매 타격에 변동이 생겨 전투가 흐릅니다.
    // ---------------------------------------------------------------------
    /// <summary>
    /// 기본 피해를 100% 막아내는 방어구 수치입니다.
    /// 감산율 = 방어구 굴림값 / 이 값. 이 수치에 도달하면 기본 피해가 전부 차단됩니다.
    /// (그래도 AP 피해는 그대로 들어오므로 완전 무적이 되지는 않습니다)
    /// </summary>
    public const float armour_Full_Block = 200.0f;

    /// <summary>
    /// 방어구 굴림의 하한 배율입니다.
    /// 실제 감산에 쓰이는 값은 rand(방어구 x 이 배율, 방어구)입니다.
    /// 0.5이면 방어구 30일 때 15~30 사이가 굴려집니다.
    /// </summary>
    public const float armour_Roll_Min_Rate = 0.5f;

    // ---------------------------------------------------------------------
    // 피격 시 밀림(넉백)
    // ---------------------------------------------------------------------
    /// <summary>
    /// 피해량 1당 가해지는 충격량입니다.
    /// ForceMode.Impulse로 적용되므로 실제 속도 변화는 (충격량 / 질량)입니다.
    /// 질량 100, 피해 16이면 약 0.16 m/s로 이동 속도(3)에 비해 가벼운 휘청임이 됩니다.
    /// </summary>
    public const float knockback_Per_Damage = 1.0f;

    /// <summary>
    /// 단일 타격이 가할 수 있는 최대 충격량입니다.
    /// 폭발적인 피해가 들어와도 유닛이 날아가지 않도록 상한을 둡니다.
    /// </summary>
    public const float knockback_Impulse_Max = 40.0f;

    // ---------------------------------------------------------------------
    // 전열 유지 (Battle Line)
    // 토탈워의 근접전은 두 대열이 '접촉면'에서 맞붙어 버티는 형태입니다.
    // 서로를 뚫고 들어가 뒤섞이면 진형·측면·포위 개념이 전부 무너집니다.
    // ---------------------------------------------------------------------
    /// <summary>
    /// 교전 중인 유닛이 자기 진형 슬롯에서 벗어날 수 있는 최대 거리입니다.
    /// 이 값을 넘어서면 적을 더 쫓지 않고 제자리를 지킵니다. (전열 이탈 방지)
    /// </summary>
    public const float distance_Engage_Leash = 0.8f;

    /// <summary>
    /// 적과 접촉했을 때 남는 전진 속도 비율입니다.
    /// 0에 가까울수록 벽처럼 맞부딪혀 서로를 밀고 들어가지 못합니다.
    /// </summary>
    public const float engage_Block_Rate = 0.05f;

    /// <summary>
    /// 교전 중 아군을 옆으로 밀어내는 힘의 비율입니다.
    /// 전투 중에는 자리를 지켜야 하므로 크게 낮춥니다.
    /// </summary>
    public const float engage_Push_Rate = 0.15f;

    /// <summary>
    /// 타겟 선정 시 '정면'을 얼마나 우선할지 결정하는 가중치입니다.
    /// 점수 = 거리 + (1 - 정면내적) * 이 값. 즉 바로 뒤의 적은 이 값의 2배만큼
    /// 더 멀리 있는 것으로 취급됩니다.
    /// 이 값이 0이면 순수 최근접이 되어, 옆이나 뒤의 적을 잡고 대열을 가로지릅니다.
    /// </summary>
    public const float target_Front_Bias = 1.5f;

    // ---------------------------------------------------------------------
    // 교전 중 보행 (Combat Footwork)
    // 교전 중에 등을 보이면 방어력과 방패를 모두 잃습니다.
    // 따라서 가까운 거리의 재배치는 적을 바라본 채 뒷걸음/옆걸음으로 처리합니다.
    // ---------------------------------------------------------------------
    /// <summary>
    /// 교전 중 '적을 바라본 채' 이동할 최대 거리입니다.
    /// 이보다 먼 목적지는 전장 재배치로 보고 몸을 돌려 정상 행군합니다.
    ///
    /// 기준: 진형 가로 길이가 인원 50 / 간격 1 기준으로 10~25m입니다.
    /// 이 값이 그보다 작으면 전열 안에서 자리를 좁히는 정도의 이동조차
    /// '먼 재배치'로 판정되어 발놀림이 거의 발동하지 않습니다.
    /// 따라서 진형 폭과 같은 수준으로 잡습니다.
    /// </summary>
    public const float distance_Combat_Footwork = 15.0f;

    /// <summary>정면 보행으로 판정하는 내적 하한입니다. (약 45도 이내)</summary>
    public const float dot_Forward_Walk = 0.7f;

    /// <summary>옆걸음으로 판정하는 내적 하한입니다. 이보다 낮으면 뒷걸음입니다.</summary>
    public const float dot_Side_Walk = -0.3f;

    /// <summary>옆걸음 속도 배율입니다.</summary>
    public const float speed_Rate_Sidestep = 0.65f;

    /// <summary>뒷걸음 속도 배율입니다. 가장 느립니다.</summary>
    public const float speed_Rate_Backpedal = 0.45f;

    /// <summary>
    /// 교전 중 대열을 메우러 이동할 때의 속도 배율입니다.
    /// 싸우면서 조금씩 자리를 좁히는 움직임이라 느려야 합니다.
    /// </summary>
    public const float reform_Speed_Rate = 0.5f;

    /// <summary>
    /// 이 비율 이상의 유닛이 적에게 막히면 부대 기준점도 전진을 멈춥니다.
    /// 기준점만 계속 나아가면 진형 슬롯이 적진 너머에 생겨
    /// 유닛들이 영원히 적을 밀며 따라가려 합니다.
    /// </summary>
    public const float army_Blocked_Rate = 0.25f;

    /// <summary>
    /// 유닛이 전방의 교전을 감지하는 레이캐스트 거리입니다.
    /// 이 거리 안에서 '이미 싸우고 있는 유닛'을 발견하면 전진을 멈춰
    /// 난전 덩어리에 뒤에서 밀고 들어가지 않도록 합니다.
    /// (돌격 중에는 적용하지 않습니다)
    /// </summary>
    public const float distance_Front_Block_Ray = 5.0f;

    /// <summary>
    /// 적과 접촉한 상태에서 남는 가속 비율입니다.
    /// 0이면 적 대열에 막혀 더 이상 전진하지 못합니다.
    /// </summary>
    public const float blocked_Accelerate_Rate = 0.0f;

    // ---------------------------------------------------------------------
    // 돌격 (Charge)
    // 토탈워의 돌격은 '충돌 순간'에 몰린 보너스입니다.
    // 접촉하면 즉시 난전으로 전환되고 보너스는 시간에 따라 사라집니다.
    // ---------------------------------------------------------------------
    /// <summary>돌격 보너스가 완전히 사라지기까지의 시간(초)입니다.</summary>
    public const float time_Charge_Bonus = 8.0f;

    /// <summary>
    /// 돌격 충격이 인정되는 최소 속도 비율입니다.
    /// 제자리에서 붙은 돌격은 충격이 거의 없고, 전속력으로 달려와 부딪혀야 위력이 나옵니다.
    /// </summary>
    public const float charge_Min_Speed_Rate = 0.15f;

    /// <summary>
    /// 돌격이 성립하려면 이동 방향이 적 쪽이어야 합니다.
    /// 이동 방향과 적 방향의 내적이 이 값 미만이면 돌격하지 않습니다. (약 60도)
    /// 이 검사가 없으면 교전 중 '후퇴 명령'조차 돌격으로 전환됩니다.
    /// </summary>
    public const float charge_Facing_Dot = 0.5f;

    /// <summary>
    /// 돌격에 필요한 최소 거리입니다. (돌격 사거리에 대한 비율)
    /// 이미 맞붙어 있는 상대에게는 가속할 여지가 없으므로 돌격이 성립하지 않습니다.
    /// </summary>
    public const float charge_Min_Distance_Rate = 0.3f;

    /// <summary>
    /// 돌격이 자동으로 시작되는 거리 배율입니다.
    /// meleeChargeRange에 이 값을 곱한 거리 안에 적이 들어오면 돌격에 들어갑니다.
    /// </summary>
    public const float charge_Trigger_Rate = 1.0f;

    /// <summary>
    /// 돌격이 멈춰야 할 접촉면까지의 여유 거리입니다.
    /// 표적과의 거리가 (내 반지름 + 상대 반지름 + 이 값) 이하가 되면 접촉으로 간주합니다.
    ///
    /// 물리 충돌(OnCollisionStay)을 기다리면 이미 한 틱 파고든 뒤이므로,
    /// 돌격은 반드시 '예측'으로 정지 지점을 잡아야 합니다.
    /// 이 값이 0이면 콜라이더가 정확히 맞닿는 지점에서 멈춥니다.
    /// </summary>
    public const float charge_Contact_Skin = 0.05f;

    /// <summary>
    /// 한 틱에 허용되는 최대 진입 깊이(상대 반지름에 대한 비율)입니다.
    ///
    /// 돌격은 고속이라 틱당 이동량이 콜라이더 반지름에 근접합니다.
    /// 이동 벡터를 이 비율 이내로 잘라내면, Discrete 충돌 감지에서도
    /// 상대를 통과하지 못하고 접촉면에 정확히 붙습니다.
    /// </summary>
    public const float charge_Max_Penetration_Rate = 0.5f;

    // ---------------------------------------------------------------------
    // 지휘 (General)
    //
    // 토탈워에서 장군은 주변 부대의 사기를 떠받치는 기둥입니다.
    // 그래서 장군을 잡으면 전선이 통째로 무너집니다.
    // ---------------------------------------------------------------------
    /// <summary>장군 오라가 미치는 반경입니다.</summary>
    public const float general_Aura_Radius = 30.0f;

    /// <summary>장군 근처에서 받는 최대 사기 보너스입니다. 가까울수록 큽니다.</summary>
    public const float general_Aura_Morale = 15.0f;

    /// <summary>
    /// 장군이 전사했을 때 아군 전체가 받는 사기 충격입니다.
    /// 반경과 무관하게 전군에 적용됩니다. 지휘 계통이 끊긴 충격입니다.
    /// </summary>
    public const float general_Death_Shock = 30.0f;

    // ---------------------------------------------------------------------
    // 지형 (Terrain)
    //
    // 고지를 잡는 것은 토탈워 전투 배치의 첫 번째 원칙입니다.
    // 위에서 내려치는 쪽이 유리하고, 오르막을 오르는 쪽은 지칩니다.
    // ---------------------------------------------------------------------
    /// <summary>고지 우위가 최대에 도달하는 높이 차(m)입니다.</summary>
    public const float terrain_Height_Full = 8.0f;

    /// <summary>고지에서 얻는 최대 명중 보너스입니다.</summary>
    public const float terrain_High_Ground_Attack = 12.0f;

    /// <summary>고지에서 얻는 최대 사기 보너스입니다.</summary>
    public const float terrain_High_Ground_Morale = 8.0f;

    /// <summary>
    /// 원거리 사거리에 대한 고지 보정 배율입니다.
    /// 높은 곳에서 쏘면 더 멀리 날아갑니다.
    /// </summary>
    public const float terrain_High_Ground_Range_Rate = 0.25f;

    /// <summary>
    /// 오르막을 오를 때 최대로 깎이는 이동 속도 비율입니다.
    /// 0.4이면 가장 가파른 경사에서 속도가 60%가 됩니다.
    /// </summary>
    public const float terrain_Slope_Speed_Penalty = 0.4f;

    /// <summary>속도 페널티가 최대에 도달하는 경사(도)입니다.</summary>
    public const float terrain_Slope_Full_Angle = 35.0f;

    // ---------------------------------------------------------------------
    // 태세 (Stance)
    //
    // 방패벽과 창벽은 '멈춰서 버티는 대신 기동을 포기하는' 거래입니다.
    // 창벽이 정면에서 기병 돌격을 무력화하는 것이 토탈워 전술의 축입니다.
    // ---------------------------------------------------------------------
    /// <summary>방패벽/창벽 태세에서의 이동 속도 배율입니다. 걷기 수준으로 떨어집니다.</summary>
    public const float stance_Wall_Speed_Rate = 0.5f;

    /// <summary>방패벽의 원거리 방어 보너스입니다.</summary>
    public const float stance_ShieldWall_Range_Defence = 25.0f;

    /// <summary>방패벽의 돌격 저항입니다. 받는 돌격 보너스를 이 비율만큼 없앱니다.</summary>
    public const float stance_ShieldWall_Charge_Resist = 0.5f;

    /// <summary>창벽의 근접 방어 보너스입니다.</summary>
    public const float stance_SpearWall_Melee_Defence = 15.0f;

    /// <summary>
    /// 창벽의 돌격 저항입니다. 1.0이면 정면 돌격 보너스를 완전히 무효화합니다.
    /// 창을 세운 대열에 말을 몰아넣는 것은 자살이라는 뜻입니다.
    /// </summary>
    public const float stance_SpearWall_Charge_Resist = 1.0f;

    /// <summary>창벽은 원거리에 취약합니다. 밀집해 있어 화살이 잘 박힙니다.</summary>
    public const float stance_SpearWall_Range_Penalty = 10.0f;

    /// <summary>
    /// 돌격 저항이 정면으로 인정되는 내적 하한입니다.
    /// 측후방에서 들어온 돌격은 창벽으로도 막지 못합니다.
    /// </summary>
    public const float stance_Front_Dot = 0.3f;

    /// <summary>
    /// 창벽이 정지 상태에서 돌격을 반사할 때, 돌격한 쪽이 받는 사기 충격입니다.
    /// 말이 창에 꿰뚫리는 광경이 돌격 부대를 흔듭니다.
    /// </summary>
    public const float stance_SpearWall_Reflect_Shock = 20.0f;

    /// <summary>산개 태세가 후퇴를 시작하는 거리입니다. 이보다 가까우면 물러납니다.</summary>
    public const float stance_Skirmish_Flee_Distance = 8.0f;

    /// <summary>산개 후퇴 속도 배율입니다.</summary>
    public const float stance_Skirmish_Flee_Rate = 1.1f;

    /// <summary>
    /// 돌격 충격이 상대 부대에 주는 사기 충격량입니다.
    /// 정면 기준이며, 측후방이면 아래 배율이 곱해집니다.
    /// </summary>
    public const float morale_Shock_Charge = 25.0f;

    /// <summary>측후방에서 돌격당했을 때 사기 충격 배율입니다.</summary>
    public const float morale_Shock_Flank_Rate = 2.0f;

    /// <summary>사기 충격이 초당 회복되는 양입니다.</summary>
    public const float morale_Shock_Recover = 8.0f;

    // ---------------------------------------------------------------------
    // 피격 시각 피드백 (Hit Feedback)
    // 대규모 전투에서는 개별 타격이 눈에 잘 띄지 않으므로
    // 색 점멸과 크기 반동으로 '맞았다'는 사실을 분명히 보여줍니다.
    // ---------------------------------------------------------------------
    /// <summary>일반 피격 점멸이 사라지는 데 걸리는 시간(초)입니다.</summary>
    public const float time_Hit_Flash = 0.22f;

    /// <summary>돌격 충돌 점멸이 사라지는 데 걸리는 시간(초)입니다. 더 길게 남깁니다.</summary>
    public const float time_Charge_Flash = 0.45f;

    /// <summary>이 피해량에서 점멸 강도가 최대가 됩니다.</summary>
    public const float hit_Flash_Full_Damage = 25.0f;

    /// <summary>점멸이 최대일 때 스프라이트가 커지는 비율입니다. (타격 반동)</summary>
    public const float hit_Flash_Punch_Scale = 0.35f;

    /// <summary>돌격 충돌 시 반동 배율입니다.</summary>
    public const float charge_Flash_Punch_Rate = 2.0f;

    // ---------------------------------------------------------------------
    // 피로도 (Fatigue)
    // 토탈워에서 피로는 돌격 남발을 막는 견제 장치이자
    // 장기 교전에서 승부가 갈리는 지점입니다.
    // 지친 부대는 덜 맞히고, 덜 아프게 때리고, 느려지고, 먼저 무너집니다.
    // ---------------------------------------------------------------------
    /// <summary>피로도의 최댓값입니다.</summary>
    public const float fatigue_Max = 100.0f;

    /// <summary>돌격 중 초당 누적되는 피로도입니다. 가장 빠르게 지칩니다.</summary>
    public const float fatigue_Gain_Charge = 5.0f;

    /// <summary>패주(전력 질주) 중 초당 누적되는 피로도입니다.</summary>
    public const float fatigue_Gain_Rout = 3.5f;

    /// <summary>근접 교전 중 초당 누적되는 피로도입니다.</summary>
    public const float fatigue_Gain_Melee = 1.2f;

    /// <summary>일반 행군 중 초당 누적되는 피로도입니다.</summary>
    public const float fatigue_Gain_Move = 0.8f;

    /// <summary>제자리에서 쉴 때 초당 회복되는 피로도입니다.</summary>
    public const float fatigue_Recover = 2.0f;

    /// <summary>
    /// 완전히 지쳤을 때 남는 전투/이동 성능 비율입니다.
    /// 1.0이면 피로가 무의미하고, 0에 가까우면 지친 부대가 무력해집니다.
    /// </summary>
    public const float fatigue_Min_Rate = 0.5f;

    /// <summary>완전히 지쳤을 때 목표 사기에서 차감되는 양입니다.</summary>
    public const float fatigue_Morale_Penalty = 25.0f;

    // 피로 단계 판정 기준 (토탈워의 Fresh~Exhausted에 대응)
    public const float fatigue_Active = 15.0f;
    public const float fatigue_Winded = 35.0f;
    public const float fatigue_Tired = 55.0f;
    public const float fatigue_VeryTired = 75.0f;
    public const float fatigue_Exhausted = 90.0f;

    // ---------------------------------------------------------------------
    // 사기 (Morale) — 토탈워식 전투의 핵심
    // 부대는 전멸이 아니라 '무너져서' 패배합니다.
    // 현재 사기는 매 틱 목표 사기(modifier 합)를 향해 서서히 이동합니다.
    // ---------------------------------------------------------------------
    /// <summary>사기의 최댓값이자 초기값입니다.</summary>
    public const float morale_Max = 100.0f;

    /// <summary>현재 사기가 목표 사기를 향해 움직이는 속도(초당)입니다. 급변을 막습니다.</summary>
    public const float morale_Drift_Rate = 12.0f;

    /// <summary>전멸(손실률 100%) 시 적용되는 최대 사기 감소량입니다.</summary>
    public const float morale_Penalty_Casualty = 65.0f;

    /// <summary>교전 중인 적 부대가 하나 늘어날 때마다 받는 포위 사기 감소량입니다.</summary>
    public const float morale_Penalty_Surrounded = 20.0f;

    /// <summary>깃발을 든 유닛이 살아 있을 때 받는 사기 보너스입니다.</summary>
    public const float morale_Bonus_Flag = 10.0f;

    /// <summary>
    /// 수적 열세가 최대일 때의 사기 감소량입니다.
    /// 교전 상대와의 인원 비에 비례해 이 값까지 깎입니다.
    /// </summary>
    public const float morale_Penalty_Outnumbered = 18.0f;

    /// <summary>
    /// 수적 열세 페널티가 최대에 도달하는 인원 비입니다.
    /// 3.0이면 '상대가 3배 이상'일 때 페널티가 최대가 됩니다.
    /// </summary>
    public const float morale_Outnumbered_Full_Ratio = 3.0f;

    /// <summary>
    /// 인접 아군 부대 하나가 붕괴할 때마다 받는 연쇄 사기 충격입니다.
    /// 전열이 한 번 무너지기 시작하면 옆으로 번지게 만드는 장치입니다.
    /// </summary>
    public const float morale_Shock_Allied_Rout = 15.0f;

    /// <summary>연쇄 붕괴가 전파되는 반경입니다. 이 거리 안의 아군만 영향을 받습니다.</summary>
    public const float morale_Allied_Rout_Radius = 25.0f;

    /// <summary>
    /// 국지적으로 우세할 때 받는 최대 사기 보너스입니다.
    /// 이기고 있다는 실감이 부대를 버티게 합니다.
    /// </summary>
    public const float morale_Bonus_Winning = 12.0f;

    /// <summary>
    /// 이 손실률을 넘긴 상태에서 붕괴하면 재결집이 불가능해집니다. (Shattered)
    /// 토탈워에서 너무 많이 죽은 부대는 다시 싸우지 못하고 전장을 떠납니다.
    /// </summary>
    public const float morale_Shatter_Loss_Rate = 0.7f;

    /// <summary>이 값 이하가 되면 부대가 무너져 패주합니다.</summary>
    public const float morale_Rout_Threshold = 0.0f;

    /// <summary>흔들림(Wavering) 상태로 판정되는 기준입니다.</summary>
    public const float morale_Waver_Threshold = 30.0f;

    /// <summary>패주 부대가 재결집하기 위해 회복해야 하는 사기입니다.</summary>
    public const float morale_Rally_Threshold = 35.0f;

    /// <summary>패주 후 재결집을 시도할 수 있게 되기까지의 시간(초)입니다.</summary>
    public const float time_Rally = 10.0f;

    /// <summary>패주 중인 부대의 이동 속도 배율입니다. 공포에 질려 달아납니다.</summary>
    public const float rout_Speed_Rate = 1.6f;

    public const float Army_Move_Rate = 0.3f;
    public const float Army_Move_Slow_Rate = 0.3f;
    public const float Army_ReFormation_Rate = 0.8f;
}
