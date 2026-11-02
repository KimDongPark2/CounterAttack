using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 명찰 화면 (InitSetting Panel에 붙이기)
// 인트로 "게임 시작" → 명찰 열림 → 학번이름 적고 📷 찍고 → 근무 시작 → 셔터 → Game 씬
// 판마다 새 사람: 열 때마다 이름 칸 비우고 사진도 기본 사진으로 돌아감
// Enter = 근무 시작, Esc = 메뉴로 돌아가기
public class StaffBadge : MonoBehaviour
{
    // 명찰이 열려 있는지 (열려 있는 동안 인트로 메뉴는 키를 안 받음)
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => IsOpen = false;

    [Header("연결")]
    [Tooltip("이름 칸 (nameInput)")]
    public TMP_InputField nameInput;
    [Tooltip("근무 시작 버튼 (StartBtn)")]
    public Button startButton;
    [Tooltip("사진 칸 (Photo에 붙인 WebcamCapture)")]
    public WebcamCapture photo;
    [Tooltip("\"ID 0003 · POS 01\" 글자 (선택)")]
    public TMP_Text idText;
    [Tooltip("명찰 뒤를 어둡게 덮는 판 (선택)")]
    public GameObject dim;

    [Header("설정")]
    public string gameSceneName = "Game";
    [Tooltip("이름 최대 글자 수 (학번 5 + 이름 3~4)")]
    public int maxLength = 10;
    [Tooltip("{0} = 오늘 몇 번째 알바인지")]
    public string idFormat = "ID {0:0000} · POS 01";

    [Header("움직임")]
    public float popTime = 0.25f;
    public float shakeTime = 0.35f;
    public float shakeAmount = 14f;
    [Tooltip("빈칸으로 시작하려 할 때 이름 칸 색")]
    public Color errorColor = new Color32(0xFF, 0x9A, 0x9A, 0xFF);

    private RectTransform rect;
    private Vector2 basePos;
    private Image inputImage;
    private Color inputColor = Color.white;
    private bool starting;
    private int openedFrame = -1;
    private Coroutine shaking;
    private const string CountKey = "StaffCount";

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePos = rect.anchoredPosition;

        if (startButton != null) startButton.onClick.AddListener(StartWork);
        if (nameInput != null)
        {
            nameInput.contentType = TMP_InputField.ContentType.Standard;   // Name 타입은 숫자(학번)를 막아서 Standard로
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.characterLimit = maxLength;
            nameInput.richText = false;
            nameInput.onSubmit.AddListener(_ => StartWork());
            inputImage = nameInput.GetComponent<Image>();
            if (inputImage != null) inputColor = inputImage.color;
        }
    }

    private void OnDisable()
    {
        IsOpen = false;
        if (dim != null) dim.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || starting) return;
        if (Time.frameCount == openedFrame) return;   // 메뉴에서 Enter/Space로 연 그 프레임은 무시 (바로 시작돼 버리지 않게)
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        // 이름 칸 밖을 눌러서 포커스가 빠졌을 때도 Enter로 시작 (칸 안에서는 onSubmit이 처리)
        if (nameInput != null && !nameInput.isFocused && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            StartWork();
    }

    // ---------- 열기 / 닫기 ----------
    // IntroMenu의 "게임 시작"이 부름
    public void Open()
    {
        PlayerSession.Clear();   // 새 사람 차례 (이전 사람 이름 · 사진 지우기)
        gameObject.SetActive(true);
        if (dim != null) dim.SetActive(true);
        IsOpen = true;
        starting = false;
        openedFrame = Time.frameCount;
        rect.anchoredPosition = basePos;

        if (nameInput != null)
        {
            nameInput.text = "";
            if (inputImage != null) inputImage.color = inputColor;
        }
        if (photo != null) photo.ResetPhoto();
        if (idText != null) idText.text = string.Format(idFormat, PlayerPrefs.GetInt(CountKey, 0) + 1);

        StartCoroutine(Pop());
        StartCoroutine(FocusNextFrame());
    }

    public void Close()
    {
        if (starting) return;
        if (photo != null) photo.ResetPhoto();   // 카메라 끄고 사진 지우기
        gameObject.SetActive(false);              // OnDisable에서 IsOpen = false
    }

    // ---------- 근무 시작 ----------
    public void StartWork()
    {
        if (starting || !IsOpen) return;
        if (photo != null && photo.Busy) return;   // 3, 2, 1 찍는 중

        string n = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(n))
        {
            if (shaking != null) StopCoroutine(shaking);
            shaking = StartCoroutine(Shake());
            StartCoroutine(FocusNextFrame());
            return;
        }

        starting = true;
        PlayerPrefs.SetInt(CountKey, PlayerPrefs.GetInt(CountKey, 0) + 1);
        PlayerSession.Begin(n, photo != null ? photo.TakePhoto() : null);
        SoundManager.Play(Sfx.MenuChoose);
        if (nameInput != null) nameInput.DeactivateInputField();
        SceneShutter.Load(gameSceneName);
    }

    // ---------- 연출 ----------
    private IEnumerator FocusNextFrame()
    {
        yield return null;
        if (nameInput == null || !IsOpen) yield break;
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    private IEnumerator Pop()
    {
        float t = 0f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popTime);
            float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);   // 0 → 1, 끝에서 살짝 넘쳤다가 돌아옴
            float s = Mathf.LerpUnclamped(0.85f, 1f, back);
            rect.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    private IEnumerator Shake()
    {
        SoundManager.Play(Sfx.ButtonClick);
        if (inputImage != null) inputImage.color = errorColor;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / shakeTime);
            rect.anchoredPosition = basePos + Vector2.right * (Mathf.Sin(t * 60f) * shakeAmount * k);
            yield return null;
        }
        rect.anchoredPosition = basePos;
        yield return new WaitForSecondsRealtime(0.4f);
        if (inputImage != null) inputImage.color = inputColor;
        shaking = null;
    }
}
