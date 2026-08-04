using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대의 "감지" 책임을 담당하는 부분 클래스입니다.
///
/// 담는 것: 지형 판정(고지/경사), 시야 탐지, 접촉 집계.
/// 공통점은 전부 '부대가 주변 세계를 읽어 들이는' 일이라는 점입니다.
/// 판단(Army_Fight)이나 행동(Army_Move)과는 관심사가 다릅니다.
///
/// 이 영역은 부대 수 N에 대해 O(N^2)입니다. allArmies를 전수 순회하기
/// 때문이며, 부대가 수십 개를 넘어가면 여기가 먼저 병목이 됩니다.
/// 부대 단위 공간 격자를 도입한다면 손댈 곳은 이 파일입니다.
/// </summary>
partial class Army
{

    /// <summary>
    /// 지형 상태(고지 우위, 경사)를 갱신합니다.
    ///
    /// 고지를 잡는 것은 토탈워 배치의 첫 번째 원칙입니다.
    /// 위에서 내려치는 쪽은 더 잘 맞히고 사기가 오르며 화살이 멀리 날아가고,
    /// 오르막을 오르는 쪽은 느려집니다.
    /// </summary>
    private void _Update_Terrain()
    {
        Vector3 myPosition = formation_Move_Transform.position;

        // 1. 고지 우위: 교전 상대와의 높이 차로 계산합니다.
        //    상대가 없으면 우위도 열세도 없습니다.
        float rate = 0.0f;

        if (targetArmy != null && targetArmy.units.Count > 0)
        {
            float heightDelta = myPosition.y - targetArmy.formation_Move_Transform.position.y;

            rate = heightDelta / Constant.terrain_Height_Full;
            if (rate < -1.0f) rate = -1.0f;
            if (rate > 1.0f) rate = 1.0f;
        }

        army_Data.highGroundRate = rate;

        // 2. 경사: 발밑 지면의 기울기를 재서 이동 속도에 반영합니다.
        //    레이캐스트 한 번이면 충분하므로 부대 기준점에서만 봅니다.
        //    (유닛마다 쏘면 부대 수 x 인원만큼 늘어나 낭비가 큽니다)
        if (Physics.Raycast(myPosition + Vector3.up * 2.0f, Vector3.down,
                            out RaycastHit hit, 10.0f, groundLayerMask))
        {
            army_Data.slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        }
        else
        {
            army_Data.slopeAngle = 0.0f;
        }
    }

    /// <summary>"Ground" 레이어 마스크를 캐시한 값입니다.</summary>
    private static int groundLayerMaskCache = -1;

    /// <summary>지면 레이어 마스크입니다. 최초 접근 시 한 번만 조회합니다.</summary>
    private static int groundLayerMask
    {
        get
        {
            if (groundLayerMaskCache == -1)
            {
                groundLayerMaskCache = LayerMask.GetMask("Ground");

                // "Ground" 레이어가 없는 프로젝트에서는 모든 레이어를 봅니다.
                // 그래야 경사 계산이 조용히 0으로 굳지 않습니다.
                if (groundLayerMaskCache == 0) groundLayerMaskCache = ~0;
            }

            return groundLayerMaskCache;
        }
    }

    /// <summary>
    /// 사거리 안의 적 부대를 '접촉 없이' 탐지합니다.
    ///
    /// 왜 필요한가:
    /// 기존 탐지(army_Detected)는 물리 충돌로만 채워졌습니다.
    /// 즉 몸이 닿아야 적으로 인식하므로, 원거리 부대는 구조적으로
    /// 사거리 밖의 적을 공격할 수 없었습니다. 이건 미구현이 아니라
    /// 아키텍처 차원의 차단이었습니다.
    ///
    /// 여기서 시야 기반 탐지를 더해 그 벽을 걷어냅니다.
    /// 접촉 집계(Add_Contact)는 격자 계산이 매 틱 따로 채우며,
    /// 이 함수는 '아직 닿지 않은' 적을 추가로 등록할 뿐입니다.
    /// </summary>
    private void _Update_Detection()
    {
        // 이 부대가 실제로 닿을 수 있는 최대 거리입니다.
        float reach = army_Data.GetMeleeRange();

        if (army_Data.GetE_Unit_AttackType() == E_Unit_AttackType.Range
            || army_Data.IsRangeAttackAble())
        {
            float rangeRange = army_Data.GetEffectiveRangeRange();
            if (rangeRange > reach) reach = rangeRange;
        }

        // 근접 전용 부대의 탐지 거리입니다.
        //
        // 물리 콜백이 없으므로 근접 부대도 스스로 주변을 봐야 합니다.
        // 접촉 직전에 인식하도록 근접 사거리에 여유를 둡니다.
        // (충돌로만 인식하던 시절과 사실상 같은 타이밍입니다)
        if (reach <= army_Data.GetMeleeRange())
        {
            reach = army_Data.GetMeleeRange() + army_Data.GetRadius() * 4.0f;
        }

        float reachSqr = reach * reach;
        Vector3 myPosition = GetPosition();

        // 격자로 후보를 줄입니다.
        //
        // 예전에는 allArmies를 전수 순회했습니다. 부대 수 N에 대해
        // 이 함수 하나만으로 틱당 N^2번의 거리 계산이 발생합니다.
        // 격자는 반경에 걸치는 셀만 훑으므로 멀리 있는 부대는 아예 보지 않습니다.
        //
        // 주의: 격자는 후보를 줄일 뿐 판정을 대신하지 않습니다.
        // 셀 경계 때문에 반경 밖의 부대도 섞여 나오므로 거리 검사는 그대로 둡니다.
        List<Army> candidates = Query_Nearby(myPosition, reach);

        for (int i = 0; i < candidates.Count; i++)
        {
            Army other = candidates[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other.units.Count == 0) continue;
            if (other.army_Data.bplayer == army_Data.bplayer) continue;

            Vector3 to = other.GetPosition() - myPosition;
            to.y = 0.0f;

            if (to.sqrMagnitude > reachSqr)
            {
                // 사거리를 벗어났으면 시야 탐지분을 거둬들입니다.
                Remove_Army_Sighted(other);
                continue;
            }

            Add_Army_Sighted(other);
        }

        // 격자 밖으로 완전히 벗어난 부대의 시야 탐지분을 거둬들입니다.
        //
        // 위 루프는 '후보로 잡힌' 부대만 봅니다. 멀리 달아나 후보에서
        // 빠진 부대는 bsighted가 켜진 채 남으므로, 이미 등록된 항목을
        // 따로 확인해야 합니다. (항목 수는 보통 한 자릿수라 저렴합니다)
        for (int i = army_Detected.Count - 1; i >= 0; i--)
        {
            Army_Count detected = army_Detected[i];
            if (!detected.bsighted) continue;
            if (detected.army == null) { army_Detected.RemoveAt(i); continue; }

            Vector3 to = detected.army.GetPosition() - myPosition;
            to.y = 0.0f;

            if (to.sqrMagnitude > reachSqr) Remove_Army_Sighted(detected.army);
        }
    }

    /// <summary>
    /// 부대 단위 조회를 위한 전역 격자입니다.
    ///
    /// 정적인 이유: 모든 부대가 같은 틱에 같은 배치를 봐야 하므로
    /// 격자는 틱당 하나만 존재해야 합니다. Rebuild가 틱 번호로
    /// 중복 구축을 막으므로, 누가 먼저 부르든 결과가 같습니다.
    /// </summary>
    private static readonly Army_Grid armyGrid = new Army_Grid();

    /// <summary>
    /// 질의 결과를 담는 재사용 버퍼입니다.
    ///
    /// 질의마다 새 리스트를 만들면 격자로 아낀 것을 GC가 도로 가져갑니다.
    /// 메인 스레드에서만 쓰이므로 정적 하나로 충분합니다.
    /// </summary>
    private static readonly List<Army> queryBuffer = new List<Army>(32);

    /// <summary>
    /// 반경 안에 있을 가능성이 있는 부대들을 돌려줍니다.
    ///
    /// 반환된 리스트는 다음 질의에서 덮어써지는 공용 버퍼입니다.
    /// 결과를 보관해야 한다면 반드시 복사하십시오.
    /// </summary>
    private static List<Army> Query_Nearby(Vector3 center, float radius)
    {
        armyGrid.Rebuild(allArmies, Simulation_Clock.tick);
        armyGrid.Query(center, radius, queryBuffer);

        return queryBuffer;
    }

    /// <summary>부대 격자를 비웁니다. 씬 시작 시 호출합니다.</summary>
    public static void Clear_Army_Grid()
    {
        armyGrid.Clear();
    }

    /// <summary>
    /// 시야로 탐지한 부대를 등록합니다.
    /// 충돌 탐지와 달리 카운트를 누적하지 않고 '있다/없다'만 표시합니다.
    /// </summary>
    private void Add_Army_Sighted(Army army)
    {
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].bsighted = true;
                return;
            }
        }

        Army_Count count = new Army_Count(army, 0);
        count.bsighted = true;
        army_Detected.Add(count);
    }

    /// <summary>
    /// 시야에서 벗어난 부대의 탐지 표시를 지웁니다.
    /// 물리 접촉 카운트가 남아 있으면 항목 자체는 유지합니다.
    /// </summary>
    private void Remove_Army_Sighted(Army army)
    {
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army != army) continue;

            army_Detected[i].bsighted = false;

            // 접촉 카운트도 없으면 더 이상 탐지 대상이 아닙니다.
            if (army_Detected[i].num <= 0)
            {
                army_Detected.RemoveAt(i);
            }
            return;
        }
    }

    /// <summary>
    /// 이번 틱의 물리 접촉 카운트를 모두 비웁니다.
    ///
    /// 자체 충돌을 쓸 때 Controller가 매 틱 호출합니다.
    /// 시야 탐지분(bsighted)은 건드리지 않습니다. 그쪽은 별도로 관리됩니다.
    /// </summary>
    public void Clear_Contact_Counts()
    {
        if (army_Detected == null) return;

        for (int i = army_Detected.Count - 1; i >= 0; i--)
        {
            army_Detected[i].num = 0;

            // 접촉도 시야도 없으면 더 이상 탐지 대상이 아닙니다.
            if (!army_Detected[i].bsighted) army_Detected.RemoveAt(i);
        }
    }

    /// <summary>
    /// 이 부대의 피해 전달 실태를 계측기에 넘깁니다.
    ///
    /// 무엇을 재는가:
    /// 몸을 맞댄 적 부대가 몇 개이고, 그중 몇 개가 실제로 나에게 피해를
    /// 줄 수 있는지를 셉니다. 두 수가 같아야 정상입니다.
    ///
    /// 피해를 줄 수 있다는 것의 정의:
    /// <see cref="Collect_Fight_Armies"/>가 피해원으로 삼는 조건과 같아야
    /// 합니다. 그쪽이 좁아지면 이 지표가 즉시 0이 아니게 되어 드러납니다.
    /// 두 곳이 따로 놀지 않도록 판정을 <see cref="Is_Damage_Source"/> 하나로
    /// 모아 두었습니다.
    ///
    /// 시야 탐지(bsighted)만 된 상대는 접촉 수에 넣지 않습니다.
    /// 아직 칼이 닿지 않은 것은 정상이므로 '막혔다'고 볼 수 없습니다.
    /// </summary>
    public void Measure_Damage_Path()
    {
        if (army_Detected == null) return;

        int contact = 0;
        int sources = 0;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            Army_Count detected = army_Detected[i];

            if (detected.army == null) continue;
            if (detected.num <= 0) continue;
            if (detected.army.army_Data.bplayer == army_Data.bplayer) continue;

            contact++;
            if (Is_Damage_Source(detected.army)) sources++;
        }

        Tick_Profiler.Count_Engagement(contact, sources);

        // 이번 틱에 '때리는 데까지 성공한' 유닛 수를 셉니다.
        //
        // 이 자리에서만 셀 수 있습니다. bhitTarget은 Unit_Job이 세우고
        // 다음 틱의 _Update_Fight가 첫 줄에서 내리므로, Apply가 끝난
        // 지금이 이번 틱의 값을 볼 수 있는 유일한 시점입니다.
        //
        // 이 값과 Tick_Profiler.damageApplied를 나란히 보면
        // "공격은 성립하는데 피해가 전달되지 않는" 상태가 드러납니다.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            if (units[i].unit_Data.bhitTarget) Tick_Profiler.Count_Attack_Landed();
        }
    }

    /// <summary>
    /// 상대 부대와의 접촉을 1 늘립니다.
    /// OnCollisionEnter를 대신해 자체 충돌 결과로 호출됩니다.
    /// </summary>
    public void Add_Contact(Army army)
    {
        if (army == null) return;
        if (this == army) return;
        if (army.army_Data.bplayer == army_Data.bplayer) return;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].num++;
                return;
            }
        }

        army_Detected.Add(new Army_Count(army, 1));
    }

    // Add_Army_Detected / Remove_Army_Detected는 제거되었습니다.
    //
    // 물리 충돌 콜백(OnCollisionEnter/Exit)에서만 호출되던 함수들입니다.
    // 접촉 집계는 이제 Clear_Contact_Counts() + Add_Contact()가 매 틱
    // 격자 계산 결과로 다시 세우고, 시야 탐지는 _Update_Detection()이
    // 담당합니다. 두 경로 모두 위에 있습니다.
}
