using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 순위 줄에 마우스를 올리면 그 줄 옆에 뜨는 사원증
// - 왼쪽 줄이면 오른쪽에, 오른쪽 줄이면 왼쪽에 뜸 (꼭지가 줄을 가리킴)
// - 화면 위아래 밖으로 안 나가게 맞춤
// - 사진은 순위표 줄에 이미 불러온 걸 그대로 씀
// RankPanel 맨 아래(맨 위에 그려지게)에 놓고 꺼둔 채로 두기. RankingBoard의 Popup 칸에 넣기
public class RankBadgePopup : MonoBehaviour
{
    [Header("연결")]
    public RawImage photo;
    [Tooltip("순위 동그라미")]
    public Image medal;
    [Tooltip("동그라미 안 숫자")]
    public TMP_Text rankText;
    [Tooltip("직급 (점장 · 매니저 · 알바)")]
    public TMP_Text titleText;
    public TMP_Text nameText;
    [Tooltip("학번")]
    public TMP_Text idText;
    public TMP_Text scoreText;
    public TMP_Text revenueText;
    [Tooltip("별 5개 (왼쪽부터)")]
    public Image[] stars;
    public TMP_Text servedText;
    public TMP_Text dateText;
    [Tooltip("바코드 옆 \"RANK 03\"")]
    public TMP_Text codeText;
    [Tooltip("줄을 가리키는 꼭지 (왼쪽을 보는 그림 · 카드 왼쪽 가운데에)")]
    public RectTransform tip;

    [Header("스프라이트")]
    [Tooltip("1위 · 2위 · 3위 메달")]
    public Sprite[] medalSprites = new Sprite[3];
    [Tooltip("4위부터 동그라미")]
    public Sprite medalOther;
    public Sprite starOn;
    public Sprite starOff;

    [Header("글자")]
    [Tooltip("1위 · 2위 · 3위 직급")]
    public string[] titles = { "점장", "매니저", "매니저" };
    [Tooltip("4위부터 직급")]
    public string defaultTitle = "알바";
    public string idFormat = "학번 {0}";
    public string scoreFormat = "{0:N0}<size=65%>점</size>";
    public string moneyFormat = "{0:N0}원";
    public string countFormat = "{0}명";
    public string codeFormat = "RANK {0:00}";
    public Color medalNumberColor = new Color32(0x0A, 0x24, 0x56, 0xFF);
    [Tooltip("4위부터 숫자 색 (남색 동그라미 위)")]
    public Color otherNumberColor = Color.white;

    [Header("위치 · 움직임")]
    [Tooltip("줄과 사원증 사이 간격")]
    public float gap = 28f;
    [Tooltip("화면 위아래 여백")]
    public float margin = 16f;
    [Tooltip("살짝 기울이기 (도)")]
    public float tilt = 1.5f;
    public float popTime = 0.12f;

    // 지금 사원증을 보여주고 있는 줄
    public RankRowUI Current { get; private set; }

    private RectTransform rect;
    private CanvasGroup group;
    private float popTimer = 99f;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // 사원증이 마우스를 가로채서 깜빡이지 않게
        group.interactable = false;
    }

    private void Update()
    {
        if (popTimer >= popTime) return;
        popTimer += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(popTimer / popTime);
        float s = Mathf.Lerp(0.92f, 1f, 1f - (1f - k) * (1f - k));
        rect.localScale = new Vector3(s, s, 1f);
        group.alpha = k;
    }

    // ---------- 보여주기 ----------
    public void Show(RankRowUI row)
    {
        if (row == null || row.entry == null) return;
        if (rect == null) Awake();
        RankingStore.Entry e = row.entry;
        int rank = row.rank;

        // 사진 (줄에 불러온 걸 그대로)
        if (photo != null && row.photo != null)
        {
            photo.texture = row.photo.texture;
            photo.color = Color.white;
        }

        // 순위 · 직급
        if (medal != null)
        {
            Sprite s = rank >= 1 && rank <= 3 && medalSprites != null && rank - 1 < medalSprites.Length ? medalSprites[rank - 1] : medalOther;
            if (s != null) medal.sprite = s;
        }
        if (rankText != null)
        {
            rankText.text = rank.ToString();
            rankText.color = rank <= 3 ? medalNumberColor : otherNumberColor;
        }
        if (titleText != null) titleText.text = rank >= 1 && rank <= titles.Length ? titles[rank - 1] : defaultTitle;

        // 이름 · 학번 ("20314 김민준" → 학번 20314 / 김민준)
        SplitName(e.name, out string id, out string name);
        if (nameText != null) nameText.text = name;
        if (idText != null)
        {
            idText.gameObject.SetActive(id.Length > 0);
            idText.text = string.Format(idFormat, id);
        }

        // 숫자들
        if (scoreText != null) scoreText.text = string.Format(scoreFormat, e.score);
        if (revenueText != null) revenueText.text = string.Format(moneyFormat, e.revenue);
        if (servedText != null) servedText.text = string.Format(countFormat, e.served);
        if (dateText != null) dateText.text = e.date;
        if (codeText != null) codeText.text = string.Format(codeFormat, rank);
        if (stars != null)
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].sprite = i < e.stars ? starOn : starOff;

        Current = row;
        gameObject.SetActive(true);
        Place((RectTransform)row.transform);

        popTimer = 0f;
        rect.localScale = new Vector3(0.92f, 0.92f, 1f);
        group.alpha = 0f;
    }

    public void Hide(RankRowUI row = null)
    {
        if (row != null && row != Current) return;   // 다른 줄에서 나간 건 무시
        Current = null;
        gameObject.SetActive(false);
    }

    // ---------- 위치 ----------
    private void Place(RectTransform row)
    {
        RectTransform parent = rect.parent as RectTransform;
        if (parent == null) return;

        // 줄의 네 모서리를 사원증 부모 기준 좌표로
        Vector3[] c = new Vector3[4];
        row.GetWorldCorners(c);
        Vector3 bl = parent.InverseTransformPoint(c[0]);
        Vector3 tr = parent.InverseTransformPoint(c[2]);
        float rowCenterX = (bl.x + tr.x) * 0.5f;
        float rowCenterY = (bl.y + tr.y) * 0.5f;

        // 줄이 화면 왼쪽 절반이면 오른쪽에, 아니면 왼쪽에
        bool toRight = rowCenterX < parent.rect.center.x;
        rect.pivot = new Vector2(toRight ? 0f : 1f, 0.5f);
        float x = toRight ? tr.x + gap : bl.x - gap;

        // 위아래로 화면 밖에 안 나가게
        float half = rect.rect.height * 0.5f;
        float y = Mathf.Clamp(rowCenterY, parent.rect.yMin + margin + half, parent.rect.yMax - margin - half);

        rect.localPosition = new Vector3(x, y, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, toRight ? -tilt : tilt);

        // 꼭지: 줄 쪽 가장자리, 줄 높이에 맞춤
        if (tip != null)
        {
            tip.anchorMin = tip.anchorMax = new Vector2(toRight ? 0f : 1f, 0.5f);
            tip.pivot = new Vector2(1f, 0.5f);
            tip.localScale = new Vector3(toRight ? 1f : -1f, 1f, 1f);
            float ty = Mathf.Clamp(rowCenterY - y, -half + 60f, half - 60f);
            tip.anchoredPosition = new Vector2(toRight ? 8f : -8f, ty);
        }
    }

    // 앞의 숫자는 학번, 나머지는 이름
    private static void SplitName(string full, out string id, out string name)
    {
        full = (full ?? "").Trim();
        int i = 0;
        while (i < full.Length && char.IsDigit(full[i])) i++;
        id = full.Substring(0, i);
        name = full.Substring(i).Trim();
        if (name.Length == 0)
        {
            name = full;
            id = "";
        }
    }
}
