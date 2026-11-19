using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 호흡기 체력에 따른 화면 효과: ① 시야 흐려짐(연기/안개 오버레이) ② 화면 가장자리 붉은빛(비네팅).
    /// 비네팅 스프라이트는 실행 시 코드로 생성하므로 별도 이미지 에셋이 필요 없다.
    /// </summary>
    public class VitalsScreenEffect : MonoBehaviour
    {
        [SerializeField] private PlayerVitals vitals;
        [SerializeField] private Image fogOverlay;
        [SerializeField] private Image redVignette;
        [SerializeField] private Color fogColor = new Color(0.55f, 0.53f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] private float maxFogAlpha = 0.75f;
        [SerializeField, Range(0f, 1f)] private float maxVignetteAlpha = 0.9f;
        [SerializeField, Tooltip("이 비율 이하부터 효과 시작")] private float startBelowRatio = 0.8f;
        [SerializeField] private float smoothing = 5f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.15f;

        private float fogAlpha, vignetteAlpha;

        private void Awake()
        {
            if (redVignette != null && redVignette.sprite == null) redVignette.sprite = CreateVignetteSprite(128);
            if (fogOverlay != null) fogOverlay.color = WithAlpha(fogColor, 0f);
            if (redVignette != null) redVignette.color = new Color(0.8f, 0.05f, 0.05f, 0f);
        }

        private void Update()
        {
            if (vitals == null) return;

            // 0(정상) ~ 1(위험): startBelowRatio 에서 0, 체력 0에서 1
            float danger = Mathf.Clamp01((startBelowRatio - vitals.Normalized) / Mathf.Max(0.01f, startBelowRatio));
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount * danger;

            fogAlpha = Mathf.Lerp(fogAlpha, danger * maxFogAlpha, Time.unscaledDeltaTime * smoothing);
            float burnDanger = vitals.Burn01;   // 화상 수치가 높을수록 붉은 비네팅 (비그래픽)
            float burnPulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed * 1.5f) * pulseAmount * burnDanger;
            float vignetteTarget = Mathf.Max(Mathf.Clamp01(danger * pulse), Mathf.Clamp01(burnDanger * burnPulse));
            vignetteAlpha = Mathf.Lerp(vignetteAlpha, vignetteTarget * maxVignetteAlpha, Time.unscaledDeltaTime * smoothing);

            if (fogOverlay != null) fogOverlay.color = WithAlpha(fogColor, fogAlpha);
            if (redVignette != null) { Color c = redVignette.color; c.a = vignetteAlpha; redVignette.color = c; }
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>가운데는 투명, 가장자리로 갈수록 불투명한 정사각형 스프라이트</summary>
        public static Sprite CreateVignetteSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VignetteGenerated" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (size - 1f)) * 2f - 1f;
                    float dy = (y / (size - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
