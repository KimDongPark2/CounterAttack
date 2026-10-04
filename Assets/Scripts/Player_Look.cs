using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Look : MonoBehaviour
{
    [Header("View Settings")]
    public Transform playerCamera;
    [Range(1f, 100f)]
    public float moveSensitivity = 50f; // 슬라이더용 1~100 값

    [Tooltip("1~100 감도를 실제 회전량으로 바꾸는 배율. 전체적으로 너무 빠르거나 느리면 이것만 조절")]
    public float sensitivityScale = 0.01f;

    [SerializeField] private PlayerInput playerInput; // 비워두면 같은 오브젝트에서 찾음

    private InputAction lookAction;
    private float xRotation = 0f;
    private bool canLook = true;

    public bool CanLook => canLook;

    private void Start()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        lookAction = playerInput.actions["Look"];
        SetLookEnabled(true);
    }

    public void SetLookEnabled(bool enable)
    {
        canLook = enable;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    public void ToggleLook() => SetLookEnabled(!canLook);

    public void MoveSensitivityChanged(float value)
    {
        moveSensitivity = value;
    }

    private void Update()
    {
        if (!canLook) return;

        // 이번 프레임 동안 마우스가 움직인 양을 직접 읽기
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        // 마우스 delta는 이미 프레임당 값이라 Time.deltaTime을 곱하지 않음
        float sensitivity = moveSensitivity * sensitivityScale; // 50 → 0.5
        float mouseX = lookInput.x * sensitivity;
        float mouseY = lookInput.y * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}