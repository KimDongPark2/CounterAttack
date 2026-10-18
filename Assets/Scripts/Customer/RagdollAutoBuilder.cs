#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Project 창에서 손님 프리팹들을 여러 개 선택하고
// 메뉴 Tools > 카운터 어택 > 선택한 손님에 래그돌 만들기 를 누르면 한꺼번에 래그돌을 만든다
//   1순위: Unity의 Ragdoll 마법사를 뼈 이름으로 자동으로 채워서 실행
//   마법사를 못 쓰면: 이 스크립트가 직접 Rigidbody + 콜라이더 + 관절을 붙임
public static class RagdollAutoBuilder
{
    // 래그돌 마법사 칸 → 이 모델의 뼈 이름
    private static readonly string[][] Map =
    {
        new[] { "pelvis", "Hips" },
        new[] { "leftHips", "LeftUpLeg" },   new[] { "leftKnee", "LeftLeg" },   new[] { "leftFoot", "LeftFoot" },
        new[] { "rightHips", "RightUpLeg" }, new[] { "rightKnee", "RightLeg" }, new[] { "rightFoot", "RightFoot" },
        new[] { "leftArm", "LeftArm" },      new[] { "leftElbow", "LeftForeArm" },
        new[] { "rightArm", "RightArm" },    new[] { "rightElbow", "RightForeArm" },
        new[] { "middleSpine", "Spine1" },
        new[] { "head", "Head" },
    };

    private const float TotalMass = 30f;

    [MenuItem("Tools/카운터 어택/선택한 손님에 래그돌 만들기")]
    private static void BuildForSelection()
    {
        Type wizardType = FindWizardType();
        if (wizardType == null) Debug.Log("[래그돌] Unity 마법사를 못 찾아서 직접 만드는 방식으로 진행합니다.");

        int done = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path))
            {
                if (Build(wizardType, go)) { EditorUtility.SetDirty(go); done++; }
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (Build(wizardType, root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log($"[래그돌] {done}개에 래그돌을 만들었습니다.");
    }

    // Unity 버전마다 마법사가 들어 있는 모듈이 달라서 전부 뒤져서 찾음
    private static Type FindWizardType()
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType("UnityEditor.RagdollBuilder");
            if (t != null) return t;
        }
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.GetName().Name.StartsWith("UnityEditor")) continue;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            foreach (Type t in types)
                if (t != null && t.Name == "RagdollBuilder") return t;
        }
        return null;
    }

    private static bool Build(Type wizardType, GameObject root)
    {
        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (string[] pair in Map)
        {
            Transform t = FindBone(root.transform, pair[1]);
            if (t == null)
            {
                Debug.LogError($"[래그돌] '{root.name}'에서 '{pair[1]}' 뼈를 찾지 못해 건너뜁니다.", root);
                return false;
            }
            bones[pair[0]] = t;
        }

        if (wizardType != null)
        {
            try
            {
                BuildWithWizard(wizardType, bones);
                return true;
            }
            catch (Exception e)
            {
                Debug.Log($"[래그돌] 마법사 실행 실패({e.Message}) → 직접 만드는 방식으로 진행합니다.");
            }
        }

        BuildManually(root.transform, bones);
        return true;
    }

    // ---------- 1. Unity 마법사로 만들기 ----------
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void BuildWithWizard(Type type, Dictionary<string, Transform> bones)
    {
        ScriptableObject wizard = ScriptableObject.CreateInstance(type);
        try
        {
            foreach (KeyValuePair<string, Transform> kv in bones) SetField(type, wizard, kv.Key, kv.Value);
            SetField(type, wizard, "totalMass", TotalMass);
            Call(type, wizard, "OnWizardUpdate");  // 축 계산, 뼈 준비
            Call(type, wizard, "OnWizardCreate");  // 콜라이더, Rigidbody, 관절 만들기
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wizard);
        }
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        FieldInfo f = type.GetField(name, Flags);
        if (f == null) throw new Exception($"'{name}' 칸이 없음");
        f.SetValue(target, value);
    }

    private static void Call(Type type, object target, string method)
    {
        MethodInfo m = type.GetMethod(method, Flags);
        if (m == null) throw new Exception($"'{method}' 함수가 없음");
        m.Invoke(target, null);
    }

    // ---------- 2. 직접 만들기 ----------
    private static void BuildManually(Transform root, Dictionary<string, Transform> b)
    {
        // 예전에 붙어 있던 래그돌 부품 정리
        foreach (Transform t in b.Values)
        {
            foreach (CharacterJoint j in t.GetComponents<CharacterJoint>()) UnityEngine.Object.DestroyImmediate(j);
            foreach (Collider c in t.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody r in t.GetComponents<Rigidbody>()) UnityEngine.Object.DestroyImmediate(r);
        }

        Transform leftHand = FindBone(root, "LeftHand");
        Transform rightHand = FindBone(root, "RightHand");
        Transform neck = FindBone(root, "Neck");
        Transform headTop = FindBone(root, "HeadTop_End");

        float hipWidth = Vector3.Distance(b["leftHips"].position, b["rightHips"].position);
        float shoulderWidth = Vector3.Distance(b["leftArm"].position, b["rightArm"].position);

        // 몸통
        Sphere(b["pelvis"], Mid(b["pelvis"].position, b["middleSpine"].position), hipWidth * 0.75f);
        Sphere(b["middleSpine"], Mid(b["middleSpine"].position, neck != null ? neck.position : b["head"].position), shoulderWidth * 0.4f);
        float headSize = headTop != null ? Vector3.Distance(b["head"].position, headTop.position) * 0.55f : shoulderWidth * 0.3f;
        Sphere(b["head"], headTop != null ? Mid(b["head"].position, headTop.position) : b["head"].position, headSize);

        // 팔다리
        Capsule(b["leftHips"], b["leftKnee"].position, 0.22f);
        Capsule(b["leftKnee"], b["leftFoot"].position, 0.18f);
        Capsule(b["rightHips"], b["rightKnee"].position, 0.22f);
        Capsule(b["rightKnee"], b["rightFoot"].position, 0.18f);
        Capsule(b["leftArm"], b["leftElbow"].position, 0.2f);
        Capsule(b["leftElbow"], leftHand != null ? leftHand.position : Extend(b["leftArm"], b["leftElbow"]), 0.18f);
        Capsule(b["rightArm"], b["rightElbow"].position, 0.2f);
        Capsule(b["rightElbow"], rightHand != null ? rightHand.position : Extend(b["rightArm"], b["rightElbow"]), 0.18f);

        // 무게 나누기 + 관절
        Body(b["pelvis"], 0.20f, null);
        Body(b["middleSpine"], 0.20f, b["pelvis"]);
        Body(b["head"], 0.08f, b["middleSpine"]);
        Body(b["leftHips"], 0.10f, b["pelvis"]);
        Body(b["leftKnee"], 0.07f, b["leftHips"]);
        Body(b["rightHips"], 0.10f, b["pelvis"]);
        Body(b["rightKnee"], 0.07f, b["rightHips"]);
        Body(b["leftArm"], 0.05f, b["middleSpine"]);
        Body(b["leftElbow"], 0.04f, b["leftArm"]);
        Body(b["rightArm"], 0.05f, b["middleSpine"]);
        Body(b["rightElbow"], 0.04f, b["rightArm"]);
    }

    private static Vector3 Mid(Vector3 a, Vector3 c) => (a + c) * 0.5f;
    private static Vector3 Extend(Transform from, Transform to) => to.position + (to.position - from.position) * 0.8f;

    private static void Sphere(Transform bone, Vector3 worldCenter, float worldRadius)
    {
        SphereCollider s = bone.gameObject.AddComponent<SphereCollider>();
        s.center = bone.InverseTransformPoint(worldCenter);
        s.radius = worldRadius / MaxScale(bone);
    }

    private static void Capsule(Transform bone, Vector3 worldEnd, float thickness)
    {
        Vector3 local = bone.InverseTransformPoint(worldEnd);
        int axis = 0;
        Vector3 abs = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        if (abs.y > abs.x && abs.y >= abs.z) axis = 1;
        else if (abs.z > abs.x && abs.z > abs.y) axis = 2;

        float length = local.magnitude;
        CapsuleCollider c = bone.gameObject.AddComponent<CapsuleCollider>();
        c.direction = axis;
        c.center = local * 0.5f;
        c.height = length;
        c.radius = length * thickness;
    }

    private static float MaxScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
    }

    private static void Body(Transform bone, float massPart, Transform parent)
    {
        Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = TotalMass * massPart;
        if (parent == null) return;

        CharacterJoint j = bone.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = parent.GetComponent<Rigidbody>();
        j.enableProjection = true;
    }

    // 이름이 같거나 "어쩌구:Hips"처럼 끝이 같은 뼈 찾기
    private static Transform FindBone(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name || t.name.EndsWith(":" + name)) return t;
        return null;
    }
}
#endif