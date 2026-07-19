using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Settings")]
    public GameObject itemSlotPrefab;
    public Transform inventoryContent;
    private void Awake()
    {
        Instance = this;
    }

    public void AddItemUI(string itemName, int number, Sprite Image)
    {
        GameObject newSlot = Instantiate(itemSlotPrefab);
        newSlot.transform.SetParent(inventoryContent, false);
        ItemSlot slotScript = newSlot.GetComponent<ItemSlot>();

        if (slotScript != null)
        {
            slotScript.Setup(itemName, number, Image);
        }
    }
}
