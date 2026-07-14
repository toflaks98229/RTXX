using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Linq;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static Unity.Collections.AllocatorManager;

public class Controller : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// 게임 내 모든 유닛들을 저장하는 리스트입니다.
    /// </summary>
    public List<Unit> units;
    /// <summary>
    /// 게임 내 모든 부대들을 저장하는 리스트입니다.
    /// </summary>
    public List<Army> armies;
    /// <summary>
    /// 현재 선택된 부대들을 저장하는 리스트입니다.
    /// </summary>
    public List<Army> armies_Selected;
    /// <summary>
    /// 지면 레이어 마스크입니다.
    /// </summary>
    public LayerMask layer_Ground;
    /// <summary>
    /// 클릭 가능한 오브젝트 레이어 마스크입니다.
    /// </summary>
    public LayerMask layer_Clickable;
    /// <summary>
    /// 선택 해제 기능을 비활성화하는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_disable_clear = KeyCode.LeftControl;
    /// <summary>
    /// 모든 유닛을 선택하는 키 코드입니다. (현재 사용되지 않음)
    /// </summary>
    public KeyCode keyCode_select_All;
    /// <summary>
    /// 선택 기능을 위한 마우스 키 코드입니다 (기본: 마우스 왼쪽).
    /// </summary>
    public KeyCode keyCode_Select = KeyCode.Mouse0;
    /// <summary>
    /// 명령 기능을 위한 마우스 키 코드입니다 (기본: 마우스 오른쪽).
    /// </summary>
    public KeyCode keyCode_Command = KeyCode.Mouse1;
    /// <summary>
    /// 드래그 선택 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bselect;
    /// <summary>
    /// 드래그 선택 시작점입니다.
    /// </summary>
    public Vector2 select_Start;
    /// <summary>
    /// 드래그 선택 끝점입니다.
    /// </summary>
    public Vector2 select_End;
    /// <summary>
    /// 드래그 선택 UI 박스를 나타내는 RectTransform입니다.
    /// </summary>
    [SerializeField]
    public RectTransform select_UI_Box;
    /// <summary>
    /// 명령 드래그 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bdrag;
    /// <summary>
    /// 진형 형성 중임을 나타내는 플래그입니다.
    /// </summary>
    public bool bformation;
    /// <summary>
    /// 진형 시작 위치입니다.
    /// </summary>
    public Vector3 formation_Start;
    /// <summary>
    /// 진형 끝 위치입니다.
    /// </summary>
    public Vector3 formation_End;
    /// <summary>
    /// 드래그로 진형을 형성할 때 표시되는 깃발 1번입니다.
    /// </summary>
    public Transform Flag1;
    /// <summary>
    /// 드래그로 진형을 형성할 때 표시되는 깃발 2번입니다.
    /// </summary>
    public Transform Flag2;

    // 비공개 멤버 변수
    /// <summary>
    /// 드래그 선택 영역을 저장하는 Rect 구조체입니다.
    /// </summary>
    private Rect select_Box;

    // Unity 이벤트 함수
    /// <summary>
    /// MonoBehaviour 인스턴스가 생성될 때 호출됩니다.
    /// </summary>
    private void Awake()
    {
        // 애플리케이션의 목표 프레임 속도를 60으로 설정합니다.
        Application.targetFrameRate = 60;
    }

    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// </summary>
    private void Start()
    {
        // 모든 부대를 초기화하고 유닛 리스트를 채웁니다.
        for (int i = 0; i < armies.Count; i++)
        {
            armies[i]._Start();
            units.AddRange(armies[i].units);
        }

        // 모든 유닛을 초기화합니다.
        for (int i = 0; i < units.Count; i++)
        {
            units[i]._Start(i);
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    private void Update()
    {
        // 마우스 버튼 입력에 따른 선택 및 명령 업데이트를 처리합니다.
        _Update_MouseButton_Select();
        _Update_MouseButton_Command();
    }

    /// <summary>
    /// 고정된 시간 간격으로 호출됩니다.
    /// </summary>
    private void FixedUpdate()
    {
        // 드래그 중인 경우 진형 형성 로직을 처리합니다.
        if (bdrag)
        {
            RaycastHit raycastHit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layer_Ground))
            {
                formation_End = raycastHit.point;

                if (Vector3.Distance(formation_Start, formation_End) > Constant.distance_Formation && bformation == false)
                {
                    bformation = true;
                    Draw_Formation_UI();
                }
            }

            if (bformation == true)
            {
                Drag_Formaion();
            }
        }
        // 부대 상태를 업데이트합니다.
        _Update_Army();
    }

    // 비공개 메서드
    /// <summary>
    /// 선택 기능을 위한 마우스 버튼 입력을 처리합니다.
    /// </summary>
    private void _Update_MouseButton_Select()
    {
        if (Input.GetKeyDown(keyCode_Select))
        {
            if (bdrag)
            {
                bdrag = false;
                bformation = false;
                Erase_Formation_UI();
            }

            // Ctrl 키를 누르지 않았을 경우, 선택된 부대를 모두 해제합니다.
            if (!Input.GetKey(keyCode_disable_clear))
            {
                for (int i = 0; i < armies_Selected.Count; i++)
                {
                    armies_Selected[i].UnSelected();
                }
                armies_Selected.Clear();
            }

            // 마우스 클릭 위치에 있는 유닛을 선택합니다.
            RaycastHit raycastHit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layer_Clickable))
            {
                bselect = false;
                Unit unit = raycastHit.transform.GetComponent<Unit>();

                if (unit.GetArmy_Data().bplayer && !unit.GetArmy().IsSelected())
                {
                    unit.GetArmy().Selected();
                    armies_Selected.Add(unit.GetArmy());
                }
            }
            else
            {
                // 클릭한 곳에 유닛이 없을 경우 드래그 선택을 시작합니다.
                bselect = true;
                select_Start = Input.mousePosition;
            }
        }

        if (Input.GetKey(keyCode_Select))
        {
            if (bselect)
            {
                select_End = Input.mousePosition;
                Draw_Drag_UI_Box();
            }
        }

        if (Input.GetKeyUp(keyCode_Select))
        {
            if (bselect)
            {
                Select_Drag();
            }
            Erase_Drag_UI();
            bselect = false;
        }
    }

    /// <summary>
    /// 명령 기능을 위한 마우스 버튼 입력을 처리합니다.
    /// </summary>
    private void _Update_MouseButton_Command()
    {
        if (Input.GetKeyDown(keyCode_Command))
        {
            if (bselect)
            {
                bselect = false;
                Erase_Drag_UI();
            }

            // 마우스 클릭 위치에 있는 지면을 감지하여 명령 드래그를 시작합니다.
            RaycastHit raycastHit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layer_Ground))
            {
                bdrag = true;
                formation_Start = raycastHit.point;
                bformation = false;
            }
        }

        if (Input.GetKey(keyCode_Command))
        {
            // 드래그 중인 상태를 유지합니다.
        }

        if (Input.GetKeyUp(keyCode_Command))
        {
            if (bdrag)
            {
                RaycastHit raycastHit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity, layer_Ground))
                {
                    if (bformation == false)
                    {
                        Start_Move_Click();
                    }
                    else
                    {
                        Start_Move_Drag();
                        Draw_Formation_UI();
                        Erase_Formation_UI();
                        bformation = false;
                    }
                }
                bdrag = false;
            }
        }
    }

    /// <summary>
    /// 드래그 선택 UI 박스를 화면에 그립니다.
    /// </summary>
    void Draw_Drag_UI_Box()
    {
        Vector2 drag_mid = (select_Start + select_End) / 2.0f;
        select_UI_Box.position = drag_mid;
        Vector2 uI_drag_Box_Size = new Vector2(Mathf.Abs(select_Start.x - select_End.x), Mathf.Abs(select_Start.y - select_End.y));
        select_UI_Box.sizeDelta = uI_drag_Box_Size;
    }

    /// <summary>
    /// 드래그 선택 UI를 지우고 변수를 초기화합니다.
    /// </summary>
    void Erase_Drag_UI()
    {
        select_Start = Vector2.zero;
        select_End = Vector2.zero;
        Draw_Drag_UI_Box();
    }

    /// <summary>
    /// 드래그 영역 내의 모든 유닛을 선택합니다.
    /// </summary>
    void Select_Drag()
    {
        // 드래그 영역의 Rect를 계산합니다.
        if (select_Start.x > select_End.x)
        {
            select_Box.xMax = select_Start.x;
            select_Box.xMin = select_End.x;
        }
        else
        {
            select_Box.xMax = select_End.x;
            select_Box.xMin = select_Start.x;
        }

        if (select_Start.y > select_End.y)
        {
            select_Box.yMax = select_Start.y;
            select_Box.yMin = select_End.y;
        }
        else
        {
            select_Box.yMax = select_End.y;
            select_Box.yMin = select_Start.y;
        }

        // 드래그 영역에 포함된 유닛을 찾아 선택합니다.
        foreach (Unit unit in units)
        {
            if (unit == null) continue;
            if (select_Box.Contains(Camera.main.WorldToScreenPoint(unit.transform.position)))
            {
                if (unit.GetArmy_Data().bplayer && !unit.GetArmy().IsSelected())
                {
                    unit.GetArmy().Selected();
                    armies_Selected.Add(unit.GetArmy());
                }
            }
        }
    }

    /// <summary>
    /// 부대의 진형을 드래그하여 형성합니다.
    /// </summary>
    void Drag_Formaion()
    {
        Set_Army_Formation(false);
    }

    /// <summary>
    /// 선택된 부대의 진형 UI를 그립니다.
    /// </summary>
    void Draw_Formation_UI()
    {
        foreach (Army army in armies_Selected)
        {
            army.Draw_Formation_UI();
        }
    }

    /// <summary>
    /// 선택된 부대의 진형 UI를 지웁니다.
    /// </summary>
    void Erase_Formation_UI()
    {
        foreach (Army army in armies_Selected)
        {
            army.Erase_Formation_UI();
        }
    }

    /// <summary>
    /// 드래그하여 형성된 진형으로 부대 이동을 시작합니다.
    /// </summary>
    void Start_Move_Drag()
    {
        Set_Army_Formation(true);
    }

    /// <summary>
    /// 클릭 지점으로 부대 이동을 시작합니다.
    /// </summary>
    void Start_Move_Click()
    {
        if (armies_Selected.Count == 0)
        {
            return;
        }

        Vector3 formation_Direction;
        Vector3 armies_Position = new Vector3();

        // 선택된 모든 부대의 평균 위치를 계산합니다.
        for (int i = 0; i < armies_Selected.Count; i++)
        {
            armies_Position = armies_Position + armies_Selected[i].GetPosition();
        }
        armies_Position = armies_Position / armies_Selected.Count;

        // 진형 방향을 설정합니다.
        formation_Direction = formation_Start - armies_Position;
        formation_Direction = Quaternion.AngleAxis(90, Vector3.up) * formation_Direction;

        float formation_Length = 0.0f;
        for (int i = 0; i < armies_Selected.Count; i++)
        {
            formation_Length = formation_Length + armies_Selected[i].GetFormation_Length();
        }

        Vector3 formation_Position = new Vector3();
        formation_Position = formation_Position - formation_Direction.normalized * formation_Length * 0.5f;

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            formation_Position =
                formation_Position
                + formation_Direction.normalized
                * armies_Selected[i].Set_Formation(formation_Direction, formation_Position).GetNum()
                * armies_Selected[i].army_Data.GetInterval();
        }

        // 헝가리안 알고리즘을 사용하여 부대와 진형 위치를 매칭합니다.
        List<Vector3> armies_position = new List<Vector3>();
        List<Vector3> armies_formation_position = new List<Vector3>();

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            armies_position.Add(armies_Selected[i].GetPosition());
            armies_formation_position.Add(armies_Selected[i].GetFormation_Position());
        }

        Hungarian hungarian = new Hungarian(armies_position, armies_formation_position);
        int[] _matchX = hungarian.Run();

        formation_Position = formation_Start;
        formation_Position = formation_Position - formation_Direction.normalized * formation_Length * 0.5f;

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            armies_Selected[_matchX[i]].Move_Start(formation_Direction, formation_Position);
            formation_Position = formation_Position + formation_Direction.normalized * armies_Selected[_matchX[i]].GetFormation_Length();
        }
    }

    /// <summary>
    /// 선택된 부대의 진형을 설정하고 이동을 시작합니다.
    /// </summary>
    /// <param name="bMove">이동을 시작할지 여부입니다.</param>
    public void Set_Army_Formation(bool bMove)
    {
        if (armies_Selected.Count == 0)
        {
            return;
        }

        // 진형 시작 및 끝 지점에 깃발을 표시합니다.
        Flag1.position = formation_Start;
        Flag2.position = formation_End;

        // 진형 방향과 위치를 계산합니다.
        float m1;
        float n1;

        m1 = (formation_End - formation_Start).z / (formation_End - formation_Start).x;
        n1 = formation_Start.z - m1 * formation_Start.x;

        List<float> floatList = new List<float>();

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            float m2 = -1.0f / m1;
            float n2 = armies_Selected[i].formation_Move_Transform.position.z - m2 * armies_Selected[i].formation_Move_Transform.position.x;
            float x = (n2 - n1) / (m1 - m2);
            if (formation_End.x < formation_Start.x) floatList.Add(x);
            else floatList.Add(-x);
        }

        // 부대 위치를 기준으로 정렬합니다.
        for (int i = 0; i < armies_Selected.Count; i++)
        {
            for (int j = 0; j < armies_Selected.Count; j++)
            {
                if (floatList[i] > floatList[j])
                {
                    Army army = armies_Selected[i];
                    armies_Selected[i] = armies_Selected[j];
                    armies_Selected[j] = army;

                    float float_Compare = floatList[i];
                    floatList[i] = floatList[j];
                    floatList[j] = float_Compare;
                }
            }
        }

        Vector3 formation_Direction;
        formation_Direction = formation_End - formation_Start;
        Vector3 formation_Position;
        List<float> formation_Lengths = new List<float>();

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            formation_Lengths.Add(0.0f);
        }

        float formation_Length;
        bool bformation = true;

        while (bformation)
        {
            int formationLength_Max_Num = 0;
            for (int i = 0; i < armies_Selected.Count; i++)
            {
                if (formation_Lengths[i] < armies_Selected[i].formationLength_Max)
                {
                    formation_Lengths[i] += armies_Selected[i].army_Data.GetInterval();
                }
                else
                {
                    formationLength_Max_Num += 1;
                }

                formation_Length = 0.0f;
                for (int j = 0; j < armies_Selected.Count; j++)
                {
                    formation_Length += formation_Lengths[j];
                }

                if (formation_Length > formation_Direction.magnitude)
                {
                    bformation = false;
                    break;
                }
                else if (formationLength_Max_Num == armies_Selected.Count)
                {
                    bformation = false;
                    break;
                }
            }
        }

        formation_Position = formation_Start;

        if (bMove)
        {
            for (int i = 0; i < armies_Selected.Count; i++)
            {
                armies_Selected[i].Move_Start(formation_Lengths[i], formation_Direction, formation_Position);
                formation_Position = formation_Position + formation_Direction.normalized * armies_Selected[i].GetFormation_Length();
            }
        }
        else
        {
            for (int i = 0; i < armies_Selected.Count; i++)
            {
                formation_Position =
                    formation_Position
                    + formation_Direction.normalized
                    * armies_Selected[i].Set_Formation(formation_Lengths[i], formation_Direction, formation_Position).GetNum()
                * armies_Selected[i].army_Data.GetInterval();
            }
        }
    }

    /// <summary>
    /// 모든 부대의 데이터를 업데이트합니다.
    /// </summary>
    private void _Update_Army()
    {
        // 1. 모든 유닛의 ID와 데이터를 담을 NativeHashMap을 생성합니다.
        var unitDataMap = new NativeHashMap<int, Unit_Data>(units.Count, Allocator.TempJob);
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;
            int colliderID = units[i].GetComponent<Collider>().GetInstanceID();
            unitDataMap.TryAdd(colliderID, units[i].unit_Data);
        }

        NativeArray<Army_Data> army_Datas;
        Army_Job army_Job;
        JobHandle jobHandle;

        army_Datas = new NativeArray<Army_Data>(armies.Count, Allocator.TempJob);

        for (int i = 0; i < armies.Count; i++)
        {
            army_Datas[i] = armies[i].army_Data;
        }

        army_Job = new Army_Job();
        army_Job.army_Datas = army_Datas;

        jobHandle = army_Job.Schedule(armies.Count, 1);
        jobHandle.Complete();

        for (int i = 0; i < armies.Count; i++)
        {
            armies[i].army_Data = army_Datas[i];
        }

        army_Datas.Dispose();

        // 2. 각 Army의 _Update 함수에 조회 테이블을 전달합니다.
        for (int i = 0; i < armies.Count; i++)
        {
            armies[i]._Update(unitDataMap);
        }

        // 3. 모든 작업이 끝난 후 NativeHashMap의 메모리를 해제합니다.
        unitDataMap.Dispose();
    }
}
