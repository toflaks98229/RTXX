
using System.Collections.Generic;
using UnityEngine;

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

    // 공개 메서드
    /// <summary>
    /// UI 유닛을 초기화합니다.
    /// </summary>
    /// <param name="army_Data">초기화에 사용될 부대 데이터입니다.</param>
    public void _Start(Army_Data army_Data)
    {
        // 이 스크립트를 비활성화하여 Start()와 Update()가 자동으로 호출되지 않도록 합니다.
        // 이 스크립트의 로직은 수동으로 호출되는 _Update(Vector3, Vector3)에 의해 제어됩니다.
        enabled = false;

        // SpriteRenderer 컴포넌트를 가져오고 초기에는 비활성화합니다.
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;

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
        // 유닛의 위치에 맞춰 UI 위치를 업데이트하고, 약간 위로 띄웁니다.
        transform.position = position + new Vector3(0, 0.1f, 0);
        // 유닛의 방향을 향하도록 UI의 회전을 설정합니다.
        transform.rotation = Quaternion.LookRotation(direction);
        // SpriteRenderer가 정면을 바라보도록 X축으로 90도 회전합니다.
        transform.Rotate(new Vector3(90, 0, 0));
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
