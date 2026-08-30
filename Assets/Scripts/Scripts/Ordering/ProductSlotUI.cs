using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ConvenienceStore.Ordering
{
    /// <summary>
    /// 주문 목록의 한 줄. 프리팹으로 만들어 두고 OrderScreenUI 가 복제해서 쓴다.
    ///
    /// 프리팹 구조 예시 (Horizontal Layout Group)
    ///   ProductSlot
    ///     ├ Icon        (Image)
    ///     ├ Name        (TextMeshProUGUI)
    ///     ├ Price       (TextMeshProUGUI)
    ///     ├ MinusButton (Button)
    ///     ├ Qty         (TextMeshProUGUI)
    ///     └ PlusButton  (Button)
    /// </summary>
    public class ProductSlotUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button minusButton;

        private ProductData product;

        public ProductData Product => product;

        public void Bind(ProductData data)
        {
            product = data;
            if (product == null) return;

            if (icon != null)
            {
                icon.sprite = product.icon;
                icon.enabled = product.icon != null;
            }
            if (nameText != null) nameText.text = product.displayName;
            if (priceText != null)
                priceText.text = $"{product.wholesalePrice:N0}원 · {product.unitsPerBox}개입";

            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() =>
                {
                    OrderManager.Instance?.AddQuantity(product, +1);
                });
            }
            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(() =>
                {
                    OrderManager.Instance?.AddQuantity(product, -1);
                });
            }

            Refresh();
        }

        /// <summary>장바구니가 바뀌었을 때 OrderScreenUI 가 호출한다.</summary>
        public void Refresh()
        {
            if (product == null || quantityText == null) return;
            int qty = OrderManager.Instance != null ? OrderManager.Instance.GetQuantity(product) : 0;
            quantityText.text = qty.ToString();
        }
    }
}
