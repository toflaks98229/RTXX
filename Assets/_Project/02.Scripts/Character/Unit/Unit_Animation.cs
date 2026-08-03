
using System.Collections.Generic;

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

    // --- 피격 시각 피드백 ---
    /// <summary>스프라이트의 원래 색입니다. 점멸이 끝나면 이 색으로 돌아갑니다.</summary>
    private Color baseColor = Color.white;
    /// <summary>현재 점멸 강도(0~1)입니다. 시간에 따라 0으로 줄어듭니다.</summary>
    private float flash;
    /// <summary>점멸이 사라지는 데 걸리는 시간입니다. 피격 종류에 따라 다릅니다.</summary>
    private float flashDuration = Constant.time_Hit_Flash;
    /// <summary>점멸 색입니다. 일반 피격은 붉게, 돌격 충돌은 희게 번쩍입니다.</summary>
    private Color flashColor = Color.red;
    /// <summary>반동으로 커지는 비율입니다.</summary>
    private float punchScale;

    // --- 공격 모션 ---
    /// <summary>현재 내지르기 진행도(1 -> 0)입니다. 0이면 원위치입니다.</summary>
    private float lunge;
    /// <summary>내지를 방향입니다. 표적 쪽을 향합니다.</summary>
    private Vector3 lungeDirection;
    /// <summary>스프라이트의 원래 로컬 위치입니다. 내지르기가 끝나면 여기로 돌아옵니다.</summary>
    private Vector3 baseLocalPosition;
    /// <summary>점멸을 적용할 렌더러들입니다. (본체 + 무기 + 방패)</summary>
    private readonly List<SpriteRenderer> flashRenderers = new List<SpriteRenderer>();
    /// <summary>각 렌더러의 원래 색입니다. flashRenderers와 인덱스가 대응합니다.</summary>
    private readonly List<Color> baseColors = new List<Color>();

    // Public methods
    /// <summary>유닛 애니메이션을 초기화합니다.</summary>
    /// <param name="army">소속 부대입니다.</param>
    /// <param name="weapon">무기 스프라이트입니다. 점멸을 함께 적용합니다. (없으면 null)</param>
    /// <param name="shield">방패 스프라이트입니다. 점멸을 함께 적용합니다. (없으면 null)</param>
    public void _Start(Army army, SpriteRenderer weapon = null, SpriteRenderer shield = null)
    {
        enabled = false;

        unit_Animation_Data = new Unit_Animation_Data();

        spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.sprite = army.images_Unit[Random.Range(0, army.images_Unit.Count)];

        size = army.army_Data.GetSize();
        baseColor = spriteRenderer.color;

        // 본체 외에 무기/방패도 함께 물들여야 타격이 한 덩어리로 읽힙니다.
        // 같은 렌더러가 중복으로 들어오지 않도록 걸러냅니다.
        flashRenderers.Clear();
        flashRenderers.Add(spriteRenderer);

        if (weapon != null && weapon != spriteRenderer) flashRenderers.Add(weapon);
        if (shield != null && shield != spriteRenderer) flashRenderers.Add(shield);

        baseColors.Clear();
        for (int i = 0; i < flashRenderers.Count; i++)
        {
            baseColors.Add(flashRenderers[i].color);
        }

        transform.localPosition = new Vector3(0.0f, (size - 1) * 0.5f, 0.0f);

        // 내지르기가 끝나면 돌아올 자리를 기억해 둡니다.
        baseLocalPosition = transform.localPosition;
    }

    /// <summary>
    /// 공격 모션을 시작합니다. 표적 쪽으로 짧게 내질렀다가 돌아옵니다.
    ///
    /// 토탈워의 매치드 컴뱃(두 병사를 짝지어 전용 애니메이션을 재생)을
    /// 스프라이트로 흉내 내는 것은 비용 대비 이득이 없습니다.
    /// 대신 이 짧은 전진만으로도 '누가 누구를 치고 있는지'가 눈에 들어옵니다.
    /// </summary>
    /// <param name="direction">표적을 향한 방향입니다.</param>
    public void Play_Attack_Lunge(Vector3 direction)
    {
        direction.y = 0.0f;
        if (direction.sqrMagnitude < 0.000001f) return;

        lunge = 1.0f;
        lungeDirection = direction.normalized;
    }

    /// <summary>
    /// 일반 피격 점멸을 시작합니다. 피해량이 클수록 강하게 번쩍입니다.
    /// </summary>
    /// <param name="damage">받은 피해량입니다.</param>
    public void Flash_Hit(float damage)
    {
        float intensity = damage / Constant.hit_Flash_Full_Damage;
        if (intensity < 0.25f) intensity = 0.25f;   // 약한 타격도 보이도록 하한
        if (intensity > 1.0f) intensity = 1.0f;

        // 이미 더 강한 점멸이 진행 중이면 덮어쓰지 않습니다.
        if (intensity < flash) return;

        flash = intensity;
        flashDuration = Constant.time_Hit_Flash;
        flashColor = Color.red;
        punchScale = intensity * Constant.hit_Flash_Punch_Scale;
    }

    /// <summary>
    /// 돌격 충돌 점멸을 시작합니다. 일반 피격보다 희고, 길고, 크게 반동합니다.
    /// </summary>
    /// <param name="power">충돌 세기(0~1)입니다.</param>
    public void Flash_Charge_Impact(float power)
    {
        if (power < 0.0f) power = 0.0f;
        if (power > 1.0f) power = 1.0f;

        flash = 1.0f;
        flashDuration = Constant.time_Charge_Flash;
        flashColor = Color.white;
        punchScale = Constant.hit_Flash_Punch_Scale
                     * Constant.charge_Flash_Punch_Rate
                     * Mathf.Max(0.4f, power);
    }

    /// <summary>연출이 없는 상태에서 마지막으로 반영한 좌우 반전입니다.</summary>
    private bool bidleFlip;

    /// <summary>연출이 없는 상태의 값이 이미 계산되어 있는지 여부입니다.</summary>
    private bool bidleValid;

    /// <summary>이번 틱에 계산된 스프라이트 스케일입니다. 일괄 쓰기가 읽어 갑니다.</summary>
    public Vector3 spriteScale { get; private set; }
    /// <summary>이번 틱에 계산된 스프라이트 로컬 위치입니다. (내지르기 포함)</summary>
    public Vector3 spriteLocalPosition { get; private set; }

    /// <summary>
    /// 매 프레임마다 호출되어 애니메이션 값을 계산합니다.
    /// Transform 반영은 하지 않습니다. 계산 결과만 남기면
    /// Controller의 일괄 쓰기가 읽어 갑니다.
    /// </summary>
    public void _Update()
    {
        bool bflip = unit_Animation_Data.bflip;

        // 아무 연출도 진행 중이 아니면 계산을 건너뜁니다.
        //
        // 대규모 전투에서 '이번 틱에 맞았거나 내지르는' 유닛은 소수입니다.
        // 나머지 수천 명은 점멸도 반동도 없어 결과가 지난 틱과 똑같습니다.
        // 그런데도 매 틱 스케일을 다시 만들고 렌더러 색을 훑고 있었습니다.
        //
        // 좌우 반전만 바뀌었을 수 있으므로 그 경우는 스케일만 다시 만듭니다.
        if (flash <= 0.0f && lunge <= 0.0f)
        {
            if (bflip != bidleFlip || !bidleValid)
            {
                float idleScale = size;

                spriteScale = bflip
                    ? new Vector3(idleScale, idleScale, idleScale)
                    : new Vector3(-idleScale, idleScale, idleScale);

                spriteLocalPosition = baseLocalPosition;

                bidleFlip = bflip;
                bidleValid = true;
            }

            return;
        }

        // 연출이 진행 중입니다. 다음에 멈출 때 한 번 다시 계산해야 합니다.
        bidleValid = false;

        _Update_Flash();
        _Update_Lunge();

        // 반동으로 순간적으로 커졌다가 원래 크기로 돌아옵니다.
        float scale = size * (1.0f + punchScale * flash);

        // 좌우 반전은 X 스케일 부호로 표현합니다.
        spriteScale = bflip
            ? new Vector3(scale, scale, scale)
            : new Vector3(-scale, scale, scale);

        // Transform은 여기서 만지지 않습니다.
        // Controller가 틱 마지막에 Write_Sprite_Job으로 전 유닛의
        // 회전/스케일/로컬위치를 한 번에 씁니다.
        // 유닛마다 대입하면 9,600명 기준 7.44 ms가 듭니다.
    }

    /// <summary>
    /// 내지르기를 감쇠시키고 스프라이트 위치에 반영합니다.
    ///
    /// 부모(유닛 본체)는 시뮬레이션이 움직이므로 건드리지 않고,
    /// 자식 스프라이트의 로컬 위치만 흔듭니다. 그래야 물리와 충돌하지 않습니다.
    /// </summary>
    private void _Update_Lunge()
    {
        if (lunge <= 0.0f)
        {
            spriteLocalPosition = baseLocalPosition;
            return;
        }

        lunge -= Constant.deltaTime / Constant.attack_Lunge_Time;
        if (lunge < 0.0f) lunge = 0.0f;

        // 내지른 뒤 되돌아오는 느낌을 주기 위해 sin 곡선을 씁니다.
        // (1에서 0으로 선형 감쇠하면 '뽑아 든 채 미끄러지는' 모양이 됩니다)
        float offset = Mathf.Sin(lunge * Mathf.PI) * Constant.attack_Lunge_Distance;

        // 스프라이트는 카메라를 향해 서 있으므로 월드 방향을 로컬로 바꿔 줍니다.
        //
        // 부모 회전은 시뮬레이션이 정한 유닛 회전과 같으므로,
        // Transform을 읽지 않고 데이터로 역변환합니다.
        Vector3 local = Quaternion.Inverse(unit_Animation_Data.rotation) * lungeDirection;
        local.y = 0.0f;

        spriteLocalPosition = baseLocalPosition + local * offset;
    }

    /// <summary>점멸을 감쇠시키고 스프라이트 색에 반영합니다.</summary>
    private void _Update_Flash()
    {
        if (flash <= 0.0f) return;

        flash -= Constant.deltaTime / flashDuration;

        bool bdone = flash <= 0.0f;
        if (bdone)
        {
            flash = 0.0f;
            punchScale = 0.0f;
        }

        for (int i = 0; i < flashRenderers.Count; i++)
        {
            SpriteRenderer renderer = flashRenderers[i];
            if (renderer == null) continue;

            renderer.color = bdone
                ? baseColors[i]
                : Color.Lerp(baseColors[i], flashColor, flash);
        }
    }
}
