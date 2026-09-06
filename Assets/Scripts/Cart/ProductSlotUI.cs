using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductSlotUI : MonoBehaviour
{
    [Header("Data")]
    public Item_info item;          // Project 창의 프리팹을 드래그
    public ShoppingBags cart;

    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Button buyButton;

    void Start()
    {
        if (item == null) { Debug.LogError($"{name}: item 미지정"); return; }

        if (icon) icon.sprite = item.iconImage;
        if (nameText) nameText.text = item.Name;
        if (priceText) priceText.text = $"가격 : {item.Price}$";

        buyButton.onClick.AddListener(OnClickBuy);
    }

    void OnClickBuy()
    {
        cart.Add(item, 1);
    }
}