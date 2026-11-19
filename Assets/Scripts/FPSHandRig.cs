using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 1인칭 손 리그 (마디마다 따로 움직이는 로우폴리 손 + 팔)
// - 켜져 있는 HandSocket(총, 스캐너 등)을 찾아서 손이 그 자리로 가서 쥠
// - 소켓이 없으면 손은 화면 아래(rest)로 내려가서 숨음
// - 코드에서 부르는 기능: Reach(줍기), Follow/StopFollow(재장전 때 왼손 등), SetPose/ClearPose, PulseTrigger(방아쇠)
// - 손 모양(포즈)은 Inspector의 Poses 목록. 에디터에서 손가락을 직접 돌린 뒤 우클릭 메뉴로 새 포즈 저장 가능
// Tools/손 만들기/손 리그 설치 로 자동 생성됨
[DefaultExecutionOrder(10000)]
public class FPSHandRig : MonoBehaviour
{
    public static FPSHandRig Instance { get; private set; }

    // 관절 순서: 엄지1~3, 검지1~3, 중지1~3, 약지1~3, 새끼1~3 (1 = 손바닥 쪽 첫 마디)
    public const int JointCount = 15;
    public static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    [Serializable]
    public class HandPose
    {
        public string name;
        [Tooltip("관절 15개 회전값 (오른손 기준, x = 굽힘, y = 벌림). 왼손은 자동으로 좌우 반전")]
        public Vector3[] joints = new Vector3[JointCount];
    }

    [Serializable]
    public class HandBones
    {
        public Transform wrist;
        public Transform forearm;
        [Tooltip("팔꿈치 방향 (화면 밖). 팔이 이쪽을 향함")]
        public Transform elbow;
        [Tooltip("잡을 게 없을 때 손이 내려가 숨는 자리 (화면 아래)")]
        public Transform rest;
        public Transform[] joints = new Transform[JointCount];
        [Tooltip("손가락 첫 마디 기본 방향 (오른손 기준, 건드리지 않기)")]
        public Vector3[] restEuler = new Vector3[5];
        [NonSerialized] public Renderer[] renderers;
    }

    public HandBones right = new HandBones();
    public HandBones left = new HandBones();
    public List<HandPose> poses = new List<HandPose>();

    [Header("움직임")]
    [Tooltip("잡는 물건이 바뀔 때 손이 옮겨가는 시간 (총 꺼낼 때 손이 올라오는 시간)")]
    public float moveTime = 0.2f;
    [Tooltip("손가락 모양이 바뀌는 빠르기")]
    public float poseSharpness = 16f;
    [Tooltip("잡을 소켓이 없을 때의 손 모양")]
    public string idlePose = "Relaxed";

    [Header("방아쇠 (PulseTrigger)")]
    [Tooltip("쏠 때 검지 1~3마디가 더 굽는 각도")]
    public Vector3 triggerCurl = new Vector3(8f, 28f, 18f);
    public float triggerInTime = 0.04f;
    public float triggerOutTime = 0.10f;

    [Header("에디터 도구 (컴포넌트 우클릭 메뉴에서 사용)")]
    public HandSide editSide = HandSide.Right;
    public string editPoseName = "MyPose";

    private class State
    {
        public HandBones b;
        public HandSide side;
        public bool init;
        public object source;
        public Vector3 fromPos; public Quaternion fromRot;
        public float moveT = 1f;
        public Vector3 lastPos; public Quaternion lastRot;
        public Transform followTarget; public bool followUsePoint; public Vector3 followPos; public Quaternion followRot;
        public float overrideW, overrideGoal, overrideSpeed = 10f;
        public string poseOverride;
        public Coroutine routine;
        public bool visible = true;
        public float trigger;
    }

    private State rs, ls;
    private Dictionary<string, HandPose> poseMap;
    private Coroutine triggerRoutine;

    private void Awake()
    {
        Instance = this;
        rs = new State { b = right, side = HandSide.Right };
        ls = new State { b = left, side = HandSide.Left };
        BuildPoseMap();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnValidate() { poseMap = null; }

    private void BuildPoseMap()
    {
        poseMap = new Dictionary<string, HandPose>();
        foreach (HandPose p in poses)
            if (p != null && !string.IsNullOrEmpty(p.name)) poseMap[p.name] = p;
    }

    // ================= 코드에서 부르는 기능 =================

    // 쏠 때 검지가 방아쇠를 당기는 움직임 (오른손)
    public void PulseTrigger()
    {
        if (rs == null) return;
        if (triggerRoutine != null) StopCoroutine(triggerRoutine);
        triggerRoutine = StartCoroutine(TriggerCo());
    }

    // 줍기: 손이 worldPoint로 뻗어서 쥐고(onGrab 호출) 돌아옴. 이미 움직이는 중이면 false
    public bool Reach(HandSide side, Vector3 worldPoint, Action onGrab = null, string grabPose = "Grab",
                      float reachTime = 0.16f, float holdTime = 0.10f, float returnTime = 0.22f)
    {
        Camera cam = GetComponentInParent<Camera>();
        Transform c = cam != null ? cam.transform : transform;
        Vector3 dir = worldPoint - c.position;
        if (dir.sqrMagnitude < 1e-6f) dir = c.forward;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, c.up);   // 손가락은 앞, 손등은 위
        float sc = transform.lossyScale.x;
        Vector3 wrist = worldPoint - rot * new Vector3(0f, -0.025f, 0.055f) * sc;  // 손바닥 가운데가 물건 위에 오게
        return ReachPose(side, wrist, rot, onGrab, grabPose, reachTime, holdTime, returnTime);
    }

    // 줍기 (손목 위치·방향을 직접 지정)
    public bool ReachPose(HandSide side, Vector3 wristPos, Quaternion wristRot, Action onGrab = null, string grabPose = "Grab",
                          float reachTime = 0.16f, float holdTime = 0.10f, float returnTime = 0.22f)
    {
        State s = Get(side);
        if (s == null || s.routine != null) return false;
        s.routine = StartCoroutine(ReachCo(s, wristPos, wristRot, onGrab, grabPose, reachTime, holdTime, returnTime));
        return true;
    }

    public bool IsBusy(HandSide side) { State s = Get(side); return s != null && (s.routine != null || s.overrideGoal > 0f); }

    // 손이 target을 계속 따라가게 (예: 재장전 때 왼손이 탄창을 잡고 같이 움직임). pose가 null이면 손 모양은 그대로
    public void Follow(HandSide side, Transform target, string pose = null, float blendTime = 0.12f)
    {
        State s = Get(side);
        if (s == null || target == null) return;
        StopRoutine(s);
        s.followTarget = target; s.followUsePoint = false;
        s.poseOverride = pose;
        s.overrideGoal = 1f; s.overrideSpeed = 1f / Mathf.Max(0.01f, blendTime);
    }

    // Follow 끝내고 원래 자리(소켓)로 돌아가기
    public void StopFollow(HandSide side, float blendTime = 0.15f)
    {
        State s = Get(side);
        if (s == null) return;
        s.overrideGoal = 0f; s.overrideSpeed = 1f / Mathf.Max(0.01f, blendTime);
        s.poseOverride = null;
    }

    // 손 모양만 잠깐 바꾸기 (예: 탄창 잡을 때 "Grab"). ClearPose로 원래대로
    public void SetPose(HandSide side, string pose) { State s = Get(side); if (s != null) s.poseOverride = pose; }
    public void ClearPose(HandSide side) { State s = Get(side); if (s != null) s.poseOverride = null; }

    public Transform GetWrist(HandSide side) { return side == HandSide.Right ? right.wrist : left.wrist; }

    // ================= 매 프레임 =================

    private void LateUpdate()
    {
        if (rs == null) Awake();
        if (poseMap == null) BuildPoseMap();
        float dt = Time.unscaledDeltaTime;   // F1 시간 정지 중에도 손은 움직임
        UpdateHand(rs, dt);
        UpdateHand(ls, dt);
    }

    private void UpdateHand(State s, float dt)
    {
        HandBones b = s.b;
        if (b.wrist == null) return;

        HandSocket sock = HandSocket.Find(s.side);
        object src; Vector3 tPos; Quaternion tRot; string pose;
        if (sock != null)
        {
            src = sock; tPos = sock.transform.position; tRot = sock.transform.rotation; pose = sock.pose;
        }
        else
        {
            src = b.rest;
            tPos = b.rest != null ? b.rest.position : b.wrist.position;
            tRot = b.rest != null ? b.rest.rotation : b.wrist.rotation;
            pose = idlePose;
        }

        Quaternion inv = Quaternion.Inverse(transform.rotation);
        Vector3 lp = transform.InverseTransformPoint(tPos);
        Quaternion lr = inv * tRot;

        if (!s.init) { s.init = true; s.source = src; s.moveT = 1f; s.lastPos = lp; s.lastRot = lr; }
        if (!ReferenceEquals(src, s.source))
        {
            s.source = src; s.fromPos = s.lastPos; s.fromRot = s.lastRot; s.moveT = 0f;
        }
        if (s.moveT < 1f)
        {
            s.moveT = Mathf.Min(1f, s.moveT + dt / Mathf.Max(0.01f, moveTime));
            float e = Smooth(s.moveT);
            lp = Vector3.LerpUnclamped(s.fromPos, lp, e);
            lr = Quaternion.Slerp(s.fromRot, lr, e);
        }

        // 줍기 / Follow
        s.overrideW = Mathf.MoveTowards(s.overrideW, s.overrideGoal, dt * s.overrideSpeed);
        if (s.overrideW <= 0f && s.overrideGoal <= 0f && s.routine == null) { s.followTarget = null; s.followUsePoint = false; }
        if (s.overrideW > 0f && (s.followUsePoint || s.followTarget != null))
        {
            Vector3 op = s.followUsePoint ? s.followPos : s.followTarget.position;
            Quaternion orot = s.followUsePoint ? s.followRot : s.followTarget.rotation;
            float e = Smooth(s.overrideW);
            lp = Vector3.LerpUnclamped(lp, transform.InverseTransformPoint(op), e);
            lr = Quaternion.Slerp(lr, inv * orot, e);
        }

        s.lastPos = lp; s.lastRot = lr;
        b.wrist.SetPositionAndRotation(transform.TransformPoint(lp), transform.rotation * lr);

        SetVisible(s, sock != null || s.moveT < 1f || s.overrideW > 0f);

        if (!string.IsNullOrEmpty(s.poseOverride)) pose = s.poseOverride;
        ApplyPose(s, pose, 1f - Mathf.Exp(-poseSharpness * dt));
        AimForearm(b);
    }

    private void AimForearm(HandBones b)
    {
        if (b.forearm == null || b.wrist == null) return;
        b.forearm.position = b.wrist.position;
        Vector3 dir = b.elbow != null ? b.wrist.position - b.elbow.position : b.wrist.forward;
        if (dir.sqrMagnitude > 1e-8f) b.forearm.rotation = Quaternion.LookRotation(dir, transform.up);
    }

    private void ApplyPose(State s, string name, float k)
    {
        HandPose p = FindPose(name);
        if (p == null) return;
        bool isLeft = s.side == HandSide.Left;
        for (int i = 0; i < JointCount; i++)
        {
            Transform j = i < s.b.joints.Length ? s.b.joints[i] : null;
            if (j == null) continue;
            Vector3 e = p.joints != null && i < p.joints.Length ? p.joints[i] : Vector3.zero;
            bool trig = !isLeft && s.trigger > 0f && i >= 3 && i <= 5;
            if (trig) e.x += triggerCurl[i - 3] * s.trigger;
            Quaternion q = JointRotation(s.b, i, e, isLeft);
            j.localRotation = (k >= 1f || trig) ? q : Quaternion.Slerp(j.localRotation, q, k);
        }
    }

    private HandPose FindPose(string name)
    {
        if (poseMap == null) BuildPoseMap();
        HandPose p;
        if (!string.IsNullOrEmpty(name) && poseMap.TryGetValue(name, out p)) return p;
        if (poseMap.TryGetValue(idlePose, out p)) return p;
        return null;
    }

    public static Quaternion JointRotation(HandBones b, int i, Vector3 e, bool isLeft)
    {
        if (isLeft) e = new Vector3(e.x, -e.y, -e.z);
        Quaternion q = Quaternion.Euler(e);
        if (i % 3 == 0 && b.restEuler != null && i / 3 < b.restEuler.Length)
        {
            Vector3 r = b.restEuler[i / 3];
            if (isLeft) r = new Vector3(r.x, -r.y, -r.z);
            q = Quaternion.Euler(r) * q;
        }
        return q;
    }

    private void SetVisible(State s, bool v)
    {
        if (s.b.renderers == null)
        {
            List<Renderer> list = new List<Renderer>();
            if (s.b.wrist != null) list.AddRange(s.b.wrist.GetComponentsInChildren<Renderer>(true));
            if (s.b.forearm != null) list.AddRange(s.b.forearm.GetComponentsInChildren<Renderer>(true));
            s.b.renderers = list.ToArray();
            s.visible = !v;
        }
        if (s.visible == v) return;
        s.visible = v;
        foreach (Renderer r in s.b.renderers) if (r != null) r.enabled = v;
    }

    private State Get(HandSide side)
    {
        if (rs == null) Awake();
        return side == HandSide.Right ? rs : ls;
    }

    private void StopRoutine(State s)
    {
        if (s.routine != null) { StopCoroutine(s.routine); s.routine = null; }
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private IEnumerator TriggerCo()
    {
        float t = 0f;
        while (t < triggerInTime) { t += Time.unscaledDeltaTime; rs.trigger = Mathf.Clamp01(t / triggerInTime); yield return null; }
        t = 0f;
        while (t < triggerOutTime) { t += Time.unscaledDeltaTime; rs.trigger = 1f - Mathf.Clamp01(t / triggerOutTime); yield return null; }
        rs.trigger = 0f;
        triggerRoutine = null;
    }

    private IEnumerator ReachCo(State s, Vector3 pos, Quaternion rot, Action onGrab, string grabPose,
                                float reachTime, float holdTime, float returnTime)
    {
        s.followTarget = null; s.followUsePoint = true; s.followPos = pos; s.followRot = rot;
        s.poseOverride = "Open";
        s.overrideGoal = 1f; s.overrideSpeed = 1f / Mathf.Max(0.01f, reachTime);
        while (s.overrideW < 1f) yield return null;

        s.poseOverride = grabPose;
        float t = 0f;
        while (t < holdTime * 0.5f) { t += Time.unscaledDeltaTime; yield return null; }
        try { onGrab?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
        t = 0f;
        while (t < holdTime * 0.5f) { t += Time.unscaledDeltaTime; yield return null; }

        s.overrideGoal = 0f; s.overrideSpeed = 1f / Mathf.Max(0.01f, returnTime);
        while (s.overrideW > 0f) yield return null;
        s.poseOverride = null; s.followUsePoint = false;
        s.routine = null;
    }

#if UNITY_EDITOR
    // ================= 에디터 도구 =================

    // HandSocket 우클릭 → "이 소켓에 손 맞춰보기"에서 사용
    public void EditorSnapToSocket(HandSocket sock)
    {
        HandBones b = sock.side == HandSide.Right ? right : left;
        if (b.wrist == null) return;
        RecordUndo(b, "손 맞춰보기");
        b.wrist.SetPositionAndRotation(sock.transform.position, sock.transform.rotation);
        EditorApply(b, sock.side, FindPose(sock.pose));
        AimForearm(b);
    }

    [ContextMenu("손 모양 미리보기 (Edit Side / Edit Pose Name)")]
    private void EditorPreview()
    {
        HandBones b = editSide == HandSide.Right ? right : left;
        BuildPoseMap();
        HandPose p;
        if (!poseMap.TryGetValue(editPoseName, out p)) { Debug.LogWarning("[FPSHandRig] 포즈 '" + editPoseName + "'가 없어요."); return; }
        RecordUndo(b, "손 모양 미리보기");
        EditorApply(b, editSide, p);
    }

    [ContextMenu("지금 손가락 모양을 Edit Pose Name으로 저장")]
    private void EditorCapture()
    {
        HandBones b = editSide == HandSide.Right ? right : left;
        bool isLeft = editSide == HandSide.Left;
        HandPose p = poses.Find(x => x != null && x.name == editPoseName);
        UnityEditor.Undo.RecordObject(this, "손 포즈 저장");
        if (p == null) { p = new HandPose { name = editPoseName }; poses.Add(p); }
        p.joints = new Vector3[JointCount];
        for (int i = 0; i < JointCount; i++)
        {
            Transform j = b.joints[i];
            if (j == null) continue;
            Quaternion q = j.localRotation;
            if (i % 3 == 0)
            {
                Vector3 r = b.restEuler[i / 3];
                if (isLeft) r = new Vector3(r.x, -r.y, -r.z);
                q = Quaternion.Inverse(Quaternion.Euler(r)) * q;
            }
            Vector3 e = q.eulerAngles;
            e = new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));
            if (isLeft) e = new Vector3(e.x, -e.y, -e.z);
            p.joints[i] = e;
        }
        poseMap = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[FPSHandRig] 포즈 '" + editPoseName + "' 저장 완료 (" + editSide + " 손 기준)");
    }

    private void EditorApply(HandBones b, HandSide side, HandPose p)
    {
        if (p == null) return;
        for (int i = 0; i < JointCount; i++)
            if (b.joints[i] != null)
                b.joints[i].localRotation = JointRotation(b, i, p.joints != null && i < p.joints.Length ? p.joints[i] : Vector3.zero, side == HandSide.Left);
    }

    private void RecordUndo(HandBones b, string label)
    {
        List<UnityEngine.Object> objs = new List<UnityEngine.Object>();
        if (b.wrist != null) foreach (Transform t in b.wrist.GetComponentsInChildren<Transform>(true)) objs.Add(t);
        if (b.forearm != null) objs.Add(b.forearm);
        UnityEditor.Undo.RecordObjects(objs.ToArray(), label);
    }
#endif
}
