using UnityEngine;

namespace ConvenienceStore.Tablet
{
    /// <summary>
    /// 태블릿 화면 안에서 "앱"(패널)을 전환한다.
    /// ScreenCanvas 아래에 HomePanel / OrderPanel / StatsPanel ... 을 두고
    /// 이 스크립트의 panels 배열에 순서대로 넣으면 된다.
    /// 홈 버튼은 OnClick 에 ShowHome() 을, 주문 앱 아이콘은 Show(1) 을 연결.
    /// </summary>
    public class TabletAppSwitcher : MonoBehaviour
    {
        [Tooltip("0번은 보통 홈 화면")]
        [SerializeField] private GameObject[] panels;

        [Tooltip("태블릿을 닫았다 열면 홈으로 돌아갈지")]
        [SerializeField] private bool resetToHomeOnClose = true;

        private int current = -1;

        private void OnEnable()
        {
            TabletController.OnTabletToggled += HandleTabletToggled;
        }

        private void OnDisable()
        {
            TabletController.OnTabletToggled -= HandleTabletToggled;
        }

        private void Start() => Show(0);

        private void HandleTabletToggled(bool open)
        {
            if (!open && resetToHomeOnClose) Show(0);
        }

        public void ShowHome() => Show(0);

        public void Show(int index)
        {
            if (panels == null || panels.Length == 0) return;
            index = Mathf.Clamp(index, 0, panels.Length - 1);
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) panels[i].SetActive(i == index);
            current = index;
        }

        public int Current => current;
    }
}
