using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using ConvenienceStore.Tablet;

// 바라보는 물건 줍기 + F키 처리 + 핫바 슬롯 선택 + 스캔 + 권총
// F키는 이 스크립트만 받는다.
//   바라보는 물건을 주울 수 있으면 → 줍기
//   아니면 → Player_ItemInstall에 "설치 시작"을 알리고, 떼면 "설치"
// 핫바 1~5번은 물건 칸, 6번은 권총 칸 (상점에서 권총을 사면 생김)
//   권총을 들고 있으면 클릭 = 쏘기, 아니면 클릭 = 스캔
//   계산대 안에서는 권총 칸이 잠깐 사라지고 스캐너를 듦 (나오면 다시 권총)
// 태블릿을 열면 손에 든 총·스캐너는 안 보이게 숨김 (닫으면 다시 보임)
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

    [Header("Pistol (6번 칸)")]
    [Tooltip("핫바 6번 칸 (InventoryManager의 Item Slots에는 넣지 말 것)")]
    public ItemSlot pistolSlot;
    [Tooltip("총 Animator에서 다 꺼낸 상태의 이름. 이 상태일 때만 쏠 수 있음")]
    public string gunReadyState = "idle";

    [Header("UI")]
    public TextMeshProUGUI noticeText;

    public const int GunIndex = 6;   // handIndex가 6이면 권총을 든 상태

    // 총알이 실제로 나간 순간 알려줌 (WeaponFeel · CameraFeel이 반동에 씀). 스캐너 클릭에는 안 불림
    public static event System.Action OnGunFired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGunFired() => OnGunFired = null;

    // 지금 권총을 손에 들고 있는지
    public bool GunInHand => state != null && state.hasGun && state.handIndex == GunIndex;

    private Player_State state;
    private Animator scanAnim;
    private Animator gunAnim;
    private SimpleShoot gunShoot;
    private bool gunShown;
    private bool slotSelected;
    private bool wasInCounter;
    private bool gunBeforeCounter;   // 계산대에 들어갈 때 권총을 들고 있었는지
    private int lastItemIndex = 1;   // 마지막으로 고른 물건 칸 (1~5)
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

        if (Scanner != null) scanAnim = Scanner.GetComponent<Animator>();
        if (handGun != null)
        {
            gunAnim = handGun.GetComponent<Animator>();
            gunShoot = handGun.GetComponentInChildren<SimpleShoot>(true);
            handGun.SetActive(false);  // 처음엔 총 숨김 (6번을 고르면 꺼냄)
        }
        UpdateGun();

        SetNotice("");
    }

    private void OnEnable() => TabletController.OnTabletToggled += HandleTabletToggled;
    private void OnDisable() => TabletController.OnTabletToggled -= HandleTabletToggled;

    // 태블릿이 열려 있는 동안에는 줍기/설치/슬롯 변경을 막고, 손에 든 총·스캐너를 숨긴다
    private void HandleTabletToggled(bool open)
    {
        tabletOpen = open;
        if (open)
        {
            SetNotice("");
            itemInstall?.CancelHold();
        }
        SetHandsHidden(open);
    }

    // 총·스캐너 숨기기 / 다시 보이기
    // SetActive(false) 대신 그림(Renderer)만 끔 → 스캐너의 계산대 확인, 총 애니메이션 상태가 그대로 유지돼서
    // 태블릿을 닫으면 열기 전 모습 그대로 돌아옴
    private readonly System.Collections.Generic.List<Renderer> hiddenHands = new System.Collections.Generic.List<Renderer>();

    private void SetHandsHidden(bool hide)
    {
        if (hide)
        {
            if (hiddenHands.Count > 0) return;
            foreach (GameObject hand in new[] { Scanner, handGun })
            {
                if (hand == null) continue;
                foreach (Renderer r in hand.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hiddenHands.Add(r);
                }
            }
        }
        else
        {
            foreach (Renderer r in hiddenHands)
                if (r != null) r.enabled = true;
            hiddenHands.Clear();
        }
    }

    private void Update()
    {
        UpdateGun(); // 태블릿에서 권총을 사면 바로 6번 칸이 보이게 (태블릿이 열려 있어도 실행)

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
        SoundManager.Play(Sfx.ItemPickup);
    }

    // ---------- 숫자키 / 마우스 휠로 슬롯 고르기 ----------
    private void HandleSlotInput()
    {
        if (itemInstall.IsHolding) return; // 설치 중에는 슬롯 변경 불가

        int restrict = state.inventoryRestrict;

        // 6번 키: 권총 (샀을 때만, 계산대 밖에서만)
        if (state.hasGun && !state.inCounter && Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            SelectSlot(GunIndex);
            return;
        }

        if (Keyboard.current != null)
        {
            for (int i = 0; i < slotKeys.Length && i < restrict; i++)
            {
                if (Keyboard.current[slotKeys[i]].wasPressedThisFrame)
                {
                    SelectSlot(i + 1);
                    return;
                }
            }
        }

        // 휠: 1 ~ 5 (권총이 있으면 6까지) 돌아가며 선택
        if (Mouse.current != null && state.handIndex > 0)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            int last = (state.hasGun && !state.inCounter) ? GunIndex : restrict;
            int cur = state.handIndex;
            if (scroll > 0)
            {
                int prev = cur <= 1 ? last : (cur == GunIndex ? restrict : cur - 1);
                SelectSlot(prev);
            }
            else if (scroll < 0)
            {
                int next = cur >= last ? 1 : (cur == restrict ? last : cur + 1);
                SelectSlot(next);
            }
        }
    }

    // 플레이어가 직접 칸을 바꿀 때 (권총을 꺼낼 때만 소리)
    private void SelectSlot(int index)
    {
        if (index == GunIndex && index != state.handIndex)
            SoundManager.Play(Sfx.GunDraw);
        inventoryManager.SelectItem(index);
    }

    // ---------- 권총 보이기 / 6번 칸 표시 ----------
    private void UpdateGun()
    {
        if (state == null) return;

        if (state.handIndex >= 1 && state.handIndex <= state.inventoryRestrict)
            lastItemIndex = state.handIndex;

        // 계산대에 들어가면 권총 넣고 물건 칸으로 (스캐너가 나옴), 나오면 다시 권총
        if (state.inCounter != wasInCounter)
        {
            wasInCounter = state.inCounter;
            if (state.inCounter)
            {
                gunBeforeCounter = GunInHand;
                if (gunBeforeCounter) inventoryManager.SelectItem(lastItemIndex);
            }
            else if (gunBeforeCounter)
            {
                gunBeforeCounter = false;
                if (state.hasGun) inventoryManager.SelectItem(GunIndex);
            }
        }

        // 권총을 샀고 계산대 밖이면 핫바 6번 칸 보이기
        bool slotVisible = state.hasGun && !state.inCounter;
        if (pistolSlot != null && pistolSlot.gameObject.activeSelf != slotVisible)
        {
            if (!slotVisible && slotSelected)
            {
                pistolSlot.Deselect();
                slotSelected = false;
            }
            pistolSlot.gameObject.SetActive(slotVisible);
        }

        // 6번을 골랐으면 총 꺼내기, 다른 칸이면 넣기
        // GunMotion이 있으면 꺼내기 · 넣기 모션 (넣기는 모션이 끝난 뒤에 총을 끔). 없으면 예전처럼 바로 켜고 끔
        bool show = GunInHand;
        if (show != gunShown)
        {
            gunShown = show;
            if (show)
            {
                if (handGun != null) handGun.SetActive(true);
                if (gunAnim != null) gunAnim.SetBool("IsOpen", true);
                if (Motion != null) Motion.Draw();
            }
            else if (handGun != null)
            {
                if (Motion != null) Motion.Holster(() => { if (!gunShown && handGun != null) handGun.SetActive(false); });
                else handGun.SetActive(false);
            }
        }

        // 6번 칸 노란색(선택) 표시
        if (pistolSlot != null && pistolSlot.gameObject.activeInHierarchy && show != slotSelected)
        {
            slotSelected = show;
            if (show) pistolSlot.Select();
            else pistolSlot.Deselect();
        }
    }

    // ---------- G키: 손에 든 물건 던져서 버리기 ----------
    // (Update에서 액션을 직접 확인. 태블릿이 열려 있으면 Update가 먼저 return해서 안 던져짐)
    private void TryThrow()
    {
        if (GunInHand) return; // 권총은 못 버림

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

    // ---------- 클릭: 스캔 (Input Actions의 Scan) ----------
    public void OnScan(InputValue value)
    {
        if (tabletOpen || !value.isPressed) return;
        if (GunInHand) return;                                       // 권총 들고 있으면 스캔 안 함
        if (scanAnim == null || !scanAnim.GetBool("IsOpen")) return; // 계산대 밖이면 무시

        scanAnim.SetTrigger("Scan");
        if (CheckoutCounter.Instance != null) CheckoutCounter.Instance.TryScan(playerCamera); // 계산대 위 물건 찍기
    }

    // ---------- 클릭: 쏘기 (Input Actions의 Shoot) ----------
    public void OnShoot(InputValue value)
    {
        if (tabletOpen || !value.isPressed) return;
        if (!GunInHand) return;                                      // 권총을 들고 있을 때만
        if (!GunReady()) return;                                     // 꺼내는 중 · 쏘는 중이면 무시 (Animator 상태)
        if (Motion != null && Motion.Busy) return;                   // 꺼내기 · 넣기 모션 중이면 무시 (GunMotion)
        if (Reload != null && Reload.IsReloading) return;            // 재장전 중엔 쏘기만 막음 (Animator 상태는 그대로)
        if (FPSHandRig.Instance != null) FPSHandRig.Instance.PulseTrigger();   // 검지 당김 (빈 탄창이어도)
        if (Reload != null && !Reload.TryUseRound()) return;         // 총알이 없으면 딸깍 (GunReload)

        if (gunShoot != null) gunShoot.Fire();          // 애니메이션 + 레이캐스트 (손님 맞추기)
        else if (gunAnim != null) gunAnim.SetTrigger("Fire"); // SimpleShoot가 없으면 애니메이션만
        else return;                                          // 쏠 게 없음

        OnGunFired?.Invoke();                                 // 반동 · 화면 진동
    }

    // 탄약 · 재장전 (실행할 때 자동으로 붙어서, 처음 쓸 때 찾음)
    private GunReload gunReload;
    private GunReload Reload => gunReload != null ? gunReload : (gunReload = GetComponent<GunReload>());

    // 꺼내기 · 넣기 · 쏘기 모션 (실행할 때 자동으로 붙어서, 처음 쓸 때 찾음)
    private GunMotion gunMotion;
    private GunMotion Motion => gunMotion != null ? gunMotion : (gunMotion = GetComponent<GunMotion>());

    // 총을 다 꺼내서 idle 상태로 가만히 있을 때만 true (Animator 상태만 봄). GunReload도 씀
    public bool GunReady()
    {
        if (gunAnim == null) return true;
        if (gunAnim.IsInTransition(0)) return false;
        return gunAnim.GetCurrentAnimatorStateInfo(0).IsName(gunReadyState);
    }
}