using System.Collections;
using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>자막 표시. EventBus.ShowSubtitle(문구, 시간)을 받아 순서대로 화면 하단에 보여준다.</summary>
    public class SubtitleDisplay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float fadeTime = 0.25f;

        private readonly Queue<(string text, float seconds)> queue = new Queue<(string, float)>();
        private Coroutine routine;

        private void Awake() { if (group != null) group.alpha = 0f; }
        private void OnEnable() => EventBus.SubtitleRequested += Enqueue;
        private void OnDisable() => EventBus.SubtitleRequested -= Enqueue;

        private void Enqueue(string line, float seconds)
        {
            queue.Enqueue((line, seconds));
            if (routine == null) routine = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            while (queue.Count > 0)
            {
                var (line, seconds) = queue.Dequeue();
                text.text = line;
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(seconds);
                if (queue.Count == 0) yield return Fade(1f, 0f);
            }
            routine = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime))
            {
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
