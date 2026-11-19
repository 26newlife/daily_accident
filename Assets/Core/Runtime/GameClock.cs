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
        [SerializeField, Tooltip("전체 템포/시간 설정 (Assets/Data/GameTimingConfig.asset). 비워도 기본값으로 동작")] private GameTimingConfig timing;

        private struct Timer { public float time; public Action action; public int runId; public string label; }
        private readonly List<Timer> schedule = new List<Timer>();
        private readonly List<Timer> due = new List<Timer>();
        private int lastMinute = -1;

        public static GameClock Instance { get; private set; }

        /// <summary>경과한 게임 시간(초)</summary>
        public float GameSeconds { get; private set; }
        /// <summary>플레이 시작 후 실제 경과 시간(초, 일시정지 제외) — 엔딩 화면용</summary>
        public float RealSeconds { get; private set; }
        public float Ratio => gameSecondsPerRealSecond;
        public bool Running => running;
        /// <summary>예약돼 있는(아직 실행 전인) 타이머 수 — 재시작 후에도 처음과 같아야 한다</summary>
        public int ActiveTimerCount => schedule.Count;
        /// <summary>HUD/스마트폰에 보이는 시계. 진행은 1:1 이고 표시만 가상의 오전 시각으로 환산한다 (GameTimingConfig)</summary>
        public string FormattedTime => GameTimingConfig.Current.FormatClock(GameSeconds);

        /// <summary>게임 분이 바뀔 때</summary>
        public event Action<int> MinuteChanged;

        private void Awake()
        {
            Instance = this;
            if (timing != null) GameTimingConfig.SetActive(timing);
            GameSeconds = startGameSeconds;
        }

        private void OnDestroy()
        {
            schedule.Clear();   // 씬이 사라지면 예약된 이벤트도 모두 취소
            if (Instance == this) Instance = null;
            GameTimingConfig.ClearActive(timing);
        }

        private void Update()
        {
            float dt = Time.deltaTime; // timeScale 0이면 0
            if (dt <= 0f) return;
            if (GameFlow.IsEnded) return;   // 사망/엔딩 후에는 시간도 예약 이벤트도 멈춘다
            RealSeconds += dt;
            if (!running) return;

            GameSeconds += dt * gameSecondsPerRealSecond;

            int minute = Mathf.FloorToInt(GameSeconds / 60f);
            if (minute != lastMinute) { lastMinute = minute; MinuteChanged?.Invoke(minute); }

            // 실행 중 새 예약이 추가돼도 안전하도록 도래한 것을 먼저 모은 뒤 시각 순으로 실행
            due.Clear();
            for (int i = schedule.Count - 1; i >= 0; i--)
            {
                if (GameSeconds < schedule[i].time) continue;
                due.Add(schedule[i]);
                schedule.RemoveAt(i);
            }
            due.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (Timer t in due)
            {
                if (t.runId != GameRunContext.RunId) continue;   // 이전 판에서 예약된 타이머는 무시
                t.action?.Invoke();
            }
        }

        public void SetRatio(float ratio) => gameSecondsPerRealSecond = Mathf.Max(0f, ratio);
        public void SetRunning(bool value) => running = value;

        /// <summary>게임 시간 gameSeconds 가 되면 action 실행</summary>
        public void ScheduleAt(float gameSeconds, Action action, string label = null)
        {
            if (action != null) schedule.Add(new Timer { time = gameSeconds, action = action, runId = GameRunContext.RunId, label = label });
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
