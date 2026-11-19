using System;
using System.Collections.Generic;
using ConvenienceStore.Tablet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// 움직임 느낌 (처음 · 끝을 얼마나 부드럽게 할지)
public enum ReloadEase
{
    Linear,     // 일정한 속도
    EaseIn,     // 천천히 시작해서 빨라짐 (스냅: 가속하다가 딱 멈춤)
    EaseOut,    // 빠르게 시작해서 천천히 멈춤
    EaseInOut,  // 천천히 시작 · 천천히 멈춤
}

// 스프링으로 따라가는 구간의 자세 (슬라이드 멈치 구간에서 씀)
[Serializable]
public class ReloadPose
{
    [Tooltip("구간 길이 (초)")]
    public float duration;
    [Tooltip("구간 목표 위치 (m, 카메라 기준). x = 오른쪽(+) / 화면 가운데 쪽(-), y = 위, z = 앞")]
    public Vector3 position;
    [Tooltip("구간 목표 회전 (도) = (pitch 위아래, yaw 좌우, roll 기울기). roll이 +면 왼쪽으로 눕고, -면 오른쪽으로 누움")]
    public Vector3 rotation;
    [Tooltip("움직이는 느낌")]
    public ReloadEase ease;
    [Tooltip("구간의 몇 % 지점에서 목표에 도착할지 (1 = 구간 끝)")]
    [Range(0.1f, 1f)] public float arrive = 1f;

    public ReloadPose() { }

    public ReloadPose(float duration, Vector3 position, Vector3 rotation, ReloadEase ease, float arrive)
    {
        this.duration = duration;
        this.position = position;
        this.rotation = rotation;
        this.ease = ease;
        this.arrive = arrive;
    }
}

// 순간적으로 "툭" 튀는 움직임 (슬라이드 철컥에서 씀)
[Serializable]
public class ReloadKick
{
    [Tooltip("총이 튀는 위치 (m). z가 음수면 뒤로")]
    public Vector3 position;
    [Tooltip("총이 튀는 회전 (도)")]
    public Vector3 rotation;
    [Tooltip("튀는 데 걸리는 시간 (초)")]
    public float riseTime = 0.02f;
    [Tooltip("원래대로 돌아오는 데 걸리는 시간 (초)")]
    public float returnTime = 0.1f;
    [Tooltip("카메라가 튀는 각도 (도, 다이나믹 시점만). x가 음수면 위로")]
    public Vector3 cameraPunch;
    [Tooltip("카메라 진동 세기 (도, 다이나믹 시점만). 0이면 없음")]
    public float cameraShake;
    [Tooltip("카메라 진동 시간 (초)")]
    public float cameraShakeTime = 0.06f;

    [NonSerialized] public FeelKick pos, rot;   // 실행 중 계산용

    public ReloadKick() { }

    public ReloadKick(Vector3 position, Vector3 rotation, float riseTime, float returnTime, Vector3 cameraPunch, float cameraShake = 0f)
    {
        this.position = position;
        this.rotation = rotation;
        this.riseTime = riseTime;
        this.returnTime = returnTime;
        this.cameraPunch = cameraPunch;
        this.cameraShake = cameraShake;
    }
}

// 재장전 순서 (Inspector에서 바꾸기). 위치는 m(카메라 기준), 회전은 도 (pitch, yaw, roll)
//   왼쪽으로 = 화면 가운데 쪽으로 오면서 왼쪽으로 눕기 (탄창 구멍이 보이게) / 오른쪽으로 = 바깥쪽으로 가면서 오른쪽으로 눕기
// 동작마다 따로 움직이고, 목표에 도착하면 멈췄다가(hold) 다음 동작으로 넘어감. 스프링은 충격 순간에만
[Serializable]
public class ReloadTimeline
{
    [Header("① 왼쪽으로")]
    [Tooltip("왼쪽으로 가는 시간 (초)")]
    public float leftTime = 0.20f;
    public Vector3 leftPosition = new Vector3(-0.04f, -0.02f, 0f);
    public Vector3 leftRotation = new Vector3(5f, -10f, 20f);
    [Tooltip("왼쪽에서 멈춰 있는 시간 (초)")]
    public float leftHold = 0.08f;

    [Header("준비 동작 (스냅 직전에 반대쪽으로 살짝)")]
    [Tooltip("준비 동작 시간 (초)")]
    public float anticipationTime = 0.06f;
    [Tooltip("왼쪽 자세에서 더 당기는 위치 (m)")]
    public Vector3 anticipationPosition = new Vector3(-0.008f, 0f, 0f);
    [Tooltip("왼쪽 자세에서 더 당기는 회전 (도)")]
    public Vector3 anticipationRotation = new Vector3(0f, 0f, 5f);

    [Header("② 빼기 스냅 (오른쪽으로 확)")]
    [Tooltip("스냅 시간 (초). 0.06 ~ 0.08이 좋음")]
    public float snapOutTime = 0.08f;
    public Vector3 rightPosition = new Vector3(0.04f, -0.03f, 0f);
    public Vector3 rightRotation = new Vector3(8f, 15f, -25f);
    [Tooltip("딱 멈출 때 넘치는 정도 (스냅 이동량의 몇 배)")]
    public float snapOvershoot = 0.1f;
    [Tooltip("탄창이 손잡이에서 미끄러져 나오는 거리 (m)")]
    public float magSlideOut = 0.05f;
    [Tooltip("스냅 중 탄창이 분리되는 시점 (0 ~ 1). 0.5 = 0.38초")]
    [Range(0f, 1f)] public float magDetachAt = 0.5f;
    [Tooltip("오른쪽에서 멈춰 있는 시간 (초). 반동이 정리되고 손잡이 아래가 빈 채로")]
    public float rightHold = 0.20f;
    [Tooltip("스냅 때 카메라 roll 킥 (도, 다이나믹 시점만)")]
    public float cameraRollKick = 0.8f;

    [Header("좌우 방향")]
    [Tooltip("켜면 ① · 준비 · ② · ③의 좌우를 뒤집음 (위치 x, 회전 yaw · roll, 카메라 roll). 실제 플레이에서 방향이 반대로 보여서 기본으로 켬. 슬라이드 멈치 · 충격은 그대로")]
    public bool mirrorSwing = true;

    [Header("③ 왼쪽으로 + 새 탄창")]
    [Tooltip("왼쪽으로 가는 시간 (초)")]
    public float moveInTime = 0.20f;
    public Vector3 insertPosition = new Vector3(-0.03f, -0.01f, 0f);
    public Vector3 insertRotation = new Vector3(5f, -10f, 20f);
    [Tooltip("새 탄창이 정렬되는 거리 (손잡이 아래 m)")]
    public float magAlignDistance = 0.06f;
    [Tooltip("새 탄창이 처음 나타나는 거리 (탄창 길이의 몇 배 아래 · 화면 밖)")]
    public float newMagStartDistance = 2f;
    [Tooltip("정렬한 채 멈춰 있는 시간 (초)")]
    public float aimHold = 0.08f;

    [Header("④ 넣기 스냅")]
    [Tooltip("탄창이 쑥 들어가는 시간 (초). 0.06 ~ 0.08이 좋음")]
    public float snapInTime = 0.06f;
    [Tooltip("충격: 총이 튀는 위치 (m)")]
    public Vector3 impactPosition = new Vector3(0f, 0.02f, 0f);
    [Tooltip("충격: 총이 튀는 회전 (도). x가 음수면 총구가 위로")]
    public Vector3 impactRotation = new Vector3(-8f, 0f, 0f);
    [Tooltip("충격 때 카메라가 위로 튀는 각도 (도, 다이나믹 시점만)")]
    public float cameraImpactPunch = 1.2f;
    [Tooltip("충격 뒤 정리 시간 (초)")]
    public float impactHold = 0.14f;

    [Header("슬라이드 멈치 (빈 탄창일 때 · 기존 동작)")]
    [Tooltip("슬라이드 멈치 전에 멈춰 있는 시간 (초)")]
    public float preReleaseHold = 0.10f;
    [Tooltip("엄지로 누르듯 살짝 세우는 자세 (스프링으로 따라감)")]
    public ReloadPose release = new ReloadPose(0.40f, new Vector3(0f, -0.01f, 0f), new Vector3(3f, 0f, 8f), ReloadEase.EaseInOut, 0.625f);
    [Tooltip("슬라이드가 앞으로 \"철컥\" 가는 시점 (구간의 몇 %)")]
    [Range(0f, 1f)] public float releaseAt = 0.625f;
    [Tooltip("슬라이드가 앞으로 가는 데 걸리는 시간 (초)")]
    public float slideReleaseTime = 0.05f;
    [Tooltip("슬라이드가 앞으로 갈 때: 뒤로 \"철컥\"")]
    public ReloadKick releaseKick = new ReloadKick(new Vector3(0f, 0f, -0.02f), new Vector3(5f, 0f, 0f), 0.02f, 0.12f, new Vector3(-1.2f, 0f, 0f), 0.3f);

    [Header("⑤ 제자리 (스프링으로 정확히 0)")]
    [Tooltip("탄이 남았을 때 제자리 시간 (초)")]
    public float tacticalSettleTime = 0.40f;
    [Tooltip("빈 탄창일 때 제자리 시간 (초)")]
    public float emptySettleTime = 0.40f;
    [Tooltip("슬라이드만 풀 때 제자리 시간 (초)")]
    public float releaseOnlySettleTime = 0.20f;
    [Tooltip("제자리 구간의 몇 % 지점에서 0에 도착할지. 나머지는 스프링이 살짝 넘쳤다 멈춤")]
    [Range(0.1f, 1f)] public float settleArrive = 0.55f;

    [Header("충격 스프링 (② 멈출 때 · ④ 탁)")]
    [Tooltip("1초에 출렁이는 횟수. 클수록 짧고 빠르게")]
    public float impactFrequency = 9f;
    [Tooltip("감쇠 0 ~ 1. 작을수록 더 출렁임")]
    [Range(0.1f, 0.95f)] public float impactDamping = 0.45f;

    [Header("멈춤 구간의 아주 작은 떨림")]
    [Tooltip("위치 떨림 (m). 0.001 이하")]
    public float holdJitterPosition = 0.0008f;
    [Tooltip("회전 떨림 (도)")]
    public float holdJitterRotation = 0.25f;
    [Tooltip("떨림 빠르기")]
    public float holdJitterSpeed = 9f;
}

// 권총 탄약 + 재장전 (R키)
//   마지막 탄을 쏘면 슬라이드가 뒤로 간 채 멈춤 (실제 권총처럼). 탄이 남아 있으면 발사 애니메이션 그대로
//   쏘기를 막는 조건 = "슬라이드가 뒤에 있음"
//   빈 총을 쏘려고 하면 딸깍 + 자동 재장전 (끌 수 있음)
//
//   탄이 남았을 때 (약 1.5초): ① 왼쪽 → 멈춤 → 준비 → ② 오른쪽 스냅(탄창 빠짐) → 멈춤(빈 손잡이)
//                              → ③ 왼쪽 + 새 탄창 정렬 → 멈춤 → ④ 넣기 스냅 "탁" → 정리 → ⑤ 제자리
//   탄을 다 썼을 때 (약 2.0초): ①~④ 같음 → 멈춤 → 슬라이드 멈치 "철컥" → 제자리
//   탄은 있는데 슬라이드만 뒤에 있을 때 (철컥 전에 취소됨): 클릭이나 R → 슬라이드 멈치만 짧게
//   총을 바꾸거나 · 태블릿을 열거나 · 게임이 끝나면 0.15초 동안 부드럽게 원위치하며 취소
//
//   재장전은 원래 위치에서 함. 이 게임의 총은 크고(5배) 손잡이 아래가 원래 화면 밖이라 탄창은 일부만 보일 수 있음
//   (더 보이게 하려면 Extra Lift를 조금 올리거나, 씬에서 총 위치를 올리기)
//
// 계층 (각자 자기 Transform만 건드림)
//   Camera
//    └ WeaponFeelPivot    흔들림 · 반동 (WeaponFeel)
//       └ GunReloadPivot  재장전 모션 (이 스크립트). 평소엔 위치 0 · 회전 0
//          └ 총 프리팹 루트 (위치 · 회전 · 크기 그대로, 이 스크립트는 절대 안 건드림)
// 실행할 때 만드는 거라 씬 파일은 안 바뀜. 총 모션은 두 시점 다 나오고, 카메라는 다이나믹일 때만 움직임
//
// Player(Player_Interaction이 붙은 오브젝트)에 붙이기. 안 붙여도 실행할 때 자동으로 붙음
[DisallowMultipleComponent]
public class GunReload : MonoBehaviour
{
    public const string PivotName = "GunReloadPivot";

    [Header("탄약")]
    [Tooltip("탄창 하나에 들어가는 총알 수 (M1911은 7발)")]
    [Min(1)] public int magazineSize = 7;
    [Tooltip("탄이 남았을 때 재장전하면 약실에 한 발 더 (7 + 1 = 8발)")]
    public bool chamberBonus = false;
    [Tooltip("켜면 예비 탄약이 무한")]
    public bool infiniteReserve = true;
    [Tooltip("예비 탄약 (무한이 꺼져 있을 때만). 게임 시작할 때 갖고 있는 양")]
    [Min(0)] public int reserveAmmo = 21;

    [Header("조작")]
    [Tooltip("재장전 키")]
    public Key reloadKey = Key.R;
    [Tooltip("빈 총을 쏘려고 하면 자동으로 재장전")]
    public bool autoReloadWhenEmpty = true;

    [Header("모션")]
    public ReloadTimeline timeline = new ReloadTimeline();
    [Tooltip("재장전 빠르기 배율 (2 = 두 배 빠르게)")]
    [Min(0.1f)] public float reloadSpeed = 1f;
    [Tooltip("스프링으로 따라가는 구간(슬라이드 멈치 · 제자리)의 출렁임 횟수")]
    public float springFrequency = 7f;
    [Tooltip("스프링으로 따라가는 구간의 감쇠 0 ~ 1")]
    [Range(0.1f, 1f)] public float springDamping = 0.55f;
    [Tooltip("위치 움직임 배율. 0이면 자동 (총이 멀리 있을수록 크게 → 보통 FPS처럼 보이게, 지금 총은 약 4배)")]
    public float positionScale = 0f;
    [Tooltip("취소할 때 원위치까지 걸리는 시간 (초)")]
    public float cancelTime = 0.15f;
    [Tooltip("(에디터 전용) 재장전만 0.25배속으로 느리게 보기")]
    public bool slowMotionPreview = false;

    [Header("탄창")]
    [Tooltip("재장전하는 동안 총을 이만큼 더 올림 (m, 카메라 기준). 0 = 원래 위치에서 장전. 손잡이 아래가 화면 밖이라 탄창을 더 보이게 하고 싶을 때만 조금씩")]
    public float extraLift = 0f;
    [Tooltip("평소에도 총에 탄창을 보이게 (프리팹에서 탄창이 꺼져 있어도 켬). 끄면 재장전 중에만 보임")]
    public bool showMagazineInGun = true;

    [Header("탄창 · 슬라이드 (비우면 이름으로 자동으로 찾음)")]
    [Tooltip("비워 두기 권장 (자동으로 찾음). 넣으려면 Hierarchy에서 씬의 총 안에 있는 탄창을 끌어다 넣기. 프로젝트 창의 프리팹 파일은 안 됨")]
    public Transform magazine;
    [Tooltip("비워 두기 권장 (이름이 Slider인 것을 자동으로 찾음). 없으면 총 전체가 뒤로 튀는 것으로 대신함")]
    public Transform slide;
    [Tooltip("슬라이드가 뒤로 멈춰 있는 거리 (슬라이드의 로컬 단위)")]
    public float slideBackDistance = 0.06f;
    [Tooltip("탄창이 빠지는 방향 (총 기준). (0,-1,0) = 손잡이 아래쪽")]
    public Vector3 magOutDirection = new Vector3(0f, -1f, 0f);

    [Header("떨어지는 빈 탄창")]
    [Tooltip("켜면 빠진 탄창이 날아가 떨어짐 (끄면 그냥 사라짐)")]
    public bool dropMagazine = true;
    [Tooltip("분리 직후 속도 (m/초). 오른쪽 아래로")]
    public float detachSpeed = 1.5f;
    [Tooltip("빈 탄창은 가벼워서 이 배만큼 빠르게")]
    public float emptyDetachMultiplier = 1.2f;
    [Tooltip("분리 직후 회전 속도")]
    public float detachSpin = 4f;
    [Tooltip("이 시간(초) 동안은 중력을 약하게 → 화면 안에서 보임")]
    public float floatTime = 0.3f;
    [Tooltip("그동안의 중력 세기 (1 = 보통)")]
    public float floatGravity = 0.15f;
    [Tooltip("떨어진 탄창이 사라지기까지 시간 (초)")]
    public float dropLifetime = 3f;

    [Header("소리 (SoundManager 목록에서 소리 파일을 넣기)")]
    public Sfx emptySound = Sfx.GunEmpty;
    public Sfx magOutSound = Sfx.GunMagOut;
    public Sfx magInSound = Sfx.GunMagIn;
    [Tooltip("슬라이드 철컥. 비어 있으면 아래 Slide Rack 소리로 대신")]
    public Sfx slideReleaseSound = Sfx.GunSlideRelease;
    public Sfx slideRackSound = Sfx.GunSlideRack;
    [Tooltip("위 소리(탄창 · 슬라이드)가 다 비어 있으면 시작할 때 이것 하나만 재생")]
    public Sfx fallbackSound = Sfx.GunReload;

    [Header("연결 (비우면 자동으로 찾음)")]
    public Player_Interaction interaction;

    // ---------- 다른 스크립트가 읽는 값 ----------
    public int AmmoInMag { get; private set; }
    public int Reserve => infiniteReserve ? int.MaxValue : reserveAmmo;
    // 슬라이드가 뒤로 멈춰 있는지 (이때는 못 쏨)
    public bool SlideLocked { get; private set; }
    // 총의 슬라이드 (GunMotion이 왼손 잡을 자리를 만들 때 씀)
    public Transform Slide => slide;
    // 재장전 중인지 (이때는 못 쏨). 마지막 제자리 구간부터는 false → 바로 쏠 수 있음
    public bool IsReloading => state == State.Reloading && time < readyTime;
    // 재장전 진행도 0 ~ 1 (원형 게이지용). 쏠 수 있게 되는 순간 1
    public float ReloadProgress => IsReloading ? Mathf.Clamp01(time / readyTime) : 0f;
    // 총알 수가 바뀌거나 재장전이 시작 · 끝날 때 (AmmoHUD가 들음)
    public event Action Changed;

    // ---------- 내부 ----------
    private enum State { Idle, Reloading, Settling, Cancelling }
    private enum Mode { Tactical, Empty, ReleaseOnly }

    // 한 동작 구간: from → to 로 따로 보간 (멈춤이면 from == to)
    private struct Segment
    {
        public float start, duration;
        public Vector3 fromPos, fromRot, toPos, toRot;
        public ReloadEase ease;
        public float arrive;
        public bool spring;   // 스프링으로 따라감 (슬라이드 멈치 · 제자리)
        public bool hold;     // 멈춤 (아주 작은 떨림)
    }

    private struct TimedEvent
    {
        public float time;
        public Action run;
    }

    private State state = State.Idle;
    private Mode mode;
    private readonly List<Segment> segments = new List<Segment>();
    private readonly List<TimedEvent> events = new List<TimedEvent>();
    private int nextEvent;
    private float time, totalTime, readyTime, settleTime, queuedTimer;
    private bool magFilled;
    private bool splitSounds;
    private float noiseSeed;

    // 탄창 움직임 시각
    private float tSnapOut, tDetach, tMoveIn, tAim, tSnapIn, tImpact;

    private Transform cam;
#if UNITY_EDITOR
    private bool loggedDrop;   // 빠진 탄창 위치를 Console에 한 번만 찍기
#endif
    private Transform pivot;        // GunReloadPivot
    private Transform gunTop;       // 총 프리팹 루트 (안 건드림, 끝나고 그대로인지 확인만)
    private Vector3 gunTopPos, gunTopScale;
    private Quaternion gunTopRot;
    private CameraFeel cameraFeel;
    private CharacterController playerBody;
    private GameEndScreen endScreen;
    private bool tabletOpen;

    // 자세 상태
    private Vector3 posNow, posVel, rotNow, rotVel;
    private FeelImpact impact;      // 충격 스프링 (② 멈출 때 · ④ 탁)
    private Vector3 center;         // 회전 중심 (피벗 기준)
    private float scaleNow = 1f;    // 위치 배율
    private Vector3 cancelPos, cancelRot;

    // 탄창
    private Vector3 magRest, magDirLocal;
    private float magLength;
    private bool magRestActive, magHidden, magTouched;
    private bool magAttached;   // 탄창을 Hand Gun 안으로 옮겼는지

    // 슬라이드
    private Vector3 slideRest;
    private bool slideReleasing;
    private float slideReleaseTimer;
    // 슬라이드 한 번 왕복 (쏘기 · 꺼낼 때 당기기)
    private bool cycleActive;
    private float cycleT, cycleDist, cyclePull, cycleHold, cycleRelease, cyclePost;
    private GunMotion motion;

    // ---------- 자동으로 붙이기 ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitAutoAttach()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        foreach (Player_Interaction pi in FindObjectsByType<Player_Interaction>(FindObjectsSortMode.None))
            if (pi.GetComponent<GunReload>() == null) pi.gameObject.AddComponent<GunReload>();
    }

    // ---------- 준비 ----------
    private void Awake()
    {
        if (interaction == null) interaction = GetComponent<Player_Interaction>();
        playerBody = GetComponent<CharacterController>();
        AmmoInMag = magazineSize;
    }

    // WeaponFeel이 Awake에서 WeaponFeelPivot을 만든 다음, 그 안에 피벗을 하나 더 넣어야 해서 Start에서
    private void Start()
    {
        endScreen = FindAnyObjectByType<GameEndScreen>();

        GameObject gun = interaction != null ? interaction.handGun : null;
        Player_Look look = GetComponent<Player_Look>();
        cam = look != null && look.playerCamera != null ? look.playerCamera
            : (interaction != null && interaction.playerCamera != null ? interaction.playerCamera.transform : null);
        if (gun == null || cam == null || PivotUtil.TopUnder(cam, gun.transform) == null)
        {
            Debug.LogWarning("[GunReload] 카메라 아래에서 Hand Gun을 못 찾아서 재장전을 끕니다.", this);
            enabled = false;
            return;
        }
        cameraFeel = cam.GetComponent<CameraFeel>();

        // 총을 넣을 때(꺼질 때) Animator가 슬라이드 · 공이치기를 원래 값으로 돌려놓게 함
        // 안 그러면 발사 애니메이션 중간(슬라이드가 뒤로 간 순간)에 칸을 바꿨을 때, 다시 꺼낼 때 그 위치를 기본값으로 기억해서
        // 탄이 남아 있는데도 슬라이드가 계속 뒤에 붙어 있음
        foreach (Animator a in gun.GetComponentsInChildren<Animator>(true))
            a.writeDefaultValuesOnDisable = true;

        // WeaponFeelPivot이 있으면 그 안에, 없으면 카메라 바로 아래에 GunReloadPivot
        Transform feel = cam.Find(WeaponFeel.PivotName);
        Transform container = feel != null && PivotUtil.TopUnder(feel, gun.transform) != null ? feel : cam;
        // GunDrawPivot(꺼내기 · 쏘기)이 먼저 생겼으면 그 안에 → WeaponFeelPivot → GunDrawPivot → GunReloadPivot 순서
        Transform draw = container.Find(GunMotion.PivotName);
        if (draw != null && PivotUtil.TopUnder(draw, gun.transform) != null) container = draw;
        pivot = PivotUtil.WrapUnder(container, gun.transform, PivotName);
        gunTop = PivotUtil.TopUnder(pivot, gun.transform);
        gunTopPos = gunTop.localPosition;
        gunTopRot = gunTop.localRotation;
        gunTopScale = gunTop.localScale;

        // 직접 넣은 탄창 · 슬라이드가 "씬의 이 총 안"에 있는 게 아니면 무시하고 다시 찾음
        // (프로젝트 창의 프리팹 파일을 넣으면 복제 탄창이 맵 원점(0,0,0)에 생기고, 프리팹 파일까지 바뀌어 버림)
        magazine = CheckPart(magazine, "Magazine");
        slide = CheckPart(slide, "Slide");

        // 탄창 · 슬라이드 찾기 (프리팹 루트부터. 부모부터 찾으니까 탄창 묶음 전체가 잡힘)
        Transform magLocation = null;
        foreach (Transform t in gunTop.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Contains("Magazine") && t.name.Contains("Location")) { if (magLocation == null) magLocation = t; }
            else if (magazine == null && t.name.Contains("Magazine")) magazine = t;
            if (slide == null && t.name == "Slider") slide = t;
        }
        if (magazine == null && magLocation != null) magazine = MakeFakeMagazine(magLocation);
        if (magazine == null) Debug.LogWarning("[GunReload] 총에서 탄창을 못 찾았어요. 탄창 없이 총만 움직여요.", this);
        if (slide == null) Debug.LogWarning("[GunReload] 총에 따로 된 슬라이드(Slider)가 없어요. 슬라이드 멈춤은 총 전체가 뒤로 튀는 것으로 대신해요.", this);

        if (magazine != null)
        {
            magRest = magazine.localPosition;
            // 총 Variant 프리팹에서 탄창이 꺼져 있음 → 그대로 두면 복제한 탄창도 꺼진 채로 생겨서 안 보임
            if (!magazine.gameObject.activeSelf && showMagazineInGun)
            {
                magazine.gameObject.SetActive(true);
                Debug.Log($"[GunReload] 총 프리팹에서 꺼져 있던 탄창 '{magazine.name}'을 켰어요. (안 보이게 하려면 Show Magazine In Gun 끄기)", magazine);
            }
            magRestActive = magazine.gameObject.activeSelf;
            magHidden = !magRestActive;
            magAttached = magazine.IsChildOf(gun.transform);
        }
        if (slide != null) slideRest = slide.localPosition;   // Animator가 움직이기 전, 앞으로 가 있는 자리
    }

    // 부품 칸 검사: 씬에 있고 이 총 안에 있어야 함. 아니면 경고하고 비움 (→ 이름으로 다시 찾음)
    private Transform CheckPart(Transform part, string label)
    {
        if (part == null) return null;
        if (part.gameObject.scene.IsValid() && part.IsChildOf(gunTop)) return part;
        Debug.LogWarning($"[GunReload] {label} 칸에 '{part.name}'이(가) 들어 있는데, 씬의 총 안에 있는 게 아니에요 " +
                         "(프로젝트 창의 프리팹 파일이거나 다른 오브젝트). 무시하고 총 안에서 자동으로 찾을게요. " +
                         $"Inspector에서 {label} 칸을 비워 두세요.", this);
        return null;
    }

    private void OnEnable() => TabletController.OnTabletToggled += HandleTablet;

    private void OnDisable()
    {
        TabletController.OnTabletToggled -= HandleTablet;
        if (state != State.Idle) { RestoreMagazine(); SnapToRest(); }
    }

    private void HandleTablet(bool open)
    {
        tabletOpen = open;
        if (open) Cancel();
    }

    // ---------- 다른 스크립트에서 부르기 ----------
    // Player_Interaction이 쏘기 직전에 부름. 쏠 수 있으면 탄을 하나 빼고 true
    public bool TryUseRound()
    {
        if (!enabled) return true;   // 재장전 기능이 꺼져 있으면 무한
        if (IsReloading) return false;

        if (SlideLocked)
        {
            if (AmmoInMag > 0) Request();   // 탄은 있는데 슬라이드가 뒤에 → 슬라이드만 풀기
            else
            {
                SoundManager.Play(emptySound);
                if (autoReloadWhenEmpty) Request();
            }
            return false;
        }
        if (AmmoInMag <= 0)
        {
            SoundManager.Play(emptySound);
            if (autoReloadWhenEmpty) Request();
            return false;
        }

        AmmoInMag--;
        if (AmmoInMag == 0) SlideLocked = true;   // 마지막 탄 → 슬라이드가 뒤로 멈춤
        Changed?.Invoke();
        return true;
    }

    // 예비 탄약 추가 (나중에 상점에서 탄약을 팔 때)
    public void AddReserve(int amount)
    {
        if (amount <= 0) return;
        reserveAmmo += amount;
        Changed?.Invoke();
    }

    // 슬라이드를 한 번 뒤로 갔다 앞으로 (쏘기 · 꺼낼 때 당기기). 뒤로 멈춰 있으면(마지막 탄) 무시
    //   distance = 뒤로 가는 거리, pull = 뒤로 가는 시간, hold = 멈춤, release = 앞으로 가는 시간
    //   post = 그다음에도 이 시간 동안 제자리를 붙잡음 (발사 애니메이션이 슬라이드를 또 움직여서 튀지 않게)
    public void PlaySlideCycle(float distance, float pull, float hold, float release, float post)
    {
        if (slide == null || SlideLocked || slideReleasing) return;
        cycleActive = true;
        cycleT = 0f;
        cycleDist = distance;
        cyclePull = Mathf.Max(pull, 0.001f);
        cycleHold = Mathf.Max(hold, 0f);
        cycleRelease = Mathf.Max(release, 0.001f);
        cyclePost = Mathf.Max(post, 0f);
    }

    // 재장전 동작 없이 탄창 가득 (슬라이드도 앞으로)
    public void RefillMagazine()
    {
        AmmoInMag = magazineSize;
        SlideLocked = false;
        Changed?.Invoke();
    }

    // 재장전 취소 (0.15초 동안 부드럽게 원위치)
    public void Cancel()
    {
        if (state != State.Reloading && state != State.Settling) return;
        bool wasReloading = state == State.Reloading;
        state = State.Cancelling;
        time = 0f;
        cancelPos = posNow + impact.Position;
        cancelRot = rotNow + impact.Rotation;
        posVel = rotVel = Vector3.zero;
        impact.Reset();
        timeline.releaseKick.pos.Reset();
        timeline.releaseKick.rot.Reset();
        RestoreMagazine();   // 탄창은 원래대로. 탄 수 · 슬라이드는 지금 상태 그대로
        if (wasReloading) Changed?.Invoke();
    }

    // 클릭이나 R로 "재장전해 줘" (쏘는 중이면 잠깐 기억했다가)
    private void Request() => queuedTimer = 0.4f;

    // ---------- 매 프레임: 입력 ----------
    private void Update()
    {
        if (pivot == null || interaction == null) return;

        bool ended = endScreen != null && endScreen.Shown;
        bool blocked = tabletOpen || ended || !interaction.enabled || !interaction.GunInHand;
        if (blocked)
        {
            if (state == State.Reloading || state == State.Settling) Cancel();
            queuedTimer = 0f;
            return;
        }

        if (IsReloading || state == State.Cancelling) return;

        // 꺼내기 · 넣기 중에는 재장전 안 함 (R · 자동 재장전 둘 다)
        if (motion == null) motion = GetComponent<GunMotion>();
        if (motion != null && motion.Busy) { queuedTimer = 0f; return; }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb[reloadKey].wasPressedThisFrame) Request();
        if (queuedTimer <= 0f) return;
        queuedTimer -= CheatManager.PlayerDelta;

        if (!interaction.GunReady()) return;   // 꺼내는 중 · 쏘는 중이면 기다림
        queuedTimer = 0f;

        if (SlideLocked && AmmoInMag > 0) Begin(Mode.ReleaseOnly);          // 슬라이드만 풀기
        else if (AmmoInMag < FullAmount && (infiniteReserve || reserveAmmo > 0))
            Begin(SlideLocked || AmmoInMag <= 0 ? Mode.Empty : Mode.Tactical);
    }

    private int FullAmount => magazineSize + (chamberBonus && AmmoInMag > 0 ? 1 : 0);

    // ---------- 순서 만들기 ----------
    // 탄창은 원래 Hand Gun 바깥(프리팹 루트 아래)에 있어서, 총을 넣어도(Hand Gun을 꺼도) 혼자 화면에 남음
    // → 총이 idle 자세로 멈춰 있을 때 Hand Gun 안으로 옮김 (꺼내기 애니메이션 도중이면 기울어진 자세가 섞여서 기다림)
    //   옮긴 뒤엔 총을 넣거나 · 태블릿을 열 때 같이 숨고, 꺼내기 애니메이션도 같이 따라감
    //   옮기기 전에는 탄창을 숨겨 둠
    private void UpdateMagazineOwner()
    {
        if (magazine == null || magAttached) return;
        GameObject gun = interaction.handGun;
        bool shown = gun.activeInHierarchy && !tabletOpen;

        if (shown && state == State.Idle && interaction.GunReady())
        {
            magazine.SetParent(gun.transform, true);   // 보이는 자리 그대로 옮김
            magRest = magazine.localPosition;
            magAttached = true;
            magHidden = magRestActive;                 // 아래에서 확실히 맞추게
            SetMagHidden(!magRestActive);
            return;
        }
        if (magazine.gameObject.activeSelf) magazine.gameObject.SetActive(false);
        magHidden = true;
    }

    private void Begin(Mode m)
    {
        UpdateMagazineOwner();   // 같은 프레임에 재장전이 시작돼도 탄창이 먼저 Hand Gun 안에 있게
        mode = m;
        if (mode == Mode.Empty) SlideLocked = true;   // 빈 탄창 재장전은 슬라이드가 뒤에서 시작
        state = State.Reloading;
        time = 0f;
        magFilled = false;
        magTouched = false;
        noiseSeed = UnityEngine.Random.value * 100f;
        center = PivotUtil.CenterIn(pivot, gunTop);   // 피벗 기준이라 피벗 자세와 상관없이 같은 값
        scaleNow = positionScale > 0f ? positionScale : PivotUtil.AutoPositionScale(center);

        // 탄창이 빠지는 방향 (월드 1m가 탄창 부모 기준으로 얼마인지)
        if (magazine != null)
        {
            magazine.localPosition = magRest;
            Vector3 dir = interaction.handGun.transform.TransformDirection(magOutDirection.normalized);
            magLength = MagazineLength(dir);
            magDirLocal = magazine.parent != null ? magazine.parent.InverseTransformVector(dir) : dir;
        }

        ReloadTimeline tl = timeline;
        Vector3 lift = mode == Mode.ReleaseOnly ? Vector3.zero : new Vector3(0f, LiftFor(tl), 0f);   // 탄창이 보이게 올리는 높이

        // 구간 (위치 값은 모두 "배율 적용 전" 단위)
        segments.Clear();
        events.Clear();
        nextEvent = 0;
        if (mode != Mode.ReleaseOnly)
        {
            // 좌우 뒤집기 (Mirror Swing): 위치는 x, 회전은 yaw(y) · roll(z)의 부호를 바꿈
            bool mirror = tl.mirrorSwing;
            Vector3 MP(Vector3 v) => mirror ? new Vector3(-v.x, v.y, v.z) : v;
            Vector3 MR(Vector3 v) => mirror ? new Vector3(v.x, -v.y, -v.z) : v;
            Vector3 leftRot = MR(tl.leftRotation), rightRot = MR(tl.rightRotation), insertRot = MR(tl.insertRotation);
            Vector3 left = MP(tl.leftPosition) + lift;
            Vector3 ready = left + MP(tl.anticipationPosition);
            Vector3 readyRot = leftRot + MR(tl.anticipationRotation);
            Vector3 right = MP(tl.rightPosition) + lift;
            Vector3 insert = MP(tl.insertPosition) + lift;
            float rollKick = mirror ? -tl.cameraRollKick : tl.cameraRollKick;

            Move(tl.leftTime, left, leftRot, ReloadEase.EaseOut);           // ① 왼쪽으로
            Hold(tl.leftHold);                                                       //    멈춤
            Move(tl.anticipationTime, ready, readyRot, ReloadEase.EaseOut);         //    준비 (반대로 살짝)
            tSnapOut = End;
            Move(tl.snapOutTime, right, rightRot, ReloadEase.EaseIn);       // ② 빼기 스냅 → 딱 멈춤
            tDetach = tSnapOut + tl.snapOutTime * tl.magDetachAt;
            Vector3 overPos = (right - ready) * tl.snapOvershoot;
            Vector3 overRot = (rightRot - readyRot) * tl.snapOvershoot;
            AddEvent(tSnapOut, () => PunchCamera(new Vector3(0f, 0f, rollKick), 0f, 0f));
            AddEvent(tDetach, MagOut);
            AddEvent(End, () => impact.Hit(overPos, overRot, tl.impactFrequency, tl.impactDamping));   // 멈출 때 살짝 넘침
            Hold(tl.rightHold);                                                      //    멈춤 (빈 손잡이)
            tMoveIn = End;
            Move(tl.moveInTime, insert, insertRot, ReloadEase.EaseInOut);   // ③ 왼쪽으로 + 새 탄창 올라옴
            tAim = End;
            Hold(tl.aimHold);                                                        //    멈춤 (정렬)
            tSnapIn = End;
            Hold(tl.snapInTime);                                                     // ④ 넣기 스냅 (탄창만 움직임)
            tImpact = End;
            AddEvent(tImpact, MagIn);                                                //    "탁"
            Hold(tl.impactHold);                                                     //    충격 정리
        }
        if (mode != Mode.Tactical)
        {
            if (mode == Mode.Empty) Hold(tl.preReleaseHold);
            float relStart = End;
            SpringTo(tl.release.duration, tl.release.position, tl.release.rotation, tl.release.ease, tl.release.arrive);   // 슬라이드 멈치 (기존 동작)
            AddEvent(relStart + tl.release.duration * tl.releaseAt, SlideRelease);
        }
        float readyAt = End;
        float settle = mode == Mode.Empty ? tl.emptySettleTime : mode == Mode.Tactical ? tl.tacticalSettleTime : tl.releaseOnlySettleTime;
        SpringTo(settle, Vector3.zero, Vector3.zero, ReloadEase.EaseInOut, tl.settleArrive);   // ⑤ 제자리
        totalTime = End;
        events.Sort((a, b) => a.time.CompareTo(b.time));

        // 쏠 수 있게 되는 때: 택티컬 = 제자리 시작 / 빈 탄창 · 슬라이드만 = 슬라이드가 앞으로 다 간 순간
        readyTime = readyAt;
        if (mode != Mode.Tactical)
        {
            foreach (TimedEvent e in events)
                if (e.run == (Action)SlideRelease) readyTime = Mathf.Min(readyTime, e.time + tl.slideReleaseTime);
        }

        // 구간별 소리가 하나도 없으면 통째로 된 재장전 소리 하나만
        splitSounds = SoundManager.Has(magOutSound) || SoundManager.Has(magInSound) ||
                      SoundManager.Has(slideReleaseSound) || SoundManager.Has(slideRackSound);
        if (!splitSounds && mode != Mode.ReleaseOnly) SoundManager.Play(fallbackSound);

        Changed?.Invoke();
    }

    private float End => segments.Count > 0 ? segments[segments.Count - 1].start + segments[segments.Count - 1].duration : 0f;
    private Vector3 LastPos => segments.Count > 0 ? segments[segments.Count - 1].toPos : posNow;
    private Vector3 LastRot => segments.Count > 0 ? segments[segments.Count - 1].toRot : rotNow;

    private void Move(float duration, Vector3 toPos, Vector3 toRot, ReloadEase ease) =>
        AddSegment(duration, toPos, toRot, ease, 1f, false, false);

    private void Hold(float duration) =>
        AddSegment(duration, LastPos, LastRot, ReloadEase.Linear, 1f, false, true);

    private void SpringTo(float duration, Vector3 toPos, Vector3 toRot, ReloadEase ease, float arrive) =>
        AddSegment(duration, toPos, toRot, ease, arrive, true, false);

    private void AddSegment(float duration, Vector3 toPos, Vector3 toRot, ReloadEase ease, float arrive, bool spring, bool hold)
    {
        segments.Add(new Segment
        {
            start = End, duration = Mathf.Max(0.001f, duration),
            fromPos = LastPos, fromRot = LastRot, toPos = toPos, toRot = toRot,
            ease = ease, arrive = arrive, spring = spring, hold = hold,
        });
    }

    private void AddEvent(float t, Action run) => events.Add(new TimedEvent { time = t, run = run });

    // 재장전 중에 더 올릴 높이 (배율 적용 전 단위). 기본 0 = 원래 위치에서 장전
    private float LiftFor(ReloadTimeline tl) => extraLift / scaleNow;

    // ---------- 순간 일들 ----------
    // ② 탄창이 분리되어 오른쪽 아래로 날아감
    private void MagOut()
    {
        if (splitSounds) SoundManager.Play(magOutSound);
        if (magazine == null) return;
        magTouched = true;

        if (dropMagazine)
        {
            bool empty = mode == Mode.Empty;
            GameObject drop = Instantiate(magazine.gameObject, magazine.position, magazine.rotation);
            drop.SetActive(true);                              // 원본이 꺼져 있었어도 보이게
            drop.name = "Dropped Magazine";
            drop.tag = "Untagged";                             // 줍기 대상(Item)이 아니게
            drop.transform.localScale = magazine.lossyScale;    // 부모 없이 만들어서 크기를 맞춰 줌
            foreach (Transform t in drop.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 0;                        // Default 레이어
            foreach (Renderer r in drop.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;   // 그림자 끄기
            }

            if (drop.GetComponentInChildren<Collider>(true) == null)
                foreach (MeshFilter mf in drop.GetComponentsInChildren<MeshFilter>(true))
                    mf.gameObject.AddComponent<BoxCollider>();
            Collider[] gunColliders = gunTop.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in drop.GetComponentsInChildren<Collider>(true))
            {
                if (playerBody != null) Physics.IgnoreCollision(c, playerBody);   // 플레이어 몸에 안 부딪히게
                foreach (Collider g in gunColliders) Physics.IgnoreCollision(c, g); // 총에도 안 부딪히게
            }

            Rigidbody rb = drop.GetComponent<Rigidbody>();
            if (rb == null) rb = drop.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.mass = empty ? 0.12f : 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // 플레이어 키가 8m라 약 7m를 떨어지며 빨라짐 → 보통 충돌 검사로는 얇은 바닥을 뚫고 맵 아래까지 떨어짐
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            DroppedMagazine dm = drop.AddComponent<DroppedMagazine>();   // 처음 0.3초는 중력 약하게
            dm.floatTime = floatTime;
            dm.floatGravity = floatGravity;
            if (playerBody != null)   // 바닥에 콜라이더가 없거나 뚫고 지나가도 플레이어 발 높이에서 멈춤
                dm.floorY = playerBody.transform.TransformPoint(playerBody.center - Vector3.up * (playerBody.height * 0.5f)).y;
#if UNITY_EDITOR
            if (!loggedDrop)
            {
                loggedDrop = true;
                Debug.Log($"[GunReload] 빠진 탄창 '{magazine.name}' 생성 위치 {drop.transform.position} · 카메라에서 {Vector3.Distance(drop.transform.position, cam.position):F2}m · " +
                          $"크기 {drop.transform.localScale} · 바닥(발) 높이 {dm.floorY:F2} (처음 한 번만 표시. 거리가 몇 m 정도면 정상)", drop);
            }
#endif

            // 오른쪽 아래로 + 플레이어가 걷던 속도
            Vector3 dir = (cam.right * 0.8f - cam.up * 0.6f).normalized;
            Vector3 carry = playerBody != null ? playerBody.velocity : Vector3.zero;
            rb.linearVelocity = dir * detachSpeed * (empty ? emptyDetachMultiplier : 1f) + carry;
            rb.angularVelocity = -cam.forward * detachSpin + UnityEngine.Random.insideUnitSphere;
            Destroy(drop, dropLifetime);
        }
        SetMagHidden(true);   // 새 탄창이 올라올 때까지 손잡이 아래는 비어 있음
    }

    // ④ 새 탄창 "탁" + 탄 채우기 (슬라이드는 그대로. 빈 탄창이면 철컥 전까지 뒤에 있어서 못 쏨)
    private void MagIn()
    {
        if (splitSounds) SoundManager.Play(magInSound);
        impact.Hit(timeline.impactPosition, timeline.impactRotation, timeline.impactFrequency, timeline.impactDamping);
        PunchCamera(new Vector3(-timeline.cameraImpactPunch, 0f, 0f), 0f, 0f);
        FillMagazine();
    }

    // 슬라이드 멈치 → 슬라이드가 앞으로 "철컥" (기존 동작 그대로)
    private void SlideRelease()
    {
        if (splitSounds) SoundManager.Play(SoundManager.Has(slideReleaseSound) ? slideReleaseSound : slideRackSound);
        Kick(timeline.releaseKick);   // 슬라이드가 없어도 총 전체가 뒤로 튀어서 대신함
        slideReleasing = true;
        slideReleaseTimer = 0f;
    }

    private void PunchCamera(Vector3 euler, float shake, float shakeTime)
    {
        if (cameraFeel != null) cameraFeel.Punch(euler, shake, shakeTime);   // 고정 시점이면 CameraFeel이 무시함
    }

    private void Kick(ReloadKick k)
    {
        Vector3 limitPos = new Vector3(Mathf.Abs(k.position.x), Mathf.Abs(k.position.y), Mathf.Abs(k.position.z)) * 2f;
        Vector3 limitRot = new Vector3(Mathf.Abs(k.rotation.x), Mathf.Abs(k.rotation.y), Mathf.Abs(k.rotation.z)) * 2f;
        k.pos.Add(k.position, limitPos);
        k.rot.Add(k.rotation, limitRot);
        if (k.cameraPunch != Vector3.zero || k.cameraShake > 0f) PunchCamera(k.cameraPunch, k.cameraShake, k.cameraShakeTime);
    }

    private void FillMagazine()
    {
        int full = magazineSize + (chamberBonus && mode == Mode.Tactical ? 1 : 0);
        int need = Mathf.Max(0, full - AmmoInMag);
        int take = infiniteReserve ? need : Mathf.Min(need, reserveAmmo);
        if (!infiniteReserve) reserveAmmo -= take;
        AmmoInMag += take;
        magFilled = true;
        Changed?.Invoke();
    }

    // ---------- 매 프레임: 움직이기 (Animator가 움직인 다음이라 슬라이드를 덮어쓸 수 있음) ----------
    private void LateUpdate()
    {
        if (pivot == null) return;

        float dt = CheatManager.PlayerDelta * reloadSpeed;   // 시간 정지 중에도 흐르는 시간
#if UNITY_EDITOR
        if (slowMotionPreview) dt *= 0.25f;
#endif
        if (state == State.Cancelling) UpdateCancel(dt);
        else if (state != State.Idle) UpdateSequence(dt);

        UpdateMagazineOwner();
        UpdateSlide(dt);
    }

    private void UpdateSequence(float dt)
    {
        time += dt;

        // 지금 구간 찾기 (끝났으면 마지막 구간)
        Segment s = segments[segments.Count - 1];
        foreach (Segment seg in segments)
            if (time < seg.start + seg.duration) { s = seg; break; }
        float u = Mathf.Clamp01((time - s.start) / s.duration);
        float k = Ease(s.ease, Mathf.Clamp01(u / Mathf.Max(s.arrive, 0.01f)));
        Vector3 goalPos = Vector3.LerpUnclamped(s.fromPos, s.toPos, k);
        Vector3 goalRot = Vector3.LerpUnclamped(s.fromRot, s.toRot, k);

        // 슬라이드 철컥 킥 (기존 동작: 목표에 더해서 스프링으로 따라감)
        ReloadKick rk = timeline.releaseKick;
        rk.pos.Step(rk.riseTime, rk.returnTime, dt);
        rk.rot.Step(rk.riseTime, rk.returnTime, dt);

        if (s.spring)
        {
            // 슬라이드 멈치 · 제자리: 스프링으로 따라감 (살짝 넘쳤다가 정확히 도착)
            posNow = FeelSpring.Bouncy(posNow, ref posVel, goalPos + rk.pos.Value, springFrequency, springDamping, dt);
            rotNow = FeelSpring.Bouncy(rotNow, ref rotVel, goalRot + rk.rot.Value, springFrequency, springDamping, dt);
        }
        else
        {
            // 나머지: 구간마다 따로 보간한 값 그대로 (덩실거림 없음). 속도는 다음 스프링 구간을 위해 기억
            if (dt > 0f)
            {
                posVel = (goalPos - posNow) / dt;
                rotVel = (goalRot - rotNow) / dt;
            }
            posNow = goalPos;
            rotNow = goalRot;
        }

        // 충격 스프링 (② 멈출 때 · ④ 탁)
        impact.Step(timeline.impactFrequency, timeline.impactDamping, dt);
        Vector3 pos = posNow + impact.Position;
        Vector3 rot = rotNow + impact.Rotation;

        // 멈춤 구간: 아주 작은 떨림
        if (s.hold && state == State.Reloading)
        {
            float env = Mathf.Sin(u * Mathf.PI);
            float n = time * timeline.holdJitterSpeed;
            pos += new Vector3(Noise(n, noiseSeed), Noise(n, noiseSeed + 5f), 0f) * (timeline.holdJitterPosition * env);
            rot += new Vector3(Noise(n, noiseSeed + 9f), Noise(n, noiseSeed + 13f), Noise(n, noiseSeed + 21f)) * (timeline.holdJitterRotation * env);
        }
        ApplyPivot(pos, rot);

        // 이번 프레임까지 지나간 순간 일들 (프레임이 길어도 빠짐없이 차례로)
        while (nextEvent < events.Count && time >= events[nextEvent].time)
            events[nextEvent++].run();

        UpdateMagazine();

        if (state == State.Reloading && time >= totalTime)
        {
            if (mode != Mode.ReleaseOnly && !magFilled) FillMagazine();   // 혹시 ④를 못 거쳤어도 탄은 꼭
            state = State.Settling;   // 마지막 출렁임만 남음
            settleTime = 0f;
            RestoreMagazine();
            Changed?.Invoke();
        }
        else if (state == State.Settling)
        {
            settleTime += dt;
            bool still = posNow.sqrMagnitude < 1e-8f && rotNow.sqrMagnitude < 1e-4f &&
                         posVel.sqrMagnitude < 1e-6f && rotVel.sqrMagnitude < 1e-2f &&
                         impact.IsZero && rk.pos.IsZero && rk.rot.IsZero;
            if (still || settleTime > 0.6f)
            {
#if UNITY_EDITOR
                if (!still && (posNow.magnitude > 0.001f || rotNow.magnitude > 0.001f))
                    Debug.LogWarning($"[GunReload] 재장전 뒤 스프링이 다 안 멈췄어요 (위치 {posNow.magnitude:F4}m · 회전 {rotNow.magnitude:F4}도). Spring 값을 확인하세요.", this);
#endif
                SnapToRest();
            }
        }
    }

    private void UpdateCancel(float dt)
    {
        time += dt;
        float k = Smooth(time / Mathf.Max(cancelTime, 0.001f));
        posNow = Vector3.Lerp(cancelPos, Vector3.zero, k);
        rotNow = Vector3.Lerp(cancelRot, Vector3.zero, k);
        ApplyPivot(posNow, rotNow);
        if (k >= 1f) SnapToRest();
    }

    // "기준값(0) + 오프셋"을 매 프레임 새로 계산해서 대입 (+= 안 씀)
    private void ApplyPivot(Vector3 pos, Vector3 rot)
    {
        PivotUtil.Apply(pivot, pos * scaleNow, Quaternion.Euler(rot), center);
    }

    // 정확히 원위치 (위치 0 · 회전 0)
    private void SnapToRest()
    {
        state = State.Idle;
        posNow = posVel = rotNow = rotVel = Vector3.zero;
        impact.Reset();
        timeline.releaseKick.pos.Reset();
        timeline.releaseKick.rot.Reset();
        if (pivot != null)
        {
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
        }
#if UNITY_EDITOR
        // 총 프리팹 루트는 절대 안 건드려야 함 → 처음 값과 다르면 경고
        if (gunTop != null &&
            ((gunTop.localPosition - gunTopPos).magnitude > 0.001f ||
             Quaternion.Angle(gunTop.localRotation, gunTopRot) > 0.001f ||
             (gunTop.localScale - gunTopScale).magnitude > 0.001f))
            Debug.LogWarning("[GunReload] 재장전 뒤 총 프리팹 루트의 위치 · 회전 · 크기가 바뀌었어요. 다른 스크립트가 총을 움직이고 있는지 확인하세요.", gunTop);
#endif
        Changed?.Invoke();
    }

    // ---------- 탄창 ----------
    //   ② 스냅 시작 ~ 분리 : 손잡이에서 아래로 미끄러져 나옴 (보임)
    //   분리 ~ ③ 시작       : 손잡이 아래가 비어 있음
    //   ③ 이동              : 새 탄창이 화면 아래에서 올라와 손잡이 바로 아래에 정렬
    //   멈춤                : 정렬한 채 보임
    //   ④ 넣기 스냅         : 가속하며 쑥 들어감
    private void UpdateMagazine()
    {
        if (magazine == null || mode == Mode.ReleaseOnly || state != State.Reloading) return;
        ReloadTimeline tl = timeline;
        float meters;   // 손잡이에서 아래로 몇 m (월드)

        if (time < tSnapOut) return;
        if (time < tDetach)
        {
            meters = tl.magSlideOut * scaleNow * Ease(ReloadEase.EaseIn, (time - tSnapOut) / Mathf.Max(tDetach - tSnapOut, 0.001f));
            SetMagHidden(false);
        }
        else if (time < tMoveIn)
        {
            SetMagHidden(true);
            return;
        }
        else if (time < tAim)
        {
            float u = (time - tMoveIn) / Mathf.Max(tAim - tMoveIn, 0.001f);
            meters = Mathf.Lerp(tl.newMagStartDistance * magLength, tl.magAlignDistance * scaleNow, Ease(ReloadEase.EaseOut, u));
            SetMagHidden(false);
        }
        else if (time < tSnapIn)
        {
            meters = tl.magAlignDistance * scaleNow;
            SetMagHidden(false);
        }
        else if (time < tImpact)
        {
            float u = (time - tSnapIn) / Mathf.Max(tImpact - tSnapIn, 0.001f);
            meters = Mathf.Lerp(tl.magAlignDistance * scaleNow, 0f, Ease(ReloadEase.EaseIn, u));
            SetMagHidden(false);
        }
        else
        {
            if (magTouched) RestoreMagazine();
            magTouched = false;
            return;
        }
        magazine.localPosition = magRest + magDirLocal * meters;
    }

    private void SetMagHidden(bool hide)
    {
        if (magHidden == hide || magazine == null) return;
        magHidden = hide;
        magazine.gameObject.SetActive(!hide);
    }

    // 탄창을 원래 자리 · 원래 상태로
    private void RestoreMagazine()
    {
        if (magazine == null) return;
        magazine.localPosition = magRest;
        magHidden = magRestActive;   // 확실히 다시 맞추게
        SetMagHidden(!magRestActive);
    }

    // ---------- 슬라이드: 뒤로 멈춤 / 앞으로 "철컥" ----------
    // Animator(발사 애니메이션)가 매 프레임 슬라이드를 제자리로 돌려놓기 때문에, 멈춰 있는 동안엔 계속 덮어씀
    private void UpdateSlide(float dt)
    {
        if (slide == null && !slideReleasing) return;

        // 총이 꺼져 있는 동안엔 항상 제자리에 둠: 다시 꺼낼 때 Animator가 "지금 위치"를 기본값으로 기억하기 때문
        // (뒤에 있는 채로 꺼내면 철컥 뒤에도, 탄이 남아 있어도 슬라이드가 계속 뒤에 붙어 버림)
        bool shown = interaction != null && interaction.handGun != null && interaction.handGun.activeInHierarchy;
        if (!shown)
        {
            if (slideReleasing)
            {
                slideReleasing = false;   // 철컥 도중에 넣었으면 바로 끝난 걸로
                SlideLocked = false;
                Changed?.Invoke();
            }
            cycleActive = false;
            if (slide != null) slide.localPosition = slideRest;
            return;
        }

        if (slideReleasing)
        {
            slideReleaseTimer += dt;
            float u = slideReleaseTimer / Mathf.Max(timeline.slideReleaseTime, 0.001f);
            if (slide != null) slide.localPosition = slideRest + new Vector3(0f, 0f, -slideBackDistance) * (1f - Ease(ReloadEase.EaseIn, u));
            if (u >= 1f)
            {
                slideReleasing = false;
                SlideLocked = false;   // 이제 쏠 수 있음
                if (slide != null) slide.localPosition = slideRest;
                Changed?.Invoke();
            }
        }
        else if (SlideLocked && slide != null)
        {
            cycleActive = false;
            slide.localPosition = slideRest + new Vector3(0f, 0f, -slideBackDistance);   // 뒤로 멈춰 있음
        }
        else if (cycleActive && slide != null)
        {
            // 한 번 왕복: 뒤로 → 멈춤 → 빠르게 앞으로 "철컥" → 잠깐 제자리 붙잡기
            cycleT += dt;
            float s;
            if (cycleT < cyclePull) s = Ease(ReloadEase.EaseInOut, cycleT / cyclePull);
            else if (cycleT < cyclePull + cycleHold) s = 1f;
            else s = 1f - Ease(ReloadEase.EaseIn, (cycleT - cyclePull - cycleHold) / cycleRelease);
            slide.localPosition = slideRest + new Vector3(0f, 0f, -cycleDist) * s;
            if (cycleT >= cyclePull + cycleHold + cycleRelease + cyclePost)
            {
                cycleActive = false;
                slide.localPosition = slideRest;
            }
        }
    }

    // 탄창이 dir 방향으로 얼마나 긴지 (월드 m)
    private float MagazineLength(Vector3 dir)
    {
        if (!PivotUtil.BoundsIn(magazine, magazine, out Bounds b)) return 0.1f;
        Vector3 size = Vector3.Scale(b.size, magazine.lossyScale);
        Vector3 d = magazine.InverseTransformDirection(dir).normalized;
        return Mathf.Abs(size.x * d.x) + Mathf.Abs(size.y * d.y) + Mathf.Abs(size.z * d.z);
    }

    // 탄창이 따로 없는 총: 손잡이 아래(Magazine_Location)에 어두운 회색 상자 탄창을 만들어 끼워 둠
    private Transform MakeFakeMagazine(Transform location)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Fake Magazine";
        Destroy(box.GetComponent<Collider>());
        box.layer = location.gameObject.layer;
        box.transform.SetParent(location, false);
        box.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        box.transform.localScale = new Vector3(0.025f, 0.08f, 0.03f);   // 총 모델 단위 (총 길이 약 0.2)
        Renderer gunRenderer = gunTop.GetComponentInChildren<Renderer>();
        Renderer r = box.GetComponent<Renderer>();
        if (gunRenderer != null) r.sharedMaterial = gunRenderer.sharedMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color gray = new Color(0.18f, 0.18f, 0.2f);
        block.SetColor("_BaseColor", gray);   // URP
        block.SetColor("_Color", gray);
        r.SetPropertyBlock(block);
        return box.transform;
    }

    // ---------- 계산 도우미 ----------
    private static float Ease(ReloadEase e, float t)
    {
        t = Mathf.Clamp01(t);
        switch (e)
        {
            case ReloadEase.EaseIn: return t * t * t;   // EaseInCubic: 가속하다가 딱 멈춤
            case ReloadEase.EaseOut: { float u = 1f - t; return 1f - u * u * u; }
            case ReloadEase.EaseInOut: return t * t * (3f - 2f * t);
            default: return t;
        }
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // -1 ~ 1 사이의 부드러운 랜덤 값
    private static float Noise(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;
}
