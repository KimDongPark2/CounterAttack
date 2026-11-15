using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 계산대: 손님 줄 관리 + 계산대 위 물건 스캔 + 결제
// 계산대 근처 빈 오브젝트에 붙이고, 줄 서는 자리와 물건 놓는 자리를 연결한다
public class CheckoutCounter : MonoBehaviour
{
    public static CheckoutCounter Instance { get; private set; }

    // ---------- UI(CounterHUD)가 읽는 값 ----------
    public Customer Front => queue.Count > 0 ? queue[0] : null;  // 맨 앞 손님 (계산 중이면 그 손님)
    public int Total => total;                                  // 지금까지 찍은 합계
    public int OrderTotal { get; private set; }                 // 손님이 가져온 물건 전체 금액 (도착하자마자 계산)
    public int ItemCount { get; private set; }                  // 지금 손님이 가져온 물건 수
    public int ScannedCount { get; private set; }               // 그중 찍은 수
    public bool IsCheckingOut => current != null;
    public bool HasItemsOut => current != null && placedCount > 0;  // 계산대 위에 물건이 올라오기 시작했는지

    [Header("줄 서는 자리 (0번이 계산대 바로 앞)")]
    [Tooltip("빈 오브젝트들. 파란 화살표(앞)가 계산대를 보게 돌려두기")]
    public Transform[] queueSpots;
    [Tooltip("자리보다 손님이 많을 때 마지막 자리 뒤로 늘어서는 간격")]
    public float extraSpacing = 1f;

    [Header("계산대 위 물건 놓는 자리")]
    public Transform[] itemSpots;

    [Header("물건 올려놓기")]
    [Tooltip("손님이 물건을 하나씩 올려놓는 간격(초). 첫 물건도 이만큼 기다렸다가 나옴")]
    public float placeInterval = 0.8f;
    [Tooltip("자리보다 이만큼 위에서 생겨서 툭 떨어짐 (물리로 떨어짐)")]
    public float dropHeight = 0.3f;
    [Tooltip("켜면 지금 올라온 물건을 찍어야 다음 물건이 올라옴")]
    public bool oneAtATime = false;
    [Tooltip("물건이 뿅 하고 커지며 나타나는 시간 (0이면 연출 없음)")]
    public float popDuration = 0.15f;

    [Header("판매 가격")]
    [Tooltip("물건에 판매가(SellPrice)를 안 적었으면 주문 가격 × 이 배율로 팜")]
    public float sellMarkup = 1.5f;
    [Tooltip("판매가를 이 단위로 반올림 (10이면 450원, 900원 …)")]
    public int roundTo = 10;

    [Header("스캔")]
    public float scanRange = 4f;
    [Tooltip("켜면 물건을 조준해야 찍힘. 끄면 클릭할 때마다 다음 물건이 찍힘")]
    public bool requireAim = true;

    [Header("표시 (선택)")]
    [Tooltip("계산 중인 합계를 보여줄 글자")]
    public TMP_Text totalText;

    private readonly List<Customer> queue = new List<Customer>();
    private readonly List<CheckoutItem> pending = new List<CheckoutItem>();  // 계산대 위에 올라온 물건
    private readonly List<GameObject> waiting = new List<GameObject>();      // 아직 손님이 들고 있는 물건
    private Customer current;
    private int total;
    private int placedCount;
    private Coroutine placing;

    private void Awake()
    {
        Instance = this;
        UpdateTotalText();
    }

    // ---------- 줄 ----------
    public void Join(Customer c)
    {
        if (c == null || queue.Contains(c)) return;
        queue.Add(c);
        RefreshQueue();
    }

    // 화나서 나가거나 총 맞으면 줄에서 빠짐
    public void Remove(Customer c)
    {
        if (c == null) return;
        if (c == current) CancelCheckout();
        if (queue.Remove(c)) RefreshQueue();
    }

    public bool IsFront(Customer c) => queue.Count > 0 && queue[0] == c;

    // 손님한테 받는 가격: 물건에 적힌 판매가, 없으면 주문 가격 × 판매 배율
    public int SellPriceOf(Item_info info)
    {
        if (info == null) return 0;
        if (info.SellPrice > 0) return info.SellPrice;
        float raw = info.Price * sellMarkup;
        int unit = Mathf.Max(1, roundTo);
        return Mathf.RoundToInt(raw / unit) * unit;
    }

    private void RefreshQueue()
    {
        for (int i = 0; i < queue.Count; i++)
        {
            GetSpot(i, out Vector3 pos, out Vector3 forward);
            queue[i].SetQueueSpot(pos, forward);
        }
    }

    private void GetSpot(int i, out Vector3 pos, out Vector3 forward)
    {
        if (queueSpots == null || queueSpots.Length == 0)
        {
            pos = transform.position; forward = transform.forward;
            return;
        }
        int n = queueSpots.Length;
        if (i < n)
        {
            pos = queueSpots[i].position; forward = queueSpots[i].forward;
            return;
        }
        Transform last = queueSpots[n - 1];
        Vector3 back = n >= 2 ? (last.position - queueSpots[n - 2].position).normalized : -last.forward;
        pos = last.position + back * extraSpacing * (i - n + 1);
        forward = last.forward;
    }

    // ---------- 계산 ----------
    // 맨 앞 손님이 계산대 앞에 도착하면 부름: 물건을 하나씩 계산대 위에 올려놓기 시작
    public bool ReadyForCheckout(Customer c)
    {
        if (current != null || !IsFront(c)) return false;

        current = c;
        total = 0;
        placedCount = 0;
        waiting.Clear();
        foreach (GameObject obj in c.HandOverItems())
            if (obj != null) waiting.Add(obj);
        ItemCount = waiting.Count;
        ScannedCount = 0;
        OrderTotal = 0;
        foreach (GameObject obj in waiting)
        {
            Item_info info = obj.GetComponentInChildren<Item_info>(true);
            OrderTotal += SellPriceOf(info);
        }

        if (waiting.Count == 0)
        {
            Complete();
            return true;
        }

        placing = StartCoroutine(PlaceRoutine());
        UpdateTotalText();
        return true;
    }

    // placeInterval마다 하나씩 올려놓기 (oneAtATime이면 하나만 올리고 멈춤 → 찍으면 다음)
    private IEnumerator PlaceRoutine()
    {
        while (waiting.Count > 0)
        {
            yield return new WaitForSeconds(placeInterval);
            PlaceNext();
            if (oneAtATime) break;
        }
        placing = null;
    }

    private void PlaceNext()
    {
        if (waiting.Count == 0) return;
        GameObject obj = waiting[0];
        waiting.RemoveAt(0);
        if (obj == null) return;

        GetItemSpot(oneAtATime ? 0 : placedCount, out Vector3 pos, out Quaternion rot);
        placedCount++;
        obj.transform.SetParent(null, true);
        obj.transform.SetPositionAndRotation(pos + Vector3.up * dropHeight, rot);
        obj.SetActive(true);

        Item_info info = obj.GetComponentInChildren<Item_info>(true);
        CheckoutItem ci = obj.AddComponent<CheckoutItem>();
        ci.Prepare(SellPriceOf(info), info != null ? info.Name : obj.name);
        pending.Add(ci);
        EnablePhysics(obj);   // Prepare 다음에 (물건이 고정되지 않고 떨어지게)

        SoundManager.PlayAt(Sfx.CheckoutPlace, obj.transform.position);
        if (popDuration > 0f) StartCoroutine(Pop(obj.transform));
        UpdateTotalText();
    }

    // Rigidbody를 살려서 계산대 위로 떨어지게 (선반에 설치됐던 물건은 Kinematic이라 풀어줌)
    // Rigidbody가 없는 물건이면 하나 붙여줌
    private void EnablePhysics(GameObject obj)
    {
        foreach (MeshCollider mc in obj.GetComponentsInChildren<MeshCollider>(true))
            mc.convex = true; // 움직이는 물체는 볼록 콜라이더만 가능

        Rigidbody[] bodies = obj.GetComponentsInChildren<Rigidbody>(true);
        if (bodies.Length == 0) bodies = new[] { obj.AddComponent<Rigidbody>() };

        foreach (Rigidbody rb in bodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
    }

    // 작게 나타났다가 원래 크기로 커지는 연출
    private IEnumerator Pop(Transform t)
    {
        Vector3 full = t.localScale;
        float time = 0f;
        while (time < popDuration)
        {
            if (t == null) yield break; // 그새 찍혀서 없어짐
            time += Time.deltaTime;
            t.localScale = full * Mathf.SmoothStep(0.2f, 1f, time / popDuration);
            yield return null;
        }
        if (t != null) t.localScale = full;
    }

    private void GetItemSpot(int i, out Vector3 pos, out Quaternion rot)
    {
        if (itemSpots == null || itemSpots.Length == 0)
        {
            pos = transform.position + transform.right * (i * 0.3f);
            rot = transform.rotation;
            return;
        }
        Transform s = itemSpots[i % itemSpots.Length];
        pos = s.position + Vector3.up * (0.15f * (i / itemSpots.Length)); // 자리가 모자라면 위로 쌓기
        rot = s.rotation;
    }

    // Player_Interaction의 OnScan에서 부름. 찍었으면 true
    public bool TryScan(Camera cam)
    {
        if (current == null || pending.Count == 0) return false;

        CheckoutItem target = null;
        // Ignore Raycast 레이어(투명벽 등)는 통과해서 물건만 찍음
        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit,
                                           scanRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            target = hit.collider.GetComponentInParent<CheckoutItem>();
        }

        if (target == null || !pending.Contains(target))
        {
            if (requireAim) return false;
            target = pending[0];
        }

        Scan(target);
        return true;
    }

    private void Scan(CheckoutItem item)
    {
        total += item.price;
        ScannedCount++;
        pending.Remove(item);
        Destroy(item.gameObject);
        SoundManager.Play(Sfx.ScanBeep);

        // 하나씩 모드: 찍었으면 다음 물건 올리기
        if (oneAtATime && waiting.Count > 0 && placing == null)
            placing = StartCoroutine(PlaceRoutine());

        // 올라온 것도, 남은 것도 없으면 계산 끝
        if (pending.Count == 0 && waiting.Count == 0) Complete();
        else UpdateTotalText();
    }

    private void Complete()
    {
        Customer c = current;
        int paid = total;
        current = null;
        total = 0;
        ItemCount = ScannedCount = OrderTotal = 0;

        queue.Remove(c);
        RefreshQueue();
        SoundManager.Play(Sfx.Pay);
        UpdateTotalText();

        if (c != null) c.Pay(paid);
    }

    // 계산 도중에 손님이 가버리면: 남은 물건은 계산대 위에 그대로, 다시 집을 수 있게
    private void CancelCheckout()
    {
        if (placing != null)
        {
            StopCoroutine(placing);
            placing = null;
        }

        foreach (CheckoutItem ci in pending)
            if (ci != null) ci.RestoreAsPickup();
        pending.Clear();

        // 아직 안 올린 물건도 계산대 위에 내려놓기
        foreach (GameObject obj in waiting)
        {
            if (obj == null) continue;
            GetItemSpot(placedCount++, out Vector3 pos, out Quaternion rot);
            obj.transform.SetParent(null, true);
            obj.transform.SetPositionAndRotation(pos + Vector3.up * dropHeight, rot);
            obj.SetActive(true);
            EnablePhysics(obj);
        }
        waiting.Clear();

        ItemCount = ScannedCount = OrderTotal = 0;
        current = null;
        total = 0;
        UpdateTotalText();
    }

    private void UpdateTotalText()
    {
        if (totalText == null) return;
        totalText.text = current != null
            ? $"합계 {Player_State.Won(OrderTotal)}  (남은 물건 {pending.Count + waiting.Count}개)"
            : "";
    }
}
