using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CartPanelUI : MonoBehaviour
{
    [Header("Component")]
    public ShoppingBags cart;
    public Player_State state;
    public DeliverySystem spawner;

    [Header("UI")]
    public Transform contentRoot;     
    public CartSlotUI slotPrefab;
    public TextMeshProUGUI totalText;
    public Button orderButton;

    void Awake()
    {
        if (orderButton) orderButton.onClick.AddListener(OnClickOrder);
    }

    void OnEnable()
    {
        cart.OnCartChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        cart.OnCartChanged -= Refresh;
    }

    void Refresh()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        foreach (var e in cart.items)
        {
            var slot = Instantiate(slotPrefab, contentRoot);
            slot.Bind(e.item, e.count, cart);
        }

        int total = cart.TotalPrice;
        if (totalText) totalText.text = $"{total}$";
        if (orderButton) orderButton.interactable = total > 0 && state.money >= total;
    }

    public void OnClickOrder()
    {
        int total = cart.TotalPrice;
        if (total <= 0 || state.money < total) return;

        state.money -= total;

        var order = new List<ShoppingBags.CartItem>();
        foreach (var e in cart.items)
            order.Add(new ShoppingBags.CartItem { item = e.item, count = e.count });

        spawner.Deliver(order);
        cart.Clear();
    }
}