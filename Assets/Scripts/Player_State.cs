using System;
using UnityEngine;

// 플레이어의 돈, 날짜, 평가 같은 상태 모음
// 돈은 여기 하나로만 관리 (원 단위)
public class Player_State : MonoBehaviour
{
    [Header("Financial Information")]
    public int money = 0;              // 원
    public int debt = 0;
    public int totalSales = 0;
    public int startingMoney = 50000;  // Init()에서 넣어줄 시작 금액

    [Header("Entire Play Information")]
    public int star = 3;
    public int days = 0;
    public int countedCustomers = 0;
    public int goodProcess = 0;
    public int badProcess = 0;

    [Header("Player State")]
    public int inventoryRestrict = 5;  // 쓸 수 있는 핫바 칸 수
    public int handIndex;              // 지금 손에 든 슬롯 번호 (1부터, 0이면 없음, 6이면 권총)
    public bool hasGun = false;        // 상점에서 권총을 샀는지 (사면 핫바 6번 칸이 생김)
    public bool inCounter = false;     // 지금 계산대 안에 있는지 (Scanner가 매 프레임 갱신)

    // 돈이 바뀔 때마다 알려줌 (UI 갱신용)
    public event Action OnMoneyChanged;

    public void Init()
    {
        money = startingMoney;
        debt = 0;

        days = 1;
        countedCustomers = 0;
        goodProcess = 0;
        badProcess = 0;

        inventoryRestrict = 5;
        hasGun = false;
        OnMoneyChanged?.Invoke();
    }

    // 돈이 충분하면 빼고 true, 모자라면 아무것도 안 하고 false
    public bool TrySpend(int amount)
    {
        if (amount < 0 || money < amount) return false;
        money -= amount;
        OnMoneyChanged?.Invoke();
        return true;
    }

    public void Earn(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        OnMoneyChanged?.Invoke();
    }

    // 1800 → "1,800원"
    public static string Won(int amount) => $"{amount:N0}원";
}