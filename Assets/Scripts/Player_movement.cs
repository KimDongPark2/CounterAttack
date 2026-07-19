using UnityEngine;
using UnityEngine.InputSystem; // 신식 인풋 시스템 필수 네임스페이스

public class Player_movement : MonoBehaviour
{
    private CharacterController controller;
    private Vector2 moveInput;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    void Update()
    {
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }
}