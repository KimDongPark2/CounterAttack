using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 위 가운데 "하루 진행 바"
// - 슬라이더 값 = 하루가 얼마나 지났는지 (0~1)
// - 손잡이(말풍선)에 지금 시각 표시
// - 칸 5개 길이를 CustomerSpawner의 구간(오전/점심 러시/...) 시간에 맞춰 자동 조절
// - 러시 구간이면 "손님 몰려와요!" 태그를 지금 칸 아래에 띄움
// 슬라이더 오브젝트(DayBar)에 붙이면 됨
[RequireComponent(typeof(Slider))]
public class DayTimelineUI : MonoBehaviour
{
    public CustomerSpawner spawner;

    [Header("말풍선 시계")]
    public TMP_Text clockText;

    [Header("구간 칸 (왼쪽부터 순서대로)")]
    [Tooltip("Segments 안의 칸들. 스포너의 구간 수와 같게")]
    public RectTransform[] segments;
    [Tooltip("켜면 칸 안 글자를 스포너 구간 이름으로 자동으로 바꿈")]
    public bool autoLabel = true;

    [Header("양 끝 시각 (비워도 됨)")]
    public TMP_Text startLabel;
    public TMP_Text endLabel;

    [Header("러시 태그")]
    public RectTransform rushTag;
    [Tooltip("구간 이름에 이 글자가 들어가면 러시로 봄")]
    public string rushWord = "러시";
    [Tooltip("러시가 시작되고 몇 초 동안 보여줄지. 0이면 러시 끝날 때까지 계속")]
    public float rushTagSeconds = 0f;
    public float bobHeight = 4f;
    public float bobSpeed = 4f;

    private Slider slider;
    private string lastClock;
    private int lastPhase = -1;
    private float rushTimer;
    private float popTimer = 1f;
    private float rushBaseY;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        if (rushTag != null)
        {
            rushBaseY = rushTag.anchoredPosition.y;
            rushTag.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<CustomerSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[DayTimelineUI] CustomerSpawner를 연결하세요.");
            enabled = false;
            return;
        }

        if (startLabel != null) startLabel.text = $"{spawner.startHour}시";
        if (endLabel != null) endLabel.text = $"{spawner.endHour}시";
        SetupSegments();
    }

    // 칸 길이 = 구간 길이(초). 레이아웃 그룹이 비율대로 나눠줌
    private void SetupSegments()
    {
        if (segments == null) return;
        CustomerSpawner.Phase[] phases = spawner.phases;
        int n = Mathf.Min(segments.Length, phases.Length);

        for (int i = 0; i < n; i++)
        {
            if (segments[i] == null || phases[i] == null) continue;

            float end = i + 1 < phases.Length && phases[i + 1] != null ? phases[i + 1].startTime : spawner.dayLength;
            float length = Mathf.Max(1f, end - phases[i].startTime);

            LayoutElement le = segments[i].GetComponent<LayoutElement>();
            if (le == null) le = segments[i].gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = length;

            if (autoLabel)
            {
                TMP_Text label = segments[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = phases[i].name;
            }
        }
    }

    private void Update()
    {
        float t = spawner.dayLength > 0f ? Mathf.Clamp01(spawner.DayTime / spawner.dayLength) : 0f;
        slider.SetValueWithoutNotify(t);

        if (clockText != null && spawner.ClockText != lastClock)
        {
            lastClock = spawner.ClockText;
            clockText.text = lastClock;
        }

        UpdateRushTag();
    }

    private int CurrentPhaseIndex()
    {
        int index = -1;
        CustomerSpawner.Phase[] phases = spawner.phases;
        for (int i = 0; i < phases.Length; i++)
            if (phases[i] != null && spawner.DayTime >= phases[i].startTime) index = i;
        return index;
    }

    private void UpdateRushTag()
    {
        if (rushTag == null) return;

        int phase = CurrentPhaseIndex();
        bool rush = phase >= 0 && !string.IsNullOrEmpty(rushWord) && spawner.phases[phase].name.Contains(rushWord);

        // 구간이 바뀌는 순간
        if (phase != lastPhase)
        {
            lastPhase = phase;
            rushTimer = 0f;
            if (rush) popTimer = 0f; // 뿅 하고 나타나기
        }
        rushTimer += Time.deltaTime;

        bool show = rush && spawner.DayRunning && (rushTagSeconds <= 0f || rushTimer < rushTagSeconds);
        if (rushTag.gameObject.activeSelf != show) rushTag.gameObject.SetActive(show);
        if (!show) return;

        // 지금 칸 가운데 아래로 이동
        if (segments != null && phase < segments.Length && segments[phase] != null)
        {
            RectTransform seg = segments[phase];
            Vector3 center = seg.TransformPoint(seg.rect.center);
            Vector3 p = rushTag.position;
            p.x = center.x;
            rushTag.position = p;
        }

        // 위아래로 살랑살랑
        Vector2 ap = rushTag.anchoredPosition;
        ap.y = rushBaseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        rushTag.anchoredPosition = ap;

        // 나타날 때 커졌다 돌아오기
        popTimer += Time.deltaTime;
        float k = Mathf.Clamp01(popTimer / 0.3f);
        float s = k < 1f ? EaseOutBack(k) : 1f;
        rushTag.localScale = new Vector3(s, s, 1f);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
