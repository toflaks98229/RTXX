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
        /// <summary>발사 지점(월드 좌표)입니다.</summary>
        public Vector3 from;

        /// <summary>도착 지점(월드 좌표)입니다.</summary>
        public Vector3 to;

        /// <summary>발사 후 지난 시간(초)입니다.</summary>
        public float elapsed;

        /// <summary>이 화살이 날아가는 데 걸리는 총 시간(초)입니다.</summary>
        public float duration;

        /// <summary>이 화살을 그리는 선입니다. 다 날면 풀로 돌아갑니다.</summary>
        public LineRenderer line;
    }

    // 공개 멤버 변수
    /// <summary>화살이 목표에 닿기까지의 비행 시간(초)입니다.</summary>
    [Header("표현")]
    [Tooltip("화살이 목표에 닿기까지의 비행 시간(초)입니다.")]
    public float flightTime = 0.35f;

    /// <summary>화살이 그리는 포물선의 최고 높이입니다. 곡사 궤적의 과장 정도를 정합니다.</summary>
    [Tooltip("화살이 그리는 포물선의 최고 높이입니다.")]
    public float arcHeight = 2.5f;

    /// <summary>궤적 선의 굵기입니다.</summary>
    [Tooltip("궤적 선의 굵기입니다.")]
    public float lineWidth = 0.06f;

    /// <summary>궤적 색입니다.</summary>
    [Tooltip("궤적 색입니다.")]
    public Color lineColor = new Color(0.95f, 0.92f, 0.75f, 0.9f);

    /// <summary>
    /// 동시에 표시할 수 있는 최대 화살 수입니다.
    /// 일제사격으로 수천 발이 몰려도 화면과 비용을 이 값으로 묶어 둡니다.
    /// </summary>
    [Tooltip("동시에 표시할 수 있는 최대 화살 수입니다. 넘으면 새 발사를 무시합니다.")]
    public int maxShots = 256;

    // 비공개 멤버 변수
    /// <summary>현재 날아가는 중인 화살들입니다.</summary>
    private readonly List<Shot> shots = new List<Shot>();

    /// <summary>
    /// 다 쓴 선을 담아 두는 풀입니다.
    ///
    /// 파괴하지 않고 재사용하는 이유: 사격은 초당 수백 번 일어날 수 있고,
    /// 그때마다 GameObject를 만들고 버리면 GC가 프레임을 끊습니다.
    /// </summary>
    private readonly Stack<LineRenderer> pool = new Stack<LineRenderer>();

    /// <summary>궤적 선이 공유하는 머티리얼입니다. Awake에서 한 번만 만듭니다.</summary>
    private Material lineMaterial;

    // Unity 이벤트 함수
    /// <summary>궤적 선이 쓸 머티리얼을 준비합니다.</summary>
    private void Awake()
    {
        // URP/빌트인 어디서든 보이는 가장 단순한 언릿 머티리얼입니다.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lineMaterial = new Material(shader);
    }

    /// <summary>
    /// 동적으로 만든 머티리얼을 반납합니다.
    /// new Material로 만든 것은 명시적으로 지우지 않으면 누수됩니다.
    /// </summary>
    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    // 공개 메서드
    /// <summary>
    /// 화살 한 발을 띄웁니다. 발사 지점과 목표 지점만 주면 됩니다.
    ///
    /// 표시 한도(maxShots)를 넘겼거나 좌표가 유한하지 않으면 조용히 무시합니다.
    /// 이 궤적은 순수 표현이므로, 한 발 빠지는 것보다 오류를 내는 편이 나쁩니다.
    /// </summary>
    /// <param name="from">발사 지점(월드 좌표)입니다.</param>
    /// <param name="to">도착 지점(월드 좌표)입니다.</param>
    public void Fire(Vector3 from, Vector3 to)
    {
        if (shots.Count >= maxShots) return;

        // 좌표가 유한한지 반드시 확인합니다.
        //
        // 왜 필요한가:
        // 표적 좌표는 Unit_target_Data.RemoveTarget()이 Vector3.positiveInfinity로
        // 초기화합니다. 표적을 잃은 직후에 발사가 성립하면 그 무한대가 여기까지
        // 들어오고, Get_Arc_Point의 Vector3.Lerp(from, ∞, t)가 NaN을 만듭니다.
        //
        // NaN이 LineRenderer.SetPosition에 들어가면 Unity가 매 프레임
        //   Invalid localAABB. Object transform is corrupt.
        //   Assertion failed on expression: 'IsFinite(distanceForSort)'
        //   Assertion failed on expression: 'IsFinite(distanceAlongView)'
        // 를 쏟아냅니다. distanceForSort는 반투명 렌더러의 정렬 거리이며,
        // LineRenderer가 바로 그 대상입니다.
        //
        // 이 오류는 렌더 스레드에서 나므로 스택이 게임 코드를 가리키지 않아
        // 원인 추적이 매우 어렵습니다. 발사 지점에서 막는 것이 확실합니다.
        if (!Is_Finite(from) || !Is_Finite(to)) return;

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

    // Unity 이벤트 함수 (계속)
    /// <summary>
    /// 날아가는 화살들의 진행도를 갱신하고, 다 날아간 것은 풀로 돌려보냅니다.
    ///
    /// FixedUpdate가 아닌 Update인 이유: 이 궤적은 시뮬레이션이 아니라
    /// 표현이므로 화면 프레임에 맞춰 부드럽게 움직이는 편이 자연스럽습니다.
    /// </summary>
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

    // 비공개 메서드
    /// <summary>화살의 현재 구간을 짧은 선분으로 그립니다.</summary>
    /// <param name="shot">그릴 화살입니다.</param>
    /// <param name="t">비행 진행도(0~1)입니다.</param>
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
    /// <param name="from">발사 지점입니다.</param>
    /// <param name="to">도착 지점입니다.</param>
    /// <param name="t">비행 진행도(0~1)입니다.</param>
    /// <returns>그 시점의 화살 위치(월드 좌표)입니다.</returns>
    private Vector3 Get_Arc_Point(Vector3 from, Vector3 to, float t)
    {
        Vector3 point = Vector3.Lerp(from, to, t);

        // 4t(1-t)는 t=0.5에서 1이 되는 가장 단순한 포물선입니다.
        point.y += arcHeight * 4.0f * t * (1.0f - t);

        return point;
    }

    /// <summary>풀에서 선 하나를 꺼내거나, 없으면 새로 만듭니다.</summary>
    /// <returns>사용 준비가 끝난 선입니다.</returns>
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

    /// <summary>
    /// 좌표의 모든 성분이 유한한 수인지 확인합니다.
    ///
    /// 무한대(표적 없음의 기본값)와 NaN(무한대끼리의 연산 결과)을 모두 걸러냅니다.
    /// </summary>
    private static bool Is_Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    /// <summary>다 쓴 선을 풀로 돌려보냅니다. 파괴하지 않습니다.</summary>
    /// <param name="line">반납할 선입니다.</param>
    private void Return_Line(LineRenderer line)
    {
        if (line == null) return;

        line.gameObject.SetActive(false);
        pool.Push(line);
    }
}
