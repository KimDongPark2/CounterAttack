using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면 볼륨 슬라이더
// 소리 Slider에 붙이고 Bus만 고르면 끝 (전체 · 브금 · 효과음)
// Percent Text에 글자를 넣으면 "80%"처럼 보여줌 (슬라이더 범위가 0~1이든 0~100이든 상관없음)
// 화면이 열릴 때 저장된 값으로 맞춰지고, 움직이면 믹서 볼륨이 바로 바뀌고 저장됨
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public SoundBus bus = SoundBus.Master;
    [Tooltip("퍼센트를 보여줄 글자 (선택)")]
    public TMP_Text percentText;
    [Tooltip("{0} = 0 ~ 100")]
    public string percentFormat = "{0}%";
    [Tooltip("움직일 때 미리 들려줄 효과음 (효과음 슬라이더에 좋음 · None이면 안 냄)")]
    public Sfx previewSound = Sfx.None;

    private Slider slider;
    private bool syncing;
    private float lastPreview;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        syncing = true;   // 저장된 값으로 맞추는 동안엔 소리 · 저장 안 함
        float v = SoundManager.GetVolume(bus);
        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, v);
        ShowPercent(v);
        syncing = false;
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();   // 설정 화면을 닫을 때 한 번 저장
    }

    private void OnChanged(float value)
    {
        float v = Mathf.InverseLerp(slider.minValue, slider.maxValue, value);   // 슬라이더 범위 → 0 ~ 1
        ShowPercent(v);
        if (syncing) return;
        SoundManager.SetVolume(bus, v);

        if (previewSound != Sfx.None && Time.unscaledTime - lastPreview > 0.15f)
        {
            lastPreview = Time.unscaledTime;
            SoundManager.Play(previewSound);
        }
    }

    private void ShowPercent(float v)
    {
        if (percentText != null) percentText.text = string.Format(percentFormat, Mathf.RoundToInt(v * 100f));
    }
}