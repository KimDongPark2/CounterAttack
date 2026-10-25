using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이 정보 점수 내역 한 줄 (아이콘 · 이름 · "+2점 × 7" · "+14")
// 줄 프리팹(또는 씬에 만들어 둔 줄 하나)에 붙이고 칸 4개 연결
public class PlayInfoRow : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    [Tooltip("\"+2점 × 7\" 처럼 한 번에 몇 점, 몇 번인지")]
    public TMP_Text eachText;
    [Tooltip("오른쪽 끝 점수 \"+14\"")]
    public TMP_Text valueText;

    private CanvasGroup group;
    private bool prepared;

    // 글자가 칸보다 길어도 두 줄로 쪼개지지 않게 (예: "+65"가 "+6" / "5"로 나뉘던 문제)
    private void Prepare()
    {
        prepared = true;
        foreach (TMP_Text t in new[] { nameText, eachText, valueText })
        {
            if (t == null) continue;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }
    }

    public void Set(Sprite sprite, string label, string each, string value, Color valueColor, float alpha)
    {
        if (!prepared) Prepare();
        if (icon != null && sprite != null) icon.sprite = sprite;
        if (nameText != null) nameText.text = label;
        if (eachText != null) eachText.text = each;
        if (valueText != null)
        {
            valueText.text = value;
            valueText.color = valueColor;
        }

        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = alpha;
    }
}