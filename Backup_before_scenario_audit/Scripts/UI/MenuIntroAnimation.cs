using System;
using System.Collections;
using UnityEngine;

namespace GameUI
{
    /// <summary>
    /// 씬 시작 연출: 검은 화면 페이드인 + 버튼 위에서부터 순차 슬라이드인.
    /// 화면 페이드(FadeScreen)는 씬 전환 시 MainMenuController에서도 재사용한다.
    /// </summary>
    public class MenuIntroAnimation : MonoBehaviour
    {
        [Header("화면 페이드")]
        [SerializeField] private CanvasGroup screenFader;         // 전체 화면을 덮는 검은 Image의 CanvasGroup
        [SerializeField] private float fadeInDuration = 0.8f;

        [Header("버튼 순차 등장")]
        [SerializeField] private RectTransform[] items;           // 위에서 아래 순서
        [SerializeField] private float startDelay = 0.15f;        // 페이드 시작 후 첫 버튼까지 대기
        [SerializeField] private float itemInterval = 0.1f;       // 버튼 간 간격
        [SerializeField] private float itemDuration = 0.4f;       // 버튼 하나의 슬라이드 시간
        [SerializeField] private Vector2 slideOffset = new Vector2(-140f, 0f); // 시작 위치 오프셋

        [Header("옵션")]
        [SerializeField] private bool playOnStart = true;

        private Vector2[] targetPositions;
        private CanvasGroup[] itemGroups;

        public bool IsPlaying { get; private set; }
        public event Action Finished;

        private void Awake()
        {
            CacheItems();
            if (playOnStart) PrepareHidden();
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        private void CacheItems()
        {
            if (items == null) items = Array.Empty<RectTransform>();
            targetPositions = new Vector2[items.Length];
            itemGroups = new CanvasGroup[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                targetPositions[i] = items[i].anchoredPosition;
                itemGroups[i] = items[i].GetComponent<CanvasGroup>();
                if (itemGroups[i] == null) itemGroups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>첫 프레임이 보이기 전에 숨긴 상태로 만든다.</summary>
        private void PrepareHidden()
        {
            if (screenFader != null)
            {
                screenFader.alpha = 1f;
                screenFader.blocksRaycasts = true;
            }
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                items[i].anchoredPosition = targetPositions[i] + slideOffset;
                itemGroups[i].alpha = 0f;
            }
        }

        public void Play()
        {
            StopAllCoroutines();
            PrepareHidden();
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            StartCoroutine(FadeScreen(0f, fadeInDuration));

            yield return new WaitForSecondsRealtime(startDelay);

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) StartCoroutine(SlideIn(i));
                yield return new WaitForSecondsRealtime(itemInterval);
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, itemDuration - itemInterval));
            IsPlaying = false;
            Finished?.Invoke();
        }

        private IEnumerator SlideIn(int index)
        {
            RectTransform rt = items[index];
            CanvasGroup cg = itemGroups[index];
            Vector2 from = targetPositions[index] + slideOffset;
            Vector2 to = targetPositions[index];

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, itemDuration);
                float e = EaseOutCubic(Mathf.Clamp01(t));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                cg.alpha = Mathf.Clamp01(t * 1.6f);
                yield return null;
            }
            rt.anchoredPosition = to;
            cg.alpha = 1f;
        }

        /// <summary>화면 페이드. to=1이면 검게, to=0이면 밝게.</summary>
        public IEnumerator FadeScreen(float to, float duration)
        {
            if (screenFader == null) yield break;
            screenFader.blocksRaycasts = true; // 페이드 중 입력 차단
            float from = screenFader.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
                screenFader.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            screenFader.alpha = to;
            screenFader.blocksRaycasts = to > 0.01f;
        }

        private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    }
}
