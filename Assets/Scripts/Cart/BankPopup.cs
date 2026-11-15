using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 대출 · 상환 팝업 (상품 주문 → 플레이어의 "대출 받기" / "빚 갚기" 카드를 누르면 뜸)
//  - 금액을 직접 치거나 +1천 · +5천 · +1만 · 최대 버튼으로 정한 다음 [빌리기] 또는 [갚기]
//  - 빌리면 빚이 이자만큼 더 늘어남 (이자 10% → 10,000원 빌리면 빚 +11,000원)
//  - 빚 한도를 넘게는 못 빌리고, 가진 돈보다 많이는 못 갚음 (못 하는 버튼은 회색)
//  - 금액이 0이면 갚기 버튼이 "전부 갚기"로 바뀜 (가진 돈 안에서 갚을 수 있는 만큼 한 번에)
//  - 남은 빚보다 많이 치고 갚으면 남은 빚만큼만 갚음
// 꺼둔 채로 두고, 대출 받기 / 빚 갚기 카드(FinanceCardUI)의 Popup 칸에 넣기
public class BankPopup : MonoBehaviour
{
    [Header("연결 (State는 비우면 자동으로 찾음)")]
    public Player_State state;
    public TMP_InputField amountInput;
    public Button borrowButton;
    public Button repayButton;
    [Tooltip("갚기 버튼 글자 (\"갚기\" / \"전부 갚기\")")]
    public TMP_Text repayButtonText;
    public Button closeButton;
    [Tooltip("뒤 화면을 덮는 어두운 배경. 누르면 닫힘 (선택)")]
    public Button dimButton;

    [Header("빠른 금액 버튼")]
    public Button add1000;
    public Button add5000;
    public Button add10000;
    public Button maxButton;
    public Button clearButton;

    [Header("글자")]
    [Tooltip("지금 빚 \"5,500원\"")]
    public TMP_Text debtText;
    [Tooltip("\"한도 20,000원\"")]
    public TMP_Text limitText;
    [Tooltip("\"빌리면 빚 +11,000원\"")]
    public TMP_Text borrowPreview;
    [Tooltip("\"갚으면 빚 -3,000원\"")]
    public TMP_Text repayPreview;
    [Tooltip("빚 막대 안쪽 빨간 칸 (가로 길이를 스크립트가 바꿈)")]
    public RectTransform debtFill;

    [Header("설정")]
    [Tooltip("대출 이자(%). 10이면 10,000원 빌릴 때 빚이 11,000원 늘어남")]
    [Range(0, 100)] public int interestPercent = 10;
    [Tooltip("빚이 이 금액을 넘게는 못 빌림 (0이면 제한 없음)")]
    public int maxDebt = 20000;
    [Tooltip("최대 버튼이 이 단위로 내림 (100이면 13,181원 → 13,100원)")]
    public int roundUnit = 100;
    [Tooltip("입력칸에 칠 수 있는 최대 금액")]
    public int maxInput = 9999999;
    public Color borrowColor = new Color32(0xD4, 0x24, 0x30, 0xFF);
    public Color repayColor = new Color32(0x1E, 0x9E, 0x5A, 0xFF);

    // 팝업이 떠 있는지
    public static bool IsOpen { get; private set; }

    private int amount;
    private bool hooked;

    private void Awake()
    {
        if (state == null) state = FindAnyObjectByType<Player_State>();
        Hook();
    }

    private void OnEnable()
    {
        IsOpen = true;
        if (state == null) state = FindAnyObjectByType<Player_State>();
        if (state != null) state.OnMoneyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        IsOpen = false;
        if (state != null) state.OnMoneyChanged -= Refresh;
    }

    private void Hook()
    {
        if (hooked) return;
        hooked = true;
        if (amountInput != null)
        {
            amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            amountInput.characterLimit = maxInput.ToString().Length;
            amountInput.onValueChanged.AddListener(OnTyped);
        }
        On(borrowButton, Borrow);
        On(repayButton, Repay);
        On(closeButton, Close);
        On(dimButton, Close);
        On(add1000, () => Add(1000));
        On(add5000, () => Add(5000));
        On(add10000, () => Add(10000));
        On(maxButton, () => { Click(); SetAmount(MaxBorrow); });
        On(clearButton, () => { Click(); SetAmount(0); });
    }

    // ---------- 열기 · 닫기 ----------
    // 대출 받기 / 빚 갚기 카드가 부름
    public void Open()
    {
        Hook();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // 다른 화면 위에 그려지게
        SetAmount(0);
    }

    public void Close()
    {
        Click();
        gameObject.SetActive(false);
    }

    // ---------- 금액 ----------
    private void OnTyped(string text)
    {
        int v = 0;
        if (!string.IsNullOrEmpty(text) && !int.TryParse(text, out v)) v = maxInput;
        int clamped = Mathf.Clamp(v, 0, maxInput);
        amount = clamped;
        if (clamped != v && amountInput != null) amountInput.SetTextWithoutNotify(clamped.ToString());
        Refresh();
    }

    private void Add(int value)
    {
        Click();
        SetAmount(amount + value);
    }

    private void SetAmount(int value)
    {
        amount = Mathf.Clamp(value, 0, maxInput);
        if (amountInput != null) amountInput.SetTextWithoutNotify(amount > 0 ? amount.ToString() : "");
        Refresh();
    }

    // 빌리면 늘어나는 빚 (이자 포함)
    private int DebtAdd(int money) => money + Mathf.RoundToInt(money * interestPercent / 100f);

    // 지금 더 빌릴 수 있는 최대 금액
    private int MaxBorrow
    {
        get
        {
            if (state == null) return 0;
            if (maxDebt <= 0) return maxInput;
            int room = maxDebt - state.debt;
            if (room <= 0) return 0;
            int unit = Mathf.Max(1, roundUnit);
            int m = Mathf.FloorToInt(room * 100f / (100 + interestPercent));
            m -= m % unit;
            while (m > 0 && DebtAdd(m) > room) m -= unit;
            return Mathf.Clamp(m, 0, maxInput);
        }
    }

    // 지금 갚을 수 있는 최대 금액 (남은 빚과 가진 돈 중 작은 쪽)
    private int MaxRepay => state == null ? 0 : Mathf.Max(0, Mathf.Min(state.debt, state.money));

    // 이번에 갚을 돈 (0이면 갚을 수 있는 만큼 전부, 남은 빚보다 많으면 남은 빚만큼)
    private int RepayAmount => state == null ? 0 : (amount > 0 ? Mathf.Min(amount, state.debt) : MaxRepay);

    private bool CanBorrow => state != null && amount > 0 && amount <= MaxBorrow;
    private bool CanRepay => state != null && state.debt > 0 && RepayAmount > 0 && state.money >= RepayAmount;

    // ---------- 빌리기 · 갚기 ----------
    public void Borrow()
    {
        if (!CanBorrow) return;
        state.debt += DebtAdd(amount);   // 빚 먼저 (돈 알림이 갈 때 다른 화면이 새 빚을 보게)
        state.Earn(amount);
        SoundManager.Play(Sfx.Pay);
        SetAmount(0);
    }

    public void Repay()
    {
        if (!CanRepay) return;
        int pay = RepayAmount;
        state.debt -= pay;
        if (!state.TrySpend(pay)) state.debt += pay;   // 혹시 못 냈으면 되돌림
        SoundManager.Play(Sfx.Pay);
        SetAmount(0);
    }

    // ---------- 화면 ----------
    private void Refresh()
    {
        if (state == null) return;
        string red = "#" + ColorUtility.ToHtmlStringRGB(borrowColor);
        string green = "#" + ColorUtility.ToHtmlStringRGB(repayColor);

        Set(debtText, Player_State.Won(state.debt));
        Set(limitText, maxDebt > 0 ? "한도 " + Player_State.Won(maxDebt) : "한도 없음");
        if (debtFill != null)
        {
            float ratio = maxDebt > 0 ? Mathf.Clamp01((float)state.debt / maxDebt) : 0f;
            debtFill.anchorMin = new Vector2(0f, debtFill.anchorMin.y);
            debtFill.anchorMax = new Vector2(ratio, debtFill.anchorMax.y);
            debtFill.gameObject.SetActive(ratio > 0f);
        }

        // 빌리기 미리보기
        int maxBorrow = MaxBorrow;
        if (amount <= 0) Set(borrowPreview, maxBorrow > 0 ? $"최대 {Player_State.Won(maxBorrow)} 빌릴 수 있어요" : "한도가 꽉 찼어요");
        else if (CanBorrow) Set(borrowPreview, $"빌리면 빚 <color={red}>+{Player_State.Won(DebtAdd(amount))}</color>");
        else Set(borrowPreview, $"<color={red}>한도 초과</color> (최대 {Player_State.Won(maxBorrow)})");

        // 갚기 미리보기
        int pay = RepayAmount;
        if (state.debt <= 0) Set(repayPreview, "갚을 빚이 없어요");
        else if (CanRepay) Set(repayPreview, $"{(amount <= 0 ? "전부 갚으면" : "갚으면")} 빚 <color={green}>-{Player_State.Won(pay)}</color>");
        else Set(repayPreview, $"<color={red}>돈이 모자라요</color> (최대 {Player_State.Won(MaxRepay)})");

        if (borrowButton != null) borrowButton.interactable = CanBorrow;
        if (repayButton != null) repayButton.interactable = CanRepay;
        Set(repayButtonText, amount <= 0 ? "전부 갚기" : "갚기");
    }

    private static void Click() => SoundManager.Play(Sfx.ButtonClick);

    private static void On(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b != null) b.onClick.AddListener(action);
    }

    private static void Set(TMP_Text t, string s)
    {
        if (t != null) t.text = s;
    }
}
