using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 계산대: 손님 줄 관리 + 계산대 위 물건 스캔 + 결제
// 계산대 근처 빈 오브젝트에 붙이고, 줄 서는 자리와 물건 놓는 자리를 연결한다
public class CheckoutCounter : MonoBehaviour
{
    public static CheckoutCounter Instance { get; private set; }

    [Header("줄 서는 자리 (0번이 계산대 바로 앞)")]
    [Tooltip("빈 오브젝트들. 파란 화살표(앞)가 계산대를 보게 돌려두기")]
    public Transform[] queueSpots;
    [Tooltip("자리보다 손님이 많을 때 마지막 자리 뒤로 늘어서는 간격")]
    public float extraSpacing = 1f;

    [Header("계산대 위 물건 놓는 자리")]
    public Transform[] itemSpots;

    [Header("스캔")]
    public float scanRange = 4f;
    [Tooltip("켜면 물건을 조준해야 찍힘. 끄면 클릭할 때마다 다음 물건이 찍힘")]
    public bool requireAim = true;
    public AudioSource audioSource;
    public AudioClip scanSound;
    public AudioClip paySound;

    [Header("표시 (선택)")]
    [Tooltip("계산 중인 합계를 보여줄 글자")]
    public TMP_Text totalText;

    private readonly List<Customer> queue = new List<Customer>();
    private readonly List<CheckoutItem> pending = new List<CheckoutItem>();
    private Customer current;
    private int total;

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
    // 맨 앞 손님이 계산대 앞에 도착하면 부름: 물건을 계산대 위에 올려놓음
    public bool ReadyForCheckout(Customer c)
    {
        if (current != null || !IsFront(c)) return false;

        current = c;
        total = 0;
        List<GameObject> items = c.HandOverItems();
        for (int i = 0; i < items.Count; i++)
        {
            GameObject obj = items[i];
            if (obj == null) continue;

            GetItemSpot(i, out Vector3 pos, out Quaternion rot);
            obj.transform.SetParent(null, true);
            obj.transform.SetPositionAndRotation(pos, rot);
            obj.SetActive(true);

            Item_info info = obj.GetComponentInChildren<Item_info>(true);
            CheckoutItem ci = obj.AddComponent<CheckoutItem>();
            ci.Prepare(info != null ? info.Price : 0, info != null ? info.Name : obj.name);
            pending.Add(ci);
        }

        if (pending.Count == 0) Complete();
        else UpdateTotalText();
        return true;
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
        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit,
                                           scanRange, ~0, QueryTriggerInteraction.Ignore))
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
        pending.Remove(item);
        Destroy(item.gameObject);
        PlaySound(scanSound);

        if (pending.Count == 0) Complete();
        else UpdateTotalText();
    }

    private void Complete()
    {
        Customer c = current;
        int paid = total;
        current = null;
        total = 0;

        queue.Remove(c);
        RefreshQueue();
        PlaySound(paySound);
        UpdateTotalText();

        if (c != null) c.Pay(paid);
    }

    // 계산 도중에 손님이 가버리면: 남은 물건은 계산대 위에 그대로, 다시 집을 수 있게
    private void CancelCheckout()
    {
        foreach (CheckoutItem ci in pending)
            if (ci != null) ci.RestoreAsPickup();
        pending.Clear();
        current = null;
        total = 0;
        UpdateTotalText();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void UpdateTotalText()
    {
        if (totalText == null) return;
        totalText.text = current != null
            ? $"합계 {Player_State.Won(total)}  (남은 물건 {pending.Count}개)"
            : "";
    }
}
