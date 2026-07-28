using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Settings")]
    public Player_State state;
    public ItemSlot[] itemSlots;
    private void Awake()
    {
        Instance = this;
    }
    public void SelectItem(int num)
    {
        state.handIndex = num;
        Debug.Log($"{num} Pressed");
        // 모든 슬롯 선택 해제 후 해당 슬롯만 선택
        for (int i = 0; i < itemSlots.Length; i++)
        {
            itemSlots[i].Deselect();
        }
        int index = num - 1; // 1~5 키 → 0~4 인덱스
        if (index >= 0 && index < itemSlots.Length)
        {
            itemSlots[index].Select();
        }
    }
    public void AddItemUI(string itemName, int num, Sprite Image)
    {
        if (num >= 0 && num < itemSlots.Length)
        {
            itemSlots[num].Setup(itemName, num, Image);
        }
    }
    public void ClearSlotUI(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < itemSlots.Length)
        {
            itemSlots[slotIndex].Clear();
        }
    }
}
