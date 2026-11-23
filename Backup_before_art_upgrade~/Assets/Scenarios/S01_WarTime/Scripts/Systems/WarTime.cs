using System;
using GameCore;

namespace GameGameplay
{
    /// <summary>시나리오 1 타이머 헬퍼: GameClock.ScheduleAt 위에서 "지금부터 N초 뒤" 를 예약한다 (런 ID·일시정지 안전, 코루틴/Invoke 사용 안 함)</summary>
    public static class WarTime
    {
        public static float Now => GameClock.Instance != null ? GameClock.Instance.GameSeconds : 0f;

        public static void After(float seconds, Action action, string label = null)
        {
            if (GameClock.Instance == null || action == null) return;
            GameClock.Instance.ScheduleAt(Now + seconds, action, label);
        }

        /// <summary>자막 여러 줄을 순서대로 보여준다. 각 줄 길이 = 글자 수에 비례 (최소 secondsPerLine)</summary>
        public static float Sequence(string[] lines, float startDelay, float secondsPerLine, Action onEnd = null)
        {
            float t = startDelay;
            if (lines != null)
                foreach (string line in lines)
                {
                    string l = line; float len = Math.Max(secondsPerLine, l.Length * 0.09f);
                    After(t, () => EventBus.ShowSubtitle(l, len));
                    t += len + 0.4f;
                }
            if (onEnd != null) After(t, onEnd);
            return t;
        }
    }
}
