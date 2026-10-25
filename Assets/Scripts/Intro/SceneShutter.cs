using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환 셔터
// 셔터 쿵 내려오기 → "로딩 중..." → 다음 씬 불러오기 → 셔터 올라가기
// - ShutterCanvas(맨 바깥 오브젝트)에 붙이고 Intro 씬에 두면, 씬이 바뀌어도 계속 남아 있음
// - 프리팹으로 만들어서 Game 씬에도 두면 Game 씬만 따로 실행해도 됨 (겹치면 하나만 남음)
// - 다른 스크립트에서:  SceneShutter.Load("Game");
// - 버튼이나 On Day End 같은 이벤트에서는 SceneShutterLink를 쓰면 됨
public class SceneShutter : MonoBehaviour
{
    public static SceneShutter Instance { get; private set; }

    // 셔터가 화면을 가리고 있는 동안 true (내려오기 시작 ~ 올라가기 시작 전)
    // CustomerSpawner가 이게 false가 될 때까지 기다렸다가 하루를 시작함
    public static bool Blocking { get; private set; }

    // 전환 중인지 (내려오기 시작 ~ 다 올라갈 때까지)
    public static bool Busy => Instance != null && Instance.busy;

    [Header("연결")]
    [Tooltip("비우면 이 오브젝트의 Canvas")]
    public Canvas canvas;
    [Tooltip("위아래로 움직일 셔터 (Pos Y 0 = 닫힘)")]
    public RectTransform shutter;
    [Tooltip("셔터가 닫혀 있는 동안 보일 글자")]
    public TMP_Text loadingText;

    [Header("로딩 글자")]
    public string loadingLabel = "로딩 중";
    [Tooltip("뒤에 점이 하나씩 늘어남 (0이면 점 없음)")]
    public int dotCount = 3;
    [Tooltip("점이 하나 늘어나는 간격 (초)")]
    public float dotInterval = 0.3f;

    [Header("시간 (초)")]
    [Tooltip("셔터가 떨어지는 시간 (점점 빨라짐)")]
    public float dropTime = 0.55f;
    [Tooltip("바닥에 닿고 살짝 튕기는 시간")]
    public float bounceTime = 0.18f;
    [Tooltip("튕길 때 바닥보다 더 내려가는 거리")]
    public float bounceDepth = 18f;
    [Tooltip("셔터가 닫혀 있는 최소 시간 (로딩이 빨라도 이만큼은 보여줌)")]
    public float holdTime = 0.6f;
    [Tooltip("로딩 글자가 나타나고 사라지는 시간")]
    public float textFadeTime = 0.15f;
    [Tooltip("셔터가 올라가는 시간")]
    public float riseTime = 0.7f;
    [Tooltip("다 올라갔을 때 화면 위로 더 올리는 거리 (아래 그림자까지 숨기기)")]
    public float hideExtra = 60f;

    [Header("게임 켤 때")]
    [Tooltip("게임을 처음 켜면 셔터가 닫힌 채로 시작해서 올라감")]
    public bool openOnStart = true;
    [Tooltip("처음 올라가기 전 기다리는 시간")]
    public float openOnStartDelay = 0.3f;

    [Header("소리 (소리 파일은 SoundManager의 ShutterDrop · ShutterLand 칸에)")]
    [Tooltip("셔터를 내릴 때 지금 브금을 줄여서 끄기 (다음 씬 브금은 셔터가 올라갈 때 시작)")]
    public bool fadeOutBgm = true;

    private bool busy;
    private bool duplicate;
    private bool textOn;
    private int shownDots = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
        Blocking = false;
    }

    // ---------- 다른 스크립트에서 부르기 ----------
    // 셔터가 없으면(Instance 없음) 그냥 바로 씬을 바꿈
    public static void Load(string sceneName)
    {
        if (Instance != null) Instance.LoadScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    // UnityEvent / 버튼용
    public void LoadScene(string sceneName)
    {
        if (busy) return;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneShutter] 씬 이름이 비어 있어요.", this);
            return;
        }
        StartCoroutine(Run(sceneName));
    }

    // ---------- 준비 ----------
    private void Awake()
    {
        // 씬을 다시 불러오면 셔터가 하나 더 생기니까, 원래 있던 것만 남기기
        if (Instance != null && Instance != this)
        {
            duplicate = true;
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);   // DontDestroyOnLoad는 맨 바깥 오브젝트만 됨
        DontDestroyOnLoad(gameObject);

        if (canvas == null) canvas = GetComponent<Canvas>();
        if (shutter == null)
        {
            Debug.LogWarning("[SceneShutter] Shutter 칸을 연결하세요.", this);
            enabled = false;
            return;
        }
        SetText(0f);

        if (openOnStart)
        {
            // 닫힌 채로 시작 (Awake에서 해야 다른 스크립트의 Start보다 먼저 Blocking이 켜짐)
            Blocking = true;
            busy = true;
            SetY(0f);
            SetCanvas(true);
        }
        else
        {
            SetY(OpenY());
            SetCanvas(false);
        }
    }

    private void Start()
    {
        if (duplicate || !enabled) return;
        if (openOnStart) StartCoroutine(OpenAtStart());
    }

    private void Update()
    {
        // 로딩 중 → 로딩 중. → 로딩 중.. → 로딩 중... (안 보이는 점도 자리는 차지해서 글자가 안 흔들림)
        if (!textOn || loadingText == null || dotCount <= 0) return;
        int n = Mathf.FloorToInt(Time.unscaledTime / Mathf.Max(0.05f, dotInterval)) % (dotCount + 1);
        if (n == shownDots) return;
        shownDots = n;
        loadingText.text = loadingLabel + new string('.', n) + "<alpha=#00>" + new string('.', dotCount - n);
    }

    private IEnumerator OpenAtStart()
    {
        yield return null;   // 첫 프레임 (화면 크기 정해진 뒤)
        float t = 0f;
        while (t < openOnStartDelay) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Open();
        busy = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Blocking = false;
        }
    }

    // ---------- 전환 ----------
    private IEnumerator Run(string sceneName)
    {
        busy = true;
        yield return Close();
        float closedAt = Time.unscaledTime;

        // 로딩 글자 나타나기
        textOn = true;
        shownDots = -1;
        for (float t = 0f; t < textFadeTime; t += Time.unscaledDeltaTime)
        {
            SetText(t / textFadeTime);
            yield return null;
        }
        SetText(1f);

        // 셔터가 다 닫힌 다음에 불러오기 (로딩 중 멈칫하는 게 안 보임)
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneShutter] '{sceneName}' 씬을 못 찾았어요. File → Build Settings에 씬을 넣었는지 확인하세요.", this);
        }
        else
        {
            while (!op.isDone) yield return null;
            yield return null;   // 새 씬 첫 프레임
        }

        while (Time.unscaledTime - closedAt < holdTime) yield return null;

        // 로딩 글자 사라지기
        for (float t = 0f; t < textFadeTime; t += Time.unscaledDeltaTime)
        {
            SetText(1f - t / textFadeTime);
            yield return null;
        }
        SetText(0f);
        textOn = false;

        yield return Open();
        busy = false;
    }

    private IEnumerator Close()
    {
        Blocking = true;
        SetCanvas(true);
        SetText(0f);

        float open = OpenY();
        float t;
        Play(Sfx.ShutterDrop);
        if (fadeOutBgm) SoundManager.StopBgm(dropTime + bounceTime);

        // 떨어지기 (점점 빨라지다가 바닥보다 살짝 더 내려감)
        for (t = 0f; t < dropTime; t += Time.unscaledDeltaTime)
        {
            float k = t / dropTime;
            SetY(Mathf.Lerp(open, -bounceDepth, k * k));
            yield return null;
        }
        Play(Sfx.ShutterLand);

        // 튕겨서 제자리
        for (t = 0f; t < bounceTime; t += Time.unscaledDeltaTime)
        {
            float k = t / bounceTime;
            SetY(Mathf.Lerp(-bounceDepth, 0f, 1f - (1f - k) * (1f - k)));
            yield return null;
        }
        SetY(0f);
    }

    private IEnumerator Open()
    {
        Blocking = false;   // 여기서부터 게임 시작
        float open = OpenY();
        for (float t = 0f; t < riseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / riseTime;
            float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;   // 천천히 → 빠르게 → 천천히
            SetY(Mathf.Lerp(0f, open, e));
            yield return null;
        }
        SetY(open);
        SetCanvas(false);
    }

    // ---------- 도우미 ----------
    // 다 올라갔을 때 위치 = 화면 높이 + 여유
    private float OpenY()
    {
        RectTransform parent = shutter.parent as RectTransform;
        float h = parent != null && parent.rect.height > 1f ? parent.rect.height : 1080f;
        return h + hideExtra;
    }

    private void SetY(float y)
    {
        Vector2 p = shutter.anchoredPosition;
        p.y = y;
        shutter.anchoredPosition = p;
    }

    private void SetText(float alpha)
    {
        if (loadingText != null) loadingText.alpha = Mathf.Clamp01(alpha);
    }

    // 안 쓸 때는 Canvas를 꺼서 화면 클릭을 안 막게
    private void SetCanvas(bool on)
    {
        if (canvas != null) canvas.enabled = on;
    }

    private static void Play(Sfx id) => SoundManager.Play(id);
}