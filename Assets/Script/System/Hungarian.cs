using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 헝가리안 알고리즘을 구현하여 최적의 이분 매칭을 찾는 클래스입니다.
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
    /// <summary>
    /// 기본 생성자입니다.
    /// </summary>
    public Hungarian()
    {
    }

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

    /// <summary>
    /// 현재 위치 벡터와 목표 트랜스폼을 사용하여 비용 매트릭스를 생성하고 클래스를 초기화합니다.
    /// </summary>
    /// <param name="currentVectors">현재 위치 벡터 리스트입니다.</param>
    /// <param name="targetTransforms">목표 트랜스폼 리스트입니다.</param>
    public Hungarian(List<Vector3> currentVectors, List<Transform> targetTransforms)
    {
        int num = currentVectors.Count;
        _costMatrix = new float[num, num];

        Vector3 position1 = new Vector3(0, 0, 0);
        Vector3 position2 = new Vector3(0, 0, 0);
        Vector3 length;

        for (int i = 0; i < num; i++)
        {
            position1 += targetTransforms[i].position;
            position2 += currentVectors[i];
        }

        position1 = position1 / num;
        position2 = position2 / num;
        length = position1 - position2;

        for (int i = 0; i < num; i++)
        {
            for (int j = 0; j < num; j++)
            {
                _costMatrix[i, j] = (targetTransforms[i].position - currentVectors[j] - length).magnitude;
            }
        }
    }

    /// <summary>
    /// 유닛 리스트와 목표 위치 벡터를 사용하여 비용 매트릭스를 생성하고 클래스를 초기화합니다.
    /// </summary>
    /// <param name="Units">유닛 리스트입니다.</param>
    /// <param name="targetVectors">목표 위치 벡터 리스트입니다.</param>
    public Hungarian(List<Unit> Units, List<Vector3> targetVectors)
    {
        int num = Units.Count;
        _costMatrix = new float[num, num];

        Vector3 position1 = new Vector3(0, 0, 0);
        Vector3 position2 = new Vector3(0, 0, 0);
        Vector3 length;

        for (int i = 0; i < num; i++)
        {
            position1 += targetVectors[i];
            position2 += Units[i].transform.position;
        }

        position1 = position1 / num;
        position2 = position2 / num;
        length = position1 - position2;

        for (int i = 0; i < num; i++)
        {
            for (int j = 0; j < num; j++)
            {
                _costMatrix[i, j] = (targetVectors[i] - Units[j].transform.position - length).magnitude;
            }
        }
    }

    /// <summary>
    /// 유닛 리스트와 목표 트랜스폼을 사용하여 비용 매트릭스를 생성하고 클래스를 초기화합니다.
    /// </summary>
    /// <param name="Units">유닛 리스트입니다.</param>
    /// <param name="targetTransforms">목표 트랜스폼 리스트입니다.</param>
    public Hungarian(List<Unit> Units, List<Transform> targetTransforms)
    {
        int num = Units.Count;
        _costMatrix = new float[num, num];

        Vector3 position1 = new Vector3(0, 0, 0);
        Vector3 position2 = new Vector3(0, 0, 0);
        Vector3 length;

        for (int i = 0; i < num; i++)
        {
            position1 += targetTransforms[i].position;
            position2 += Units[i].transform.position;
        }

        position1 = position1 / num;
        position2 = position2 / num;
        length = position1 - position2;

        for (int i = 0; i < num; i++)
        {
            for (int j = 0; j < num; j++)
            {
                _costMatrix[i, j] = (targetTransforms[i].position - Units[j].transform.position - length).magnitude;
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

            for (var i = 0; i < _n; i++)
            {
                _slack[i] = _costMatrix[root, i] - _lx[root] - _ly[i];
                _slackx[i] = root;
            }

            while (true)
            {
                while (q.Count != 0)
                {
                    x = q.Dequeue();
                    var lxx = _lx[x];
                    for (y = 0; y < _n; y++)
                    {
                        if (_costMatrix[x, y] != lxx + _ly[y] || _t[y]) continue;
                        if (_matchY[y] == -1) break;
                        _t[y] = true;
                        q.Enqueue(_matchY[y]);
                        AddToTree(_matchY[y], x);
                    }
                    if (y < _n) break;
                }
                if (y < _n) break;

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

    // 정적 메서드
    /// <summary>
    /// Vector3 배열을 사용하여 헝가리안 알고리즘을 실행하고 결과를 반환합니다.
    /// </summary>
    /// <param name="currentVectors">현재 위치 벡터 배열입니다.</param>
    /// <param name="targetVectors">목표 위치 벡터 배열입니다.</param>
    /// <returns>매칭된 목표 벡터의 배열입니다.</returns>
    public static Vector3[] HungarianVector(Vector3[] currentVectors, Vector3[] targetVectors)
    {
        return HungarianVector(currentVectors.ToList(), targetVectors.ToList()).ToArray();
    }

    /// <summary>
    /// Vector3 리스트를 사용하여 헝가리안 알고리즘을 실행하고 결과를 반환합니다.
    /// 이 함수는 더 이상 사용되지 않습니다.
    /// </summary>
    public static List<Vector3> HungarianVector(List<Vector3> currentVectors, List<Vector3> targetVectors)
    {
        int N = 500;
        int INF = 987654321;
        int[,] w = new int[N, N];
        int n = currentVectors.Count;
        int[] match_x = new int[N];
        int[] match_y = new int[N];
        int[] l_x = new int[N];
        int[] l_y = new int[N];
        bool[] s = new bool[N];
        bool[] t = new bool[N];
        int[] slack = new int[N];
        int[] slack_x = new int[N];
        int[] tree_x = new int[N];
        int[] tree_y = new int[N];

        // 비용 매트릭스 생성
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                w[i, j] = Mathf.RoundToInt((targetVectors[i] - currentVectors[j]).magnitude);
            }
        }

        // 변수 초기화
        for (int i = 0; i < N; i++)
        {
            match_x[i] = -1;
            match_y[i] = -1;
            l_x[i] = INF;
            l_y[i] = INF;
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                l_x[i] = Mathf.Min(l_x[i], w[i, j]);
            }
        }

        // 알고리즘 실행
        int m = 0;
        while (m != n)
        {
            for (int i = 0; i < N; i++)
            {
                tree_x[i] = -1;
                tree_y[i] = -1;
                s[i] = false;
                t[i] = false;
            }

            int s_start = 0;
            for (int i = 0; i < n; ++i)
            {
                if (match_x[i] == -1)
                {
                    s[i] = true;
                    s_start = i;
                    break;
                }
            }

            for (int i = 0; i < n; ++i)
            {
                slack[i] = w[s_start, i] - l_x[s_start] - l_y[i];
                slack_x[i] = s_start;
            }

            bool bStop = true;
            while (bStop)
            {
                int y = -1;
                for (int i = 0; i < n; ++i)
                {
                    if (slack[i] == 0 && t[i] == false)
                    {
                        y = i;
                    }
                }

                if (y == -1)
                {
                    int alpha = INF;
                    for (int i = 0; i < n; ++i)
                    {
                        if (t[i] == false)
                        {
                            alpha = Mathf.Min(alpha, slack[i]);
                        }
                    }
                    for (int i = 0; i < n; ++i)
                    {
                        if (s[i]) l_x[i] += alpha;
                        if (t[i]) l_y[i] -= alpha;
                    }
                    for (int i = 0; i < n; ++i)
                    {
                        if (!t[i])
                        {
                            slack[i] -= alpha;
                            if (slack[i] == 0)
                            {
                                y = i;
                            }
                        }
                    }
                }

                if (match_y[y] == -1)
                {
                    tree_y[y] = slack_x[y];
                    while (y != -1)
                    {
                        int x = tree_y[y];
                        match_y[y] = x;
                        int next_y = match_x[x];
                        match_x[x] = y;
                        y = next_y;
                    }
                    m++;
                    bStop = false;
                }
                else
                {
                    int z = match_y[y];
                    tree_x[z] = y;
                    tree_y[y] = slack_x[y];
                    s[z] = true;
                    t[y] = true;
                    for (int i = 0; i < n; ++i)
                    {
                        if (slack[i] > w[z, i] - l_x[z] - l_y[i])
                        {
                            slack[i] = w[z, i] - l_x[z] - l_y[i];
                            slack_x[i] = z;
                        }
                    }
                }
            }
        }

        List<Vector3> vector = new List<Vector3>();
        for (int i = 0; i < n; ++i)
        {
            vector.Add(targetVectors[match_y[i]]);
            Debug.Log("match_y : " + match_y[i] + " i : " + i);
        }
        return vector;
    }

    /// <summary>
    /// Army 유닛과 Vector3 목표를 사용하여 헝가리안 알고리즘을 실행하고 결과를 반환합니다.
    /// 이 함수는 더 이상 사용되지 않습니다.
    /// </summary>
    public static List<Army> HungarianArmy(List<Army> army, Vector3[] targetVectors)
    {
        int N = 500;
        int INF = 987654321;
        int[,] w = new int[N, N];
        int n = army.Count;
        int[] match_x = new int[N];
        int[] match_y = new int[N];
        int[] l_x = new int[N];
        int[] l_y = new int[N];
        bool[] s = new bool[N];
        bool[] t = new bool[N];
        int[] slack = new int[N];
        int[] slack_x = new int[N];
        int[] tree_x = new int[N];
        int[] tree_y = new int[N];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                w[i, j] = Mathf.RoundToInt((targetVectors[i] - army[j].transform.position).magnitude);
            }
        }

        for (int i = 0; i < N; i++)
        {
            match_x[i] = -1;
            match_y[i] = -1;
            l_x[i] = INF;
            l_y[i] = INF;
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                l_x[i] = Mathf.Min(l_x[i], w[i, j]);
            }
        }

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                l_y[i] = Mathf.Min(l_y[i], w[j, i] - l_x[j]);
            }
        }

        int m = 0;
        while (m != n)
        {
            for (int i = 0; i < N; i++)
            {
                tree_x[i] = -1;
                tree_y[i] = -1;
                s[i] = false;
                t[i] = false;
            }

            int s_start = 0;
            for (int i = 0; i < n; ++i)
            {
                if (match_x[i] == -1)
                {
                    s[i] = true;
                    s_start = i;
                    break;
                }
            }

            for (int i = 0; i < n; ++i)
            {
                slack[i] = w[s_start, i] - l_x[s_start] - l_y[i];
                slack_x[i] = s_start;
            }

            bool bStop = true;
            while (bStop)
            {
                int y = -1;
                for (int i = 0; i < n; ++i)
                {
                    if (slack[i] == 0 && t[i] == false)
                    {
                        y = i;
                    }
                }

                if (y == -1)
                {
                    int alpha = INF;
                    for (int i = 0; i < n; ++i)
                    {
                        if (t[i] == false)
                        {
                            alpha = Mathf.Min(alpha, slack[i]);
                        }
                    }
                    for (int i = 0; i < n; ++i)
                    {
                        if (s[i]) l_x[i] += alpha;
                        if (t[i]) l_y[i] -= alpha;
                    }
                    for (int i = 0; i < n; ++i)
                    {
                        if (!t[i])
                        {
                            slack[i] -= alpha;
                            if (slack[i] == 0)
                            {
                                y = i;
                            }
                        }
                    }
                }

                if (match_y[y] == -1)
                {
                    tree_y[y] = slack_x[y];
                    while (y != -1)
                    {
                        int x = tree_y[y];
                        match_y[y] = x;
                        int next_y = match_x[x];
                        match_x[x] = y;
                        y = next_y;
                    }
                    m++;
                    bStop = false;
                }
                else
                {
                    int z = match_y[y];
                    tree_x[z] = y;
                    tree_y[y] = slack_x[y];
                    s[z] = true;
                    t[y] = true;
                    for (int i = 0; i < n; ++i)
                    {
                        if (slack[i] > w[z, i] - l_x[z] - l_y[i])
                        {
                            slack[i] = w[z, i] - l_x[z] - l_y[i];
                            slack_x[i] = z;
                        }
                    }
                }
            }
        }

        List<Army> currentarmyUnits = new List<Army>();

        for (int i = 0; i < n; ++i)
        {
            if (match_y[i] == -1)
            {
                // 매칭되지 않은 경우
            }
            else
            {
                currentarmyUnits.Add(army[match_y[i]]);
            }
        }
        return currentarmyUnits;
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
    /// 초기 레이블(label) 값을 설정합니다.
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
    /// 레이블을 업데이트하여 교대 경로를 찾습니다.
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
    /// 매칭 트리에 노드를 추가합니다.
    /// </summary>
    /// <param name="x">현재 노드입니다.</param>
    /// <param name="prevx">이전 노드입니다.</param>
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
    /// 초기 매칭을 수행합니다.
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
