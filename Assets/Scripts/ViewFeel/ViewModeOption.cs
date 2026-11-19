using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면의 "시점 모드" 줄
// 아래 칸 중에 쓰는 것만 연결하면 됨 (나머지는 비워 두기)
//   방법 1) 버튼 두 개 : 고정 / 다이나믹. 켜진 쪽만 강조 색 (+ 표시 오브젝트)
//   방법 2) 토글 하나  : 켜면 다이나믹, 끄면 고정
//   세기 슬라이더      : 0 ~ 100% (범위가 0~1이든 0~100이든 상관없음). 고정 시점일 땐 흐리게
// 화면이 열릴 때 저장된 값으로 맞춰지고, 바꾸면 바로 적용 · 저장됨 (게임 중에도 바로)
// Intro 씬 설정 화면에 붙여도 됨 (저장만 해 두면 Game 씬에서 읽음)
public class ViewModeOption : MonoBehaviour
{
    [Header("방법 1) 버튼 두 개")]
    public Button classicButton;
    public Button dynamicButton;
    [Tooltip("켜진 버튼 색")]
    public Color onColor = new Color(1f, 0.85f, 0.25f);
    [Tooltip("꺼진 버튼 색")]
    public Color offColor = Color.white;
    [Tooltip("고정 시점이 켜졌을 때만 보일 표시 (선택 · 예: 체크 표시, 밑줄)")]
    public GameObject classicMark;
    [Tooltip("다이나믹 시점이 켜졌을 때만 보일 표시 (선택)")]
    public GameObject dynamicMark;

    [Header("방법 2) 토글 하나 (켜면 다이나믹)")]
    public Toggle dynamicToggle;

    [Header("효과 세기 (선택)")]
    public Slider intensitySlider;
    [Tooltip("퍼센트를 보여줄 글자 (선택)")]
    public TMP_Text intensityText;
    [Tooltip("{0} = 0 ~ 100")]
    public string intensityFormat = "{0}%";
    [Tooltip("고정 시점일 때 세기 슬라이더를 못 움직이게 (흐리게)")]
    public bool lockSliderInClassic = true;

    [Header("소리 (선택)")]
    public Sfx clickSound = Sfx.ButtonClick;

    private bool syncing;

    private void Awake()
    {
        if (classicButton != null) classicButton.onClick.AddListener(() => Choose(ViewMode.Classic));
        if (dynamicButton != null) dynamicButton.onClick.AddListener(() => Choose(ViewMode.Dynamic));
        if (dynamicToggle != null) dynamicToggle.onValueChanged.AddListener(OnToggle);
        if (intensitySlider != null) intensitySlider.onValueChanged.AddListener(OnSlider);
    }

    private void OnEnable()
    {
        ViewModeSettings.Changed += Refresh;
        Refresh();   // 저장된 값으로 맞추기
    }

    private void OnDisable()
    {
        ViewModeSettings.Changed -= Refresh;
        ViewModeSettings.Save();   // 설정 화면을 닫을 때 한 번 저장
    }

    private void Choose(ViewMode mode)
    {
        if (ViewModeSettings.Mode != mode) SoundManager.Play(clickSound);
        ViewModeSettings.Mode = mode;
        Refresh();
    }

    private void OnToggle(bool on)
    {
        if (syncing) return;
        Choose(on ? ViewMode.Dynamic : ViewMode.Classic);
    }

    private void OnSlider(float value)
    {
        float v = Mathf.InverseLerp(intensitySlider.minValue, intensitySlider.maxValue, value);   // 슬라이더 범위 → 0 ~ 1
        v = Mathf.Round(v * 100f) / 100f;   // 1% 단위
        ShowPercent(v);
        if (syncing) return;
        ViewModeSettings.Intensity = v;
    }

    // 화면을 저장된 값에 맞추기 (이때는 저장 · 소리 안 함)
    private void Refresh()
    {
        syncing = true;
        bool dynamic = ViewModeSettings.IsDynamic;

        Highlight(classicButton, !dynamic);
        Highlight(dynamicButton, dynamic);
        if (classicMark != null) classicMark.SetActive(!dynamic);
        if (dynamicMark != null) dynamicMark.SetActive(dynamic);
        if (dynamicToggle != null) dynamicToggle.isOn = dynamic;

        float v = ViewModeSettings.Intensity;
        if (intensitySlider != null)
        {
            intensitySlider.value = Mathf.Lerp(intensitySlider.minValue, intensitySlider.maxValue, v);
            if (lockSliderInClassic) intensitySlider.interactable = dynamic;
        }
        ShowPercent(v);
        syncing = false;
    }

    private void Highlight(Button b, bool on)
    {
        if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? onColor : offColor;
    }

    private void ShowPercent(float v)
    {
        if (intensityText != null) intensityText.text = string.Format(intensityFormat, Mathf.RoundToInt(v * 100f));
    }
}
