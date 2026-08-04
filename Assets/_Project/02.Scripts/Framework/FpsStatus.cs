using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 좌상단에 현재 프레임 속도와 프레임 시간을 표시하는 디버그 오버레이입니다.
///
/// 이 프로젝트는 9,600명 규모를 목표로 하므로, 편집 중에 프레임이 언제
/// 무너지는지 즉시 보이는 것이 중요합니다. FPS만으로는 체감이 어려워
/// 밀리초(ms)를 함께 표시합니다. 60fps 예산이 16.67ms이므로 그 값과
/// 직접 비교할 수 있습니다.
///
/// 표시 주기를 두는 이유:
/// 매 프레임 갱신하면 숫자가 너무 빨리 바뀌어 읽을 수 없습니다.
/// 일정 간격으로만 새로 계산해 눈이 따라갈 수 있게 합니다.
/// </summary>
public class FpsStatus : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>표시할 글자 크기입니다.</summary>
    [Range(10, 150)]
    public int fontSize = 30;

    /// <summary>표시할 글자 색입니다. 배경과 대비되는 색을 고르십시오.</summary>
    public Color color = new Color(.0f, .0f, .0f, 1.0f);

    /// <summary>표시 위치의 가로 오프셋(픽셀)입니다.</summary>
    public float width;

    /// <summary>표시 위치의 세로 오프셋(픽셀)입니다.</summary>
    public float height;

    // 비공개 멤버 변수
    /// <summary>표시값을 다시 계산하는 간격(초)입니다. 짧으면 숫자가 읽히지 않습니다.</summary>
    float updatetime = 0.3f;

    /// <summary>마지막 갱신 이후 누적된 시간(초)입니다.</summary>
    float time = 0.0f;

    /// <summary>라벨을 그릴 화면 영역입니다.</summary>
    Rect position;

    /// <summary>마지막으로 계산한 초당 프레임 수입니다.</summary>
    float fps;

    /// <summary>마지막으로 계산한 프레임 시간(밀리초)입니다.</summary>
    float ms;

    /// <summary>화면에 그릴 문자열입니다. 갱신 시점에만 새로 만듭니다.</summary>
    string text;

    /// <summary>라벨 스타일입니다. 크기와 색을 갱신 시점에 반영합니다.</summary>
    GUIStyle style = new GUIStyle();

    // Unity 이벤트 함수
    /// <summary>
    /// 프레임 지표를 갱신하고 화면에 그립니다.
    ///
    /// 계산은 updatetime 간격으로만 수행하고, 그리기는 매 프레임 합니다.
    /// 그리기까지 건너뛰면 라벨이 깜박이기 때문입니다.
    /// </summary>
    void OnGUI()
    {
        time += Time.deltaTime;

        if (time > updatetime)
        {
            position = new Rect(width, height, Screen.width, Screen.height);

            fps = 1.0f / Time.deltaTime;
            ms = Time.deltaTime * 1000.0f;
            text = string.Format("{0:N1} FPS ({1:N1}ms)", fps, ms);

            style.fontSize = fontSize;
            style.normal.textColor = color;
            time -= updatetime;
        }

        GUI.Label(position, text, style);
    }
}
