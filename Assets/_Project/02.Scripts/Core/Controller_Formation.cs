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

        // 예전에는 여기서 월드 원점을 기준으로 Set_Formation을 한 바퀴 돌렸습니다.
        // 그 결과값은 곧바로 버려지고 아래에서 formation_Start 기준으로 다시
        // 계산했으므로, 실제 효과는 '진형 마커가 원점 근처로 한 번 튀는' 것뿐이었습니다.
        // 진형 규약을 정리하면서 함께 제거합니다.

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

        // 부대들을 전열축을 따라 나란히 세웁니다.
        //
        // edge는 '다음 부대가 시작할 가장자리'이고, 각 부대에 넘기는 값은
        // '그 부대 대열의 한가운데'입니다. 진형 좌표 규약이 중심이므로
        // 여기서 절반 길이만큼 안쪽으로 넣어 줘야 합니다.
        // (가장자리라는 개념을 가진 곳은 이 함수뿐입니다)
        Vector3 lineDirection = formation_Direction.normalized;
        Vector3 edge = formation_Start - lineDirection * (formation_Length * 0.5f);

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            Army army = armies_Selected[_matchX[i]];

            float length = army.GetFormation_Length();

            army.Move_Start(formation_Direction, edge + lineDirection * (length * 0.5f));

            edge = edge + lineDirection * length;
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

        // 드래그한 길이를 선택된 부대들에 나눠 줍니다.
        //
        // 배분 방식:
        // 부대마다 한 칸(interval)씩 돌아가며 늘립니다. 한 부대에 몰아주지
        // 않고 번갈아 늘리므로, 드래그 길이가 모자라도 전 부대가 고르게
        // 좁아집니다. 앞쪽 부대만 넓고 뒤쪽은 한 줄이 되는 일이 없습니다.
        //
        // 종료 조건은 둘입니다.
        //   1) 합계가 드래그 길이를 넘음   -> 더 넓힐 공간이 없음
        //   2) 전 부대가 자기 최대 폭 도달 -> 더 넓힐 이유가 없음
        while (bformation)
        {
            if (++guard > guard_Max)
            {
                Debug.LogWarning("Set_Army_Formation: 진형 길이 분배가 수렴하지 않았습니다. " +
                                 "Army_Data의 interval 값이 0보다 큰지 확인하십시오.");
                break;
            }

            // 이번 회차에 '더 이상 넓힐 수 없는' 부대 수입니다.
            int formationLength_Max_Num = 0;

            for (int i = 0; i < armies_Selected.Count; i++)
            {
                // 아직 여유가 있으면 한 칸 넓힙니다.
                if (formation_Lengths[i] < armies_Selected[i].formationLength_Max)
                {
                    formation_Lengths[i] += armies_Selected[i].army_Data.GetInterval();
                }
                else
                {
                    formationLength_Max_Num += 1;
                }

                // 한 칸 늘릴 때마다 합계를 다시 확인합니다.
                //
                // 회차가 끝난 뒤에 확인하면 마지막 회차에서 여러 부대가
                // 동시에 넘겨 드래그 길이를 크게 초과할 수 있습니다.
                formation_Length = 0.0f;
                for (int j = 0; j < armies_Selected.Count; j++)
                {
                    formation_Length += formation_Lengths[j];
                }

                // 종료 조건 1: 드래그한 길이를 다 썼습니다.
                if (formation_Length > formation_Direction.magnitude)
                {
                    bformation = false;
                    break;
                }
                // 종료 조건 2: 전 부대가 최대 폭에 도달했습니다.
                else if (formationLength_Max_Num == armies_Selected.Count)
                {
                    bformation = false;
                    break;
                }
            }
        }

        // 드래그한 선을 따라 부대들을 나란히 세웁니다.
        //
        // formation_Position은 '다음 부대가 시작할 가장자리'입니다.
        // 각 부대에 실제로 넘기는 값은 그 부대 대열의 '한가운데'이므로,
        // 미리 구한 실제 길이의 절반만큼 안쪽으로 넣습니다.
        // (진형 좌표 규약은 언제나 중심입니다. Army_Formation.cs 참조)
        //
        // 실제 길이를 Predict_Formation_Length로 미리 묻는 이유:
        // 요청 길이는 간격과 최대 폭 보정을 거쳐 달라질 수 있습니다.
        // 요청값으로 중심을 잡고 실제값으로 가장자리를 밀면 부대 사이가
        // 조금씩 어긋나 누적됩니다. 두 곳 모두 실제 길이를 써야 합니다.
        Vector3 lineDirection = formation_Direction.normalized;
        formation_Position = formation_Start;

        for (int i = 0; i < armies_Selected.Count; i++)
        {
            Army army = armies_Selected[i];

            float length = army.Predict_Formation_Length(formation_Lengths[i]);
            Vector3 center = formation_Position + lineDirection * (length * 0.5f);

            if (bMove)
            {
                army.Move_Start(formation_Lengths[i], formation_Direction, center);
            }
            else
            {
                // 드래그 미리보기입니다. 이동 명령 없이 진형만 계산해 마커를 갱신합니다.
                army.Set_Formation(formation_Lengths[i], formation_Direction, center);
            }

            formation_Position = formation_Position + lineDirection * length;
        }
    }
}
