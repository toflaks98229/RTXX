using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 대규모 전투 씬을 자동으로 만들어 주는 에디터 도구입니다.
///
/// 왜 필요한가:
/// 기존 Scene1은 12개 부대 x 50명 = 600명입니다. 그런데 이 프로젝트의
/// 최적화(TransformAccessArray 일괄 쓰기, 격자 충돌, LOD, 부대 격자)는
/// 전부 '수천~만 명' 규모를 상정하고 만들어졌습니다. 주석에 적힌 실측값도
/// 4,800명과 9,600명 기준입니다.
///
/// 즉 지금 씬으로는 그 최적화들이 실제로 동작하는지 확인할 수 없습니다.
/// 600명에서는 어떤 구현이든 60fps가 나오기 때문입니다.
/// 이 도구는 그 검증에 필요한 규모를 재현 가능한 방식으로 만듭니다.
///
/// 왜 손으로 배치하지 않는가:
/// 부대 60개를 인스펙터로 배치하면 한 시간이 걸리고, 다음에 조건을 바꾸려면
/// 또 한 시간이 듭니다. 무엇보다 '같은 조건'을 두 번 만들 수 없어
/// 성능 비교가 성립하지 않습니다. 코드로 만들면 규모만 바꿔 재생성할 수 있고,
/// 시드를 고정하면 언제나 같은 배치가 나옵니다.
///
/// 사용법: 메뉴 > RTXX > 대규모 전투 씬 생성
/// </summary>
public static class Mass_Battle_Builder
{
    // =====================================================================
    // 생성 규격
    // =====================================================================

    /// <summary>한쪽 진영의 부대 수입니다. 총 부대 수는 이 값의 두 배입니다.</summary>
    private const int armiesPerSide = 30;

    /// <summary>부대 한 개의 인원입니다.</summary>
    private const int unitsPerArmy = 160;

    /// <summary>
    /// 전열에서 부대 사이의 간격(미터)입니다.
    ///
    /// 기존 씬의 NavMesh가 넓지 않으므로 조밀하게 잡습니다.
    /// 너무 넓게 펴면 가장자리 부대가 NavMesh 밖으로 나가 움직이지 못합니다.
    /// </summary>
    private const float armySpacing = 14.0f;

    /// <summary>양 진영 사이의 거리(미터)입니다. 금방 접촉하도록 가깝게 둡니다.</summary>
    private const float sideSeparation = 34.0f;

    /// <summary>부대를 몇 줄로 배치할지입니다. 전열 + 예비대 구조를 만듭니다.</summary>
    private const int ranks = 3;

    /// <summary>줄 사이의 간격(미터)입니다.</summary>
    private const float rankSpacing = 11.0f;

    /// <summary>생성 씬 경로입니다.</summary>
    private const string scenePath = "Assets/_Project/01.Scenes/Scene_MassBattle.unity";

    /// <summary>참고할 원본 씬입니다. 카메라/조명/지면 설정을 물려받습니다.</summary>
    private const string templateScenePath = "Assets/_Project/01.Scenes/Scene1.unity";

    [MenuItem("RTXX/대규모 전투 씬 생성")]
    public static void Build()
    {
        Build_Internal(armiesPerSide, unitsPerArmy);
    }

    /// <summary>
    /// 배치모드(CLI)에서 호출되는 진입점입니다.
    /// Unity.exe -batchmode -executeMethod Mass_Battle_Builder.Build_From_CLI
    /// </summary>
    public static void Build_From_CLI()
    {
        int perSide = armiesPerSide;
        int perArmy = unitsPerArmy;

        // 명령줄에서 규모를 덮어쓸 수 있게 합니다.
        // 여러 규모를 연속으로 재보려면 이 편이 편합니다.
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-armiesPerSide") int.TryParse(args[i + 1], out perSide);
            if (args[i] == "-unitsPerArmy") int.TryParse(args[i + 1], out perArmy);
        }

        Build_Internal(perSide, perArmy);

        Debug.Log($"[MassBattle] 생성 완료: 부대 {perSide * 2}개, " +
                  $"총 인원 {perSide * 2 * perArmy}명");
    }

    private static void Build_Internal(int perSide, int perArmy)
    {
        GameObject armyPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/05.Prefabs/Army.prefab");

        if (armyPrefab == null)
        {
            Debug.LogError("[MassBattle] Army.prefab을 찾지 못했습니다.");
            return;
        }

        // 원본 씬을 복사해서 시작합니다.
        //
        // 새 씬을 빈 상태로 만들면 카메라, 조명, 지면, NavMesh 설정이 전부
        // 빠져 시뮬레이션이 제대로 돌지 않습니다. 검증이 목적이므로
        // 환경은 기존 씬과 같아야 합니다.
        Scene scene = EditorSceneManager.OpenScene(templateScenePath, OpenSceneMode.Single);

        Controller controller = Object.FindAnyObjectByType<Controller>();
        if (controller == null)
        {
            Debug.LogError("[MassBattle] 원본 씬에서 Controller를 찾지 못했습니다.");
            return;
        }

        // 기존 부대에서 스프라이트 목록을 먼저 걷어 둡니다.
        //
        // 왜 필요한가:
        // Army.prefab의 images_Unit / images_Weapon / images_Shield는 비어 있고,
        // 실제 스프라이트는 씬의 프리팹 오버라이드로 채워져 있습니다.
        // 이걸 물려받지 않으면 Unit_Animation._Start가
        //   images_Unit[Random.Range(0, 0)]  ->  ArgumentOutOfRangeException
        // 으로 터지고, 그 예외가 Controller.Start의 유닛 초기화 루프를
        // 중간에 끊어 버립니다. (실제로 첫 실행에서 그렇게 실패했습니다)
        Sprite_Set sprites = Collect_Sprites();

        // 전장 중심도 기존 부대가 남아 있는 동안 구해 둡니다.
        Vector3 origin = Get_Battlefield_Origin();

        // 기존 부대를 모두 걷어냅니다.
        Remove_Existing_Armies(controller);

        List<Army> created = new List<Army>();

        // NavMesh를 찾지 못한 부대 수입니다. 0이 아니면 그 부대들은
        // 명령을 받아도 움직이지 못하므로 배치 규격을 줄여야 합니다.
        int offNavMesh = 0;

        for (int side = 0; side < 2; side++)
        {
            bool bplayer = side == 0;

            // 아군은 -Z, 적군은 +Z에 섭니다. 서로 마주 보게 배치합니다.
            float z = bplayer ? -sideSeparation * 0.5f : sideSeparation * 0.5f;
            float facing = bplayer ? 0.0f : 180.0f;

            for (int i = 0; i < perSide; i++)
            {
                int rank = i % ranks;
                int columnIndex = i / ranks;
                int columns = Mathf.CeilToInt(perSide / (float)ranks);

                // 전열을 원점 기준으로 좌우 대칭이 되게 폅니다.
                float x = (columnIndex - (columns - 1) * 0.5f) * armySpacing;

                // 뒷줄일수록 뒤로 물러납니다. (예비대)
                float rankOffset = rank * rankSpacing * (bplayer ? -1.0f : 1.0f);

                Vector3 position = origin + new Vector3(x, 0.0f, z + rankOffset);

                // NavMesh 위로 끌어당깁니다.
                //
                // 왜 필요한가:
                // 부대 기준점은 NavMeshAgent로 움직입니다. NavMesh 밖에 놓이면
                // agent.Move()가 아무 효과도 내지 못해, 부대가 Move 상태인 채로
                // 제자리에 서 있게 됩니다. 예외도 경고도 나지 않습니다.
                //
                // 실제로 첫 검증에서 60개 부대 전부가 Move 상태인데 적 탐지가
                // 0인 상태로 10초를 보냈습니다. 원인이 이것이었습니다.
                Vector3 snapped = Snap_To_NavMesh(position);
                if (snapped == position) offNavMesh++;
                position = snapped;

                Army army = Create_Army(armyPrefab, position, facing,
                                        bplayer, perArmy, i, rank, sprites);
                if (army != null) created.Add(army);
            }
        }

        // Controller에 부대 목록을 물려줍니다.
        controller.armies = created;
        controller.units = new List<Unit>();
        controller.armies_Selected = new List<Army>();

        // 검증 목적이므로 시드를 고정합니다.
        // 같은 씬을 두 번 돌리면 같은 전투가 나와야 비교가 성립합니다.
        controller.simulationSeed = 12345;

        EditorUtility.SetDirty(controller);

        // 성능 측정 컴포넌트가 없으면 붙여 줍니다.
        Ensure_Probe(controller);

        // 양측 모두 AI가 지휘하게 합니다.
        //
        // 왜 필요한가:
        // 원본 씬의 Battle_AI는 적군만 지휘합니다(bcontrolPlayerSide = false).
        // 플레이어 부대는 사람이 명령을 내리는 것이 전제이기 때문입니다.
        // 그런데 이 씬은 '사람 없이' 돌려 검증하는 것이 목적이므로,
        // 아군에게 명령을 내릴 주체가 없으면 양측이 90m 떨어진 채
        // 아무 일도 일어나지 않습니다. (실제로 첫 실행이 그랬습니다)
        Ensure_Both_Side_AI(controller);

        // 배치 단계를 건너뜁니다.
        // 배치모드에는 키 입력이 없어 Space로 전투를 시작할 수 없습니다.
        Battle_Manager battleManager = Object.FindAnyObjectByType<Battle_Manager>();
        if (battleManager != null)
        {
            battleManager.buseDeployment = false;
            EditorUtility.SetDirty(battleManager);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[MassBattle] {scenePath} 저장 완료. " +
                  $"부대 {created.Count}개, 총 인원 {created.Count * perArmy}명, " +
                  $"전장 중심 {origin}");

        if (offNavMesh > 0)
        {
            Debug.LogWarning(
                $"[MassBattle] {offNavMesh}개 부대가 NavMesh 밖에 배치되었습니다. " +
                "이 부대들은 명령을 받아도 움직이지 않습니다. " +
                "armySpacing / sideSeparation을 줄이십시오.");
        }
    }

    /// <summary>
    /// 전장의 중심을 구합니다.
    ///
    /// 기존 부대들의 평균 위치를 씁니다. NavMesh와 지형은 그 주변에만
    /// 준비되어 있으므로, 원점(0,0)에 배치하면 전군이 NavMesh 밖에 서서
    /// 한 발짝도 움직이지 못합니다.
    /// </summary>
    private static Vector3 Get_Battlefield_Origin()
    {
        Army[] existing = Object.FindObjectsByType<Army>(FindObjectsSortMode.None);

        if (existing.Length == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null) continue;
            sum += existing[i].transform.position;
            count++;
        }

        if (count == 0) return Vector3.zero;

        Vector3 center = sum / count;
        center.y = 0.0f;

        return center;
    }

    /// <summary>
    /// 주어진 위치를 가장 가까운 NavMesh 지점으로 끌어당깁니다.
    ///
    /// 반경 안에서 NavMesh를 찾지 못하면 원래 위치를 그대로 돌려줍니다.
    /// 그 경우 그 부대는 움직이지 못하지만, 배치 자체는 실패하지 않습니다.
    /// </summary>
    private static Vector3 Snap_To_NavMesh(Vector3 position)
    {
        // 넉넉한 반경으로 찾습니다. 전장이 넓으므로 가장자리 부대는
        // 꽤 멀리 끌려올 수 있지만, NavMesh 밖에 서는 것보다 낫습니다.
        if (UnityEngine.AI.NavMesh.SamplePosition(
                position, out UnityEngine.AI.NavMeshHit hit, 60.0f,
                UnityEngine.AI.NavMesh.AllAreas))
        {
            return hit.position;
        }

        return position;
    }

    /// <summary>부대가 쓰는 스프라이트 묶음입니다.</summary>
    private struct Sprite_Set
    {
        public List<Sprite> unit;
        public List<Sprite> weapon;
        public List<Sprite> shield;
    }

    /// <summary>
    /// 원본 씬의 부대에서 스프라이트 목록을 걷어 옵니다.
    ///
    /// 프리팹이 아니라 씬에서 걷는 이유:
    /// Army.prefab의 이미지 목록은 비어 있고, 실제 값은 씬의 프리팹
    /// 오버라이드로 들어가 있습니다. 그래서 프리팹만 보면 빈 목록을 얻습니다.
    /// </summary>
    private static Sprite_Set Collect_Sprites()
    {
        Sprite_Set set = new Sprite_Set
        {
            unit = new List<Sprite>(),
            weapon = new List<Sprite>(),
            shield = new List<Sprite>()
        };

        Army[] existing = Object.FindObjectsByType<Army>(FindObjectsSortMode.None);

        for (int i = 0; i < existing.Length; i++)
        {
            Army a = existing[i];
            if (a == null) continue;

            Add_Unique(set.unit, a.images_Unit);
            Add_Unique(set.weapon, a.images_Weapon);
            Add_Unique(set.shield, a.images_Shield);
        }

        // 씬에서 아무것도 못 찾았으면 프로젝트 전체에서 찾아봅니다.
        // (원본 씬이 비어 있는 경우를 대비한 안전장치입니다)
        if (set.unit.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project" });

            for (int i = 0; i < guids.Length && set.unit.Count < 3; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) set.unit.Add(sprite);
            }

            Debug.LogWarning($"[MassBattle] 씬에서 스프라이트를 찾지 못해 " +
                             $"프로젝트에서 {set.unit.Count}개를 대신 씁니다.");
        }

        return set;
    }

    /// <summary>중복 없이 스프라이트를 추가합니다.</summary>
    private static void Add_Unique(List<Sprite> target, List<Sprite> source)
    {
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;
            if (target.Contains(source[i])) continue;
            target.Add(source[i]);
        }
    }

    /// <summary>기존 부대 오브젝트를 씬에서 제거합니다.</summary>
    private static void Remove_Existing_Armies(Controller controller)
    {
        Army[] existing = Object.FindObjectsByType<Army>(FindObjectsSortMode.None);

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] == null) continue;
            Object.DestroyImmediate(existing[i].gameObject);
        }

        if (controller.armies != null) controller.armies.Clear();
    }

    /// <summary>
    /// 부대 하나를 만들고 병종을 배정합니다.
    ///
    /// 병종 구성은 의도적으로 섞습니다. 전부 같은 보병이면 상성, 태세,
    /// 돌격 반사, 산개 후퇴 같은 규칙이 아예 실행되지 않아
    /// '동작하는지' 검증할 수 없습니다.
    /// </summary>
    private static Army Create_Army(GameObject prefab, Vector3 position, float facing,
                                    bool bplayer, int unitNum, int index, int rank,
                                    Sprite_Set sprites)
    {
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0.0f, facing, 0.0f));

        Army army = go.GetComponent<Army>();
        if (army == null) return null;

        // 스프라이트를 반드시 채웁니다.
        // 비어 있으면 유닛 생성 중에 예외가 나고, 그 예외가
        // Controller.Start의 초기화 루프를 통째로 끊습니다.
        army.images_Unit = new List<Sprite>(sprites.unit);
        army.images_Weapon = new List<Sprite>(sprites.weapon);
        army.images_Shield = new List<Sprite>(sprites.shield);

        army.army_Data.bplayer = bplayer;
        army.army_Data.unit_Num = unitNum;
        army.army_Data.unit_Num_Max = unitNum;

        // 병종 배정
        //   앞줄(rank 0) : 보병과 창병이 전열을 이룹니다.
        //   중간(rank 1) : 궁병이 뒤에서 쏩니다.
        //   뒷줄(rank 2) : 기병이 측면 기동을 노립니다.
        E_Unit_Class unitClass;

        if (rank == 0) unitClass = (index % 2 == 0) ? E_Unit_Class.Infantry : E_Unit_Class.Spear;
        else if (rank == 1) unitClass = E_Unit_Class.Archer;
        else unitClass = E_Unit_Class.Cavalry;

        Apply_Class_Stats(ref army.army_Data, unitClass);

        // 각 진영의 첫 부대를 장군으로 세웁니다.
        // 장군 오라와 전사 시 전군 충격을 검증하기 위한 것입니다.
        army.army_Data.bgeneral = index == 0;

        go.name = $"Army_{(bplayer ? "P" : "E")}_{index:D2}_{unitClass}" +
                  (army.army_Data.bgeneral ? "_General" : "");

        // 진형 폭은 인원에 맞춰 잡습니다.
        // 너무 좁으면 세로로 길쭉해져 전열이 성립하지 않습니다.
        army.formationLength_Max = Mathf.Max(12, Mathf.RoundToInt(Mathf.Sqrt(unitNum) * 1.6f));

        EditorUtility.SetDirty(army);
        return army;
    }

    /// <summary>
    /// 병종별 스탯을 적용합니다.
    ///
    /// 수치는 밸런싱이 아니라 '규칙이 실행되게 하는 것'이 목적입니다.
    /// 예를 들어 궁병에게 탄약을 주는 이유는 탄약 소진 경로를 타게 하려는 것이고,
    /// 창병에게 대형 보너스를 주는 이유는 기병 돌격 반사를 발동시키려는 것입니다.
    /// </summary>
    private static void Apply_Class_Stats(ref Army_Data data, E_Unit_Class unitClass)
    {
        data.unit_Stat.e_Unit_Class = unitClass;

        switch (unitClass)
        {
            case E_Unit_Class.Spear:
                // 창병: 느리지만 대형(기병)에 강하고 창벽으로 돌격을 받아냅니다.
                data.unit_Stat.moveSpeed = 2.8f;
                data.unit_Stat.meleeRange = 2.2f;
                data.unit_Stat.bonusVsLarge = 30.0f;
                data.unit_Stat.bonusVsInfantry = 0.0f;
                data.unit_Stat.brangeAttackAble = false;
                data.unit_Stat.e_Unit_AttackType = E_Unit_AttackType.Melee;
                break;

            case E_Unit_Class.Archer:
                // 궁병: 근접에 약하지만 멀리서 갉아먹습니다. 탄약이 유한합니다.
                data.unit_Stat.moveSpeed = 3.2f;
                data.unit_Stat.meleeAttack = 15.0f;
                data.unit_Stat.meleeDamage = 10.0f;
                data.unit_Stat.armor = 1.0f;
                data.unit_Stat.shieldArmor = 0.0f;
                data.unit_Stat.brangeAttackAble = true;
                data.unit_Stat.e_Unit_AttackType = E_Unit_AttackType.Range;
                data.unit_Stat.rangeRange = 30.0f;
                data.unit_Stat.rangeDamage = 14.0f;
                data.unit_Stat.rangeAccuracy = 30.0f;
                data.unit_Stat.rangeAttackSpeed = 1.4f;
                data.unit_Stat.ammunition = 24;
                break;

            case E_Unit_Class.Cavalry:
                // 기병: 빠르고 무겁습니다. 돌격 충격과 충돌 공격을 검증합니다.
                data.unit_Stat.moveSpeed = 5.5f;
                data.unit_Stat.meleeChargeSpeed = 9.0f;
                data.unit_Stat.meleeChargeRange = 14.0f;
                data.unit_Stat.meleeChargeBonus = 55.0f;
                data.unit_Stat.mass = 320.0f;
                data.unit_Stat.radius = 0.45f;
                data.unit_Stat.size = 1.4f;
                data.unit_Stat.interval = 1.5f;
                data.unit_Stat.bonusVsInfantry = 20.0f;
                data.unit_Stat.brangeAttackAble = false;
                data.unit_Stat.e_Unit_AttackType = E_Unit_AttackType.Melee;
                break;

            default:
                // 보병: 프리팹 기본값을 그대로 씁니다.
                data.unit_Stat.brangeAttackAble = false;
                data.unit_Stat.e_Unit_AttackType = E_Unit_AttackType.Melee;
                break;
        }
    }

    /// <summary>성능 측정 컴포넌트를 씬에 보장합니다.</summary>
    private static void Ensure_Probe(Controller controller)
    {
        if (Object.FindAnyObjectByType<Mass_Battle_Probe>() != null) return;

        GameObject probe = new GameObject("Mass_Battle_Probe");
        probe.AddComponent<Mass_Battle_Probe>();
    }

    /// <summary>
    /// 양 진영을 각각 지휘하는 AI를 보장합니다.
    ///
    /// 기존 AI는 적군 담당으로 두고, 아군용 AI를 하나 더 세웁니다.
    /// 두 AI가 같은 API(Move_Start / Set_Stance)만 쓰므로,
    /// 사람이 조작하는 것과 규칙상 동일한 전투가 벌어집니다.
    /// </summary>
    private static void Ensure_Both_Side_AI(Controller controller)
    {
        Battle_AI[] ais = Object.FindObjectsByType<Battle_AI>(FindObjectsSortMode.None);

        bool bhasEnemyAI = false;
        bool bhasPlayerAI = false;

        for (int i = 0; i < ais.Length; i++)
        {
            if (ais[i] == null) continue;

            // 연결이 비어 있으면 채워 줍니다.
            if (ais[i].controller == null) ais[i].controller = controller;

            if (ais[i].bcontrolPlayerSide) bhasPlayerAI = true;
            else bhasEnemyAI = true;

            EditorUtility.SetDirty(ais[i]);
        }

        if (!bhasEnemyAI) Create_AI(controller, false);
        if (!bhasPlayerAI) Create_AI(controller, true);
    }

    /// <summary>한쪽 진영을 지휘하는 AI를 만듭니다.</summary>
    private static void Create_AI(Controller controller, bool bplayerSide)
    {
        GameObject go = new GameObject(bplayerSide ? "Battle_AI_Player" : "Battle_AI_Enemy");

        Battle_AI ai = go.AddComponent<Battle_AI>();
        ai.controller = controller;
        ai.bcontrolPlayerSide = bplayerSide;

        // 배치 단계를 기다리지 않도록 매니저를 물려줍니다.
        ai.battle_Manager = Object.FindAnyObjectByType<Battle_Manager>();

        EditorUtility.SetDirty(ai);
    }
}
