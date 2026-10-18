using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시간대 단계(오전 / 점심 러시 / 오후 / 퇴근 러시 / 마감)가 바뀔 때
// 화면 가운데에 기울어진 배너가 왼쪽에서 들어왔다가 잠깐 멈추고 오른쪽으로 나감
// 이 스크립트는 배너의 "부모"(빈 오브젝트)에 붙이기. 배너 자체에 붙이면 숨길 때 같이 멈춤
public class PhaseBanner : MonoBehaviour
{
    [Serializable]
    public class Style
    {
        [Tooltip("배너 면 색")] public Color fill = Color.white;
        [Tooltip("글자 색")] public Color text = Color.white;
        [Tooltip("아래 작은 글씨 (비워도 됨)")] public string sub;
        public Style(Color f, Color t, string s) { fill = f; text = t; sub = s; }
    }

    public CustomerSpawner spawner;

    [Header("배너")]
    [Tooltip("움직일 배너 (바탕 + 면 + 글자를 담은 오브젝트)")]
    public RectTransform banner;
    [Tooltip("색을 바꿀 면 Image (phase_banner_fill)")]
    public Image fill;
    public TMP_Text titleText;
    public TMP_Text subText;

    [Header("단계별 모양 (스포너 단계 순서대로)")]
    public Style[] styles =
    {
        new Style(new Color32(0x8E, 0xD0, 0xF7, 0xFF), new Color32(0x0A, 0x24, 0x56, 0xFF), "영업 시작!"),
        new Style(new Color32(0xFF, 0x91, 0x47, 0xFF), Color.white, "손님이 몰려와요!"),
        new Style(new Color32(0xFF, 0xD8, 0x4D, 0xFF), new Color32(0x0A, 0x24, 0x56, 0xFF), "잠깐 숨 돌릴 시간"),
        new Style(new Color32(0xF2, 0x60, 0x6A, 0xFF), Color.white, "손님이 몰려와요!"),
        new Style(new Color32(0x5C, 0x67, 0xC2, 0xFF), Color.white, "곧 문 닫아요"),
    };

    [Header("움직임")]
    [Tooltip("옆에서 들어오고 나가는 거리")]
    public float slideDistance = 900f;
    public float inTime = 0.35f;
    [Tooltip("가운데 멈춰 있는 시간")]
    public float stayTime = 1.6f;
    public float outTime = 0.3f;
    [Tooltip("하루 시작할 때 첫 단계(오전)도 보여줄지")]
    public bool showFirstPhase = true;

    [Header("소리 (선택)")]
    public AudioSource audioSource;
    public AudioClip sound;

    private CanvasGroup group;
    private Vector2 home;
    private int lastPhase = -1;
    private float timer = -1f;   // -1이면 안 보이는 중

    private void Awake()
    {
        if (banner == null || banner == transform)
        {
            Debug.LogWarning("[PhaseBanner] Banner 칸에 배너(자식 오브젝트)를 연결하세요. 이 스크립트는 배너의 부모에 붙여야 해요.", this);
            enabled = false;
            return;
        }
        home = banner.anchoredPosition;
        group = banner.GetComponent<CanvasGroup>();
        if (group == null) group = banner.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Hide();
    }

    private void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<CustomerSpawner>();
    }

    private void Update()
    {
        if (spawner == null) return;

        if (spawner.DayRunning)
        {
            int phase = CurrentPhase();
            if (phase != lastPhase)
            {
                bool first = lastPhase < 0;
                lastPhase = phase;
                if (phase >= 0 && (!first || showFirstPhase)) Play(phase);
            }
        }
        else if (spawner.DayTime <= 0f) lastPhase = -1;   // 새 하루가 시작되면 다시 처음부터

        Animate();
    }

    private int CurrentPhase()
    {
        int index = -1;
        CustomerSpawner.Phase[] phases = spawner.phases;
        for (int i = 0; i < phases.Length; i++)
            if (phases[i] != null && spawner.DayTime >= phases[i].startTime) index = i;
        return index;
    }

    // 원하는 때에 직접 띄우고 싶으면 이걸 불러도 됨 (예: PlayText("영업 끝!"))
    public void Play(int phase)
    {
        Style st = phase < styles.Length ? styles[phase] : null;
        string title = spawner != null && phase < spawner.phases.Length ? spawner.phases[phase].name : "";
        Show(title, st != null ? st.sub : "", st != null ? st.fill : Color.white, st != null ? st.text : Color.white);
    }

    public void Show(string title, string sub, Color fillColor, Color textColor)
    {
        if (titleText != null) { titleText.text = title; titleText.color = textColor; }
        if (subText != null)
        {
            subText.text = sub;
            subText.color = textColor;
            subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }
        if (fill != null) fill.color = fillColor;
        if (audioSource != null && sound != null) audioSource.PlayOneShot(sound);

        banner.gameObject.SetActive(true);
        timer = 0f;
    }

    private void Animate()
    {
        if (timer < 0f) return;
        timer += Time.deltaTime;

        float x, alpha;
        if (timer < inTime)
        {
            float k = EaseOutBack(timer / inTime);
            x = Mathf.LerpUnclamped(-slideDistance, 0f, k);
            alpha = Mathf.Clamp01(timer / inTime * 2f);
        }
        else if (timer < inTime + stayTime)
        {
            x = 0f;
            alpha = 1f;
        }
        else if (timer < inTime + stayTime + outTime)
        {
            float k = (timer - inTime - stayTime) / outTime;
            x = Mathf.Lerp(0f, slideDistance, k * k);   // 점점 빨라지며 나감
            alpha = 1f - k;
        }
        else
        {
            Hide();
            return;
        }

        banner.anchoredPosition = home + new Vector2(x, 0f);
        group.alpha = alpha;
    }

    private void Hide()
    {
        timer = -1f;
        if (group != null) group.alpha = 0f;
        if (banner != null)
        {
            banner.anchoredPosition = home;
            banner.gameObject.SetActive(false);
        }
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
