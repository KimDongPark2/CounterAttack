#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 메뉴 Tools > 카운터 어택 > 손님 Animator 만들기 를 누르면
// Assets 폴더에 Customer_Controller가 만들어진다
//   - Speed(Float) 블렌드 트리: 0 = Idle, 1.5 = Walk, 3.5 = Run
//   - Greet(Trigger): 결제 후 인사
// 클립 이름이 다르면 아래 이름만 바꾸면 됨
public static class CustomerControllerMaker
{
    private const string IdleClip = "A_Idle A";
    private const string WalkClip = "A_Walk A";
    private const string RunClip = "A_Run A";
    private const string GreetClip = "A_Greet";

    // CustomerSpawner의 Walk Speed, Run Speed와 같게
    private const float WalkSpeed = 1.5f;
    private const float RunSpeed = 3.5f;

    [MenuItem("Tools/카운터 어택/손님 Animator 만들기")]
    private static void Create()
    {
        AnimationClip idle = FindClip(IdleClip);
        AnimationClip walk = FindClip(WalkClip);
        AnimationClip run = FindClip(RunClip);
        AnimationClip greet = FindClip(GreetClip);

        if (idle == null || walk == null || run == null)
        {
            Debug.LogError($"[손님 Animator] 클립을 못 찾았습니다. Idle:{idle != null} Walk:{walk != null} Run:{run != null}\n" +
                           "CustomerControllerMaker.cs 위쪽의 클립 이름을 실제 이름으로 바꿔주세요.");
            return;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Customer_Controller.controller");
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Greet", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // 걷기/뛰기/서기 블렌드 트리
        AnimatorState move = ctrl.CreateBlendTreeInController("Move", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        tree.AddChild(run, RunSpeed);
        sm.defaultState = move;

        // 인사 (클립이 있을 때만)
        if (greet != null)
        {
            AnimatorState g = sm.AddState("Greet");
            g.motion = greet;

            AnimatorStateTransition toGreet = sm.AddAnyStateTransition(g);
            toGreet.AddCondition(AnimatorConditionMode.If, 0f, "Greet");
            toGreet.hasExitTime = false;
            toGreet.duration = 0.1f;
            toGreet.canTransitionToSelf = false;

            AnimatorStateTransition back = g.AddTransition(move);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = ctrl;
        EditorGUIUtility.PingObject(ctrl);
        Debug.Log($"[손님 Animator] 만들었습니다: {path}");
    }

    // 프로젝트 안의 모든 애니메이션 클립 중에서 이름이 같은 것 찾기 (모델 파일 안에 든 클립 포함)
    private static AnimationClip FindClip(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
            {
                if (o is AnimationClip c && c.name == clipName && !c.name.StartsWith("__preview__"))
                    return c;
            }
        }
        return null;
    }
}
#endif
