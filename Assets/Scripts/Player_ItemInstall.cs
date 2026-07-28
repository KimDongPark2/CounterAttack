using UnityEngine;
using UnityEngine.InputSystem;

public class Player_ItemInstall : MonoBehaviour
{
    [Header("Preview Settings")]
    public GameObject prevObject;
    public float installRange = 10f;
    public Camera playerCamera;

    private bool isHoldingF = false;
    private bool canInstall = false;
    private Vector3 installPosition;

    private Player_State state;
    private InventoryManager inventoryManager;

    private void Start()
    {
        state = GetComponent<Player_State>();
        inventoryManager = InventoryManager.Instance;
        Debug.Log("ItemInstall Ready");
    }

    private void Update()
    {
        if (isHoldingF && HasItemInHand())
        {
            UpdatePreview();
        }
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            if (HasItemInHand())
                isHoldingF = true;
        }
        else
        {
            if (isHoldingF && canInstall)
                InstallItem();
            isHoldingF = false;
            canInstall = false;
            HidePreview();
        }
    }

    private bool HasItemInHand()
    {
        return state.handIndex > 0 && state.inventoryCnt > 0
            && GetHandItemInfo() != null;
    }

    private Item_info GetHandItemInfo()
    {
        int slotIndex = state.handIndex - 1;
        int count = 0;
        foreach (InventoryItem item in state.inventoryList)
        {
            count += item.amount;
            if (slotIndex < count)
                return item.itemData;
        }
        return null;
    }

    private void UpdatePreview()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo, installRange))
        {
            if (hitInfo.collider.CompareTag("InstallAvailable"))
            {
                canInstall = true;
                installPosition = hitInfo.point;

                Item_info handItem = GetHandItemInfo();
                if (handItem == null || handItem.Prefab == null) return;

                if (prevObject == null)
                {
                    prevObject = Instantiate(handItem.Prefab, installPosition, Quaternion.identity);
                    SetPreviewMode(prevObject);
                }
                else
                {
                    prevObject.transform.position = installPosition;
                }
                return;
            }
        }

        canInstall = false;
        HidePreview();
    }

    private void SetPreviewMode(GameObject obj)
    {
        foreach (Collider col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        foreach (MonoBehaviour mb in obj.GetComponentsInChildren<MonoBehaviour>())
            Destroy(mb);

        foreach (Renderer rend in obj.GetComponentsInChildren<Renderer>())
        {
            foreach (Material mat in rend.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    mat.color = new Color(c.r, c.g, c.b, 0.4f);
                }
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
        }
    }

    private void HidePreview()
    {
        if (prevObject != null)
        {
            Destroy(prevObject);
            prevObject = null;
        }
    }

    private void InstallItem()
    {
        Item_info handItem = GetHandItemInfo();
        if (handItem == null || handItem.Prefab == null) return;

        GameObject installed = Instantiate(handItem.Prefab, installPosition, Quaternion.identity);
        installed.tag = "Untagged";

        Debug.Log(handItem.Name + " installed");

        int slotIndex = state.handIndex - 1;
        state.RemoveItemAtSlot(slotIndex);
        inventoryManager.ClearSlotUI(slotIndex);

        HidePreview();
    }
}