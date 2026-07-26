using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public Item_info itemData;
    public int amount;
}
public class Player_State : MonoBehaviour
{
    [Header("Financial Information")]
    public float money = 0;
    public float debt = 0;

    [Header("Entire Play Information")]
    public int days = 0;
    public int countedCustomers = 0;
    public int goodProcess = 0;
    public int badProcess = 0;

    [Header("Player State")]
    public int inventoryRestrict = 5;
    public int handIndex;
    public int curitemIndex = 0;
    public int inventoryCnt = 0;
    public List<InventoryItem> inventoryList = new List<InventoryItem>();


    public void AddItemToState(Item_info info)
    {
        foreach (InventoryItem item in inventoryList) 
            if(item.itemData.Name == info.Name)
            {
                item.amount++;
                inventoryCnt++;
                return;
            }
        InventoryItem newItem = new InventoryItem();
        newItem.itemData = info;
        newItem.amount = 1;
        inventoryCnt++;
        inventoryList.Add(newItem);
    }
    public int CheckItemAmount(Item_info info)
    {
        foreach (InventoryItem item in inventoryList)
            if (item.itemData.Name == info.Name)
                return item.amount;
        return 0;
    }
}
