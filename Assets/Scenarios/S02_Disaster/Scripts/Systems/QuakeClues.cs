using System;
using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 단계별로 정해진 시간에 단서(재난문자, 상황 자막, 냄새)를 띄운다 (원본 ClueSchedule 이식).
    /// 정답을 직접 알려주지 않고 상황만 묘사한다. 이미 조치한 것의 단서는 띄우지 않는다(가스를 잠갔으면 냄새가 나지 않음).
    /// 재난문자는 스마트폰(Tab) 문자함에도 남는다.
    /// </summary>
    public class QuakeClues : MonoBehaviour
    {
        public enum ClueKind { Subtitle = 0, Alert = 1, Scent = 2 }

        [Serializable]
        public struct Clue
        {
            public QuakePhase phase;
            public float delay;
            public ClueKind kind;
            [TextArea] public string text;
            [Tooltip("이 조치가 이미 완료돼 있으면 띄우지 않는다")] public string skipIfDone;
            [Tooltip("단서와 함께 재생할 소리 / 위치 (선택)")] public AudioClip sound;
            public Vector3 soundAt;
            [Tooltip("불꽃 튀는 효과 위치 (y < -100 이면 없음)")] public Vector3 sparkAt;
        }

        [SerializeField] private Clue[] clues = new Clue[0];

        /// <summary>문구 자리표시: {M} 이번 판 본진 규모, {TS} 지진해일 경보/주의보, {DMG} 규모별 본진 뒤 안내</summary>
        public static string Fill(string text, QuakeTimeline tl)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 || tl == null) return text;
            return text
                .Replace("{M}", tl.Magnitude.ToString("0.0"))
                .Replace("{TS}", tl.IsStrong ? "경보" : "주의보")
                .Replace("{DMG}", tl.IsStrong
                    ? "강한 흔들림으로 건물이 손상됐을 수 있음. 위층·옥상으로 가지 말고 건물 밖 넓은 곳으로 대피 바랍니다"
                    : "진동 멈춘 후 가까운 옥외대피장소로 대피하며 여진 주의");
        }
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private float alertSeconds = 8f, subtitleSeconds = 4.5f;

        public void Hook()
        {
            if (QuakeTimeline.Instance != null) { QuakeTimeline.Instance.PhaseChanged -= OnPhase; QuakeTimeline.Instance.PhaseChanged += OnPhase; }
        }

        private void OnDestroy() { if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase; }

        private void OnPhase(QuakePhase phase)
        {
            foreach (Clue c in clues) if (c.phase == phase) StartCoroutine(ShowLater(c));
        }

        private IEnumerator ShowLater(Clue c)
        {
            yield return new WaitForSeconds(c.delay);
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || tl.Phase != c.phase || GameFlow.IsEnded) yield break;
            if (!string.IsNullOrEmpty(c.skipIfDone) && QuakeChecklist.Instance != null && QuakeChecklist.Instance.IsDone(c.skipIfDone)) yield break;
            switch (c.kind)
            {
                case ClueKind.Alert:
                    string alert = Fill(c.text, tl);
                    EventBus.ShowSubtitle(alert, alertSeconds);
                    QuakeFx.Play2D(alertSound, 0.6f);
                    PhoneUI phone = FindFirstObjectByType<PhoneUI>(FindObjectsInactive.Include);
                    if (phone != null) phone.AddMessage("긴급재난문자", alert.Replace("[긴급재난문자] ", ""));
                    break;
                case ClueKind.Scent: EventBus.ShowScent(Fill(c.text, tl)); break;
                default: EventBus.ShowSubtitle(Fill(c.text, tl), subtitleSeconds); break;
            }
            if (c.sound != null) QuakeFx.PlayAt(c.sound, c.soundAt, 0.8f, 0.05f);
            if (c.sparkAt.y > -100f && QuakeFx.Instance != null)
            {
                var spark = new GameObject("Spark").transform;
                spark.position = c.sparkAt;
                QuakeFx.Burst(spark, QuakeFx.Instance.Fire, new Color(1f, 0.85f, 0.4f), 0.35f, 3f, 0.05f, 1.5f, 0.05f, 30);
                Destroy(spark.gameObject, 3f);
            }
        }
    }
}
