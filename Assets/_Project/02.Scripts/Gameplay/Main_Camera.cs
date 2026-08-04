using UnityEngine;

/// <summary>
/// 메인 카메라를 캐시해 두는 정적 접근점입니다.
///
/// 배경:
/// Camera.main은 내부적으로 "MainCamera" 태그를 가진 오브젝트를 찾는 동작입니다.
/// 이 프로젝트는 Army._Update_Unit에서 '부대 수 x 매 FixedUpdate'로 호출하고 있어
/// 부대가 늘어날수록 순수한 낭비가 커집니다.
///
/// 사용 시 주의:
/// 씬을 다시 로드하면 캐시가 파괴된 카메라를 가리키므로 Clear()를 호출해야 합니다.
/// Controller.Awake에서 호출하고 있습니다.
/// </summary>
public static class Main_Camera
{
    /// <summary>캐시된 메인 카메라입니다. 씬을 다시 로드하면 무효가 됩니다.</summary>
    private static Camera cached;
    /// <summary>캐시된 카메라의 Transform입니다.</summary>
    private static Transform cachedTransform;

    /// <summary>메인 카메라를 반환합니다. 없으면 null입니다.</summary>
    public static Camera Get()
    {
        // Unity의 == 연산자 오버로드 덕분에 파괴된 객체도 null로 판정됩니다.
        if (cached == null)
        {
            cached = Camera.main;
            cachedTransform = cached != null ? cached.transform : null;
        }

        return cached;
    }

    /// <summary>메인 카메라의 Transform을 반환합니다. 없으면 null입니다.</summary>
    public static Transform GetTransform()
    {
        if (cached == null) Get();
        return cachedTransform;
    }

    /// <summary>캐시를 비웁니다. 씬 시작 시 호출해야 합니다.</summary>
    public static void Clear()
    {
        cached = null;
        cachedTransform = null;
    }
}
