using UnityEngine;

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
    public int inventoryCnt = 0;
}