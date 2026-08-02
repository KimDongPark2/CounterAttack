using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UIElements;

public class Player_Interaction : MonoBehaviour
{
    private GameObject curTarget;
    private Item_info curitemInfo;
    [Header("Interaction Settings")]
    private Player_State state;
    public InventoryManager inventoryManager;
    public Player_ItemInstall itemInstall;
    public Camera playerCamera;
    public float interactRange = 10f;
    public int invenRt;
    [Header("UI")]
    public TextMeshProUGUI noticeText;

    //YAS

    private void Start()
    {
        itemInstall = GetComponent<Player_ItemInstall>();
        state = GetComponent<Player_State>();
        invenRt = state.inventoryRestrict;
        Init();
    }
    private void Update()
    {
        CheckItem();
        HandleInventoryInput();
    }
    private void Init()
    {
        noticeText.text = "";
        noticeText.gameObject.SetActive(false);
    }
    public void CheckItem()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo, interactRange))
        {
            if (hitInfo.collider.CompareTag("Item"))
            {
                curTarget = hitInfo.collider.gameObject;
                Item_info info = curTarget.GetComponent<Item_info>();
                noticeText.gameObject.SetActive(true);
                if (state.inventoryCnt < state.inventoryRestrict)
                    noticeText.text = $"(F) {info.Name} 집기";
                else
                    noticeText.text = "인벤토리가 부족합니다.";
                return;
            }
        }
        curTarget = null;
        noticeText.gameObject.SetActive(false);
        noticeText.text = "";
    }

    public bool CanPickUpItem()
    {
        return curTarget != null && state.inventoryCnt < state.inventoryRestrict;
    }

    public void OnInteract(InputValue value)
    { 
        if (value.isPressed && curTarget != null && (state.inventoryCnt < state.inventoryRestrict))
        {
            curitemInfo = curTarget.GetComponent<Item_info>();
            if (curitemInfo == null) return;

            int slotIndex = inventoryManager.FindEmptySlot();
            if (slotIndex < 0) return;

            // 설치용 프리팹 결정: Item_info.Prefab이 없거나 씬 오브젝트 자체를 가리키면
            // 현재 아이템을 복제해서 런타임 프리팹으로 사용
            GameObject prefabToStore = curitemInfo.Prefab;
            if (prefabToStore == null || prefabToStore == curTarget || prefabToStore.scene.IsValid())
            {
                prefabToStore = Instantiate(curTarget);
                prefabToStore.SetActive(false);
                prefabToStore.name = curitemInfo.Name + "_RuntimePrefab";
                DontDestroyOnLoad(prefabToStore);
            }

            Debug.Log($"{curTarget.name}을(를) 주웠습니다.");
            inventoryManager.AddItemUI(curitemInfo.Name, slotIndex, curitemInfo.iconImage, curitemInfo.Price, prefabToStore);
            state.inventoryCnt++;

            // 손에 아무 슬롯도 선택되어 있지 않으면 주운 슬롯(1~5)을 자동 선택
            if (state.handIndex == 0)
            {
                inventoryManager.SelectItem(slotIndex + 1);
            }

            Destroy( curTarget );
            curTarget = null;
            curitemInfo = null;
        }
    }
    public void Onthrow(InputValue value)
    {
        if (value.isPressed)
        {
            Debug.Log("아이템 버림");
        }
    }
    private void HandleInventoryInput()
    {
        // 설치 중(F키 홀드)일 때는 슬롯 변경 불가
        if (itemInstall != null && itemInstall.isHoldingF) return;

        int restrict = state.inventoryRestrict;

        // 1. 키보드 숫자키 (1~0) 자동 체크 루프
        if (Keyboard.current != null)
        {
            // 인덱스 0~9는 슬롯 1~10 (키보드 1~9, 0) 매칭
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, 
                           Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            
            for (int i = 0; i < keys.Length; i++)
            {
                int slotNum = i + 1; // 1~10
                if (slotNum <= restrict && Keyboard.current[keys[i]].wasPressedThisFrame)
                {
                    inventoryManager.SelectItem(slotNum);
                    break;
                }
            }
        }

        // 2. 마우스 휠(Scroll)을 이용한 인벤토리 슬롯 변경 (편의성 기능)
        if (Mouse.current != null && state.handIndex > 0)
        {
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) 
            {
                int nextSlot = state.handIndex - 1;
                if (nextSlot < 1) nextSlot = restrict;
                inventoryManager.SelectItem(nextSlot);
            }
            else if (scroll < 0) 
            {
                int nextSlot = state.handIndex + 1;
                if (nextSlot > restrict) nextSlot = 1;
                inventoryManager.SelectItem(nextSlot);
            }
        }
    }
}
