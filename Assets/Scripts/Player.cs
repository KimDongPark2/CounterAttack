using UnityEngine;
using UnityEngine.InputSystem; // 신식 인풋 시스템 필수 네임스페이스

public class SimplePlayerMove : MonoBehaviour
{
    private PlayerInputActions inputActions;
    public float moveSpeed = 5f;

    private void Awake()
    {
        // 1. 에디터에서 만든 인풋 액션 클래스를 메모리에 생성해
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        // 2. 스크립트가 켜질 때 'Player' 액션 맵의 입력을 활성화
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        // 3. 스크립트가 꺼지면 입력을 비활성화 (메모리 누수 방지용)
        inputActions.Player.Disable();
    }

    private void Update()
    {
        // 4. WASD 입력값을 2D 벡터 형태로 읽어오기
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

        // 5. 2D 입력을 3D 방향(X축 좌우, Z축 앞뒤)으로 변환
        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);

        // 6. 초당 moveSpeed만큼 이동하도록 transform 적용
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
    }
}