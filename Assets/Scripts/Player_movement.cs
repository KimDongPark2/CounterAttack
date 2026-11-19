using UnityEngine;
using UnityEngine.InputSystem;

public class Player_movement : MonoBehaviour
{
    private CharacterController controller;
    private Vector2 moveInput;
    private bool isRunning;
    private bool isSitting;
    private bool isLookingUp;

    // 서 있을 때 원래 값 기억해두기
    private float standHeight;
    private Vector3 standCenter;
    private Vector3 cameraStandPos;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float RunningSpeed = 7f;
    public float sitSpeed = 2f;

    [Header("Sit Settings")]
    public float sitHeight = 1f;
    public Transform cameraTransform;    // 1인칭 카메라
    public float cameraSitOffset = 0.5f; // 앉을 때 카메라 내려가는 정도

    [Header("LookUp Settings")]
    public float cameraLookUpOffset = 0.2f; // 까치발 들 때 카메라 올라가는 정도

    [Header("Camera Smooth")]
    public float cameraMoveSpeed = 8f; // 시점 바뀌는 속도 (클수록 빠르게 바뀜)

    [Header("Height Lock")]
    public bool lockHeight = true;          // 시작 높이보다 올라가면 되돌리기
    public float heightTolerance = 0.01f;   // 이 정도 차이는 무시
    public float heightReturnSpeed = 10f;   // 되돌아가는 속도 (클수록 빠르게)

    [Header("Camera Smooth (다이나믹 시점)")]
    [Tooltip("다이나믹 시점일 때 앉기 · 까치발로 카메라 높이가 바뀌는 데 걸리는 시간 (초). 고정 시점은 위의 Camera Move Speed를 그대로 씀")]
    public float dynamicHeightTime = 0.25f;

    private float startY;                   // 게임 시작 때 플레이어 높이

    // 흔들림이 안 들어간 카메라 높이 (CameraFeel이 여기에 흔들림을 더해서 씀)
    private Vector3 cameraBasePos;
    private Vector3 cameraBaseVel;

    // 다른 스크립트(WeaponFeel · CameraFeel)가 읽는 값
    public bool IsRunning => isRunning;
    public bool IsSitting => isSitting;
    public Vector2 MoveInput => moveInput;
    public Vector3 CameraBasePos => cameraBasePos;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        standHeight = controller.height;
        standCenter = controller.center;
        startY = transform.position.y;      // 시작 높이 기록

        if (cameraTransform != null)
        {
            cameraStandPos = cameraTransform.localPosition;
            cameraBasePos = cameraStandPos;
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    // 누르고 있는 동안만 달리기
    public void OnRun(InputValue value)
    {
        isRunning = value.isPressed;
    }

    // 누르고 있는 동안만 까치발
    public void OnLookUp(InputValue value)
    {
        isLookingUp = value.isPressed;
    }

    // 한 번 누르면 앉고, 다시 누르면 일어나기
    public void OnSit(InputValue value)
    {
        if (!value.isPressed) return; // 뗄 때는 무시

        isSitting = !isSitting;

        if (isSitting)
        {
            controller.height = sitHeight;
            // 발 위치는 그대로 두고 키만 줄이기
            controller.center = standCenter - new Vector3(0, (standHeight - sitHeight) / 2f, 0);
        }
        else
        {
            controller.height = standHeight;
            controller.center = standCenter;
        }
    }

    void Update()
    {
        // 상황에 따라 속도 고르기 (앉아 있으면 달리기 무시)
        float speed = moveSpeed;
        if (isSitting) speed = sitSpeed;
        else if (isRunning) speed = RunningSpeed;

        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(moveDirection * speed * CheatManager.PlayerDelta);   // 시간 정지 중에도 나는 움직임

        KeepStartHeight();
        UpdateCameraHeight();
    }

    // 손님/물건에 밀리거나 턱을 밟아서 올라가면 시작 높이로 되돌리기
    // (중력이 없어서 한 번 올라가면 스스로는 안 내려옴)
    void KeepStartHeight()
    {
        if (!lockHeight) return;

        float diff = transform.position.y - startY;
        if (diff <= heightTolerance) return;

        // 부드럽게 내리되, 시작 높이 아래로는 안 내려가도록 diff 만큼만
        // controller.Move를 써서 물체를 뚫고 내려가지는 않음
        float step = Mathf.Min(diff, Mathf.Max(diff * heightReturnSpeed * CheatManager.PlayerDelta, 0.001f));
        controller.Move(Vector3.down * step);
    }

    // 앉기/까치발 상태에 맞춰 카메라 높이를 부드럽게 옮기기
    void UpdateCameraHeight()
    {
        if (cameraTransform == null) return;

        Vector3 target = cameraStandPos;
        if (isSitting)
            target.y -= cameraSitOffset;      // 앉으면 내려가고
        else if (isLookingUp)
            target.y += cameraLookUpOffset;   // 까치발이면 올라감 (앉아 있을 땐 무시)

        // 카메라의 지금 위치 대신 cameraBasePos를 움직임 (CameraFeel이 더한 흔들림이 높이 계산에 섞이지 않게)
        if (ViewModeSettings.Strength > 0f)
        {
            // 다이나믹: 스프링으로 스르륵 (프레임레이트와 상관없이 같은 느낌)
            cameraBasePos = FeelSpring.Step(cameraBasePos, ref cameraBaseVel, target,
                FeelSpring.SpeedFromTime(dynamicHeightTime), CheatManager.PlayerDelta);
        }
        else
        {
            // 고정: 예전과 똑같은 계산
            cameraBasePos = Vector3.Lerp(cameraBasePos, target, cameraMoveSpeed * CheatManager.PlayerDelta);
            cameraBaseVel = Vector3.zero;
        }
        cameraTransform.localPosition = cameraBasePos;
    }
}
