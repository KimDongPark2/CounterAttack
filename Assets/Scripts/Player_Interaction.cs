using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Interaction : MonoBehaviour
{
    private GameObject curTarget;
    [Header("Interaction Settings")]
    public Camera playerCamera;
    public float interactRange = 10f;
    [Header("UI")]
    public TextMeshProUGUI noticeText;


    private void Start()
    {
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
                noticeText.gameObject.SetActive(true);
                noticeText.text = $"(F) {hitInfo.collider.gameObject.name} 집기";
                return;
            }
        }
        curTarget = null;
        noticeText.gameObject.SetActive(false);
        noticeText.text = "";
    }
    public void OnInteract(InputValue value)
    {
        if (value.isPressed && curTarget != null)
        {
            Debug.Log($"{curTarget.name}을(를) 주웠습니다.");
            Destroy( curTarget );
            curTarget = null;
        }
    }
}
