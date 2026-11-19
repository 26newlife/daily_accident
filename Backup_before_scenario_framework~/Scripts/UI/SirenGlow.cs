using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 소방차 경광등 느낌: 붉은/푸른 글로우 Image를 교대로 페이드.
    /// Image에 Additive 머티리얼(GameUI/UIAdditive)을 쓰면 더 자연스럽다.
    /// </summary>
    public class SirenGlow : MonoBehaviour
    {
        [SerializeField] private Image redGlow;
        [SerializeField] private Image blueGlow;
        [SerializeField] private Color redColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color blueColor = new Color(0.15f, 0.35f, 1f, 1f);
        [SerializeField] private float cycleDuration = 1.2f;         // 붉은→푸른 한 주기(초)
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.02f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.35f;
        [SerializeField, Range(1f, 8f)] private float sharpness = 3f; // 클수록 깜빡임이 짧고 날카로움

        private void Update()
        {
            float t = Time.unscaledTime / Mathf.Max(0.01f, cycleDuration);
            Set(redGlow, redColor, Pulse(t));
            Set(blueGlow, blueColor, Pulse(t + 0.5f));
        }

        private float Pulse(float t)
        {
            float s = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
            return Mathf.Lerp(minAlpha, maxAlpha, Mathf.Pow(s, sharpness));
        }

        private static void Set(Image img, Color baseColor, float alpha)
        {
            if (img == null) return;
            baseColor.a = alpha;
            img.color = baseColor;
        }
    }
}
