using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// RawImage의 UV를 천천히 스크롤해서 연기가 흐르는 느낌을 낸다.
    /// 텍스처 Wrap Mode는 Repeat이어야 한다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UVScroller : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.01f, 0.004f); // UV/초
        [SerializeField] private Vector2 tiling = new Vector2(1.5f, 1f);
        [Header("투명도 숨쉬기 (선택)")]
        [SerializeField] private float baseAlpha = 0.25f;
        [SerializeField] private float alphaVariation = 0.06f;
        [SerializeField] private float alphaSpeed = 0.15f;

        private RawImage rawImage;
        private Vector2 offset;
        private float phase;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            phase = Random.value * 10f;
        }

        private void Update()
        {
            offset += scrollSpeed * Time.unscaledDeltaTime;
            offset.x = Mathf.Repeat(offset.x, 1f);
            offset.y = Mathf.Repeat(offset.y, 1f);
            rawImage.uvRect = new Rect(offset, tiling);

            Color c = rawImage.color;
            c.a = baseAlpha + Mathf.Sin((Time.unscaledTime + phase) * alphaSpeed * Mathf.PI * 2f) * alphaVariation;
            rawImage.color = c;
        }
    }
}
