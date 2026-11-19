using ConvenienceStore.Tablet;
using UnityEngine;

// 손에 든 총 · 스캐너의 FPS 느낌 (다이나믹 시점일 때만)
//   1) 흔들림 Sway      : 마우스를 돌리면 총이 살짝 늦게 따라오고 기울어짐
//   2) 걸음 흔들림 Bob  : 걸으면 8자로 출렁, 달리면 더 크고 빠르게
//   3) 달리기 자세      : 달리는 동안 총을 내리고 비스듬히 돌림
//   4) 옆걸음 기울기    : A · D로 움직이면 그쪽으로 살짝 기울어짐
//   (쏠 때 총 반동은 GunMotion으로 옮김 → 고정 시점에서도 총은 반동함)
// 계산대 스캐너는 1) 2)만 약하게. 카메라 쪽 효과는 CameraFeel이 담당
//
// 총 · 스캐너를 직접 움직이면 꺼내기 애니메이션(Animator)과 싸우게 됨
// → 게임이 시작되면 카메라 아래에 빈 오브젝트 "WeaponFeelPivot"을 만들고 총 · 스캐너를 그 안에 넣은 뒤 피벗만 움직임
//   (실행 중에만 옮기는 거라 씬 파일은 안 바뀜)
// 고정 시점이면 피벗은 위치 (0,0,0) · 회전 없음 그대로 → 예전과 똑같음
//
// Player(Player_Interaction이 붙은 오브젝트)에 붙이기
// 안 붙여도 ViewFeelAutoSetup이 실행할 때 자동으로 붙여 줌 (값을 저장하고 싶으면 Tools 메뉴로 미리 붙이기)
[DefaultExecutionOrder(50)]   // Player_Look · Player_movement(0) 다음, CameraFeel(100) 전에
[DisallowMultipleComponent]
public class WeaponFeel : MonoBehaviour
{
    public const string PivotName = "WeaponFeelPivot";

    [Header("1) 흔들림 Sway (마우스를 돌릴 때)")]
    [Tooltip("총이 옆 · 위아래로 밀려나는 최대 거리 (m)")]
    public float swayPosition = 0.02f;
    [Tooltip("총이 돌아가고 기울어지는 최대 각도 (도)")]
    public float swayRotation = 4f;
    [Tooltip("시점을 1초에 이 각도(도)만큼 돌리면 최대로 흔들림. 작을수록 조금만 돌려도 크게 흔들림")]
    public float swayFullTurnSpeed = 180f;
    [Tooltip("총이 따라오는 속도. 작을수록 더 늦게(무겁게) 따라옴")]
    public float swaySpeed = 8f;

    [Header("2) 걸음 흔들림 Bob")]
    [Tooltip("걸을 때 흔들리는 크기 (m)")]
    public float walkBobAmount = 0.012f;
    [Tooltip("걸을 때 흔들리는 빠르기")]
    public float walkBobSpeed = 8f;
    [Tooltip("달릴 때 흔들리는 크기 (m)")]
    public float runBobAmount = 0.03f;
    [Tooltip("달릴 때 흔들리는 빠르기")]
    public float runBobSpeed = 13f;
    [Tooltip("위아래 흔들림 크기 (좌우 흔들림의 몇 배인지). 8자 모양을 만듦")]
    public float bobVerticalRatio = 0.6f;

    [Header("3) 달리기 자세")]
    [Tooltip("달릴 때 총이 움직이는 위치 (m). y가 음수면 아래로")]
    public Vector3 sprintPosition = new Vector3(0f, -0.05f, 0f);
    [Tooltip("달릴 때 총이 돌아가는 각도 (도)")]
    public Vector3 sprintRotation = new Vector3(15f, -25f, 10f);
    [Tooltip("달리기 자세로 바뀌거나 돌아오는 데 걸리는 시간 (초)")]
    public float sprintBlendTime = 0.25f;
    [Tooltip("쏘고 나서 이 시간(초) 동안은 달려도 총을 안 내림")]
    public float sprintBlockAfterShot = 0.3f;

    [Header("4) 옆걸음 기울기")]
    [Tooltip("A · D로 움직일 때 기울어지는 각도 (도)")]
    public float strafeTilt = 3f;
    [Tooltip("기울어지는 데 걸리는 시간 (초)")]
    public float strafeTiltTime = 0.2f;

    [Header("계산대 스캐너")]
    [Tooltip("스캐너를 들었을 때 흔들림 세기 (총 = 1). 반동 · 달리기 자세는 없음")]
    [Range(0f, 1f)] public float scannerScale = 0.5f;

    [Header("공통")]
    [Tooltip("모드를 바꾸거나 태블릿을 열고 닫을 때 효과가 켜지고 꺼지는 시간 (초)")]
    public float fadeTime = 0.2f;
    [Tooltip("총이 돌아갈 때 중심이 되는 점 (카메라 기준 좌표, m). (0,0,0)이면 손에 든 물건의 가운데를 자동으로 씀")]
    public Vector3 rotationCenter = Vector3.zero;
    [Tooltip("위치 흔들림 배율. 0이면 자동 (총이 멀리 있을수록 크게 → 보통 FPS처럼 보이게, 지금 총은 약 4배). 회전에는 안 곱함")]
    public float positionScale = 0f;

    [Header("연결 (비우면 자동으로 찾음)")]
    public Player_Interaction interaction;
    public Player_movement movement;
    public Player_Look look;
    public Player_State state;

    // 지금 효과가 얼마나 켜져 있는지 0 ~ 1 (모드 · 세기 · 태블릿을 다 따진 값, 부드럽게 바뀜). CameraFeel도 씀
    public float Blend { get; private set; }
    // 걸음 흔들림 값 (총 · 스캐너와 상관없이). CameraFeel이 이 값의 일부를 카메라에 씀
    public Vector3 BobOffset { get; private set; }
    // 총 · 스캐너가 들어 있는 피벗
    public Transform Pivot => pivot;

    private Transform pivot;
    private GunReload reload;
    private bool tabletOpen;
    private GameEndScreen endScreen;
    private bool cleared = true;

    // 지난 프레임 값 (얼마나 움직였는지 계산용)
    private float lastYaw, lastPitch;
    private Vector3 lastPos;
    private bool hasLast;

    // 스프링 상태 (값 + 속도)
    private float blendVel;
    private float handWeight, handWeightVel;   // 총 1 · 스캐너 0.5 · 빈손 0
    private float gunWeight, gunWeightVel;     // 총을 들었으면 1
    private Vector3 swayPos, swayPosVel, swayRot, swayRotVel;
    private float moveAmount, moveAmountVel;   // 얼마나 빨리 걷는지 0 ~ 1
    private float runBlend, runBlendVel;       // 달리는 중이면 1 (Bob 크기 · 빠르기)
    private float sprintPose, sprintPoseVel;   // 달리기 자세 0 ~ 1
    private float tilt, tiltVel;
    private float bobPhase;
    private float shotTimer;                   // 쏜 뒤 남은 "달리기 자세 금지" 시간

    private void Awake()
    {
        if (interaction == null) interaction = GetComponentInParent<Player_Interaction>();
        if (movement == null) movement = GetComponentInParent<Player_movement>();
        if (look == null) look = GetComponentInParent<Player_Look>();
        if (state == null) state = GetComponentInParent<Player_State>();
        CreatePivot();
    }

    private void Start()
    {
        endScreen = FindAnyObjectByType<GameEndScreen>();
    }

    private void OnEnable()
    {
        TabletController.OnTabletToggled += HandleTablet;
        Player_Interaction.OnGunFired += HandleGunFired;
        hasLast = false;
    }

    private void OnDisable()
    {
        TabletController.OnTabletToggled -= HandleTablet;
        Player_Interaction.OnGunFired -= HandleGunFired;
        Clear();   // 꺼지면 총을 원래 자리로
    }

    private void HandleTablet(bool open) => tabletOpen = open;

    // ---------- 피벗 만들고 총 · 스캐너 옮기기 ----------
    // 피벗은 카메라 바로 아래(크기 1)에 만들고, 총 · 스캐너가 들어 있는 "카메라 바로 아래 오브젝트"를 통째로 옮김
    // (Hand Gun의 부모는 크기 5인 프리팹 루트라서, 그 아래에 만들면 움직임이 5배가 됨 → PivotUtil 설명 참고)
    private void CreatePivot()
    {
        GameObject gun = interaction != null ? interaction.handGun : null;
        GameObject scanner = interaction != null ? interaction.Scanner : null;
        Transform cam = look != null && look.playerCamera != null ? look.playerCamera
                      : (interaction != null && interaction.playerCamera != null ? interaction.playerCamera.transform : null);
        if (cam == null || (gun == null && scanner == null))
        {
            Debug.LogWarning("[WeaponFeel] 카메라나 Hand Gun / Scanner가 연결돼 있지 않아서 무기 흔들림을 끕니다.", this);
            enabled = false;
            return;
        }

        GameObject first = gun != null ? gun : scanner;
        pivot = PivotUtil.GetOrCreate(cam, PivotName, first.layer);

        // 피벗이 (0,0,0)에 회전 · 크기도 그대로라서, 로컬 값을 그대로 두고 옮기면 보이는 자리도 똑같음
        foreach (GameObject hand in new[] { gun, scanner })
        {
            if (hand == null) continue;
            Transform top = PivotUtil.TopUnder(cam, hand.transform);
            if (top == null)
            {
                Debug.LogWarning($"[WeaponFeel] '{hand.name}'이(가) 카메라 아래에 없어서 피벗에 넣지 않았어요. (흔들림이 안 들어감)", hand);
                continue;
            }
            if (top != pivot) top.SetParent(pivot, false);
        }
    }

    // 지금 손에 든 물건의 가운데 (피벗 기준). 물건이 바뀔 때 한 번만 계산해 두고, 그 물건을 따라 움직임
    private Transform centerOf;
    private Vector3 centerLocal;

    private Vector3 HeldCenter(Transform held)
    {
        if (rotationCenter != Vector3.zero) return rotationCenter;   // 직접 정해 줬으면 그 값
        if (held == null) return lastCenter;
        if (held != centerOf)
        {
            centerOf = held;
            centerLocal = held.InverseTransformPoint(pivot.TransformPoint(PivotUtil.CenterIn(pivot, held)));
        }
        lastCenter = pivot.InverseTransformPoint(held.TransformPoint(centerLocal));
        return lastCenter;
    }
    private Vector3 lastCenter;

    // ---------- 쏠 때 (Player_Interaction이 알려줌) ----------
    private void HandleGunFired()
    {
        shotTimer = sprintBlockAfterShot;
    }

    // ---------- 매 프레임 ----------
    private void LateUpdate()
    {
        float dt = CheatManager.PlayerDelta;   // 시간 정지 중에도 흐르는 시간
        if (pivot == null || dt <= 0f) return;

        // 지난 프레임보다 시점 · 위치가 얼마나 바뀌었는지
        float yaw = transform.eulerAngles.y;
        float pitch = look != null ? SignedAngle(look.BaseCameraRotation.eulerAngles.x) : 0f;
        Vector3 pos = transform.position;
        float yawSpeed = 0f, pitchSpeed = 0f;
        Vector3 localMove = Vector3.zero;
        if (hasLast)
        {
            yawSpeed = Mathf.DeltaAngle(lastYaw, yaw) / dt;       // 1초에 몇 도 도는지
            pitchSpeed = Mathf.DeltaAngle(lastPitch, pitch) / dt;
            Vector3 move = (pos - lastPos) / dt;                  // 1초에 몇 m 가는지
            move.y = 0f;
            localMove = transform.InverseTransformDirection(move); // x = 옆, z = 앞
        }
        lastYaw = yaw; lastPitch = pitch; lastPos = pos; hasLast = true;

        // 효과를 켤지: 다이나믹 시점 + 태블릿 닫힘 + 게임 안 끝남
        bool ended = endScreen != null && endScreen.Shown;
        float goal = (tabletOpen || ended) ? 0f : ViewModeSettings.Strength;
        Blend = Mathf.Clamp01(FeelSpring.Step(Blend, ref blendVel, goal, FeelSpring.SpeedFromTime(fadeTime), dt));
        if (goal <= 0f && Blend < 0.001f)
        {
            if (!cleared) Clear();   // 완전히 꺼지면 피벗을 정확히 원래대로 (고정 시점 = 예전과 똑같음)
            return;
        }
        cleared = false;

        // 지금 손에 든 것
        bool gunInHand = interaction != null && interaction.GunInHand;
        bool scannerInHand = !gunInHand && state != null && state.inCounter;   // 계산대 안이면 스캐너
        float handGoal = gunInHand ? 1f : (scannerInHand ? scannerScale : 0f);
        float quick = FeelSpring.SpeedFromTime(0.15f);
        handWeight = FeelSpring.Step(handWeight, ref handWeightVel, handGoal, quick, dt);
        gunWeight = FeelSpring.Step(gunWeight, ref gunWeightVel, gunInHand ? 1f : 0f, quick, dt);

        // 1) Sway: 돌리는 반대쪽으로 밀려났다가 따라옴
        float yawK = Mathf.Clamp(yawSpeed / swayFullTurnSpeed, -1f, 1f);     // 오른쪽으로 돌면 +
        float pitchK = Mathf.Clamp(pitchSpeed / swayFullTurnSpeed, -1f, 1f); // 위를 보면 -
        Vector3 swayPosGoal = new Vector3(-yawK, pitchK, 0f) * swayPosition;
        Vector3 swayRotGoal = new Vector3(-pitchK, -yawK, -yawK) * swayRotation;   // z = 도는 쪽으로 기울기
        swayPos = FeelSpring.Step(swayPos, ref swayPosVel, swayPosGoal, swaySpeed, dt);
        swayRot = FeelSpring.Step(swayRot, ref swayRotVel, swayRotGoal, swaySpeed, dt);

        // 2) Bob: 걷는 빠르기만큼 8자로 출렁
        float walkSpeed = movement != null ? Mathf.Max(movement.moveSpeed, 0.1f) : 5f;
        float moveGoal = Mathf.Clamp01(localMove.magnitude / walkSpeed);
        moveAmount = FeelSpring.Step(moveAmount, ref moveAmountVel, moveGoal, quick, dt);

        bool running = movement != null && movement.IsRunning && !movement.IsSitting && localMove.z > walkSpeed * 0.5f;
        runBlend = FeelSpring.Step(runBlend, ref runBlendVel, running ? 1f : 0f, FeelSpring.SpeedFromTime(sprintBlendTime), dt);

        float amount = Mathf.Lerp(walkBobAmount, runBobAmount, runBlend) * moveAmount;
        bobPhase = Mathf.Repeat(bobPhase + Mathf.Lerp(walkBobSpeed, runBobSpeed, runBlend) * moveAmount * dt, Mathf.PI * 2f);
        BobOffset = new Vector3(Mathf.Sin(bobPhase) * amount, Mathf.Sin(bobPhase * 2f) * amount * bobVerticalRatio, 0f);

        // 3) 달리기 자세 (쏜 직후엔 안 내림)
        if (shotTimer > 0f) shotTimer -= dt;
        if (reload == null) reload = GetComponentInParent<GunReload>();
        bool sprintGoal = running && shotTimer <= 0f && !(reload != null && reload.IsReloading);   // 재장전 중엔 안 내림
        sprintPose = FeelSpring.Step(sprintPose, ref sprintPoseVel, sprintGoal ? 1f : 0f, FeelSpring.SpeedFromTime(sprintBlendTime), dt);

        // 4) 옆걸음: 오른쪽(D)으로 가면 오른쪽으로 기울기 (z가 음수 = 시계 방향)
        float tiltGoal = -Mathf.Clamp(localMove.x / walkSpeed, -1f, 1f) * strafeTilt;
        tilt = FeelSpring.Step(tilt, ref tiltVel, tiltGoal, FeelSpring.SpeedFromTime(strafeTiltTime), dt);


        // 전부 더하기 (스캐너는 1 · 2만, 총은 전부)
        Vector3 offsetPos = (swayPos + BobOffset) * handWeight
                          + sprintPosition * sprintPose * gunWeight;
        Vector3 offsetRot = swayRot * handWeight
                          + (sprintRotation * sprintPose + new Vector3(0f, 0f, tilt)) * gunWeight;
        offsetPos *= Blend;
        offsetRot *= Blend;

        // 카메라 원점이 아니라 손에 든 물건의 가운데를 중심으로 돌리기
        Transform held = gunInHand ? interaction.handGun.transform
                       : (scannerInHand && interaction.Scanner != null ? interaction.Scanner.transform : null);
        Vector3 center = HeldCenter(held);
        float scale = positionScale > 0f ? positionScale : PivotUtil.AutoPositionScale(center);
        PivotUtil.Apply(pivot, offsetPos * scale, Quaternion.Euler(offsetRot), center);
    }

    // 피벗과 모든 효과를 0으로
    private void Clear()
    {
        cleared = true;
        Blend = 0f; blendVel = 0f;
        if (pivot != null)
        {
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
        }
        handWeight = handWeightVel = gunWeight = gunWeightVel = 0f;
        swayPos = swayPosVel = swayRot = swayRotVel = Vector3.zero;
        moveAmount = moveAmountVel = runBlend = runBlendVel = 0f;
        sprintPose = sprintPoseVel = tilt = tiltVel = 0f;
        bobPhase = 0f;
        shotTimer = 0f;
        BobOffset = Vector3.zero;
    }

    // 0 ~ 360도 → -180 ~ 180도 (위를 보면 음수)
    private static float SignedAngle(float a) => a > 180f ? a - 360f : a;
}
