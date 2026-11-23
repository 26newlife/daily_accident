using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// RectTransform 을 화면 세이프 에어리어(노치·둥근 모서리·콘솔 TV 오버스캔)에 맞춘다.
    /// HUD 루트에 붙여 두면 HUD 의 모든 앵커가 세이프 에어리어 안쪽 기준이 된다. 해상도·세이프 에어리어가 바뀌면 다시 계산한다.
    /// 일반 PC 창에서는 세이프 에어리어 = 전체 화면이라 아무 영향이 없다. (런타임에만 동작 — 에디터에서 앵커가 씬에 저장되지 않는다)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastSafe; private Vector2Int lastScreen;

        private void OnEnable() { if (Application.isPlaying) Apply(true); }
        private void Update() { if (Application.isPlaying) Apply(false); }

        private void Apply(bool force)
        {
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == lastSafe && size == lastScreen) return;
            lastSafe = safe; lastScreen = size;
            if (size.x <= 0 || size.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rt.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
