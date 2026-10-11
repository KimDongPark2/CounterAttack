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

    [Header("총 맞았을 때 날아가기")]
    public float flyForce = 8f;
    public float flyUp = 5f;
    public float removeDelay = 2.5f;

    public CustomerType Type { get; private set; }
    public float PatienceRatio => maxPatience > 0 ? Mathf.Clamp01(patience / maxPatience) : 1f;

    private NavMeshAgent agent;
    private Animator anim;
    private bool hasSpeedParam, hasGreetParam;

    private CustomerSpawner spawner;
    private CheckoutCounter counter;
    private State state;

    private readonly List<PlacedItem> shoppingList = new List<PlacedItem>();
    private readonly List<GameObject> carried = new List<GameObject>();
    private int shopIndex;
    private bool walking;
    private float actionTimer;

    private float patience, maxPatience, waitTime;
    private Vector3 faceDir;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false; // 이동은 NavMeshAgent가 함
            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeedParam = true;
                if (p.name == "Greet" && p.type == AnimatorControllerParameterType.Trigger) hasGreetParam = true;
            }
        }
        Active.Add(this);
    }

    public void Setup(CustomerSpawner s, CheckoutCounter c, CustomerType type)
    {
        spawner = s;
        counter = c;
        Type = type;

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

    private void Update()
    {
        if (state == State.Gone) return;

        if (hasSpeedParam)
            anim.SetFloat("Speed", agent.enabled ? agent.velocity.magnitude : 0f);

        switch (state)
        {
            case State.Shopping: UpdateShopping(); break;

            case State.Queue:
                UpdatePatience(1f);
                if (state == State.Queue && counter.IsFront(this) && Arrived() && counter.ReadyForCheckout(this))
                    state = State.Checkout;
                break;

            case State.Checkout:
                UpdatePatience(0.5f); // 계산 중에는 인내심이 천천히 닳음
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
        if (shopIndex < shoppingList.Count && shoppingList[shopIndex] == null)
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
        actionTimer = hasGreetParam ? spawner.greetTime : 0.2f;
        if (hasGreetParam) anim.SetTrigger("Greet");
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
    public void OnShot()
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
        if (anim != null) anim.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 from = Camera.main != null ? Camera.main.transform.position : transform.position - transform.forward;
        Vector3 dir = transform.position - from;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;
        rb.AddForce(dir * flyForce + Vector3.up * flyUp, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);

        Destroy(gameObject, removeDelay);
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
