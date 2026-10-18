using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 손님 한 명. CustomerSpawner가 만들고 Setup을 불러준다 (직접 씬에 둘 필요 없음)
// 흐름: 선반에서 물건 집기 → 계산대 줄 → 계산 → 결제 → 퇴장
//       도둑은 물건 집고 계산 안 하고 뛰어서 나감
[RequireComponent(typeof(NavMeshAgent))]
public class Customer : MonoBehaviour
{
    public enum CustomerType { Normal, Hurry, Thief }
    private enum State { Shopping, Queue, Checkout, Paying, Leaving, Gone }

    // 지금 가게 안에 있는 손님들
    public static readonly List<Customer> Active = new List<Customer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetList() => Active.Clear();

    [Header("표시 (선택)")]
    [Tooltip("인내심이 30% 아래로 떨어지면 켜질 머리 위 화난 표시")]
    public GameObject angryMark;

    [Header("선반 찾기")]
    [Tooltip("선반 칸에서 이 거리 안의 바닥(NavMesh)까지 걸어가서 집음")]
    public float itemReachRadius = 3f;

    [Header("애니메이션 (Animator 파라미터 이름. 컨트롤러는 그대로 씀)")]
    [Tooltip("걸을 때 켜는 Bool")] public string walkBool = "Walk 1";
    [Tooltip("뛸 때 켜는 Bool (도둑, 화나서 나갈 때)")] public string runBool = "Run 1";
    [Tooltip("결제 후 인사할 때 켜는 Bool")] public string greetBool = "Greet";

    [Header("총 맞았을 때 (래그돌)")]
    [Tooltip("맞은 쪽으로 날아가는 힘 (몸 전체)")]
    public float shotForce = 6f;
    [Tooltip("위로 띄우는 힘")]
    public float shotUp = 3f;
    [Tooltip("맞은 부위에 추가로 주는 힘 (거기가 더 휙 꺾임)")]
    public float hitPartForce = 4f;
    [Tooltip("쓰러진 뒤 사라지기까지 시간(초)")]
    public float removeDelay = 5f;
    [Tooltip("래그돌이 없는 모델일 때만: 통째로 넘어지는 힘")]
    public float fallbackPush = 3f;

    public CustomerType Type { get; private set; }
    public float PatienceRatio => maxPatience > 0 ? Mathf.Clamp01(patience / maxPatience) : 1f;

    private NavMeshAgent agent;
    private Animator anim;
    private Rigidbody[] ragdollBodies;   // 뼈에 붙은 Rigidbody들 (Ragdoll Wizard로 만든 것)
    private Collider rootCollider;       // 손님 몸통 캡슐
    private readonly HashSet<string> animParams = new HashSet<string>();

    private CustomerSpawner spawner;
    private CheckoutCounter counter;
    private State state;

    private readonly List<PlacedItem> shoppingList = new List<PlacedItem>();
    private readonly List<GameObject> carried = new List<GameObject>();
    private int shopIndex;
    private bool walking;
    private float actionTimer;

    private float patience, maxPatience, waitTime;
    private bool initialized;   // CustomerSpawner가 Setup을 불렀는지
    private Vector3 faceDir;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
            foreach (AnimatorControllerParameter p in anim.parameters) animParams.Add(p.name);

        // 래그돌: 살아 있는 동안은 뼈 물리를 꺼둠 (애니메이션이 움직이게)
        rootCollider = GetComponent<Collider>();
        List<Rigidbody> bones = new List<Rigidbody>();
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
            if (rb.gameObject != gameObject) bones.Add(rb);
        ragdollBodies = bones.ToArray();
        SetRagdoll(false);
        Active.Add(this);
    }

    public void Setup(CustomerSpawner s, CheckoutCounter c, CustomerType type)
    {
        spawner = s;
        counter = c;
        Type = type;
        initialized = true;

        agent.speed = type == CustomerType.Hurry ? s.hurrySpeed : s.walkSpeed;
        maxPatience = patience = type == CustomerType.Hurry ? s.hurryPatience : s.normalPatience;
        if (angryMark != null) angryMark.SetActive(false);
        if (!agent.isOnNavMesh)
            Debug.LogWarning($"[Customer] '{name}'이(가) NavMesh 위에 없습니다. 바닥에 NavMesh Surface를 Bake했는지, 입구가 바닥 위에 있는지 확인하세요.");

        // 살 물건 고르기 (선반에 놓인 것 중에서, 다른 손님이 찜 안 한 것)
        int want = Random.Range(s.minItems, s.maxItems + 1);
        List<PlacedItem> pool = PlacedItem.GetAvailable();
        for (int i = 0; i < want && pool.Count > 0; i++)
        {
            int k = Random.Range(0, pool.Count);
            pool[k].Reserved = true;
            shoppingList.Add(pool[k]);
            pool.RemoveAt(k);
        }
        for (int i = shoppingList.Count; i < want; i++) Complain(); // 선반에 없는 만큼 불만

        state = State.Shopping;
        shopIndex = 0;
        GoToNextItem();
    }

    private void Start()
    {
        // 씬에 직접 놓아둔 손님은 Setup을 거치지 않아서 움직이지 않음
        if (!initialized)
        {
            Debug.LogWarning($"[Customer] '{name}'은(는) CustomerSpawner가 만든 손님이 아니라서 움직이지 않습니다. " +
                             "씬에 놓아둔 손님은 지우고, 프리팹은 CustomerSpawner에만 넣어주세요.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!initialized || state == State.Gone) return;

        switch (state)
        {
            case State.Shopping: UpdateShopping(); break;

            case State.Queue:
                // 줄 서서 계산대로 가는 동안은 인내심이 안 닳음
                if (counter.IsFront(this) && Arrived())
                {
                    state = State.Checkout;
                    if (!counter.ReadyForCheckout(this)) state = State.Queue;
                }
                break;

            case State.Checkout:
                // 계산대 위에 물건이 올라오기 시작하면 인내심이 닳기 시작
                if (counter.HasItemsOut) UpdatePatience(1f);
                break;

            case State.Paying:
                actionTimer -= Time.deltaTime;
                if (actionTimer <= 0f) Leave(false);
                break;

            case State.Leaving:
                if (Arrived()) Finish();
                break;
        }

        if (state != State.Gone && Arrived()) FaceTarget();
        UpdateAnimation();
    }

    // ---------- 애니메이션 (Bool 켜고 끄기만 함) ----------
    // 움직이면 Walk 1 (뛰는 속도면 Run 1), 서 있으면 전부 끔 = Idle, 결제 후엔 Greet
    private void UpdateAnimation()
    {
        if (anim == null || state == State.Gone) return;

        float v = agent.enabled ? agent.velocity.magnitude : 0f;
        bool moving = v > agent.speed * 0.1f;
        bool running = moving && agent.speed >= spawner.runSpeed - 0.01f;
        bool greeting = state == State.Paying && !moving;

        SetAnimBool(walkBool, moving && !running);
        SetAnimBool(runBool, running);
        SetAnimBool(greetBool, greeting);
    }

    private void SetAnimBool(string param, bool value)
    {
        if (!string.IsNullOrEmpty(param) && animParams.Contains(param)) anim.SetBool(param, value);
    }

    // ---------- 쇼핑 ----------
    private void GoToNextItem()
    {
        while (shopIndex < shoppingList.Count && shoppingList[shopIndex] == null)
        {
            Complain(); // 가는 사이에 물건이 없어짐
            shopIndex++;
        }
        if (shopIndex >= shoppingList.Count)
        {
            FinishShopping();
            return;
        }

        Transform item = shoppingList[shopIndex].transform;
        Vector3 target = item.position;
        if (NavMesh.SamplePosition(item.position, out NavMeshHit hit, itemReachRadius, NavMesh.AllAreas))
            target = hit.position;

        Vector3 d = item.position - target;
        d.y = 0f;
        faceDir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
        MoveTo(target);
    }

    private void UpdateShopping()
    {
        if (shopIndex >= shoppingList.Count)
        {
            FinishShopping();
            return;
        }

        if (shoppingList[shopIndex] == null)
        {
            Complain();
            shopIndex++;
            GoToNextItem();
            return;
        }

        if (walking)
        {
            if (!Arrived()) return;
            walking = false;
            actionTimer = spawner.pickTime; // 잠깐 고르는 시간
        }

        actionTimer -= Time.deltaTime;
        if (actionTimer > 0f) return;

        TakeItem(shoppingList[shopIndex]);
        shopIndex++;
        GoToNextItem();
    }

    private void TakeItem(PlacedItem p)
    {
        if (p == null) { Complain(); return; }

        GameObject obj = p.gameObject;
        p.Release();   // 선반 칸 비우고 이름표 지우기
        Destroy(p);
        obj.SetActive(false);
        obj.transform.SetParent(transform, true);
        carried.Add(obj);
    }

    private void FinishShopping()
    {
        if (carried.Count == 0)
        {
            Leave(false); // 아무것도 못 삼
            return;
        }
        if (Type == CustomerType.Thief)
        {
            Leave(true); // 계산 안 하고 도망
            return;
        }
        state = State.Queue;
        counter.Join(this);
    }

    private void Complain()
    {
        if (Type != CustomerType.Thief) StoreStats.AddMissing(spawner.missingPoint);
    }

    // ---------- 줄 / 계산 ----------
    public void SetQueueSpot(Vector3 pos, Vector3 forward)
    {
        if (state != State.Queue && state != State.Checkout) return;
        faceDir = forward;
        MoveTo(pos);
    }

    public List<GameObject> HandOverItems()
    {
        List<GameObject> list = new List<GameObject>(carried);
        carried.Clear();
        return list;
    }

    private void UpdatePatience(float rate)
    {
        waitTime += Time.deltaTime;
        patience -= Time.deltaTime * rate;

        if (angryMark != null && PatienceRatio < 0.3f) angryMark.SetActive(true);
        if (patience <= 0f) GetAngry();
    }

    private void GetAngry()
    {
        StoreStats.AddAngry(spawner.angryPoint);
        if (spawner.state != null) spawner.state.badProcess++;

        counter.Remove(this);
        DropCarried();
        Leave(true);
    }

    // 계산대가 다 찍고 나서 부름
    public void Pay(int total)
    {
        bool fast = waitTime <= maxPatience * 0.5f;

        if (spawner.state != null)
        {
            spawner.state.Earn(total);
            spawner.state.totalSales += total;
            if (fast) spawner.state.goodProcess++;
        }
        StoreStats.AddServed(total, fast ? spawner.fastPoint : spawner.slowPoint, fast);

        if (angryMark != null) angryMark.SetActive(false);
        if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        walking = false;

        state = State.Paying;
        actionTimer = spawner.greetTime; // 결제 후 잠깐 멈췄다가 나감
    }

    // ---------- 퇴장 ----------
    private void Leave(bool run)
    {
        ReleaseReservations();
        state = State.Leaving;
        if (run) agent.speed = spawner.runSpeed;
        Vector3 exit = spawner.exit != null ? spawner.exit.position : transform.position;
        faceDir = Vector3.zero;
        MoveTo(exit);
    }

    private void Finish()
    {
        if (Type == CustomerType.Thief && carried.Count > 0)
            StoreStats.AddThiefEscaped(spawner.thiefEscapePoint);
        state = State.Gone;
        Destroy(gameObject); // 들고 있던(훔친) 물건도 같이 사라짐
    }

    // ---------- 총 맞음 (SimpleShoot가 SendMessageUpwards로 부름) ----------
    public void OnShot(RaycastHit hit)
    {
        if (state == State.Gone) return;

        if (Type == CustomerType.Thief) StoreStats.AddThiefCaught(spawner.thiefShotPoint);
        else
        {
            StoreStats.AddWrongShot(spawner.wrongShotPoint);
            if (spawner.state != null) spawner.state.badProcess++;
        }

        counter.Remove(this);
        ReleaseReservations();
        DropCarried(); // 들고 있던 물건은 바닥에 떨어뜨림 (다시 주워서 진열 가능)

        state = State.Gone;
        Active.Remove(this);
        if (angryMark != null) angryMark.SetActive(false);
        if (agent != null) agent.enabled = false;

        // 총 쏜 방향 (수평)
        Vector3 from = Camera.main != null ? Camera.main.transform.position : transform.position - transform.forward;
        Vector3 dir = hit.point - from;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

        if (ragdollBodies.Length > 0)
        {
            // 힘 풀리면서 쓰러지기: 애니메이션 끄고 뼈 물리 켜기
            if (anim != null) anim.enabled = false;
            if (rootCollider != null) rootCollider.enabled = false;
            SetRagdoll(true);

            // 몸 전체를 맞은 방향으로 날리고, 맞은 부위는 더 세게 (흐물거리며 날아감)
            Vector3 launch = dir * shotForce + Vector3.up * shotUp;
            Rigidbody nearest = ragdollBodies[0];
            float best = float.MaxValue;
            foreach (Rigidbody rb in ragdollBodies)
            {
                rb.AddForce(launch, ForceMode.VelocityChange);
                float d = (rb.worldCenterOfMass - hit.point).sqrMagnitude;
                if (d < best) { best = d; nearest = rb; }
            }
            nearest.AddForce(dir * hitPartForce, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogWarning($"[Customer] '{name}'에 래그돌이 없어서 통째로 넘어집니다. 손님 프리팹에 Ragdoll을 만들어주세요.", this);
            // 래그돌이 없는 모델: 몸 전체가 맞은 방향으로 넘어짐
            if (anim != null) anim.enabled = false;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForceAtPosition(dir * fallbackPush, hit.point, ForceMode.VelocityChange);
        }

        Destroy(gameObject, removeDelay);
    }

    // 뼈 물리 켜기/끄기
    private void SetRagdoll(bool on)
    {
        foreach (Rigidbody rb in ragdollBodies)
        {
            rb.isKinematic = !on;
            rb.useGravity = on;
            if (on)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    // 들고 있던 물건을 F키로 주울 수 있는 물건으로 바닥에 떨어뜨림
    private void DropCarried()
    {
        Collider body = GetComponentInChildren<Collider>();
        Vector3 center = body != null ? body.bounds.center : transform.position + Vector3.up;

        foreach (GameObject obj in carried)
        {
            if (obj == null) continue;
            obj.transform.SetParent(null, true);
            obj.transform.position = center + transform.forward * 0.5f + Random.insideUnitSphere * 0.2f;
            obj.SetActive(true);
            obj.tag = "Item";

            foreach (MeshCollider mc in obj.GetComponentsInChildren<MeshCollider>()) mc.convex = true;
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = false;
        }
        carried.Clear();
    }

    private void ReleaseReservations()
    {
        foreach (PlacedItem p in shoppingList)
            if (p != null) p.Reserved = false;
        shoppingList.Clear();
    }

    private void OnDestroy()
    {
        Active.Remove(this);
        ReleaseReservations();
        if (counter != null) counter.Remove(this);
    }

    // ---------- 이동 도우미 ----------
    private void MoveTo(Vector3 pos)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.SetDestination(pos);
        walking = true;
    }

    private bool Arrived()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return true;
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true; // 갈 수 없는 곳이면 멈춰 있지 않게
        return agent.remainingDistance <= agent.stoppingDistance + Mathf.Max(0.2f, agent.radius);
    }

    private void FaceTarget()
    {
        if (faceDir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(faceDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 8f);
    }
}