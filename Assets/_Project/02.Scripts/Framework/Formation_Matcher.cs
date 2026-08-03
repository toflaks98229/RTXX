using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛과 진형 슬롯을 짝지어 주는 매칭기입니다.
///
/// 배경:
/// 기존에는 인원 수와 무관하게 항상 헝가리안 알고리즘(O(n^3))을 돌렸습니다.
/// 300명 부대 하나를 재배치할 때마다 약 2,700만 번의 연산이 메인 스레드에서
/// 동기 실행되어 명령을 내리는 순간 눈에 띄는 프레임 스파이크가 생겼습니다.
///
/// 해결:
/// 인원이 적으면 기존처럼 '정확한' 헝가리안을 그대로 씁니다.
/// 인원이 많아지면 진형 축을 따라 정렬한 뒤 분대 단위로 잘라서 각 분대 안에서만
/// 헝가리안을 돌립니다. 비용은 O(n * s^2) 수준으로 떨어집니다. (s = 분대 크기)
///
/// 품질:
/// 진형은 축을 따라 규칙적으로 배열되므로, 축 정렬 후 국소 매칭의 결과는
/// 전역 최적해와 거의 같습니다. 유닛이 교차해서 걸어가는 부자연스러움도 생기지 않습니다.
/// </summary>
public static class Formation_Matcher
{
    /// <summary>이 인원 이하면 전역 최적(헝가리안)을 그대로 사용합니다.</summary>
    public const int exactThreshold = 64;

    /// <summary>분대 단위 매칭에서 한 분대의 최대 인원입니다.</summary>
    public const int squadSize = 32;

    /// <summary>
    /// 현재 위치와 목표 위치를 짝지어 반환합니다.
    /// </summary>
    /// <param name="currents">유닛들의 현재 위치입니다.</param>
    /// <param name="targets">진형 슬롯 위치입니다. currents와 개수가 같아야 합니다.</param>
    /// <returns>result[유닛 인덱스] = 배정된 슬롯 인덱스. 실패 시 null입니다.</returns>
    public static int[] Match(List<Vector3> currents, List<Vector3> targets)
    {
        if (currents == null || targets == null) return null;

        int n = currents.Count;
        if (n == 0) return new int[0];
        if (targets.Count < n) return null;

        if (n <= exactThreshold)
        {
            return new Hungarian(currents, targets).Run();
        }

        return Match_Hierarchical(currents, targets, n);
    }

    /// <summary>
    /// 진형 축을 따라 정렬한 뒤 분대 단위로 나누어 매칭합니다.
    /// </summary>
    private static int[] Match_Hierarchical(List<Vector3> currents, List<Vector3> targets, int n)
    {
        // 1. 목표 슬롯들이 늘어선 주축을 구합니다. (XZ 평면 주성분)
        Vector3 axis = Get_Principal_Axis(targets, n);

        // 2. 축 위 좌표로 양쪽을 정렬합니다.
        int[] currentOrder = Sort_By_Projection(currents, n, axis);
        int[] targetOrder = Sort_By_Projection(targets, n, axis);

        // 3. 전체 중심 간 offset을 미리 구해 둡니다.
        //    기존 Hungarian 생성자와 동일하게, 이동량이 아닌 '상대 배치'로 비교하기 위함입니다.
        Vector3 currentCenter = Get_Center(currents, n);
        Vector3 targetCenter = Get_Center(targets, n);
        Vector3 offset = targetCenter - currentCenter;

        int[] result = new int[n];

        // 4. 같은 순번의 분대끼리 국소 매칭합니다.
        for (int start = 0; start < n; start += squadSize)
        {
            int count = Mathf.Min(squadSize, n - start);

            float[,] cost = new float[count, count];

            // 기존 규약과 동일하게 행 = 목표, 열 = 유닛입니다.
            for (int row = 0; row < count; row++)
            {
                Vector3 targetPos = targets[targetOrder[start + row]];

                for (int col = 0; col < count; col++)
                {
                    Vector3 currentPos = currents[currentOrder[start + col]];
                    cost[row, col] = (targetPos - currentPos - offset).sqrMagnitude;
                }
            }

            int[] localMatch = new Hungarian(cost).Run();
            if (localMatch == null) return null;

            // localMatch[열(유닛)] = 행(목표) 이므로 원래 인덱스로 되돌립니다.
            for (int col = 0; col < count; col++)
            {
                int unitIndex = currentOrder[start + col];
                int slotIndex = targetOrder[start + localMatch[col]];
                result[unitIndex] = slotIndex;
            }
        }

        return result;
    }

    /// <summary>
    /// XZ 평면에서 점들이 가장 넓게 퍼진 방향(주성분)을 구합니다.
    /// 진형이 어떤 각도로 놓여 있어도 올바른 정렬 축을 얻기 위한 것입니다.
    /// </summary>
    private static Vector3 Get_Principal_Axis(List<Vector3> points, int n)
    {
        Vector3 center = Get_Center(points, n);

        float sxx = 0.0f, sxz = 0.0f, szz = 0.0f;
        for (int i = 0; i < n; i++)
        {
            float dx = points[i].x - center.x;
            float dz = points[i].z - center.z;
            sxx += dx * dx;
            sxz += dx * dz;
            szz += dz * dz;
        }

        // 2x2 공분산 행렬의 주축 각도입니다.
        float angle = 0.5f * Mathf.Atan2(2.0f * sxz, sxx - szz);
        Vector3 axis = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));

        // 모든 점이 한 곳에 몰려 축을 정할 수 없으면 임의의 안정적인 축을 씁니다.
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;

        return axis;
    }

    /// <summary>점들의 평균 위치를 구합니다.</summary>
    private static Vector3 Get_Center(List<Vector3> points, int n)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < n; i++) sum += points[i];
        return sum / n;
    }

    /// <summary>
    /// 축에 정사영한 값을 기준으로 인덱스를 오름차순 정렬해 반환합니다.
    /// </summary>
    private static int[] Sort_By_Projection(List<Vector3> points, int n, Vector3 axis)
    {
        int[] order = new int[n];
        float[] keys = new float[n];

        for (int i = 0; i < n; i++)
        {
            order[i] = i;
            keys[i] = Vector3.Dot(points[i], axis);
        }

        System.Array.Sort(keys, order);
        return order;
    }
}
