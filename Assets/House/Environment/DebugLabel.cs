using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameGameplay
{
    /// <summary>
    /// 개발용 라벨(동 번호, 구역 이름 등). 플레이 카메라/빌드에는 어떤 경우에도 렌더링되지 않고,
    /// 에디터 Scene 뷰에서만 OnDrawGizmos + Handles.Label 로 보인다 (Tools > GameUI > Toggle Debug Labels 로 켜고 끔).
    /// 빌더는 TextMesh/World Space Canvas 대신 이 컴포넌트가 붙은 빈 오브젝트만 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugLabel : MonoBehaviour
    {
        public const string PrefKey = "GameUI.DebugLabels.Visible";

        [SerializeField] private string text = "label";
        [SerializeField] private Color color = new Color(1f, 0.92f, 0.3f);
        [SerializeField, Min(0f), Tooltip("Scene 뷰에서 이 거리(m) 안에서만 표시. 0 이면 항상")] private float maxViewDistance;

        public string Text => text;

        public void Set(string newText, Color newColor)
        {
            text = newText; color = newColor;
        }

        private void Awake()
        {
            // 혹시 이 오브젝트 아래에 렌더러(TextMesh 등)가 남아 있어도 게임 화면에는 나오지 않게 끈다
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
#if !UNITY_EDITOR
            gameObject.SetActive(false);
#endif
        }

#if UNITY_EDITOR
        public static bool ShowInSceneView
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        private void OnDrawGizmos()
        {
            if (!ShowInSceneView) return;
            if (maxViewDistance > 0f && SceneView.currentDrawingSceneView != null &&
                Vector3.Distance(SceneView.currentDrawingSceneView.camera.transform.position, transform.position) > maxViewDistance) return;
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color }, alignment = TextAnchor.MiddleCenter };
            Handles.Label(transform.position, text, style);
        }
#endif
    }
}
