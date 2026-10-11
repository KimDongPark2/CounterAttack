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
    public GameObject Scanner;
    public GameObject handGun;
    public Camera playerCamera;
    public InventoryManager inventoryManager;
    public Player_ItemInstall itemInstall;
    public PlayerInput playerInput;

    [Header("Interaction Settings")]
    public float interactRange = 10f;
    [Tooltip("Input Actions에 있는 F키 액션 이름")]
    public string interactActionName = "Interact";

    [Header("Throw (G)")]
    [Tooltip("Input Actions에 있는 G키 액션 이름")]
    public string throwActionName = "throw";
    [Tooltip("앞으로 던지는 힘. 0이면 발밑에 툭 내려놓기")]
    public float throwForce = 4f;
    [Tooltip("살짝 위로 띄우는 힘")]
    public float throwUpward = 1.5f;
    [Tooltip("눈앞 몇 m 지점에서 던질지")]
    public float throwDistance = 0.8f;

    [Header("UI")]
    public TextMeshProUGUI noticeText;

    private Player_State state;
    private InputAction interactAction;
    private InputAction throwAction;
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

        throwAction = playerInput != null ? playerInput.actions.FindAction(throwActionName) : null;
        if (throwAction == null)
            Debug.LogError($"[Interaction] '{throwActionName}' 액션을 찾을 수 없습니다. Input Actions의 G키 액션 이름을 확인하세요.", this);

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

        if (throwAction != null && throwAction.WasPressedThisFrame()) TryThrow();
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

    // ---------- G키: 손에 든 물건 던져서 버리기 ----------
    // (Update에서 액션을 직접 확인. 태블릿이 열려 있으면 Update가 먼저 return해서 안 던져짐)
    private void TryThrow()
    {
        if (itemInstall != null && itemInstall.IsHolding)
        {
            Debug.Log("[Throw] 설치 미리보기 중이라 던지지 않습니다.");
            return;
        }

        ItemSlot slot = inventoryManager.HandSlot;
        if (slot == null)
        {
            Debug.Log($"[Throw] 선택된 슬롯이 없습니다. (handIndex = {state.handIndex}) 숫자키로 슬롯을 고르세요.");
            return;
        }
        if (!slot.isOccupied)
        {
            Debug.Log($"[Throw] {state.handIndex}번 슬롯이 비어 있습니다.");
            return;
        }
        if (slot.itemPrefab == null)
        {
            Debug.LogWarning($"[Throw] {state.handIndex}번 슬롯의 '{slot.currentItemName}'에 프리팹이 없습니다.");
            return;
        }
        ThrowHeldItem(slot);
    }

    private void ThrowHeldItem(ItemSlot slot)
    {
        Transform cam = playerCamera.transform;

        // 눈앞에 생성. 벽에 바짝 붙어 있으면 벽 앞쪽에 생성해서 벽을 뚫고 나가지 않게
        Vector3 spawnPos = cam.position + cam.forward * throwDistance;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, throwDistance + 0.3f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point - cam.forward * 0.3f;
        }

        Quaternion rot = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        GameObject thrown = Instantiate(slot.itemPrefab, spawnPos, rot);
        thrown.SetActive(true);
        thrown.tag = "Item";

        // 날아가려면 물리가 살아 있어야 함 (설치했던 물건은 Kinematic일 수 있어서 다시 풀어줌)
        foreach (MeshCollider mc in thrown.GetComponentsInChildren<MeshCollider>())
            mc.convex = true; // 움직이는 물체는 볼록 콜라이더만 가능
        Rigidbody rb = thrown.GetComponent<Rigidbody>();
        if (rb == null) rb = thrown.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.linearVelocity = cam.forward * throwForce + Vector3.up * throwUpward;
        rb.angularVelocity = Random.insideUnitSphere * 2f; // 살짝 빙글 돌면서 날아가게
        Debug.Log($"[Throw] '{slot.currentItemName}' 던짐 (위치 {spawnPos}, 속도 {rb.linearVelocity})");

        inventoryManager.ClearSlotUI(state.handIndex - 1);
    }

    private void SetNotice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
        noticeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void OnScan(InputValue value)
    {
        if (value.isPressed)
        {
            Animator scanAnim = Scanner.GetComponent<Animator>();
            scanAnim.SetTrigger("Scan");
        }
    }

    public void OnShoot(InputValue value)
    {
        if (value.isPressed)
        {
            Animator gunAnim = handGun.GetComponent<Animator>();
            gunAnim.SetTrigger("Fire");
        }
    }
}