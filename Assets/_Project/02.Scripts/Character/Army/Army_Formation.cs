
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 부대의 진형 관련 로직을 담당하는 부분 클래스입니다.
/// </summary>
partial class Army
{
    // 비공개 멤버 변수 (Army.cs에서 관리)
    // 공개 메서드 (Army.cs에서 관리)
    // Unity 이벤트 함수 (Army.cs에서 관리)

    // 비공개 메서드
    /// <summary>
    /// 유닛들의 포메이션 상태를 업데이트하는 함수입니다.
    /// 유닛의 절반 이상이 포지션에 있으면 포메이션 상태로 설정합니다.
    /// </summary>
    void _Update_Formation()
    {
        int army_InPosition_Num = 0;
        for (int i = 0; i < units.Count; i++)
        {
            if (unit_Datas[i].IsOnPosition(army_Data))
            {
                army_InPosition_Num += 1;
            }
        }

        if (army_InPosition_Num < units.Count * 0.5f)
        {
            army_Data.e_Army_Formation = E_Army_Formation.NonFormation;
        }
        else
        {
            army_Data.e_Army_Formation = E_Army_Formation.Formation;
        }
    }

    /// <summary>
    /// 진형의 가로 유닛 수를 유효 범위로 보정합니다.
    /// 최대 진형 길이, 부대 인원 수, 최소값(1)을 모두 만족하도록 조정합니다.
    /// </summary>
    /// <param name="num_width">보정할 가로 유닛 수입니다.</param>
    /// <returns>보정된 가로 유닛 수입니다.</returns>
    private int Clamp_Formation_Width(int num_width)
    {
        float interval = army_Data.GetInterval();

        // interval이 0 이하이면 진형 계산이 성립하지 않습니다.
        // (0으로 나누면 Infinity가 전파되고 아래 while이 종료되지 않습니다)
        if (interval <= 0.0f)
        {
            Debug.LogWarning($"{name}: Army_Data의 interval이 0 이하입니다. 진형 폭을 1로 고정합니다.");
            return 1;
        }

        if (num_width > formationLength_Max / interval)
            num_width = Mathf.RoundToInt(formationLength_Max / interval);
        else if (num_width < 1)
            num_width = 1;
        else if (num_width > army_Data.unit_Num)
            num_width = army_Data.unit_Num;

        if (num_width < 1) num_width = 1;

        // 부대 인원을 num_width로 나눈 '열 수'가 최대 길이를 넘지 않을 때까지 폭을 넓힙니다.
        // num_width는 단조 증가하고 unit_Num을 넘으면 좌변이 0이 되므로,
        // formationLength_Max가 0 이상이면 반드시 종료됩니다.
        int guard_Max = army_Data.unit_Num + 1;
        while (num_width <= guard_Max
               && army_Data.unit_Num / num_width * interval > formationLength_Max)
        {
            num_width++;
        }

        if (num_width < 1) num_width = 1;

        return num_width;
    }

    // 공개 메서드
    /// <summary>
    /// 기본 포메이션 데이터를 설정합니다.
    /// </summary>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation()
    {
        return Set_Formation(GetFormation_Data());
    }

    /// <summary>
    /// 방향과 위치를 이용해 포메이션 데이터를 설정합니다.
    /// </summary>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(Vector3 direction, Vector3 position)
    {
        float num = formation_Data.GetNum();
        return Set_Formation(num, direction, position);
    }

    /// <summary>
    /// 기존 Formation_Data를 이용해 포메이션을 설정합니다.
    /// </summary>
    /// <param name="formation_Data">사용할 Formation_Data입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(Formation_Data formation_Data)
    {
        float length = GetFormation_Length();
        Vector3 direction = formation_Data.GetDirection();
        Vector3 position = formation_Data.GetPosition();
        return Set_Formation(length, direction, position);
    }

    /// <summary>
    /// 길이, 방향, 위치를 이용해 포메이션 데이터를 설정합니다.
    /// </summary>
    /// <param name="length">진형의 길이입니다. 간격으로 나누어 가로 유닛 수로 환산합니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(float length, Vector3 direction, Vector3 position)
    {
        float interval = army_Data.GetInterval();
        int num_width = interval > 0.0f ? Mathf.RoundToInt(length / interval) : 1;

        // 주의: 이 오버로드는 UI 마커를 -add_vertical 방향으로 세웁니다.
        //       아래 int 오버로드는 +add_vertical을 씁니다. 원본부터 서로 달랐던
        //       동작이라 그대로 보존했습니다. 통일이 필요하면 여기 부호를 맞추십시오.
        return Set_Formation_Internal(num_width, direction, position, -1.0f);
    }

    /// <summary>
    /// 유닛 수, 방향, 위치를 이용해 포메이션 데이터를 설정합니다.
    /// </summary>
    /// <param name="num">진형의 너비를 결정하는 유닛 수입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(int num, Vector3 direction, Vector3 position)
    {
        return Set_Formation_Internal(num, direction, position, 1.0f);
    }

    /// <summary>
    /// 진형 좌표를 계산하는 공통 구현입니다.
    /// 원본에서 4곳에 복사되어 있던 로직을 한 곳으로 모았습니다.
    /// </summary>
    /// <param name="num_width">보정 전 가로 유닛 수입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 기준 위치입니다.</param>
    /// <param name="markerFacingSign">
    /// UI 마커가 바라볼 방향의 부호입니다. 호출부마다 달랐던 기존 동작을 유지하기 위한 값입니다.
    /// </param>
    private Formation_Data Set_Formation_Internal(int num_width, Vector3 direction, Vector3 position, float markerFacingSign)
    {
        Vector3 add_width = direction.normalized * army_Data.GetInterval();
        Vector3 add_vertical = Quaternion.AngleAxis(90, Vector3.up) * add_width;

        num_width = Clamp_Formation_Width(num_width);

        int unitNum = army_Data.unit_Num;
        if (unitNum <= 0)
        {
            // 전멸한 부대는 계산할 진형이 없습니다.
            return new Formation_Data(num_width, add_width, position, new List<Vector3>());
        }

        Vector3 formation_Start = position + add_width * num_width * 0.5f;

        NativeArray<Vector3> locationMoveTo = new NativeArray<Vector3>(unitNum, Allocator.TempJob);

        Formation_Job formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        JobHandle jobHandle = formation_Job.Schedule(unitNum, Constant.jobBatchCount);
        jobHandle.Complete();

        List<Vector3> vector3s = new List<Vector3>();
        vector3s.AddRange(locationMoveTo);

        Vector3 markerFacing = add_vertical * markerFacingSign;
        for (int i = 0; i < unitNum && i < uI_Units.Count; i++)
        {
            uI_Units[i]._Update(vector3s[i], markerFacing);
        }

        locationMoveTo.Dispose();

        return new Formation_Data(num_width, add_width, vector3s[0], vector3s);
    }

    /// <summary>
    /// 포메이션 이동을 위한 데이터를 설정합니다.
    /// </summary>
    public void Set_Formation_Move()
    {
        // 전멸했으면 배치할 것이 없습니다. (길이 0 NativeArray 생성도 피합니다)
        int unitNum = army_Data.unit_Num;
        if (unitNum <= 0) return;

        Vector3 add_width = formation_Move_Transform.right * army_Data.GetInterval();
        Vector3 add_vertical = formation_Move_Transform.forward * army_Data.GetInterval() * -1.0f;

        Vector3 formation_Start = formation_Move_Transform.position;

        NativeArray<Vector3> locationMoveTo = new NativeArray<Vector3>(unitNum, Allocator.TempJob);

        int num_width = Clamp_Formation_Width(GetFormation_Num());

        Formation_Job formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        JobHandle jobHandle = formation_Job.Schedule(locationMoveTo.Length, Constant.jobBatchCount);
        jobHandle.Complete();

        // formation_Moves는 '생성 시점 인원'만큼 만들어져 있고 전사해도 줄지 않습니다.
        // 반면 locationMoveTo는 '현재 생존 인원'(unit_Num) 크기입니다.
        // 따라서 반드시 둘 중 작은 쪽까지만 순회해야 합니다.
        // (이 가드가 없으면 전사자가 생기는 순간 IndexOutOfRangeException이 납니다)
        int count = Mathf.Min(formation_Moves.Count, locationMoveTo.Length);

        for (int i = 0; i < count; i++)
        {
            formation_Moves[i].position = locationMoveTo[i];
        }

        locationMoveTo.Dispose();
    }

    /// <summary>
    /// 현재 진형 데이터를 반환합니다.
    /// </summary>
    /// <returns>Formation_Data 인스턴스입니다.</returns>
    public Formation_Data GetFormation_Data()
    {
        return formation_Data;
    }

    /// <summary>
    /// 현재 진형의 방향을 반환합니다.
    /// </summary>
    /// <returns>방향 벡터입니다.</returns>
    public Vector3 GetFormation_Direction()
    {
        return formation_Data.GetDirection();
    }

    /// <summary>
    /// 현재 진형의 위치를 반환합니다.
    /// </summary>
    /// <returns>위치 벡터입니다.</returns>
    public Vector3 GetFormation_Position()
    {
        return formation_Data.GetPosition();
    }

    /// <summary>
    /// 현재 진형의 너비를 반환합니다.
    /// </summary>
    /// <returns>너비 값입니다.</returns>
    public int GetFormation_Num()
    {
        return formation_Data.GetNum();
    }

    /// <summary>
    /// 현재 진형의 길이를 계산하여 반환합니다.
    /// </summary>
    /// <returns>진형 길이입니다.</returns>
    public float GetFormation_Length()
    {
        return formation_Data.GetNum() * army_Data.GetInterval();
    }

    /// <summary>
    /// 진형 데이터를 설정합니다.
    /// </summary>
    /// <param name="formation_Data">설정할 Formation_Data입니다.</param>
    public void Set_Formation_Data(Formation_Data formation_Data)
    {
        this.formation_Data.Set_Formation_Data(formation_Data);
    }

    /// <summary>
    /// 진형 데이터를 설정합니다.
    /// </summary>
    /// <param name="num">진형의 너비입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <param name="formation">유닛들의 진형 위치 리스트입니다.</param>
    public void Set_Formation_Data(int num, Vector3 direction, Vector3 position, List<Vector3> formation)
    {
        formation_Data.Set_Formation_Data(num, direction, position, formation);
    }
}
