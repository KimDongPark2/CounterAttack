using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 순위표 한 줄. 칸만 연결해두면 RankingBoard가 채움
// 줄 하나 만들고 연결한 다음 Ctrl+D로 10줄 복사하기 (위에서부터 1위)
// 마우스를 올리면 RankingBoard가 옆에 사원증(RankBadgePopup)을 띄움
// → 마우스를 받으려면 줄(Row)에 Image가 하나 있어야 함 (색 알파 0, Raycast Target 켜기)
public class RankRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("순위 숫자 (1, 2, 3 …)")]
    public TMP_Text rankText;
    [Tooltip("1~3위 메달 동그라미 (선택 · 4위부터는 숨김)")]
    public Image medal;
    [Tooltip("사진 (RawImage · Mask 안에)")]
    public RawImage photo;
    [Tooltip("학번이름")]
    public TMP_Text nameText;
    [Tooltip("점수")]
    public TMP_Text scoreText;
    [Tooltip("1위 줄 노란 바탕 (선택 · 1위 기록이 있을 때만 보임)")]
    public GameObject highlight;
    [Tooltip("마우스를 올렸을 때 보일 파란 테두리 바탕 (선택)")]
    public GameObject hover;

    // RankingBoard가 채워줌
    [System.NonSerialized] public RankingBoard board;
    [System.NonSerialized] public RankingStore.Entry entry;
    [System.NonSerialized] public int rank;

    public void OnPointerEnter(PointerEventData e)
    {
        if (board != null) board.Hover(this, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (board != null) board.Hover(this, false);
    }

    // 처음 사진 칸에 들어 있던 그림 (기록이 없을 때 다시 보여줌)
    [System.NonSerialized] public Texture emptyPhoto;
    [System.NonSerialized] public bool remembered;
}
