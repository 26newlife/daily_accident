using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>화면 전체를 번쩍이게 하는 연출 (폭발 섬광 등). ScreenFlash.Instance.Flash(...)</summary>
    public class ScreenFlash : MonoBehaviour
    {
        [SerializeField] private Image image;

        public static ScreenFlash Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (image != null) image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>peakAlpha 까지 즉시 밝아졌다가 fadeSeconds 동안 사라진다</summary>
        public void Flash(Color color, float peakAlpha, float fadeSeconds)
        {
            if (image == null) return;
            StopAllCoroutines();
            StartCoroutine(Run(color, peakAlpha, fadeSeconds));
        }

        private IEnumerator Run(Color color, float peak, float fade)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fade))
            {
                image.color = new Color(color.r, color.g, color.b, Mathf.Lerp(peak, 0f, t * t));
                yield return null;
            }
            image.color = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
