using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상품 / 재화 화면 "플레이어" 카테고리의 재화 카드
//   대출 받기: 누르면 돈이 들어오고 빚이 늘어남 (이자를 넣으면 빚이 더 늘어남)
//   빚 갚기:   누르면 돈을 내고 빚이 줄어듦
// 장바구니/배달 없이 버튼 누르면 바로 적용
public class FinanceCardUI : MonoBehaviour, IShopCategory
{
    public enum FinanceType
    {
        [InspectorName("대출 받기")] Loan,
        [InspectorName("빚 갚기")] Repay,
    }

    [Header("종류")]
    public FinanceType type;

    [Header("연결")]
    public Player_State state;

    [Header("금액")]
    [Tooltip("한 번에 빌리는/갚는 돈(원). 빚 갚기에서 0이면 남은 빚을 한 번에 다 갚음")]
    public int amount = 10000;
    [Tooltip("대출 이자(%). 10이면 10,000원 빌릴 때 빚이 11,000원 늘어남")]
    [Range(0, 100)] public int interestPercent = 0;
    [Tooltip("빚이 이 금액을 넘으면 더 못 빌림. 0이면 제한 없음")]
    public int maxDebt = 50000;

    [Header("UI (비워도 되는 칸은 비워도 됨)")]
    public TextMeshProUGUI amountText;     // "+10,000원" / "-10,000원"
    public TextMeshProUGUI debtText;       // "지금 빚 10,000원"
    public TextMeshProUGUI infoText;       // "갚을 때 11,000원" / "남은 빚 다 갚기"
    public Button button;
    public TextMeshProUGUI buttonText;     // "빌리기" / "갚기" / "한도 초과" / "빚 없음" / "돈 부족"

    public ProductCardUI.Category ShopCategory => ProductCardUI.Category.Player;

    private void Start()
    {
        if (button) button.onClick.AddListener(Use);
        Refresh();
    }

    private void OnEnable()
    {
        if (state) state.OnMoneyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (state) state.OnMoneyChanged -= Refresh;
    }

    // 대출하면 늘어나는 빚 (이자 포함)
    private int DebtAdd => amount + amount * interestPercent / 100;

    // 이번에 갚을 돈
    private int RepayAmount
    {
        get
        {
            if (state == null) return 0;
            int want = amount > 0 ? amount : state.debt;
            return Mathf.Min(want, state.debt);
        }
    }

    private bool CanUse
    {
        get
        {
            if (state == null) return false;
            if (type == FinanceType.Loan) return maxDebt <= 0 || state.debt + DebtAdd <= maxDebt;
            int pay = RepayAmount;
            return pay > 0 && state.money >= pay;
        }
    }

    public void Use()
    {
        SoundManager.Play(Sfx.ButtonClick);
        if (!CanUse) return;

        if (type == FinanceType.Loan)
        {
            state.debt += DebtAdd;   // 빚 먼저 (돈 알림이 갈 때 다른 카드가 새 빚을 보게)
            state.Earn(amount);
        }
        else
        {
            int pay = RepayAmount;
            state.debt -= pay;
            if (!state.TrySpend(pay)) state.debt += pay;  // 혹시 못 냈으면 되돌림
        }
        Refresh();
    }

    private void Refresh()
    {
        if (state == null) return;

        if (type == FinanceType.Loan)
        {
            SetText(amountText, "+" + Player_State.Won(amount));
            SetText(infoText, interestPercent > 0 ? $"갚을 때 {Player_State.Won(DebtAdd)} (이자 {interestPercent}%)" : "이자 없음");
            SetText(buttonText, CanUse ? "빌리기" : "한도 초과");
        }
        else
        {
            int pay = RepayAmount;
            SetText(amountText, pay > 0 ? "-" + Player_State.Won(pay) : "-");
            SetText(infoText, amount > 0 ? "" : "남은 빚 다 갚기");
            SetText(buttonText, state.debt <= 0 ? "빚 없음" : (CanUse ? "갚기" : "돈 부족"));
        }
        SetText(debtText, "지금 빚 " + Player_State.Won(state.debt));
        if (button) button.interactable = CanUse;
    }

    private static void SetText(TextMeshProUGUI t, string s)
    {
        if (t != null) t.text = s;
    }
}