using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Look : MonoBehaviour
{
    [Header("View Settings")]
    public Transform playerCamera;
    public float moveSensitivity = 50f;

    private Vector2 lookInput;
    private float xRotation = 0f;
    private bool canLook = true;

    public bool CanLook => canLook;

    private void Start()
    {
        SetLookEnabled(true);
    }

    public void SetLookEnabled(bool enable)
    {
        canLook = enable;
        lookInput = Vector2.zero;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    public void ToggleLook() => SetLookEnabled(!canLook);

    public void OnLook(InputValue value)
    {
        if (!canLook) return;
        lookInput = value.Get<Vector2>();
    }

    public void MoveSensitivityChanged(float value)
    {
        moveSensitivity = value;
    }

    private void Update()
    {
        if (!canLook) return;

        float mouseX = lookInput.x * moveSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * moveSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}