using UnityEngine;

// 물건 하나의 정보. 상품 프리팹에 붙여둔다.
// 줍기, 장바구니, 배달, 설치가 전부 이 정보를 같이 쓴다.
public class Item_info : MonoBehaviour
{
    public enum ItemType
    {
        product,
        Item,
    }

    public ItemType Type;
    public string Name = "상품";
    [TextArea(1, 3)] public string Description;  // 주문 화면에 나오는 짧은 설명 (예: 신선한)
    public int Price = 0;                        // 원
    public Sprite iconImage;

    [Tooltip("이 물건의 프리팹 자신을 넣어두면 배달/설치 때 이걸로 생성함")]
    public GameObject Prefab;
}