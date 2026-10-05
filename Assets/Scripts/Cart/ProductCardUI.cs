using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 주문 화면 왼쪽 상품 카드 한 장 (A안)
// 사진, 이름, 설명, 가격, 수량 − / +, 담기 버튼
// 카드를 Ctrl+D로 복제하고 Category와 Item 칸만 바꾸면 된다
public class ProductCardUI : MonoBehaviour
{
    // 카테고리 탭에서 거를 때 쓰는 분류 (Inspector에는 한글로 보임)
    public enum Category
    {
        [InspectorName("음식")] Food,
        [InspectorName("플레이어")] Player,
        [InspectorName("물건")] Goods,
    }

    [Header("카테고리")]
    public Category category;

    [Header("상품")]
    [Tooltip("팔 상품 프리팹 (Item_info가 붙어 있는 것)")]
    public Item_info item;
    public ShoppingBags cart;

    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI quantityText;
    public Button minusButton;
    public Button plusButton;
    public Button addButton;

    [Min(1)] public int maxQuantity = 99;

    private int quantity = 1;

    private void Start()
    {
        if (minusButton) minusButton.onClick.AddListener(() => SetQuantity(quantity - 1));
        if (plusButton) plusButton.onClick.AddListener(() => SetQuantity(quantity + 1));
        if (addButton) addButton.onClick.AddListener(AddToCart);
        Show(item);
    }

    // 코드에서 다른 상품으로 바꿔 끼울 때도 사용 가능
    public void Show(Item_info newItem)
    {
        item = newItem;
        if (item == null) return;

        if (icon)
        {
            icon.sprite = item.iconImage;
            icon.enabled = item.iconImage != null;
        }
        if (nameText) nameText.text = item.Name;
        if (descText) descText.text = item.Description;
        if (priceText) priceText.text = $"{Player_State.Won(item.Price)} <size=15>개당</size>";
        SetQuantity(1);
    }

    private void SetQuantity(int value)
    {
        quantity = Mathf.Clamp(value, 1, maxQuantity);
        if (quantityText) quantityText.text = quantity.ToString();
    }

    private void AddToCart()
    {
        if (item == null || cart == null) return;
        cart.Add(item, quantity);
        SetQuantity(1);
    }
}