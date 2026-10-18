using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 태블릿 "플레이 정보" 화면 (정보 E)
// - 별 5개 + "4/5"
// - 점수 내역 7줄 (빠른 계산, 보통 계산, 화나서 나감, 물건 없음, 일반 손님 쏨, 도둑 잡음, 도둑 놓침)
// - 합계
// 화면이 켜질 때, 그리고 켜져 있는 동안 점수가 바뀔 때마다 다시 채움
// 플레이 정보 패널(카드)에 붙이면 됨
public class PlayInfoUI : MonoBehaviour
{
    public enum Kind { Fast, Normal, Angry, Missing, WrongShot, ThiefCaught, ThiefEscaped }

    [Serializable]
    public class Entry
    {
        public Kind kind;
        public string name;
        public Sprite icon;
        public Entry(Kind k, string n) { kind = k; name = n; }
    }

    [Tooltip("점수 값(+2, -2 …)을 여기서 읽음. 비워도 자동으로 찾음")]
    public CustomerSpawner spawner;

    [Header("평점")]
    [Tooltip("별 이미지 5개 (왼쪽부터)")]
    public Image[] stars;
    [Tooltip("켜진 별 / 꺼진 별 그림. 비우면 색만 바꿈")]
    public Sprite starOn;
    public Sprite starOff;
    public Color starOnColor = Color.white;
    public Color starOffColor = new Color32(0xDC, 0xE8, 0xF8, 0xFF);
    public TMP_Text ratingText;
    [Tooltip("{0} = 별 개수")]
    public string ratingFormat = "{0}<size=50%>/5</size>";

    [Header("점수 내역")]
    [Tooltip("줄 하나 (PlayInfoRow가 붙은 프리팹, 또는 씬에 만들어 둔 줄 하나)")]
    public PlayInfoRow rowPrefab;
    [Tooltip("줄이 들어갈 곳 (Vertical Layout Group 있는 오브젝트)")]
    public Transform rowParent;
    [Tooltip("켜면 처음 채울 때 줄 들어갈 곳에 미리 넣어둔 줄들을 지움")]
    public bool clearExisting = true;
    public Entry[] entries =
    {
        new Entry(Kind.Fast, "빠른 계산"),
        new Entry(Kind.Normal, "보통 계산"),
        new Entry(Kind.Angry, "화나서 나감"),
        new Entry(Kind.Missing, "물건 없음"),
        new Entry(Kind.WrongShot, "일반 손님 쏨"),
        new Entry(Kind.ThiefCaught, "도둑 잡음"),
        new Entry(Kind.ThiefEscaped, "도둑 놓침"),
    };
    [Tooltip("{0} = 한 번에 점수, {1} = 횟수")]
    public string eachFormat = "{0}점 × {1}";
    [Tooltip("0번 일어난 줄은 흐리게")]
    [Range(0f, 1f)] public float zeroAlpha = 0.4f;

    [Header("합계")]
    public TMP_Text totalText;
    [Tooltip("{0} = 합계")]
    public string totalFormat = "{0}점";
    [Tooltip("비워도 됨. \"= 1명당 +0.86점\"")]
    public TMP_Text averageText;
    public string averageFormat = "= 1명당 {0}점";

    [Header("색")]
    public Color plusColor = new Color32(0x1E, 0x7A, 0x47, 0xFF);
    public Color minusColor = new Color32(0xB0, 0x1C, 0x27, 0xFF);
    public Color zeroColor = new Color32(0x8A, 0x97, 0xAB, 0xFF);

    private readonly List<PlayInfoRow> rows = new List<PlayInfoRow>();

    private void OnEnable()
    {
        if (spawner == null) spawner = FindFirstObjectByType<CustomerSpawner>();
        StoreStats.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        StoreStats.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        // 별
        int starCount = StoreStats.Stars;
        if (stars != null)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                bool on = i < starCount;
                if (starOn != null && starOff != null) stars[i].sprite = on ? starOn : starOff;
                else stars[i].color = on ? starOnColor : starOffColor;
            }
        }
        if (ratingText != null) ratingText.text = string.Format(ratingFormat, starCount);

        // 점수 내역
        BuildRows();
        for (int i = 0; i < rows.Count && i < entries.Length; i++)
        {
            Entry e = entries[i];
            int count = Count(e.kind);
            int point = Point(e.kind);
            int value = count * point;
            rows[i].Set(e.icon, e.name, string.Format(eachFormat, Signed(point), count),
                        Signed(value), ColorOf(value), count == 0 ? zeroAlpha : 1f);
        }

        // 합계
        int total = StoreStats.Satisfaction;
        if (totalText != null)
        {
            totalText.text = string.Format(totalFormat, Signed(total));
            totalText.color = ColorOf(total);
        }
        if (averageText != null)
        {
            float avg = StoreStats.Visitors == 0 ? 0f : (float)StoreStats.Satisfaction / StoreStats.Visitors;
            string a = avg > 0f ? "+" + avg.ToString("0.00") : avg.ToString("0.00");
            averageText.text = string.Format(averageFormat, a);
        }
    }

    // 처음 한 번만 줄을 만듦
    private void BuildRows()
    {
        if (rows.Count > 0 || rowPrefab == null || rowParent == null) return;

        bool prefabInScene = rowPrefab.gameObject.scene.IsValid();
        if (clearExisting)
        {
            for (int i = rowParent.childCount - 1; i >= 0; i--)
            {
                Transform child = rowParent.GetChild(i);
                if (child == rowPrefab.transform) continue;
                Destroy(child.gameObject);
            }
        }
        // 씬에 있는 줄을 본으로 썼으면 그 줄은 숨겨둠
        if (prefabInScene) rowPrefab.gameObject.SetActive(false);

        foreach (Entry e in entries)
        {
            PlayInfoRow row = Instantiate(rowPrefab, rowParent);
            row.gameObject.SetActive(true);
            row.name = "Row_" + e.kind;
            rows.Add(row);
        }
    }

    private static int Count(Kind k)
    {
        switch (k)
        {
            case Kind.Fast: return StoreStats.FastServed;
            case Kind.Normal: return StoreStats.Served - StoreStats.FastServed;
            case Kind.Angry: return StoreStats.Angry;
            case Kind.Missing: return StoreStats.MissingItems;
            case Kind.WrongShot: return StoreStats.WrongShots;
            case Kind.ThiefCaught: return StoreStats.ThievesCaught;
            case Kind.ThiefEscaped: return StoreStats.ThievesEscaped;
        }
        return 0;
    }

    // 점수 값은 스포너 Inspector 값을 그대로 씀 (없으면 기본값)
    private int Point(Kind k)
    {
        bool s = spawner != null;
        switch (k)
        {
            case Kind.Fast: return s ? spawner.fastPoint : 2;
            case Kind.Normal: return s ? spawner.slowPoint : 0;
            case Kind.Angry: return s ? spawner.angryPoint : -2;
            case Kind.Missing: return s ? spawner.missingPoint : -1;
            case Kind.WrongShot: return s ? spawner.wrongShotPoint : -5;
            case Kind.ThiefCaught: return s ? spawner.thiefShotPoint : 3;
            case Kind.ThiefEscaped: return s ? spawner.thiefEscapePoint : -3;
        }
        return 0;
    }

    private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

    private Color ColorOf(int v) => v > 0 ? plusColor : (v < 0 ? minusColor : zeroColor);
}