using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 치트 (태블릿 게임 설정의 치트 카드 + 단축키)
//   돈 무한         : 돈이 줄면 바로 다시 채움. 끄면 켜기 전 돈으로 돌아감 (태블릿 버튼으로만)
//   시간 정지 (F1) : 세상이 멈춤 (손님 · 시계 · 물리 · 배달). 나만 움직임
//                    - 걷기 · 시점 · 줍기 · 진열 · 스캔 · 총 · 태블릿은 그대로
//                    - 멈춘 동안 쏜 손님 · 던진 물건은 공중에 멈춰 있다가 풀리면 날아감
//   시간 조절       : 태블릿에서 켜 둔 동안  F3 꾹 = 시계 앞으로 감기,  F2 꾹 = 뒤로 감기
//                    끝까지 밀면 바로 하루가 끝남 (남은 손님을 안 기다림)
//   다시 소환 (F4) : 플레이어가 어딘가에 끼었을 때 시작 자리로 돌려보냄 (치트로 안 침 · 순위 그대로)
//                    (시계만 움직임. 손님 · 돈은 그대로. 준비 시간 중이면 남은 준비 시간을 감음)
// 버튼은 줄마다 켜기 / 끄기 두 개. 켜져 있으면 끄기 버튼만, 꺼져 있으면 켜기 버튼만 보임 (스크립트가 바꿔줌)
// 치트를 한 번이라도 켜면 그 판은 순위표에 안 남음 (GameEndScreen이 확인)
//   단, 관리자 설정 "치트를 써도 기록 인정"을 켜면 치트를 써도 순위표 · 최고 점수에 남음
// 단축키가 태블릿을 닫아도 먹게, 항상 켜져 있는 오브젝트(Canvas(Always))에 붙이기
public class CheatManager : MonoBehaviour
{
    // 이번 판에 치트를 쓴 적이 있는지 (Game 씬이 새로 열릴 때마다 false로)
    public static bool UsedThisGame { get; private set; }
    // 관리자 설정: 치트를 써도 기록 인정 (AdminSettings가 넣어줌 · 판이 바뀌어도 유지)
    public static bool AllowRecordWithCheat;
    // 이번 판 기록을 남기면 안 되는지 (치트를 썼고, 기록 인정이 꺼져 있을 때)
    public static bool RecordBlocked => UsedThisGame && !AllowRecordWithCheat;
    // 시간 정지 중인지
    public static bool TimeStopped { get; private set; }
    // 플레이어 쪽 스크립트가 쓰는 시간 (시간 정지 중에도 흘러감)
    public static float PlayerDelta => TimeStopped ? Time.unscaledDeltaTime : Time.deltaTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        UsedThisGame = false;
        TimeStopped = false;
    }

    [Header("버튼 (줄마다 켜기 · 끄기)")]
    public Button moneyOn;
    public Button moneyOff;
    public Button stopOn;
    public Button stopOff;
    public Button timeOn;
    public Button timeOff;

    [Header("연결 (비우면 자동으로 찾음)")]
    public GameObject CheatMessage;
    public Player_State state;
    public CustomerSpawner spawner;
    public GameEndScreen endScreen;
    [Tooltip("단축키로 켜고 끌 때 잠깐 뜨는 안내 글자 (선택)")]
    public TMP_Text notice;
    [Tooltip("시간 정지 중에만 켜지는 효과 (선택 · 예: 채도를 뺀 Volume, 화면 테두리 이미지)")]
    public GameObject stopEffect;
    [Tooltip("치트 카드의 \"치트 활성화 클릭 시 미기록\" 글자 (선택). 기록 인정 설정에 맞춰 글자를 바꿔줌")]
    public TMP_Text recordRuleText;
    public string recordBlockedRule = "치트 활성화 클릭 시 미기록";
    public string recordAllowedRule = "치트를 써도 기록돼요";

    [Header("단축키")]
    [Tooltip("시간 정지 켜기 / 끄기")]
    public Key timeStopKey = Key.F1;
    [Tooltip("시간 조절이 켜져 있을 때 꾹 누르면 앞으로 감기 (시간 증가)")]
    public Key clockForwardKey = Key.F3;
    [Tooltip("시간 조절이 켜져 있을 때 꾹 누르면 뒤로 감기 (시간 감소)")]
    public Key clockBackKey = Key.F2;

    [Header("끼었을 때 다시 소환")]
    [Tooltip("누르면 플레이어를 소환 자리로 옮김")]
    public Key respawnKey = Key.F4;
    [Tooltip("소환 자리 (비우면 게임 시작할 때 플레이어가 서 있던 자리)")]
    public Transform respawnPoint;

    [Header("설정")]
    [Tooltip("돈 무한일 때 채워두는 돈")]
    public int infiniteMoney = 9999999;
    [Tooltip("시간 조절: 1초 꾹 누를 때 시계가 움직이는 게임 시간(초). 하루 300초면 30 = 10초에 하루 전체")]
    public float scrubSpeed = 30f;
    public float noticeTime = 1.5f;

    [Header("소리 (선택)")]
    public Sfx stopSound = Sfx.None;
    public Sfx resumeSound = Sfx.None;

    public bool MoneyOn { get; private set; }
    public bool TimeMode { get; private set; }

    private int moneyBefore;
    private float noticeTimer;

    // 시간 정지 중에도 움직여야 하는 Animator (플레이어 손 · 스캐너 · 총 · UI) 원래 설정
    private readonly Dictionary<Animator, AnimatorUpdateMode> unscaled = new Dictionary<Animator, AnimatorUpdateMode>();

    private void Awake()
    {
        UsedThisGame = false;
        TimeStopped = false;
        if (state == null) state = FindAnyObjectByType<Player_State>();
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (endScreen == null) endScreen = FindAnyObjectByType<GameEndScreen>();

        Hook(moneyOn, () => { if (!MoneyOn) ToggleMoney(); });
        Hook(moneyOff, () => { if (MoneyOn) ToggleMoney(); });
        Hook(stopOn, () => { if (!TimeStopped) ToggleStop(); });
        Hook(stopOff, () => { if (TimeStopped) ToggleStop(); });
        Hook(timeOn, () => { if (!TimeMode) ToggleTime(); });
        Hook(timeOff, () => { if (TimeMode) ToggleTime(); });

        if (notice != null) notice.text = "";
        if (stopEffect != null) stopEffect.SetActive(false);
        if (recordRuleText != null) recordRuleText.text = AllowRecordWithCheat ? recordAllowedRule : recordBlockedRule;
        Refresh();
    }

    private void OnDestroy()
    {
        if (TimeStopped) Time.timeScale = 1f;   // 씬을 나가면 시간 원래대로
        TimeStopped = false;
    }

    private void Update()
    {
        bool ended = endScreen != null && endScreen.Shown;
        if (ended && TimeStopped) SetStop(false, false);   // 영수증은 멈춘 시간에서 안 나오게

        Keyboard kb = Keyboard.current;
        if (kb != null && !ended)
        {
            if (kb[timeStopKey].wasPressedThisFrame) ToggleStop();
            if (kb[respawnKey].wasPressedThisFrame) Respawn();

            // 시간 조절: 꾹 누르는 동안 시계 감기 (시간 정지 중에도 됨)
            if (TimeMode && spawner != null)
            {
                float dir = (kb[clockForwardKey].isPressed ? 1f : 0f) - (kb[clockBackKey].isPressed ? 1f : 0f);
                if (dir != 0f) spawner.ShiftClock(dir * scrubSpeed * Time.unscaledDeltaTime);
            }
        }

        // 돈 무한: 쓰자마자 다시 채움
        if (MoneyOn && state != null && state.money < infiniteMoney) state.Earn(infiniteMoney - state.money);

        if (notice != null && noticeTimer > 0f)
        {
            noticeTimer -= Time.unscaledDeltaTime;
            if (noticeTimer <= 0f) notice.text = "";
        }
    }

    // ---------- 돈 무한 ----------
    public void ToggleMoney()
    {
        if (state == null) return;
        MoneyOn = !MoneyOn;
        if (MoneyOn)
        {
            moneyBefore = state.money;
            Used();
        }
        else if (state.money > moneyBefore) state.TrySpend(state.money - moneyBefore);   // 켜기 전 돈으로
        Feedback(MoneyOn ? "돈 무한 켜짐" : "돈 무한 꺼짐");
    }

    // ---------- 시간 정지 ----------
    public void ToggleStop()
    {
        SetStop(!TimeStopped, true);
    }

    private void SetStop(bool on, bool feedback)
    {
        if (TimeStopped == on) return;
        TimeStopped = on;

        if (on)
        {
            Used();
            // 나와 상관있는 Animator만 계속 움직이게 (손님 Animator는 같이 멈춤)
            unscaled.Clear();
            foreach (Animator a in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (a == null || a.GetComponentInParent<Customer>(true) != null) continue;
                unscaled[a] = a.updateMode;
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            Time.timeScale = 0f;   // 손님 · 시계 · 물리 · 배달 · 손님 애니메이션 전부 멈춤
            SoundManager.Play(stopSound);
        }
        else
        {
            Time.timeScale = 1f;
            foreach (KeyValuePair<Animator, AnimatorUpdateMode> p in unscaled)
                if (p.Key != null) p.Key.updateMode = p.Value;
            unscaled.Clear();
            SoundManager.Play(resumeSound);
        }

        if (stopEffect != null) stopEffect.SetActive(on);
        if (feedback) Feedback(on ? "시간 정지!" : "시간이 다시 흐름");
        else Refresh();
    }

    // ---------- 시간 조절 ----------
    public void ToggleTime()
    {
        TimeMode = !TimeMode;
        if (TimeMode) Used();
        Feedback(TimeMode ? "시간 조절 켜짐 · F3 앞으로  F2 뒤로 (꾹)" : "시간 조절 꺼짐");
    }

    // ---------- 다시 소환 ----------
    private Transform player;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool startSaved;

    private void Start()
    {
        Player_movement pm = FindAnyObjectByType<Player_movement>();
        if (pm == null) return;
        player = pm.transform;
        startPos = player.position;
        startRot = player.rotation;
        startSaved = true;
    }

    public void Respawn()
    {
        if (player == null || (!startSaved && respawnPoint == null)) return;
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPos;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRot;

        // CharacterController가 켜져 있으면 위치를 바로 못 바꿔서 잠깐 끄고 옮김
        CharacterController cc = player.GetComponent<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false;
        player.SetPositionAndRotation(pos, rot);
        if (ccOn) cc.enabled = true;

        SoundManager.Play(Sfx.ButtonClick);
        if (notice != null)
        {
            notice.text = "시작 자리로 돌아왔어요";
            noticeTimer = noticeTime;
        }
    }

    // ---------- 공통 ----------
    private void Used()
    {
        UsedThisGame = true;
        // "기록 안 남음" 안내는 기록이 실제로 막힐 때만
        if (CheatMessage != null && !AllowRecordWithCheat) CheatMessage.SetActive(true);
    }

    private void Feedback(string message)
    {
        SoundManager.Play(Sfx.ButtonClick);
        Refresh();
        if (notice != null)
        {
            notice.text = message;
            noticeTimer = noticeTime;
        }
    }

    // 켜져 있으면 끄기 버튼만, 꺼져 있으면 켜기 버튼만
    private void Refresh()
    {
        Show(moneyOn, moneyOff, MoneyOn);
        Show(stopOn, stopOff, TimeStopped);
        Show(timeOn, timeOff, TimeMode);
    }

    private static void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b != null) b.onClick.AddListener(action);
    }

    private static void Show(Button onButton, Button offButton, bool on)
    {
        if (onButton != null) onButton.gameObject.SetActive(!on);
        if (offButton != null) offButton.gameObject.SetActive(on);
    }
}
