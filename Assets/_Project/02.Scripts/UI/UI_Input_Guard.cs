using UnityEngine;

/// <summary>
/// HUD 위에서 일어난 마우스 입력이 전장으로 새어 나가는 것을 막는 계층입니다.
///
/// 왜 필요한가:
/// 이 프로젝트의 HUD는 IMGUI(OnGUI)로 그리고, 전장 조작은 Controller가
/// Input.GetKeyDown으로 직접 읽습니다. **이 둘은 서로를 모릅니다.**
///
/// IMGUI의 Event.current.Use()는 그 이벤트를 IMGUI 안에서 소비할 뿐,
/// Input 클래스에는 아무 영향이 없습니다. 그래서 카드나 버튼을 눌러도
/// 같은 프레임에 Controller가 "빈 땅을 클릭했다"고 판단해 선택을
/// 풀어 버립니다.
///
/// 실제로 보고된 두 증상이 모두 이것입니다.
///   - 부대 카드를 클릭해도 선택되지 않음 (선택 직후 해제됨)
///   - 태세 버튼을 클릭하면 부대 선택이 취소됨
///
/// 해결 방법:
/// HUD가 그려질 때 '지금 마우스가 내 영역 위에 있다'고 등록해 두고,
/// Controller는 조작을 처리하기 전에 그것을 확인합니다.
///
/// ---------------------------------------------------------------------
/// 실행 순서 주의 — 한 프레임 늦게 반영됩니다
/// ---------------------------------------------------------------------
/// 유니티는 Update를 먼저 돌리고 OnGUI를 나중에 돌립니다. 그래서
/// OnGUI에서 등록한 값을 같은 프레임의 Update가 볼 수 없습니다.
///
/// 그런데 HUD 패널의 위치는 매 프레임 같은 식으로 계산되므로, 직전
/// 프레임에 등록된 영역을 그대로 써도 사실상 문제가 없습니다.
/// 마우스가 1프레임 만에 패널 밖으로 나갈 수는 있지만, 그 경우
/// 전장 클릭 한 번이 무시될 뿐 잘못된 선택이 일어나지는 않습니다.
///
/// (반대로 놓치면 '카드를 눌렀는데 선택이 풀리는' 증상이 남습니다.
///  둘 중에서는 이쪽이 훨씬 낫습니다)
///
/// 그래서 등록 값을 **다음 프레임까지 유지**합니다. Time.frameCount로
/// 두 프레임 이상 지난 것만 지웁니다.
/// </summary>
public static class UI_Input_Guard
{
    /// <summary>마지막으로 등록된 프레임 번호입니다.</summary>
    private static int frame = -1;

    /// <summary>이번 프레임에 마우스가 HUD 위에 있었는지 여부입니다.</summary>
    private static bool bover;

    // 공개 메서드
    /// <summary>
    /// 마우스가 이 영역 위에 있으면 HUD 점유로 등록합니다.
    ///
    /// OnGUI 안에서 패널을 그린 직후에 부르십시오.
    /// </summary>
    /// <param name="rect">검사할 화면 영역입니다.</param>
    /// <returns>마우스가 영역 안에 있으면 true입니다.</returns>
    public static bool Register(Rect rect)
    {
        Event e = Event.current;
        if (e == null) return false;

        if (!rect.Contains(e.mousePosition)) return false;

        Mark();
        return true;
    }

    /// <summary>
    /// 영역과 무관하게 이번 프레임을 HUD 점유로 등록합니다.
    ///
    /// 모달처럼 화면 전체를 덮는 표시에 씁니다.
    /// </summary>
    public static void Mark()
    {
        frame = Time.frameCount;
        bover = true;
    }

    /// <summary>
    /// 지금 마우스가 HUD 위에 있는지 확인합니다.
    ///
    /// Controller가 클릭을 처리하기 전에 이 값을 봅니다.
    /// true이면 그 입력은 HUD의 것이므로 전장 조작을 하지 않습니다.
    /// </summary>
    /// <returns>HUD가 마우스를 점유하고 있으면 true입니다.</returns>
    public static bool IsOverUI()
    {
        Sync_Frame();
        return bover;
    }

    // 비공개 메서드
    /// <summary>
    /// 등록이 너무 오래되었으면 점유 상태를 지웁니다.
    ///
    /// 한 프레임은 남겨 둡니다. OnGUI(등록)가 Update(확인)보다 뒤에
    /// 실행되므로, 직전 프레임 값을 봐야 같은 클릭을 막을 수 있습니다.
    /// 자세한 이유는 클래스 요약을 보십시오.
    /// </summary>
    private static void Sync_Frame()
    {
        // 2프레임 이상 지났으면 HUD에서 벗어난 것으로 봅니다.
        if (Time.frameCount - frame <= 1) return;

        bover = false;
    }
}
