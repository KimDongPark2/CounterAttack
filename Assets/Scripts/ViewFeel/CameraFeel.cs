using UnityEngine;

// 카메라의 FPS 느낌 (다이나믹 시점일 때만)
//   걸음 흔들림 : WeaponFeel의 걸음 흔들림을 아주 약하게(30%) 같이
//   반동        : 쏠 때 위로 튀었다가 천천히 돌아옴 (좌우는 랜덤으로 아주 조금)
//   화면 진동   : 쏠 때 짧고 작게 흔들림
//
// Player_Look의 시점 값(xRotation)은 절대 안 바꿈
// → "Player_Look이 정한 회전 × 반동 회전"을 LateUpdate에서 씀. 다음 프레임에 Player_Look이 다시 원래 회전을 쓰니까 쌓이지 않고,
//   반동이 0이 되면 원래 조준점으로 정확히 돌아옴
// 위치도 마찬가지로 "Player_movement가 정한 높이 + 흔들림"
// 고정 시점이면 아무것도 안 씀 → 예전과 똑같음
//
// Main Camera에 붙이기 (안 붙여도 ViewFeelAutoSetup이 자동으로 붙여 줌). 켜고 끄는 건 WeaponFeel을 따라감
[DefaultExecutionOrder(100)]   // WeaponFeel(50) 다음에
[DisallowMultipleComponent]
public class CameraFeel : MonoBehaviour
{
    [Header("걸음 흔들림")]
    [Tooltip("총의 걸음 흔들림 중 몇 %를 카메라에도 줄지 (0.3 = 30%)")]
    [Range(0f, 1f)] public float bobRatio = 0.3f;

    [Header("반동 Recoil (카메라)")]
    [Tooltip("쏠 때 화면이 위로 튀는 각도 (도)")]
    public float recoilUp = 0.8f;
    [Tooltip("좌우로 랜덤하게 튀는 최대 각도 (도)")]
    public float recoilSide = 0f;
    [Tooltip("튀어 오르는 데 걸리는 시간 (초)")]
    public float recoilRiseTime = 0.04f;
    [Tooltip("원래 조준점으로 돌아오는 데 걸리는 시간 (초)")]
    public float recoilReturnTime = 0.25f;

    [Header("화면 진동 Shake")]
    [Tooltip("흔들리는 최대 각도 (도)")]
    public float shakeAngle = 0.4f;
    [Tooltip("흔들리는 시간 (초)")]
    public float shakeTime = 0.06f;
    [Tooltip("1초에 몇 번 흔들리는지")]
    public float shakeFrequency = 30f;

    [Header("연결 (비우면 자동으로 찾음)")]
    public Player_Look look;
    public Player_movement movement;
    public WeaponFeel weapon;

    private Transform cam;
    private FeelKick recoil;
    private float shakeTimer, shakeClock, shakeSeed;
    private float shakeAmp, shakeDur = 0.08f;   // 이번 진동의 세기 · 길이 (쏘기 · 재장전마다 다름)
    private bool wrote;   // 지난 프레임에 카메라를 건드렸는지

    private void Awake()
    {
        if (look == null) look = GetComponentInParent<Player_Look>();
        if (movement == null) movement = GetComponentInParent<Player_movement>();
        if (weapon == null) weapon = GetComponentInParent<WeaponFeel>();
        if (weapon == null && look != null) weapon = look.GetComponentInChildren<WeaponFeel>(true);
        cam = look != null && look.playerCamera != null ? look.playerCamera : transform;
    }

    private void Start()
    {
        // ViewFeelAutoSetup이 WeaponFeel을 나중에 붙였을 수도 있어서 한 번 더 찾기
        if (weapon == null && look != null) weapon = look.GetComponentInChildren<WeaponFeel>(true);
        if (look == null || weapon == null)
        {
            Debug.LogWarning("[CameraFeel] Player_Look 또는 WeaponFeel을 못 찾아서 카메라 흔들림을 끕니다.", this);
            enabled = false;
        }
    }

    private void OnEnable() => Player_Interaction.OnGunFired += HandleGunFired;

    private void OnDisable()
    {
        Player_Interaction.OnGunFired -= HandleGunFired;
        ResetEffects();
        if (wrote) WriteBase();   // 꺼질 때 카메라를 원래대로
    }

    private void HandleGunFired()
    {
        if (weapon == null || weapon.Blend <= 0f) return;   // 고정 시점이면 아무것도 안 함
        float side = Random.Range(-recoilSide, recoilSide);
        recoil.Add(new Vector3(-recoilUp, side, 0f), new Vector3(recoilUp * 3f, recoilSide * 3f, 0f));
        StartShake(shakeAngle, shakeTime);
    }

    // 다른 스크립트(GunReload)가 카메라를 툭 칠 때. euler = 튀는 각도 (x가 음수면 위로)
    // 고정 시점이면 아무것도 안 함 (카메라는 절대 안 움직임)
    public void Punch(Vector3 euler, float shake = 0f, float shakeDuration = 0f)
    {
        if (weapon == null || weapon.Blend <= 0f) return;
        Vector3 limit = new Vector3(recoilUp * 3f + Mathf.Abs(euler.x), recoilSide * 3f + Mathf.Abs(euler.y), Mathf.Abs(euler.z));
        recoil.Add(euler, limit);
        if (shake > 0f && shakeDuration > 0f) StartShake(shake, shakeDuration);
    }

    private void StartShake(float angle, float duration)
    {
        shakeAmp = angle;
        shakeDur = Mathf.Max(duration, 0.001f);
        shakeTimer = shakeDur;
        shakeClock = 0f;
        shakeSeed = Random.value * 100f;
    }

    private void LateUpdate()
    {
        if (look == null || weapon == null || cam == null) return;
        float dt = CheatManager.PlayerDelta;   // 시간 정지 중에도 흐르는 시간
        float blend = weapon.Blend;

        // 효과가 완전히 꺼져 있으면 카메라를 안 건드림 (한 번만 원래 값으로 돌려놓고 끝)
        if (blend <= 0f)
        {
            ResetEffects();
            if (wrote) WriteBase();
            return;
        }

        // 반동
        recoil.Step(recoilRiseTime, recoilReturnTime, dt);

        // 화면 진동 (부드러운 랜덤 = 펄린 노이즈, 점점 약해짐)
        Vector3 shake = Vector3.zero;
        if (shakeTimer > 0f && dt > 0f)
        {
            shakeTimer -= dt;
            shakeClock += dt;
            float fade = Mathf.Clamp01(shakeTimer / shakeDur);
            float t = shakeClock * shakeFrequency;
            shake = new Vector3(Noise(t, shakeSeed), Noise(t, shakeSeed + 17f), Noise(t, shakeSeed + 31f) * 0.5f) * (shakeAmp * fade);
        }

        Vector3 offsetRot = (recoil.Value + shake) * blend;
        Vector3 offsetPos = weapon.BobOffset * (bobRatio * blend);

        cam.localRotation = look.BaseCameraRotation * Quaternion.Euler(offsetRot);
        if (movement != null && movement.cameraTransform == cam)
            cam.localPosition = movement.CameraBasePos + offsetPos;
        wrote = true;
    }

    // Player_Look · Player_movement가 정한 원래 회전 · 높이로
    private void WriteBase()
    {
        wrote = false;
        if (cam == null || look == null) return;
        cam.localRotation = look.BaseCameraRotation;
        if (movement != null && movement.cameraTransform == cam)
            cam.localPosition = movement.CameraBasePos;
    }

    private void ResetEffects()
    {
        recoil.Reset();
        shakeTimer = 0f;
    }

    // -1 ~ 1 사이의 부드러운 랜덤 값
    private static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;
}
