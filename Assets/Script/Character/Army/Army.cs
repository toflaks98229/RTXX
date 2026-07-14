using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;

/// <summary>
/// 다른 부대와의 접촉 정보를 저장하는 클래스입니다.
/// </summary>
public class Army_Count
{
    /// <summary>
    /// 접촉한 부대의 수입니다.
    /// </summary>
    public int num;
    /// <summary>
    /// 접촉한 부대입니다.
    /// </summary>
    public Army army;

    /// <summary>
    /// Army_Count 클래스의 생성자입니다.
    /// </summary>
    /// <param name="army">접촉한 부대 인스턴스입니다.</param>
    /// <param name="num">접촉한 횟수입니다.</param>
    public Army_Count(Army army, int num)
    {
        this.army = army;
        this.num = num;
    }
}

/// <summary>
/// 유닛들의 집합인 부대를 제어하는 주 스크립트입니다.
/// 부대 이동, 전투, 상태 관리 등 전반적인 부대 제어 로직을 포함합니다.
/// </summary>
public partial class Army : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// 부대의 전반적인 데이터를 담고 있는 구조체입니다.
    /// </summary>
    public Army_Data army_Data;
    /// <summary>
    /// 유닛의 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Unit;
    /// <summary>
    /// 무기 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Weapon;
    /// <summary>
    /// 방패 스프라이트 이미지 리스트입니다.
    /// </summary>
    public List<Sprite> images_Shield;
    /// <summary>
    /// 생성할 유닛 프리팹입니다.
    /// </summary>
    public GameObject unit;
    /// <summary>
    /// 생성된 모든 유닛 인스턴스 리스트입니다.
    /// </summary>
    public List<Unit> units;
    /// <summary>
    /// 부대의 깃발 오브젝트입니다.
    /// </summary>
    public Transform Flag;
    /// <summary>
    /// 깃발을 들고 있는 유닛입니다.
    /// </summary>
    public Unit unit_Bearing_Flag;
    /// <summary>
    /// 부대의 이동을 제어하는 NavMeshAgent 컴포넌트입니다.
    /// </summary>
    public NavMeshAgent navMeshAgent;
    /// <summary>
    /// 부대가 이동할 목표 위치입니다.
    /// </summary>
    public Vector3 locationMoveTo;
    /// <summary>
    /// 현재 부대의 진형 데이터입니다.
    /// </summary>
    public Formation_Data formation_Data;
    /// <summary>
    /// 포메이션 이동 중인지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool bformation_Move = false;
    /// <summary>
    /// 부대 이동의 기준이 되는 Transform입니다.
    /// </summary>
    public Transform formation_Move_Transform;
    /// <summary>
    /// 각 유닛의 포메이션 목표 위치를 나타내는 Transform 리스트입니다.
    /// </summary>
    public List<Transform> formation_Moves;
    /// <summary>
    /// 현재 부대가 선택되었는지 여부를 나타내는 플래그입니다.
    /// </summary>
    public bool bselected;
    /// <summary>
    /// 탐지된 다른 부대들의 리스트입니다.
    /// </summary>
    public List<Army_Count> army_Detected;
    /// <summary>
    /// 현재 부대의 공격 목표인 다른 부대입니다.
    /// </summary>
    public Army targetArmy;
    /// <summary>
    /// 포메이션의 최대 길이입니다.
    /// </summary>
    public int formationLength_Max;
    /// <summary>
    /// 유닛 UI 프리팹입니다.
    /// </summary>
    public GameObject UIData;
    /// <summary>
    /// 생성된 유닛 UI 인스턴스 리스트입니다.
    /// </summary>
    public List<UI_Unit> uI_Units;

    // 비공개 멤버 변수
    /// <summary>
    /// 유닛들의 데이터를 저장하는 NativeArray입니다. 잡 시스템에 사용됩니다.
    /// </summary>
    private NativeArray<Unit_Data> unit_Datas;
    /// <summary>
    /// 타겟 유닛들의 데이터를 저장하는 NativeArray입니다. 잡 시스템에 사용됩니다.
    /// </summary>
    private NativeArray<Unit_Data> target_Unit_Datas;
    /// <summary>
    /// 타겟 재설정을 위한 타이머입니다.
    /// </summary>
    private Timer timer_ReTarget;

    // Unity 이벤트 함수
    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// </summary>
    public void _Start()
    {
        // 이 스크립트를 비활성화하여 Start()와 Update()가 자동으로 호출되지 않도록 합니다.
        // 이 스크립트의 로직은 수동으로 호출되는 _Update(NativeHashMap<int, Unit_Data>)에 의해 제어됩니다.
        enabled = false;

        Spawn_Units(); // 유닛들을 생성합니다.

        army_Data._Start();
        army_Data.position = formation_Move_Transform.position;
        army_Data.rotation = formation_Move_Transform.rotation;

        navMeshAgent = formation_Move_Transform.GetComponent<NavMeshAgent>();
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        army_Detected = new List<Army_Count>();

        timer_ReTarget = new Timer(Constant.time_ReTarget);
    }

    // 공개 메서드
    /// <summary>
    /// 부대 전체를 업데이트하는 함수입니다.
    /// 유닛 데이터, 이동, 타겟 탐지, 통계, 진형 상태 등을 갱신합니다.
    /// </summary>
    /// <param name="unitDataMap">모든 유닛의 데이터를 담고 있는 해시맵입니다.</param>
    public void _Update(NativeHashMap<int, Unit_Data> unitDataMap)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null)
            {
                units.RemoveAt(i);
                continue;
            }
        }

        _Upadate_Data(); // 부대 데이터 업데이트
        _Update_Move(); // 이동 상태 업데이트

        unit_Datas = new NativeArray<Unit_Data>(units.Count, Allocator.TempJob);
        for (int i = 0; i < units.Count; i++)
        {
            unit_Datas[i] = units[i].unit_Data;
        }

        _Update_Flag(); // 깃발 위치 업데이트
        _Update_Unit(unitDataMap); // 유닛 상태 업데이트
        _Update_Target(); // 타겟 상태 업데이트
        _Update_Stat(); // 통계 업데이트
        _Update_Formation(); // 진형 상태 업데이트

        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Data = unit_Datas[i];
            units[i]._Update();
        }

        unit_Datas.Dispose();
    }

    /// <summary>
    /// 부대 데이터를 업데이트합니다.
    /// </summary>
    public void _Upadate_Data()
    {
        army_Data.position = formation_Move_Transform.position;
        army_Data.rotation = formation_Move_Transform.rotation;
    }

    /// <summary>
    /// 모든 유닛을 생성하고 초기 위치를 설정합니다.
    /// </summary>
    void Spawn_Units()
    {
        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            GameObject unit = Instantiate(this.unit, formation_Move_Transform.position, formation_Move_Transform.rotation);
            unit.GetComponent<Unit>().SetArmy(this);
            units.Add(unit.GetComponent<Unit>());

            GameObject formation_Move = new GameObject("Formation_Move");
            formation_Moves.Add(Instantiate(formation_Move, formation_Move_Transform).transform);

            GameObject UIData = Instantiate(this.UIData, formation_Move_Transform.position, formation_Move_Transform.rotation);
            uI_Units.Add(UIData.GetComponent<UI_Unit>());
        }

        Vector3 position = formation_Move_Transform.position - formation_Move_Transform.right * formationLength_Max * army_Data.GetInterval() * 0.5f;

        formation_Data = new Formation_Data(Set_Formation(formationLength_Max, transform.right, position));

        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            units[i].transform.position = formation_Data.formation[i];
            units[i].SetArmy(this);

            formation_Moves[i].transform.position = formation_Data.formation[i];

            uI_Units[i].transform.position = formation_Data.formation[i];
            uI_Units[i]._Start(army_Data);
        }

        unit_Bearing_Flag = units[0];
    }

    /// <summary>
    /// 유닛의 상태를 업데이트합니다.
    /// </summary>
    /// <param name="unitDataMap">모든 유닛의 데이터를 담고 있는 해시맵입니다.</param>
    public void _Update_Unit(NativeHashMap<int, Unit_Data> unitDataMap)
    {
        if (units.Count == 0) return;

        // 1. 레이캐스트 명령과 결과를 담을 NativeArray 준비
        var commands = new NativeArray<RaycastCommand>(units.Count, Allocator.TempJob);
        var results = new NativeArray<RaycastHit>(units.Count, Allocator.TempJob);

        // 2. 각 유닛에 대한 레이캐스트 명령 생성
        for (int i = 0; i < units.Count; i++)
        {
            Vector3 origin = units[i].transform.position;
            Vector3 direction = units[i].transform.forward;
            float maxDistance = 5.0f;
            int layerMask = LayerMask.GetMask("Unit");

            commands[i] = new RaycastCommand(origin, direction, maxDistance, layerMask);
        }

        // 3. 레이캐스트 배치 잡(Batch Job) 스케줄링
        JobHandle raycastHandle = RaycastCommand.ScheduleBatch(commands, results, 1);

        // 4. Unit_Job에 필요한 데이터 전달
        Unit_Job unit_Job = new Unit_Job();
        unit_Job.unit_Datas = unit_Datas;
        unit_Job.raycastHits = results;
        unit_Job.unitDataMap = unitDataMap;

        // raycastHandle이 완료된 후에 unit_Job을 실행하도록 의존성 설정
        JobHandle unitJobHandle = unit_Job.Schedule(units.Count, 1, raycastHandle);

        var unit_Animation_Datas = new NativeArray<Unit_Animation_Data>(units.Count, Allocator.TempJob);

        for (int i = 0; i < units.Count; i++)
        {
            unit_Animation_Datas[i] = units[i].unit_Animation.unit_Animation_Data;
        }

        Unit_Animation_Job unit_Animation_Job;
        unit_Animation_Job = new Unit_Animation_Job();
        unit_Animation_Job.unit_Datas = unit_Datas;
        unit_Animation_Job.unit_Animation_Datas = unit_Animation_Datas;
        unit_Animation_Job.cam_Position = Camera.main.transform.position;
        unit_Animation_Job.cam_Rotation = Camera.main.transform.rotation;

        // 5. 애니메이션 잡은 Unit_Job이 끝난 후에 실행되도록 의존성 체인 연결
        JobHandle animationJobHandle = unit_Animation_Job.Schedule(units.Count, 1, unitJobHandle);

        // 6. 모든 잡(레이캐스트 -> 유닛 -> 애니메이션)이 끝날 때까지 대기
        animationJobHandle.Complete();

        // 7. 결과 반영 및 메모리 해제
        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Animation.unit_Animation_Data = unit_Animation_Datas[i];
        }

        for (int i = 0; i < units.Count; i++)
        {
            units[i].unit_Animation._Update();
        }

        commands.Dispose();
        results.Dispose();
        unit_Animation_Datas.Dispose();
    }

    /// <summary>
    /// 모든 유닛 인스턴스 리스트를 반환합니다.
    /// </summary>
    /// <returns>유닛 리스트입니다.</returns>
    public List<Unit> GetUnits()
    {
        return units;
    }

    /// <summary>
    /// 탐지된 부대 리스트에 새로운 부대를 추가합니다.
    /// </summary>
    /// <param name="army">탐지된 부대 인스턴스입니다.</param>
    public void Add_Army_Detected(Army army)
    {
        if (this == army) return;
        if (army.army_Data.bplayer == army_Data.bplayer) return;

        bool badd = false;
        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].num = army_Detected[i].num + 1;
                badd = true;
            }
        }

        if (!badd)
        {
            army_Detected.Add(new Army_Count(army, 1));
        }
    }

    /// <summary>
    /// 탐지된 부대 리스트에서 부대를 제거합니다.
    /// </summary>
    /// <param name="army">제거할 부대 인스턴스입니다.</param>
    public void Remove_Army_Detected(Army army)
    {
        if (this == army) return;

        for (int i = 0; i < army_Detected.Count; i++)
        {
            if (army_Detected[i].army == army)
            {
                army_Detected[i].num = army_Detected[i].num - 1;
                if (army_Detected[i].num <= 0)
                {
                    army_Detected.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 부대가 선택되었는지 여부를 반환합니다.
    /// </summary>
    /// <returns>선택되었으면 true, 아니면 false입니다.</returns>
    public bool IsSelected()
    {
        return bselected;
    }

    /// <summary>
    /// 부대를 선택 해제하고, 모든 유닛의 선택 상태를 해제합니다.
    /// </summary>
    public void UnSelected()
    {
        bselected = false;
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (unit.IsDead()) continue;
            unit.UnSelected();
        }
    }

    /// <summary>
    /// 부대를 선택하고, 모든 유닛의 선택 상태를 활성화합니다.
    /// </summary>
    public void Selected()
    {
        bselected = true;
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (unit.IsDead()) continue;
            unit.Selected();
        }
    }

    /// <summary>
    /// 킬 수를 증가시킵니다. (현재 로직은 비어있습니다.)
    /// </summary>
    public void AddKillCount()
    {
        // 킬 수 증가 로직 추가 예정
    }

    /// <summary>
    /// 부대 진형 UI를 보이게 합니다.
    /// </summary>
    public void Draw_Formation_UI()
    {
        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            uI_Units[i].Visible();
        }
    }

    /// <summary>
    /// 부대 진형 UI를 숨깁니다.
    /// </summary>
    public void Erase_Formation_UI()
    {
        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            uI_Units[i].Invisible();
        }
    }

    // 비공개 메서드
    /// <summary>
    /// 깃발의 위치를 업데이트합니다.
    /// </summary>
    private void _Update_Flag()
    {
        if (unit_Bearing_Flag == null) return;

        Vector3 targetVector = unit_Bearing_Flag.transform.position;
        targetVector.y = unit_Bearing_Flag.transform.position.y + 5.0f;

        Vector3 eulerAngles = Camera.main.transform.rotation.eulerAngles;
        eulerAngles = new Vector3(0.0f, eulerAngles.y, 0.0f);

        Flag.transform.position = Vector3.Lerp(Flag.transform.position, targetVector, 0.1f);
        Flag.rotation = Quaternion.Euler(eulerAngles);
    }
}
