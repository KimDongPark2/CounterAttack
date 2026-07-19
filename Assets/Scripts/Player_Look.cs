using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Look : MonoBehaviour
{
    [Header("View Settings")]
    public Transform playerCamera;
    public float moveSensitivity = 50f;

    private Vector2 lookInput;
    private float xRotation = 0f;


    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }
    public void MoveSensitivityChanged(float value)
    {
        moveSensitivity = value;
    }
    private void Update()
    {
        float mouseX = lookInput.x * moveSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * moveSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}
