using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public struct Unit_Animation_Data
{
    /// <summary>애니메이션의 위치입니다.</summary>
    public Vector3 position;
    /// <summary>애니메이션의 회전값입니다.</summary>
    public Quaternion rotation;
    /// <summary>카메라의 위치입니다.</summary>
    public Vector3 cam_Position;
    /// <summary>카메라의 회전값입니다.</summary>
    public Quaternion cam_Rotation;
    /// <summary>스프라이트가 좌우 반전되었는지 여부입니다.</summary>
    public bool bflip;

    /// <summary>애니메이션 데이터를 업데이트합니다.</summary>
    public void _Update()
    {
        Vector3 cam_eulerAngles = cam_Rotation.eulerAngles;

        float dot = Vector3.Dot(Quaternion.Euler(new Vector3(0.0f, cam_eulerAngles.y, 0.0f)) * Vector3.forward, rotation * Vector3.right);

        if (bflip)
        {
            if (dot > 0.05f)
            {
                bflip = false;
            }
        }
        else
        {
            if (dot < -0.05f)
            {
                bflip = true;
            }
        }

        rotation = Quaternion.Euler(cam_eulerAngles);
    }
}

public class Unit_Animation : MonoBehaviour
{
    // Public member variables
    /// <summary>애니메이션 데이터 구조체입니다.</summary>
    public Unit_Animation_Data unit_Animation_Data;

    // Private member variables
    /// <summary>유닛의 스프라이트 렌더러입니다.</summary>
    private SpriteRenderer spriteRenderer;
    /// <summary>유닛의 크기입니다.</summary>
    private float size;

    // Public methods
    /// <summary>유닛 애니메이션을 초기화합니다.</summary>
    public void _Start(Army army)
    {
        enabled = false;

        unit_Animation_Data = new Unit_Animation_Data();

        spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.sprite = army.images_Unit[Random.Range(0, army.images_Unit.Count)];

        size = army.army_Data.GetSize();

        transform.localPosition = new Vector3(0.0f, (size - 1) * 0.5f, 0.0f);
    }

    /// <summary>매 프레임마다 호출되어 애니메이션을 업데이트합니다.</summary>
    public void _Update()
    {
        transform.rotation = unit_Animation_Data.rotation;

        if (unit_Animation_Data.bflip)
        {
            transform.localScale = new Vector3(size, size, size);
        }
        else
        {
            transform.localScale = new Vector3(-size, size, size);
        }
    }
}
