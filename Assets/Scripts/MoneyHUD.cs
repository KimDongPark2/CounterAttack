using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 위 돈 칩
// - 돈이 바뀌면 숫자가 촤라락 올라가거나 내려감
// - 칩이 살짝 튀고, 아래에 +6,300원 / -1,200원 팝업이 떴다 사라짐
// - 짧은 시간에 여러 번 들어오면 팝업 금액이 합쳐짐
// - 빚이 있으면 칩 아래에 빨간 "빚 10,000원" 꼬리표 (빚이 0이면 숨김)
// 칩 오브젝트(MoneyChip)에 붙이면 됨
public class MoneyHUD : MonoBehaviour
{
    public Player_State state;
    public TMP_Text moneyText;
    [Tooltip("숫자 뒤에 붙는 글자. <size>로 작게")]
    public string unit = "<size=65%>원</size>";
    [Tooltip("숫자가 목표까지 가는 시간 (초)")]
    public float countTime = 0.5f;

    [Header("칩 튀기")]
    [Tooltip("비우면 이 오브젝트가 튐")]
    public RectTransform punchTarget;
    public float punchScale = 0.12f;
    public float punchTime = 0.25f;

    [Header("빚 꼬리표")]
    [Tooltip("빨간 꼬리표 오브젝트. 빚이 0이면 꺼짐")]
    public RectTransform debtTag;
    public TMP_Text debtText;
    [Tooltip("{0} = 빚")]
    public string debtFormat = "빚 {0:N0}원";
    [Tooltip("꼬리표가 보일 때 팝업을 이만큼 더 아래로 (꼬리표 높이 + 간격)")]
    public float popupShiftWhenDebt = 60f;

    [Header("팝업")]
    public RectTransform popup;
    public TMP_Text popupText;
    public Image popupBg;
    public Color gainColor = new Color32(0x1E, 0x9E, 0x5A, 0xFF);
    public Color spendColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    [Tooltip("팝업이 떠 있는 시간 (초)")]
    public float popupStay = 1.2f;
    [Tooltip("사라질 때 위로 올라가는 거리")]
    public float popupRise = 20f;

    private int target;
    private float countFrom;
    private float shown;
    private float countTimer;

    private float punchTimer = 99f;

    private int shownDebt = int.MinValue;
    private float debtPunchTimer = 99f;

    private CanvasGroup popupGroup;
    private float popupBaseY;
    private float popupTimer = 99f;
    private int popupSum;

    private void Start()
    {
        if (state == null) state = FindFirstObjectByType<Player_State>();
        if (punchTarget == null) punchTarget = (RectTransform)transform;

        if (popup != null)
        {
            popupGroup = popup.GetComponent<CanvasGroup>();
            if (popupGroup == null) popupGroup = popup.gameObject.AddComponent<CanvasGroup>();
            popupGroup.alpha = 0f;
            popupGroup.blocksRaycasts = false;
            popupBaseY = popup.anchoredPosition.y;
        }

        if (state != null)
        {
            Snap(state.money);
            UpdateDebt(false);
        }
    }

    private void Update()
    {
        if (state == null) return;

        if (state.money != target)
        {
            int diff = state.money - target;
            // 게임 켜자마자 시작 금액 들어오는 건 팝업 없이 바로
            if (Time.timeSinceLevelLoad < 0.5f) Snap(state.money);
            else Change(diff);
        }

        if (state.debt != shownDebt) UpdateDebt(true);

        UpdateCount();
        UpdatePunch();
        UpdateDebtPunch();
        UpdatePopup();
    }

    private void Snap(int value)
    {
        target = value;
        shown = value;
        countTimer = countTime;
        SetText(value);
    }

    private void Change(int diff)
    {
        countFrom = shown;
        target = state.money;
        countTimer = 0f;
        punchTimer = 0f;

        // 팝업이 아직 떠 있고 같은 방향(벌기/쓰기)이면 금액 합치기
        bool visible = popupTimer < popupStay;
        if (visible && Mathf.Sign(popupSum) == Mathf.Sign(diff)) popupSum += diff;
        else popupSum = diff;
        popupTimer = 0f;

        if (popupText != null)
            popupText.text = (popupSum > 0 ? "+" : "-") + Mathf.Abs(popupSum).ToString("N0") + "원";
        if (popupBg != null) popupBg.color = popupSum > 0 ? gainColor : spendColor;
    }

    private void UpdateCount()
    {
        if (countTimer >= countTime) return;
        countTimer += Time.unscaledDeltaTime;
        float k = countTime > 0f ? Mathf.Clamp01(countTimer / countTime) : 1f;
        k = 1f - (1f - k) * (1f - k); // 끝에서 천천히
        shown = Mathf.Lerp(countFrom, target, k);
        SetText(Mathf.RoundToInt(shown));
    }

    private void SetText(int value)
    {
        if (moneyText != null) moneyText.text = value.ToString("N0") + unit;
    }

    private void UpdatePunch()
    {
        if (punchTarget == null) return;
        if (punchTimer >= punchTime)
        {
            punchTarget.localScale = Vector3.one;
            return;
        }
        punchTimer += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(punchTimer / punchTime);
        float s = 1f + punchScale * Mathf.Sin(k * Mathf.PI);
        punchTarget.localScale = new Vector3(s, s, 1f);
    }

    // 빚 꼬리표 보이기/숨기기 + 글자
    private void UpdateDebt(bool animate)
    {
        shownDebt = state.debt;
        bool has = shownDebt > 0;
        if (debtTag != null && debtTag.gameObject.activeSelf != has) debtTag.gameObject.SetActive(has);
        if (debtText != null) debtText.text = string.Format(debtFormat, shownDebt);
        if (animate && has) debtPunchTimer = 0f;
    }

    private void UpdateDebtPunch()
    {
        if (debtTag == null) return;
        if (debtPunchTimer >= punchTime)
        {
            debtTag.localScale = Vector3.one;
            return;
        }
        debtPunchTimer += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(debtPunchTimer / punchTime);
        float s = 1f + punchScale * 1.5f * Mathf.Sin(k * Mathf.PI);
        debtTag.localScale = new Vector3(s, s, 1f);
    }

    private void UpdatePopup()
    {
        if (popup == null || popupGroup == null) return;

        const float fadeIn = 0.12f;
        const float fadeOut = 0.35f;
        popupTimer += Time.unscaledDeltaTime;
        float e = popupTimer;

        bool debtShown = debtTag != null && debtTag.gameObject.activeSelf;
        float alpha, y = popupBaseY - (debtShown ? popupShiftWhenDebt : 0f), s = 1f;
        if (e < fadeIn)
        {
            alpha = e / fadeIn;
            s = Mathf.Lerp(0.6f, 1f, alpha);
        }
        else if (e < popupStay)
        {
            alpha = 1f;
        }
        else if (e < popupStay + fadeOut)
        {
            float k = (e - popupStay) / fadeOut;
            alpha = 1f - k;
            y += popupRise * k;
        }
        else alpha = 0f;

        popupGroup.alpha = alpha;
        popup.localScale = new Vector3(s, s, 1f);
        Vector2 ap = popup.anchoredPosition;
        ap.y = y;
        popup.anchoredPosition = ap;
    }
}
