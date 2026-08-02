using System;
using UnityEngine;

/// <summary>
/// 게임 전역 이벤트를 중계하는 정적 이벤트 버스입니다.
///
/// 목적: 사운드, 이펙트, UI, 통계 같은 '반응' 기능을 추가할 때
/// Unit / Army 본체를 수정하지 않아도 되도록 만드는 것입니다.
///
/// 주의사항
/// - 정적 이벤트이므로 구독자는 반드시 OnDisable/OnDestroy에서 해지해야 합니다.
///   (해지하지 않으면 씬 전환 후에도 파괴된 오브젝트가 호출되어 예외가 납니다)
/// - 발행 지점은 시뮬레이션 틱(FixedUpdate) 안이므로, 구독자에서 무거운 작업을
///   하면 시뮬레이션 프레임이 직접 느려집니다. 무거운 작업은 큐에 쌓아 두십시오.
/// - Burst Job 내부에서는 호출할 수 없습니다. 반드시 메인 스레드 계층
///   (Army._Update_Dead 등)에서 발행합니다.
/// </summary>
public static class GameEvents
{
    // 유닛 단위 이벤트
    /// <summary>유닛이 사망했을 때 발행됩니다. (사망 유닛, 사망 유닛의 부대, 킬을 기록한 부대 또는 null)</summary>
    public static event Action<Unit, Army, Army> OnUnitKilled;

    /// <summary>유닛이 피해를 입었을 때 발행됩니다. (피격 유닛, 피해량, 피해 방향)</summary>
    public static event Action<Unit, float, Vector3> OnUnitDamaged;

    // 부대 단위 이벤트
    /// <summary>부대가 교전을 시작했을 때 발행됩니다. (아군 부대, 교전 상대 부대)</summary>
    public static event Action<Army, Army> OnArmyEngaged;

    /// <summary>부대가 전멸했을 때 발행됩니다.</summary>
    public static event Action<Army> OnArmyWiped;

    /// <summary>부대가 사기 붕괴로 패주를 시작했을 때 발행됩니다.</summary>
    public static event Action<Army> OnArmyRouted;

    /// <summary>패주하던 부대가 재결집했을 때 발행됩니다.</summary>
    public static event Action<Army> OnArmyRallied;

    /// <summary>
    /// 부대가 와해되어 재결집이 불가능해졌을 때 발행됩니다.
    /// 승패 판정에서 '전투 불능'으로 세는 기준이 됩니다.
    /// </summary>
    public static event Action<Army> OnArmyShattered;

    /// <summary>부대가 돌격을 시작했을 때 발행됩니다. (돌격 부대, 대상 부대)</summary>
    public static event Action<Army, Army> OnArmyCharged;

    /// <summary>부대의 선택 상태가 바뀌었을 때 발행됩니다. (부대, 선택 여부)</summary>
    public static event Action<Army, bool> OnArmySelectionChanged;

    // 발행 메서드
    // 구독자 예외가 시뮬레이션 루프를 중단시키지 않도록 각 발행을 보호합니다.
    public static void RaiseUnitKilled(Unit unit, Army victimArmy, Army killerArmy)
    {
        if (OnUnitKilled == null) return;
        try { OnUnitKilled(unit, victimArmy, killerArmy); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseUnitDamaged(Unit unit, float damage, Vector3 direction)
    {
        if (OnUnitDamaged == null) return;
        try { OnUnitDamaged(unit, damage, direction); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyEngaged(Army army, Army target)
    {
        if (OnArmyEngaged == null) return;
        try { OnArmyEngaged(army, target); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyWiped(Army army)
    {
        if (OnArmyWiped == null) return;
        try { OnArmyWiped(army); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyRouted(Army army)
    {
        if (OnArmyRouted == null) return;
        try { OnArmyRouted(army); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyRallied(Army army)
    {
        if (OnArmyRallied == null) return;
        try { OnArmyRallied(army); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyShattered(Army army)
    {
        if (OnArmyShattered == null) return;
        try { OnArmyShattered(army); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmyCharged(Army army, Army target)
    {
        if (OnArmyCharged == null) return;
        try { OnArmyCharged(army, target); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void RaiseArmySelectionChanged(Army army, bool selected)
    {
        if (OnArmySelectionChanged == null) return;
        try { OnArmySelectionChanged(army, selected); }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>
    /// 모든 구독을 해지합니다.
    /// 정적 이벤트는 플레이 모드를 나가도 살아남으므로(도메인 리로드 비활성 시),
    /// 씬 시작 시 반드시 호출해 이전 세션의 구독자를 제거해야 합니다.
    /// </summary>
    public static void ClearAll()
    {
        OnUnitKilled = null;
        OnUnitDamaged = null;
        OnArmyEngaged = null;
        OnArmyWiped = null;
        OnArmyRouted = null;
        OnArmyRallied = null;
        OnArmyShattered = null;
        OnArmyCharged = null;
        OnArmySelectionChanged = null;
    }
}
