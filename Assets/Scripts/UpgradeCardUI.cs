using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 카테고리 상품(업그레이드) 카드
// 장바구니/배달 없이 "구매"를 누르면 바로 돈을 내고 적용된다
//   이동 속도: 여러 번 살 수 있음
//   권총: 한 번만 살 수 있음. 사면 핫바 6번 칸에 권총이 생김
public class UpgradeCardUI : MonoBehaviour
{
    public enum UpgradeType
    {
        [InspectorName("이동 속도")] MoveSpeed,
        [InspectorName("권총")] Pistol,   // (예전 "가방 칸" 자리. 기존 가방 카드는 자동으로 권총 카드가 됨)
    }

    [Header("업그레이드 종류")]
    public UpgradeType type;

    [Header("연결")]
    public Player_State state;
    public Player_movement movement;    // 이동 속도일 때만 필요

    [Header("가격과 단계")]
    public int basePrice = 3000;        // 첫 구매 가격(원)
    public int priceIncrease = 2000;    // 한 번 살 때마다 오르는 가격
    [Min(1)] public int maxLevel = 5;   // 최대 몇 번 살 수 있는지 (권총은 무조건 1번)
    public float speedPerLevel = 1f;    // 이동 속도: 한 번에 오르는 양

    [Header("UI")]
    public TextMeshProUGUI levelText;      // "Lv 1 / 5"
    public TextMeshProUGUI valueText;      // "이동 속도 5 → 6" (없으면 비워도 됨)
    public TextMeshProUGUI priceText;      // "3,000원"
    public Button buyButton;
    public TextMeshProUGUI buyButtonText;  // "구매" / "최대"

    private int level = 0;

    private int MaxLevel => type == UpgradeType.Pistol ? 1 : maxLevel;
    private int Price => basePrice + priceIncrease * level;
    private bool IsMax => level >= MaxLevel;

    private void Start()
    {
        if (buyButton) buyButton.onClick.AddListener(Buy);
        if (type == UpgradeType.Pistol && state && state.hasGun) level = 1; // 이미 갖고 있으면 산 걸로
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

    public void Buy()
    {
        if (IsMax || state == null) return;
        if (!state.TrySpend(Price)) return;  // 돈이 모자라면 아무 일도 안 함

        Apply();
        level++;
        Refresh();
    }

    private void Apply()
    {
        switch (type)
        {
            case UpgradeType.MoveSpeed:
                if (movement)
                {
                    // 걷기만 빨라지면 달리기보다 빨라질 수 있어서 둘 다 같이 올림
                    movement.moveSpeed += speedPerLevel;
                    movement.RunningSpeed += speedPerLevel;
                }
                break;

            case UpgradeType.Pistol:
                state.hasGun = true;  // 핫바 6번 칸은 Player_Interaction이 보고 알아서 켬
                break;
        }
    }

    private void Refresh()
    {
        if (levelText) levelText.text = $"Lv {level} / {MaxLevel}";
        if (valueText) valueText.text = ValueLabel();
        if (priceText) priceText.text = IsMax ? "-" : Player_State.Won(Price);
        if (buyButtonText) buyButtonText.text = IsMax ? (type == UpgradeType.Pistol ? "보유 중" : "최대") : "구매";
        if (buyButton) buyButton.interactable = !IsMax && state != null && state.money >= Price;
    }

    private string ValueLabel()
    {
        switch (type)
        {
            case UpgradeType.MoveSpeed:
                if (!movement) return "";
                return IsMax ? $"이동 속도 {movement.moveSpeed}"
                             : $"이동 속도 {movement.moveSpeed} → {movement.moveSpeed + speedPerLevel}";

            case UpgradeType.Pistol:
                return IsMax ? "6번 키로 꺼내기" : "핫바 6번 칸에 권총 추가";
        }
        return "";
    }
}
