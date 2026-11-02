using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 치트 (태블릿 게임 설정의 치트 카드 + 단축키)
//   돈 무한   (F1) : 돈이 줄면 바로 다시 채움. 끄면 켜기 전 돈으로 돌아감
//   시간 정지 (F2) : 시계(준비 시간 · 하루 시간)가 멈춤. 손님은 계속 옴 → 하루가 안 끝남
//   시간 조절 (F3) : 누를 때마다 게임 속도 ×2 → ×4 → ×8 → ×0.5 → 끄기
//                    켜져 있을 때 [ ] 키로 한 칸씩 느리게 / 빠르게
// 치트를 한 번이라도 켜면 그 판은 순위표에 안 남음 (GameEndScreen이 확인)
// 단축키가 태블릿을 닫아도 먹게, 항상 켜져 있는 오브젝트(Canvas(Always))에 붙이기
public class CheatManager : MonoBehaviour
{
    // 이번 판에 치트를 쓴 적이 있는지 (Game 씬이 새로 열릴 때마다 false로)
    public static bool UsedThisGame { get; private set; }

    [Header("버튼 (치트 카드의 켜기 버튼들)")]
    public Button moneyButton;
    public Button stopButton;
    public Button speedButton;

    [Header("연결 (비우면 자동으로 찾음)")]
    public Player_State state;
    public CustomerSpawner spawner;
    public GameEndScreen endScreen;
    [Tooltip("단축키로 켜고 끌 때 잠깐 뜨는 안내 글자 (선택)")]
    public TMP_Text notice;

    [Header("단축키")]
    public Key moneyKey = Key.F1;
    public Key stopKey = Key.F2;
    public Key speedKey = Key.F3;
    public Key slowerKey = Key.LeftBracket;
    public Key fasterKey = Key.RightBracket;

    [Header("설정")]
    [Tooltip("돈 무한일 때 채워두는 돈")]
    public int infiniteMoney = 9999999;
    [Tooltip("시간 조절 버튼을 누를 때마다 바뀌는 속도 (마지막 다음은 끄기)")]
    public float[] speeds = { 2f, 4f, 8f, 0.5f };

    [Header("버튼 모양")]
    public string onText = "끄기";
    public string offText = "켜기";
    public string speedFormat = "×{0}";
    [Tooltip("켜졌을 때 버튼 색 (꺼졌을 때는 원래 색)")]
    public Color onColor = new Color32(0x1E, 0x9E, 0x5A, 0xFF);
    public float noticeTime = 1.5f;

    public bool MoneyOn { get; private set; }
    public bool ClockStopped { get; private set; }
    public float Speed => speedIndex >= 0 ? speeds[speedIndex] : 1f;

    private int speedIndex = -1;   // -1 = 끔
    private int moneyBefore;
    private float noticeTimer;
    private Color moneyColor, stopColor, speedColor;

    private void Awake()
    {
        UsedThisGame = false;
        if (state == null) state = FindAnyObjectByType<Player_State>();
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (endScreen == null) endScreen = FindAnyObjectByType<GameEndScreen>();

        moneyColor = Hook(moneyButton, ToggleMoney);
        stopColor = Hook(stopButton, ToggleStop);
        speedColor = Hook(speedButton, NextSpeed);
        if (notice != null) notice.text = "";
        Refresh();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;   // 씬을 나가면 속도 원래대로
    }

    private void Update()
    {
        bool ended = endScreen != null && endScreen.Shown;
        if (ended && speedIndex >= 0) SetSpeedIndex(-1, false);   // 영수증 애니메이션은 보통 속도로

        Keyboard kb = Keyboard.current;
        if (kb != null && !ended)
        {
            if (kb[moneyKey].wasPressedThisFrame) ToggleMoney();
            if (kb[stopKey].wasPressedThisFrame) ToggleStop();
            if (kb[speedKey].wasPressedThisFrame) NextSpeed();
            if (speedIndex >= 0 && kb[slowerKey].wasPressedThisFrame) StepSpeed(-1);
            if (speedIndex >= 0 && kb[fasterKey].wasPressedThisFrame) StepSpeed(+1);
        }

        // 돈 무한: 쓰자마자 다시 채움
        if (MoneyOn && state != null && state.money < infiniteMoney) state.Earn(infiniteMoney - state.money);

        if (notice != null && noticeTimer > 0f)
        {
            noticeTimer -= Time.unscaledDeltaTime;
            if (noticeTimer <= 0f) notice.text = "";
        }
    }

    // ---------- 치트 ----------
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

    public void ToggleStop()
    {
        ClockStopped = !ClockStopped;
        if (spawner != null) spawner.clockStopped = ClockStopped;
        if (ClockStopped) Used();
        Feedback(ClockStopped ? "시간 정지 켜짐" : "시간 정지 꺼짐");
    }

    // 끔 → ×2 → ×4 → ×8 → ×0.5 → 끔
    public void NextSpeed()
    {
        int next = speedIndex + 1;
        SetSpeedIndex(next >= speeds.Length ? -1 : next, true);
    }

    // [ ] 키: 느린 쪽 / 빠른 쪽으로 한 칸 (켜져 있을 때만)
    private void StepSpeed(int dir)
    {
        float cur = Speed;
        int best = speedIndex;
        float bestValue = dir > 0 ? float.MaxValue : float.MinValue;
        for (int i = 0; i < speeds.Length; i++)
        {
            float v = speeds[i];
            if (dir > 0 && v > cur && v < bestValue) { best = i; bestValue = v; }
            if (dir < 0 && v < cur && v > bestValue) { best = i; bestValue = v; }
        }
        if (best != speedIndex) SetSpeedIndex(best, true);
    }

    private void SetSpeedIndex(int index, bool feedback)
    {
        speedIndex = index;
        Time.timeScale = Speed;
        if (speedIndex >= 0) Used();
        if (feedback) Feedback(speedIndex >= 0 ? "게임 속도 " + string.Format(speedFormat, Speed) : "게임 속도 보통");
        else Refresh();
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

    private void Refresh()
    {
        Paint(moneyButton, MoneyOn, MoneyOn ? onText : offText, moneyColor);
        Paint(stopButton, ClockStopped, ClockStopped ? onText : offText, stopColor);
        Paint(speedButton, speedIndex >= 0, speedIndex >= 0 ? string.Format(speedFormat, Speed) : offText, speedColor);
    }

    private Color Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return Color.white;
        b.onClick.AddListener(action);
        Image img = b.targetGraphic as Image;
        return img != null ? img.color : Color.white;
    }

    private void Paint(Button b, bool on, string label, Color offColor)
    {
        if (b == null) return;
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = label;
        if (b.targetGraphic is Image img) img.color = on ? onColor : offColor;
    }
}
