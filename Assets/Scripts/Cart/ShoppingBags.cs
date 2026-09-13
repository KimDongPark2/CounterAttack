using ConvenienceStore.Ordering;
using System.Collections.Generic;
using UnityEngine;

public class ShoppingBags : MonoBehaviour
{
    [System.Serializable]
    public class CartItem
    {
        public Item_info item;
        public int count;
    }

    public List<CartItem> items = new List<CartItem>();
    public event System.Action OnCartChanged;

    public int TotalPrice
    {
        get
        {
            int sum = 0;
            foreach (var e in items) sum += e.item.Price * e.count;
            return sum;
        }
    }

    public int TotalCount
    {
        get
        {
            int sum = 0;
            foreach (var e in items) sum += e.count;
            return sum;
        }
    }

    CartItem Find(Item_info item)
    {
        foreach (var e in items) if (e.item == item) return e;
        return null;
    }

    public void Add(Item_info item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        var e = Find(item);
        if (e != null) e.count += amount;
        else items.Add(new CartItem { item = item, count = amount });

        Debug.Log($"[장바구니] {item.Name} +{amount} (총 {TotalCount}개 / {TotalPrice}$)");
        OnCartChanged?.Invoke();
    }
    public void SetCount(Item_info item, int count)
    {
        var e = Find(item);
        if (e == null) return;

        if (count <= 0) items.Remove(e);
        else e.count = count;

        OnCartChanged?.Invoke();
    }

    public void Remove(Item_info item) => SetCount(item, 0);

    public void Clear()
    {
        items.Clear();
        OnCartChanged?.Invoke();
    }
}
