using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면 마우스 감도 슬라이더
// Slider에 붙이기만 하면 끝 (범위가 0~1이든 1~100이든 상관없음 → 감도 1~100으로 바꿔서 씀)
// 화면이 열릴 때 저장된 감도로 맞춰지고, 움직이면 바로 적용되고 저장됨 (게임을 껐다 켜도 남음)
// Game 씬에서는 Player_Look에 바로 적용 · Intro 씬에서는 저장만 해뒀다가 게임 시작할 때 Player_Look이 읽음
[RequireComponent(typeof(Slider))]
public class SensitivitySlider : MonoBehaviour
{
    public const string PrefKey = "MouseSensitivity";
    public const float DefaultValue = 50f;

    [Tooltip("감도를 보여줄 글자 (선택)")]
    public TMP_Text valueText;
    [Tooltip("{0} = 1 ~ 100")]
    public string valueFormat = "{0}";
    [Tooltip("비우면 자동으로 찾음 (Intro 씬엔 없어도 됨)")]
    public Player_Look look;

    private Slider slider;
    private bool syncing;

    // 저장된 감도 (1 ~ 100)
    public static float Saved => Mathf.Clamp(PlayerPrefs.GetFloat(PrefKey, DefaultValue), 1f, 100f);

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        if (look == null) look = FindAnyObjectByType<Player_Look>();
        syncing = true;   // 저장된 값으로 맞추는 동안엔 저장 안 함
        float v = Saved;
        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.InverseLerp(1f, 100f, v));
        Show(v);
        syncing = false;
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();   // 설정 화면을 닫을 때 한 번 저장
    }

    private void OnChanged(float value)
    {
        float t = Mathf.InverseLerp(slider.minValue, slider.maxValue, value);
        float v = Mathf.Round(Mathf.Lerp(1f, 100f, t));   // 슬라이더 범위 → 1 ~ 100
        Show(v);
        if (syncing) return;
        PlayerPrefs.SetFloat(PrefKey, v);
        if (look != null) look.MoveSensitivityChanged(v);
    }

    private void Show(float v)
    {
        if (valueText != null) valueText.text = string.Format(valueFormat, Mathf.RoundToInt(v));
    }
}
