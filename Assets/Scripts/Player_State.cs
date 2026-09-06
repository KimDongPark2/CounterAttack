using UnityEngine;

public class Player_State : MonoBehaviour
{
    [Header("Financial Information")]
    public float money = 0;
    public float debt = 0;
    public int totalSales = 0;

    [Header("Entire Play Information")]
    public int star = 3;
    public int days = 0;
    public int countedCustomers = 0;
    public int goodProcess = 0;
    public int badProcess = 0;

    [Header("Player State")]
    public int inventoryRestrict = 5;
    public int handIndex;
    public int inventoryCnt = 0;

    public void Init()
    {
        money = 500f;
        debt = 0;

        days = 1;
        countedCustomers = 0;
        goodProcess = 0;
        badProcess = 0;

        inventoryRestrict = 5;
        inventoryCnt = 0;
    }
}