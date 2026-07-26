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
    public Camera playerCamera;
    public float interactRange = 10f;
    public int invenRt;
    [Header("UI")]
    public TextMeshProUGUI noticeText;


    private void Start()
    {
        state = GetComponent<Player_State>();
        invenRt = state.inventoryRestrict;
        Init();
    }
    private void Update()
    {
        CheckItem();
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
                if (state.inventoryCnt < 5)
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
    public void OnInteract(InputValue value)
    { 
        if (value.isPressed && curTarget != null && (state.inventoryCnt < 5))
        {
            curitemInfo = curTarget.GetComponent<Item_info>();
            int itemAmount = state.inventoryCnt;

            Debug.Log($"{curTarget.name}을(를) 주웠습니다.");
            inventoryManager.AddItemUI(curitemInfo.Name, itemAmount, curitemInfo.iconImage);
            state.AddItemToState(curitemInfo);
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
    // 인벤토리 슬롯 선택 (PlayerInput 콜백)
    public void OnInventory_1(InputValue value) { if (value.isPressed) inventoryManager.SelectItem(1); }
    public void OnInventory_2(InputValue value) { if (value.isPressed) inventoryManager.SelectItem(2); }
    public void OnInventory_3(InputValue value) { if (value.isPressed) inventoryManager.SelectItem(3); }
    public void OnInventory_4(InputValue value) { if (value.isPressed) inventoryManager.SelectItem(4); }
    public void OnInventory_5(InputValue value) { if (value.isPressed) inventoryManager.SelectItem(5); }
}
