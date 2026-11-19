using System;
using System.Collections.Generic;
using ConvenienceStore.Tablet;
using UnityEngine;
using UnityEngine.Rendering;

// 권총 꺼내기(스왑) · 넣기 · 쏘기 모션
//   꺼내기 (판에서 처음, 약 1.1초): ① 오른쪽 아래에서 올라옴 ② 왼쪽으로 눕혀 옆면 보기
//                                   ③ 왼손이 슬라이드를 잡고 당겼다 놓음 "철컥" ④ 왼손이 감싸며 정자세
//   꺼내기 (그다음부터, 약 0.35초): 아래에서 올라오며 살짝 기울었다 정자세. 왼손은 0.1초 늦게 따라옴
//   넣기 (0.25초): 오른쪽 아래로 기울이며 내려감. 왼손이 먼저 놓음. 끝나면 총을 끔
//   꺼내는 중에 칸을 바꾸면 지금 자세에서 바로 넣기로 이어짐 (튀지 않음)
//   꺼내기 · 넣기 중에는 쏘기 · 재장전 금지
//   쏘기: 킥(뒤로 · 총구 위로 · 살짝 오른쪽) → 스프링으로 살짝 넘쳤다 복귀 + 탄피. 연사하면 쌓이지만 최대값 제한
//         (예전 WeaponFeel의 총 반동을 이리로 합침 → 고정 시점에서도 총은 반동. 카메라 반동은 CameraFeel이 다이나믹일 때만)
//
// 계층 (각자 자기 Transform만 건드림)
//   Camera
//    └ WeaponFeelPivot     흔들림 (WeaponFeel)
//       └ GunDrawPivot     꺼내기 · 넣기 · 쏘기 (이 스크립트). 평소엔 위치 0 · 회전 0
//          └ GunReloadPivot 재장전 (GunReload)
//             └ 총 프리팹 루트 (이 스크립트는 안 건드림)
// 손(FPSHandRig)은 총에 붙은 HandSocket을 따라가니까 총만 움직이면 같이 움직임. 이 스크립트는 손 모양 · 왼손 Follow만 바꿈
// 손 소켓은 총이 보일 때만 켬 (소켓이 총 프리팹 루트에 붙어 있어서, 안 그러면 총을 넣어도 손이 허공을 쥐고 있음)
//
// Player(Player_Interaction이 붙은 오브젝트)에 붙이기. 안 붙여도 실행할 때 자동으로 붙음
[DefaultExecutionOrder(20)]   // GunReload(0) 다음, WeaponFeel(50) · 손(10000) 전에
[DisallowMultipleComponent]
public class GunMotion : MonoBehaviour
{
    public const string PivotName = "GunDrawPivot";

    [Header("꺼내기 · 판에서 처음 (약 1.1초)")]
    [Tooltip("① 시작 자세 위치 (m, 카메라 기준). 화면 오른쪽 아래")]
    public Vector3 firstStartPosition = new Vector3(0.06f, -0.18f, -0.05f);
    [Tooltip("① 시작 자세 회전 (도)")]
    public Vector3 firstStartRotation = new Vector3(30f, 20f, -40f);
    [Tooltip("① 올라오는 시간 (초)")]
    public float firstRiseTime = 0.25f;
    [Tooltip("② 옆으로 눕혀 보는 자세 위치 (m)")]
    public Vector3 inspectPosition = new Vector3(-0.01f, 0.01f, 0f);
    [Tooltip("② 옆으로 눕혀 보는 자세 회전 (도). roll +면 왼쪽으로 누움")]
    public Vector3 inspectRotation = new Vector3(5f, -25f, 55f);
    [Tooltip("② 눕히는 시간 (초)")]
    public float inspectTime = 0.30f;
    [Tooltip("③ 슬라이드 당기기 구간 (초)")]
    public float rackTime = 0.20f;
    [Tooltip("③ 슬라이드를 당기는 시간 · 멈춤 · 놓는 시간 (초)")]
    public float rackPull = 0.08f, rackHold = 0.02f, rackRelease = 0.05f;
    [Tooltip("③ 슬라이드를 당기는 거리 (슬라이드 로컬 단위, 재장전의 Slide Back Distance와 같은 단위)")]
    public float rackDistance = 0.06f;
    [Tooltip("③ 철컥할 때 총이 튀는 위치 (m) · 회전 (도)")]
    public Vector3 rackKickPosition = new Vector3(0f, 0f, -0.02f);
    public Vector3 rackKickRotation = new Vector3(3f, 0f, 0f);
    [Tooltip("③ 철컥할 때 카메라 위로 펀치 (도, 다이나믹 시점만)")]
    public float rackCameraPunch = 1f;
    [Tooltip("④ 정자세로 돌아오는 시간 (초, 스프링)")]
    public float firstSettleTime = 0.35f;

    [Header("꺼내기 · 그다음부터 (약 0.35초)")]
    public Vector3 quickStartPosition = new Vector3(0.03f, -0.14f, -0.03f);
    public Vector3 quickStartRotation = new Vector3(20f, 8f, -20f);
    [Tooltip("올라오는 시간 (초)")]
    public float quickRiseTime = 0.20f;
    [Tooltip("올라오며 살짝 기우는 자세 (도)")]
    public Vector3 quickTiltRotation = new Vector3(-3f, 0f, 6f);
    [Tooltip("정자세로 돌아오는 시간 (초, 스프링)")]
    public float quickSettleTime = 0.15f;
    [Tooltip("왼손이 늦게 따라오는 시간 (초)")]
    public float leftHandDelay = 0.10f;

    [Header("넣기 (0.25초)")]
    public Vector3 holsterPosition = new Vector3(0.05f, -0.16f, -0.03f);
    public Vector3 holsterRotation = new Vector3(25f, 15f, -30f);
    public float holsterTime = 0.25f;

    [Header("쏘기 (킥 → 스프링 복귀)")]
    [Tooltip("총이 튀는 위치 (m). z가 음수면 뒤로")]
    public Vector3 shotKickPosition = new Vector3(0f, 0f, -0.025f);
    [Tooltip("총구가 위로 들리는 각도 (도)")]
    public float shotKickUp = 6f;
    [Tooltip("오른쪽으로 기우는 각도 (도) · 랜덤 범위 (±도)")]
    public float shotKickRoll = 2f, shotKickRollRandom = 1f;
    [Tooltip("킥이 최대가 되는 시간 (초). 아주 짧게")]
    public float shotKickTime = 0.04f;
    [Tooltip("스프링 감쇠 0 ~ 1. 작을수록 더 넘쳤다 돌아옴")]
    [Range(0.1f, 0.95f)] public float shotDamping = 0.6f;
    [Tooltip("연사할 때 총구가 들리는 최대 각도 (도)")]
    public float shotMaxUp = 15f;

    [Header("탄피")]
    public bool ejectCasing = true;
    [Tooltip("탄피 크기 (총 모델 단위. 총 크기에 맞춰 자동으로 커짐)")]
    public Vector3 casingSize = new Vector3(0.012f, 0.012f, 0.022f);
    public Color casingColor = new Color(0.85f, 0.65f, 0.25f);   // 황동색
    [Tooltip("튀어나가는 속도 (m/초). 오른쪽 위로")]
    public float casingSpeed = 1.2f;
    [Tooltip("탄피가 사라지기까지 시간 (초)")]
    public float casingLifetime = 2f;

    [Header("공통")]
    [Tooltip("위치 움직임 배율. 0이면 자동 (총이 멀리 있을수록 크게, 지금 총은 약 4배)")]
    public float positionScale = 0f;
    [Tooltip("스프링으로 정자세에 돌아올 때 출렁임 횟수")]
    public float springFrequency = 7f;
    [Range(0.1f, 1f)] public float springDamping = 0.55f;
    [Tooltip("꺼낼 때 총 Animator의 예전 꺼내기(open) 애니메이션은 건너뛰고 이 모션만 씀")]
    public bool skipOldOpenAnimation = true;
    [Tooltip("(에디터 전용) 꺼내기 · 넣기 · 쏘기를 0.25배속으로 느리게 보기")]
    public bool slowMotionPreview = false;

    [Header("연결 (비우면 자동으로 찾음)")]
    public Player_Interaction interaction;

    // 꺼내기 · 넣기 중인지 (이때는 쏘기 · 재장전 금지)
    public bool Busy => state == State.Drawing && time < busyUntil || state == State.Holstering;

    // ---------- 내부 ----------
    private enum State { Hidden, Drawing, Held, Holstering }

    private struct Segment
    {
        public float start, duration;
        public Vector3 fromPos, fromRot, toPos, toRot;
        public ReloadEase ease;
        public bool spring;
    }

    private struct TimedEvent { public float time; public Action run; }

    private State state = State.Hidden;
    private readonly List<Segment> segments = new List<Segment>();
    private readonly List<TimedEvent> events = new List<TimedEvent>();
    private int nextEvent;
    private float time, totalTime, busyUntil;
    private bool drawnThisGame;   // 이번 판에 꺼낸 적이 있는지 (새 판 = 새 씬 = 다시 false)
    private Action onHolstered;

    private Transform cam, pivot, gunTop, slideGrip, ejectPoint;
    private Animator gunAnimator;
    private HandSocket[] sockets;
    private GunReload reload;
    private CameraFeel cameraFeel;
    private CharacterController playerBody;
    private bool tabletOpen;

    private Vector3 posNow, posVel, rotNow, rotVel;
    private FeelImpact shot;
    private Vector3 center;
    private float scaleNow = 1f;

    private static FPSHandRig Hands => FPSHandRig.Instance;

    // ---------- 자동으로 붙이기 ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitAutoAttach()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        foreach (Player_Interaction pi in FindObjectsByType<Player_Interaction>(FindObjectsSortMode.None))
            if (pi.GetComponent<GunMotion>() == null) pi.gameObject.AddComponent<GunMotion>();
    }

    // ---------- 준비 ----------
    private void Awake()
    {
        if (interaction == null) interaction = GetComponent<Player_Interaction>();
        playerBody = GetComponent<CharacterController>();
    }

    // WeaponFeel(Awake) 다음에 피벗을 넣어야 해서 Start에서. GunReload와 어느 쪽이 먼저 실행돼도 순서가 맞게 끼워 넣음
    private void Start()
    {
        GameObject gun = interaction != null ? interaction.handGun : null;
        Player_Look look = GetComponent<Player_Look>();
        cam = look != null && look.playerCamera != null ? look.playerCamera
            : (interaction != null && interaction.playerCamera != null ? interaction.playerCamera.transform : null);
        if (gun == null || cam == null || PivotUtil.TopUnder(cam, gun.transform) == null)
        {
            Debug.LogWarning("[GunMotion] 카메라 아래에서 Hand Gun을 못 찾아서 꺼내기 · 쏘기 모션을 끕니다.", this);
            enabled = false;
            return;
        }

        Transform feel = cam.Find(WeaponFeel.PivotName);
        Transform container = feel != null && PivotUtil.TopUnder(feel, gun.transform) != null ? feel : cam;
        pivot = PivotUtil.WrapUnder(container, gun.transform, PivotName);   // GunReloadPivot이 먼저 있었으면 그걸 통째로 넣음
        gunTop = gun.transform;
        while (gunTop.parent != null && gunTop.parent != pivot && gunTop.parent.name != GunReload.PivotName) gunTop = gunTop.parent;

        gunAnimator = gun.GetComponent<Animator>();
        sockets = gunTop.GetComponentsInChildren<HandSocket>(true);
        reload = GetComponent<GunReload>();
        cameraFeel = cam.GetComponent<CameraFeel>();
        foreach (Transform t in gunTop.GetComponentsInChildren<Transform>(true))
            if (ejectPoint == null && t.name == "Ejection_Location") ejectPoint = t;

        // 예전 탄피(발사 애니메이션 이벤트로 나오던 것)는 끔 → 탄피가 두 개씩 안 나오게 (실행 중에만, 프리팹은 그대로)
        SimpleShoot ss = gun.GetComponentInChildren<SimpleShoot>(true);
        if (ss != null && ejectCasing) ss.casingPrefab = null;

        if (!gun.activeSelf) SetSockets(false);
    }

    private void OnEnable()
    {
        TabletController.OnTabletToggled += HandleTablet;
        Player_Interaction.OnGunFired += HandleFired;
    }

    private void OnDisable()
    {
        TabletController.OnTabletToggled -= HandleTablet;
        Player_Interaction.OnGunFired -= HandleFired;
    }

    private void HandleTablet(bool open) => tabletOpen = open;

    // ---------- Player_Interaction이 부름 ----------
    // 총을 켠 직후 (6번을 골랐을 때)
    public void Draw()
    {
        if (pivot == null) return;
        onHolstered = null;
        bool first = !drawnThisGame;
        drawnThisGame = true;

        // 예전 꺼내기 애니메이션은 건너뛰고 바로 다 꺼낸 상태(idle)로 → 쏠 수 있는 조건(gunReadyState)은 그대로
        if (skipOldOpenAnimation && gunAnimator != null && interaction != null)
            gunAnimator.Play(interaction.gunReadyState, 0, 0f);

        Begin();
        bool fromHidden = state == State.Hidden;
        state = State.Drawing;
        // 넣는 중이었으면 지금 자세에서 시작 (튀지 않게)
        Vector3 p0 = fromHidden ? (first ? firstStartPosition : quickStartPosition) : posNow;
        Vector3 r0 = fromHidden ? (first ? firstStartRotation : quickStartRotation) : rotNow;
        if (fromHidden) { posNow = p0; rotNow = r0; posVel = rotVel = Vector3.zero; }

        SetSockets(true);
        if (Hands != null && Hands.left.rest != null) Hands.Follow(HandSide.Left, Hands.left.rest, null, 0.01f);   // 왼손은 아래에서 대기

        if (first)
        {
            if (Hands != null) Hands.SetPose(HandSide.Right, "Relaxed");
            AddEvent(firstRiseTime * 0.5f, () => { if (Hands != null) Hands.ClearPose(HandSide.Right); });   // 올라오며 쥠
            Move(p0, r0, firstRiseTime, Vector3.zero, Vector3.zero, ReloadEase.EaseOut);       // ① 올라오기
            Move(LastPos, LastRot, inspectTime, inspectPosition, inspectRotation, ReloadEase.EaseInOut);   // ② 옆으로 눕혀 보기
            float rackStart = End;
            Move(LastPos, LastRot, rackTime, inspectPosition, inspectRotation, ReloadEase.Linear);         // ③ 멈춘 채 슬라이드
            AddEvent(rackStart, RackStart);
            AddEvent(rackStart + rackPull + rackHold, RackRelease);
            AddEvent(End, () => { if (Hands != null) Hands.StopFollow(HandSide.Left, 0.2f); });           // ④ 왼손이 감쌈
            SpringTo(firstSettleTime);                                                                  // ④ 정자세
        }
        else
        {
            AddEvent(leftHandDelay, () => { if (Hands != null) Hands.StopFollow(HandSide.Left, 0.15f); });
            Move(p0, r0, quickRiseTime, Vector3.zero, quickTiltRotation, ReloadEase.EaseOut);
            SpringTo(quickSettleTime);
        }
        totalTime = End;
        busyUntil = totalTime;
        events.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // 총을 끄기 전 (다른 칸을 골랐을 때). 넣기가 끝나면 done을 부름 (그때 총을 끔)
    public void Holster(Action done)
    {
        if (pivot == null || state == State.Hidden) { done?.Invoke(); return; }
        Begin();
        state = State.Holstering;
        onHolstered = done;
        if (Hands != null && Hands.left.rest != null) Hands.Follow(HandSide.Left, Hands.left.rest, null, 0.1f);   // 왼손이 먼저 놓고 내려감
        Move(posNow, rotNow, holsterTime, holsterPosition, holsterRotation, ReloadEase.EaseIn);
        totalTime = End;
        busyUntil = totalTime;
    }

    private void Begin()
    {
        segments.Clear();
        events.Clear();
        nextEvent = 0;
        time = 0f;
        center = PivotUtil.CenterIn(pivot, gunTop);
        scaleNow = positionScale > 0f ? positionScale : PivotUtil.AutoPositionScale(center);
    }

    private float End => segments.Count > 0 ? segments[segments.Count - 1].start + segments[segments.Count - 1].duration : 0f;
    private Vector3 LastPos => segments.Count > 0 ? segments[segments.Count - 1].toPos : posNow;
    private Vector3 LastRot => segments.Count > 0 ? segments[segments.Count - 1].toRot : rotNow;

    private void Move(Vector3 fromPos, Vector3 fromRot, float duration, Vector3 toPos, Vector3 toRot, ReloadEase ease)
    {
        segments.Add(new Segment { start = End, duration = Mathf.Max(0.001f, duration), fromPos = fromPos, fromRot = fromRot, toPos = toPos, toRot = toRot, ease = ease });
    }

    private void SpringTo(float duration)
    {
        segments.Add(new Segment { start = End, duration = Mathf.Max(0.001f, duration), fromPos = LastPos, fromRot = LastRot, toPos = Vector3.zero, toRot = Vector3.zero, spring = true });
    }

    private void AddEvent(float t, Action run) => events.Add(new TimedEvent { time = t, run = run });

    // ---------- ③ 슬라이드 당기기 ----------
    private void RackStart()
    {
        if (reload == null || reload.Slide == null || reload.SlideLocked) return;
        if (slideGrip == null)
        {
            // 슬라이드 뒤쪽 윗면에 왼손이 잡을 자리를 만듦 (슬라이드와 같이 움직임)
            slideGrip = new GameObject("SlideGrip_L").transform;
            slideGrip.SetParent(reload.Slide, false);
            Transform sockL = null;
            foreach (HandSocket s in sockets) if (s.side == HandSide.Left) sockL = s.transform;
            Vector3 point = PivotUtil.BoundsIn(reload.Slide, reload.Slide, out Bounds b)
                ? new Vector3(b.center.x, b.max.y, b.min.z + b.size.z * 0.2f) : Vector3.zero;
            slideGrip.localPosition = point;
            slideGrip.rotation = sockL != null ? sockL.rotation : reload.Slide.rotation;
        }
        if (Hands != null) Hands.Follow(HandSide.Left, slideGrip, "Grab", 0.08f);
        reload.PlaySlideCycle(rackDistance, rackPull, rackHold, rackRelease, 0f);
    }

    private void RackRelease()
    {
        SoundManager.Play(Sfx.GunSlideRack);
        shot.Hit(rackKickPosition, rackKickRotation, ShotFrequency, shotDamping);
        if (cameraFeel != null) cameraFeel.Punch(new Vector3(-rackCameraPunch, 0f, 0f));   // 고정 시점이면 무시됨
    }

    // ---------- 쏘기 ----------
    // 킥이 shotKickTime에 최대가 되도록 스프링 빠르기를 계산
    private float ShotFrequency
    {
        get
        {
            float z = Mathf.Clamp(shotDamping, 0.05f, 0.95f);
            float s = Mathf.Sqrt(1f - z * z);
            float wd = Mathf.Atan2(s, z) / Mathf.Max(shotKickTime, 0.005f);   // 첫 최대값까지 걸리는 시간 = atan2(s, ζ) / ωd
            return wd / s / (2f * Mathf.PI);
        }
    }

    private void HandleFired()
    {
        if (pivot == null) return;

        // 킥 (연사하면 쌓이지만 총구가 shotMaxUp 이상은 안 들림)
        float up = Mathf.Min(shotKickUp, Mathf.Max(0f, shotMaxUp + shot.Rotation.x));   // rotation.x가 음수 = 위로
        float roll = -(shotKickRoll + UnityEngine.Random.Range(-shotKickRollRandom, shotKickRollRandom));
        float k = shotKickUp > 0f ? up / shotKickUp : 1f;
        shot.Hit(shotKickPosition * k, new Vector3(-up, 0f, roll * k), ShotFrequency, shotDamping);

        if (reload != null) reload.PlaySlideCycle(reload.slideBackDistance, 0.01f, 0f, 0.05f, 0.15f);   // 마지막 탄이면 뒤에 멈춤 (GunReload가 판단)
        if (ejectCasing) EjectCasing();
    }

    private void EjectCasing()
    {
        Transform at = ejectPoint != null ? ejectPoint : (reload != null && reload.Slide != null ? reload.Slide : gunTop);
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = "Casing";
        c.transform.SetPositionAndRotation(at.position, gunTop.rotation);
        c.transform.localScale = Vector3.Scale(casingSize, gunTop.lossyScale);

        Renderer r = c.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        Renderer gunR = gunTop.GetComponentInChildren<Renderer>();
        if (gunR != null) r.sharedMaterial = gunR.sharedMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", casingColor);   // URP
        block.SetColor("_Color", casingColor);
        r.SetPropertyBlock(block);

        Collider col = c.GetComponent<Collider>();
        if (playerBody != null) Physics.IgnoreCollision(col, playerBody);   // 플레이어와 안 부딪히게
        foreach (Collider g in gunTop.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(col, g);

        Rigidbody rb = c.AddComponent<Rigidbody>();
        rb.mass = 0.02f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // 높은 데서 떨어져도 바닥을 안 뚫게
        DroppedMagazine fall = c.AddComponent<DroppedMagazine>();               // 바닥(발 높이) 아래로는 안 떨어짐
        fall.floatTime = 0.12f;
        fall.floatGravity = 0.4f;
        if (playerBody != null)
            fall.floorY = playerBody.transform.TransformPoint(playerBody.center - Vector3.up * (playerBody.height * 0.5f)).y;

        Vector3 dir = (cam.right * 0.75f + cam.up * 0.65f).normalized;
        rb.linearVelocity = dir * casingSpeed + (playerBody != null ? playerBody.velocity : Vector3.zero);
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 15f;
        Destroy(c, casingLifetime);
    }

    // ---------- 손 소켓 ----------
    private void SetSockets(bool on)
    {
        if (sockets == null) return;
        foreach (HandSocket s in sockets) if (s != null && s.enabled != on) s.enabled = on;
    }

    // ---------- 매 프레임 ----------
    private void LateUpdate()
    {
        if (pivot == null) return;
        float dt = CheatManager.PlayerDelta;   // 시간 정지 중에도 흐르는 시간
#if UNITY_EDITOR
        if (slowMotionPreview) dt *= 0.25f;
#endif
        bool gunShown = interaction.handGun != null && interaction.handGun.activeInHierarchy;
        SetSockets(gunShown && !tabletOpen);   // 총이 보일 때만 손이 쥠

        if (!gunShown && state != State.Holstering)
        {
            // 총이 꺼져 있으면 피벗은 정확히 0
            if (state != State.Hidden) { state = State.Hidden; ResetPose(); }
            return;
        }
        if (state == State.Hidden) state = State.Held;   // (Draw 없이 켜진 경우)

        if (state == State.Drawing || state == State.Holstering)
        {
            time += dt;
            Segment s = segments[segments.Count - 1];
            foreach (Segment seg in segments) if (time < seg.start + seg.duration) { s = seg; break; }
            float u = Mathf.Clamp01((time - s.start) / s.duration);
            Vector3 goalPos = s.spring ? Vector3.zero : Vector3.LerpUnclamped(s.fromPos, s.toPos, Ease(s.ease, u));
            Vector3 goalRot = s.spring ? Vector3.zero : Vector3.LerpUnclamped(s.fromRot, s.toRot, Ease(s.ease, u));
            if (s.spring)
            {
                posNow = FeelSpring.Bouncy(posNow, ref posVel, goalPos, springFrequency, springDamping, dt);
                rotNow = FeelSpring.Bouncy(rotNow, ref rotVel, goalRot, springFrequency, springDamping, dt);
            }
            else
            {
                if (dt > 0f) { posVel = (goalPos - posNow) / dt; rotVel = (goalRot - rotNow) / dt; }
                posNow = goalPos;
                rotNow = goalRot;
            }

            while (nextEvent < events.Count && time >= events[nextEvent].time) events[nextEvent++].run();

            if (state == State.Holstering && time >= totalTime)
            {
                // 다 내려감 → 총을 끄게 함
                Action done = onHolstered;
                onHolstered = null;
                state = State.Hidden;
                if (Hands != null) Hands.StopFollow(HandSide.Left, 0.01f);
                done?.Invoke();
                ResetPose();
                return;
            }
            if (state == State.Drawing && time >= totalTime + 0.4f) state = State.Held;   // 스프링 꼬리까지 끝
        }
        else
        {
            // 들고 있을 때: 남은 스프링만 0으로
            posNow = FeelSpring.Bouncy(posNow, ref posVel, Vector3.zero, springFrequency, springDamping, dt);
            rotNow = FeelSpring.Bouncy(rotNow, ref rotVel, Vector3.zero, springFrequency, springDamping, dt);
            if (posNow.sqrMagnitude < 1e-10f && rotNow.sqrMagnitude < 1e-6f && posVel.sqrMagnitude < 1e-8f && rotVel.sqrMagnitude < 1e-4f)
                posNow = posVel = rotNow = rotVel = Vector3.zero;
        }

        // 쏘기 킥 (스프링)
        shot.Step(ShotFrequency, shotDamping, dt);

        Vector3 pos = posNow + shot.Position;
        Vector3 rot = rotNow + shot.Rotation;
        if (pos == Vector3.zero && rot == Vector3.zero) { pivot.localPosition = Vector3.zero; pivot.localRotation = Quaternion.identity; return; }
        PivotUtil.Apply(pivot, pos * scaleNow, Quaternion.Euler(rot), center);   // "기준값(0) + 오프셋"을 매번 새로 대입
    }

    private void ResetPose()
    {
        posNow = posVel = rotNow = rotVel = Vector3.zero;
        shot.Reset();
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
    }

    private static float Ease(ReloadEase e, float t)
    {
        t = Mathf.Clamp01(t);
        switch (e)
        {
            case ReloadEase.EaseIn: return t * t * t;
            case ReloadEase.EaseOut: { float u = 1f - t; return 1f - u * u * u; }
            case ReloadEase.EaseInOut: return t * t * (3f - 2f * t);
            default: return t;
        }
    }
}
