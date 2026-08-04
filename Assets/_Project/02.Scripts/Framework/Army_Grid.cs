using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부대 '중심점'만 담는 거친 공간 격자입니다.
///
/// 왜 필요한가:
/// 부대 단위 조회가 전부 allArmies 전수 순회였습니다.
///
///   _Update_Detection      : 부대마다 전 부대 순회
///   Get_General_Aura       : 부대마다 전 부대 순회
///   Find_Nearest_Enemy_Army: 산개 태세 부대마다 전 부대 순회
///   Battle_AI.Find_Soft_Target / Is_Cavalry_Near
///
/// 부대 수 N에 대해 틱당 최소 3N^2의 거리 계산이 발생합니다.
/// N=20이면 1,200회로 무시할 수 있지만 N=100이면 30,000회가 되어
/// 그 자체가 병목이 됩니다. 유닛 수와 무관하게 '부대 수'만으로 나빠지므로,
/// 대규모 전투를 상정하면 반드시 걷어내야 하는 구조입니다.
///
/// 유닛 격자(Spatial_Grid)와 다른 점:
/// 항목이 부대(수십~수백)라 유닛(수천~수만)보다 훨씬 적습니다.
/// 그래서 NativeArray나 Job이 필요 없고, 관리 배열로 충분히 쌉니다.
/// 대신 셀이 훨씬 커야 합니다. 부대는 '점'이 아니라 넓게 퍼진 덩어리이고,
/// 탐지 반경도 유닛 사거리보다 훨씬 크기 때문입니다.
///
/// 정확성:
/// Query()는 반경 안의 부대를 '빠짐없이' 돌려줍니다. 다만 셀 경계 때문에
/// 반경 밖의 부대도 일부 섞여 나올 수 있으므로, 호출부는 기존과 동일하게
/// 거리 검사를 그대로 유지해야 합니다. 격자는 후보를 줄일 뿐이며
/// 판정을 대신하지 않습니다.
/// </summary>
public class Army_Grid
{
    /// <summary>
    /// 격자 셀 한 변의 길이입니다.
    ///
    /// 부대 탐지 반경(장군 오라, 기병 경계 등)이 보통 수십 미터이므로
    /// 그 정도 크기가 적당합니다. 너무 작으면 한 질의가 훑는 셀이 많아지고,
    /// 너무 크면 걸러지는 후보가 줄어 순회와 다를 바 없어집니다.
    /// </summary>
    public const float cellSize = 32.0f;

    /// <summary>셀 해시 -> 그 셀에 속한 부대들입니다.</summary>
    private readonly Dictionary<int, List<Army>> cells = new Dictionary<int, List<Army>>();

    /// <summary>
    /// 비워 둔 리스트를 재사용하기 위한 풀입니다.
    /// 매 틱 Dictionary를 통째로 새로 만들면 그 할당이 GC를 부릅니다.
    /// </summary>
    private readonly Stack<List<Army>> pool = new Stack<List<Army>>();

    /// <summary>이 격자가 만들어진 시점의 틱입니다. 중복 구축을 막습니다.</summary>
    private uint builtTick = uint.MaxValue;

    /// <summary>
    /// 부대 중심점으로 격자를 다시 만듭니다.
    ///
    /// 같은 틱에 여러 번 불러도 한 번만 실제로 구축합니다.
    /// 부대 갱신 순서와 무관하게 '틱 시작 시점의 배치'를 모두가 보게 하려면
    /// 틱당 한 번만 만들어야 하기 때문입니다. (결정론)
    /// </summary>
    /// <param name="armies">격자에 담을 부대들입니다.</param>
    /// <param name="tick">현재 시뮬레이션 틱입니다.</param>
    public void Rebuild(IReadOnlyList<Army> armies, uint tick)
    {
        if (builtTick == tick) return;
        builtTick = tick;

        Clear_Cells();

        if (armies == null) return;

        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;

            // 전멸한 부대는 어떤 질의에도 잡히면 안 됩니다.
            if (army.units.Count == 0) continue;

            int hash = Hash(army.GetPosition());

            if (!cells.TryGetValue(hash, out List<Army> bucket))
            {
                bucket = pool.Count > 0 ? pool.Pop() : new List<Army>(8);
                cells[hash] = bucket;
            }

            bucket.Add(army);
        }
    }

    /// <summary>
    /// 반경 안에 있을 '가능성이 있는' 부대들을 result에 담습니다.
    ///
    /// 주의: 반경 밖의 부대도 섞여 나옵니다. 호출부는 반드시 거리 검사를
    /// 그대로 수행해야 합니다. 이 함수의 역할은 후보를 줄이는 것뿐입니다.
    ///
    /// result는 호출부가 재사용하는 리스트여야 합니다.
    /// 매 질의마다 새로 만들면 격자로 아낀 것을 GC가 도로 가져갑니다.
    /// </summary>
    /// <param name="center">질의 중심입니다.</param>
    /// <param name="radius">질의 반경입니다.</param>
    /// <param name="result">결과를 담을 리스트입니다. 먼저 비워집니다.</param>
    public void Query(Vector3 center, float radius, List<Army> result)
    {
        result.Clear();

        // 반경이 셀보다 크면 그만큼 넓게 훑어야 빠뜨리지 않습니다.
        int span = Mathf.Max(1, Mathf.CeilToInt(radius / cellSize));

        int cx = Mathf.FloorToInt(center.x / cellSize);
        int cz = Mathf.FloorToInt(center.z / cellSize);

        for (int dz = -span; dz <= span; dz++)
        {
            for (int dx = -span; dx <= span; dx++)
            {
                int hash = Hash(cx + dx, cz + dz);

                if (!cells.TryGetValue(hash, out List<Army> bucket)) continue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    result.Add(bucket[i]);
                }
            }
        }
    }

    /// <summary>
    /// 격자를 비웁니다. 씬을 다시 시작할 때 호출합니다.
    /// </summary>
    public void Clear()
    {
        Clear_Cells();
        builtTick = uint.MaxValue;
    }

    /// <summary>셀 내용을 비우고 리스트는 풀로 돌려보냅니다.</summary>
    private void Clear_Cells()
    {
        foreach (var pair in cells)
        {
            pair.Value.Clear();
            pool.Push(pair.Value);
        }

        cells.Clear();
    }

    /// <summary>월드 좌표를 셀 해시로 변환합니다. y축은 무시합니다.</summary>
    private static int Hash(Vector3 position)
    {
        return Hash(Mathf.FloorToInt(position.x / cellSize),
                    Mathf.FloorToInt(position.z / cellSize));
    }

    /// <summary>격자 좌표를 해시 키로 변환합니다. Spatial_Grid와 같은 방식입니다.</summary>
    private static int Hash(int cellX, int cellZ)
    {
        unchecked
        {
            return (cellX * 73856093) ^ (cellZ * 19349663);
        }
    }
}
