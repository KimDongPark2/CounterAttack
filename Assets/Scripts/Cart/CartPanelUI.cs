using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CartPanelUI : MonoBehaviour
{
    [Header("Component")]
    public ShoppingBags cart;
    public Player_State state;

    [Header("UI")]
    public Transform contentRoot;       // ShoppingBag List/Viewport/Content
    public CartSlotUI slotPrefab;
    public TextMeshProUGUI totalText;
    //public TextMeshProUGUI balanceText;
    public Button orderButton;
    //public GameObject emptyText;        // "장바구니가 비었습니다" (선택)

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
        //if (balanceText) balanceText.text = $"잔액 : {state.money}$";
        //if (emptyText) emptyText.SetActive(cart.items.Count == 0);
        if (orderButton) orderButton.interactable = total > 0 && state.money >= total;
    }

    public void OnClickOrder()
    {
        int total = cart.TotalPrice;
        if (total <= 0 || state.money < total) return;

        state.money -= total;

        foreach (var e in cart.items)
            Debug.Log($"[주문] {e.item.Name} x{e.count}");

        cart.Clear();
    }
}