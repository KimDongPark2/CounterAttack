using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConvenienceStore.Tablet
{
    /// <summary>
    /// Tab 키로 손에 든 3D 태블릿을 눈앞으로 올리고 내린다.
    /// 이 스크립트는 카메라의 자식으로 들어간 "TabletRig" 오브젝트에 붙인다.
    ///
    /// 하이어라키 예시
    ///   Player
    ///     └ Main Camera
    ///         └ TabletRig            <- 이 스크립트
    ///             ├ TabletModel      <- 실제 3D 모델 (tabletRoot)
    ///             │   └ ScreenCanvas <- World Space Canvas (screenCanvas)
    ///             ├ HiddenPoint      <- 화면 아래 숨김 위치
    ///             └ ShownPoint       <- 눈앞 표시 위치
    ///
    /// 주의: tabletRoot / hiddenPoint / shownPoint 는 모두 같은 부모(TabletRig)의
    /// 자식이어야 한다. localPosition·localRotation 을 그대로 복사해서 쓰기 때문이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TabletController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("움직일 태블릿 3D 모델의 최상위 Transform")]
        [SerializeField] private Transform tabletRoot;

        [Tooltip("숨김 위치 (보통 화면 아래, 살짝 기울인 상태)")]
        [SerializeField] private Transform hiddenPoint;

        [Tooltip("표시 위치 (눈앞, 화면이 보이도록 세운 상태)")]
        [SerializeField] private Transform shownPoint;

        [Tooltip("태블릿 화면의 World Space Canvas")]
        [SerializeField] private Canvas screenCanvas;

        [Tooltip("태블릿을 열 때 숨길 조준점 (없으면 비워둬도 됨)")]
        [SerializeField] private GameObject crosshair;

        [Header("동작")]
        [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("열 때 커서를 풀고, 닫을 때 다시 잠글지 여부")]
        [SerializeField] private bool controlCursor = true;

        [Tooltip("태블릿이 열려 있는 동안 꺼둘 컴포넌트들. 보통 마우스 시점 회전 스크립트를 넣는다.")]
        [SerializeField] private Behaviour[] disableWhileOpen;

        [Header("입력 (새 Input System)")]
        [Tooltip("인스펙터에서 바인딩을 바꿀 수 있다. 기본값은 키보드 Tab.")]
        [SerializeField]
        private InputAction toggleAction = new InputAction(
            name: "ToggleTablet",
            type: InputActionType.Button,
            binding: "<Keyboard>/tab");

        /// <summary>태블릿이 현재 열려 있는지.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>열림(true) / 닫힘(false) 이 바뀔 때 호출. 다른 시스템에서 구독하면 된다.</summary>
        public static event System.Action<bool> OnTabletToggled;

        private Coroutine moveRoutine;
        private GraphicRaycaster screenRaycaster;

        private void Awake()
        {
            if (screenCanvas != null)
                screenRaycaster = screenCanvas.GetComponent<GraphicRaycaster>();
        }

        private void OnEnable()
        {
            toggleAction.performed += HandleToggle;
            toggleAction.Enable();
        }

        private void OnDisable()
        {
            toggleAction.performed -= HandleToggle;
            toggleAction.Disable();
        }

        private void Start()
        {
            // 시작할 때는 확실히 닫힌 상태로.
            if (tabletRoot != null && hiddenPoint != null)
            {
                tabletRoot.localPosition = hiddenPoint.localPosition;
                tabletRoot.localRotation = hiddenPoint.localRotation;
            }
            SetScreenActive(false);
            IsOpen = false;
        }

        private void HandleToggle(InputAction.CallbackContext ctx) => Toggle();

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRoutine(open));

            if (disableWhileOpen != null)
            {
                foreach (var b in disableWhileOpen)
                    if (b != null) b.enabled = !open;
            }

            if (crosshair != null) crosshair.SetActive(!open);

            if (controlCursor)
            {
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = open;
            }

            OnTabletToggled?.Invoke(open);
        }

        private IEnumerator MoveRoutine(bool open)
        {
            // 열 때는 애니메이션 시작과 동시에 화면을 켜고,
            // 닫을 때는 애니메이션이 끝난 뒤에 끈다.
            if (open) SetScreenActive(true);

            Transform target = open ? shownPoint : hiddenPoint;
            if (tabletRoot == null || target == null)
            {
                moveRoutine = null;
                yield break;
            }

            Vector3 startPos = tabletRoot.localPosition;
            Quaternion startRot = tabletRoot.localRotation;
            Vector3 endPos = target.localPosition;
            Quaternion endRot = target.localRotation;

            float t = 0f;
            while (t < 1f)
            {
                // 일시정지(Time.timeScale = 0) 중에도 동작하도록 unscaled 사용
                t += Time.unscaledDeltaTime / moveDuration;
                float k = ease.Evaluate(Mathf.Clamp01(t));
                tabletRoot.localPosition = Vector3.LerpUnclamped(startPos, endPos, k);
                tabletRoot.localRotation = Quaternion.SlerpUnclamped(startRot, endRot, k);
                yield return null;
            }

            tabletRoot.localPosition = endPos;
            tabletRoot.localRotation = endRot;

            if (!open) SetScreenActive(false);
            moveRoutine = null;
        }

        private void SetScreenActive(bool on)
        {
            if (screenCanvas != null) screenCanvas.enabled = on;
            if (screenRaycaster != null) screenRaycaster.enabled = on; // 닫힌 상태에서 클릭이 먹지 않도록
        }

#if UNITY_EDITOR
        // 인스펙터 우클릭 → "현재 위치를 ShownPoint 로" 로 위치를 편하게 잡을 수 있다.
        [ContextMenu("현재 위치를 ShownPoint 로 저장")]
        private void CaptureShown()
        {
            if (tabletRoot == null || shownPoint == null) return;
            shownPoint.localPosition = tabletRoot.localPosition;
            shownPoint.localRotation = tabletRoot.localRotation;
            UnityEditor.EditorUtility.SetDirty(shownPoint);
        }

        [ContextMenu("현재 위치를 HiddenPoint 로 저장")]
        private void CaptureHidden()
        {
            if (tabletRoot == null || hiddenPoint == null) return;
            hiddenPoint.localPosition = tabletRoot.localPosition;
            hiddenPoint.localRotation = tabletRoot.localRotation;
            UnityEditor.EditorUtility.SetDirty(hiddenPoint);
        }
#endif
    }
}
