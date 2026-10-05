using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 주문 화면 오른쪽 장바구니 패널 (A안)
// 담긴 목록, 개수 배지, 합계, 결제하기 버튼, 안내 메시지
public class CartPanelUI : MonoBehaviour
{
    [Header("Component")]
    public ShoppingBags cart;
    public Player_State state;
    [FormerlySerializedAs("spawner")] public DeliverySystem delivery;

    [Header("UI")]
    public Transform contentRoot;
    public CartSlotUI slotPrefab;
    public TextMeshProUGUI countText;    // 빨간 배지 숫자 (없으면 비워도 됨)
    public TextMeshProUGUI totalText;
    public TextMeshProUGUI messageText;  // "주문 완료!" 같은 안내 (없으면 비워도 됨)
    public Button orderButton;
    public float messageDuration = 2.5f;

    private Coroutine messageRoutine;

    private void Awake()
    {
        if (orderButton) orderButton.onClick.AddListener(OnClickOrder);
        if (messageText) messageText.text = "";
    }

    private void OnEnable()
    {
        cart.OnCartChanged += Refresh;
        state.OnMoneyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        cart.OnCartChanged -= Refresh;
        state.OnMoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        foreach (var e in cart.items)
            Instantiate(slotPrefab, contentRoot).Bind(e.item, e.count, cart);

        int total = cart.TotalPrice;
        if (countText) countText.text = cart.TotalCount.ToString();
        if (totalText) totalText.text = Player_State.Won(total);
        if (orderButton) orderButton.interactable = total > 0;
    }

    public void OnClickOrder()
    {
        int total = cart.TotalPrice;
        if (total <= 0) return;

        if (!state.TrySpend(total))
        {
            ShowMessage($"돈이 부족해요 (필요: {Player_State.Won(total)})");
            return;
        }

        // 장바구니를 비우기 전에 주문 내용을 복사해서 배달로 넘긴다
        var order = new List<ShoppingBags.CartItem>();
        foreach (var e in cart.items)
            order.Add(new ShoppingBags.CartItem { item = e.item, count = e.count });

        delivery.Deliver(order);
        cart.Clear();
        ShowMessage("주문 완료! 곧 배달이 도착해요");
    }

    private void ShowMessage(string message)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(message));
    }

    private IEnumerator MessageRoutine(string message)
    {
        messageText.text = message;
        yield return new WaitForSecondsRealtime(messageDuration);
        messageText.text = "";
        messageRoutine = null;
    }
}
