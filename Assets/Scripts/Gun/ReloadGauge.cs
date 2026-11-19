using UnityEngine;
using UnityEngine.UI;

// 재장전 원형 게이지 (화면 가운데 조준점 둘레에 작은 링)
//   남색 테두리 안에서 노란색이 시계 방향으로 차오름. 재장전 중에만 보임
// Canvas 안의 아무 오브젝트에 붙이면 그 Canvas 가운데에 게이지를 만들어 줌 (AmmoHUD가 있으면 자동으로 붙음)
// 그림 파일 없이 코드로 링 모양을 만들어서 따로 준비할 것은 없음
public class ReloadGauge : MonoBehaviour
{
    [Tooltip("게이지 지름 (픽셀)")]
    public float size = 46f;
    [Tooltip("링 두께 (픽셀, 테두리 포함)")]
    public float thickness = 7f;
    [Tooltip("남색 테두리 두께 (픽셀)")]
    public float border = 1.5f;
    [Tooltip("화면 가운데에서 옮길 거리 (픽셀). (0,0)이면 조준점 둘레")]
    public Vector2 offset = Vector2.zero;

    [Header("색")]
    public Color outlineColor = new Color32(0x0A, 0x24, 0x56, 0xFF);   // 남색 #0A2456
    public Color fillColor = new Color32(0xFF, 0xC2, 0x1F, 0xFF);      // 노랑 #FFC21F
    [Tooltip("아직 안 찬 부분")]
    public Color emptyColor = new Color32(0x0A, 0x24, 0x56, 0x73);

    [Header("연결 (비우면 자동으로 찾음)")]
    public GunReload reload;

    private RectTransform root;
    private Image fill;

    private void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[ReloadGauge] Canvas 안에 붙여야 해요.", this);
            enabled = false;
            return;
        }
        canvas = canvas.rootCanvas;

        root = new GameObject("Reload Gauge", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = offset;
        root.sizeDelta = new Vector2(size, size);
        root.SetAsLastSibling();   // 다른 UI 위에 보이게

        // 남색 링(테두리) 위에, 테두리만큼 안쪽으로 바탕 링 · 노란 링을 겹침
        float innerSize = size - border * 2f;
        float innerThick = Mathf.Max(1f, thickness - border * 2f);
        MakeRing("Outline", size, thickness, outlineColor, false);
        MakeRing("Empty", innerSize, innerThick, emptyColor, false);
        fill = MakeRing("Fill", innerSize, innerThick, fillColor, true);

        root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void Update()
    {
        if (root == null) return;
        if (reload == null) reload = FindAnyObjectByType<GunReload>();

        bool show = reload != null && reload.IsReloading;
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (show) fill.fillAmount = reload.ReloadProgress;
    }

    private Image MakeRing(string name, float diameter, float ringThickness, Color color, bool filled)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(diameter, diameter);

        Image img = rt.GetComponent<Image>();
        img.sprite = RingSprite(1f - 2f * ringThickness / Mathf.Max(diameter, 1f));
        img.color = color;
        img.raycastTarget = false;   // 클릭을 막지 않게
        if (filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;   // 12시부터
            img.fillClockwise = true;
            img.fillAmount = 0f;
        }
        return img;
    }

    // 링 모양 그림을 코드로 만들기 (innerRatio = 안쪽 구멍 크기 0 ~ 1). 가장자리는 부드럽게
    private static Sprite RingSprite(float innerRatio)
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = n * 0.5f;
        float aa = 1.5f / half;   // 1.5픽셀만큼 부드럽게
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - d) / aa) * Mathf.Clamp01((d - innerRatio) / aa);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
