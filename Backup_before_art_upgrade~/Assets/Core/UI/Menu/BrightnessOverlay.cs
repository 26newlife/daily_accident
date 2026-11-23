using GameCore;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 밝기 설정용 전체 화면 오버레이. GameSettings.Brightness에 따라
    /// 1 미만이면 검은색, 1 초과면 흰색 반투명 막을 덮는다.
    /// 씬 설정 없이 게임 시작 시 자동 생성되며(DontDestroyOnLoad) 모든 씬에서 유지된다.
    /// </summary>
    public class BrightnessOverlay : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float maxDarken = 0.5f;   // 밝기 최소일 때 검은 막 알파
        [SerializeField, Range(0f, 1f)] private float maxBrighten = 0.25f; // 밝기 최대일 때 흰 막 알파

        private static BrightnessOverlay instance;
        private Image image;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (instance != null) return;

            var go = new GameObject("BrightnessOverlay", typeof(Canvas));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // 모든 UI 위

            var imgGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = imgGo.GetComponent<Image>();
            img.raycastTarget = false; // 클릭을 막지 않는다
            img.color = Color.clear;

            instance = go.AddComponent<BrightnessOverlay>();
            instance.image = img;
            instance.Refresh(); // AddComponent 시점의 OnEnable에서는 image가 아직 없었으므로 다시 반영
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void OnEnable()
        {
            GameSettings.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => GameSettings.OnChanged -= Refresh;

        private void Refresh()
        {
            if (image == null) return;
            float b = GameSettings.Brightness;
            if (b < 1f)
            {
                float k = Mathf.InverseLerp(1f, GameSettings.BrightnessMin, b);
                image.color = new Color(0f, 0f, 0f, k * maxDarken);
            }
            else
            {
                float k = Mathf.InverseLerp(1f, GameSettings.BrightnessMax, b);
                image.color = new Color(1f, 1f, 1f, k * maxBrighten);
            }
        }
    }
}
