
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지면에 그려지는 마커 스프라이트입니다. 두 가지 용도로 쓰입니다.
///
///   1) 유닛 선택 링  : 유닛 프리팹의 자식으로 붙어 부모를 따라다닙니다.
///   2) 진형 슬롯 마커 : Army가 별도로 생성하며 월드 좌표로 직접 배치합니다.
///
/// 두 용도가 같은 컴포넌트를 쓰므로, 높이 규약도 한 곳(_Start)에서 정합니다.
/// 마커는 평소 꺼져 있다가 선택하거나 진형을 그릴 때만 켜집니다.
/// </summary>
public class UI_Unit : MonoBehaviour
{
    // 비공개 멤버 변수
    /// <summary>
    /// 유닛의 UI를 렌더링하는 데 사용되는 스프라이트 렌더러 컴포넌트입니다.
    /// </summary>
    SpriteRenderer spriteRenderer;

    // Unity 이벤트 함수
    /// <summary>
    /// MonoBehaviour가 활성화될 때 한 번 호출됩니다. 여기서는 사용하지 않습니다.
    /// </summary>
    public void Start()
    {
        // Start() 함수는 기본적으로 제공되므로 비워둡니다.
    }

    /// <summary>
    /// 이 마커를 지면 위로 띄우는 높이입니다.
    ///
    /// 왜 필요한가:
    /// 유닛 프리팹의 Unit_UI 자식은 로컬 y가 -0.5입니다. 즉 유닛 발밑보다
    /// 0.5m 아래에 있습니다. 평지에서는 지면이 y=0이라 눈에 띄지 않았지만,
    /// 지형이 있는 맵에서는 마커가 통째로 땅속에 묻혀 보이지 않습니다.
    ///
    /// unit_Data.position은 '유닛의 발' 위치이므로, 마커는 그보다 조금
    /// 위에 있어야 지면 위에 그려집니다.
    /// </summary>
    private const float groundOffset = 0.05f;

    /// <summary>카메라 위치를 읽은 프레임입니다. 프레임당 한 번만 읽습니다.</summary>
    private static int liftFrame = -1;

    /// <summary>이번 프레임의 카메라 위치입니다.</summary>
    private static Vector3 liftCameraPosition;

    /// <summary>
    /// 마커를 지면 위로 띄울 높이를 구합니다. 카메라에서 멀수록 커집니다.
    ///
    /// 카메라 위치를 프레임당 한 번만 읽는 이유:
    /// 이 함수는 마커마다 불립니다. 난전에서는 9,600개가 될 수 있으므로
    /// 마커마다 Camera를 조회하면 그 자체가 비용이 됩니다. 한 프레임
    /// 안에서 카메라는 움직이지 않으므로 한 번만 읽어 나눠 씁니다.
    ///
    /// 깊이 테스트를 끄는 방법(ZTest Always)을 쓰지 않는 이유:
    /// 그러면 언덕 뒤에 있는 마커까지 비쳐 보여 지형을 읽을 수 없습니다.
    /// 가려질 것은 가려지되, 지면에 붙은 것만 살아남아야 합니다.
    ///
    /// 카메라가 직교 투영이라 깊이 정밀도는 거리와 무관합니다.
    /// 여기서 거리를 쓰는 것은 **지형 LOD 오차**가 거리에 비례하기
    /// 때문이며, 깊이 버퍼 문제가 아닙니다.
    /// </summary>
    /// <param name="position">마커가 놓일 지면 좌표입니다.</param>
    /// <returns>더할 높이(m)입니다.</returns>
    private static float Ground_Lift(Vector3 position)
    {
        if (liftFrame != Time.frameCount)
        {
            liftFrame = Time.frameCount;

            Camera camera = Main_Camera.Get();

            // 카메라가 없으면(배치모드 등) 거리 보정을 걸지 않습니다.
            liftCameraPosition = camera != null ? camera.transform.position : position;
        }

        float distance = Vector3.Distance(liftCameraPosition, position);

        return Constant.marker_Ground_Lift + distance * Constant.marker_Ground_Lift_Rate;
    }

    /// <summary>마커를 초기화하고 부대 스탯에 맞춰 크기와 높이를 맞춥니다.</summary>
    /// <param name="army_Data">크기를 읽어 올 부대 데이터입니다.</param>
    public void _Start(Army_Data army_Data)
    {
        // 이 스크립트를 비활성화하여 Start()와 Update()가 자동으로 호출되지 않도록 합니다.
        // 이 스크립트의 로직은 수동으로 호출되는 _Update(Vector3, Vector3)에 의해 제어됩니다.
        enabled = false;

        // SpriteRenderer 컴포넌트를 가져오고 초기에는 비활성화합니다.
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;

        // 유닛의 자식으로 붙어 있는 경우(선택 링) 로컬 높이를 바로잡습니다.
        //
        // 프리팹의 Unit_UI는 로컬 y가 -0.5, 즉 발밑보다 아래에 있습니다.
        // 평지에서는 지면이 y=0이라 가려지지 않았지만, 지형이 있으면
        // 마커가 땅속에 묻혀 선택해도 보이지 않습니다.
        //
        // 프리팹을 고치지 않고 여기서 바로잡는 이유:
        // 이 컴포넌트는 부대 진형 마커로도 쓰이며(Army가 별도 생성),
        // 그쪽은 부모가 없어 월드 좌표로 직접 배치됩니다. 한 곳에서
        // 규칙을 정해 두면 두 용도가 같은 높이 규약을 따릅니다.
        if (transform.parent != null)
        {
            Vector3 local = transform.localPosition;

            if (local.y < groundOffset)
            {
                local.y = groundOffset;
                transform.localPosition = local;
            }
        }

        // 부대 데이터에 따라 유닛 UI의 크기를 설정합니다.
        float size = army_Data.GetSize();
        transform.localScale = new Vector3(size, size, size);
    }

    /// <summary>
    /// 유닛 UI의 위치와 방향을 업데이트합니다.
    /// </summary>
    /// <param name="position">유닛의 현재 위치입니다.</param>
    /// <param name="direction">유닛의 현재 전방 방향 벡터입니다.</param>
    public void _Update(Vector3 position, Vector3 direction)
    {
        // 들어온 값이 정상인지 먼저 확인합니다.
        //
        // 왜 필요한가:
        // Quaternion.LookRotation은 영벡터나 NaN을 받으면 회전을 만들지 못하고
        // 손상된 값을 그대로 Transform에 씁니다. 그러면 Unity 렌더러가
        //   Invalid localAABB. Object transform is corrupt.
        //   Assertion failed on expression: 'IsFinite(distanceForSort)'
        // 를 매 프레임 쏟아내고, 그 로그 자체가 프레임을 잡아먹습니다.
        //
        // 영벡터가 들어오는 실제 경로:
        // 진형 방향은 add_width(= direction.normalized * interval)에서 나옵니다.
        // interval이 0이거나 원본 direction이 영벡터면 그 곱이 영벡터가 되고,
        // 그 값이 Formation_Data.direction에 저장되어 여기까지 전파됩니다.
        //
        // 마커는 표시용이므로, 방향을 못 정하면 위치만 갱신하고 회전은
        // 이전 값을 유지하는 편이 안전합니다. (손상된 회전을 쓰는 것보다 낫습니다)
        if (!Is_Finite(position)) return;

        // 카메라에서 멀수록 더 높이 띄웁니다. (지형 LOD 대응)
        //
        // 마커 높이는 실제 높이맵에 레이캐스트해서 정하는데, 유니티
        // 터레인은 멀어질수록 메시를 단순화합니다. 그 단순화된 면이
        // 원래 높이맵보다 위로 솟으면 마커가 지형에 먹혀 잘립니다.
        transform.position = position + new Vector3(0.0f, Ground_Lift(position), 0.0f);

        direction.y = 0.0f;

        if (!Is_Finite(direction) || direction.sqrMagnitude < 0.0000001f) return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);

        // SpriteRenderer가 정면을 바라보도록 X축으로 90도 회전합니다.
        transform.Rotate(new Vector3(90, 0, 0));
    }

    /// <summary>벡터의 모든 성분이 유한한 수인지 확인합니다.</summary>
    private static bool Is_Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    /// <summary>
    /// 유닛 UI를 보이게 합니다.
    /// </summary>
    public void Visible()
    {
        spriteRenderer.enabled = true;
    }

    /// <summary>
    /// 유닛 UI를 숨깁니다.
    /// </summary>
    public void Invisible()
    {
        spriteRenderer.enabled = false;
    }
}
