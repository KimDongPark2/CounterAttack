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

    [Header("Rotation")]
    [Tooltip("처음 미리보기 방향 보정값. 물건이 뒤돌아 있으면 180, 옆을 보고 있으면 90 또는 -90")]
    public float rotationOffsetY = 180f;
    [Tooltip("선반이 아닌 바닥에 놓을 때, 내가 바라보는 방향 기준으로 놓기 (90도 단위로 반듯하게)")]
    public bool alignToView = true;

    [Header("Install")]
    [Tooltip("설치한 물건의 물리를 고정해서, 부딪혀도 밀리거나 떨어지지 않게")]
    public bool freezeInstalled = true;

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

        // 보정값 + Q/E로 돌린 만큼
        Quaternion spin = Quaternion.Euler(0, rotationOffsetY + rotationY, 0);
        installRotation = ViewRotation() * spin;
        installPosition = ray.origin + ray.direction * installRange;

        if (Physics.Raycast(ray, out RaycastHit hit, installRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            installPosition = hit.point;

            if (hit.collider.CompareTag("InstallAvailable"))
            {
                Transform nearest = FindSnapPoint(hit.collider.transform, hit.point, out bool hasSlots);

                if (!hasSlots)
                {
                    // 칸(Pos)이 없는 곳(바닥, 데크)은 자유롭게 놓기
                    canInstall = true;
                }
                else if (nearest == null)
                {
                    // 선반인데 칸에서 벗어난 곳을 보고 있음 → 칸에만 놓을 수 있음
                    canInstall = false;
                }
                else
                {
                    // 칸에 맞춰 미리보기를 보여주고, 이미 물건이 있으면 빨간색
                    snapPoint = nearest;
                    installPosition = nearest.position;
                    if (snapRotation) installRotation = nearest.rotation * spin;
                    canInstall = !PlacedItem.IsOccupied(nearest);
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

    // 바닥에 놓을 때 기준 방향: 내가 보는 방향을 90도 단위로 맞춘 것
    private Quaternion ViewRotation()
    {
        if (!alignToView) return Quaternion.identity;
        float yaw = Mathf.Round(playerCamera.transform.eulerAngles.y / 90f) * 90f;
        return Quaternion.Euler(0, yaw, 0);
    }

    // 맞은 오브젝트 안에서 이름이 Pos로 시작하는 자리 중 가장 가까운 곳
    // hasSlots: 그 오브젝트에 Pos가 하나라도 있는지 (= 칸이 있는 선반인지)
    private Transform FindSnapPoint(Transform root, Vector3 point, out bool hasSlots)
    {
        hasSlots = false;
        Transform best = null;
        float bestDist = snapRadius;
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            if (!t.name.StartsWith("Pos")) continue;
            hasSlots = true;
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
        SoundManager.PlayAt(Sfx.ItemPlace, installPosition);
        installed.tag = "Item";
        installed.SetActive(true);

        // 물리 고정: 그 자리에 딱 붙어 있고, 콜라이더는 남아 있어서 다시 주울 수 있음
        if (freezeInstalled)
        {
            foreach (Rigidbody rb in installed.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
        }

        // 선반 칸에 놓았으면: 칸을 사용 중으로 기록 + 이름표에 이름 쓰기
        // (이 물건을 주워가면 PlacedItem이 알아서 칸을 비우고 이름표를 지움)
        if (snapPoint != null)
        {
            PlacedItem placed = installed.GetComponent<PlacedItem>();
            if (placed == null) placed = installed.AddComponent<PlacedItem>();
            placed.Place(snapPoint, slot.currentItemName);
        }

        inventory.ClearSlotUI(state.handIndex - 1);
    }

    private void Warn(string message)
    {
        if (Time.unscaledTime - lastWarnTime < 2f) return;
        lastWarnTime = Time.unscaledTime;
        Debug.LogWarning("[ItemInstall] " + message);
    }
}
