using System.Linq;

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 헝가리안 알고리즘(Kuhn-Munkres)으로 최적 이분 매칭을 찾는 클래스입니다.
///
/// 이 프로젝트에서의 쓰임:
/// 병사 N명을 진형 슬롯 N개에 배정할 때, '총 이동 거리가 최소'가 되는
/// 짝을 찾습니다. 가까운 순서대로 탐욕적으로 배정하면 병사들의 경로가
/// 서로 교차해 대열이 뒤엉키는데, 최적 매칭은 그것을 막아 줍니다.
///
/// 동작 원리 (요약):
/// 각 행(작업자)과 열(작업)에 '레이블'이라는 값을 붙여 두고,
/// 비용이 정확히 lx[x] + ly[y]인 간선만 골라 부분 그래프를 만듭니다.
/// 그 안에서 최대 매칭을 찾다가 막히면 레이블을 조정해 간선을 늘리고,
/// 모든 행이 짝을 찾을 때까지 이 과정을 반복합니다.
/// 레이블 조정 폭(delta)은 slack이 알려 줍니다.
///
/// 비용:
/// O(n^3)입니다. 인원이 많으면 메인 스레드가 눈에 띄게 멈추므로,
/// 호출부(Formation_Matcher)가 64명을 넘으면 분대 단위로 잘라
/// 이 클래스를 작은 크기로 여러 번 부릅니다.
/// </summary>
public class Hungarian
{
    // 비공개 멤버 변수
    /// <summary>
    /// 비용 매트릭스입니다. 각 요소는 한 작업자와 한 작업 간의 비용을 나타냅니다.
    /// </summary>
    private readonly float[,] _costMatrix;
    /// <summary>
    /// 무한대 값을 나타냅니다.
    /// </summary>
    private float _inf;
    /// <summary>
    /// 매트릭스의 크기(요소의 수)입니다.
    /// </summary>
    private int _n;
    /// <summary>
    /// 작업자 노드에 대한 레이블입니다.
    /// </summary>
    private float[] _lx;
    /// <summary>
    /// 작업 노드에 대한 레이블입니다.
    /// </summary>
    private float[] _ly;
    /// <summary>
    /// 작업자 노드가 매칭 트리에 포함되었는지 여부를 나타냅니다.
    /// </summary>
    private bool[] _s;
    /// <summary>
    /// 작업 노드가 매칭 트리에 포함되었는지 여부를 나타냅니다.
    /// </summary>
    private bool[] _t;
    /// <summary>
    /// x 노드(작업자)와 매칭된 y 노드(작업)의 인덱스입니다.
    /// </summary>
    private int[] _matchX;
    /// <summary>
    /// y 노드(작업)와 매칭된 x 노드(작업자)의 인덱스입니다.
    /// </summary>
    private int[] _matchY;
    /// <summary>
    /// 현재까지 찾은 최대 매칭의 수입니다.
    /// </summary>
    private int _maxMatch;
    /// <summary>
    /// 각 노드의 슬랙(slack) 값입니다.
    /// </summary>
    private float[] _slack;
    /// <summary>
    /// 슬랙 값을 업데이트하는 데 사용되는 x 노드의 인덱스입니다.
    /// </summary>
    private int[] _slackx;
    /// <summary>
    /// 교대 경로(augmenting path)를 기억하는 데 사용됩니다.
    /// </summary>
    private int[] _prev;

    // 생성자
    //
    // 예전에는 생성자가 여섯 개였고 그중 넷이 호출부 없는 죽은 코드였습니다.
    //   Hungarian()
    //   Hungarian(List<Vector3>, List<Transform>)
    //   Hungarian(List<Unit>,    List<Vector3>)
    //   Hungarian(List<Unit>,    List<Transform>)
    //
    // 넷 다 Transform에서 위치를 읽는 구식 경로였습니다. 진형 슬롯이
    // Transform이던 시절의 잔재이며, 슬롯이 배열로 바뀌면서 쓰이지 않게
    // 되었습니다(실측 2.955ms -> 0.029ms). 남겨 두면 다음 사람이
    // "슬롯을 Transform으로 다뤄도 된다"고 읽으므로 지웁니다.
    //
    // 지금 쓰이는 것은 아래 둘뿐입니다. (Formation_Matcher)

    /// <summary>
    /// 비용 매트릭스를 받아 헝가리안 알고리즘 클래스를 초기화합니다.
    /// </summary>
    /// <param name="costMatrix">비용 매트릭스</param>
    public Hungarian(float[,] costMatrix)
    {
        _costMatrix = costMatrix;
    }

    /// <summary>
    /// 현재 위치 벡터와 목표 위치 벡터를 사용하여 비용 매트릭스를 생성하고 클래스를 초기화합니다.
    /// </summary>
    /// <param name="currentVectors">현재 위치 벡터 리스트입니다.</param>
    /// <param name="targetVectors">목표 위치 벡터 리스트입니다.</param>
    public Hungarian(List<Vector3> currentVectors, List<Vector3> targetVectors)
    {
        int num = currentVectors.Count;
        _costMatrix = new float[num, num];

        Vector3 position1 = new Vector3(0, 0, 0);
        Vector3 position2 = new Vector3(0, 0, 0);
        Vector3 length;

        // 두 집합의 중심을 각각 구합니다.
        //
        // 왜 필요한가:
        // 비용을 '실제 이동 거리'로 두면, 부대가 통째로 멀리 이동할 때
        // 모든 값에 같은 큰 수가 더해집니다. 그러면 비용 차이가 묻혀
        // 어느 배정이 나은지 구분되지 않습니다.
        //
        // 중심 간 오프셋(length)을 빼면 '상대 배치'만 남습니다.
        // 즉 '누가 어느 자리에 서야 대열이 덜 꼬이는가'만 비교하게 됩니다.
        for (int i = 0; i < num; i++)
        {
            position1 += targetVectors[i];
            position2 += currentVectors[i];
        }

        position1 = position1 / num;
        position2 = position2 / num;
        length = position1 - position2;

        for (int i = 0; i < num; i++)
        {
            for (int j = 0; j < num; j++)
            {
                _costMatrix[i, j] = (targetVectors[i] - currentVectors[j] - length).magnitude;
            }
        }
    }

    // 공개 메서드
    /// <summary>
    /// 헝가리안 알고리즘을 실행하여 최적의 매칭을 찾고 결과를 반환합니다.
    /// </summary>
    /// <returns>매칭된 작업자-작업 쌍의 인덱스 배열입니다.</returns>
    public int[] Run()
    {
        _n = _costMatrix.GetLength(0);

        _lx = new float[_n];
        _ly = new float[_n];
        _s = new bool[_n];
        _t = new bool[_n];
        _matchX = new int[_n];
        _matchY = new int[_n];
        _slack = new float[_n];
        _slackx = new int[_n];
        _prev = new int[_n];
        _inf = int.MaxValue;

        InitMatches();

        // 정사각 행렬이 아니면 1:1 매칭이 성립하지 않습니다.
        if (_n != _costMatrix.GetLength(1)) return null;

        InitLbls();

        _maxMatch = 0;
        InitialMatching();

        var q = new Queue<int>();

        #region augment
        while (_maxMatch != _n)
        {
            q.Clear();
            InitSt();

            // 이번 회차의 탐색 시작점(root)입니다.
            // 아직 짝이 없는 행 하나를 골라 거기서 증대 경로를 찾습니다.
            var root = 0;
            int x;
            var y = 0;

            for (x = 0; x < _n; x++)
            {
                if (_matchX[x] != -1) continue;
                q.Enqueue(x);
                root = x;
                _prev[x] = -2;
                _s[x] = true;
                break;
            }

            // slack[y]는 '열 y를 부분 그래프에 넣으려면 레이블을 얼마나
            // 낮춰야 하는가'입니다. 이 값이 0이면 이미 쓸 수 있는 간선입니다.
            // 막혔을 때 이 최솟값만큼 레이블을 조정하면 간선이 하나 늘어납니다.
            for (var i = 0; i < _n; i++)
            {
                _slack[i] = _costMatrix[root, i] - _lx[root] - _ly[i];
                _slackx[i] = root;
            }

            while (true)
            {
                // 1) 현재 부분 그래프 안에서 너비 우선으로 증대 경로를 찾습니다.
                while (q.Count != 0)
                {
                    x = q.Dequeue();
                    var lxx = _lx[x];
                    for (y = 0; y < _n; y++)
                    {
                        // 비용이 정확히 lx+ly인 간선만 부분 그래프에 속합니다.
                        // 이미 트리에 들어온 열(_t)은 다시 보지 않습니다.
                        if (_costMatrix[x, y] != lxx + _ly[y] || _t[y]) continue;

                        // 짝이 없는 열을 만났으면 증대 경로가 완성된 것입니다.
                        if (_matchY[y] == -1) break;
                        _t[y] = true;
                        q.Enqueue(_matchY[y]);
                        AddToTree(_matchY[y], x);
                    }
                    if (y < _n) break;
                }
                if (y < _n) break;

                // 2) 부분 그래프 안에서 더 나아갈 수 없습니다.
                //    레이블을 조정해 간선을 늘린 뒤 다시 시도합니다.
                UpdateLabels();

                for (y = 0; y < _n; y++)
                {
                    if (_t[y] || _slack[y] != 0) continue;
                    if (_matchY[y] == -1)
                    {
                        x = _slackx[y];
                        break;
                    }
                    _t[y] = true;
                    if (_s[_matchY[y]]) continue;
                    q.Enqueue(_matchY[y]);
                    AddToTree(_matchY[y], _slackx[y]);
                }
                if (y < _n) break;
            }

            _maxMatch++;

            // 3) 찾은 증대 경로를 따라 짝을 뒤집습니다.
            //    경로를 거슬러 올라가며 기존 짝을 끊고 새 짝을 잇습니다.
            //    이 한 번으로 매칭 수가 정확히 하나 늘어납니다.
            //    (_prev가 -2인 지점이 root이며 거기서 멈춥니다)
            int ty;
            for (int cx = x, cy = y; cx != -2; cx = _prev[cx], cy = ty)
            {
                ty = _matchX[cx];
                _matchY[cy] = cx;
                _matchX[cx] = cy;
            }
        }
        #endregion

        return _matchY;
    }

    // 비공개 메서드
    /// <summary>
    /// 매칭 배열을 초기화합니다.
    /// </summary>
    private void InitMatches()
    {
        for (var i = 0; i < _n; i++)
        {
            _matchX[i] = -1;
            _matchY[i] = -1;
        }
    }

    /// <summary>
    /// 매칭 트리에 포함된 노드 배열을 초기화합니다.
    /// </summary>
    private void InitSt()
    {
        for (var i = 0; i < _n; i++)
        {
            _s[i] = false;
            _t[i] = false;
        }
    }

    /// <summary>
    /// 초기 레이블 값을 설정합니다.
    ///
    /// 행 레이블은 그 행의 최솟값, 열 레이블은 (행 레이블을 뺀 뒤의) 열 최솟값으로
    /// 둡니다. 이렇게 하면 모든 간선에서 cost >= lx + ly가 성립하고(유효한 레이블),
    /// 등호가 성립하는 간선이 최소 하나씩은 생겨 탐색을 시작할 수 있습니다.
    /// </summary>
    private void InitLbls()
    {
        for (var i = 0; i < _n; i++)
        {
            var minRow = _costMatrix[i, 0];
            for (var j = 0; j < _n; j++)
            {
                if (_costMatrix[i, j] < minRow) minRow = _costMatrix[i, j];
                if (minRow == 0) break;
            }
            _lx[i] = minRow;
        }
        for (var j = 0; j < _n; j++)
        {
            var minColumn = _costMatrix[0, j] - _lx[0];
            for (var i = 0; i < _n; i++)
            {
                if (_costMatrix[i, j] - _lx[i] < minColumn) minColumn = _costMatrix[i, j] - _lx[i];
                if (minColumn == 0) break;
            }
            _ly[j] = minColumn;
        }
    }

    /// <summary>
    /// 레이블을 조정해 부분 그래프에 간선을 추가합니다.
    ///
    /// 트리에 없는 열들의 slack 중 최솟값(delta)만큼 움직입니다.
    ///   트리에 든 행  : 레이블 증가  (기존 간선 유지)
    ///   트리에 든 열  : 레이블 감소  (기존 간선 유지)
    ///   나머지 열     : slack 감소   (그중 하나가 0이 되어 새 간선이 열림)
    ///
    /// delta보다 크게 움직이면 레이블이 무효가 되고, 작게 움직이면
    /// 새 간선이 생기지 않아 진전이 없습니다. 그래서 정확히 최솟값을 씁니다.
    /// </summary>
    private void UpdateLabels()
    {
        var delta = _inf;
        for (var i = 0; i < _n; i++)
            if (!_t[i])
                if (delta > _slack[i])
                    delta = _slack[i];
        for (var i = 0; i < _n; i++)
        {
            if (_s[i]) _lx[i] = _lx[i] + delta;
            if (_t[i]) _ly[i] = _ly[i] - delta;
            else _slack[i] = _slack[i] - delta;
        }
    }

    /// <summary>
    /// 행 하나를 탐색 트리에 넣고, 그로 인해 줄어드는 slack을 갱신합니다.
    ///
    /// 트리에 행이 추가되면 그 행을 통해 열에 닿는 새 경로가 생기므로,
    /// 각 열의 slack을 '더 싼 값이 있으면' 낮춰 둡니다.
    /// 이 갱신이 있어야 UpdateLabels의 delta가 정확해집니다.
    /// </summary>
    /// <param name="x">트리에 추가할 행 인덱스입니다.</param>
    /// <param name="prevx">이 행에 도달한 이전 행 인덱스입니다. 경로 복원에 씁니다.</param>
    private void AddToTree(int x, int prevx)
    {
        _s[x] = true;
        _prev[x] = prevx;
        var lxx = _lx[x];
        for (var y = 0; y < _n; y++)
        {
            if (_costMatrix[x, y] - lxx - _ly[y] >= _slack[y]) continue;
            _slack[y] = _costMatrix[x, y] - lxx - _ly[y];
            _slackx[y] = x;
        }
    }

    /// <summary>
    /// 탐욕적으로 초기 매칭을 만듭니다.
    ///
    /// 정확성에는 영향이 없고 속도만 위한 단계입니다. 아무 매칭 없이
    /// 시작해도 결과는 같지만, 여기서 쉬운 짝을 미리 맺어 두면
    /// 비싼 증대 경로 탐색을 그만큼 덜 돌게 됩니다.
    /// </summary>
    private void InitialMatching()
    {
        for (var x = 0; x < _n; x++)
        {
            for (var y = 0; y < _n; y++)
            {
                if (_costMatrix[x, y] != _lx[x] + _ly[y] || _matchY[y] != -1) continue;
                _matchX[x] = y;
                _matchY[y] = x;
                _maxMatch++;
                break;
            }
        }
    }
}
