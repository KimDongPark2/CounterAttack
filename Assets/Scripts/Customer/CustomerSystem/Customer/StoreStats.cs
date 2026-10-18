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

    // ---------- 점수 체계 ----------
    // 응대 점수 합계 ÷ 손님 수 = 1명당 점수 → 별 1~5개
    // 최종 점수(순위표) = 오늘 매출 × 별 배율 − 남은 빚

    // 별을 나누는 기준 (1명당 점수가 이 값 이상이면 그 별)  ★2, ★3, ★4, ★5
    public static readonly float[] StarCuts = { -1f, 0f, 0.8f, 1.5f };
    // 별 배율  ★1, ★2, ★3, ★4, ★5
    public static readonly float[] StarMultipliers = { 0.5f, 0.8f, 1.0f, 1.3f, 1.6f };

    // 손님 1명당 점수 (손님이 없으면 0)
    public static float AveragePoint => Visitors == 0 ? 0f : (float)Satisfaction / Visitors;

    // 별 1~5개 (손님이 없으면 3개)
    public static int Stars
    {
        get
        {
            if (Visitors == 0) return 3;
            float avg = AveragePoint;
            int stars = 1;
            for (int i = 0; i < StarCuts.Length; i++)
                if (avg >= StarCuts[i]) stars = i + 2;
            return stars;
        }
    }

    // 지금 별의 배율 (★4면 1.3)
    public static float Multiplier => StarMultipliers[Stars - 1];

    // 빚 빼기 전 점수 = 오늘 매출 × 별 배율
    public static int FinalScore => Mathf.RoundToInt(Revenue * Multiplier);

    // 순위표에 올리는 점수 = 매출 × 별 배율 − 남은 빚 (0점 아래로는 안 내려감)
    public static int ScoreAfterDebt(int debt) => Mathf.Max(0, FinalScore - Mathf.Max(0, debt));

    // ---------- 최고 점수 (게임을 껐다 켜도 남음) ----------
    private const string BestKey = "BestScore";
    public static int BestScore => PlayerPrefs.GetInt(BestKey, 0);

    // 최고 점수보다 높으면 저장하고 true
    public static bool SaveBest(int score)
    {
        if (score <= BestScore) return false;
        PlayerPrefs.SetInt(BestKey, score);
        PlayerPrefs.Save();
        return true;
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