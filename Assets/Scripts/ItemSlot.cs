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

    [Header("Selected")]
    [Tooltip("손에 든 칸일 때 바꿔 끼울 배경 이미지")]
    public Sprite selectedSprite;

    private Sprite defaultBackground;   // 원래 배경 이미지 (선택 해제하면 이걸로 돌아감)

    private Sprite defaultSprite;
    private bool defaultImageEnabled;

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();
        if (slotBackground != null)
            defaultBackground = slotBackground.sprite;

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
        numberText.text = (itemNum + 1).ToString();
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
        if (slotBackground != null && selectedSprite != null)
            slotBackground.sprite = selectedSprite;
    }

    public void Deselect()
    {
        if (slotBackground != null)
            slotBackground.sprite = defaultBackground;
    }
}