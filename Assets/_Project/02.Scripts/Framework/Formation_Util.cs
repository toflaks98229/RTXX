using UnityEngine;

/// <summary>
/// 전열축과 정면 방향 사이의 변환 규약을 한 곳에 모아 둔 도우미입니다.
///
/// 왜 필요한가:
/// 이 프로젝트에는 '전열축(lineDirection)'과 '정면(facing)'이라는 두 방향이
/// 함께 존재합니다. 부대는 옆으로 늘어선 채 앞을 보므로 둘은 항상 직각입니다.
///
///     전열축 ──────────────▶   (부대가 늘어선 방향)
///        │
///        ▼ 정면                (유닛들이 바라보는 방향)
///
/// 이 90도 관계가 예전에는 네 곳에 서로 다른 형태로 흩어져 있었습니다.
///
///   Army_Move.Move_Stop            : LookRotation(dir) * Euler(0, -90, 0)
///   Army_Move.Rotation_To_Formation: LookRotation(dir) * Euler(0, -90, 0)
///   Army_Move.Move_Reformation_Line: AngleAxis(-90, up) * dir
///   Unit_Move.Move_Start           : LookRotation(dir) * Euler(0, -90, 0)
///
/// 전부 같은 뜻이지만 표기가 달라, 부호 하나를 잘못 읽으면 전열이 180도
/// 뒤집힙니다. 실제로 UI 마커의 방향 부호가 호출 경로마다 갈려 있었습니다.
///
/// 규약을 여기 한 곳에만 두면, 바꿔야 할 때도 한 줄만 고치면 됩니다.
/// </summary>
public static class Formation_Util
{
    /// <summary>
    /// 전열축에서 정면 방향(정규화)을 구합니다.
    ///
    /// 입력이 0에 가까우면 회전을 정의할 수 없으므로 Vector3.forward를
    /// 돌려줍니다. 호출부가 그 값으로 LookRotation을 부르면 예외가 나기 때문입니다.
    /// </summary>
    /// <param name="lineDirection">전열이 늘어선 방향입니다. 정규화되지 않아도 됩니다.</param>
    public static Vector3 Facing_From_Line(Vector3 lineDirection)
    {
        lineDirection.y = 0.0f;

        if (lineDirection.sqrMagnitude < 0.0001f) return Vector3.forward;

        return Quaternion.AngleAxis(-90.0f, Vector3.up) * lineDirection.normalized;
    }

    /// <summary>
    /// 전열축에서 부대 기준점이 취해야 할 회전을 구합니다.
    ///
    /// Move_Stop / Rotation_To_Formation이 쓰던
    /// LookRotation(dir) * Euler(0, -90, 0)과 정확히 같은 값입니다.
    /// </summary>
    /// <param name="lineDirection">전열이 늘어선 방향입니다.</param>
    public static Quaternion Rotation_From_Line(Vector3 lineDirection)
    {
        return Quaternion.LookRotation(Facing_From_Line(lineDirection), Vector3.up);
    }
}
