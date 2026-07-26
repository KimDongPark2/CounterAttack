using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    [Header("Slot State")]
    public bool isOccupied = false;
    public string currentItemName = "";

    [Header("components Connection")]
    public Image slotBackground;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI numberText;
    public Image itemImage;

    private Color defaultColor;
    private Color selectedColor = new Color(1f, 0.839f, 0f, 0.28f); // #FFD600

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();
        if (slotBackground != null)
            defaultColor = slotBackground.color;
    }

    public void Setup(string itemName,int itemNum, Sprite icon)
    {
        isOccupied = true;
        currentItemName = itemName;
        nameText.text = itemName;
        numberText.text = (itemNum+1).ToString();
        itemImage.sprite = icon;
        itemImage.enabled = true;
    }
    public void Clear()
    {
        isOccupied = false;
        currentItemName = "";
        nameText.text = "";
        numberText.text = "";
        itemImage.sprite = null;
        itemImage.enabled = false;
    }
    public void Select()
    {
        if (slotBackground != null)
            slotBackground.color = selectedColor;
    }
    public void Deselect()
    {
        if (slotBackground != null)
            slotBackground.color = defaultColor;
    }
}
