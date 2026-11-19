using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탄약 표시 글자 (예: "7 / 21")
// TextMeshPro 글자에 붙이기만 하면 끝. 권총을 들고 있을 때만 보임
// 오브젝트를 끄지 않고 글자만 숨겨서, 이 오브젝트에 붙여도 계속 동작함
// 재장전 진행은 글자 대신 조준점 둘레의 원형 게이지(ReloadGauge)로 보여줌 (자동으로 붙음)
// 글자는 항상 한 줄 · 고정 크기 (줄바꿈 · 자동 크기를 끔) → 두 줄로 깨져서 핫바를 덮지 않게
[RequireComponent(typeof(TMP_Text))]
public class AmmoHUD : MonoBehaviour
{
    [Tooltip("{0} = 탄창에 남은 총알, {1} = 예비 탄약")]
    public string format = "{0} / {1}";
    [Tooltip("예비 탄약이 무한일 때 모양. {0} = 탄창에 남은 총알, {1} = 아래 글자")]
    public string infiniteFormat = "{0} / {1}";
    [Tooltip("예비 탄약이 무한일 때 {1} 자리에 들어갈 글자 (폰트에 ∞ 글자가 없으면 바꾸기)")]
    public string infiniteReserveText = "-";
    [Tooltip("재장전 중에 숫자 대신 보일 짧은 글자 (비우면 숫자 그대로). 길면 잘릴 수 있음")]
    public string reloadingLabel = "";
    [Tooltip("탄창이 비었을 때 덧붙일 짧은 안내 (비우면 안 붙임)")]
    public string emptyHint = "  R";

    [Header("글자 모양")]
    [Tooltip("글자 크기 고정 (0이면 지금 크기 그대로)")]
    public float fontSize = 0f;

    [Header("색")]
    public Color normalColor = Color.white;
    [Tooltip("총알이 이 개수 이하로 남으면 경고 색")]
    public int lowAmmo = 2;
    public Color lowColor = new Color(1f, 0.35f, 0.3f);

    [Header("같이 숨길 그림 (선택 · 예: 총알 아이콘)")]
    public Graphic[] extraGraphics;

    [Header("재장전 게이지")]
    [Tooltip("조준점 둘레에 원형 게이지를 자동으로 만들기 (씬에 ReloadGauge가 없을 때)")]
    public bool createReloadGauge = true;

    [Header("연결 (비우면 자동으로 찾음)")]
    public GunReload reload;
    public Player_Interaction interaction;

    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        text.textWrappingMode = TextWrappingModes.NoWrap;   // 줄바꿈 끄기 (한 줄)
        text.enableAutoSizing = false;                       // 자동 크기 끄기
        if (fontSize > 0f) text.fontSize = fontSize;
        text.overflowMode = TextOverflowModes.Overflow;

        if (createReloadGauge && FindAnyObjectByType<ReloadGauge>() == null)
            gameObject.AddComponent<ReloadGauge>();
    }

    private void Update()
    {
        // GunReload는 실행할 때 자동으로 붙어서, 처음 몇 프레임은 없을 수 있음
        if (reload == null) reload = FindAnyObjectByType<GunReload>();
        if (interaction == null) interaction = FindAnyObjectByType<Player_Interaction>();

        bool show = reload != null && reload.enabled && interaction != null && interaction.GunInHand;
        text.enabled = show;
        if (extraGraphics != null)
            foreach (Graphic g in extraGraphics) if (g != null) g.enabled = show;
        if (!show) return;

        int mag = reload.AmmoInMag;
        string s;
        if (reload.IsReloading && !string.IsNullOrEmpty(reloadingLabel)) s = reloadingLabel;
        else
        {
            s = reload.infiniteReserve
                ? string.Format(infiniteFormat, mag, infiniteReserveText)
                : string.Format(format, mag, reload.reserveAmmo);
            if (mag <= 0 && !reload.IsReloading && !string.IsNullOrEmpty(emptyHint)) s += emptyHint;
        }

        if (text.text != s) text.text = s;   // 바뀔 때만
        text.color = mag <= lowAmmo && !reload.IsReloading ? lowColor : normalColor;
    }
}
