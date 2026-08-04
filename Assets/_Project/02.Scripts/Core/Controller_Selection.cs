using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controller의 "부대 선택" 책임을 담당하는 부분 클래스입니다.
/// 드래그 박스 선택과 클릭 선택 입력을 처리합니다.
/// </summary>
partial class Controller
{
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
            // 누르고 있으면 기존 선택에 더합니다. (RTS의 관례입니다)
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
            Ray ray = Main_Camera.Get().ScreenPointToRay(Input.mousePosition);

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
            if (select_Box.Contains(Main_Camera.Get().WorldToScreenPoint(unit.transform.position)))
            {
                if (unit.GetArmy_Data().bplayer && !unit.GetArmy().IsSelected())
                {
                    unit.GetArmy().Selected();
                    armies_Selected.Add(unit.GetArmy());
                }
            }
        }
    }
}
