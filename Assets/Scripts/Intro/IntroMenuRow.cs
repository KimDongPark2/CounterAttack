using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인트로 영수증의 메뉴 한 줄 ("게임 시작 ······ START")
// 마우스를 올리면 이 줄이 선택되고, 누르면 실행
// 줄 오브젝트에 붙이고, 줄 자체에 투명한 Image(알파 0)를 하나 넣어야 마우스가 닿음
public class IntroMenuRow : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public enum RowAction
    {
        [InspectorName("게임 시작 (씬 불러오기)")] StartGame,
        [InspectorName("나가기")] Quit,
        [InspectorName("준비 중 (안내만)")] ComingSoon,
        [InspectorName("직접 연결 (On Choose만)")] Custom,
    }

    public RowAction action = RowAction.ComingSoon;

    [Header("모양")]
    [Tooltip("선택됐을 때만 보이는 남색 띠 (info_round)")]
    public Image highlight;
    [Tooltip("선택됐을 때만 보이는 ▶ (intro_arrow)")]
    public Image arrow;
    [Tooltip("가운데 점선 (intro_dots)")]
    public Image dots;
    [Tooltip("왼쪽 이름, 오른쪽 영어 글자")]
    public TMP_Text[] texts;

    [Header("눌렀을 때 추가로 할 일 (선택)")]
    public UnityEvent onChoose;

    [HideInInspector] public IntroMenu menu;
    [HideInInspector] public int index;

    public void OnPointerEnter(PointerEventData e)
    {
        if (menu != null) menu.Select(index);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (menu == null) return;
        menu.Select(index);
        menu.Choose();
    }

    public void Paint(bool on, Color textOn, Color textOff, Color dotsOn, Color dotsOff)
    {
        if (highlight != null) highlight.enabled = on;
        if (arrow != null) arrow.enabled = on;          // 꺼도 자리는 남아서 글자가 안 밀림
        if (dots != null) dots.color = on ? dotsOn : dotsOff;
        if (texts != null)
            foreach (TMP_Text t in texts)
                if (t != null) t.color = on ? textOn : textOff;
    }
}
