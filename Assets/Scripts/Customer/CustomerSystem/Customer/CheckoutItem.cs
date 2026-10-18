using UnityEngine;

// 계산대 위에 올라간 물건 표시 (CheckoutCounter가 자동으로 붙임. 직접 붙일 필요 없음)
// 물리(Rigidbody)는 그대로 살아 있음
// 올라가 있는 동안은 F키로 못 집게 태그를 잠깐 바꿔두고, 계산이 취소되면 원래대로 돌려놓는다
public class CheckoutItem : MonoBehaviour
{
    public int price;
    public string itemName;

    private Transform[] parts;
    private string[] savedTags;

    public void Prepare(int itemPrice, string name)
    {
        price = itemPrice;
        itemName = name;

        parts = GetComponentsInChildren<Transform>(true);
        savedTags = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            savedTags[i] = parts[i].tag;
            parts[i].tag = "Untagged";
        }

    }

    // 손님이 계산 도중에 가버리면: 다시 F키로 집을 수 있는 물건으로
    public void RestoreAsPickup()
    {
        if (parts != null)
        {
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) parts[i].tag = savedTags[i];
        }
        tag = "Item";
        Destroy(this);
    }
}