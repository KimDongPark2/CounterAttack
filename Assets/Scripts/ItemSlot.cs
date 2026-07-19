using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    [Header("UI components Connection")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI numberText;
    public Image itemImage;

    public void Setup(string itemName, int itemNum, Sprite icon)
    {
        nameText.text = itemName;
        numberText.text = itemNum.ToString();
        itemImage.sprite = icon;
    }
}
