using System;
using UnityEngine;

// 오늘 하루 손님 기록 (만족도, 손님 수, 매출 등)
// 태블릿 정보 화면에서 StoreStats.Satisfaction 처럼 바로 읽어 쓰면 됨
public static class StoreStats
{
    public static int Satisfaction;    // 만족도 점수 합계
    public static int Visitors;        // 들어온 손님
    public static int Served;          // 계산 끝낸 손님
    public static int FastServed;      // 그중 빨리 계산한 손님
    public static int Angry;           // 화나서 나간 손님
    public static int MissingItems;    // 선반에 없어서 못 산 물건 수
    public static int WrongShots;      // 일반 손님을 쏜 횟수
    public static int ThievesCaught;   // 잡은 도둑
    public static int ThievesEscaped;  // 놓친 도둑
    public static int Revenue;         // 오늘 매출 (원)

    // 숫자가 바뀔 때마다 알려줌 (UI 갱신용)
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        OnChanged = null;
        Reset();
    }

    public static void Reset()
    {
        Satisfaction = Visitors = Served = FastServed = Angry = MissingItems = 0;
        WrongShots = ThievesCaught = ThievesEscaped = Revenue = 0;
        OnChanged?.Invoke();
    }

    // 손님 1명당 평균 점수로 별 1~5개
    public static int Stars
    {
        get
        {
            if (Visitors == 0) return 3;
            float avg = (float)Satisfaction / Visitors;
            if (avg >= 1.5f) return 5;
            if (avg >= 0.8f) return 4;
            if (avg >= 0f) return 3;
            if (avg >= -1f) return 2;
            return 1;
        }
    }

    public static void AddVisitor() { Visitors++; Changed(); }
    public static void AddServed(int revenue, int point, bool fast) { Served++; if (fast) FastServed++; Revenue += revenue; Satisfaction += point; Changed(); }
    public static void AddAngry(int point) { Angry++; Satisfaction += point; Changed(); }
    public static void AddMissing(int point) { MissingItems++; Satisfaction += point; Changed(); }
    public static void AddWrongShot(int point) { WrongShots++; Satisfaction += point; Changed(); }
    public static void AddThiefCaught(int point) { ThievesCaught++; Satisfaction += point; Changed(); }
    public static void AddThiefEscaped(int point) { ThievesEscaped++; Satisfaction += point; Changed(); }

    private static void Changed() => OnChanged?.Invoke();
}
