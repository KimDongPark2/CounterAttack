using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CartSlotUI : MonoBehaviour
{
    [Header("UI")]
    //public Image icon;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI countText;
    //public TextMeshProUGUI subtotalText;
    public Button plusButton;
    public Button minusButton;
    public Button removeButton;

    public void Bind(Item_info item, int count, ShoppingBags cart)
    {
        //if (icon) icon.sprite = item.iconImage;
        if (nameText) nameText.text = item.Name;
        if (priceText) priceText.text = $"{item.Price}$";
        if (countText) countText.text = count.ToString();
        //if (subtotalText) subtotalText.text = $"{item.Price * count}$";

        plusButton.onClick.RemoveAllListeners();
        minusButton.onClick.RemoveAllListeners();
        removeButton.onClick.RemoveAllListeners();

        plusButton.onClick.AddListener(() => cart.SetCount(item, count + 1));
        minusButton.onClick.AddListener(() => cart.SetCount(item, count - 1));
        removeButton.onClick.AddListener(() => cart.Remove(item));
    }
}