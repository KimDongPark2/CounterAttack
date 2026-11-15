using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Intro 설정 창 버그 고치기 (Intro 씬을 열어둔 채로)
// 메뉴: Tools → 버그 수정 → 설정 뒤로 버튼
// 설정 창의 뒤로 버튼이 SettingsCanvas가 아니라 안쪽 Settings를 꺼서,
// 두 번째로 설정에 들어가면 빈 화면(하늘)만 보이던 문제 → 뒤로 버튼이 SettingsCanvas를 끄게 바꿈
public static class IntroSettingsFix
{
    [MenuItem("Tools/버그 수정/설정 뒤로 버튼")]
    private static void Fix()
    {
        GameObject canvas = null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == "SettingsCanvas" && go.scene.IsValid()) canvas = go;
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("버그 수정", "Intro 씬을 열고 다시 눌러주세요.\n(SettingsCanvas를 못 찾았어요)", "확인");
            return;
        }

        Transform inner = canvas.transform.Find("Settings");
        int fixedCount = 0;
        foreach (Button b in canvas.GetComponentsInChildren<Button>(true))
        {
            var so = new SerializedObject(b);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty target = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target");
                if (inner != null && target.objectReferenceValue == inner.gameObject)
                {
                    target.objectReferenceValue = canvas;
                    fixedCount++;
                }
            }
            so.ApplyModifiedProperties();
        }

        if (fixedCount == 0)
        {
            EditorUtility.DisplayDialog("버그 수정", "고칠 게 없어요 (이미 고쳐져 있음).", "확인");
            return;
        }
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorUtility.DisplayDialog("버그 수정", $"뒤로 버튼 {fixedCount}곳을 SettingsCanvas를 끄게 바꿨어요.\n씬을 저장(Ctrl+S)하면 끝이에요.", "확인");
    }

    // 게임 끝 영수증의 "재시도(RETRY)" 줄이 타이틀로 가던 것 → 다시 하기(Retry)로 (Game 씬을 열어둔 채로)
    [MenuItem("Tools/버그 수정/게임 끝 재시도 버튼")]
    private static void FixRetry()
    {
        int fixedCount = 0;
        Scene? scene = null;
        foreach (IntroMenuRow row in Object.FindObjectsByType<IntroMenuRow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isRetry = false;
            foreach (TMP_Text t in row.GetComponentsInChildren<TMP_Text>(true))
                if (t.text.Contains("RETRY") || t.text.Contains("재시도") || t.text.Contains("다시")) isRetry = true;
            if (!isRetry) continue;

            var so = new SerializedObject(row);
            SerializedProperty calls = so.FindProperty("onChoose.m_PersistentCalls.m_Calls");
            if (calls == null) continue;
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty method = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_MethodName");
                if (method.stringValue == "GoTitle")
                {
                    method.stringValue = "Retry";
                    fixedCount++;
                }
            }
            so.ApplyModifiedProperties();
            scene = row.gameObject.scene;
        }

        if (fixedCount == 0)
        {
            EditorUtility.DisplayDialog("버그 수정", "고칠 게 없어요.\nGame 씬을 열었는지 확인해주세요 (이미 고쳐져 있을 수도 있어요).", "확인");
            return;
        }
        EditorSceneManager.MarkSceneDirty(scene.Value);
        EditorUtility.DisplayDialog("버그 수정", "재시도 줄이 이제 같은 사람으로 다시 하기(Retry)를 해요.\n씬을 저장(Ctrl+S)하면 끝이에요.", "확인");
    }
}
