using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliverySystem : MonoBehaviour
{
    public Transform DeliveryPoint;
    public float coolTime = 0.5f;
    public float spreadRadius = 0.15f;
    public bool randomRotation = true;

    public bool isDelivery = false;


    public void Deliver(List<ShoppingBags.CartItem> order)
    {
        StartCoroutine(DeliverRoutine(order));
    }

    IEnumerator DeliverRoutine(List<ShoppingBags.CartItem> order)
    {
        isDelivery = true;

        foreach (var entry in order)
        {
            if (entry.item == null || entry.item.Prefab == null)
            {
                Debug.LogWarning($"[배송] {entry.item?.Name} 프리팹 없음");
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                SpawnOne(entry.item);
                yield return new WaitForSecondsRealtime(coolTime);
            }
        }

        isDelivery = false;
        Debug.Log("[배송] 완료");
    }

    void SpawnOne(Item_info item)
    {
        Vector2 offset = Random.insideUnitCircle * spreadRadius;
        Vector3 pos = DeliveryPoint.position + new Vector3(offset.x, 0f, offset.y);

        Quaternion rot = randomRotation
            ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            : DeliveryPoint.rotation;

        var go = Instantiate(item.Prefab, pos, rot);

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
