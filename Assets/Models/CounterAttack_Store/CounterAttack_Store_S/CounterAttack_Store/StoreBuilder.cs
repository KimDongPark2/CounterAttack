using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 편의점 조각들을 정한 크기대로 자동 배치하는 도구
// 1. 빈 오브젝트에 붙이고 아래 조각 칸에 Project 창의 OBJ들을 끌어다 넣기
// 2. 가로/세로 칸 수, 문 위치 정하기
// 3. 컴포넌트 이름 오른쪽 점 세 개(⋮) → "편의점 만들기"
public class StoreBuilder : MonoBehaviour
{
    [Header("크기 (한 칸 = 4m)")]
    [Min(1)] public int width = 3;    // 가로 칸 수 (3이면 12m)
    [Min(1)] public int depth = 2;    // 세로 칸 수 (2면 8m)
    [Tooltip("앞벽에서 자동문이 들어갈 칸 번호 (왼쪽부터 0, 1, 2 ...)")]
    public int doorIndex = 1;
    [Tooltip("양옆 벽의 맨 앞 칸을 창문으로")]
    public bool sideWindows = true;
    [Tooltip("입구 앞 차양과 바깥 데크")]
    public bool frontCanopy = true;
    [Tooltip("바닥과 데크에 붙일 태그 (물건 설치 가능 바닥)")]
    public string installTag = "InstallAvailable";

    [Header("조각 (Project 창의 OBJ를 끌어다 넣기)")]
    public GameObject wallSolid;
    public GameObject wallWindow;
    public GameObject wallDoor;
    public GameObject pillar;
    public GameObject pillarShort;
    public GameObject fascia;
    public GameObject roof;
    public GameObject floor;
    public GameObject canopy;
    public GameObject deck;

    private const float CELL = 4f;

    [ContextMenu("편의점 만들기")]
    public void Build()
    {
        Clear();

        float xl = -width * CELL / 2f;
        float xr = width * CELL / 2f;
        float zb = depth * CELL;

        // 앞벽, 간판 띠, 차양, 데크, 뒷벽, 바닥, 지붕
        for (int i = 0; i < width; i++)
        {
            float x = xl + CELL / 2f + CELL * i;
            Place(i == doorIndex ? wallDoor : wallWindow, x, 0f, 0f, "Front");
            Place(fascia, x, 0f, 0f, "Fascia");
            if (frontCanopy)
            {
                Place(canopy, x, 0f, 0f, "Canopy");
                Tag(Place(deck, x, 0f, 0f, "Deck"));
            }
            Place(wallSolid, x, zb, 180f, "Back");

            for (int j = 0; j < depth; j++)
            {
                float z = CELL / 2f + CELL * j;
                Tag(Place(floor, x, z, 0f, "Floor"));
                Place(roof, x, z, 0f, "Roof");
            }
        }

        // 양옆 벽 (바깥쪽을 보도록 회전)
        for (int j = 0; j < depth; j++)
        {
            float z = CELL / 2f + CELL * j;
            GameObject side = (sideWindows && j == 0) ? wallWindow : wallSolid;
            Place(side, xl, z, 90f, "Left");
            Place(side, xr, z, -90f, "Right");
        }

        // 기둥: 앞쪽은 간판 높이, 뒤쪽 모서리는 지붕 높이
        for (int k = 0; k <= width; k++) Place(pillar, xl + CELL * k, 0f, 0f, "Pillar");
        Place(pillarShort, xl, zb, 0f, "Pillar");
        Place(pillarShort, xr, zb, 0f, "Pillar");
    }

    [ContextMenu("만든 것 지우기")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(child);
#else
            Destroy(child);
#endif
        }
    }

    private GameObject Place(GameObject prefab, float x, float z, float rotY, string group)
    {
        if (prefab == null) return null;

        Transform parent = GetGroup(group);
        GameObject go;
#if UNITY_EDITOR
        go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "편의점 만들기");
#else
        go = Instantiate(prefab, parent);
#endif
        go.transform.localPosition = new Vector3(x, 0f, z);
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        return go;
    }

    // 바닥/데크와 그 안의 모든 오브젝트에 설치 가능 태그 붙이기
    private void Tag(GameObject go)
    {
        if (go == null || string.IsNullOrEmpty(installTag)) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            try { t.gameObject.tag = installTag; }
            catch
            {
                Debug.LogWarning($"[StoreBuilder] '{installTag}' 태그가 없어서 붙이지 못했습니다.", this);
                return;
            }
        }
    }

    private Transform GetGroup(string name)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(t.gameObject, "편의점 만들기");
#endif
        }
        return t;
    }
}
