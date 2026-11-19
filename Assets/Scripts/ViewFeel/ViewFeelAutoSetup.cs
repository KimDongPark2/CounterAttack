using UnityEngine;
using UnityEngine.SceneManagement;

// 씬이 열릴 때 Player에 WeaponFeel, 카메라에 CameraFeel이 없으면 자동으로 붙여 줌
// (실행 중에만 붙이는 거라 씬 파일은 안 바뀜. 기본값으로 동작)
// 인스펙터에서 값을 조절해서 저장하고 싶으면 에디터 메뉴 Tools > 카운터 어택 > 시점 효과 붙이기 로 미리 붙여 두기
public static class ViewFeelAutoSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    public static void Attach()
    {
        foreach (Player_Interaction pi in Object.FindObjectsByType<Player_Interaction>(FindObjectsSortMode.None))
        {
            if (pi.GetComponentInChildren<WeaponFeel>(true) == null)
                pi.gameObject.AddComponent<WeaponFeel>();

            Player_Look look = pi.GetComponentInParent<Player_Look>();
            Transform cam = look != null && look.playerCamera != null ? look.playerCamera
                          : (pi.playerCamera != null ? pi.playerCamera.transform : null);
            if (cam != null && pi.GetComponentInChildren<CameraFeel>(true) == null)
                cam.gameObject.AddComponent<CameraFeel>();
        }
    }
}
