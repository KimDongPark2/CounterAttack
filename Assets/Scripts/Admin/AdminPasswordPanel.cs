using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 관리자 비밀번호 입력 창
// 이미 만들어 둔 비밀번호 창 오브젝트에 붙이면 됨 (버튼으로 이 오브젝트를 켜면 동작)
// - 창이 켜지면 입력 칸을 비우고 바로 입력할 수 있게 함
// - Enter 또는 확인 버튼 → 맞으면 창 닫고 관리자 설정 창 열기 / 틀리면 안내 + 흔들기
// - Esc 또는 닫기 버튼 → 창 닫기
// 연결: 확인 버튼 OnClick → AdminPasswordPanel.Submit,  닫기 버튼 OnClick → AdminPasswordPanel.Close
public class AdminPasswordPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // 비밀번호를 맞혀서 연 "직접 만든 설정 창"이 아직 켜져 있는지 (인트로 메뉴가 키를 안 받게)
    private static GameObject openedSettings;
    public static bool SettingsOpen => openedSettings != null && openedSettings.activeInHierarchy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { IsOpen = false; openedSettings = null; }

    // 씬에 있는 비밀번호 창을 찾아서 켜기 (꺼져 있어도 찾음). 못 찾으면 false
    public static bool OpenInScene()
    {
        AdminPasswordPanel[] found = FindObjectsByType<AdminPasswordPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (found.Length == 0) return false;
        found[0].gameObject.SetActive(true);
        return true;
    }

    [Header("비밀번호")]
    [Tooltip("기본 비밀번호. 관리자 설정 창에서 바꾸면 그 값이 우선")]
    public string password = "0000";

    [Header("연결 (비우면 자식에서 자동으로 찾음)")]
    public TMP_InputField input;
    [Tooltip("확인 버튼 (선택). 넣으면 OnClick을 따로 안 걸어도 됨")]
    public Button confirmButton;
    [Tooltip("닫기 버튼 (선택)")]
    public Button closeButton;
    [Tooltip("틀렸을 때 안내 글자 (선택)")]
    public TMP_Text messageText;
    [Tooltip("흔들 창 (비우면 이 오브젝트)")]
    public RectTransform shakeTarget;

    [Header("관리자 설정 창 (셋 중 하나)")]
    [Tooltip("직접 만든 설정 창 오브젝트. 넣으면 비밀번호가 맞을 때 이 오브젝트를 켬 (줄마다 AdminSettingField를 붙여서 씀)")]
    public GameObject settingsObject;
    [Tooltip("코드로 만드는 설정 창을 씬에 둔 경우. 둘 다 비우면 코드로 만든 창이 자동으로 열림")]
    public AdminSettingsPanel settingsPanel;

    [Header("글자")]
    public string wrongMessage = "비밀번호가 틀렸어요";
    public string emptyMessage = "비밀번호를 입력하세요";

    private Vector2 basePos;
    private Coroutine shaking;
    private int openFrame = -1;     // 창을 연 Enter가 바로 "확인"으로 먹히지 않게
    private int submitFrame = -1;   // 같은 Enter로 두 번 확인되지 않게

    private void Awake()
    {
        if (input == null) input = GetComponentInChildren<TMP_InputField>(true);
        if (shakeTarget == null) shakeTarget = transform as RectTransform;
        if (shakeTarget != null) basePos = shakeTarget.anchoredPosition;

        if (input != null)
        {
            input.contentType = TMP_InputField.ContentType.Password;   // ●●●● 로 보임
            input.ForceLabelUpdate();
            input.onSubmit.AddListener(_ => Submit());
        }
        else Debug.LogWarning("[AdminPasswordPanel] 자식에 TMP_InputField가 없어요.", this);

        if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        IsOpen = true;
        openFrame = Time.frameCount;
        if (input != null) input.text = "";
        SetMessage("");
        if (shakeTarget != null) shakeTarget.anchoredPosition = basePos;
        StartCoroutine(FocusNextFrame());   // 켜진 그 프레임엔 포커스가 안 먹어서 한 프레임 뒤에
    }

    private void OnDisable()
    {
        IsOpen = false;
        shaking = null;
        if (shakeTarget != null) shakeTarget.anchoredPosition = basePos;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        // 입력 칸 밖을 눌러서 포커스가 빠졌을 때도 Enter로 확인 (칸 안에서는 onSubmit이 처리)
        else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && input != null && !input.isFocused) Submit();
    }

    public void Submit()
    {
        if (!isActiveAndEnabled || Time.frameCount == openFrame || Time.frameCount == submitFrame) return;
        submitFrame = Time.frameCount;
        string typed = input != null ? input.text : "";

        if (string.IsNullOrEmpty(typed))
        {
            Fail(emptyMessage);
            return;
        }

        if (typed == AdminSettings.GetPassword(password))
        {
            SoundManager.Play(Sfx.MenuChoose);
            gameObject.SetActive(false);
            if (settingsObject != null)
            {
                settingsObject.SetActive(true);
                openedSettings = settingsObject;
            }
            else if (settingsPanel != null) settingsPanel.Open();
            else AdminSettingsPanel.OpenNew(FontFromHere());
            return;
        }

        Fail(wrongMessage);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Fail(string message)
    {
        SoundManager.Play(Sfx.MenuMove);
        SetMessage(message);
        if (input != null) input.text = "";
        StartCoroutine(FocusNextFrame());
        if (shakeTarget != null)
        {
            if (shaking != null) StopCoroutine(shaking);
            shaking = StartCoroutine(Shake());
        }
    }

    private void SetMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null;
        if (input != null && isActiveAndEnabled)
        {
            input.Select();
            input.ActivateInputField();
        }
    }

    private IEnumerator Shake()
    {
        const float time = 0.3f, power = 18f;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - t / time;
            shakeTarget.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 60f) * power * k, 0f);
            yield return null;
        }
        shakeTarget.anchoredPosition = basePos;
        shaking = null;
    }

    // 관리자 설정 창도 이 창과 같은 글꼴(한글 되는 것)을 쓰게
    private TMP_FontAsset FontFromHere()
    {
        TMP_Text t = GetComponentInChildren<TMP_Text>(true);
        return t != null ? t.font : null;
    }
}
