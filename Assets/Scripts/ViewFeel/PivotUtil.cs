using UnityEngine;

// 피벗(빈 오브젝트) 만들기 · 찾기 도우미 (WeaponFeel · GunReload가 같이 씀)
//
// 주의: 씬의 Hand Gun 칸에는 총 프리팹의 "루트"가 아니라 그 안의 자식(Animator가 붙은 것)이 들어 있고,
//       루트는 크기(Scale)가 5. 그래서 피벗을 Hand Gun의 부모 아래에 만들면 모든 움직임이 5배가 됨
//       → 피벗은 항상 카메라 바로 아래(크기 1)에 만들고, 프리팹 루트를 통째로 옮김
//   Camera
//    └ WeaponFeelPivot   (흔들림 · 반동)
//       └ GunReloadPivot (재장전)
//          └ 총 프리팹 루트 (위치 · 회전 · 크기 그대로)
//             └ Hand Gun (Animator)
public static class PivotUtil
{
    // child를 품고 있는 parent의 "바로 아래 자식"을 찾기 (child가 parent 밑에 없으면 null)
    public static Transform TopUnder(Transform parent, Transform child)
    {
        if (parent == null) return null;
        for (Transform t = child; t != null; t = t.parent)
            if (t.parent == parent) return t;
        return null;
    }

    // parent 바로 아래에 이름이 name인 피벗을 찾거나, 없으면 만들기 (위치 0 · 회전 0 · 크기 1)
    public static Transform GetOrCreate(Transform parent, string name, int layer)
    {
        Transform p = parent.Find(name);
        if (p != null) return p;
        p = new GameObject(name).transform;
        p.gameObject.layer = layer;
        p.SetParent(parent, false);
        p.localPosition = Vector3.zero;
        p.localRotation = Quaternion.identity;
        p.localScale = Vector3.one;
        return p;
    }

    // target을 container의 자식 피벗(pivotName) 안으로 넣기
    //   target이 이미 그 피벗 안에 있으면 그대로. 피벗의 로컬 값이 0이라 보이는 자리는 안 바뀜
    public static Transform WrapUnder(Transform container, Transform target, string pivotName)
    {
        Transform top = TopUnder(container, target);
        if (top == null) return null;
        if (top.name == pivotName) return top;
        Transform pivot = GetOrCreate(container, pivotName, top.gameObject.layer);
        top.SetParent(pivot, false);
        return pivot;
    }

    // obj 그림(Renderer)들의 가운데를 space 기준 좌표로 (못 찾으면 obj 위치)
    // 메시 자체의 상자 8개 꼭짓점으로 계산해서 회전해 있어도 정확함
    public static Vector3 CenterIn(Transform space, Transform obj)
    {
        return BoundsIn(space, obj, out Bounds b) ? b.center : space.InverseTransformPoint(obj.position);
    }

    // obj 그림(Renderer)들을 감싸는 상자를 space 기준 좌표로 (그림이 없으면 false)
    public static bool BoundsIn(Transform space, Transform obj, out Bounds b)
    {
        bool any = false;
        b = new Bounds();
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;   // 총구 불꽃 같은 건 빼기
            Bounds lb = r.localBounds;
            if (lb.size == Vector3.zero) continue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return any;
    }

    // 보통 FPS 게임에서 총이 카메라에서 떨어진 거리 (m)
    public const float TypicalGunDistance = 0.4f;

    // 위치 배율 자동 계산: 이 게임의 총은 카메라에서 멀리(약 1.6m) 크게(5배) 놓여 있어서
    // 같은 0.02m를 움직여도 화면에선 훨씬 덜 움직여 보임 → 거리 ÷ 0.4m 만큼 키워서 보통 FPS처럼 보이게
    // (회전 각도는 거리와 상관없어서 안 키움)
    public static float AutoPositionScale(Vector3 centerInCamera)
    {
        return Mathf.Clamp(centerInCamera.magnitude / TypicalGunDistance, 0.5f, 8f);
    }

    // 기준점 center를 중심으로 rot만큼 돌리고 offset만큼 옮기는 피벗 값
    public static void Apply(Transform pivot, Vector3 offset, Quaternion rot, Vector3 center)
    {
        pivot.localRotation = rot;
        pivot.localPosition = offset + center - rot * center;
    }
}
