using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 배경 스프라이트 교체용. Inspector에서 Sprite만 바꾸면
    /// Image와 AspectRatioFitter(Envelope Parent)의 비율이 자동으로 맞춰져 찌그러지지 않는다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image), typeof(AspectRatioFitter))]
    public class MenuBackground : MonoBehaviour
    {
        [SerializeField] private Sprite backgroundSprite;       // ← 버튼을 지운 배경 이미지로 교체
        [SerializeField] private Color tint = Color.white;

        /// <summary>현재 표시 중인 스프라이트</summary>
        public Sprite CurrentSprite => backgroundSprite;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void SetSprite(Sprite sprite)
        {
            backgroundSprite = sprite;
            Apply();
        }

        private void Apply()
        {
            var image = GetComponent<Image>();
            var fitter = GetComponent<AspectRatioFitter>();
            if (image == null || fitter == null) return;

            image.sprite = backgroundSprite;
            image.color = tint;
            image.preserveAspect = false;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (backgroundSprite != null && backgroundSprite.rect.height > 0f)
                fitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;
        }
    }
}
