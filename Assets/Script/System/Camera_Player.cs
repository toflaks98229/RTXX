using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using Unity.VisualScripting;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public class Camera_Player : MonoBehaviour
{
    // 공개 멤버 변수
    /// <summary>
    /// URP(Universal Render Pipeline) 렌더러 데이터입니다.
    /// </summary>
    public UniversalRendererData rendererData;
    /// <summary>
    /// 픽셀화 효과에 사용될 렌더러 기능의 이름입니다.
    /// </summary>
    public string featureName;
    /// <summary>
    /// 카메라의 Transform 컴포넌트입니다.
    /// </summary>
    public Transform cameraTransform;
    /// <summary>
    /// 카메라의 이동 속도입니다.
    /// </summary>
    public float moveSpeed = 10.0f;
    /// <summary>
    /// 카메라의 회전 속도입니다.
    /// </summary>
    public float rotatSpeed = 10.0f;
    /// <summary>
    /// 왼쪽 이동 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_MoveLeft = KeyCode.A;
    /// <summary>
    /// 오른쪽 이동 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_MoveRight = KeyCode.D;
    /// <summary>
    /// 전진 이동 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_MoveForward = KeyCode.W;
    /// <summary>
    /// 후진 이동 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_MoveBack = KeyCode.S;
    /// <summary>
    /// 빠르게 이동하는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_MoveFast = KeyCode.LeftShift;
    /// <summary>
    /// 왼쪽으로 회전하는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_RotateLeft = KeyCode.Q;
    /// <summary>
    /// 오른쪽으로 회전하는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_RotateRight = KeyCode.E;
    /// <summary>
    /// 드래그로 카메라를 이동시키는 키 코드입니다.
    /// </summary>
    public KeyCode keyCode_Drag_Move;
    /// <summary>
    /// 마우스 스크롤 민감도입니다.
    /// </summary>
    public float mouseScrollSensitive = 10.0f;
    /// <summary>
    /// 줌 속도입니다.
    /// </summary>
    public float zoomSpeed = 0.1f;
    /// <summary>
    /// 최대 줌 거리입니다.
    /// </summary>
    public float zoomMax = 10.0f;
    /// <summary>
    /// 최소 줌 거리입니다.
    /// </summary>
    public float zoomMin = 10.0f;
    /// <summary>
    /// 카메라 확대/축소 효과에 사용되는 민감도입니다.
    /// </summary>
    [Range(0, 50.0f)]
    public float mouseMoveSensitive;
    /// <summary>
    /// 픽셀화 효과의 비율을 조절하는 변수입니다.
    /// </summary>
    [Range(0, 128.0f)]
    public float pixelizeRate;

    // 비공개 멤버 변수
    /// <summary>
    /// 카메라의 현재 줌 거리입니다.
    /// </summary>
    private float zoomlengthcurrent = 10.0f;
    /// <summary>
    /// 카메라의 목표 줌 거리입니다.
    /// </summary>
    private float zoomlength = 10.0f;
    /// <summary>
    /// 카메라 줌 벡터입니다.
    /// </summary>
    private Vector3 zoomVector;
    /// <summary>
    /// 마우스의 현재 위치입니다.
    /// </summary>
    private Vector2 mousePosition;
    /// <summary>
    /// 화면의 X축 크기입니다.
    /// </summary>
    private int xScreenSize;
    /// <summary>
    /// 화면의 Y축 크기입니다.
    /// </summary>
    private int yScreenSize;
    /// <summary>
    /// 마우스 드래그 시작 위치입니다.
    /// </summary>
    private Vector2 drag_MousePosition;
    /// <summary>
    /// 렌더러 기능 중 픽셀화 기능을 참조합니다.
    /// </summary>
    private ScriptableRendererFeature pixelizeRendererFeature;

    // 이동 상태를 나타내는 플래그 변수들
    private bool bmoveLeft = false;
    private bool bmoveRight = false;
    private bool bmoveForward = false;
    private bool bmoveBack = false;

    // Unity 이벤트 함수
    /// <summary>
    /// MonoBehaviour 인스턴스가 생성될 때 호출됩니다.
    /// </summary>
    private void Awake()
    {
        // Awake() 함수는 기본적으로 제공되므로 비워둡니다.
    }

    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// </summary>
    private void Start()
    {
        // 게임 시작 시 전체 화면 모드를 비활성화합니다.
        Screen.fullScreen = false;
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    private void Update()
    {
        // 마우스의 현재 위치와 화면 크기를 업데이트합니다.
        mousePosition = Input.mousePosition;
        xScreenSize = Screen.width;
        yScreenSize = Screen.height;

        // 드래그 이동 기능
        if (Input.GetKeyDown(keyCode_Drag_Move))
        {
            drag_MousePosition = mousePosition;
        }
        else if (Input.GetKey(keyCode_Drag_Move))
        {
            Vector2 vector2 = mousePosition - drag_MousePosition;

            if (vector2.magnitude > 1)
            {
                // 이 부분은 현재 비어있으며, 드래그 이동 로직을 추가할 수 있습니다.
            }

            drag_MousePosition = mousePosition;
        }

        // 카메라 이동, 회전, 줌 기능을 호출합니다.
        Move();
        Rotate();
        Zoom();

        // 카메라가 플레이어(이 스크립트가 부착된 오브젝트)를 바라보도록 설정합니다.
        cameraTransform.LookAt(transform);
    }

    // 비공개 메서드
    /// <summary>
    /// 키보드 입력 및 마우스 위치에 따라 카메라를 이동시킵니다.
    /// </summary>
    void Move()
    {
        // 이동 플래그들을 초기화합니다.
        bmoveLeft = false;
        bmoveRight = false;
        bmoveForward = false;
        bmoveBack = false;

        // 현재 줌 거리에 따라 이동 속도를 보정합니다.
        float moveSpeed = this.moveSpeed * Time.deltaTime * zoomlengthcurrent * 0.1f;

        // 빠르게 이동하는 키가 눌렸는지 확인합니다.
        if (Input.GetKey(keyCode_MoveFast))
        {
            moveSpeed = moveSpeed * 2.0f;
        }

        // 키보드 입력에 따라 이동 플래그를 설정합니다.
        if (Input.GetKey(keyCode_MoveLeft)) bmoveLeft = true;
        if (Input.GetKey(keyCode_MoveRight)) bmoveRight = true;
        if (Input.GetKey(keyCode_MoveForward)) bmoveForward = true;
        if (Input.GetKey(keyCode_MoveBack)) bmoveBack = true;

        // 화면 경계에 마우스가 위치하면 이동 플래그를 설정합니다.
        if (mousePosition.x < xScreenSize * mouseMoveSensitive * 0.01f) bmoveLeft = true;
        else if (mousePosition.x > xScreenSize * (100.0f - mouseMoveSensitive) * 0.01f) bmoveRight = true;

        if (mousePosition.y > yScreenSize * (100.0f - mouseMoveSensitive) * 0.01f) bmoveForward = true;
        else if (mousePosition.y < yScreenSize * mouseMoveSensitive * 0.01f) bmoveBack = true;

        // 설정된 플래그에 따라 카메라를 이동시킵니다.
        if (bmoveLeft) transform.Translate(Vector3.left * moveSpeed);
        if (bmoveRight) transform.Translate(Vector3.right * moveSpeed);
        if (bmoveForward) transform.Translate(Vector3.forward * moveSpeed);
        if (bmoveBack) transform.Translate(Vector3.back * moveSpeed);
    }

    /// <summary>
    /// 키보드 입력에 따라 카메라를 회전시킵니다.
    /// </summary>
    void Rotate()
    {
        if (Input.GetKey(keyCode_RotateLeft))
        {
            transform.Rotate(Vector3.down * rotatSpeed * Time.deltaTime);
        }
        if (Input.GetKey(keyCode_RotateRight))
        {
            transform.Rotate(Vector3.up * rotatSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 마우스 휠 입력에 따라 카메라를 확대/축소합니다.
    /// </summary>
    void Zoom()
    {
        // 줌 방향 벡터를 계산합니다.
        zoomVector = cameraTransform.position - transform.position;
        zoomVector = Vector3.Normalize(zoomVector);

        // 마우스 휠 입력을 가져옵니다.
        float wheelInput = Input.GetAxis("Mouse ScrollWheel");

        // 마우스 휠 방향에 따라 목표 줌 거리를 설정합니다.
        if (wheelInput > 0)
        {
            zoomlength = zoomlength - mouseScrollSensitive;
            if (zoomlength < zoomMin) zoomlength = zoomMin;
        }
        else if (wheelInput < 0)
        {
            zoomlength = zoomlength + mouseScrollSensitive;
            if (zoomlength > zoomMax) zoomlength = zoomMax;
        }

        // 현재 줌 거리를 목표 줌 거리로 부드럽게 보간합니다.
        zoomlengthcurrent = Mathf.Lerp(zoomlengthcurrent, zoomlength, Time.deltaTime * zoomSpeed);
        zoomlengthcurrent = Mathf.Floor(zoomlengthcurrent * 100f) / 100f;

        // 모든 카메라의 Orthographic 및 Field of View 크기를 줌 거리에 따라 업데이트합니다.
        foreach (Camera camera in Camera.allCameras)
        {
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature.name == featureName)
                {
                    // PixelizeFeature에 대한 픽셀화 비율을 줌 거리에 따라 설정합니다.
                    PixelizeFeature pixelizeFeature = (PixelizeFeature)feature;
                    int pixelizeint = (int)(pixelizeRate * zoomlengthcurrent);
                    //pixelizeFeature.settings.screenHeight = pixelizeint;
                }
            }

            camera.orthographicSize = zoomlengthcurrent;
            camera.fieldOfView = zoomlengthcurrent;
        }
    }
}
