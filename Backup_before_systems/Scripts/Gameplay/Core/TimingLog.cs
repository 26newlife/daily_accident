using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 플레이 타임 로그. 각 Act 시작/종료와 주요 사건(방송, 폭발, 화재 시작, 클리어)의 시각을
    /// Console 에 "[Timing] Act2 start 01:42" 형식으로 남긴다. 에디터와 Development Build 에서만 동작하고 릴리스 빌드에서는 제거된다.
    /// 시각은 일시정지를 제외한 실제 플레이 경과 시간(GameClock.RealSeconds) 기준이다.
    /// </summary>
    public static class TimingLog
    {
        public struct Entry { public string label; public float seconds; }

        private static readonly List<Entry> entries = new List<Entry>();

        public static IReadOnlyList<Entry> Entries => entries;
        public static string CurrentAct { get; private set; } = "-";

        public static float Now => GameClock.Instance != null ? GameClock.Instance.RealSeconds : 0f;

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Mark(string label)
        {
            float t = Now;
            entries.Add(new Entry { label = label, seconds = t });
            if (label.EndsWith(" start")) CurrentAct = label.Substring(0, label.Length - " start".Length);
            if (GameTimingConfig.Current.logTimings) Debug.Log($"[Timing] {label} {GameClock.Format(t)}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { entries.Clear(); CurrentAct = "-"; }
    }
}
