#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 마디마다 움직이는 로우폴리 손 리그를 만들어 설치하는 에디터 도구
// 메뉴
//  Tools/손 만들기/손 리그 설치 (권총 선택 후)
//    - Hierarchy에서 손에 든 권총을 선택하고 실행 (선택 안 하면 Player_Interaction의 handGun을 찾음)
//    - 권총에 HandSocket_R / HandSocket_L(손 잡는 자리)을 만들고, 카메라 아래에 FPSHandRig(손 + 팔)를 만듦
//  Tools/손 만들기/선택한 물건에 오른손 소켓 추가  (스캐너 등)
//  Tools/손 만들기/선택한 물건에 왼손 소켓 추가
//  Tools/손 만들기/손 리그 지우기
// 손 위치 조절: HandSocket_R / L 을 옮기고, 컴포넌트 우클릭 → "이 소켓에 손 맞춰보기"
// 손 모양 조절: FPSHandRig 아래 손가락 마디를 돌린 뒤, FPSHandRig 우클릭 → "지금 손가락 모양을 Edit Pose Name으로 저장"
public static class FPSHandsBuilder
{
    const string Folder = "Assets/Models/FPSHands";
    const string RigName = "FPSHandRig";
    const float GunLength = 0.216f;   // M1911 전체 길이 (m). 손 크기·소켓 위치는 이 길이 기준
    const float Flat = 0.86f;          // 손가락 단면 납작한 정도

    static readonly Color SkinColor = new Color32(0xE8, 0xB4, 0x8E, 0xFF);
    static readonly Color SleeveColor = new Color32(0xB0, 0x1C, 0x27, 0xFF);

    // ---------- 손 모양 데이터 (오른손 기준, m / 도) ----------
    // 손 기준: 손목 = 원점, +Z = 손가락 방향, +Y = 손등, 엄지는 -X 쪽
    // 손가락: 첫 마디 위치, 기본 방향, 마디 길이 3개, 마디 굵기(반지름) 3개
    static readonly Vector3[] FingerPos = { new Vector3(-0.021f, -0.009f, 0.016f), new Vector3(-0.026f, 0f, 0.093f), new Vector3(-0.008f, 0.001f, 0.097f), new Vector3(0.01f, 0f, 0.094f), new Vector3(0.027f, -0.002f, 0.086f) };
    static readonly Vector3[] FingerRest = { new Vector3(18f, -38f, -55f), new Vector3(4f, -4f, 0f), new Vector3(4f, 0f, 0f), new Vector3(4f, 3f, 0f), new Vector3(4f, 7f, 0f) };
    static readonly float[,] FingerLen = { { 0.0420f, 0.0310f, 0.0260f }, { 0.0410f, 0.0240f, 0.0200f }, { 0.0450f, 0.0270f, 0.0210f }, { 0.0420f, 0.0250f, 0.0200f }, { 0.0330f, 0.0190f, 0.0170f } };
    static readonly float[,] FingerRad = { { 0.0125f, 0.0105f, 0.0090f }, { 0.0090f, 0.0081f, 0.0073f }, { 0.0092f, 0.0083f, 0.0074f }, { 0.0087f, 0.0079f, 0.0071f }, { 0.0076f, 0.0069f, 0.0062f } };
    // 손바닥: z0, z1, 손목 쪽 반지름 x/y, 손가락 쪽 반지름 x/y, 중심 x/y
    static readonly float[] Palm = { 0.0000f, 0.0990f, 0.0320f, 0.0135f, 0.0440f, 0.0118f, 0.0010f, -0.0010f };

    // 소켓 (M1911 기준 좌표: x 오른쪽, y 위(손잡이 바닥=0), z 총구 방향(맨 뒤=0)) - 손목 위치·회전
    static readonly Vector3 SocketRPos = new Vector3(0.0186f, 0.0806f, -0.0448f);
    static readonly Vector3 SocketREuler = new Vector3(16.9061f, 4.0672f, -89.1476f);
    static readonly Vector3 SocketLPos = new Vector3(-0.0441f, 0.0749f, -0.023f);
    static readonly Vector3 SocketLEuler = new Vector3(22.6915f, 8.4829f, 107.1285f);

    // 기본 포즈 (관절 15개: 엄지1~3, 검지1~3, 중지1~3, 약지1~3, 새끼1~3)
    static readonly string[] PoseNames = { "Relaxed", "Open", "Grab", "Fist", "Point", "PistolGrip", "PistolSupport" };
    static readonly float[][] PoseData = {
        new float[] { -25.2f, 21.5f, -4.7f, -10f, 0f, 0f, 38.2f, 0f, 0f, 18f, 0f, 0f, 25f, 0f, 0f, 15f, 0f, 0f, 18f, 0f, 0f, 25f, 0f, 0f, 15f, 0f, 0f, 18f, 0f, 0f, 25f, 0f, 0f, 15f, 0f, 0f, 18f, 0f, 0f, 25f, 0f, 0f, 15f, 0f, 0f }, // Relaxed,
        new float[] { 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f, 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f, 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f, 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f, 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f }, // Open,
        new float[] { -23.2f, 37.5f, 20f, 23.7f, 0f, 0f, 2.1f, 0f, 0f, 45f, 0f, 0f, 55f, 0f, 0f, 35f, 0f, 0f, 45f, 0f, 0f, 55f, 0f, 0f, 35f, 0f, 0f, 45f, 0f, 0f, 55f, 0f, 0f, 35f, 0f, 0f, 45f, 0f, 0f, 55f, 0f, 0f, 35f, 0f, 0f }, // Grab,
        new float[] { -1.6f, -2.8f, 92.1f, 48.4f, 0f, 0f, 32.2f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f }, // Fist,
        new float[] { -12.3f, 33.2f, 50.5f, 25.2f, 0f, 0f, -10f, 0f, 0f, 0f, 0f, 0f, 5f, 0f, 0f, 5f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f, 80f, 0f, 0f, 95f, 0f, 0f, 60f, 0f, 0f }, // Point,
        new float[] { 17.58f, 50.73f, 47.24f, -67.43f, 0f, 0f, -0f, 0f, 0f, -17.85f, -5.9f, 0f, 72.81f, 0f, 0f, 61.27f, 0f, 0f, 27.63f, -1.04f, 0f, 101.51f, 0f, 0f, 35.37f, 0f, 0f, 22.33f, -3.61f, 0f, 96.46f, 0f, 0f, 40.14f, 0f, 0f, 4.02f, -4.12f, 0f, 77.19f, 0f, 0f, 37.95f, 0f, 0f }, // PistolGrip,
        new float[] { -39.26f, -86.87f, 143.78f, 70f, 0f, 0f, 80f, 0f, 0f, 22.26f, 17.02f, 0f, 44.38f, 0f, 0f, 63.37f, 0f, 0f, 21.84f, 16.42f, 0f, 72.42f, 0f, 0f, 46.02f, 0f, 0f, 15.66f, 15.63f, 0f, 79.98f, 0f, 0f, 31.64f, 0f, 0f, -8.67f, 11.21f, 0f, 83.43f, 0f, 0f, 27.51f, 0f, 0f }, // PistolSupport
    };

    // ================= 메뉴 =================

    [MenuItem("Tools/손 만들기/손 리그 설치 (권총 선택 후)")]
    static void Install()
    {
        Transform gun = FindGun();
        if (gun == null) { Dialog("손에 든 권총 오브젝트를 Hierarchy에서 선택한 뒤 다시 실행해 주세요."); return; }

        Camera cam = gun.GetComponentInParent<Camera>(true);
        if (cam == null) cam = Camera.main;
        if (cam == null) { Dialog("카메라를 찾지 못했어요. 권총이 플레이어 카메라 아래에 있는지 확인해 주세요."); return; }

        Fit fit;
        if (!Measure(gun, cam, out fit)) { Dialog(gun.name + " 아래에서 총 메시를 찾지 못했어요."); return; }

        Undo.SetCurrentGroupName("손 리그 설치");
        int group = Undo.GetCurrentGroup();

        // 예전 것 지우기 (통짜 손, 소켓, 리그)
        foreach (string n in new[] { "FPSHands", "HandSocket_R", "HandSocket_L" })
        {
            Transform old = gun.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        foreach (FPSHandRig r in Object.FindObjectsByType<FPSHandRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(r.gameObject);

        // 소켓
        HandSocket sr = MakeSocket(gun, "HandSocket_R", HandSide.Right, "PistolGrip", fit, SocketRPos, SocketREuler);
        HandSocket sl = MakeSocket(gun, "HandSocket_L", HandSide.Left, "PistolSupport", fit, SocketLPos, SocketLEuler);

        // 리그
        Mesh[] meshes = BuildMeshes();
        Material skin = GetMaterial(Folder + "/FPSHands_Skin.mat", SkinColor);
        Material sleeve = GetMaterial(Folder + "/FPSHands_Sleeve.mat", SleeveColor);

        GameObject root = new GameObject(RigName);
        Undo.RegisterCreatedObjectUndo(root, "손 리그 설치");
        root.transform.SetParent(cam.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (fit.scale / Mathf.Max(0.0001f, cam.transform.lossyScale.x));

        FPSHandRig rig = root.AddComponent<FPSHandRig>();
        rig.poses = DefaultPoses();

        float d = Vector3.Distance(cam.transform.position, fit.center);
        BuildHand(rig, rig.right, HandSide.Right, meshes, skin, sleeve, cam.transform, d, sr.transform);
        BuildHand(rig, rig.left, HandSide.Left, meshes, skin, sleeve, cam.transform, d, sl.transform);

        SetLayerShadows(root.transform, gun.gameObject.layer);
        rig.EditorSnapToSocket(sr);
        rig.EditorSnapToSocket(sl);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
        Selection.activeGameObject = root;

        string msg = "손 리그를 설치했어요.\n\n" +
                     $"총 길이 {fit.length:0.###}, 높이/길이 {fit.height / fit.length:0.00} (M1911은 약 0.64)\n\n" +
                     "손 위치가 어긋나면: 권총 아래 HandSocket_R / HandSocket_L을 옮기고\n컴포넌트 우클릭 → '이 소켓에 손 맞춰보기'";
        if (Mathf.Abs(fit.height / fit.length - 0.64f) > 0.15f)
            msg += "\n\n높이/길이 비율이 M1911과 많이 달라서 손 위치를 직접 맞춰야 할 수 있어요.";
        Debug.Log("[손 리그] " + msg.Replace("\n", " "));
        Dialog(msg);
    }

    [MenuItem("Tools/손 만들기/선택한 물건에 오른손 소켓 추가")]
    static void AddSocketR() { AddSocket(HandSide.Right); }

    [MenuItem("Tools/손 만들기/선택한 물건에 왼손 소켓 추가")]
    static void AddSocketL() { AddSocket(HandSide.Left); }

    static void AddSocket(HandSide side)
    {
        Transform t = Selection.activeTransform;
        if (t == null) { Dialog("손에 들 물건(스캐너 등)을 Hierarchy에서 선택한 뒤 실행해 주세요."); return; }
        Camera cam = t.GetComponentInParent<Camera>(true);
        if (cam == null) cam = Camera.main;
        Bounds b = WorldBounds(t);
        Transform c = cam != null ? cam.transform : t;
        FPSHandRig rig = Object.FindFirstObjectByType<FPSHandRig>(FindObjectsInactive.Include);
        float sc = rig != null ? rig.transform.lossyScale.x : 1f;

        string n = side == HandSide.Right ? "HandSocket_R" : "HandSocket_L";
        Transform old = t.Find(n);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject go = new GameObject(n);
        Undo.RegisterCreatedObjectUndo(go, "손 소켓 추가");
        go.transform.SetParent(t, false);
        // 물건 아래쪽을 손바닥으로 쥐는 자세: 손가락은 앞, 손등은 바깥쪽
        Quaternion rot = Quaternion.LookRotation(c.forward, c.up) * Quaternion.Euler(0f, 0f, side == HandSide.Right ? -80f : 80f);
        Vector3 pos = b.center - c.up * (b.extents.y * 0.3f) - (rot * new Vector3(0f, -0.03f, 0.06f)) * sc;
        go.transform.SetPositionAndRotation(pos, rot);
        HandSocket s = go.AddComponent<HandSocket>();
        s.side = side; s.pose = "Grab"; s.priority = 1;
        go.layer = t.gameObject.layer;
        if (rig != null) rig.EditorSnapToSocket(s);
        EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        Selection.activeGameObject = go;
        Dialog(n + "을 만들었어요.\n위치·회전을 조절한 뒤 HandSocket 컴포넌트 우클릭 → '이 소켓에 손 맞춰보기'로 확인하세요.\n손 모양은 HandSocket의 Pose 칸에서 바꿀 수 있어요 (Grab, Fist, Open, Point...).");
    }

    [MenuItem("Tools/손 만들기/손 리그 지우기")]
    static void Remove()
    {
        foreach (FPSHandRig r in Object.FindObjectsByType<FPSHandRig>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(r.gameObject);
        foreach (HandSocket s in Object.FindObjectsByType<HandSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(s.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ================= 총 재기 =================

    struct Fit
    {
        public Vector3 origin, fwd, up, right, center;
        public float scale, length, height;
        public Vector3 Point(Vector3 c) { return origin + (right * c.x + up * c.y + fwd * c.z) * scale; }
        public Quaternion Rot(Vector3 euler) { return Quaternion.LookRotation(fwd, up) * Quaternion.Euler(euler); }
    }

    static bool Measure(Transform gun, Camera cam, out Fit fit)
    {
        fit = new Fit();
        List<Vector3> pts = new List<Vector3>();
        foreach (Renderer r in gun.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || Skip(r.transform, gun)) continue;
            Mesh m = null;
            if (r is MeshRenderer) { MeshFilter mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; }
            else if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
            if (m == null) continue;
            Vector3[] vs = m.isReadable ? m.vertices : Corners(m.bounds);
            Matrix4x4 mat = r.transform.localToWorldMatrix;
            foreach (Vector3 v in vs) pts.Add(mat.MultiplyPoint3x4(v));
        }
        if (pts.Count == 0) return false;

        Vector3[] axes = { gun.right, -gun.right, gun.up, -gun.up, gun.forward, -gun.forward };
        int fi = Best(axes, cam.transform.forward, -1);
        int ui = Best(axes, cam.transform.up, fi / 2);
        Vector3 fwd = axes[fi].normalized, up = axes[ui].normalized, right = Vector3.Cross(up, fwd).normalized;

        float minF = float.MaxValue, maxF = float.MinValue, minU = float.MaxValue, maxU = float.MinValue, minR = float.MaxValue, maxR = float.MinValue;
        foreach (Vector3 p in pts)
        {
            float f = Vector3.Dot(p, fwd), u = Vector3.Dot(p, up), r = Vector3.Dot(p, right);
            minF = Mathf.Min(minF, f); maxF = Mathf.Max(maxF, f);
            minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
            minR = Mathf.Min(minR, r); maxR = Mathf.Max(maxR, r);
        }
        fit.fwd = fwd; fit.up = up; fit.right = right;
        fit.length = maxF - minF; fit.height = maxU - minU;
        fit.scale = fit.length / GunLength;
        fit.origin = fwd * minF + up * minU + right * ((minR + maxR) * 0.5f);
        fit.center = fwd * ((minF + maxF) * 0.5f) + up * ((minU + maxU) * 0.5f) + right * ((minR + maxR) * 0.5f);
        return true;
    }

    static bool Skip(Transform t, Transform root)
    {
        for (Transform c = t; c != null && c != root; c = c.parent)
            if (!c.gameObject.activeSelf || c.name == "FPSHands" || c.name.StartsWith("HandSocket")) return true;
        return false;
    }

    static Vector3[] Corners(Bounds b)
    {
        Vector3[] o = new Vector3[8];
        for (int i = 0; i < 8; i++)
            o[i] = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        return o;
    }

    static int Best(Vector3[] axes, Vector3 dir, int skipPair)
    {
        int best = 0; float bd = float.MinValue;
        for (int i = 0; i < axes.Length; i++)
        {
            if (i / 2 == skipPair) continue;
            float d = Vector3.Dot(axes[i].normalized, dir);
            if (d > bd) { bd = d; best = i; }
        }
        return best;
    }

    static Bounds WorldBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.05f);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform FindGun()
    {
        if (Selection.activeTransform != null)
        {
            Transform s = Selection.activeTransform;
            if ((s.name == "FPSHands" || s.name.StartsWith("HandSocket")) && s.parent != null) s = s.parent;
            return s;
        }
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "Player_Interaction") continue;
            SerializedObject so = new SerializedObject(mb);
            foreach (string field in new[] { "handGun", "GunInHand", "gunInHand" })
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                if (p.objectReferenceValue is GameObject go) return go.transform;
                if (p.objectReferenceValue is Component c) return c.transform;
            }
        }
        return null;
    }

    static HandSocket MakeSocket(Transform gun, string name, HandSide side, string pose, Fit fit, Vector3 pos, Vector3 euler)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "손 소켓");
        go.transform.SetParent(gun, false);
        go.transform.SetPositionAndRotation(fit.Point(pos), fit.Rot(euler));
        go.layer = gun.gameObject.layer;
        HandSocket s = go.AddComponent<HandSocket>();
        s.side = side; s.pose = pose;
        return s;
    }

    // ================= 리그 만들기 =================

    // 메시 순서: 0 손바닥, 1~15 손가락 마디(엄지1..새끼3), 16 팔 / 왼손은 +17
    static void BuildHand(FPSHandRig rig, FPSHandRig.HandBones b, HandSide side, Mesh[] meshes, Material skin, Material sleeve,
                          Transform cam, float d, Transform socket)
    {
        bool isLeft = side == HandSide.Left;
        float sx = isLeft ? -1f : 1f;
        int mo = isLeft ? 17 : 0;
        string sfx = isLeft ? "_L" : "_R";
        Transform root = rig.transform;

        Transform wrist = new GameObject("Hand" + sfx).transform;
        wrist.SetParent(root, false);
        b.wrist = wrist;
        AddMesh(wrist, "Palm", meshes[mo + 0], skin);

        b.joints = new Transform[FPSHandRig.JointCount];
        b.restEuler = new Vector3[5];
        for (int f = 0; f < 5; f++)
        {
            b.restEuler[f] = FingerRest[f];
            Transform parent = wrist;
            Vector3 lp = new Vector3(FingerPos[f].x * sx, FingerPos[f].y, FingerPos[f].z);
            for (int j = 0; j < 3; j++)
            {
                Transform jt = new GameObject(FPSHandRig.FingerNames[f] + (j + 1) + sfx).transform;
                jt.SetParent(parent, false);
                jt.localPosition = lp;
                MeshFilter mf = jt.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = meshes[mo + 1 + f * 3 + j];
                MeshRenderer mr = jt.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = skin;
                b.joints[f * 3 + j] = jt;
                parent = jt;
                lp = new Vector3(0f, 0f, FingerLen[f, j]);
            }
        }

        Transform fore = new GameObject("Forearm" + sfx).transform;
        fore.SetParent(root, false);
        MeshFilter fmf = fore.gameObject.AddComponent<MeshFilter>();
        fmf.sharedMesh = meshes[mo + 16];
        MeshRenderer fmr = fore.gameObject.AddComponent<MeshRenderer>();
        fmr.sharedMaterials = new[] { skin, sleeve };
        b.forearm = fore;

        // 팔꿈치(팔이 향하는 곳)와 쉬는 자리: 카메라 기준, 총까지 거리 d에 비례
        Transform elbow = new GameObject("Elbow" + sfx).transform;
        elbow.SetParent(root, false);
        elbow.position = cam.position + cam.rotation * (isLeft ? new Vector3(-0.55f, -1.0f, 0.0f) : new Vector3(0.70f, -0.95f, -0.15f)) * d;
        b.elbow = elbow;

        Transform rest = new GameObject("Rest" + sfx).transform;
        rest.SetParent(root, false);
        rest.position = cam.position + cam.rotation * (isLeft ? new Vector3(-0.15f, -1.2f, 0.7f) : new Vector3(0.35f, -1.2f, 0.7f)) * d;
        rest.rotation = socket.rotation;
        b.rest = rest;
    }

    static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void SetLayerShadows(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        foreach (Transform c in t) SetLayerShadows(c, layer);
    }

    static List<FPSHandRig.HandPose> DefaultPoses()
    {
        List<FPSHandRig.HandPose> list = new List<FPSHandRig.HandPose>();
        for (int p = 0; p < PoseNames.Length; p++)
        {
            FPSHandRig.HandPose hp = new FPSHandRig.HandPose { name = PoseNames[p], joints = new Vector3[FPSHandRig.JointCount] };
            for (int i = 0; i < FPSHandRig.JointCount; i++)
                hp.joints[i] = new Vector3(PoseData[p][i * 3], PoseData[p][i * 3 + 1], PoseData[p][i * 3 + 2]);
            list.Add(hp);
        }
        return list;
    }

    static Material GetMaterial(string path, Color color)
    {
        EnsureFolder();
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Models", "FPSHands");
    }

    // ================= 메시 =================

    class MB
    {
        public List<Vector3> v = new List<Vector3>();
        public List<int>[] t = { new List<int>(), new List<int>() };

        // 볼록한 덩어리 하나 (면이 바깥을 보게 자동 정렬, 각진 느낌이 나게 꼭짓점 따로)
        public void Solid(List<Vector3> verts, List<int[]> faces, int mat)
        {
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in verts) c += p;
            c /= verts.Count;
            foreach (int[] f in faces)
            {
                for (int k = 1; k < f.Length - 1; k++)
                {
                    Vector3 a = verts[f[0]], b = verts[f[k]], d = verts[f[k + 1]];
                    Vector3 n = Vector3.Cross(b - a, d - a);
                    if (n.sqrMagnitude < 1e-16f) continue;
                    if (Vector3.Dot(n, (a + b + d) / 3f - c) < 0f) { Vector3 tmp = b; b = d; d = tmp; }
                    t[mat].Add(v.Count); v.Add(a);
                    t[mat].Add(v.Count); v.Add(b);
                    t[mat].Add(v.Count); v.Add(d);
                }
            }
        }

        public Mesh ToMesh(string name, bool mirror)
        {
            List<Vector3> vv = new List<Vector3>(v);
            List<int>[] tt = { new List<int>(t[0]), new List<int>(t[1]) };
            if (mirror)
            {
                for (int i = 0; i < vv.Count; i++) vv[i] = new Vector3(-vv[i].x, vv[i].y, vv[i].z);
                foreach (List<int> l in tt)
                    for (int i = 0; i < l.Count; i += 3) { int tmp = l[i + 1]; l[i + 1] = l[i + 2]; l[i + 2] = tmp; }
            }
            Mesh m = new Mesh { name = name };
            m.SetVertices(vv);
            m.subMeshCount = tt[1].Count > 0 ? 2 : 1;
            m.SetTriangles(tt[0], 0);
            if (tt[1].Count > 0) m.SetTriangles(tt[1], 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    // 타원 기둥 (z0→z1, 반지름 x/y가 양 끝에서 다름)
    static void Prism(MB mb, float z0, float z1, float rx0, float ry0, float rx1, float ry1, int sides, int mat, float xo = 0f, float yo = 0f)
    {
        List<Vector3> vs = new List<Vector3>();
        for (int e = 0; e < 2; e++)
        {
            float z = e == 0 ? z0 : z1, rx = e == 0 ? rx0 : rx1, ry = e == 0 ? ry0 : ry1;
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides + Mathf.PI / sides;
                vs.Add(new Vector3(xo + rx * Mathf.Cos(a), yo + ry * Mathf.Sin(a), z));
            }
        }
        List<int[]> faces = new List<int[]>();
        int[] cap0 = new int[sides], cap1 = new int[sides];
        for (int i = 0; i < sides; i++) { cap0[i] = i; cap1[i] = sides + i; }
        faces.Add(cap0); faces.Add(cap1);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            faces.Add(new[] { i, j, sides + j, sides + i });
        }
        mb.Solid(vs, faces, mat);
    }

    static Mesh[] BuildMeshes()
    {
        EnsureFolder();
        List<MB> parts = new List<MB>();
        List<string> names = new List<string>();

        MB palm = new MB();
        Prism(palm, Palm[0], Palm[1], Palm[2], Palm[3], Palm[4], Palm[5], 8, 0, Palm[6], Palm[7]);
        parts.Add(palm); names.Add("Palm");

        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
            {
                MB mb = new MB();
                float L = FingerLen[f, j], r0 = FingerRad[f, j], r1 = j < 2 ? FingerRad[f, j + 1] : FingerRad[f, 2] * 0.92f;
                bool tip = j == 2;
                Prism(mb, -0.002f, L + (tip ? 0f : 0.003f), r0, r0 * Flat, r1, r1 * Flat, 6, 0);
                if (tip) Prism(mb, L - 0.0005f, L + r1 * 0.55f, r1 * 0.95f, r1 * Flat * 0.95f, r1 * 0.45f, r1 * Flat * 0.4f, 6, 0);
                parts.Add(mb); names.Add(FPSHandRig.FingerNames[f] + (j + 1));
            }

        MB arm = new MB();
        Prism(arm, 0.012f, -0.075f, 0.025f, 0.021f, 0.029f, 0.025f, 8, 0);       // 팔 (피부)
        Prism(arm, -0.068f, -0.095f, 0.036f, 0.033f, 0.037f, 0.034f, 8, 1);      // 소매 끝단
        Prism(arm, -0.09f, -0.42f, 0.034f, 0.031f, 0.044f, 0.040f, 8, 1);        // 소매
        parts.Add(arm); names.Add("Forearm");

        Mesh[] meshes = new Mesh[parts.Count * 2];
        for (int i = 0; i < parts.Count; i++)
        {
            meshes[i] = parts[i].ToMesh(names[i] + "_R", false);
            meshes[parts.Count + i] = parts[i].ToMesh(names[i] + "_L", true);
        }

        string path = Folder + "/FPSHands_Meshes.asset";
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(meshes[0], path);
        for (int i = 1; i < meshes.Length; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
        AssetDatabase.SaveAssets();
        return meshes;
    }

    static void Dialog(string msg) { EditorUtility.DisplayDialog("손 만들기", msg, "확인"); }
}
#endif
