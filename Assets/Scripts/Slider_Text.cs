using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬라이더 값을 "50%"처럼 글자로 보여줌 (값이 바뀔 때만 갱신)
public class Slider_Text : MonoBehaviour
{
    public Slider slider;
    public TextMeshProUGUI text;

    private void OnEnable()
    {
        slider.onValueChanged.AddListener(UpdateText);
        UpdateText(slider.value);
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(UpdateText);
    }

    private void UpdateText(float value)
    {
        text.text = $"{(int)value}%";
    }
}
