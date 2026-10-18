using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 계산대 UI (계산 3안): 합계 카드 + 손님 인내심 카드
// 계산대 UI 아무 오브젝트에 붙이고 칸만 연결하면 됨. 비워둔 칸은 무시함
public class CounterHUD : MonoBehaviour
{
    [Header("합계 카드")]
    [Tooltip("\"6,300원\"")]
    public TMP_Text totalText;
    [Tooltip("\"2 / 3개 찍음\"")]
    public TMP_Text progressText;
    [Tooltip("물건 개수만큼 켜지는 동그라미들 (넉넉하게 만들어 두기)")]
    public Image[] pips;
    public Sprite pipOn;
    public Sprite pipOff;

    [Header("손님 카드")]
    [Tooltip("손님이 없을 때 숨길 카드 (비우면 안 숨김)")]
    public GameObject customerCard;
    public Image faceImage;
    public Sprite faceHappy;
    public Sprite faceWorried;
    public Sprite faceAngry;
    [Tooltip("\"급한 손님\" 태그 (급한 손님일 때만 보임)")]
    public GameObject hurryTag;
    [Tooltip("인내심 Slider (Max Value 몇이든 됨, 예: 100). Slider를 쓰면 아래 Patience Fill은 비워도 됨")]
    public Slider patienceSlider;
    [Tooltip("Slider 대신 Image로 만든 바를 쓸 때만. Image Type Filled(Horizontal, Origin Left) 또는 Sliced")]
    public Image patienceFill;
    [Tooltip("켜면 인내심에 따라 바 색이 초록 → 주황 → 빨강으로 바뀜")]
    public bool changeBarColor = true;
    [Tooltip("\"45%\"")]
    public TMP_Text patienceText;

    [Header("인내심 색")]
    public Color goodColor = new Color32(0x3D, 0xBE, 0x6A, 0xFF);
    public Color warnColor = new Color32(0xF2, 0x9D, 0x12, 0xFF);
    public Color dangerColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    [Range(0f, 1f)] public float warnBelow = 0.6f;    // 이 아래면 주황 + 걱정 표정
    [Range(0f, 1f)] public float dangerBelow = 0.3f;  // 이 아래면 빨강 + 화난 표정

    private void Start()
    {
        // 플레이어가 마우스로 Slider를 끌지 못하게
        if (patienceSlider != null) patienceSlider.interactable = false;
    }

    private void Update()
    {
        CheckoutCounter counter = CheckoutCounter.Instance;
        Customer front = counter != null ? counter.Front : null;

        UpdateTotal(counter, front);
        UpdateCustomer(front);
    }

    private void UpdateTotal(CheckoutCounter counter, Customer front)
    {
        int total = counter != null ? counter.OrderTotal : 0;   // 손님이 가져온 물건 전체 금액
        int all = counter != null ? counter.ItemCount : 0;
        int done = counter != null ? counter.ScannedCount : 0;

        if (totalText != null) totalText.text = Player_State.Won(total);

        if (progressText != null)
        {
            if (all > 0) progressText.text = $"{done} / {all}개 찍음";
            else if (front != null) progressText.text = "손님이 오는 중";
            else progressText.text = "손님 없음";
        }

        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null) continue;
            bool show = i < all;
            if (pips[i].gameObject.activeSelf != show) pips[i].gameObject.SetActive(show);
            if (show) pips[i].sprite = i < done ? pipOn : pipOff;
        }
    }

    private void UpdateCustomer(Customer front)
    {
        bool has = front != null;
        if (customerCard != null && customerCard.activeSelf != has) customerCard.SetActive(has);

        // 손님이 없으면 바를 꽉 채우고 웃는 얼굴
        float ratio = has ? front.PatienceRatio : 1f;
        Color col = ratio < dangerBelow ? dangerColor : ratio < warnBelow ? warnColor : goodColor;

        if (patienceSlider != null)
        {
            patienceSlider.value = Mathf.Lerp(patienceSlider.minValue, patienceSlider.maxValue, ratio);
            if (changeBarColor && patienceSlider.fillRect != null)
            {
                Image fill = patienceSlider.fillRect.GetComponent<Image>();
                if (fill != null) fill.color = col;
            }
        }

        if (patienceFill != null)
        {
            if (changeBarColor) patienceFill.color = col;
            if (patienceFill.type == Image.Type.Filled)
            {
                patienceFill.fillAmount = ratio;
            }
            else
            {
                // Sliced로 쓰면 오른쪽 끝을 줄여서 둥근 끝이 유지되게 (앵커를 좌우로 늘린 상태여야 함)
                RectTransform rt = patienceFill.rectTransform;
                rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
            }
        }
        if (patienceText != null) patienceText.text = $"{Mathf.CeilToInt(ratio * 100f)}%";

        if (faceImage != null)
        {
            Sprite face = ratio < dangerBelow ? faceAngry : ratio < warnBelow ? faceWorried : faceHappy;
            if (face != null) faceImage.sprite = face;
        }

        if (hurryTag != null)
        {
            bool hurry = has && front.Type == Customer.CustomerType.Hurry;
            if (hurryTag.activeSelf != hurry) hurryTag.SetActive(hurry);
        }
    }
}