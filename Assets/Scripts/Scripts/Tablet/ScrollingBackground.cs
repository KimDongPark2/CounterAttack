using UnityEngine;
using UnityEngine.UI;

// RawImage에 붙이면 텍스처가 계속 흘러가는 배경이 됨
[RequireComponent(typeof(RawImage))]
public class ScrollingBackground : MonoBehaviour
{
    [Tooltip("흘러가는 방향과 속도 (X: 오른쪽+, Y: 위쪽+)")]
    public Vector2 speed = new Vector2(0.03f, -0.03f);

    [Tooltip("타일 한 장이 화면에서 차지할 크기(px). 작을수록 아이콘이 작고 촘촘해짐")]
    public float tileSize = 300f;

    private RawImage rawImage;
    private RectTransform rect;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rect = (RectTransform)transform;
    }

    private void Update()
    {
        Rect uv = rawImage.uvRect;

        // 화면 크기에 맞춰 타일 반복 횟수 계산 (아이콘이 찌그러지지 않게)
        uv.width = rect.rect.width / tileSize;
        uv.height = rect.rect.height / tileSize;

        // 게임이 일시정지(Time.timeScale = 0)여도 배경은 계속 움직이게
        uv.x = Mathf.Repeat(uv.x + speed.x * Time.unscaledDeltaTime, 1f);
        uv.y = Mathf.Repeat(uv.y + speed.y * Time.unscaledDeltaTime, 1f);

        rawImage.uvRect = uv;
    }
}
