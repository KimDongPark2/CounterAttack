using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 결제한 물건을 배달 위치에 하나씩 떨어뜨린다
public class DeliverySystem : MonoBehaviour
{
    [FormerlySerializedAs("DeliveryPoint")] public Transform deliveryPoint;

    [Tooltip("결제 후 첫 물건이 도착하기까지 기다리는 시간(초). 0이면 바로 도착")]
    public float deliveryDelay = 0f;

    [Tooltip("물건 하나씩 떨어지는 간격(초)")]
    public float coolTime = 0.5f;
    public float spreadRadius = 0.15f;
    public bool randomRotation = true;

    public bool IsDelivering { get; private set; }

    public void Deliver(List<ShoppingBags.CartItem> order)
    {
        StartCoroutine(DeliverRoutine(order));
    }

    private IEnumerator DeliverRoutine(List<ShoppingBags.CartItem> order)
    {
        IsDelivering = true;
        if (deliveryDelay > 0f) yield return new WaitForSecondsRealtime(deliveryDelay);

        foreach (var entry in order)
        {
            if (entry.item == null) continue;

            // Item_info.Prefab이 비어 있으면 상품 프리팹 자체를 사용
            GameObject prefab = entry.item.Prefab != null ? entry.item.Prefab : entry.item.gameObject;

            for (int i = 0; i < entry.count; i++)
            {
                SpawnOne(prefab);
                yield return new WaitForSecondsRealtime(coolTime);
            }
        }

        IsDelivering = false;
    }

    private void SpawnOne(GameObject prefab)
    {
        Vector2 offset = Random.insideUnitCircle * spreadRadius;
        Vector3 pos = deliveryPoint.position + new Vector3(offset.x, 0f, offset.y);
        Quaternion rot = randomRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : deliveryPoint.rotation;

        GameObject go = Instantiate(prefab, pos, rot);
        go.SetActive(true);

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
