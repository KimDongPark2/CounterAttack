using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// 손에 든 물건을 선반에 설치
// F키는 Player_Interaction이 받아서 BeginHold(누름) / EndHold(뗌)로 알려준다
//   누르고 있는 동안: 바라보는 곳에 반투명 미리보기 (초록 = 설치 가능, 빨강 = 불가)
//   떼면: 설치 가능하면 설치하고 네임택 이름 변경
public class Player_ItemInstall : MonoBehaviour
{
    [Header("Preview Settings")]
    public Camera playerCamera;
    public float installRange = 10f;
    public float snapRadius = 0.5f;
    public bool snapRotation = true;

    public bool IsHolding { get; private set; }

    private Player_State state;
    private InventoryManager inventory;

    private GameObject preview;
    private bool canInstall;
    private Vector3 installPosition;
    private Quaternion installRotation = Quaternion.identity;
    private Transform snapPoint;
    private float rotationY;

    private Material installableMat;
    private Material blockedMat;
    private float lastWarnTime;

    private void Start()
    {
        state = GetComponent<Player_State>();
        inventory = InventoryManager.Instance;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (!IsHolding) return;

        if (HandSlot() == null)
        {
            CancelHold();
            return;
        }
        UpdatePreview();
    }

    // ---------- Player_Interaction이 부르는 부분 ----------
    public void BeginHold()
    {
        if (HandSlot() == null)
        {
            Debug.Log("[ItemInstall] 손에 든 물건이 없습니다. 숫자키로 물건이 있는 슬롯을 고르세요.");
            return;
        }
        IsHolding = true;
        rotationY = 0f;
    }

    public void EndHold()
    {
        if (!IsHolding) return;
        if (canInstall) InstallItem();
        CancelHold();
    }

    public void CancelHold()
    {
        IsHolding = false;
        canInstall = false;
        HidePreview();
    }

    // ---------- 회전 (Input Actions의 RotationL / RotationR) ----------
    public void OnRotationL(InputValue value) { if (value.isPressed) Rotate(-90f); }
    public void OnRotationR(InputValue value) { if (value.isPressed) Rotate(90f); }
    private void Rotate(float degrees) { if (IsHolding) rotationY += degrees; }

    // 손에 든 슬롯에 설치할 수 있는 물건이 있으면 그 슬롯, 없으면 null
    private ItemSlot HandSlot()
    {
        if (inventory == null) inventory = InventoryManager.Instance;
        ItemSlot slot = inventory != null ? inventory.HandSlot : null;
        return (slot != null && slot.isOccupied && slot.itemPrefab != null) ? slot : null;
    }

    // ---------- 미리보기 ----------
    private void UpdatePreview()
    {
        ItemSlot slot = HandSlot();
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        canInstall = false;
        snapPoint = null;
        installRotation = Quaternion.Euler(0, rotationY, 0);
        installPosition = ray.origin + ray.direction * installRange;

        if (Physics.Raycast(ray, out RaycastHit hit, installRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            installPosition = hit.point;

            if (hit.collider.CompareTag("InstallAvailable"))
            {
                canInstall = true;
                snapPoint = FindSnapPoint(hit.collider.transform, hit.point);
                if (snapPoint != null)
                {
                    installPosition = snapPoint.position;
                    if (snapRotation) installRotation = snapPoint.rotation * installRotation;
                }
            }
            else
            {
                Warn($"'{hit.collider.name}'에는 설치할 수 없습니다. (InstallAvailable 태그 필요)");
            }
        }

        if (preview == null)
        {
            preview = Instantiate(slot.itemPrefab);
            preview.SetActive(true);
            MakePreviewOnly(preview);
        }
        preview.transform.SetPositionAndRotation(installPosition, installRotation);
        PaintPreview(preview, canInstall);
    }

    // 맞은 오브젝트 안에서 이름이 Pos로 시작하는 자리 중 가장 가까운 곳
    private Transform FindSnapPoint(Transform root, Vector3 point)
    {
        Transform best = null;
        float bestDist = snapRadius;
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("Pos")) continue;
            float d = Vector3.Distance(point, t.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }
        return best;
    }

    // 미리보기용 복사본은 레이에 안 걸리고, 안 부딪히고, 안 떨어지고, 스크립트도 안 돌게
    private void MakePreviewOnly(GameObject obj)
    {
        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreLayer != -1)
        {
            foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ignoreLayer;
        }
        foreach (Collider col in obj.GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (Rigidbody rb in obj.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        foreach (MonoBehaviour mb in obj.GetComponentsInChildren<MonoBehaviour>()) Destroy(mb);
    }

    private void PaintPreview(GameObject obj, bool installable)
    {
        if (installableMat == null) installableMat = CreateTransparentMaterial(new Color(0.2f, 0.8f, 0.2f, 0.45f));
        if (blockedMat == null) blockedMat = CreateTransparentMaterial(new Color(0.8f, 0.2f, 0.2f, 0.45f));
        Material target = installable ? installableMat : blockedMat;

        foreach (Renderer rend in obj.GetComponentsInChildren<Renderer>())
        {
            Material[] current = rend.sharedMaterials;
            if (current.Length > 0 && current[0] == target) continue;

            Material[] mats = new Material[current.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = target;
            rend.sharedMaterials = mats;
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        // URP 먼저, 없으면 기본(Standard) 셰이더
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
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

        Material standard = new Material(Shader.Find("Standard"));
        standard.SetFloat("_Mode", 3);
        standard.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        standard.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        standard.SetInt("_ZWrite", 0);
        standard.DisableKeyword("_ALPHATEST_ON");
        standard.EnableKeyword("_ALPHABLEND_ON");
        standard.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        standard.renderQueue = 3000;
        standard.color = color;
        return standard;
    }

    private void HidePreview()
    {
        if (preview != null) Destroy(preview);
        preview = null;
    }

    // ---------- 설치 ----------
    private void InstallItem()
    {
        ItemSlot slot = HandSlot();
        if (slot == null) return;

        GameObject installed = Instantiate(slot.itemPrefab, installPosition, installRotation);
        installed.tag = "Item";
        installed.SetActive(true);

        UpdateNameTag(slot.currentItemName);
        inventory.ClearSlotUI(state.handIndex - 1);
    }

    // 스냅된 Pos 안에 들어 있는 네임택 글자를 바꾼다
    private void UpdateNameTag(string itemName)
    {
        if (snapPoint == null) return;

        TMP_Text nameText = snapPoint.GetComponentInChildren<TMP_Text>(true);
        if (nameText != null) nameText.text = itemName;
        else Debug.LogWarning($"[ItemInstall] {snapPoint.name} 안에 TextMeshPro 텍스트가 없습니다.");
    }

    private void Warn(string message)
    {
        if (Time.time - lastWarnTime < 2f) return;
        lastWarnTime = Time.time;
        Debug.LogWarning("[ItemInstall] " + message);
    }
}
