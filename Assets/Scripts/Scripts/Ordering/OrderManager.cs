using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ConvenienceStore.Ordering
{
    /// <summary>
    /// 장바구니 → 결제 → 일정 시간 뒤 배송까지 담당한다.
    /// 씬에 빈 오브젝트("GameSystems") 하나 만들어서 붙이면 된다.
    /// UI 쪽에서는 OrderManager.Instance 로 접근한다.
    /// </summary>
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerWallet wallet;

        [Tooltip("배송된 상자가 생성될 위치 (가게 뒷문 앞 등)")]
        [SerializeField] private Transform deliverySpawnPoint;

        [Header("배송")]
        [Tooltip("주문 후 상자가 도착하기까지 걸리는 시간(초). 게임 시간 기준으로 바꾸고 싶으면 여기 대신 자체 시간 시스템을 쓰면 된다.")]
        [SerializeField, Min(0f)] private float deliveryDelay = 30f;

        [SerializeField] private float boxSpacing = 0.4f;
        [SerializeField, Min(1)] private int boxesPerRow = 4;

        private readonly Dictionary<ProductData, int> cart = new Dictionary<ProductData, int>();

        /// <summary>장바구니 내용이 바뀔 때. UI 갱신용.</summary>
        public event Action OnCartChanged;

        /// <summary>사용자에게 보여줄 짧은 메시지(주문 완료, 잔액 부족, 배송 도착 등).</summary>
        public event Action<string> OnMessage;

        /// <summary>배송이 실제로 도착했을 때. 재고 시스템을 붙일 지점.</summary>
        public event Action<IReadOnlyList<OrderLine>> OnDelivered;

        public IReadOnlyDictionary<ProductData, int> Cart => cart;
        public PlayerWallet Wallet => wallet;

        [Serializable]
        public struct OrderLine
        {
            public ProductData product;
            public int boxCount;
            public OrderLine(ProductData p, int c) { product = p; boxCount = c; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- 장바구니 ----------

        public int GetQuantity(ProductData product)
        {
            if (product == null) return 0;
            return cart.TryGetValue(product, out int q) ? q : 0;
        }

        public void SetQuantity(ProductData product, int quantity)
        {
            if (product == null) return;
            quantity = Mathf.Clamp(quantity, 0, 99);

            if (quantity == 0) cart.Remove(product);
            else cart[product] = quantity;

            OnCartChanged?.Invoke();
        }

        public void AddQuantity(ProductData product, int delta = 1)
            => SetQuantity(product, GetQuantity(product) + delta);

        public void ClearCart()
        {
            if (cart.Count == 0) return;
            cart.Clear();
            OnCartChanged?.Invoke();
        }

        public int TotalPrice
        {
            get
            {
                int sum = 0;
                foreach (var kv in cart) sum += kv.Key.wholesalePrice * kv.Value;
                return sum;
            }
        }

        public int TotalBoxes
        {
            get
            {
                int sum = 0;
                foreach (var kv in cart) sum += kv.Value;
                return sum;
            }
        }

        // ---------- 주문 ----------

        /// <summary>주문 확정. 성공하면 true.</summary>
        public bool PlaceOrder()
        {
            if (cart.Count == 0)
            {
                OnMessage?.Invoke("장바구니가 비어 있습니다.");
                return false;
            }

            int total = TotalPrice;

            if (wallet == null)
            {
                Debug.LogError("[OrderManager] PlayerWallet 이 연결되지 않았습니다.", this);
                return false;
            }

            if (!wallet.TrySpend(total))
            {
                OnMessage?.Invoke($"잔액이 부족합니다. (필요: {total:N0}원)");
                return false;
            }

            // 배송 목록을 스냅샷으로 떠 두고 장바구니를 비운다.
            var lines = new List<OrderLine>(cart.Count);
            foreach (var kv in cart) lines.Add(new OrderLine(kv.Key, kv.Value));
            cart.Clear();
            OnCartChanged?.Invoke();

            OnMessage?.Invoke($"주문 완료! {total:N0}원 결제 · 약 {deliveryDelay:0}초 뒤 도착합니다.");
            StartCoroutine(DeliverAfterDelay(lines));
            return true;
        }

        private IEnumerator DeliverAfterDelay(List<OrderLine> lines)
        {
            yield return new WaitForSeconds(deliveryDelay);

            if (deliverySpawnPoint != null)
            {
                int index = 0;
                foreach (var line in lines)
                {
                    if (line.product == null || line.product.boxPrefab == null) continue;

                    for (int i = 0; i < line.boxCount; i++, index++)
                    {
                        int row = index / boxesPerRow;
                        int col = index % boxesPerRow;
                        Vector3 offset = deliverySpawnPoint.right * (col * boxSpacing)
                                       + deliverySpawnPoint.forward * (row * boxSpacing);

                        Instantiate(line.product.boxPrefab,
                                    deliverySpawnPoint.position + offset,
                                    deliverySpawnPoint.rotation);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[OrderManager] deliverySpawnPoint 가 없어 상자를 생성하지 못했습니다.", this);
            }

            OnDelivered?.Invoke(lines);
            OnMessage?.Invoke("배송이 도착했습니다!");
        }
    }
}
