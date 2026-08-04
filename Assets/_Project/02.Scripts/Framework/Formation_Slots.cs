using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// 진형 슬롯 좌표의 유일한 소유자입니다.
///
/// 왜 Army에서 떼어냈는가:
/// 슬롯은 '부대 기준점 기준 지역 좌표 배열'과 '틱당 한 번 계산되는 월드
/// 캐시' 두 벌로 존재하고, 그 둘의 정합성은 Invalidate 호출 시점에 달려
/// 있습니다. 이 계약이 Army 5,500줄 안에 흩어져 있으면 누가 언제
/// 무효화해야 하는지 읽어 낼 수 없습니다.
///
/// 실제로 배치 단계에서 캐시를 무효화하지 않아 "드래그로 옮긴 뒤 태세를
/// 바꾸면 부대가 원래 자리로 돌아가는" 버그가 났습니다.
/// (Army.Snap_Units_To_Slots의 Invalidate_Center_Position 주석 참조)
///
/// 이 클래스로 모으면 슬롯을 만지는 방법이 네 개뿐이 됩니다.
///   Store_From_World / Store_From_Spawn : 슬롯을 새로 정한다
///   Invalidate                          : 기준점이 움직였다
///   Get_World                           : 읽는다
/// 그 밖의 경로가 없으므로 캐시가 조용히 낡을 수 없습니다.
///
/// ---------------------------------------------------------------------
/// 왜 지역 좌표로 저장하는가
/// ---------------------------------------------------------------------
/// 슬롯은 원래 부대 기준점의 자식 Transform이었습니다. 기준점이 움직이면
/// Unity가 자식을 자동으로 따라 옮겨 주므로 코드가 매 틱 갱신할 필요가
/// 없었지만, 대신 '읽을 때마다' 네이티브 왕복을 냈습니다.
///
///   실측(9,600명): 슬롯 읽기 2.955 ms/틱 -> 배열 읽기 0.029 ms/틱
///
/// 갱신은 명령 시에만 일어나고 소비는 매 틱이므로, 읽기가 비싼 구조는
/// 정확히 반대로 최적화되어 있었습니다.
///
/// 월드 좌표로 저장하지 않는 이유도 같습니다. 그러면 기준점이 움직일
/// 때마다 전량을 다시 써야 하고, 그건 지금 없애려는 바로 그 비용입니다.
/// 지역 좌표로 두고 '처음 읽을 때' 한 번만 변환합니다.
/// </summary>
public class Formation_Slots
{
    /// <summary>슬롯의 부대 기준점 기준 지역 좌표입니다.</summary>
    private Vector3[] localPositions;

    /// <summary>실제로 채워진 슬롯 수입니다. 배열은 이보다 클 수 있습니다.</summary>
    private int count;

    /// <summary>슬롯의 현재 월드 좌표 캐시입니다. 틱당 한 번만 계산합니다.</summary>
    private Vector3[] worldPositions;

    /// <summary>월드 좌표 캐시가 지금 기준으로 유효한지 여부입니다.</summary>
    private bool bworldValid;

    /// <summary>
    /// 슬롯이 매달린 부대 기준점입니다.
    ///
    /// 생성자에서 한 번만 받습니다. 읽을 때마다 넘기게 하면 호출부가
    /// 엉뚱한 기준점을 넘길 수 있고, 그러면 슬롯이 조용히 다른 자리에
    /// 만들어집니다.
    /// </summary>
    private readonly Transform pivot;

    /// <summary>채워진 진형 슬롯 수입니다.</summary>
    public int Count => count;

    /// <summary>슬롯 묶음을 만듭니다.</summary>
    /// <param name="pivot">슬롯의 기준이 되는 부대 기준점입니다.</param>
    public Formation_Slots(Transform pivot)
    {
        this.pivot = pivot;
    }

    // =====================================================================
    // 슬롯 인덱스 규약 — Formation_Job이 펼치는 순서의 역변환
    // =====================================================================
    //
    // Formation_Job.Execute가 인덱스를 이렇게 해석합니다.
    //
    //     vector3 += add_vertical * Mathf.Floor(index / num_width);  // 열
    //     float num = index % num_width;                             // 오
    //
    // 즉 슬롯 인덱스에 '몇 번째 오(file), 몇 번째 열(rank)'이 이미
    // 인코딩되어 있습니다. 아래 두 함수가 그 역변환입니다.
    //
    // 왜 명시적으로 두는가:
    // 이 관계는 지금까지 Formation_Job 안에만 암묵적으로 존재했습니다.
    // 선두 지정처럼 '앞줄이 누구인가'를 알아야 하는 기능은 그 관계를
    // 다시 유도해야 하는데, 두 곳에 적으면 한쪽만 바뀌어도 조용히
    // 어긋납니다. (그때 대열이 이상해질 뿐 예외는 나지 않습니다)
    //
    // 규약이 실제 기하와 맞는지는 Mass_Battle_Probe가 매 검증마다
    // 슬롯 좌표로 대조합니다. 추론이 아니라 측정으로 지킵니다.

    /// <summary>슬롯이 속한 오(세로줄) 번호입니다.</summary>
    /// <param name="slotIndex">슬롯 인덱스입니다.</param>
    /// <param name="width">진형의 가로 인원 수입니다.</param>
    /// <returns>0 이상 width 미만의 오 번호이며, width가 0 이하면 -1입니다.</returns>
    public static int File_Of(int slotIndex, int width)
    {
        if (width <= 0 || slotIndex < 0) return -1;
        return slotIndex % width;
    }

    /// <summary>슬롯이 속한 열(앞에서 몇 번째 줄인가) 번호입니다.</summary>
    /// <param name="slotIndex">슬롯 인덱스입니다.</param>
    /// <param name="width">진형의 가로 인원 수입니다.</param>
    /// <returns>0이면 맨 앞줄입니다. width가 0 이하면 -1입니다.</returns>
    public static int Rank_Of(int slotIndex, int width)
    {
        if (width <= 0 || slotIndex < 0) return -1;
        return slotIndex / width;
    }

    /// <summary>
    /// 오와 열로부터 슬롯 인덱스를 구합니다. 위 두 함수의 역입니다.
    /// </summary>
    /// <param name="file">오 번호입니다.</param>
    /// <param name="rank">열 번호입니다. 0이 맨 앞줄입니다.</param>
    /// <param name="width">진형의 가로 인원 수입니다.</param>
    /// <returns>슬롯 인덱스이며, 입력이 유효하지 않으면 -1입니다.</returns>
    public static int Index_Of(int file, int rank, int width)
    {
        if (width <= 0) return -1;
        if (file < 0 || file >= width) return -1;
        if (rank < 0) return -1;

        return rank * width + file;
    }

    /// <summary>
    /// 월드 좌표 캐시를 무효화합니다.
    ///
    /// 기준점이 움직였을 수 있으므로 매 틱 시작에 호출합니다.
    /// 실제 계산은 '처음 읽을 때'로 미룹니다. 이동 중이 아닌 부대는
    /// 아무도 슬롯을 읽지 않으므로 계산 자체가 일어나지 않습니다.
    /// </summary>
    public void Invalidate()
    {
        bworldValid = false;
    }

    /// <summary>
    /// 슬롯 하나의 현재 월드 좌표입니다.
    /// 범위를 벗어나면 부대 기준점 위치를 돌려줍니다.
    /// </summary>
    /// <param name="index">슬롯 번호입니다.</param>
    /// <returns>슬롯의 월드 좌표입니다.</returns>
    public Vector3 Get_World(int index)
    {
        if (index < 0 || index >= count) return pivot.position;

        if (!bworldValid) Rebuild_World();

        return worldPositions[index];
    }

    /// <summary>
    /// 계산된 월드 좌표들을 지역 좌표로 바꿔 저장합니다.
    ///
    /// 역변환이므로 회전의 역을 곱합니다.
    ///     local = inverse(pivot.rotation) * (world - pivot.position)
    /// </summary>
    /// <param name="world">저장할 월드 좌표들입니다.</param>
    /// <param name="n">그중 앞에서부터 사용할 개수입니다.</param>
    public void Store_From_World(NativeArray<Vector3> world, int n)
    {
        if (n <= 0) { count = 0; return; }

        Ensure_Capacity(n);

        Vector3 pivotPosition = pivot.position;
        Quaternion inverse = Quaternion.Inverse(pivot.rotation);

        for (int i = 0; i < n; i++)
        {
            localPositions[i] = inverse * (world[i] - pivotPosition);
        }

        count = n;

        // 저장 직후에는 캐시가 낡았습니다. 다음 읽기에서 다시 만듭니다.
        bworldValid = false;
    }

    /// <summary>
    /// 생성 시점의 진형 좌표를 슬롯의 초기값으로 삼습니다.
    ///
    /// Set_Formation_Move는 명령을 받아야 호출되므로, 그전까지는
    /// 유닛이 방금 세워진 그 자리가 곧 자기 슬롯입니다.
    /// </summary>
    /// <param name="world">Spawn_Units가 계산한 월드 좌표 진형입니다.</param>
    public void Store_From_World(List<Vector3> world)
    {
        if (world == null || world.Count == 0) { count = 0; return; }

        int n = world.Count;
        Ensure_Capacity(n);

        Vector3 pivotPosition = pivot.position;
        Quaternion inverse = Quaternion.Inverse(pivot.rotation);

        for (int i = 0; i < n; i++)
        {
            localPositions[i] = inverse * (world[i] - pivotPosition);
        }

        count = n;
        bworldValid = false;
    }

    /// <summary>슬롯을 모두 비웁니다.</summary>
    public void Clear()
    {
        count = 0;
        bworldValid = false;
    }

    /// <summary>
    /// 지역 좌표를 월드 좌표로 변환해 캐시를 채웁니다.
    ///
    /// 변환식은 Transform의 SetParent(pivot, false) + position 대입과
    /// 정확히 같습니다. (스케일은 1이므로 무시)
    ///     world = pivot.position + pivot.rotation * local
    /// </summary>
    private void Rebuild_World()
    {
        if (localPositions == null || count == 0)
        {
            bworldValid = true;
            return;
        }

        if (worldPositions == null || worldPositions.Length < count)
        {
            worldPositions = new Vector3[Mathf.Max(count, 16)];
        }

        // 프로퍼티 접근은 네이티브 왕복입니다. 루프 밖에서 한 번만 읽습니다.
        Vector3 pivotPosition = pivot.position;
        Quaternion pivotRotation = pivot.rotation;

        for (int i = 0; i < count; i++)
        {
            worldPositions[i] = pivotPosition + pivotRotation * localPositions[i];
        }

        bworldValid = true;
    }

    /// <summary>지역 좌표 배열이 요청한 크기 이상이 되도록 보장합니다.</summary>
    /// <param name="required">필요한 슬롯 수입니다.</param>
    private void Ensure_Capacity(int required)
    {
        if (localPositions != null && localPositions.Length >= required) return;

        localPositions = new Vector3[Mathf.Max(required, 16)];
    }
}
