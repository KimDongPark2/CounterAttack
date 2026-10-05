using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using ConvenienceStore.Tablet;

// 바라보는 물건 줍기 + F키 처리 + 핫바 슬롯 선택
// F키는 이 스크립트만 받는다.
//   바라보는 물건을 주울 수 있으면 → 줍기
//   아니면 → Player_ItemInstall에 "설치 시작"을 알리고, 떼면 "설치"
public class Player_Interaction : MonoBehaviour
{
    [Header("Components")]
    public Camera playerCamera;
    public InventoryManager inventoryManager;
    public Player_ItemInstall itemInstall;
    public PlayerInput playerInput;

    [Header("Interaction Settings")]
    public float interactRange = 10f;
    [Tooltip("Input Actions에 있는 F키 액션 이름")]
    public string interactActionName = "Interact";

    [Header("UI")]
    public TextMeshProUGUI noticeText;

    private Player_State state;
    private InputAction interactAction;
    private Item_info target;
    private bool tabletOpen;

    private static readonly Key[] slotKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
        Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
    };

    private void Start()
    {
        state = GetComponent<Player_State>();
        if (itemInstall == null) itemInstall = GetComponent<Player_ItemInstall>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();

        interactAction = playerInput != null ? playerInput.actions.FindAction(interactActionName) : null;
        if (interactAction == null)
            Debug.LogError($"[Interaction] '{interactActionName}' 액션을 찾을 수 없습니다. Player Input 연결과 액션 이름을 확인하세요.", this);

        SetNotice("");
    }

    private void OnEnable() => TabletController.OnTabletToggled += HandleTabletToggled;
    private void OnDisable() => TabletController.OnTabletToggled -= HandleTabletToggled;

    // 태블릿이 열려 있는 동안에는 줍기/설치/슬롯 변경을 막는다
    private void HandleTabletToggled(bool open)
    {
        tabletOpen = open;
        if (open)
        {
            SetNotice("");
            itemInstall?.CancelHold();
        }
    }

    private void Update()
    {
        if (tabletOpen) return;

        FindTarget();
        HandleInteractKey();
        HandleSlotInput();
    }

    // ---------- 바라보는 물건 찾기 ----------
    private void FindTarget()
    {
        target = null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange) && hit.collider.CompareTag("Item"))
            target = hit.collider.GetComponentInParent<Item_info>();

        if (target == null || itemInstall.IsHolding)
        {
            SetNotice("");
            return;
        }

        SetNotice(inventoryManager.IsFull ? "인벤토리가 부족합니다." : $"(F) {target.Name} 집기");
    }

    // ---------- F키 ----------
    private void HandleInteractKey()
    {
        if (interactAction == null) return;

        if (interactAction.WasPressedThisFrame())
        {
            if (target != null && !inventoryManager.IsFull) PickUp(target);
            else itemInstall.BeginHold();
        }
        else if (interactAction.WasReleasedThisFrame())
        {
            itemInstall.EndHold();
        }
    }

    private void PickUp(Item_info info)
    {
        int slotIndex = inventoryManager.FindEmptySlot();
        if (slotIndex < 0) return;

        // 프리팹이 비어 있거나 씬에 있는 오브젝트를 가리키면,
        // 꺼둔 복사본을 하나 만들어서 프리팹 대신 보관한다
        GameObject prefab = info.Prefab;
        if (prefab == null || prefab == info.gameObject || prefab.scene.IsValid())
        {
            prefab = Instantiate(info.gameObject);
            prefab.SetActive(false);
            prefab.name = info.Name + "_RuntimePrefab";
            DontDestroyOnLoad(prefab);
        }

        inventoryManager.AddItemUI(info.Name, slotIndex, info.iconImage, info.Price, prefab);
        if (state.handIndex == 0) inventoryManager.SelectItem(slotIndex + 1);

        Destroy(info.gameObject);
        target = null;
        SetNotice("");
    }

    // ---------- 숫자키 / 마우스 휠로 슬롯 고르기 ----------
    private void HandleSlotInput()
    {
        if (itemInstall.IsHolding) return; // 설치 중에는 슬롯 변경 불가

        int restrict = state.inventoryRestrict;

        if (Keyboard.current != null)
        {
            for (int i = 0; i < slotKeys.Length && i < restrict; i++)
            {
                if (Keyboard.current[slotKeys[i]].wasPressedThisFrame)
                {
                    inventoryManager.SelectItem(i + 1);
                    return;
                }
            }
        }

        if (Mouse.current != null && state.handIndex > 0)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) inventoryManager.SelectItem(state.handIndex <= 1 ? restrict : state.handIndex - 1);
            else if (scroll < 0) inventoryManager.SelectItem(state.handIndex >= restrict ? 1 : state.handIndex + 1);
        }
    }

    // 버리기 (아직 기능 없음)
    public void Onthrow(InputValue value)
    {
        if (value.isPressed) Debug.Log("아이템 버림");
    }

    private void SetNotice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
        noticeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}
