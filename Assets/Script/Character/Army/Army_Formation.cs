using System.Collections;
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
            if (unit_Datas[i].IsOnPosition())
            {
                army_InPosition_Num += 1;
            }
        }

        if (army_InPosition_Num < unit_Datas.Length * 0.5f)
        {
            army_Data.e_Army_Formation = E_Army_Formation.NonFormation;
        }
        else
        {
            army_Data.e_Army_Formation = E_Army_Formation.Formation;
        }
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
    /// <param name="length">진형의 길이입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(float length, Vector3 direction, Vector3 position)
    {
        Vector3 add_vertical;
        Vector3 add_width;
        int num_width;
        Vector3 formation_Start;
        NativeArray<Vector3> locationMoveTo;
        Formation_Job formation_Job;
        JobHandle jobHandle;

        add_width = direction.normalized * army_Data.GetInterval();
        add_vertical = Quaternion.AngleAxis(90, Vector3.up) * add_width;

        num_width = Mathf.RoundToInt(length / army_Data.GetInterval());

        if (num_width > formationLength_Max / army_Data.GetInterval())
            num_width = Mathf.RoundToInt(formationLength_Max / army_Data.GetInterval());
        else if (num_width < 1)
            num_width = 1;
        else if (num_width > army_Data.unit_Num)
            num_width = army_Data.unit_Num;

        while (army_Data.unit_Num / num_width * army_Data.GetInterval() > formationLength_Max)
        {
            num_width++;
        }

        formation_Start = position + add_width * num_width * 0.5f;
        locationMoveTo = new NativeArray<Vector3>(army_Data.unit_Num, Allocator.TempJob);

        formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        jobHandle = formation_Job.Schedule(army_Data.unit_Num, 1);
        jobHandle.Complete();

        List<Vector3> vector3s = new List<Vector3>();
        vector3s.AddRange(formation_Job.locationMoveTo);

        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            uI_Units[i]._Update(vector3s[i], -add_vertical);
        }

        locationMoveTo.Dispose();

        return new Formation_Data(num_width, add_width, vector3s[0], vector3s);
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
        Vector3 add_vertical;
        Vector3 add_width;
        int num_width;
        Vector3 formation_Start;
        NativeArray<Vector3> locationMoveTo;
        Formation_Job formation_Job;
        JobHandle jobHandle;

        add_width = direction.normalized * army_Data.GetInterval();
        add_vertical = Quaternion.AngleAxis(90, Vector3.up) * add_width;
        num_width = num;

        if (num_width > formationLength_Max / army_Data.GetInterval())
            num_width = Mathf.RoundToInt(formationLength_Max / army_Data.GetInterval());
        else if (num_width < 1)
            num_width = 1;
        else if (num_width > army_Data.unit_Num)
            num_width = army_Data.unit_Num;

        while (army_Data.unit_Num / num_width * army_Data.GetInterval() > formationLength_Max)
        {
            num_width++;
        }

        formation_Start = position + add_width * num_width * 0.5f;
        locationMoveTo = new NativeArray<Vector3>(army_Data.unit_Num, Allocator.TempJob);

        formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        jobHandle = formation_Job.Schedule(army_Data.unit_Num, 1);
        jobHandle.Complete();

        List<Vector3> vector3s = new List<Vector3>();
        vector3s.AddRange(formation_Job.locationMoveTo);

        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            uI_Units[i]._Update(vector3s[i], add_vertical);
        }

        locationMoveTo.Dispose();

        return new Formation_Data(num_width, add_width, vector3s[0], vector3s);
    }

    /// <summary>
    /// 포메이션 이동을 위한 데이터를 설정합니다.
    /// </summary>
    public void Set_Formation_Move()
    {
        Vector3 add_width = formation_Move_Transform.right * army_Data.GetInterval();
        Vector3 add_vertical = formation_Move_Transform.forward * army_Data.GetInterval() * -1.0f;

        int num_width;
        Vector3 formation_Start;
        formation_Start = formation_Move_Transform.position;

        NativeArray<Vector3> locationMoveTo = new NativeArray<Vector3>(army_Data.unit_Num, Allocator.TempJob);
        num_width = GetFormation_Num();

        if (num_width > formationLength_Max / army_Data.GetInterval())
            num_width = Mathf.RoundToInt(formationLength_Max / army_Data.GetInterval());
        else if (num_width < 1)
            num_width = 1;
        else if (num_width > army_Data.unit_Num)
            num_width = army_Data.unit_Num;

        while (army_Data.unit_Num / num_width * army_Data.GetInterval() > formationLength_Max)
        {
            num_width++;
        }

        Formation_Job formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        JobHandle jobHandle = formation_Job.Schedule(locationMoveTo.Length, 1);
        jobHandle.Complete();

        for (int i = 0; i < formation_Moves.Count; i++)
        {
            formation_Moves[i].position = locationMoveTo[i];
        }

        locationMoveTo.Dispose();
    }

    /// <summary>
    /// 정지 상태에서 진형 데이터를 설정합니다.
    /// </summary>
    /// <param name="length">진형의 길이입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation_Stop(float length, Vector3 direction, Vector3 position)
    {
        Vector3 add_vertical;
        Vector3 add_width;
        int num_width;
        Vector3 formation_Start;
        NativeArray<Vector3> locationMoveTo;
        Formation_Job formation_Job;
        JobHandle jobHandle;

        add_width = direction.normalized * army_Data.GetInterval();
        add_vertical = Quaternion.AngleAxis(90, Vector3.up) * add_width;

        num_width = Mathf.RoundToInt(length / army_Data.GetInterval());

        if (num_width > formationLength_Max / army_Data.GetInterval())
            num_width = Mathf.RoundToInt(formationLength_Max / army_Data.GetInterval());
        else if (num_width < 1)
            num_width = 1;
        else if (num_width > army_Data.unit_Num)
            num_width = army_Data.unit_Num;

        while (army_Data.unit_Num / num_width * army_Data.GetInterval() > formationLength_Max)
        {
            num_width++;
        }

        formation_Start = position;
        locationMoveTo = new NativeArray<Vector3>(army_Data.unit_Num, Allocator.TempJob);

        formation_Job = new Formation_Job();
        formation_Job.locationMoveTo = locationMoveTo;
        formation_Job.formation_Start = formation_Start;
        formation_Job.add_width = add_width;
        formation_Job.add_vertical = add_vertical;
        formation_Job.num_width = num_width;

        jobHandle = formation_Job.Schedule(army_Data.unit_Num, 1);
        jobHandle.Complete();

        List<Vector3> vector3s = new List<Vector3>();
        vector3s.AddRange(formation_Job.locationMoveTo);

        for (int i = 0; i < army_Data.unit_Num; i++)
        {
            uI_Units[i]._Update(vector3s[i], -add_vertical);
        }

        locationMoveTo.Dispose();

        return new Formation_Data(num_width, add_width, vector3s[0], vector3s);
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
    /// 진형이 최대 길이인지 여부를 확인합니다.
    /// </summary>
    /// <returns>최대 길이면 true, 아니면 false입니다.</returns>
    public bool IsFormation_Max()
    {
        return formation_Data.GetNum() >= formationLength_Max;
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
