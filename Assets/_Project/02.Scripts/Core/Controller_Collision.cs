using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Controller의 "유닛 겹침 해소와 Transform 동기화" 책임을 담당하는 부분 클래스입니다.
///
/// 왜 파일을 나누는가:
/// 이 영역은 시뮬레이션 틱의 다른 부분과 성격이 다릅니다. 부대 상태 머신이
/// '무엇을 할지'를 정하는 것이라면, 여기는 그 결과를 물리적으로 성립시키고
/// 화면에 내려보내는 계층입니다. 관심사가 다르고, 손대는 이유도 다릅니다.
///
/// partial로 나눈 이유:
/// 별도 클래스로 빼면 Controller가 들고 있는 units/armies/transformSync를
/// 전부 인자로 넘겨야 해 호출부가 오히려 복잡해집니다. Controller_Selection과
/// Controller_Formation이 이미 같은 방식이므로 일관성도 유지됩니다.
/// </summary>
partial class Controller
{
    // =====================================================================
    // 자체 충돌 (Rigidbody 대체)
    // =====================================================================

    /// <summary>충돌 계산용 전역 유닛 버퍼입니다. 매 틱 재사용합니다.</summary>
    private NativeArray<Collision_Body> collisionBodies;

    /// <summary>
    /// 충돌 공간 격자입니다. 매 틱 새로 만들지 않고 Clear해서 재사용합니다.
    /// 할당/해제 비용이 인원수에 비례해 커지기 때문입니다.
    /// </summary>
    private NativeParallelMultiHashMap<int, int> collisionGrid;

    /// <summary>충돌 안쪽 루프용 조밀 위치 배열입니다. 캐시 효율을 위해 분리했습니다.</summary>
    private NativeArray<Vector3> collisionPositions;
    /// <summary>충돌 안쪽 루프용 조밀 반지름 배열입니다.</summary>
    private NativeArray<float> collisionRadii;

    /// <summary>
    /// 진영만 담은 조밀 배열입니다. 적/아군 판정 반지름을 다르게 하는 데 씁니다.
    ///
    /// 위치·반지름과 같은 이유로 따로 둡니다. 겹침 판정 전에 진영을
    /// 알아야 하는데, 그때 Collision_Body(60바이트+)를 읽으면 조밀 배열을
    /// 도입한 이유가 사라집니다.
    /// </summary>
    private NativeArray<bool> collisionSides;

    /// <summary>부대별 충돌 반지름 캐시입니다. 유닛마다 부대 스탯을 다시 읽지 않기 위함입니다.</summary>
    private float[] armyRadius;
    /// <summary>부대별 질량 캐시입니다.</summary>
    private float[] armyMass;

    /// <summary>
    /// 전 유닛 Transform을 Job으로 일괄 처리하는 동기화 계층입니다.
    /// Transform 접근이 틱 비용의 26%를 차지해 이 경로로 옮겼습니다.
    /// </summary>
    private readonly Unit_Transform_Sync transformSync = new Unit_Transform_Sync();

    /// <summary>동기화 배열을 다시 만들어야 하는지 여부입니다. (유닛 사망 시 등)</summary>
    private bool btransformSyncDirty = true;

    /// <summary>
    /// 이번 프레임에 사망한 유닛 수입니다.
    /// 0보다 크면 Transform이 파괴되었으므로 동기화 배열을 다시 만들어야 합니다.
    /// GameEvents.OnUnitKilled 구독으로 채워집니다.
    /// </summary>
    private int deadThisFrame;

    /// <summary>
    /// 유닛 목록이 바뀌었음을 알립니다.
    /// 다음 틱에 Transform 동기화 배열을 다시 만듭니다.
    /// </summary>
    public void Invalidate_Transform_Sync()
    {
        btransformSyncDirty = true;
    }

    /// <summary>
    /// 부대가 파괴되었을 때 호출됩니다.
    ///
    /// 등록소에서 빼고 색인을 dirty로 표시합니다. 실제 재구축은 다음 틱
    /// 시작(_Update_Army)에서 일어나므로, 이 틱의 진행 중인 계산은
    /// 일관된 색인을 계속 봅니다.
    /// </summary>
    private void On_Army_Destroyed(Army army)
    {
        armyRegistry.Unregister(army);
        armies.Remove(army);
    }

    /// <summary>
    /// 유닛이 죽었을 때 호출됩니다.
    /// Transform이 파괴되므로 동기화 배열을 다시 만들어야 합니다.
    /// </summary>
    private void On_Unit_Killed_Sync(Unit unit, Army victimArmy, Army killerArmy)
    {
        if (unit == null) return;

        // 죽은 유닛의 자리만 비웁니다.
        //
        // 전체를 다시 만들면(Rebuild) 전 유닛을 재등록하므로,
        // 사망이 잦은 전투 중에 틱이 40~60 ms까지 튑니다.
        // 실측에서 840틱 중 215틱이 30 ms를 넘었고 전부 사망 틱이었습니다.
        // 한 칸만 null로 바꾸면 O(1)이고 인덱스 정렬도 유지됩니다.
        //
        // 반드시 simIndex를 써야 합니다. num은 '이 유닛이 누구인가'이지
        // '배열 어디에 있는가'가 아닙니다. 두 값은 지금은 우연히 같지만,
        // 유닛 목록이 한 번이라도 재구성되면 갈라집니다.
        int index = unit.unit_Data.simIndex;

        if (index >= 0 && index < units.Count && units[index] == unit)
        {
            transformSync.Clear_At(index);
            return;
        }

        // 인덱스가 어긋난 예외적인 경우에만 전체 재구축으로 되돌립니다.
        btransformSyncDirty = true;
    }

    /// <summary>이번 틱에 갱신할 부대인지 여부입니다. LOD 판정 결과입니다.</summary>
    private bool[] bupdateArmy;

    /// <summary>LOD 틱 카운터입니다.</summary>
    private int lodTick;

    /// <summary>
    /// 이번 틱에 갱신할 부대를 고릅니다.
    ///
    /// 교전 중이거나 무너진 부대는 항상 갱신합니다. 전투 결과와 직결되므로
    /// 건너뛰면 눈에 보이는 차이가 납니다.
    /// 나머지(행군 중이거나 대기 중인 부대)만 인덱스로 나눠 분산합니다.
    /// </summary>
    private void _Select_Armies_To_Update()
    {
        int n = armies.Count;

        if (bupdateArmy == null || bupdateArmy.Length < n)
            bupdateArmy = new bool[Mathf.Max(n, 16)];

        int interval = idleArmyTickInterval;
        if (interval < 1) interval = 1;

        lodTick++;

        for (int i = 0; i < n; i++)
        {
            Army a = armies[i];
            if (a == null) { bupdateArmy[i] = false; continue; }

            if (interval == 1) { bupdateArmy[i] = true; continue; }

            ref Army_Data d = ref a.army_Data;

            // 건너뛸 수 있는 부대는 '움직이지도 싸우지도 않는' 부대뿐입니다.
            //
            // 이동 중인 부대를 건너뛰면 그 틱의 이동량이 통째로 사라져
            // 실제 행군 속도가 느려집니다. (틱 간격만큼 보정하려면
            // Army_Move 전체의 시간 항을 고쳐야 해 위험이 큽니다)
            // 그래서 Idle이면서 교전도 탐지도 없는 부대만 분산합니다.
            //
            // 대기 중인 부대는 진형 재정비 정도만 하므로 몇 틱 늦어도
            // 눈에 띄지 않고, 전투 결과에도 영향이 없습니다.
            bool bskippable = d.e_Army_Move == E_Army_Move.Idle
                           && d.e_Army_Fight == E_Army_Fight.Non
                           && !d.IsBroken()
                           && a.army_Detected.Count == 0;

            bupdateArmy[i] = !bskippable || ((lodTick + i) % interval == 0);

            // 계산 주기 조절이 실제로 몇 개를 건너뛰는지 셉니다.
            if (!bupdateArmy[i]) Tick_Profiler.Count_Army_Skipped();
        }
    }

    /// <summary>
    /// 전 유닛의 겹침을 격자로 해소합니다.
    ///
    /// 왜 물리 엔진을 쓰지 않는가:
    /// PhysX에 맡기면 4800명 기준 틱당 11.5ms(전체의 40%)가 물리에 들어갑니다.
    /// 그런데 이 게임이 물리에서 실제로 필요로 하는 것은 '겹치지 않게 밀어내기'
    /// 하나뿐입니다. 관절도 마찰도 회전 관성도 쓰지 않습니다.
    /// 그 하나만 직접 계산하면 훨씬 쌉니다.
    /// </summary>
    private void _Update_Collision()
    {
        int count = units.Count;
        if (count == 0) return;

        // 버퍼 확보 (재사용)
        if (!collisionBodies.IsCreated || collisionBodies.Length < count)
        {
            if (collisionBodies.IsCreated) collisionBodies.Dispose();
            collisionBodies = new NativeArray<Collision_Body>(
                Mathf.Max(count, 64), Allocator.Persistent);
        }

        var bodies = collisionBodies.GetSubArray(0, count);

        // Transform 동기화 배열을 준비합니다.
        //
        // 유닛이 죽어 Destroy되면 units[i]가 null이 되고, 그 자리의 Transform도
        // 파괴됩니다. 파괴된 Transform이 배열에 남아 있으면 Job이 예외를 던지므로
        // 사망이 발생한 틱에는 반드시 다시 만들어야 합니다.
        //
        // 사망은 Clear_At으로 그 칸만 비우므로 재구축이 필요 없습니다.
        // 길이가 달라졌을 때만(유닛이 추가되었을 때) 다시 만듭니다.
        if (btransformSyncDirty || transformSync.Count != count)
        {
            transformSync.Rebuild(units);
            btransformSyncDirty = false;
        }

        bool bsync = transformSync.IsCreated && transformSync.Count == count;

        // Transform을 되읽지 않습니다.
        //
        // unit_Data.position이 위치의 유일한 주인입니다.
        // 시뮬레이션이 그 값을 만들고, 틱 마지막에 Transform으로 내려보냅니다.
        // 그러니 다시 읽어 올 이유가 없습니다. (9,600유닛 기준 1.8 ms 절약)

        // 1. 현재 상태를 모읍니다.
        //
        //    반지름과 질량은 '부대' 스탯이라 유닛마다 같습니다.
        //    유닛마다 GetArmy_Data()를 부르면 구조체 복사가 인원수만큼 일어나므로,
        //    부대별로 한 번만 읽어 두고 인덱스로 꺼내 씁니다.
        float maxRadius = 0.0f;

        int armyCount = armies.Count;
        if (armyRadius == null || armyRadius.Length < armyCount)
        {
            armyRadius = new float[Mathf.Max(armyCount, 16)];
            armyMass = new float[Mathf.Max(armyCount, 16)];
        }

        for (int a = 0; a < armyCount; a++)
        {
            if (armies[a] == null) { armyRadius[a] = 0.0f; armyMass[a] = 1.0f; continue; }

            armyRadius[a] = armies[a].army_Data.GetRadius();
            armyMass[a] = armies[a].army_Data.GetMass();

            if (armyRadius[a] > maxRadius) maxRadius = armyRadius[a];
        }

        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.C_Gather);

        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null)
            {
                bodies[i] = new Collision_Body { bdead = true, contactArmyIndex = -1 };
                continue;
            }

            int ai = u.unit_Data.armyIndex;
            bool bvalid = ai >= 0 && ai < armyCount;

            bodies[i] = new Collision_Body
            {
                // 시뮬레이션이 들고 있는 값이 곧 현재 위치입니다.
                position = u.unit_Data.position,
                radius = bvalid ? armyRadius[ai] : 0.3f,
                mass = bvalid ? armyMass[ai] : 1.0f,
                bdead = u.IsDead(),
                bplayer = u.unit_Data.bPlayer,
                armyIndex = ai,
                contactArmyIndex = -1
            };
        }

        Tick_Profiler.End_Sub();

        // 셀 크기는 '가장 큰 두 유닛이 닿을 수 있는 거리'와 정확히 같게 잡습니다.
        //
        // 이보다 크면 3x3 탐색이 필요 이상으로 넓은 범위를 훑어
        // 겹치지도 않을 이웃을 잔뜩 검사합니다. 난전에서 그 비용이 큽니다.
        // 이보다 작으면 3x3이 상호작용 거리를 못 덮어 겹침을 놓칩니다.
        //
        // minCellSize 하한은 좌표가 폭주하는 것을 막기 위한 것이며,
        // 유닛 반지름이 0에 수렴하는 비정상 설정에서만 걸립니다.
        float cellSize = Mathf.Max(maxRadius * 2.0f, Spatial_Grid.minCellSize);

        // 2. 격자 구축 -> 겹침 해소
        //
        //    격자는 매 틱 새로 만들지 않고 재사용합니다.
        //    NativeParallelMultiHashMap 할당/해제는 인원수에 비례해 커지는데,
        //    Clear()는 훨씬 쌉니다. (용량이 모자랄 때만 다시 만듭니다)
        if (!collisionGrid.IsCreated || collisionGrid.Capacity < count)
        {
            if (collisionGrid.IsCreated) collisionGrid.Dispose();
            collisionGrid = new NativeParallelMultiHashMap<int, int>(
                Mathf.Max(count, 64), Allocator.Persistent);
        }
        else
        {
            collisionGrid.Clear();
        }

        // 안쪽 루프가 쓸 조밀 배열입니다. (위치/반지름/진영만)
        if (!collisionPositions.IsCreated || collisionPositions.Length < count)
        {
            if (collisionPositions.IsCreated) collisionPositions.Dispose();
            if (collisionRadii.IsCreated) collisionRadii.Dispose();
            if (collisionSides.IsCreated) collisionSides.Dispose();

            int cap = Mathf.Max(count, 64);
            collisionPositions = new NativeArray<Vector3>(cap, Allocator.Persistent);
            collisionRadii = new NativeArray<float>(cap, Allocator.Persistent);
            collisionSides = new NativeArray<bool>(cap, Allocator.Persistent);
        }

        var densePos = collisionPositions.GetSubArray(0, count);
        var denseRad = collisionRadii.GetSubArray(0, count);
        var denseSide = collisionSides.GetSubArray(0, count);

        var build = new Collision_Grid_Build_Job
        {
            bodies = bodies,
            cellSize = cellSize,
            grid = collisionGrid.AsParallelWriter(),
            positions = densePos,
            radii = denseRad,
            sides = denseSide
        };
        JobHandle buildHandle = build.Schedule(count, Constant.jobBatchCount);

        var resolve = new Collision_Resolve_Job
        {
            bodies = bodies,
            positions = densePos,
            radii = denseRad,
            sides = denseSide,
            grid = collisionGrid,
            cellSize = cellSize
        };
        Tick_Profiler.Begin(Tick_Profiler.Phase.Collision);
        resolve.Schedule(count, Constant.jobBatchCount, buildHandle).Complete();
        Tick_Profiler.End();

        // 2-1. 지면 높이 조회를 걸어 둡니다.
        //
        //      아래의 결과 반영 루프가 도는 동안 워커 스레드에서 함께 돌므로
        //      사실상 공짜입니다. 실제 반영은 그 루프가 끝난 뒤에 합니다.
        //
        //      왜 여기인가: 충돌 해소가 XZ 위치를 확정한 '뒤'여야 그 자리의
        //      지면 높이를 묻는 것이 맞습니다. 밀려나기 전 위치로 물으면
        //      한 틱 어긋난 높이를 얻습니다.
        _Schedule_Ground_Sync(count);

        // 3. 결과를 유닛에 반영합니다.
        //
        //    Transform 쓰기는 배열에만 담고, 마지막에 Job으로 한 번에 씁니다.
        //    여기서 유닛마다 transform.position에 대입하면 네이티브 왕복이
        //    인원수만큼 다시 발생합니다.
        Tick_Profiler.Begin_Sub(Tick_Profiler.Phase.C_Writeback);

        // 지역 변수로 미리 꺼내 둡니다.
        //
        // transformSync.positions 같은 프로퍼티 접근은 루프 안에서 매번
        // 일어나면 그 자체가 비용입니다. 9,600명 x 5개 배열이면
        // 틱당 48,000번의 프로퍼티 조회입니다.
        var syncPositions = transformSync.positions;
        var syncRotations = transformSync.rotations;
        var syncSpriteRot = transformSync.spriteRotations;
        var syncSpriteScale = transformSync.spriteScales;
        var syncSpriteLocal = transformSync.spriteLocalPositions;

        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            Collision_Body b = bodies[i];

            // unit_Data를 한 번만 읽고 한 번만 씁니다.
            //
            // 예전에는 u.unit_Data를 최대 다섯 번 따로 건드렸습니다.
            // 264바이트 구조체라 접근마다 필드 오프셋 계산이 붙습니다.
            // 지역 복사본에서 처리하고 마지막에 한 번 되돌려 씁니다.
            ref Unit_Data data = ref u.unit_DataRef;

            if (b.bdead)
            {
                // 죽은 유닛도 배열에는 현재 값을 유지해야 Write_Transforms가
                // 엉뚱한 자리로 옮기지 않습니다.
                if (bsync) syncRotations[i] = data.rotation;
                continue;
            }

            Vector3 p = b.position;
            if (b.separation.sqrMagnitude > 0.0000001f)
            {
                p += b.separation;
                data.position = p;
            }

            if (bsync)
            {
                syncPositions[i] = p;
                syncRotations[i] = data.rotation;

                // 스프라이트도 함께 담습니다. (카메라를 향한 자세, 반동, 내지르기)
                var anim = u.unit_Animation;
                if (anim != null)
                {
                    syncSpriteRot[i] = anim.unit_Animation_Data.rotation;
                    syncSpriteScale[i] = anim.spriteScale;
                    syncSpriteLocal[i] = anim.spriteLocalPosition;
                }
            }

            // 적 접촉 정보를 계산 결과에서 채웁니다.
            // 물리 콜백(OnCollisionStay) 없이 격자 판정만으로 얻습니다.
            data.benemyContact = b.benemyContact;
            data.enemyContactNormal = b.enemyContactNormal;
        }

        Tick_Profiler.End_Sub();

        // 3-1. 지면 높이를 반영합니다.
        //
        //      위 루프가 XZ를 확정했으므로 이제 Y를 얹습니다.
        //      Transform에 쓰기 '전에' 해야 이번 틱에 바로 반영됩니다.
        Tick_Profiler.Begin(Tick_Profiler.Phase.GroundSync);
        _Complete_Ground_Sync(count, bsync);
        Tick_Profiler.End();

        // 4. 위치/회전과 스프라이트를 Job으로 일괄 반영합니다.
        //    이 두 번의 호출이 유닛마다 하던 Transform 쓰기 전부를 대체합니다.
        // 본체와 스프라이트를 하나의 대기 지점으로 묶습니다.
        // 두 Job은 서로 다른 Transform 집합을 건드려 의존성이 없으므로,
        // 각각 기다리면 스케줄 왕복만 두 번 내는 셈입니다.
        Tick_Profiler.Begin(Tick_Profiler.Phase.TransformWrite);
        if (bsync) transformSync.Write_All();
        Tick_Profiler.End();

        Tick_Profiler.Begin(Tick_Profiler.Phase.Contact);
        _Update_Army_Contact();
        Tick_Profiler.End();
    }

    // =====================================================================
    // 지면 높이 동기화
    // =====================================================================

    /// <summary>전 유닛을 지면 높이에 맞추는 계층입니다.</summary>
    private readonly Unit_Ground_Sync groundSync = new Unit_Ground_Sync();

    /// <summary>이번 틱에 지면 조회를 시작한 인덱스입니다.</summary>
    private int groundSyncOffset;

    /// <summary>이번 틱에 조회한 유닛 수입니다.</summary>
    private int groundSyncCount;

    /// <summary>
    /// 지면 높이를 몇 틱에 나눠 갱신할지입니다.
    ///
    /// 지면은 유닛이 움직인 만큼만 달라지므로 매 틱 전수 조회할 필요가 없습니다.
    /// 4로 두면 한 틱에 1/4씩 돌아 레이캐스트 비용이 1/4로 줄고,
    /// 한 유닛은 4틱(약 0.067초)마다 높이가 갱신됩니다.
    /// 걷는 속도에서는 눈에 띄지 않습니다.
    /// </summary>
    [Tooltip("지면 높이를 몇 틱에 나눠 갱신할지입니다. 1이면 매 틱 전원 갱신합니다.")]
    [Range(1, 8)]
    public int groundSyncInterval = 4;

    /// <summary>
    /// 지면 높이 동기화를 끕니다. 대조 실험용입니다.
    ///
    /// 끄면 생성 시 1회 스냅만 남으므로, 유닛이 이동하는 순간부터 Y가
    /// 고정된 채 언덕을 통과합니다. 이 계층이 실제로 일을 하는지
    /// 확인할 때만 켜십시오. 평소에는 반드시 꺼 두어야 합니다.
    /// </summary>
    [Tooltip("지면 높이 동기화를 끕니다. 대조 실험용이며 평소에는 꺼 두십시오.")]
    public bool bdisableGroundSync;

    /// <summary>지면 높이 조회를 걸어 둡니다.</summary>
    private void _Schedule_Ground_Sync(int count)
    {
        if (count <= 0) return;

        if (bdisableGroundSync) return;

        int interval = Mathf.Max(1, groundSyncInterval);

        // 빠르게 움직이는 부대가 있으면 갱신 주기를 좁힙니다.
        //
        // 왜 필요한가:
        // 지면 높이는 여러 틱에 나눠 갱신합니다(기본 4틱). 그 사이 유닛은
        // 옛 높이를 유지하므로, 이동이 빠를수록 지형과 어긋난 채로 그려집니다.
        //
        //   일반 이동 3.0 m/s -> 4틱에 20 cm
        //   패주      4.8 m/s -> 4틱에 32 cm   (rout_Speed_Rate 1.6배)
        //
        // 경사가 급한 곳에서 이 32cm가 '반쯤 잠긴 채 달리는' 모습으로 보입니다.
        // 패주는 드물게 일어나므로, 그때만 매 틱 갱신해도 평균 비용은
        // 거의 늘지 않습니다.
        if (_Has_Fast_Moving_Army()) interval = 1;

        // 이번 틱에 처리할 구간을 정합니다.
        // 틱마다 다른 구간을 맡아 전체를 순환합니다.
        int slice = Mathf.CeilToInt(count / (float)interval);
        int phase = (int)(Simulation_Clock.tick % (uint)interval);

        groundSyncOffset = phase * slice;
        groundSyncCount = Mathf.Min(slice, count - groundSyncOffset);

        if (groundSyncCount <= 0) return;

        groundSync.Schedule(units, groundSyncOffset, groundSyncCount);
    }

    /// <summary>
    /// 지면 갱신을 서둘러야 할 만큼 빠른 부대가 있는지 봅니다.
    ///
    /// 패주(MoveEscape)와 돌격(MoveCharge)이 대상입니다. 둘 다 평소보다
    /// 빠르게 움직이므로 지면 갱신이 늦으면 눈에 띄게 어긋납니다.
    /// 부대 수는 많아야 수십 개라 이 순회는 비용이 없습니다.
    /// </summary>
    private bool _Has_Fast_Moving_Army()
    {
        for (int i = 0; i < armies.Count; i++)
        {
            Army a = armies[i];
            if (a == null) continue;
            if (a.units.Count == 0) continue;

            E_Army_Move move = a.army_Data.e_Army_Move;

            if (move == E_Army_Move.MoveEscape || move == E_Army_Move.MoveCharge)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>지면 높이 조회 결과를 반영합니다.</summary>
    /// <param name="count">이번 틱의 유닛 수입니다.</param>
    /// <param name="bsync">Transform 동기화 배열이 유효한지 여부입니다.</param>
    private void _Complete_Ground_Sync(int count, bool bsync)
    {
        if (groundSyncCount <= 0) return;

        groundSync.Complete_And_Apply(units, groundSyncOffset);

        // 동기화 배열에도 새 높이를 반영합니다.
        //
        // 위쪽 루프가 이미 transformSync.positions를 채운 뒤이므로,
        // 여기서 갱신하지 않으면 이번 틱의 Transform 쓰기가 옛 Y를 씁니다.
        // 그러면 높이가 항상 한 틱 늦게 따라와 경사에서 떨립니다.
        if (!bsync) return;

        int end = Mathf.Min(groundSyncOffset + groundSyncCount, count);

        for (int i = groundSyncOffset; i < end; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            transformSync.positions[i] = u.unit_Data.position;
        }
    }

    /// <summary>
    /// 부대 간 '물리적 접촉' 수를 갱신합니다.
    ///
    /// 왜 필요한가:
    /// 표적 부대 선정(Army._Update_Target_Army)은 접촉 수(num)가 0보다 커야
    /// 근접 교전(Melee)으로 판정합니다. 예전에는 그 값이 OnCollisionEnter로
    /// 쌓였는데, 자체 충돌로 넘어오면 그 콜백이 없습니다.
    /// 이 값을 채워 주지 않으면 근접 부대가 영원히 Range 상태로 굳어
    /// 서로 맞붙어도 난전이 시작되지 않습니다.
    /// </summary>
    private void _Update_Army_Contact()
    {
        // 1. 이번 틱의 접촉 카운트를 초기화합니다.
        for (int i = 0; i < armies.Count; i++)
        {
            if (armies[i] == null) continue;
            armies[i].Clear_Contact_Counts();
        }

        // 2. 접촉 쌍의 카운트를 올립니다.
        //    상대 부대는 Job이 이미 기록해 두었으므로(contactArmyIndex) 바로 씁니다.
        //    예전에는 접촉 유닛마다 전 부대를 순회해 가장 가까운 적을 찾았는데,
        //    그 비용이 O(접촉 유닛 수 x 부대 수)라 난전에서 급격히 커졌습니다.
        int count = units.Count;
        var bodies = collisionBodies;

        for (int i = 0; i < count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;

            int foeIndex = bodies[i].contactArmyIndex;
            if (foeIndex < 0 || foeIndex >= armies.Count) continue;

            Army mine = u.GetArmy();
            if (mine == null) continue;

            Army foe = armies[foeIndex];
            if (foe != null) mine.Add_Contact(foe);
        }
    }
}
