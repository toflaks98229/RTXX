using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좌하단에 전장 전체를 축소해 보여 주는 미니맵입니다.
///
/// 왜 이것이 가장 먼저인가:
/// 이 게임은 측후방 공격이 방어력과 방패를 무력화합니다.
/// (Unit_Fight_Job.GetDamage_Melee의 각도 판정)
/// 즉 우회가 가장 강력한 전술인데, 지금까지 플레이어는 **우회당하는 것을
/// 볼 방법이 전혀 없었습니다.** 카메라 밖에서 기병이 돌아 들어와도
/// 부대가 무너지고 나서야 압니다.
///
/// 미니맵이 없으면 이 게임의 핵심 전술이 한쪽으로만 작동합니다.
/// AI는 Flank_To로 우회를 쓰는데 플레이어는 당하기만 합니다.
///
/// 구현 방식:
/// IMGUI로 점을 찍습니다. 부대는 많아야 수십 개이므로 비용이 없습니다.
/// (유닛 9,600개를 찍으면 안 됩니다. 부대 단위여야 합니다)
/// </summary>
public class UI_Minimap : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>부대 목록을 읽어 올 컨트롤러입니다. 비워 두면 씬에서 찾습니다.</summary>
    [Header("연결")]
    [Tooltip("비워 두면 씬에서 자동으로 찾습니다.")]
    public Controller controller;

    /// <summary>미니맵 한 변의 크기(픽셀)입니다.</summary>
    [Header("표시")]
    [Tooltip("미니맵 한 변의 크기입니다.")]
    public float mapSize = 190.0f;

    /// <summary>화면 가장자리와의 여백(픽셀)입니다.</summary>
    [Tooltip("화면 가장자리와의 여백입니다.")]
    public float margin = 10.0f;

    /// <summary>
    /// 카드 바에 가리지 않도록 위로 띄우는 높이(픽셀)입니다.
    ///
    /// 카드 바가 화면 하단을 쓰므로, 미니맵을 그 위에 올려야
    /// 둘이 겹치지 않습니다.
    ///
    /// 기본값 106은 카드 바의 실제 높이에서 계산한 값입니다.
    ///   UI_Command_Bar: bottomMargin(10) + cardHeight(74) + padding(8*2) = 100
    ///   여기에 여백 6을 더합니다.
    /// 카드 크기를 바꾸면 이 값과 UI_Army_Group.bottomOffset을 함께
    /// 옮겨야 합니다. (그룹 바가 미니맵 위에 얹혀 있습니다)
    /// </summary>
    [Tooltip("카드 바를 피해 위로 띄우는 높이입니다. 카드 크기를 바꾸면 함께 조정하십시오.")]
    public float bottomOffset = 106.0f;

    /// <summary>부대 점의 기본 크기(픽셀)입니다. 인원에 따라 커집니다.</summary>
    [Tooltip("부대 점의 기본 크기입니다.")]
    public float dotSize = 5.0f;

    /// <summary>클릭으로 카메라를 옮길지 여부입니다.</summary>
    [Tooltip("미니맵을 클릭하면 그 지점으로 카메라를 옮깁니다.")]
    public bool bclickToMove = true;

    // 비공개 멤버 변수
    /// <summary>패널 바탕색입니다. 카드 바와 같은 톤을 씁니다.</summary>
    private static readonly Color color_Panel = new Color(0.07f, 0.06f, 0.05f, 0.88f);

    /// <summary>패널 테두리색입니다.</summary>
    private static readonly Color color_Edge = new Color(0.55f, 0.45f, 0.25f, 0.85f);

    /// <summary>아군 부대 색입니다.</summary>
    private static readonly Color color_Player = new Color(0.45f, 0.70f, 1.00f, 1.0f);

    /// <summary>적군 부대 색입니다.</summary>
    private static readonly Color color_Enemy = new Color(1.00f, 0.45f, 0.40f, 1.0f);

    /// <summary>선택된 부대를 강조하는 색입니다.</summary>
    private static readonly Color color_Selected = new Color(0.98f, 0.90f, 0.50f, 1.0f);

    /// <summary>카메라 시야를 그리는 색입니다.</summary>
    private static readonly Color color_View = new Color(1.0f, 1.0f, 1.0f, 0.35f);

    /// <summary>사각형을 채우는 데 쓰는 1x1 텍스처입니다.</summary>
    private Texture2D pixel;

    /// <summary>
    /// 전장 경계입니다. XZ 평면의 사각형이며 (min, max)를 담습니다.
    ///
    /// 매 프레임 다시 구하지 않는 이유:
    /// 부대가 움직이면 경계도 조금씩 달라지는데, 그때마다 다시 잡으면
    /// 미니맵의 축척이 계속 변해 점들이 흔들려 보입니다.
    /// 한 번 잡은 경계를 유지하되, 부대가 그 밖으로 나가면 넓힙니다.
    /// </summary>
    private Bounds worldBounds;

    /// <summary>경계를 한 번이라도 잡았는지 여부입니다.</summary>
    private bool bboundsValid;

    // Unity 이벤트 함수
    /// <summary>사각형 채우기에 쓸 1x1 텍스처를 만듭니다.</summary>
    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    /// <summary>동적으로 만든 텍스처를 반납합니다.</summary>
    private void OnDestroy()
    {
        if (pixel != null) Destroy(pixel);
    }

    /// <summary>비어 있는 참조를 씬에서 찾아 채웁니다.</summary>
    private void Start()
    {
        if (controller == null) controller = FindAnyObjectByType<Controller>();
    }

    /// <summary>미니맵을 그리고 클릭 입력을 처리합니다.</summary>
    private void OnGUI()
    {
        List<Army> armies = controller != null ? controller.armies : Army.allArmies;
        if (armies == null || armies.Count == 0) return;

        Update_Bounds(armies);
        if (!bboundsValid) return;

        float x = margin;
        float y = Screen.height - mapSize - bottomOffset;

        Rect panel = new Rect(x, y, mapSize, mapSize);

        Draw_Rect(panel, color_Panel);
        Draw_Border(panel, color_Edge, 1.0f);

        // 미니맵 클릭은 카메라 이동입니다. 전장으로 넘기면 그 지점에
        // 부대 선택이나 이동 명령이 함께 들어갑니다.
        UI_Input_Guard.Register(panel);

        // 카메라 시야를 먼저 그립니다.
        // 부대 점보다 뒤에 있어야 점이 가려지지 않습니다.
        Draw_View_Cone(panel);

        Draw_Armies(panel, armies);

        if (bclickToMove) Handle_Click(panel);
    }

    // 비공개 메서드
    /// <summary>
    /// 전장 경계를 갱신합니다.
    ///
    /// 부대가 기존 경계 밖으로 나가면 그만큼 넓힙니다. 줄이지는 않습니다.
    /// 줄이면 부대가 움직일 때마다 축척이 변해 미니맵이 출렁입니다.
    /// </summary>
    /// <param name="armies">경계를 잡을 부대 목록입니다.</param>
    private void Update_Bounds(List<Army> armies)
    {
        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;

            Vector3 p = army.GetPosition();

            if (!bboundsValid)
            {
                worldBounds = new Bounds(p, Vector3.zero);
                bboundsValid = true;
                continue;
            }

            worldBounds.Encapsulate(p);
        }

        if (!bboundsValid) return;

        // 가장자리 부대가 테두리에 딱 붙지 않도록 여유를 둡니다.
        Vector3 size = worldBounds.size;

        if (size.x < 1.0f || size.z < 1.0f)
        {
            // 부대가 한 점에 몰려 있으면 축척을 정할 수 없습니다.
            worldBounds.Expand(new Vector3(20.0f, 0.0f, 20.0f));
        }
    }

    /// <summary>
    /// 월드 좌표를 미니맵 화면 좌표로 옮깁니다.
    ///
    /// 가로세로 축척을 따로 쓰지 않고 '긴 쪽'에 맞춥니다.
    /// 따로 쓰면 전장이 길쭉할 때 미니맵에서 찌그러져 보여
    /// 실제 거리감이 왜곡됩니다.
    /// </summary>
    /// <param name="panel">미니맵 화면 영역입니다.</param>
    /// <param name="world">변환할 월드 좌표입니다.</param>
    /// <returns>미니맵 안의 화면 좌표입니다.</returns>
    private Vector2 World_To_Map(Rect panel, Vector3 world)
    {
        Vector3 min = worldBounds.min;
        Vector3 size = worldBounds.size;

        float span = Mathf.Max(size.x, size.z);
        if (span < 0.001f) span = 1.0f;

        // 정사각 미니맵에 맞추고 가운데 정렬합니다.
        float inner = panel.width - 8.0f;
        float scale = inner / span;

        float offsetX = (inner - size.x * scale) * 0.5f;
        float offsetZ = (inner - size.z * scale) * 0.5f;

        float mx = panel.x + 4.0f + offsetX + (world.x - min.x) * scale;

        // Z는 화면 Y와 방향이 반대입니다.
        // 북쪽(+Z)이 미니맵 위로 오게 뒤집습니다.
        float my = panel.y + 4.0f + offsetZ
                 + (size.z - (world.z - min.z)) * scale;

        return new Vector2(mx, my);
    }

    /// <summary>부대들을 점으로 찍습니다.</summary>
    /// <param name="panel">미니맵 화면 영역입니다.</param>
    /// <param name="armies">그릴 부대 목록입니다.</param>
    private void Draw_Armies(Rect panel, List<Army> armies)
    {
        for (int i = 0; i < armies.Count; i++)
        {
            Army army = armies[i];
            if (army == null) continue;
            if (army.units.Count == 0) continue;

            ref Army_Data data = ref army.army_Data;

            Vector2 p = World_To_Map(panel, army.GetPosition());

            // 점 크기는 인원 비율을 따릅니다.
            // 큰 부대와 거의 전멸한 부대가 같은 크기면 전황이 왜곡됩니다.
            float rate = data.unit_Num_Max > 0
                ? (float)data.unit_Num / data.unit_Num_Max
                : 1.0f;

            float size = dotSize * (0.6f + rate * 0.7f);

            Color color = data.bplayer ? color_Player : color_Enemy;

            // 무너진 부대는 흐리게 그립니다.
            // 아직 전장에 있지만 전력이 아니라는 것을 구분해야
            // 플레이어가 남은 전력을 잘못 세지 않습니다.
            if (data.IsBroken()) color.a = 0.40f;

            // 선택된 부대는 테두리를 둘러 눈에 띄게 합니다.
            if (army.IsSelected())
            {
                Draw_Rect(new Rect(p.x - size * 0.5f - 1.0f, p.y - size * 0.5f - 1.0f,
                                   size + 2.0f, size + 2.0f), color_Selected);
            }

            Draw_Rect(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), color);

            // 장군은 위에 작은 표식을 답니다.
            // 장군이 죽으면 전군이 흔들리므로 위치를 알아야 합니다.
            if (data.bgeneral)
            {
                Draw_Rect(new Rect(p.x - 1.0f, p.y - size * 0.5f - 3.0f, 2.0f, 2.0f),
                          color_Selected);
            }
        }
    }

    /// <summary>
    /// 카메라가 보고 있는 범위를 사각형으로 그립니다.
    ///
    /// 정확한 절두체가 아니라 근사입니다. 직교 카메라이므로
    /// orthographicSize로 대략의 범위를 구할 수 있고,
    /// 미니맵에서는 '지금 어디를 보고 있는가'만 알면 충분합니다.
    /// </summary>
    /// <param name="panel">미니맵 화면 영역입니다.</param>
    private void Draw_View_Cone(Rect panel)
    {
        Camera cam = Main_Camera.Get();
        if (cam == null) return;

        // 카메라가 내려다보는 지점을 지면(y=0) 평면과의 교점으로 구합니다.
        Transform t = cam.transform;

        Vector3 forward = t.forward;
        if (Mathf.Abs(forward.y) < 0.001f) return;

        float distance = -t.position.y / forward.y;
        if (distance <= 0.0f) return;

        Vector3 center = t.position + forward * distance;

        // 직교 카메라의 보이는 범위입니다.
        float halfHeight = cam.orthographic ? cam.orthographicSize : 20.0f;
        float halfWidth = halfHeight * cam.aspect;

        Vector2 c = World_To_Map(panel, center);

        // 월드 크기를 미니맵 축척으로 환산합니다.
        float span = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
        if (span < 0.001f) return;

        float scale = (panel.width - 8.0f) / span;

        float w = halfWidth * 2.0f * scale;
        float h = halfHeight * 2.0f * scale;

        Rect view = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);

        Draw_Border(view, color_View, 1.0f);
    }

    /// <summary>
    /// 미니맵 클릭을 처리해 카메라를 그 지점으로 옮깁니다.
    ///
    /// 입력을 소비(Use)하는 이유:
    /// 소비하지 않으면 같은 클릭이 전장 드래그 선택으로도 전달되어,
    /// 미니맵을 누를 때마다 부대 선택이 풀립니다.
    /// </summary>
    /// <param name="panel">미니맵 화면 영역입니다.</param>
    private void Handle_Click(Rect panel)
    {
        Event e = Event.current;

        if (e.type != EventType.MouseDown) return;
        if (!panel.Contains(e.mousePosition)) return;

        Vector3 world = Map_To_World(panel, e.mousePosition);

        Camera_Player player = FindAnyObjectByType<Camera_Player>();
        if (player != null) player.Move_To(world);

        e.Use();
    }

    /// <summary>미니맵 화면 좌표를 월드 좌표로 되돌립니다.</summary>
    /// <param name="panel">미니맵 화면 영역입니다.</param>
    /// <param name="screen">변환할 화면 좌표입니다.</param>
    /// <returns>지면(y=0) 위의 월드 좌표입니다.</returns>
    private Vector3 Map_To_World(Rect panel, Vector2 screen)
    {
        Vector3 min = worldBounds.min;
        Vector3 size = worldBounds.size;

        float span = Mathf.Max(size.x, size.z);
        if (span < 0.001f) span = 1.0f;

        float inner = panel.width - 8.0f;
        float scale = inner / span;

        float offsetX = (inner - size.x * scale) * 0.5f;
        float offsetZ = (inner - size.z * scale) * 0.5f;

        float wx = (screen.x - panel.x - 4.0f - offsetX) / scale + min.x;
        float wz = size.z - (screen.y - panel.y - 4.0f - offsetZ) / scale + min.z;

        return new Vector3(wx, 0.0f, wz);
    }

    /// <summary>단색 사각형을 채웁니다.</summary>
    /// <param name="rect">채울 영역입니다.</param>
    /// <param name="color">채울 색입니다.</param>
    private void Draw_Rect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(rect, pixel);

        GUI.color = previous;
    }

    /// <summary>사각형의 네 변에 테두리를 그립니다.</summary>
    /// <param name="rect">테두리를 두를 영역입니다.</param>
    /// <param name="color">테두리 색입니다.</param>
    /// <param name="thickness">테두리 두께(픽셀)입니다.</param>
    private void Draw_Border(Rect rect, Color color, float thickness)
    {
        Draw_Rect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Draw_Rect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Draw_Rect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
