using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 치트 (태블릿 게임 설정의 치트 카드 + 단축키)
//   돈 무한   (F1) : 돈이 줄면 바로 다시 채움. 끄면 켜기 전 돈으로 돌아감
//   시간 정지 (F2) : 세상이 멈춤 (손님 · 시계 · 물리 · 배달). 나만 움직임
//                    - 걷기 · 시점 · 줍기 · 진열 · 스캔 · 총 · 태블릿은 그대로
//                    - 멈춘 동안 쏜 손님 · 던진 물건은 공중에 멈춰 있다가 풀리면 날아감
//   시간 조절 (F3) : 켜져 있는 동안  ] 꾹 = 시계 앞으로 감기,  [ 꾹 = 뒤로 감기
//                    (시계만 움직임. 손님 · 돈은 그대로. 준비 시간 중이면 남은 준비 시간을 감음)
// 버튼은 줄마다 켜기 / 끄기 두 개. 켜져 있으면 끄기 버튼만, 꺼져 있으면 켜기 버튼만 보임 (스크립트가 바꿔줌)
// 치트를 한 번이라도 켜면 그 판은 순위표에 안 남음 (GameEndScreen이 확인)
// 단축키가 태블릿을 닫아도 먹게, 항상 켜져 있는 오브젝트(Canvas(Always))에 붙이기
public class CheatManager : MonoBehaviour
{
    // 이번 판에 치트를 쓴 적이 있는지 (Game 씬이 새로 열릴 때마다 false로)
    public static bool UsedThisGame { get; private set; }
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
    public Player_State state;
    public CustomerSpawner spawner;
    public GameEndScreen endScreen;
    [Tooltip("단축키로 켜고 끌 때 잠깐 뜨는 안내 글자 (선택)")]
    public TMP_Text notice;
    [Tooltip("시간 정지 중에만 켜지는 효과 (선택 · 예: 채도를 뺀 Volume, 화면 테두리 이미지)")]
    public GameObject stopEffect;

    [Header("단축키")]
    public Key moneyKey = Key.F1;
    public Key stopKey = Key.F2;
    public Key timeKey = Key.F3;
    [Tooltip("시간 조절이 켜져 있을 때 꾹 누르면 뒤로 감기")]
    public Key rewindKey = Key.LeftBracket;
    [Tooltip("시간 조절이 켜져 있을 때 꾹 누르면 앞으로 감기")]
    public Key forwardKey = Key.RightBracket;

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
            if (kb[moneyKey].wasPressedThisFrame) ToggleMoney();
            if (kb[stopKey].wasPressedThisFrame) ToggleStop();
            if (kb[timeKey].wasPressedThisFrame) ToggleTime();

            // 시간 조절: 꾹 누르는 동안 시계 감기 (시간 정지 중에도 됨)
            if (TimeMode && spawner != null)
            {
                float dir = (kb[forwardKey].isPressed ? 1f : 0f) - (kb[rewindKey].isPressed ? 1f : 0f);
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
        Feedback(TimeMode ? "시간 조절 켜짐 · [ 뒤로  ] 앞으로 (꾹)" : "시간 조절 꺼짐");
    }

    // ---------- 공통 ----------
    private void Used()
    {
        UsedThisGame = true;
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
