using System;
using UnityEngine;
using UnityEngine.UI;

// 주문 화면 카테고리 탭 (Button 그대로 사용)
//  - 탭은 항상 하나만 선택됨 (다른 곳을 클릭해도 안 풀림)
//  - 선택된 탭: 흰 배경 + 남색 글자/아이콘, 나머지: 투명 배경 + 흰 글자/아이콘
//  - 마우스 올리면 살짝 밝아지고, 누르면 조금 더 밝아짐 (Button 색은 스크립트가 알아서 설정)
//  - 선택한 카테고리의 상품 카드만 보이게
//  - Slide Highlight를 넣으면 흰 배경 하나가 탭 사이를 미끄러지듯 이동
public class ProductCategoryTabs : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public Button button;
        public ProductCardUI.Category category;
        [Tooltip("\"전체\" 탭이면 체크 (카테고리 상관없이 다 보여줌)")]
        public bool showAll;
    }

    [Tooltip("상품 카드들이 들어 있는 부모 (Cards나 Content 둘 다 됨)")]
    public Transform productListRoot;
    public Tab[] tabs;

    [Header("슬라이드 애니메이션 (선택)")]
    [Tooltip("비워두면 선택된 탭 배경이 바로 흰색으로 바뀜. 넣으면 이 흰 배경이 선택된 탭으로 미끄러져 이동")]
    public RectTransform slideHighlight;
    [Tooltip("클수록 빨리 따라감")]
    public float slideSpeed = 14f;

    [Header("글자/아이콘 색")]
    public Color selectedContent = new Color32(0x0A, 0x24, 0x56, 0xFF);  // 진한 남색
    public Color normalContent = Color.white;

    private int current = -1;
    private bool placed;

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            Button b = tabs[i].button;
            if (b == null) continue;

            int index = i;  // 버튼마다 자기 번호 기억
            b.transition = Selectable.Transition.ColorTint;
            b.navigation = new Navigation { mode = Navigation.Mode.None };  // WASD/방향키로 탭이 움직이지 않게
            if (b.image != null) b.image.color = Color.white;
            b.onClick.AddListener(() => Select(index));
        }
        Select(0);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= tabs.Length) return;
        current = index;
        Tab selected = tabs[index];

        // 카드 보이기 / 숨기기 (숨긴 카드 자리는 Layout Group이 알아서 당겨줌)
        int total = 0, shown = 0;
        if (productListRoot != null)
        {
            foreach (ProductCardUI card in productListRoot.GetComponentsInChildren<ProductCardUI>(true))
            {
                bool show = selected.showAll || card.category == selected.category;
                card.gameObject.SetActive(show);
                total++;
                if (show) shown++;
            }

            // 업그레이드 카드는 항상 "플레이어" 카테고리
            foreach (UpgradeCardUI card in productListRoot.GetComponentsInChildren<UpgradeCardUI>(true))
            {
                bool show = selected.showAll || selected.category == ProductCardUI.Category.Player;
                card.gameObject.SetActive(show);
                total++;
                if (show) shown++;
            }
        }
        // 확인용 로그: 카드를 몇 개 찾았고 몇 개 보여줬는지
        Debug.Log($"[카테고리] {(selected.showAll ? "전체" : selected.category.ToString())} 탭 → 카드 {total}개 중 {shown}개 표시");

        for (int i = 0; i < tabs.Length; i++) Paint(tabs[i].button, i == index);
    }

    private void Paint(Button b, bool on)
    {
        if (b == null) return;

        // 슬라이드를 쓰면 탭 자체 배경은 투명하게 두고 흰 배경은 슬라이드가 담당
        bool solid = on && slideHighlight == null;

        ColorBlock c = b.colors;
        c.normalColor = solid ? Color.white : new Color(1f, 1f, 1f, 0f);
        c.highlightedColor = solid ? Color.white : new Color(1f, 1f, 1f, 0.15f);  // 마우스 올림
        c.pressedColor = solid ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.3f);  // 누름
        c.selectedColor = c.normalColor;  // 클릭 후 하이라이트가 남지 않게
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.1f;
        b.colors = c;

        // 탭 안의 글자와 아이콘 색
        foreach (Graphic g in b.GetComponentsInChildren<Graphic>(true))
        {
            if (g != b.targetGraphic) g.color = on ? selectedContent : normalContent;
        }
    }

    private void Update()
    {
        if (slideHighlight == null || current < 0 || tabs[current].button == null) return;

        RectTransform target = (RectTransform)tabs[current].button.transform;

        // 처음 한 번은 바로 그 자리에, 그다음부터 부드럽게 따라감
        float k = placed ? 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime) : 1f;
        slideHighlight.position = Vector3.Lerp(slideHighlight.position, target.position, k);
        slideHighlight.sizeDelta = Vector2.Lerp(slideHighlight.sizeDelta, target.rect.size, k);
        placed = true;
    }
}