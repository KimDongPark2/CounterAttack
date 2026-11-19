using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 메뉴 (Tools > 카운터 어택)
//   시점 효과 붙이기  : 열린 씬의 Player에 WeaponFeel, Main Camera에 CameraFeel을 붙임 (그다음 Ctrl+S로 씬 저장)
//                       → 인스펙터에서 흔들림 값을 조절하고 저장하고 싶을 때만 쓰면 됨 (안 붙여도 실행하면 자동으로 붙음)
//   시점 설정 지우기  : 저장된 시점 모드 · 세기를 지워서 기본값(다이나믹 · 100%)으로
public static class ViewFeelSetupMenu
{
    [MenuItem("Tools/카운터 어택/시점 효과 붙이기")]
    private static void Setup()
    {
        int added = 0;
        foreach (Player_Interaction pi in Object.FindObjectsByType<Player_Interaction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(pi)) continue;   // 프리팹 파일 말고 씬에 있는 것만

            if (pi.GetComponentInChildren<WeaponFeel>(true) == null)
            {
                Undo.AddComponent<WeaponFeel>(pi.gameObject);
                added++;
            }

            Player_Look look = pi.GetComponentInParent<Player_Look>();
            Transform cam = look != null && look.playerCamera != null ? look.playerCamera
                          : (pi.playerCamera != null ? pi.playerCamera.transform : null);
            if (cam != null && pi.GetComponentInChildren<CameraFeel>(true) == null)
            {
                Undo.AddComponent<CameraFeel>(cam.gameObject);
                added++;
            }
            EditorSceneManager.MarkSceneDirty(pi.gameObject.scene);
        }

        string msg = added > 0
            ? $"컴포넌트 {added}개를 붙였어요. Ctrl+S로 씬을 저장하세요."
            : "이미 다 붙어 있거나, 씬에 Player_Interaction이 없어요.";
        EditorUtility.DisplayDialog("시점 효과 붙이기", msg, "확인");
    }

    [MenuItem("Tools/카운터 어택/시점 효과 붙이기", true)]
    private static bool SetupValidate() => !EditorApplication.isPlaying;

    // Player에 GunReload(탄약 · 재장전)를 붙임 (안 붙여도 실행하면 자동으로 붙음. 값을 저장하고 싶을 때만)
    [MenuItem("Tools/카운터 어택/재장전 붙이기")]
    private static void SetupReload()
    {
        int added = 0;
        foreach (Player_Interaction pi in Object.FindObjectsByType<Player_Interaction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(pi) || pi.GetComponent<GunReload>() != null) continue;
            Undo.AddComponent<GunReload>(pi.gameObject);
            EditorSceneManager.MarkSceneDirty(pi.gameObject.scene);
            added++;
        }
        EditorUtility.DisplayDialog("재장전 붙이기",
            added > 0 ? "GunReload를 붙였어요. Ctrl+S로 씬을 저장하세요." : "이미 붙어 있거나, 씬에 Player_Interaction이 없어요.", "확인");
    }

    [MenuItem("Tools/카운터 어택/재장전 붙이기", true)]
    private static bool SetupReloadValidate() => !EditorApplication.isPlaying;

    [MenuItem("Tools/카운터 어택/시점 설정 지우기")]
    private static void ClearPrefs()
    {
        PlayerPrefs.DeleteKey(ViewModeSettings.ModeKey);
        PlayerPrefs.DeleteKey(ViewModeSettings.IntensityKey);
        PlayerPrefs.Save();
        Debug.Log("[ViewFeel] 저장된 시점 모드 · 세기를 지웠어요. (기본값: 다이나믹 · 100%)");
    }
}
