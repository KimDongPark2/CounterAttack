using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Slider_Text : MonoBehaviour
{
    public Slider slider;
    public TextMeshProUGUI text;

    private void Update()
    {
        text.text = $"{((int)slider.value).ToString()}%";
    }
}
