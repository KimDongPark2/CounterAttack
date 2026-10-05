using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 장바구니 목록 한 줄 (키오스크형)
//  코코시리얼 [x3]          1,800원  (X)
// 필요한 칸만 연결하면 되고, 안 쓰는 칸은 비워둬도 된다
public class CartSlotUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI nameText;      // 코코시리얼
    public TextMeshProUGUI countText;     // x3  (수량 박스 안 글자)
    public TextMeshProUGUI subtotalText;  // 1,800원

    [Header("안 쓰면 비워두기")]
    public Image icon;                                                       // 상품 사진
    [FormerlySerializedAs("priceText")] public TextMeshProUGUI detailText;  // 600원 x 3개

    [Header("Buttons (안 쓰면 비워두기)")]
    public Button removeButton;  // X 버튼
    public Button plusButton;
    public Button minusButton;

    public void Bind(Item_info item, int count, ShoppingBags cart)
    {
        if (nameText) nameText.text = item.Name;
        if (countText) countText.text = $"x{count}";
        if (subtotalText) subtotalText.text = Player_State.Won(item.Price * count);

        if (icon)
        {
            icon.sprite = item.iconImage;
            icon.enabled = item.iconImage != null;
        }
        if (detailText) detailText.text = $"{Player_State.Won(item.Price)} x {count}개";

        Connect(removeButton, () => cart.Remove(item));
        Connect(plusButton, () => cart.SetCount(item, count + 1));
        Connect(minusButton, () => cart.SetCount(item, count - 1));
    }

    private static void Connect(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}