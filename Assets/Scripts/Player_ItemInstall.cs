using UnityEngine;
using UnityEngine.InputSystem;

public class Player_ItemInstall : MonoBehaviour
{
    [Header("Preview Settings")]
    public float installRange = 10f;
    public Camera playerCamera;

    [HideInInspector]
    public GameObject prevObject;

    public bool isHoldingF = false;
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
        if (isHoldingF)
        {
            // Input System의 release 이벤트가 누락되는 경우를 대비해 Update에서 강제 체크
            if (Keyboard.current != null && !Keyboard.current.fKey.isPressed)
            {
                Debug.Log($"[ItemInstall] F키 뗌 감지 (Update) | 설치 가능 여부={canInstall}");
                if (canInstall)
                {
                    InstallItem();
                }
                else
                {
                    Debug.Log("[ItemInstall] 설치 불가 위치이므로 설치 취소 및 미리보기 삭제");
                }
                
                isHoldingF = false;
                canInstall = false;
                HidePreview();
            }
            else if (HasItemInHand())
            {
                UpdatePreview();
            }
            else
            {
                isHoldingF = false;
                HidePreview();
            }
        }
        else if (prevObject != null)
        {
            HidePreview();
        }
    }

    private float lastWarnTime = 0f;
    private Material installableMat;
    private Material notInstallableMat;
    private float currentRotationY = 0f;

    public void OnRotationL(InputValue value)
    {
        if (value.isPressed && isHoldingF)
        {
            currentRotationY -= 90f;
            if (prevObject != null)
                prevObject.transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
        }
    }

    public void OnRotationR(InputValue value)
    {
        if (value.isPressed && isHoldingF)
        {
            currentRotationY += 90f;
            if (prevObject != null)
                prevObject.transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
        }
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            Player_Interaction interaction = GetComponent<Player_Interaction>();
            if (interaction != null && interaction.CanPickUpItem())
            {
                Debug.Log("[ItemInstall] 아이템 줍기 우선 감지. 설치 준비 취소.");
                return;
            }

            Debug.Log($"[ItemInstall] F키 눌림 감지 | handIndex={state?.handIndex} | HasItemInHand={HasItemInHand()}");
            if (HasItemInHand())
            {
                ItemSlot handSlot = GetHandSlot();
                if (handSlot.itemPrefab == null)
                {
                    Debug.LogWarning($"[ItemInstall] {state.handIndex}번 슬롯의 아이템({handSlot.currentItemName})에 Prefab이 설정되어 있지 않습니다!");
                }
                else
                {
                    isHoldingF = true;
                    currentRotationY = 0f; // 새로 설치 모드 진입 시 회전 초기화
                }
            }
            else
            {
                if (state == null || state.handIndex <= 0)
                {
                    Debug.LogWarning("[ItemInstall] 현재 선택된 인벤토리 슬롯이 없습니다. (숫자키 1~5를 눌러 슬롯을 선택하세요)");
                }
                else
                {
                    ItemSlot slot = GetHandSlot();
                    if (slot == null || !slot.isOccupied)
                    {
                        Debug.LogWarning($"[ItemInstall] 현재 선택된 {state.handIndex}번 슬롯이 비어 있습니다.");
                    }
                }
            }
        }
        else
        {
            // Input System에서 정상적으로 Release 이벤트를 보내주는 경우
            if (isHoldingF)
            {
                Debug.Log($"[ItemInstall] F키 뗌 감지 (OnInteract) | 설치 가능 여부={canInstall}");
                if (canInstall)
                {
                    InstallItem();
                }
                else
                {
                    Debug.Log("[ItemInstall] 설치 불가 위치이므로 설치 취소 및 미리보기 삭제");
                }
            }
            isHoldingF = false;
            canInstall = false;
            HidePreview();
        }
    }

    private bool HasItemInHand()
    {
        return state != null && state.handIndex > 0 && GetHandSlot() != null && GetHandSlot().isOccupied;
    }

    private ItemSlot GetHandSlot()
    {
        if (inventoryManager == null) inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null || state == null) return null;
        return inventoryManager.GetSlot(state.handIndex - 1);
    }

    private void UpdatePreview()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null)
        {
            if (Time.time - lastWarnTime > 2f)
            {
                Debug.LogWarning("[ItemInstall] Player Camera가 연결되어 있지 않습니다!");
                lastWarnTime = Time.time;
            }
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hitInfo;

        ItemSlot handSlot = GetHandSlot();
        if (handSlot == null || handSlot.itemPrefab == null)
        {
            canInstall = false;
            HidePreview();
            return;
        }

        // Raycast 시 Ignore Raycast 트리거 옵션 지정
        if (Physics.Raycast(ray, out hitInfo, installRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            installPosition = hitInfo.point;
            if (hitInfo.collider.CompareTag("InstallAvailable"))
            {
                canInstall = true;
            }
            else
            {
                canInstall = false;
                if (Time.time - lastWarnTime > 2f)
                {
                    Debug.LogWarning($"[ItemInstall] 감지된 오브젝트('{hitInfo.collider.name}')의 Tag가 '{hitInfo.collider.tag}'입니다. ('InstallAvailable' 태그 필요)");
                    lastWarnTime = Time.time;
                }
            }
        }
        else
        {
            canInstall = false;
            installPosition = ray.origin + ray.direction * installRange;
            if (Time.time - lastWarnTime > 2f)
            {
                Debug.LogWarning($"[ItemInstall] 사거리({installRange}m) 내에 바라보는 콜라이더(바닥/벽)가 없습니다.");
                lastWarnTime = Time.time;
            }
        }

        Quaternion targetRotation = Quaternion.Euler(0, currentRotationY, 0);

        if (prevObject == null)
        {
            Debug.Log($"[ItemInstall] 미리보기 생성 완료: {handSlot.itemPrefab.name} (위치: {installPosition})");
            prevObject = Instantiate(handSlot.itemPrefab, installPosition, targetRotation);
            prevObject.SetActive(true);
            SetPreviewMode(prevObject, canInstall);
        }
        else
        {
            prevObject.transform.position = installPosition;
            prevObject.transform.rotation = targetRotation;
            prevObject.SetActive(true);
            UpdatePreviewColor(prevObject, canInstall);
        }
    }

    private void SetPreviewMode(GameObject obj, bool isInstallable)
    {
        // 1. 레이캐스트 간섭을 방지하기 위해 Ignore Raycast 레이어로 변경
        int ignoreRayLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRayLayer != -1)
        {
            SetLayerRecursively(obj, ignoreRayLayer);
        }

        // 2. 모든 콜라이더 비활성화
        foreach (Collider col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 3. 스크립트 비활성화/제거
        foreach (MonoBehaviour mb in obj.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb != this && mb != null)
                Destroy(mb);
        }

        UpdatePreviewColor(obj, isInstallable);
    }

    private void UpdatePreviewColor(GameObject obj, bool isInstallable)
    {
        if (installableMat == null)
            installableMat = CreateTransparentMaterial(new Color(0.2f, 0.8f, 0.2f, 0.45f));
        if (notInstallableMat == null)
            notInstallableMat = CreateTransparentMaterial(new Color(0.8f, 0.2f, 0.2f, 0.45f));

        Material targetMat = isInstallable ? installableMat : notInstallableMat;

        foreach (Renderer rend in obj.GetComponentsInChildren<Renderer>())
        {
            if (rend.sharedMaterials.Length > 0 && rend.sharedMaterials[0] == targetMat)
                continue;

            Material[] mats = new Material[rend.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = targetMat;
            rend.sharedMaterials = mats;
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        // URP 셰이더 먼저 시도, 없으면 Standard 사용
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha
            mat.SetFloat("_AlphaClip", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        // Built-in Standard 셰이더
        shader = Shader.Find("Standard");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color = color;
            return mat;
        }

        // 최후 대비: 기본 셰이더
        Material fallback = new Material(Shader.Find("Sprites/Default"));
        fallback.color = color;
        return fallback;
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void HidePreview()
    {
        if (prevObject != null)
        {
            // 에셋(Prefab Asset)이 아닌 씬 상의 오브젝트일 때만 안전하게 Destroy
            if (prevObject.scene.IsValid())
            {
                Destroy(prevObject);
            }
            prevObject = null;
        }
    }

    private void InstallItem()
    {
        ItemSlot handSlot = GetHandSlot();
        if (handSlot == null || handSlot.itemPrefab == null) return;

        Quaternion targetRotation = Quaternion.Euler(0, currentRotationY, 0);
        GameObject installed = Instantiate(handSlot.itemPrefab, installPosition, targetRotation);
        installed.tag = "Item";
        installed.SetActive(true);

        Debug.Log(handSlot.currentItemName + " installed");

        int slotIndex = state.handIndex - 1;
        inventoryManager.ClearSlotUI(slotIndex);
        state.inventoryCnt--;

        HidePreview();
    }
}