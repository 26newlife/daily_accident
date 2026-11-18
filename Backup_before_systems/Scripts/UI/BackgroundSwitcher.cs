using System.Collections;
using UnityEngine;

namespace GameUI
{
    /// <summary>
    /// 메인 배경 크로스페이드 교체. 배경 Image 2장(현재 / 다음)을 겹쳐 두고
    /// 다음 이미지의 알파를 0 → 1로 올린 뒤, 현재 이미지를 새 스프라이트로 확정한다.
    /// 스프라이트가 null이면 아무것도 하지 않고 기존 배경을 유지한다.
    /// </summary>
    public class BackgroundSwitcher : MonoBehaviour
    {
        [Header("배경 Image 2장 (다음 이미지가 위에 그려지도록 하이어라키 아래쪽에 배치)")]
        [SerializeField] private MenuBackground currentBackground;
        [SerializeField] private MenuBackground nextBackground;
        [SerializeField] private float crossfadeDuration = 0.6f;

        private CanvasGroup currentGroup;
        private CanvasGroup nextGroup;
        private Coroutine fadeRoutine;
        private Sprite pendingSprite;

        private void Awake()
        {
            currentGroup = EnsureGroup(currentBackground);
            nextGroup = EnsureGroup(nextBackground);
            if (currentGroup != null) currentGroup.alpha = 1f;
            if (nextGroup != null) nextGroup.alpha = 0f;
        }

        /// <param name="sprite">null이면 무시 (기존 배경 유지)</param>
        /// <param name="instant">true면 페이드 없이 즉시 교체</param>
        public void SwitchTo(Sprite sprite, bool instant = false)
        {
            if (sprite == null || currentBackground == null) return;

            Sprite displayed = fadeRoutine != null ? pendingSprite : currentBackground.CurrentSprite;
            if (displayed == sprite) return;

            // 진행 중인 페이드가 있으면 그 결과를 먼저 확정
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
                Commit(pendingSprite);
            }

            if (instant || nextBackground == null || crossfadeDuration <= 0f || !isActiveAndEnabled)
            {
                Commit(sprite);
                return;
            }

            pendingSprite = sprite;
            fadeRoutine = StartCoroutine(Crossfade(sprite));
        }

        private IEnumerator Crossfade(Sprite sprite)
        {
            nextBackground.SetSprite(sprite);
            nextGroup.alpha = 0f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / crossfadeDuration;
                nextGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                yield return null;
            }

            Commit(sprite);
            fadeRoutine = null;
        }

        /// <summary>현재 배경을 sprite로 확정하고 다음 배경은 숨긴다</summary>
        private void Commit(Sprite sprite)
        {
            currentBackground.SetSprite(sprite);
            if (currentGroup != null) currentGroup.alpha = 1f;
            if (nextGroup != null) nextGroup.alpha = 0f;
        }

        private static CanvasGroup EnsureGroup(MenuBackground bg)
        {
            if (bg == null) return null;
            CanvasGroup g = bg.GetComponent<CanvasGroup>();
            return g != null ? g : bg.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
