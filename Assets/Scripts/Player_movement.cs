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

    void Start()
    {
        controller = GetComponent<CharacterController>();
        standHeight = controller.height;
        standCenter = controller.center;

        if (cameraTransform != null)
            cameraStandPos = cameraTransform.localPosition;
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

        UpdateCameraHeight();
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

        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition, target, cameraMoveSpeed * CheatManager.PlayerDelta);
    }
}
