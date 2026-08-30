using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ConvenienceStore.Tablet
{
    /// <summary>
    /// World Space Canvas 를 태블릿 모델의 "액정 면"에 정확히 맞춰 준다.
    ///
    /// Body / Screen 매터리얼을 나눠 둔 모델이라면, Screen 매터리얼이 붙은 서브메시의
    /// 실제 크기·위치·방향을 읽어서 Canvas 의 position / rotation / sizeDelta / scale 을
    /// 계산해 준다. 눈대중으로 맞추다 베젤 위로 삐져나오는 문제가 사라진다.
    ///
    /// 사용법
    ///   1. 태블릿 3D 모델(MeshRenderer 가 있는 오브젝트)에 이 컴포넌트를 붙인다.
    ///   2. screenMaterial 에 화면용 매터리얼을, canvasRect 에 ScreenCanvas 를 넣는다.
    ///   3. 컴포넌트 헤더 우클릭 → "화면에 맞추기".
    ///
    /// 주의: 메시의 Read/Write Enabled 가 켜져 있어야 한다.
    /// (모델 에셋 선택 → Model 탭 → Read/Write Enabled 체크 → Apply)
    /// </summary>
    [ExecuteAlways]
    public class TabletScreenFitter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비워 두면 이 오브젝트의 Renderer 를 쓴다")]
        [SerializeField] private Renderer screenRenderer;

        [Tooltip("화면(액정)에 칠해 둔 매터리얼. 이 매터리얼이 붙은 서브메시를 찾는다.")]
        [SerializeField] private Material screenMaterial;

        [Tooltip("맞출 World Space Canvas 의 RectTransform")]
        [SerializeField] private RectTransform canvasRect;

        [Header("맞추기 설정")]
        [Tooltip("Canvas 의 가상 세로 해상도. 이 값이 클수록 글자가 선명해진다.")]
        [SerializeField, Min(64f)] private float referenceHeight = 640f;

        [Tooltip("액정 표면에서 띄울 거리(m). z-fighting 방지. 베젤이 튀어나온 모델이면 베젤 높이보다 작게.")]
        [SerializeField] private float surfaceOffset = 0.0005f;

        [Tooltip("베젤 안쪽으로 더 줄일 여백(m). 0.001~0.003 정도 주면 안전하다.")]
        [SerializeField] private float inset = 0.001f;

        [Header("방향이 이상할 때")]
        [Tooltip("화면이 뒷면을 보고 있으면 체크")]
        [SerializeField] private bool flipNormal;

        [Tooltip("가로/세로가 뒤바뀌었으면 체크")]
        [SerializeField] private bool swapAxes;

        [ContextMenu("화면에 맞추기")]
        public void Fit()
        {
            var rend = screenRenderer != null ? screenRenderer : GetComponent<Renderer>();
            if (rend == null)
            {
                Debug.LogError("[TabletScreenFitter] Renderer 를 찾을 수 없습니다.", this);
                return;
            }
            if (canvasRect == null)
            {
                Debug.LogError("[TabletScreenFitter] canvasRect 가 비어 있습니다.", this);
                return;
            }

            var filter = rend.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                Debug.LogError("[TabletScreenFitter] MeshFilter / Mesh 를 찾을 수 없습니다.", this);
                return;
            }
            if (!mesh.isReadable)
            {
                Debug.LogError(
                    "[TabletScreenFitter] 메시를 읽을 수 없습니다. 모델 에셋 선택 → Model 탭 → " +
                    "Read/Write Enabled 체크 → Apply 후 다시 시도하세요.", this);
                return;
            }

            // --- 1. 화면 매터리얼이 붙은 서브메시 찾기 -------------------------------
            int sub = -1;
            var mats = rend.sharedMaterials;
            if (screenMaterial != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == screenMaterial) { sub = i; break; }
                }
                if (sub < 0)
                {
                    Debug.LogError($"[TabletScreenFitter] '{screenMaterial.name}' 매터리얼이 이 Renderer 에 없습니다.", this);
                    return;
                }
            }
            else if (mesh.subMeshCount == 1)
            {
                sub = 0; // 서브메시가 하나뿐이면 그냥 그것
            }
            else
            {
                Debug.LogError("[TabletScreenFitter] screenMaterial 을 지정하세요.", this);
                return;
            }

            // --- 2. 그 서브메시만의 로컬 바운즈 계산 ---------------------------------
            int[] tris = mesh.GetTriangles(sub);
            if (tris.Length == 0)
            {
                Debug.LogError("[TabletScreenFitter] 해당 서브메시에 삼각형이 없습니다.", this);
                return;
            }
            Vector3[] verts = mesh.vertices;
            var bounds = new Bounds(verts[tris[0]], Vector3.zero);
            for (int i = 1; i < tris.Length; i++) bounds.Encapsulate(verts[tris[i]]);

            // --- 3. 가장 얇은 축 = 화면의 법선 ---------------------------------------
            Vector3 size = bounds.size;
            int nAxis = 0;
            if (size.y <= size.x && size.y <= size.z) nAxis = 1;
            else if (size.z <= size.x && size.z <= size.y) nAxis = 2;

            int aUp, aRight;
            switch (nAxis)
            {
                case 0: aUp = 1; aRight = 2; break;   // 법선 X → 세로 Y, 가로 Z
                case 1: aUp = 2; aRight = 0; break;   // 법선 Y → 세로 Z, 가로 X
                default: aUp = 1; aRight = 0; break;  // 법선 Z → 세로 Y, 가로 X
            }
            if (swapAxes) { int tmp = aUp; aUp = aRight; aRight = tmp; }

            Vector3 nLocal = Axis(nAxis);
            Vector3 upLocal = Axis(aUp);
            Vector3 rightLocal = Axis(aRight);

            // --- 4. 월드 공간으로 변환 (부모 스케일까지 반영) -------------------------
            Transform t = rend.transform;
            Vector3 worldCenter = t.TransformPoint(bounds.center);
            Vector3 worldNormal = t.TransformDirection(nLocal).normalized * (flipNormal ? -1f : 1f);
            Vector3 worldUp = t.TransformDirection(upLocal).normalized;

            float worldHeight = t.TransformVector(upLocal * size[aUp]).magnitude - inset * 2f;
            float worldWidth = t.TransformVector(rightLocal * size[aRight]).magnitude - inset * 2f;

            if (worldHeight <= 0f || worldWidth <= 0f)
            {
                Debug.LogError("[TabletScreenFitter] inset 이 화면 크기보다 큽니다. 값을 줄이세요.", this);
                return;
            }

#if UNITY_EDITOR
            Undo.RecordObject(canvasRect, "Fit Tablet Screen Canvas");
#endif
            // --- 5. Canvas 에 적용 ---------------------------------------------------
            canvasRect.SetPositionAndRotation(
                worldCenter + worldNormal * surfaceOffset,
                Quaternion.LookRotation(worldNormal, worldUp));

            float worldScale = worldHeight / referenceHeight;

            // 부모에 스케일이 걸려 있으면 그만큼 나눠 준다.
            float parentScale = 1f;
            if (canvasRect.parent != null)
            {
                Vector3 ls = canvasRect.parent.lossyScale;
                parentScale = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
                if (Mathf.Approximately(parentScale, 0f)) parentScale = 1f;
            }

            canvasRect.localScale = Vector3.one * (worldScale / parentScale);
            canvasRect.sizeDelta = new Vector2(worldWidth / worldScale, referenceHeight);

#if UNITY_EDITOR
            EditorUtility.SetDirty(canvasRect);
#endif
            Debug.Log(
                $"[TabletScreenFitter] 화면 크기 {worldWidth:F4}m × {worldHeight:F4}m · " +
                $"Canvas {canvasRect.sizeDelta.x:F0}×{canvasRect.sizeDelta.y:F0} · " +
                $"Scale {canvasRect.localScale.x:F6}", this);
        }

        private static Vector3 Axis(int i) =>
            i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);

        // 씬 뷰에서 인식된 화면 영역을 노란 사각형으로 그려 준다.
        private void OnDrawGizmosSelected()
        {
            if (canvasRect == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = canvasRect.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(canvasRect.sizeDelta.x, canvasRect.sizeDelta.y, 0f));
        }
    }
}
