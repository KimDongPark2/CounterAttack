using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    [Header("Slot State")]
    public bool isOccupied = false;
    public string currentItemName = "";
    public int itemPrice = 0;
    public GameObject itemPrefab;

    [Header("components Connection")]
    public Image slotBackground;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI numberText;
    public Image itemImage;

    private Color defaultColor;
    private Color selectedColor = new Color(1f, 0.839f, 0f, 0.28f); // #FFD600
    
    private Sprite defaultSprite;
    private bool defaultImageEnabled;

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();
        if (slotBackground != null)
            defaultColor = slotBackground.color;

        if (itemImage != null)
        {
            defaultSprite = itemImage.sprite;
            defaultImageEnabled = itemImage.enabled;
        }
    }

    public void Setup(string itemName, int itemNum, Sprite icon, int price, GameObject prefab)
    {
        isOccupied = true;
        currentItemName = itemName;
        itemPrice = price;
        itemPrefab = prefab;
        nameText.text = itemName;
        numberText.text = (itemNum+1).ToString();
        itemImage.sprite = icon;
        itemImage.enabled = true;
    }
    public void Clear()
    {
        isOccupied = false;
        currentItemName = "";
        itemPrice = 0;
        itemPrefab = null;
        if (nameText != null) nameText.text = "";
        
        // 단축키 번호(numberText)는 지우지 않습니다.
        
        if (itemImage != null)
        {
            itemImage.sprite = defaultSprite;
            itemImage.enabled = defaultImageEnabled;
        }
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
