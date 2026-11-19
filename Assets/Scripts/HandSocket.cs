using System.Collections.Generic;
using UnityEngine;

public enum HandSide { Right = 0, Left = 1 }

// 손이 잡을 자리. 총·스캐너 같은 물건의 자식으로 두면,
// 그 물건이 켜져 있는 동안 손(FPSHandRig)이 이 자리로 와서 pose 모양으로 쥠
// 물건이 꺼지면(슬롯 바꾸기 등) 손은 화면 아래로 내려가서 숨음
public class HandSocket : MonoBehaviour
{
    [Tooltip("어느 손이 잡을지")]
    public HandSide side = HandSide.Right;
    [Tooltip("손 모양 이름 (FPSHandRig의 Poses 목록에 있는 이름)")]
    public string pose = "PistolGrip";
    [Tooltip("같은 손에 소켓이 여러 개 켜져 있으면 숫자가 큰 쪽을 잡음")]
    public int priority = 0;

    public static readonly List<HandSocket> Active = new List<HandSocket>();

    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() { Active.Remove(this); }

    public static HandSocket Find(HandSide side)
    {
        HandSocket best = null;
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            HandSocket s = Active[i];
            if (s == null) { Active.RemoveAt(i); continue; }
            if (s.side != side || !s.isActiveAndEnabled) continue;
            if (best == null || s.priority > best.priority) best = s;
        }
        return best;
    }

#if UNITY_EDITOR
    // 우클릭 메뉴: 플레이 안 하고도 손이 여기를 잡은 모습 미리 보기 (소켓 위치 맞출 때 사용)
    [ContextMenu("이 소켓에 손 맞춰보기")]
    private void PreviewHand()
    {
        FPSHandRig rig = Object.FindFirstObjectByType<FPSHandRig>(FindObjectsInactive.Include);
        if (rig == null) { Debug.LogWarning("[HandSocket] 씬에 FPSHandRig가 없어요. Tools/손 만들기/손 리그 설치 먼저 실행"); return; }
        rig.EditorSnapToSocket(this);
    }
#endif
}
