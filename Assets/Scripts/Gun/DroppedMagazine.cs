using UnityEngine;

// 재장전 때 빠져서 날아가는 탄창 (GunReload가 만들어서 붙임)
//   처음 floatTime초 동안은 중력을 약하게 → 화면 안에서 날아가는 게 보이고, 그다음부터 보통 중력으로 떨어짐
//   플레이어 키가 8m라 탄창은 약 7m를 떨어짐 → 빨라서 얇은 바닥을 뚫고 지나갈 수 있음
//   → 혹시 뚫고 내려가도 floorY(플레이어 발 높이) 아래로는 안 내려가고 그 자리에 멈춤
[RequireComponent(typeof(Rigidbody))]
public class DroppedMagazine : MonoBehaviour
{
    [Tooltip("중력을 약하게 하는 시간 (초)")]
    public float floatTime = 0.3f;
    [Tooltip("그동안의 중력 세기 (1 = 보통)")]
    public float floatGravity = 0.15f;
    [Tooltip("이 높이(월드 y) 아래로는 안 떨어짐. 보통 플레이어 발 높이")]
    public float floorY = float.NegativeInfinity;

    private Rigidbody rb;
    private Collider[] cols;
    private float age;
    private bool landed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;   // 중력은 아래에서 직접 줌
    }

    private void FixedUpdate()
    {
        if (landed) return;
        age += Time.fixedDeltaTime;
        float g = age < floatTime ? floatGravity : 1f;
        rb.AddForce(Physics.gravity * g, ForceMode.Acceleration);

        // 바닥(발 높이)보다 아래로 내려갔으면 바닥 위에 올려놓고 멈춤
        if (float.IsNegativeInfinity(floorY)) return;
        if (cols == null) cols = GetComponentsInChildren<Collider>();
        float bottom = float.PositiveInfinity;
        foreach (Collider c in cols) if (c != null && c.enabled) bottom = Mathf.Min(bottom, c.bounds.min.y);
        if (float.IsPositiveInfinity(bottom)) bottom = rb.position.y;
        if (bottom < floorY)
        {
            rb.position += Vector3.up * (floorY - bottom);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;   // 멈춘 물체(Kinematic)는 연속 검사를 못 써서 먼저 바꿈
            rb.isKinematic = true;
            landed = true;
        }
    }
}
