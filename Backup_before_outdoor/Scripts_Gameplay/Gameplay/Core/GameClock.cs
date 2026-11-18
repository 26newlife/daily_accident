using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 게임 내 시간(00:00부터). 실제 시간과의 비율을 Inspector / SetRatio 로 조정할 수 있다.
    /// 일시정지(timeScale 0) 중에는 흐르지 않는다. Act 로직은 ScheduleAt 으로 "게임 시간 N초에 실행"을 예약한다.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        [Header("시계")]
        [Tooltip("실제 1초당 흐르는 게임 초. 1 = 실시간, 60 = 실제 1초가 게임 1분")]
        [SerializeField, Min(0f)] private float gameSecondsPerRealSecond = 1f;
        [Tooltip("시작 시각(게임 초). 0 = 00:00")]
        [SerializeField, Min(0f)] private float startGameSeconds;
        [SerializeField] private bool running = true;

        private readonly List<(float time, Action action)> schedule = new List<(float, Action)>();
        private int lastMinute = -1;

        public static GameClock Instance { get; private set; }

        /// <summary>경과한 게임 시간(초)</summary>
        public float GameSeconds { get; private set; }
        /// <summary>플레이 시작 후 실제 경과 시간(초, 일시정지 제외) — 엔딩 화면용</summary>
        public float RealSeconds { get; private set; }
        public float Ratio => gameSecondsPerRealSecond;
        public bool Running => running;
        public string FormattedTime => Format(GameSeconds);

        /// <summary>게임 분이 바뀔 때</summary>
        public event Action<int> MinuteChanged;

        private void Awake()
        {
            Instance = this;
            GameSeconds = startGameSeconds;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            float dt = Time.deltaTime; // timeScale 0이면 0
            if (dt <= 0f) return;
            RealSeconds += dt;
            if (!running) return;

            GameSeconds += dt * gameSecondsPerRealSecond;

            int minute = Mathf.FloorToInt(GameSeconds / 60f);
            if (minute != lastMinute) { lastMinute = minute; MinuteChanged?.Invoke(minute); }

            for (int i = schedule.Count - 1; i >= 0; i--)
            {
                if (GameSeconds < schedule[i].time) continue;
                Action a = schedule[i].action;
                schedule.RemoveAt(i);
                a?.Invoke();
            }
        }

        public void SetRatio(float ratio) => gameSecondsPerRealSecond = Mathf.Max(0f, ratio);
        public void SetRunning(bool value) => running = value;

        /// <summary>게임 시간 gameSeconds 가 되면 action 실행</summary>
        public void ScheduleAt(float gameSeconds, Action action)
        {
            if (action != null) schedule.Add((gameSeconds, action));
        }

        public void ClearSchedule() => schedule.Clear();

        /// <summary>"MM:SS" (1시간 넘으면 "HH:MM:SS") 형식</summary>
        public static string Format(float seconds)
        {
            int s = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            int h = s / 3600, m = (s % 3600) / 60, sec = s % 60;
            return h > 0 ? $"{h:00}:{m:00}:{sec:00}" : $"{m:00}:{sec:00}";
        }
    }
}
