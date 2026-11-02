using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ConvenienceStore.Tablet;

// 하루가 끝나면 (CustomerSpawner의 하루 끝) 영수증 화면(GameEnd)을 켜고 순위 등록
// - GameEnd를 켜면 Panel의 Animator가 End 애니메이션(영수증 출력)을 재생
// - 플레이어 조작을 막고 마우스 커서를 풀어줌
// - 명찰에서 받은 이름 · 사진으로 순위표(RankingStore)에 등록
// - 애니메이션이 끝나면 아래 메뉴(IntroMenu)가 키를 받기 시작
// 항상 켜져 있는 오브젝트(Canvas(Always))에 붙이기. GameEnd는 꺼둔 채로 두기
public class GameEndScreen : MonoBehaviour
{
    [Header("연결 (Spawner · State는 비우면 자동으로 찾음)")]
    [Tooltip("게임 끝 화면 전체 (GameEnd · 꺼둔 상태)")]
    public GameObject root;
    [Tooltip("영수증 아래 메뉴 (Receipt의 IntroMenu)")]
    public IntroMenu menu;
    public CustomerSpawner spawner;
    public Player_State state;
    [Tooltip("끝나면 숨길 HUD (핫바, 조준점, 하루 바, 조작 안내 등)")]
    public GameObject[] hideOnEnd;

    [Header("영수증 글자")]
    [Tooltip("SubTitle / Name")]
    public TMP_Text nameText;
    public TMP_Text visitorsText;
    public TMP_Text servedText;
    public TMP_Text revenueText;
    public TMP_Text ratingText;
    public TMP_Text debtText;
    [Tooltip("최종 점수 (BestScore)")]
    public TMP_Text scoreText;
    [Tooltip("순위 결과 한 줄 (선택)")]
    public TMP_Text rankText;
    [Tooltip("1등을 새로 차지하면 보일 도장 (선택)")]
    public GameObject newBestStamp;

    [Header("글자 모양")]
    public string nameFormat = "편의점 시뮬레이터 · {0}";
    public string noNameText = "연습 모드";
    public string countFormat = "{0}명";
    public string moneyFormat = "{0:N0}원";
    public string ratingFormat = "{0}/5";
    public string debtFormat = "-{0:N0}원";
    public string scoreFormat = "{0:N0}점";
    public string rankFormat = "순위표 {0}위 등록!";
    public string keepRankFormat = "내 최고 기록 {0}위 그대로";
    public string outOfRankText = "아쉽게 순위 밖 (10위까지)";
    public string noRecordText = "이름이 없어서 순위에 안 남아요";
    public string cheatText = "치트를 써서 순위에 안 남아요";

    [Header("메뉴")]
    [Tooltip("영수증이 다 올라온 뒤 메뉴가 키를 받기 시작 (End 애니메이션 길이)")]
    public float menuDelay = 2.6f;
    public string titleScene = "Intro";
    public Sfx stampSound = Sfx.PhaseChange;

    public bool Shown { get; private set; }
    // 이번 판 순위 결과 (다른 UI에서 쓰고 싶을 때)
    public RankingStore.Result LastResult { get; private set; }

    private void Awake()
    {
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (state == null) state = FindAnyObjectByType<Player_State>();
        if (root != null) root.SetActive(false);
        if (menu != null) menu.enabled = false;   // 애니메이션 끝나기 전에 Enter가 눌리지 않게
        if (newBestStamp != null) newBestStamp.SetActive(false);
        if (spawner == null) Debug.LogError("[GameEndScreen] 씬에 CustomerSpawner가 없어요.", this);
    }

    private void OnEnable()
    {
        if (spawner != null) spawner.onDayEnd.AddListener(Show);
    }

    private void OnDisable()
    {
        if (spawner != null) spawner.onDayEnd.RemoveListener(Show);
    }

    // ---------- 하루 끝 ----------
    public void Show()
    {
        if (Shown) return;
        Shown = true;
        Time.timeScale = 1f;   // 치트로 속도를 바꿨어도 영수증은 보통 속도로

        LockPlayer();
        if (hideOnEnd != null)
            foreach (GameObject go in hideOnEnd) if (go != null) go.SetActive(false);

        int debt = state != null ? Mathf.Max(0, state.debt) : 0;
        int score = StoreStats.ScoreAfterDebt(debt);
        int stars = StoreStats.Stars;

        // 영수증 숫자
        Set(nameText, PlayerSession.Active ? string.Format(nameFormat, PlayerSession.Name) : noNameText);
        Set(visitorsText, string.Format(countFormat, StoreStats.Visitors));
        Set(servedText, string.Format(countFormat, StoreStats.Served));
        Set(revenueText, string.Format(moneyFormat, StoreStats.Revenue));
        Set(ratingText, string.Format(ratingFormat, stars));
        Set(debtText, debt > 0 ? string.Format(debtFormat, debt) : string.Format(moneyFormat, 0));
        Set(scoreText, string.Format(scoreFormat, score));

        // 순위 등록 (명찰 없이 Game 씬부터 바로 실행했거나, 치트를 썼으면 등록 안 함)
        RankingStore.Result result = new RankingStore.Result();
        if (CheatManager.UsedThisGame) Set(rankText, cheatText);
        else if (PlayerSession.Active)
        {
            result = RankingStore.Submit(PlayerSession.Id, PlayerSession.Name, score, stars, StoreStats.Revenue, StoreStats.Served, PlayerSession.Photo);
            if (result.improved && result.rank > 0) Set(rankText, string.Format(rankFormat, result.rank));
            else if (result.rank > 0) Set(rankText, string.Format(keepRankFormat, result.rank));
            else Set(rankText, outOfRankText);
        }
        else Set(rankText, noRecordText);
        LastResult = result;

        if (root != null) root.SetActive(true);   // Panel의 Animator가 영수증 출력 애니메이션 재생
        StartCoroutine(AfterPrint(result.newTop));
    }

    private IEnumerator AfterPrint(bool newTop)
    {
        yield return new WaitForSecondsRealtime(menuDelay);

        // 끝날 때 태블릿이 열려 있었으면 닫히는 동안 기다렸다가 끔 (Tab으로 다시 안 열리게)
        TabletController tablet = FindAnyObjectByType<TabletController>();
        if (tablet != null) tablet.enabled = false;

        if (newTop && newBestStamp != null)
        {
            newBestStamp.SetActive(true);
            SoundManager.Play(stampSound);
            yield return Slam(newBestStamp.transform);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (menu != null) menu.enabled = true;   // 이때부터 ↑ ↓ · Enter · 마우스
    }

    // ---------- 메뉴에서 부르기 (Row의 On Choose에 연결) ----------
    // 타이틀로: 다음 사람 차례
    public void GoTitle()
    {
        SceneShutter.Load(titleScene);
    }

    // 다시 하기: 같은 사람이 이름 · 사진 그대로 한 번 더
    public void Retry()
    {
        SceneShutter.Load(SceneManager.GetActiveScene().name);
    }

    // ---------- 플레이어 멈추기 ----------
    private void LockPlayer()
    {
        // 태블릿이 열려 있으면 먼저 닫기 (닫으면서 시점 조작을 다시 켜니까, 그다음에 끔)
        TabletController tablet = FindAnyObjectByType<TabletController>();
        if (tablet != null && tablet.IsOpen) tablet.SetOpen(false);

        // 키 입력 끄기 (WASD · 클릭 · F · G · Tab). UI는 EventSystem이 따로 받아서 영향 없음
        foreach (PlayerInput pi in FindObjectsByType<PlayerInput>(FindObjectsSortMode.None)) pi.enabled = false;
        foreach (Player_movement b in FindObjectsByType<Player_movement>(FindObjectsSortMode.None)) b.enabled = false;
        foreach (Player_Look b in FindObjectsByType<Player_Look>(FindObjectsSortMode.None)) b.enabled = false;
        foreach (Player_Interaction b in FindObjectsByType<Player_Interaction>(FindObjectsSortMode.None)) b.enabled = false;
        foreach (Player_ItemInstall b in FindObjectsByType<Player_ItemInstall>(FindObjectsSortMode.None))
        {
            b.CancelHold();
            b.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void Set(TMP_Text t, string s)
    {
        if (t != null) t.text = s;
    }

    // 도장 쾅: 크게 시작해서 툭 찍힘
    private IEnumerator Slam(Transform t)
    {
        float time = 0f;
        const float dur = 0.22f;
        while (time < dur)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / dur);
            float s = Mathf.Lerp(1.8f, 1f, k * k);
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
