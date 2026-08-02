using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 원거리 발사를 화살 궤적으로 그려 주는 표현 계층입니다.
///
/// 설계 의도:
/// 피해 판정은 이미 Unit_Fight_Job이 끝냈습니다. 여기서 나는 화살은
/// '이미 결정된 결과'를 눈에 보이게 하는 장식일 뿐이며,
/// 시뮬레이션에 어떤 영향도 주지 않습니다.
/// 그래서 궤적이 늦게 도착해도 게임 규칙은 어긋나지 않습니다.
///
/// 이렇게 나눈 이유:
/// 투사체를 실제 물리 오브젝트로 날리면 수백 발이 동시에 날아다니며
/// 물리 스텝을 잡아먹고, 명중 판정이 물리 충돌에 종속되어
/// 결정론이 깨집니다. 판정과 표현을 분리하는 편이 훨씬 안전합니다.
///
/// 오브젝트를 만들지 않고 LineRenderer 풀만 돌려 쓰므로
/// 사격이 아무리 잦아도 GC가 발생하지 않습니다.
/// </summary>
public class Projectile_Renderer : MonoBehaviour
{
    /// <summary>날아가는 화살 한 발의 상태입니다.</summary>
    private struct Shot
    {
        public Vector3 from;
        public Vector3 to;
        public float elapsed;
        public float duration;
        public LineRenderer line;
    }

    [Header("표현")]
    [Tooltip("화살이 목표에 닿기까지의 비행 시간(초)입니다.")]
    public float flightTime = 0.35f;

    [Tooltip("화살이 그리는 포물선의 최고 높이입니다.")]
    public float arcHeight = 2.5f;

    [Tooltip("궤적 선의 굵기입니다.")]
    public float lineWidth = 0.06f;

    [Tooltip("궤적 색입니다.")]
    public Color lineColor = new Color(0.95f, 0.92f, 0.75f, 0.9f);

    [Tooltip("동시에 표시할 수 있는 최대 화살 수입니다. 넘으면 새 발사를 무시합니다.")]
    public int maxShots = 256;

    private readonly List<Shot> shots = new List<Shot>();
    private readonly Stack<LineRenderer> pool = new Stack<LineRenderer>();

    private Material lineMaterial;

    private void Awake()
    {
        // URP/빌트인 어디서든 보이는 가장 단순한 언릿 머티리얼입니다.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lineMaterial = new Material(shader);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    /// <summary>
    /// 화살 한 발을 띄웁니다. 발사 지점과 목표 지점만 주면 됩니다.
    /// </summary>
    public void Fire(Vector3 from, Vector3 to)
    {
        if (shots.Count >= maxShots) return;

        LineRenderer line = Rent_Line();
        if (line == null) return;

        Shot shot = new Shot
        {
            from = from,
            to = to,
            elapsed = 0.0f,
            duration = Mathf.Max(0.05f, flightTime),
            line = line
        };

        shots.Add(shot);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 뒤에서부터 순회해야 제거 시 인덱스가 밀리지 않습니다.
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            shot.elapsed += deltaTime;

            float t = shot.elapsed / shot.duration;

            if (t >= 1.0f)
            {
                Return_Line(shot.line);
                shots.RemoveAt(i);
                continue;
            }

            Draw_Shot(shot, t);
            shots[i] = shot;
        }
    }

    /// <summary>화살의 현재 구간을 짧은 선분으로 그립니다.</summary>
    private void Draw_Shot(Shot shot, float t)
    {
        // 꼬리는 조금 뒤처지게 해서 '날아가는 방향'이 보이도록 합니다.
        float tailT = Mathf.Max(0.0f, t - 0.12f);

        Vector3 head = Get_Arc_Point(shot.from, shot.to, t);
        Vector3 tail = Get_Arc_Point(shot.from, shot.to, tailT);

        shot.line.SetPosition(0, tail);
        shot.line.SetPosition(1, head);
    }

    /// <summary>포물선 위의 한 점을 구합니다. 곡사 궤적을 흉내 냅니다.</summary>
    private Vector3 Get_Arc_Point(Vector3 from, Vector3 to, float t)
    {
        Vector3 point = Vector3.Lerp(from, to, t);

        // 4t(1-t)는 t=0.5에서 1이 되는 가장 단순한 포물선입니다.
        point.y += arcHeight * 4.0f * t * (1.0f - t);

        return point;
    }

    /// <summary>풀에서 선 하나를 꺼내거나 없으면 새로 만듭니다.</summary>
    private LineRenderer Rent_Line()
    {
        LineRenderer line;

        if (pool.Count > 0)
        {
            line = pool.Pop();
        }
        else
        {
            GameObject go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);

            line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.material = lineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.gameObject.SetActive(true);

        return line;
    }

    /// <summary>다 쓴 선을 풀로 돌려보냅니다. 파괴하지 않습니다.</summary>
    private void Return_Line(LineRenderer line)
    {
        if (line == null) return;

        line.gameObject.SetActive(false);
        pool.Push(line);
    }
}
