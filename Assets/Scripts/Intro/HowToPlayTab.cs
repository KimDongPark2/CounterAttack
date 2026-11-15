using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이 방법 창의 목차 한 칸 (Button이 있는 오브젝트에 붙이기)
// HowToPlayPanel이 단계 수만큼 복사해서 번호 · 이름을 채움
[RequireComponent(typeof(Button))]
public class HowToPlayTab : MonoBehaviour
{
    [Tooltip("번호 (1, 2, 3 …)")]
    public TMP_Text numberText;
    [Tooltip("단계 이름")]
    public TMP_Text label;
    [Tooltip("고른 칸일 때만 켜지는 노란 바탕")]
    public GameObject selected;

    private HowToPlayPanel panel;
    private int index;
    private bool hooked;

    public void Setup(HowToPlayPanel owner, int i, string title)
    {
        panel = owner;
        index = i;
        if (numberText != null) numberText.text = (i + 1).ToString();
        if (label != null) label.text = title;
        if (!hooked)
        {
            hooked = true;
            GetComponent<Button>().onClick.AddListener(Click);
        }
    }

    public void SetSelected(bool on)
    {
        if (selected != null) selected.SetActive(on);
    }

    private void Click()
    {
        SoundManager.Play(Sfx.ButtonClick);
        if (panel != null) panel.Select(index);
    }
}
