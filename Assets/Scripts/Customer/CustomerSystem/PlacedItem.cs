using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 선반 칸(Pos)에 설치된 물건에 자동으로 붙는 표시 (직접 붙일 필요 없음)
//  - 칸 하나에 물건 하나만 놓이도록, 그 칸을 "사용 중"으로 기록
//  - 물건이 사라지면(주워가거나 파괴되면) 칸을 비우고 이름표 글자도 지움
//  - 손님이 살 물건을 찾을 때 이 기록을 봄
public class PlacedItem : MonoBehaviour
{
    private static readonly Dictionary<Transform, PlacedItem> occupied = new Dictionary<Transform, PlacedItem>();

    // 플레이를 다시 시작할 때 이전 기록이 남지 않게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRecords() => occupied.Clear();

    // 손님이 사려고 찜해둔 물건 (다른 손님이 같은 물건을 노리지 않게)
    public bool Reserved { get; set; }
    public string ItemName { get; private set; }

    private Transform slot;
    private TMP_Text nameTag;

    // 이 칸에 이미 물건이 있는지
    public static bool IsOccupied(Transform slotPoint)
    {
        if (slotPoint == null) return false;
        if (occupied.TryGetValue(slotPoint, out PlacedItem item) && item != null) return true;
        occupied.Remove(slotPoint);
        return false;
    }

    // 선반에 놓여 있고, 아직 아무 손님도 찜하지 않은 물건들
    public static List<PlacedItem> GetAvailable()
    {
        List<PlacedItem> list = new List<PlacedItem>();
        foreach (PlacedItem p in occupied.Values)
            if (p != null && !p.Reserved) list.Add(p);
        return list;
    }

    // 설치할 때 Player_ItemInstall이 부름
    public void Place(Transform slotPoint, string itemName)
    {
        Release();
        slot = slotPoint;
        ItemName = itemName;
        if (slot == null) return;

        occupied[slot] = this;
        nameTag = slot.GetComponentInChildren<TMP_Text>(true);
        if (nameTag != null) nameTag.text = itemName;
    }

    // 칸 비우기 + 이름표 지우기 (손님이 집어갈 때도 부름)
    public void Release()
    {
        if (slot != null && occupied.TryGetValue(slot, out PlacedItem item) && item == this)
            occupied.Remove(slot);
        if (nameTag != null) nameTag.text = "";
        slot = null;
        nameTag = null;
    }

    // 주울 때 Destroy되면 자동으로 호출됨
    private void OnDestroy() => Release();
}
