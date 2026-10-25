using TMPro;
using UnityEngine;

// 영업 준비 중에 하루 바 아래에 뜨는 알약 ("영업 준비  0:25  Enter 바로 열기")
// 준비가 끝나면 숨김. 마지막 5초는 숫자가 빨개지고 1초마다 톡 튐
// 이 스크립트는 알약의 "부모"(HUD Canvas 등)에 붙이기. 알약 자체에 붙이면 숨길 때 같이 멈춤
public class PrepCountdownUI : MonoBehaviour
{
    [Tooltip("비우면 자동으로 찾음")]
    public CustomerSpawner spawner;
    [Tooltip("준비 중에만 보일 알약 (PrepTag)")]
    public RectTransform root;
    [Tooltip("남은 시간 글자")]
    public TMP_Text timeText;
    [Tooltip("\"Enter 바로 열기\" 글자 (선택). 스포너의 Allow Skip을 끄면 숨김")]
    public TMP_Text hintText;

    [Header("끝나갈 때")]
    [Tooltip("이 초부터 숫자가 빨개지고 튐")]
    public float urgentTime = 5f;
    public Color normalColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
    public Color urgentColor = new Color32(0xFF, 0x8A, 0x8A, 0xFF);
    public float punchScale = 0.08f;
    public float punchTime = 0.2f;

    private int shownSeconds = -1;
    private float punchTimer = 99f;

    private void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (root != null && root == transform)
            Debug.LogWarning("[PrepCountdownUI] 이 스크립트는 알약의 부모에 붙이고, Root에는 알약을 연결하세요.", this);
        SetVisible(false);
    }

    private void Update()
    {
        bool on = spawner != null && spawner.Preparing;
        SetVisible(on);
        if (!on) { shownSeconds = -1; return; }

        int s = Mathf.Max(0, Mathf.CeilToInt(spawner.PrepTimeLeft));
        if (s != shownSeconds)
        {
            shownSeconds = s;
            if (timeText != null)
            {
                timeText.text = $"{s / 60}:{s % 60:00}";
                timeText.color = s <= urgentTime ? urgentColor : normalColor;
            }
            if (s <= urgentTime) punchTimer = 0f;
        }
        if (hintText != null && hintText.gameObject.activeSelf != spawner.allowSkip)
            hintText.gameObject.SetActive(spawner.allowSkip);

        // 톡 튀기
        if (root != null)
        {
            float k = 1f;
            if (punchTimer < punchTime)
            {
                punchTimer += Time.unscaledDeltaTime;
                k = 1f + punchScale * Mathf.Sin(Mathf.Clamp01(punchTimer / punchTime) * Mathf.PI);
            }
            root.localScale = new Vector3(k, k, 1f);
        }
    }

    private void SetVisible(bool on)
    {
        if (root != null && root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
    }
}
