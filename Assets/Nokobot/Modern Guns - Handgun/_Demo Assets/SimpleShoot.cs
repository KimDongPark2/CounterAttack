using UnityEngine;

[AddComponentMenu("Nokobot/Modern Guns/Simple Shoot")]
public class SimpleShoot : MonoBehaviour
{
    [Header("Prefab Refrences")]
    public GameObject casingPrefab;
    public GameObject muzzleFlashPrefab;
    [Tooltip("사람을 맞혔을 때 맞은 자리에 터트릴 이펙트")]
    public GameObject hitEffectPrefab;

    [Header("Location Refrences")]
    [SerializeField] private Animator gunAnimator;
    [SerializeField] private Transform barrelLocation;
    [SerializeField] private Transform casingExitLocation;

    [Header("Raycast")]
    [Tooltip("레이를 쏘는 카메라 (비우면 Main Camera)")]
    [SerializeField] private Camera shootCamera;
    [Tooltip("총이 닿는 거리")]
    [SerializeField] private float range = 50f;
    [Tooltip("맞을 수 있는 레이어 (기본: 전부)")]
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("사람으로 인식할 태그")]
    [SerializeField] private string customerTag = "Customer";

    [Header("Settings")]
    [Tooltip("Specify time to destory the casing object")][SerializeField] private float destroyTimer = 2f;
    [Tooltip("Casing Ejection Speed")][SerializeField] private float ejectPower = 150f;

    void Start()
    {
        if (barrelLocation == null)
            barrelLocation = transform;

        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();

        if (shootCamera == null)
            shootCamera = Camera.main;
    }

    // Player_Interaction의 OnShoot에서 부름
    public void Fire()
    {
        // 발사 애니메이션 (반동, 총구 불꽃, 탄피는 애니메이션 이벤트로 나옴)
        if (gunAnimator != null) gunAnimator.SetTrigger("Fire");

        // 화면 가운데(조준점)로 레이 발사. 트리거 상자(ScanPoint 등)는 무시
        Ray ray = new Ray(shootCamera.transform.position, shootCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            return;

        if (!IsCustomer(hit.collider.transform))
            return;

        // 맞은 자리에 이펙트
        if (hitEffectPrefab)
        {
            GameObject fx = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(fx, destroyTimer);
        }

        // 나중에 손님 스크립트에 OnShot() 함수를 만들면 맞았을 때 자동으로 불림 (없으면 무시)
        hit.collider.SendMessageUpwards("OnShot", SendMessageOptions.DontRequireReceiver);
    }

    // 맞은 콜라이더나 그 부모 중에 Customer 태그가 있으면 사람
    private bool IsCustomer(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.CompareTag(customerTag)) return true;
        return false;
    }

    // 발사 애니메이션 이벤트: 총구 불꽃만 (총알은 안 날림)
    void Shoot()
    {
        if (muzzleFlashPrefab)
        {
            GameObject tempFlash = Instantiate(muzzleFlashPrefab, barrelLocation.position, barrelLocation.rotation);
            Destroy(tempFlash, destroyTimer);
        }
    }

    // 발사 애니메이션 이벤트: 탄피 배출 (탄피 프리팹이 비어 있으면 아무것도 안 함)
    void CasingRelease()
    {
        if (!casingExitLocation || !casingPrefab)
        { return; }

        GameObject tempCasing = Instantiate(casingPrefab, casingExitLocation.position, casingExitLocation.rotation);
        Rigidbody rb = tempCasing.GetComponent<Rigidbody>();
        rb.AddExplosionForce(Random.Range(ejectPower * 0.7f, ejectPower), (casingExitLocation.position - casingExitLocation.right * 0.3f - casingExitLocation.up * 0.6f), 1f);
        rb.AddTorque(new Vector3(0, Random.Range(100f, 500f), Random.Range(100f, 1000f)), ForceMode.Impulse);

        Destroy(tempCasing, destroyTimer);
    }
}