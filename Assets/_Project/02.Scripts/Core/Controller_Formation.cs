using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controller의 "진형 명령" 책임을 담당하는 부분 클래스입니다.
/// 우클릭 드래그로 진형을 구성하고 이동 명령을 내리는 로직을 담습니다.
/// </summary>
partial class Controller
{
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
            Ray ray = Main_Camera.Get().ScreenPointToRay(Input.mousePosition);

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
                Ray ray = Main_Camera.Get().ScreenPointToRay(Input.mousePosition);

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

        int[] _matchX = Formation_Matcher.Match(armies_position, armies_formation_position);
        if (_matchX == null) return;

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

        // 진형 축(시작 -> 끝) 위에 각 부대를 정사영하여 정렬 기준값을 구합니다.
        // 기울기(m = dz/dx) 방식은 드래그가 수직/수평일 때 0 또는 무한대가 되어
        // NaN이 진형 전체로 전파되므로, 내적 기반 투영으로 계산합니다.
        Vector3 axis = formation_End - formation_Start;
        axis.y = 0.0f;

        // 드래그 길이가 0에 가까우면 진형을 구성할 수 없습니다.
        if (axis.sqrMagnitude < Mathf.Epsilon) return;

        Vector3 axisNormalized = axis.normalized;

        List<float> projections = new List<float>();

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            Vector3 offset = armies_Selected[i].formation_Move_Transform.position - formation_Start;
            offset.y = 0.0f;

            // 축 위에서의 위치(스칼라 투영). 축 방향이 이미 정규화되어 있으므로 안전합니다.
            projections.Add(Vector3.Dot(offset, axisNormalized));
        }

        // 투영값 기준 오름차순으로 부대를 정렬합니다. (축 시작점에 가까운 부대가 앞)
        for (int i = 0; i < armies_Selected.Count - 1; i++)
        {
            for (int j = i + 1; j < armies_Selected.Count; j++)
            {
                if (projections[j] < projections[i])
                {
                    Army army = armies_Selected[i];
                    armies_Selected[i] = armies_Selected[j];
                    armies_Selected[j] = army;

                    float float_Compare = projections[i];
                    projections[i] = projections[j];
                    projections[j] = float_Compare;
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

        // interval이 0 이하로 설정되면 formation_Lengths가 영원히 늘지 않아
        // 두 종료 조건이 모두 성립하지 않습니다. 반복 상한으로 무한 루프를 차단합니다.
        int guard = 0;
        const int guard_Max = 10000;

        while (bformation)
        {
            if (++guard > guard_Max)
            {
                Debug.LogWarning("Set_Army_Formation: 진형 길이 분배가 수렴하지 않았습니다. " +
                                 "Army_Data의 interval 값이 0보다 큰지 확인하십시오.");
                break;
            }

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
}
