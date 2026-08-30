using System;
using UnityEngine;

namespace ConvenienceStore.Ordering
{
    /// <summary>플레이어가 가진 돈. 주문 결제와 매출 정산이 여기를 거친다.</summary>
    public class PlayerWallet : MonoBehaviour
    {
        [SerializeField] private int startingMoney = 200000;

        public int Money { get; private set; }

        /// <summary>잔액이 바뀔 때마다 새 잔액을 전달한다.</summary>
        public event Action<int> OnMoneyChanged;

        private void Awake() => Money = startingMoney;

        public bool CanAfford(int amount) => Money >= amount;

        /// <summary>성공하면 차감하고 true. 잔액이 모자라면 아무것도 하지 않고 false.</summary>
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Money < amount) return false;
            Money -= amount;
            OnMoneyChanged?.Invoke(Money);
            return true;
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
            OnMoneyChanged?.Invoke(Money);
        }
    }
}
