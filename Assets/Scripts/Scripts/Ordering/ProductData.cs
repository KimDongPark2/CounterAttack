using UnityEngine;

namespace ConvenienceStore.Ordering
{
    /// <summary>
    /// 편의점에서 취급하는 상품 1종의 정보.
    /// Project 창 우클릭 → Create → ConvenienceStore → Product 로 에셋을 만든다.
    /// (삼각김밥, 콜라, 컵라면 ... 하나씩 에셋으로)
    /// </summary>
    [CreateAssetMenu(fileName = "Product_", menuName = "ConvenienceStore/Product")]
    public class ProductData : ScriptableObject
    {
        [Header("표시 정보")]
        public string displayName = "새 상품";
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("가격")]
        [Tooltip("한 박스를 주문할 때 내가 내는 돈")]
        public int wholesalePrice = 8000;

        [Tooltip("손님에게 파는 개당 가격")]
        public int retailPrice = 1200;

        [Tooltip("한 박스에 들어 있는 개수")]
        [Min(1)] public int unitsPerBox = 10;

        [Header("배송")]
        [Tooltip("주문이 도착했을 때 씬에 생성될 상자 프리팹")]
        public GameObject boxPrefab;

        /// <summary>한 박스를 다 팔았을 때 남는 이익. UI 에 참고용으로 띄우기 좋다.</summary>
        public int ProfitPerBox => retailPrice * unitsPerBox - wholesalePrice;
    }
}
