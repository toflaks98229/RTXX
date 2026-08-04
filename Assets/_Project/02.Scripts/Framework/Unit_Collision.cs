using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// 전 유닛의 겹침을 격자로 해소하는 자체 충돌 시스템입니다.
///
/// 왜 물리 엔진을 대신하는가:
/// 유닛마다 Rigidbody + CapsuleCollider를 달고 PhysX에 맡기면
/// 4800명 기준 틱당 11.5ms가 물리에만 들어갑니다. (전체의 40%)
/// 그런데 이 게임이 물리에서 실제로 필요로 하는 것은
/// '두 병사가 같은 자리에 겹치지 않게 밀어내기' 하나뿐입니다.
/// 관절도, 마찰도, 회전 관성도 쓰지 않습니다.
///
/// 그 하나만 직접 계산하면 훨씬 쌉니다. 이미 있는 Spatial_Grid로
/// 인접 유닛만 추려 반지름 합보다 가까운 쌍을 밀어내면 끝입니다.
///
/// 물리 엔진과 다른 점:
/// - 질량비를 반영해 무거운 쪽이 덜 밀립니다. (기병이 보병을 밀어냄)
/// - 속도/관성이 없어 튕겨 나가거나 진동하지 않습니다.
///   대열이 흔들리지 않아 오히려 진형이 안정적입니다.
/// - 넉백은 별도 임펄스로 처리합니다. (Unit_Data.knockback)
/// </summary>
public static class Unit_Collision
{
    /// <summary>
    /// 한 틱에 허용하는 최대 분리 이동량입니다.
    /// 깊게 겹친 유닛이 한 번에 튕겨 나가지 않도록 상한을 둡니다.
    /// </summary>
    public const float maxSeparationPerTick = 0.35f;

    /// <summary>
    /// 겹침을 한 번에 얼마나 해소할지의 비율입니다.
    /// 1.0이면 즉시 완전 분리라 딱딱하고, 낮으면 부드럽지만 겹침이 남습니다.
    /// </summary>
    public const float separationRate = 0.5f;
}

/// <summary>
/// 전역 유닛 배열을 격자에 색인하는 잡입니다.
/// Spatial_Grid_Build_Job과 달리 '모든 부대'의 유닛을 한 배열로 다룹니다.
/// </summary>
[BurstCompile]
public struct Collision_Grid_Build_Job : IJobParallelFor
{
    /// <summary>색인할 전 유닛의 충돌 정보입니다.</summary>
    [ReadOnly] public NativeArray<Collision_Body> bodies;

    /// <summary>
    /// 격자 셀 크기입니다.
    /// '가장 큰 두 유닛이 닿을 수 있는 거리'와 같아야 3x3 탐색이 정확합니다.
    /// </summary>
    public float cellSize;

    /// <summary>해시 키 -> 유닛 인덱스 맵입니다. 여러 워커가 동시에 씁니다.</summary>
    public NativeParallelMultiHashMap<int, int>.ParallelWriter grid;

    /// <summary>해소 잡의 안쪽 루프가 쓸 조밀 위치 배열입니다. (출력)</summary>
    [WriteOnly] [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> positions;
    /// <summary>조밀 반지름 배열입니다. (출력)</summary>
    [WriteOnly] [NativeDisableParallelForRestriction]
    public NativeArray<float> radii;

    /// <summary>조밀 진영 배열입니다. (출력) true면 플레이어 측입니다.</summary>
    [WriteOnly] [NativeDisableParallelForRestriction]
    public NativeArray<bool> sides;

    /// <summary>유닛 하나를 격자에 넣고 조밀 배열에도 값을 복사합니다.</summary>
    /// <param name="index">처리할 유닛의 인덱스입니다.</param>
    public void Execute(int index)
    {
        Collision_Body body = bodies[index];

        // 죽은 유닛도 배열은 채웁니다.
        // 해소 잡이 인덱스로 바로 읽으므로 빈칸이 있으면 안 됩니다.
        // (격자에는 넣지 않으므로 이웃으로 잡히지는 않습니다)
        positions[index] = body.position;
        radii[index] = body.bdead ? 0.0f : body.radius;
        sides[index] = body.bplayer;

        if (body.bdead) return;

        grid.Add(Spatial_Grid.Hash(body.position, cellSize), index);
    }
}

/// <summary>
/// 충돌 계산에 필요한 최소 정보입니다.
/// Unit_Data 전체를 넘기면 캐시 효율이 떨어지므로 필요한 것만 추립니다.
/// </summary>
public struct Collision_Body
{
    /// <summary>현재 위치입니다.</summary>
    public Vector3 position;
    /// <summary>충돌 반지름입니다.</summary>
    public float radius;
    /// <summary>질량입니다. 무거울수록 덜 밀립니다.</summary>
    public float mass;
    /// <summary>사망 여부입니다. 죽은 유닛은 충돌하지 않습니다.</summary>
    public bool bdead;
    /// <summary>플레이어 소속 여부입니다. 적/아군 구분에 씁니다.</summary>
    public bool bplayer;
    /// <summary>소속 부대 인덱스입니다. 접촉 집계에 씁니다.</summary>
    public int armyIndex;

    /// <summary>이번 틱에 계산된 분리 이동량입니다. (출력)</summary>
    public Vector3 separation;
    /// <summary>이번 틱에 적과 접촉했는지 여부입니다. (출력)</summary>
    public bool benemyContact;
    /// <summary>나를 막고 있는 적 방향입니다. (출력, 정규화)</summary>
    public Vector3 enemyContactNormal;

    /// <summary>
    /// 접촉한 적이 속한 부대 인덱스입니다. 없으면 -1입니다. (출력)
    ///
    /// Job이 직접 기록합니다. 예전에는 메인 스레드가 접촉 유닛마다
    /// 전 부대를 순회하며 가장 가까운 적 부대를 찾았는데,
    /// 그 비용이 O(접촉 유닛 수 x 부대 수)로 커졌습니다.
    /// 어차피 충돌 판정에서 상대를 알고 있으므로 그때 적어 두면 O(1)입니다.
    /// </summary>
    public int contactArmyIndex;
}

/// <summary>
/// 격자를 이용해 겹친 유닛 쌍을 찾아 밀어냅니다.
///
/// 각 유닛이 자기 주변 3x3 셀만 보고 '자기가 받을 밀림'을 스스로 계산합니다.
/// 상대를 건드리지 않으므로 병렬 쓰기 충돌이 없습니다.
/// (양쪽이 서로를 밀어내는 것은 각자가 자기 몫을 계산해 자연히 성립합니다)
/// </summary>
[BurstCompile]
public struct Collision_Resolve_Job : IJobParallelFor
{
    /// <summary>
    /// 유닛들의 충돌 정보입니다. 각 항목은 자기 인덱스만 쓰므로 병렬 제한을 해제합니다.
    /// </summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<Collision_Body> bodies;

    /// <summary>
    /// 위치만 따로 담은 조밀 배열입니다.
    ///
    /// 안쪽 루프는 이웃의 '위치'만 반복해서 읽습니다. 그런데 Collision_Body는
    /// 60바이트가 넘어, 이웃 하나를 볼 때마다 캐시 라인을 통째로 끌어옵니다.
    /// 난전에서는 이웃 수가 많아 그 대역폭이 그대로 비용이 됩니다.
    /// 위치(12바이트)만 따로 두면 한 캐시 라인에 5개가 들어옵니다.
    /// </summary>
    [ReadOnly] public NativeArray<Vector3> positions;

    /// <summary>반지름만 따로 담은 조밀 배열입니다. 같은 이유입니다.</summary>
    [ReadOnly] public NativeArray<float> radii;

    /// <summary>
    /// 진영만 따로 담은 조밀 배열입니다. true면 플레이어 측입니다.
    ///
    /// 왜 따로 두는가:
    /// 적과 아군의 판정 반지름을 다르게 하려면 **겹침을 판정하기 전에**
    /// 진영을 알아야 합니다. 그런데 Collision_Body는 60바이트가 넘어,
    /// 그것을 읽는 순간 1단계 필터(무거운 구조체를 안 읽는 것)의 이점이
    /// 사라집니다. bool 1바이트짜리 배열이면 캐시 라인 하나에 64개가
    /// 들어오므로 사실상 공짜입니다.
    /// </summary>
    [ReadOnly] public NativeArray<bool> sides;

    /// <summary>이웃을 빠르게 찾기 위한 공간 격자입니다.</summary>
    [ReadOnly] public NativeParallelMultiHashMap<int, int> grid;

    /// <summary>격자 셀 크기입니다. 구축 잡과 반드시 같은 값이어야 합니다.</summary>
    public float cellSize;

    /// <summary>
    /// 유닛 하나의 겹침을 해소하고 적 접촉 여부를 판정합니다.
    ///
    /// 주변 3x3 셀만 훑습니다. 셀 크기가 최대 상호작용 거리와 같으므로
    /// 그 범위를 벗어난 유닛은 애초에 닿을 수 없습니다.
    /// </summary>
    /// <param name="index">처리할 유닛의 인덱스입니다.</param>
    public void Execute(int index)
    {
        Collision_Body me = bodies[index];

        me.separation = Vector3.zero;
        me.benemyContact = false;
        me.enemyContactNormal = Vector3.zero;
        me.contactArmyIndex = -1;

        if (me.bdead) { bodies[index] = me; return; }

        Spatial_Grid.ToCell(me.position, cellSize, out int cx, out int cz);

        Vector3 push = Vector3.zero;
        Vector3 contactSum = Vector3.zero;
        int contactCount = 0;

        // 가장 가까운(가장 깊게 겹친) 적의 부대를 접촉 상대로 봅니다.
        int nearestFoeArmy = -1;
        float nearestFoeSqr = float.MaxValue;

        // 내 반지름 기준 최대 상호작용 거리입니다.
        // 이 값을 미리 제곱해 두면 안쪽 루프에서 곱셈을 반복하지 않습니다.
        float myR = me.radius;

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int hash = Spatial_Grid.Hash(cx + dx, cz + dz);

                if (!grid.TryGetFirstValue(hash, out int other, out var it)) continue;

                do
                {
                    if (other == index) continue;

                    // 1단계: 위치와 반지름만 읽어 겹침을 판정합니다.
                    //
                    // 대부분의 이웃은 겹치지 않습니다. 여기서 걸러내면
                    // 60바이트짜리 Collision_Body를 아예 읽지 않아도 됩니다.
                    // 위치(12바이트)만 담은 조밀 배열이라 한 캐시 라인에
                    // 5개가 들어와, 난전처럼 이웃이 많을 때 효과가 큽니다.
                    Vector3 yourPos = positions[other];

                    float dxp = me.position.x - yourPos.x;
                    float dzp = me.position.z - yourPos.z;
                    float distSqr = dxp * dxp + dzp * dzp;

                    // 적은 더 큰 반지름으로 판정합니다.
                    //
                    // 왜 그런가:
                    // 이것이 '적과 아군이 걸리는 충돌체를 다르게' 만드는
                    // 실질입니다. 판정 반지름을 키우면 실제로 몸이 닿기
                    // 전에 먼저 밀리기 시작하므로, 전진 명령이 세도
                    // 적 사이로 비집고 들어갈 틈이 줄어듭니다.
                    //
                    // 콜라이더 레이어를 나누지 않는 이유:
                    // 그러면 격자를 두 번 순회해야 합니다. 9,600 유닛에서
                    // 비용이 두 배가 됩니다. 같은 순회 안에서 계수만
                    // 바꾸면 비용이 늘지 않습니다.
                    //
                    // 여기서 bodies[other]를 미리 읽지 않는 것에 주의하십시오.
                    // 1단계 필터의 목적이 '무거운 구조체를 안 읽는 것'이라,
                    // 진영 판정을 위해 읽어 버리면 최적화가 무너집니다.
                    // 그래서 진영은 조밀 배열(sides)에서 읽습니다.
                    bool bfoe = sides[other] != me.bplayer;

                    float sum = myR + radii[other];
                    if (bfoe) sum *= Constant.collide_Enemy_Radius_Rate;

                    if (distSqr >= sum * sum) continue;

                    // 2단계: 실제로 겹친 상대만 전체 정보를 읽습니다.
                    Collision_Body you = bodies[other];
                    if (you.bdead) continue;

                    Vector3 to = new Vector3(dxp, 0.0f, dzp);
                    if (distSqr < 0.000001f)
                    {
                        // 정확히 겹쳤습니다. 인덱스로 방향을 갈라 서로 반대로 밀어냅니다.
                        // (그대로 두면 영원히 겹친 채 멈춥니다)
                        float sign = index < other ? 1.0f : -1.0f;
                        push += new Vector3(sign * 0.01f, 0.0f, 0.0f);
                        continue;
                    }

                    float dist = Mathf.Sqrt(distSqr);
                    Vector3 dir = to / dist;
                    float overlap = sum - dist;

                    // 질량비: 무거운 쪽이 덜 밀립니다.
                    // 상대가 무거울수록 내가 많이 밀려납니다.
                    float total = me.mass + you.mass;
                    float share = total > 0.0001f ? you.mass / total : 0.5f;

                    float rate = Unit_Collision.separationRate;

                    if (bfoe)
                    {
                        // 적은 더 세게 튕겨냅니다. 겹침이 빨리 풀릴수록
                        // 파고들 틈이 줄어듭니다.
                        rate *= Constant.collide_Enemy_Separation_Rate;

                        // 다만 질량이 크게 앞서면 뚫고 들어갈 수 있어야
                        // 합니다. 그것이 돌격의 의미입니다. 토탈워에서도
                        // 질량이 큰 유닛은 대열을 헤집고 후방으로 들어갑니다.
                        //
                        // 완전히 막으면 기병 돌격이 보병 벽에 부딪혀
                        // 그대로 서 버립니다.
                        if (you.mass > 0.0001f
                            && me.mass / you.mass >= Constant.collide_Push_Through_Mass_Rate)
                        {
                            rate *= Constant.collide_Push_Through_Block_Rate;
                        }
                    }

                    push += dir * (overlap * share * rate);

                    // 적과 닿았는지 기록합니다. (전진 차단 판정에 씁니다)
                    if (bfoe)
                    {
                        contactSum += -dir;   // 적이 있는 방향
                        contactCount++;

                        // 가장 가까운 적의 부대를 기억해 둡니다.
                        // 메인 스레드가 다시 찾지 않아도 되도록 여기서 정합니다.
                        if (distSqr < nearestFoeSqr)
                        {
                            nearestFoeSqr = distSqr;
                            nearestFoeArmy = you.armyIndex;
                        }
                    }
                }
                while (grid.TryGetNextValue(out other, ref it));
            }
        }

        // 한 틱에 너무 많이 밀리지 않도록 상한을 겁니다.
        float pushLen = push.magnitude;
        if (pushLen > Unit_Collision.maxSeparationPerTick)
        {
            push = push / pushLen * Unit_Collision.maxSeparationPerTick;
        }

        me.separation = push;

        if (contactCount > 0)
        {
            me.benemyContact = true;
            Vector3 n = contactSum / contactCount;
            me.enemyContactNormal = n.sqrMagnitude > 0.000001f ? n.normalized : Vector3.zero;
            me.contactArmyIndex = nearestFoeArmy;
        }

        bodies[index] = me;
    }
}
