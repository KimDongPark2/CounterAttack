using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConvenienceStore.Tablet
{
    // Tab 키로 태블릿을 열고 닫는 건 이 스크립트 하나만 담당한다.
    // 움직이는 방법은 둘 중 하나:
    //   1) Tablet Animator를 넣으면 → Open / Close 트리거로 애니메이션 재생
    //   2) 비워두면 → HiddenPoint ↔ ShownPoint 사이를 코드로 부드럽게 이동
    //
    // 하이어라키 예시 (2번 방식)
    //   Main Camera
    //     └ TabletRig            <- 이 스크립트
    //         ├ TabletModel      <- tabletRoot
    //         │   └ ScreenCanvas <- screenCanvas (World Space)
    //         ├ HiddenPoint
    //         └ ShownPoint
    [DisallowMultipleComponent]
    public class TabletController : MonoBehaviour
    {
        [Header("화면")]
        [Tooltip("태블릿 화면의 World Space Canvas")]
        [SerializeField] private Canvas screenCanvas;

        [Tooltip("체크하면 닫혀 있을 때 화면을 끔. 끄면 닫혀 있어도 로고/메뉴는 보이고 클릭만 막힘")]
        [SerializeField] private bool hideScreenWhenClosed = false;

        [Header("움직이기 1) 애니메이터 방식")]
        [Tooltip("넣으면 아래 위치 이동 대신 애니메이션을 쓴다")]
        [SerializeField] private Animator tabletAnimator;
        [SerializeField] private string openTrigger = "Open";
        [SerializeField] private string closeTrigger = "Close";

        [Header("움직이기 2) 위치 이동 방식")]
        [SerializeField] private Transform tabletRoot;
        [SerializeField] private Transform hiddenPoint;
        [SerializeField] private Transform shownPoint;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("공통")]
        [Tooltip("여는/닫는 데 걸리는 시간(초). 애니메이터 방식이면 애니메이션 길이에 맞춰주기")]
        [SerializeField, Min(0.01f)] private float moveDuration = 0.25f;

        [Tooltip("열 때 커서를 풀고, 닫을 때 다시 잠글지")]
        [SerializeField] private bool controlCursor = true;

        [Tooltip("열려 있는 동안 꺼둘 스크립트 (예: Player_Look, Player_movement)")]
        [SerializeField] private Behaviour[] disableWhileOpen;

        [Tooltip("열려 있는 동안 숨길 오브젝트 (예: 조준점, 핫바 UI Canvas)")]
        [SerializeField] private GameObject[] hideWhileOpen;

        [Header("입력")]
        [SerializeField]
        private InputAction toggleAction = new InputAction(
            name: "ToggleTablet",
            type: InputActionType.Button,
            binding: "<Keyboard>/tab");

        public bool IsOpen { get; private set; }

        // 열림(true) / 닫힘(false)이 바뀔 때 알려줌 (Player_Interaction, TabletAppSwitcher가 듣고 있음)
        public static event System.Action<bool> OnTabletToggled;

        private Coroutine moveRoutine;
        private GraphicRaycaster screenRaycaster;

        private void Awake()
        {
            if (screenCanvas != null) screenRaycaster = screenCanvas.GetComponent<GraphicRaycaster>();
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
            if (tabletAnimator == null && tabletRoot != null && hiddenPoint != null)
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
                foreach (var b in disableWhileOpen) if (b != null) b.enabled = !open;

            if (hideWhileOpen != null)
                foreach (var go in hideWhileOpen) if (go != null) go.SetActive(!open);

            if (controlCursor)
            {
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = open;
            }

            OnTabletToggled?.Invoke(open);
        }

        private IEnumerator MoveRoutine(bool open)
        {
            // 열 때는 바로 화면을 켜고, 닫을 때는 다 내려간 뒤에 끈다
            if (open) SetScreenActive(true);

            if (tabletAnimator != null)
            {
                tabletAnimator.ResetTrigger(openTrigger);
                tabletAnimator.ResetTrigger(closeTrigger);
                tabletAnimator.SetTrigger(open ? openTrigger : closeTrigger);
                yield return new WaitForSecondsRealtime(moveDuration);
            }
            else if (tabletRoot != null && hiddenPoint != null && shownPoint != null)
            {
                Transform target = open ? shownPoint : hiddenPoint;
                Vector3 startPos = tabletRoot.localPosition;
                Quaternion startRot = tabletRoot.localRotation;

                float t = 0f;
                while (t < 1f)
                {
                    // 일시정지(Time.timeScale = 0) 중에도 움직이게 unscaled 사용
                    t += Time.unscaledDeltaTime / moveDuration;
                    float k = ease.Evaluate(Mathf.Clamp01(t));
                    tabletRoot.localPosition = Vector3.LerpUnclamped(startPos, target.localPosition, k);
                    tabletRoot.localRotation = Quaternion.SlerpUnclamped(startRot, target.localRotation, k);
                    yield return null;
                }
                tabletRoot.localPosition = target.localPosition;
                tabletRoot.localRotation = target.localRotation;
            }

            if (!open) SetScreenActive(false);
            moveRoutine = null;
        }

        private void SetScreenActive(bool on)
        {
            if (screenCanvas != null) screenCanvas.enabled = on || !hideScreenWhenClosed;
            if (screenRaycaster != null) screenRaycaster.enabled = on; // 닫힌 상태에서 클릭만 안 되게
        }

#if UNITY_EDITOR
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