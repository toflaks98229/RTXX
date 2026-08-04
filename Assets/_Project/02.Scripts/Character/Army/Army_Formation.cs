
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

    // =====================================================================
    // 진형 슬롯 (데이터 배열)
    //
    // 슬롯은 원래 부대 기준점의 자식 Transform(formation_Moves)이었습니다.
    // 기준점이 움직이면 Unity가 자식들을 자동으로 따라 옮겨 주므로 코드가
    // 매 틱 갱신할 필요가 없었지만, 대신 '읽을 때마다' 네이티브 왕복을 냅니다.
    //
    // 실측: 슬롯 읽기 2.955 ms/틱 vs 배열 읽기 0.029 ms/틱 (약 102배)
    // 갱신은 명령 시에만 일어나고 소비는 매 틱이므로, 읽기가 비싼 구조는
    // 정확히 반대로 최적화되어 있었습니다.
    // =====================================================================

    /// <summary>
    /// 진형 슬롯 좌표의 소유자입니다.
    ///
    /// 예전에는 배열 두 벌(지역/월드 캐시)과 유효 플래그가 Army의 필드로
    /// 흩어져 있었습니다. 그 셋의 정합성은 '누가 언제 무효화하는가'에
    /// 달려 있는데, 5,500줄짜리 타입 안에서는 그 계약을 읽어 낼 수 없습니다.
    ///
    /// 실제로 배치 단계에서 캐시를 무효화하지 않아 "드래그로 옮긴 뒤
    /// 태세를 바꾸면 부대가 원래 자리로 돌아가는" 버그가 났습니다.
    ///
    /// 이제 슬롯을 만지는 방법은 Formation_Slots의 네 개뿐입니다.
    /// 그 밖의 경로가 없으므로 캐시가 조용히 낡을 수 없습니다.
    ///
    /// _Start에서 만듭니다. 기준점(formation_Move_Transform)이 필요하고,
    /// 그 값은 인스펙터에서 배선되므로 그전에는 유효하지 않습니다.
    /// </summary>
    private Formation_Slots slots;

    /// <summary>채워진 진형 슬롯 수입니다.</summary>
    public int SlotCount => slots != null ? slots.Count : 0;

    /// <summary>
    /// 슬롯 소유자를 준비합니다. 유닛을 만들기 전에 한 번 호출합니다.
    /// </summary>
    private void Init_Slots()
    {
        slots = new Formation_Slots(formation_Move_Transform);
    }

    /// <summary>
    /// 월드 좌표 캐시를 무효화합니다.
    ///
    /// 기준점이 움직였을 수 있으므로 매 틱 시작에 호출합니다.
    /// 실제 계산은 '처음 읽을 때'로 미룹니다. 이동 중이 아닌 부대는
    /// 아무도 슬롯을 읽지 않으므로 계산 자체가 일어나지 않습니다.
    /// </summary>
    public void Invalidate_Slot_World()
    {
        slots?.Invalidate();
        bslotOwnerValid = false;
    }

    // =====================================================================
    // 슬롯 소유자 역색인 (선두 지정의 토대)
    //
    // 유닛은 자기 슬롯 번호(targetSlotIndex)를 압니다. 그 반대 방향
    // ('이 슬롯에 선 유닛이 누구인가')은 지금까지 물을 수 없었습니다.
    //
    // 선두 지정은 그 반대 방향이 필요합니다. 어떤 유닛의 앞줄 유닛을
    // 찾으려면 '같은 오(file)의 rank 0 슬롯을 든 유닛'을 알아야 합니다.
    //
    // 이 단계에서는 조회만 추가하고 이동 로직은 건드리지 않습니다.
    // 아무도 묻지 않으면 재구축도 일어나지 않으므로 비용이 0입니다.
    // =====================================================================

    /// <summary>슬롯 번호 -> units 리스트 인덱스입니다. 비어 있으면 -1입니다.</summary>
    private int[] slotOwners;

    /// <summary>소유자 색인이 지금 기준으로 유효한지 여부입니다.</summary>
    private bool bslotOwnerValid;

    /// <summary>
    /// 슬롯 소유자 색인을 다시 만듭니다.
    ///
    /// 유닛이 죽거나 슬롯 배정이 바뀌면 낡으므로, 월드 좌표 캐시와 같은
    /// 시점(매 틱 시작)에 무효화하고 '처음 물을 때' 다시 만듭니다.
    /// </summary>
    private void Rebuild_Slot_Owners()
    {
        int count = SlotCount;

        if (slotOwners == null || slotOwners.Length < count)
        {
            slotOwners = new int[Mathf.Max(count, 16)];
        }

        for (int i = 0; i < count; i++) slotOwners[i] = -1;

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == null) continue;

            int slot = units[i].targetSlotIndex;
            if (slot < 0 || slot >= count) continue;

            // 같은 슬롯을 둘이 들고 있으면 앞선 유닛을 남깁니다.
            // 정상 상태에서는 일어나지 않으며, 검증기가 그 수를 셉니다.
            if (slotOwners[slot] < 0) slotOwners[slot] = i;
        }

        bslotOwnerValid = true;
    }

    /// <summary>
    /// 이 슬롯에 배정된 유닛입니다. 없으면 null입니다.
    /// </summary>
    /// <param name="slotIndex">슬롯 인덱스입니다.</param>
    public Unit Get_Slot_Owner(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return null;

        if (!bslotOwnerValid) Rebuild_Slot_Owners();

        int unitIndex = slotOwners[slotIndex];
        if (unitIndex < 0 || unitIndex >= units.Count) return null;

        return units[unitIndex];
    }

    /// <summary>이 슬롯이 속한 오(세로줄) 번호입니다.</summary>
    /// <param name="slotIndex">슬롯 인덱스입니다.</param>
    public int Get_Slot_File(int slotIndex)
    {
        return Formation_Slots.File_Of(slotIndex, GetFormation_Num());
    }

    /// <summary>이 슬롯이 속한 열 번호입니다. 0이 맨 앞줄입니다.</summary>
    /// <param name="slotIndex">슬롯 인덱스입니다.</param>
    public int Get_Slot_Rank(int slotIndex)
    {
        return Formation_Slots.Rank_Of(slotIndex, GetFormation_Num());
    }

    /// <summary>
    /// 이 오(세로줄)의 선두 유닛입니다.
    ///
    /// 맨 앞줄이 비어 있으면(전사·미배정) 그 뒤에서 가장 앞선 유닛을
    /// 돌려줍니다. 이것이 곧 선두 승격 규칙이며, 지금은 순수한 조회라
    /// 어떤 동작도 바꾸지 않습니다.
    ///
    /// 반환값이 null이면 그 오에 남은 유닛이 없다는 뜻입니다.
    /// </summary>
    /// <param name="file">오 번호입니다.</param>
    public Unit Get_File_Leader(int file)
    {
        int width = GetFormation_Num();
        if (width <= 0) return null;
        if (file < 0 || file >= width) return null;

        int count = SlotCount;

        for (int rank = 0; ; rank++)
        {
            int slot = Formation_Slots.Index_Of(file, rank, width);
            if (slot < 0 || slot >= count) return null;

            Unit owner = Get_Slot_Owner(slot);
            if (owner != null && !owner.IsDead()) return owner;
        }
    }

    // ---------------------------------------------------------------------
    // 진형 좌표 규약 — 하나뿐입니다
    // ---------------------------------------------------------------------
    //     position 은 언제나 '대열의 한가운데'다. 입력도, 저장값도.
    //
    // Formation_Job이 formation_Start를 기준으로 좌우 번갈아 슬롯을
    // 펼치므로, formation_Start가 곧 열의 한가운데입니다.
    // 따라서 Set_Formation 계열은 받은 position을 그대로 formation_Start로
    // 쓰고, 같은 값을 Formation_Data.position에 저장합니다.
    //
    // 그 덕분에 **나온 값을 그대로 다시 넣어도 같은 자리에 만들어집니다.**
    // (Move_Start()가 GetFormation_Position()을 되먹이는 경로가 그렇습니다)
    //
    // ---------------------------------------------------------------------
    // 예전 규약이 냈던 버그 (되돌리지 마십시오)
    // ---------------------------------------------------------------------
    // 예전에는 입력 position이 '왼쪽 끝'이고 저장값은 '첫 슬롯'이었습니다.
    //
    //     formation_Start = position + add_width * num_width * 0.5f;   // 입력: 왼쪽 끝
    //     return new Formation_Data(..., vector3s[0], vector3s);        // 저장: 첫 슬롯
    //
    // 입력·내부·저장의 뜻이 셋 다 달랐습니다. 그래서 출력을 입력으로
    // 되먹이면 매번 절반 폭씩 옆으로 밀려났고, 태세를 바꿀 때마다 대열이
    // 이동하는 버그로 두 번 나타났습니다. 그때는 "기준점을 옮기고
    // Set_Formation_Move를 부르라"는 관례로 우회했지만, 관례는 다음 사람에게
    // 전달되지 않습니다. 규약 자체를 하나로 만든 것이 이 변경입니다.
    //
    // 왼쪽 끝을 기준으로 여러 부대를 나란히 세우는 호출부
    // (Controller_Formation)는 자기 쪽에서 중심으로 환산합니다.
    // 그쪽이 '가장자리'라는 개념을 가진 유일한 곳이기 때문입니다.

    /// <summary>
    /// 슬롯 하나의 현재 월드 좌표입니다.
    /// 범위를 벗어나면 부대 기준점 위치를 돌려줍니다.
    /// </summary>
    public Vector3 Get_Slot_World(int index)
    {
        if (slots == null) return formation_Move_Transform.position;

        return slots.Get_World(index);
    }

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
    //
    // 오버로드 세 개가 제거되었습니다. 전부 호출부가 없었습니다.
    //
    //   Set_Formation()
    //   Set_Formation(Formation_Data)
    //   Set_Formation(Vector3 direction, Vector3 position)
    //
    // 두 번째는 인자로 받은 formation_Data에서 방향과 위치만 꺼내 쓰면서
    // 길이는 자기 필드(GetFormation_Length)에서 읽었습니다. 즉 "내가 넘긴
    // 값이 쓰인다"고 읽히지만 실제로는 절반만 쓰이는 함수였습니다.
    //
    // 세 번째는 유닛 '수'를 길이(미터)를 받는 오버로드에 넘기고 있어
    // 폭이 num/interval로 계산됐습니다. 부르는 곳이 없어 드러나지 않았을 뿐
    // 그 자체로 틀린 코드였습니다.
    //
    // 남은 둘(길이 기준 / 인원 수 기준)이 실제로 쓰이는 전부입니다.

    /// <summary>
    /// 길이, 방향, 위치를 이용해 포메이션 데이터를 설정합니다.
    /// </summary>
    /// <param name="length">진형의 길이입니다. 간격으로 나누어 가로 유닛 수로 환산합니다.</param>
    /// <param name="direction">진형의 전열축 방향입니다.</param>
    /// <param name="position">대열의 한가운데가 될 위치입니다.</param>
    /// <returns>설정된 Formation_Data입니다.</returns>
    public Formation_Data Set_Formation(float length, Vector3 direction, Vector3 position)
    {
        float interval = army_Data.GetInterval();
        int num_width = interval > 0.0f ? Mathf.RoundToInt(length / interval) : 1;

        return Set_Formation_Internal(num_width, direction, position);
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
        return Set_Formation_Internal(num, direction, position);
    }

    /// <summary>
    /// 진형 좌표를 계산하는 공통 구현입니다.
    /// 원본에서 4곳에 복사되어 있던 로직을 한 곳으로 모았습니다.
    /// </summary>
    /// <param name="num_width">보정 전 가로 유닛 수입니다.</param>
    /// <param name="direction">진형의 방향입니다.</param>
    /// <param name="position">진형의 기준 위치입니다.</param>
    private Formation_Data Set_Formation_Internal(int num_width, Vector3 direction, Vector3 position)
    {
        // 진형 축이 정해지지 않으면 계산 전체가 무너집니다.
        //
        // add_width는 아래에서 Formation_Data.direction으로 저장되어,
        // 이후 GetFormation_Direction()과 UI 마커 회전의 근거가 됩니다.
        // 이 값이 영벡터가 되면 Quaternion.LookRotation이 Transform을 손상시키고
        // Unity가 'Invalid localAABB' / 'IsFinite' 어서션을 매 프레임 쏟아냅니다.
        //
        // 영벡터가 되는 경로는 둘입니다.
        //   1) 호출부가 영벡터 direction을 넘김 (부대가 목표와 같은 자리에 있을 때)
        //   2) army_Data의 interval이 0 (스탯 에셋 미설정)
        //
        // 어느 쪽이든 여기서 막아야 손상이 퍼지지 않습니다.
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.0000001f
            || float.IsNaN(direction.x) || float.IsNaN(direction.z)
            || float.IsInfinity(direction.x) || float.IsInfinity(direction.z))
        {
            // 이전에 쓰던 축이 있으면 그것을 유지하고, 없으면 기본 축을 씁니다.
            Vector3 previous = formation_Data != null ? formation_Data.direction : Vector3.zero;
            previous.y = 0.0f;

            direction = previous.sqrMagnitude > 0.0000001f ? previous : Vector3.right;
        }

        float interval = army_Data.GetInterval();

        if (interval <= 0.0f)
        {
            Debug.LogWarning(
                $"{name}: interval이 0 이하입니다({interval}). 진형 간격을 1로 대체합니다. " +
                "Army_Data 또는 UnitStatSO의 interval을 확인하십시오.", this);

            interval = 1.0f;
        }

        Vector3 add_width = direction.normalized * interval;
        Vector3 add_vertical = Quaternion.AngleAxis(90, Vector3.up) * add_width;

        num_width = Clamp_Formation_Width(num_width);

        int unitNum = army_Data.unit_Num;
        if (unitNum <= 0)
        {
            // 전멸한 부대는 계산할 진형이 없습니다.
            return new Formation_Data(num_width, add_width, position, new List<Vector3>());
        }

        // 입력 position이 곧 대열의 한가운데입니다. (위 규약 주석 참조)
        Vector3 formation_Start = position;

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

        // 마커는 유닛이 실제로 서게 될 방향, 즉 전열의 '정면'을 가리켜야 합니다.
        //
        // add_vertical은 열이 뒤로 쌓이는 방향입니다. (AngleAxis(90) * add_width)
        // 정면은 그 반대이며, 이는 Move_Stop / Rotation_To_Formation이 쓰는
        // LookRotation(전열축) * Euler(0,-90,0) 규약과 정확히 같은 벡터입니다.
        //
        // 예전에는 이 부호가 호출 경로마다 달랐습니다. float 오버로드는 -1,
        // int 오버로드는 +1이어서, 드래그 프리뷰(Set_Army_Formation(false))와
        // 단순 클릭 확정(Start_Move_Click)이 마커를 180도 반대로 세웠습니다.
        // 두 경로 모두 같은 진형을 뜻하므로 부호가 갈릴 이유가 없습니다.
        Vector3 markerFacing = -add_vertical;

        // 이미 만들어진 마커만 갱신합니다.
        //
        // 마커는 진형을 그리는 순간(Draw_Formation_UI)에 처음 생성되므로,
        // 그전까지 이 목록은 비어 있고 이 루프는 통째로 건너뜁니다.
        // 여기서 새로 만들면 지연 생성의 의미가 사라집니다.
        Update_Markers(vector3s, unitNum, markerFacing);

        locationMoveTo.Dispose();

        // 저장값도 입력과 같은 '중심'입니다. 그래야 되먹여도 밀려나지 않습니다.
        return new Formation_Data(num_width, add_width, position, vector3s);
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

        // 계산된 월드 좌표를 슬롯에 넘깁니다.
        //
        // 예전에는 여기서 Transform 슬롯에 하나씩 대입했습니다.
        // 이제 Formation_Slots가 슬롯의 유일한 주인이므로 그 루프가 사라졌습니다.
        slots.Store_From_World(locationMoveTo, locationMoveTo.Length);

        locationMoveTo.Dispose();
    }

    // =====================================================================
    // 슬롯 초기화
    // =====================================================================

    /// <summary>
    /// 이미 존재하는 진형 마커의 위치와 방향을 갱신합니다.
    ///
    /// 마커가 아직 만들어지지 않았으면 아무 일도 하지 않습니다.
    /// 생성은 오직 Draw_Formation_UI(진형을 그리기 시작할 때)에서만 일어납니다.
    /// </summary>
    /// <param name="slots">진형 슬롯 월드 좌표입니다.</param>
    /// <param name="count">갱신할 최대 개수입니다.</param>
    /// <param name="facing">마커가 바라볼 방향입니다.</param>
    private void Update_Markers(List<Vector3> slots, int count, Vector3 facing)
    {
        if (uI_Units == null || uI_Units.Count == 0) return;
        if (slots == null) return;

        int n = Mathf.Min(count, Mathf.Min(slots.Count, uI_Units.Count));

        for (int i = 0; i < n; i++)
        {
            if (uI_Units[i] == null) continue;

            uI_Units[i]._Update(slots[i], facing);
        }
    }

    /// <summary>
    /// 생성 시점의 진형 좌표를 슬롯 배열의 초기값으로 삼습니다.
    ///
    /// Set_Formation_Move는 명령을 받아야 호출되므로, 그전까지는
    /// 유닛이 방금 세워진 그 자리가 곧 자기 슬롯입니다.
    /// </summary>
    /// <param name="spawnSlots">Spawn_Units가 계산한 월드 좌표 진형입니다.</param>
    private void Store_Slots_From_Spawn(List<Vector3> spawnSlots)
    {
        slots.Store_From_World(spawnSlots);
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
    /// 요청한 길이로 대열을 만들면 실제로 몇 미터가 되는지 미리 구합니다.
    ///
    /// 왜 필요한가:
    /// 요청 길이는 간격으로 나눠 열 수로 환산된 뒤 <see cref="Clamp_Formation_Width"/>에
    /// 걸려 보정됩니다. 그래서 실제 대열 길이는 요청값과 다를 수 있습니다.
    ///
    /// 여러 부대를 한 줄로 나란히 세우는 쪽(Controller_Formation)은 각 부대의
    /// '중심'을 알아야 하는데, 중심은 가장자리에서 실제 길이의 절반만큼
    /// 들어간 곳입니다. 만들어 보기 전에는 그 길이를 알 수 없으므로
    /// 같은 계산을 미리 한 번 돌려 줍니다.
    ///
    /// 진형을 실제로 만들지는 않습니다. 순수한 조회입니다.
    /// </summary>
    /// <param name="requestedLength">요청하는 대열 길이(미터)입니다.</param>
    /// <returns>보정을 거친 실제 대열 길이입니다.</returns>
    public float Predict_Formation_Length(float requestedLength)
    {
        float interval = army_Data.GetInterval();
        if (interval <= 0.0f) return 0.0f;

        int width = Clamp_Formation_Width(Mathf.RoundToInt(requestedLength / interval));
        return width * interval;
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
