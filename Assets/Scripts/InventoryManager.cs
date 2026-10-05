using UnityEngine;

// 하단 핫바(ItemSlot들) 관리
// 몇 칸이 찼는지는 따로 세지 않고 ItemSlot.isOccupied를 보고 계산한다
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory Settings")]
    public Player_State state;
    public ItemSlot[] itemSlots;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshSlots();
    }

    // 쓸 수 있는 칸만 핫바에 보이게 (가방 칸 업그레이드 후에도 호출됨)
    public void RefreshSlots()
    {
        for (int i = 0; i < itemSlots.Length; i++)
            itemSlots[i].gameObject.SetActive(i < Capacity);
    }

    // 실제로 쓸 수 있는 칸 수 (Player_State의 제한과 슬롯 개수 중 작은 쪽)
    private int Capacity => state != null ? Mathf.Min(state.inventoryRestrict, itemSlots.Length) : itemSlots.Length;

    public bool IsFull => FindEmptySlot() < 0;

    // 지금 손에 든 슬롯 (없으면 null)
    public ItemSlot HandSlot => state != null ? GetSlot(state.handIndex - 1) : null;

    public void SelectItem(int num)
    {
        state.handIndex = num;
        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (i == num - 1) itemSlots[i].Select();
            else itemSlots[i].Deselect();
        }
    }

    public void AddItemUI(string itemName, int slotIndex, Sprite icon, int price, GameObject prefab)
    {
        if (slotIndex >= 0 && slotIndex < itemSlots.Length)
            itemSlots[slotIndex].Setup(itemName, slotIndex, icon, price, prefab);
    }

    public int FindEmptySlot()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (!itemSlots[i].isOccupied) return i;
        }
        return -1;
    }

    public ItemSlot GetSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < itemSlots.Length) return itemSlots[slotIndex];
        return null;
    }

    public void ClearSlotUI(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < itemSlots.Length) itemSlots[slotIndex].Clear();
    }
}