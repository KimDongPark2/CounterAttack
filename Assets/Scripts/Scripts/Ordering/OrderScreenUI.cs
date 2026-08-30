using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ConvenienceStore.Ordering
{
    /// <summary>
    /// 태블릿의 "주문" 앱 화면. ScreenCanvas 아래 OrderPanel 에 붙인다.
    ///
    /// 구조 예시
    ///   OrderPanel
    ///     ├ ScrollView
    ///     │   └ Viewport
    ///     │       └ Content        <- slotParent (Vertical Layout Group + Content Size Fitter)
    ///     ├ TotalText              (TextMeshProUGUI)
    ///     ├ MoneyText              (TextMeshProUGUI)
    ///     ├ MessageText            (TextMeshProUGUI)
    ///     ├ ConfirmButton          (Button)
    ///     └ ClearButton            (Button)
    /// </summary>
    public class OrderScreenUI : MonoBehaviour
    {
        [Header("카탈로그")]
        [Tooltip("이 화면에서 주문할 수 있는 상품들")]
        [SerializeField] private List<ProductData> catalog = new List<ProductData>();

        [Header("References")]
        [SerializeField] private ProductSlotUI slotPrefab;
        [SerializeField] private Transform slotParent;
        [SerializeField] private TextMeshProUGUI totalText;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button clearButton;

        [Header("옵션")]
        [SerializeField] private float messageDuration = 3f;

        private readonly List<ProductSlotUI> slots = new List<ProductSlotUI>();
        private Coroutine messageRoutine;

        private void Start()
        {
            BuildList();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(() => OrderManager.Instance?.PlaceOrder());

            if (clearButton != null)
                clearButton.onClick.AddListener(() => OrderManager.Instance?.ClearCart());

            RefreshAll();
        }

        private void OnEnable()
        {
            var om = OrderManager.Instance;
            if (om != null)
            {
                om.OnCartChanged += RefreshAll;
                om.OnMessage += ShowMessage;
                if (om.Wallet != null) om.Wallet.OnMoneyChanged += HandleMoneyChanged;
            }
            RefreshAll();
        }

        private void OnDisable()
        {
            var om = OrderManager.Instance;
            if (om != null)
            {
                om.OnCartChanged -= RefreshAll;
                om.OnMessage -= ShowMessage;
                if (om.Wallet != null) om.Wallet.OnMoneyChanged -= HandleMoneyChanged;
            }
        }

        private void BuildList()
        {
            if (slotPrefab == null || slotParent == null)
            {
                Debug.LogError("[OrderScreenUI] slotPrefab 또는 slotParent 가 비어 있습니다.", this);
                return;
            }

            foreach (var product in catalog)
            {
                if (product == null) continue;
                var slot = Instantiate(slotPrefab, slotParent);
                slot.Bind(product);
                slots.Add(slot);
            }
        }

        private void RefreshAll()
        {
            foreach (var slot in slots) slot.Refresh();

            var om = OrderManager.Instance;
            if (om == null) return;

            int total = om.TotalPrice;

            if (totalText != null)
                totalText.text = $"합계 {total:N0}원 · {om.TotalBoxes}박스";

            if (om.Wallet != null)
            {
                HandleMoneyChanged(om.Wallet.Money);
                if (confirmButton != null)
                    confirmButton.interactable = om.TotalBoxes > 0 && om.Wallet.CanAfford(total);
            }
        }

        private void HandleMoneyChanged(int money)
        {
            if (moneyText != null) moneyText.text = $"보유 {money:N0}원";
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
            yield return new WaitForSeconds(messageDuration);
            messageText.text = string.Empty;
            messageRoutine = null;
        }
    }
}
