using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 사망 / 엔딩 처리. 사망 원인 → DeathReasonData(교육 피드백) 조회, 엔딩 id → EndingData 조회 후
    /// 결과 화면(DeathScreen / EndingScreen)에 넘긴다. 점수 집계는 ScoreSystem 에 위임한다.
    /// 사망: Die(reason) 또는 EventBus.RaisePlayerDied  /  엔딩: TriggerEnding(id)
    /// </summary>
    public class DeathAndEnding : MonoBehaviour
    {
        [SerializeField] private DeathReasonData[] deathReasons;
        [SerializeField] private DeathReasonData fallbackReason;
        [SerializeField] private EndingData[] endings;
        [SerializeField] private DeathScreen deathScreen;
        [SerializeField] private EndingScreen endingScreen;
        [SerializeField] private ScoreSystem scoreSystem;
        [SerializeField, Min(0f), Tooltip("사망/엔딩 후 결과 화면이 뜨기까지 지연(초, 실시간)")]
        private float screenDelay = 1.2f;

        public static DeathAndEnding Instance { get; private set; }
        public bool HasEnded { get; private set; }

        public event Action<DeathReasonData> Died;
        public event Action<EndingData> EndingTriggered;

        private void Awake() { Instance = this; }
        private void OnEnable() => EventBus.PlayerDied += OnPlayerDied;
        private void OnDisable() => EventBus.PlayerDied -= OnPlayerDied;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>사망 처리 (EventBus.RaisePlayerDied 와 동일)</summary>
        public void Die(DeathReason reason) => EventBus.RaisePlayerDied(reason);

        private void OnPlayerDied(DeathReason reason)
        {
            if (HasEnded) return;
            HasEnded = true;
            GameFlow.SetEnded();

            DeathReasonData data = FindReason(reason);
            Died?.Invoke(data);
            StartCoroutine(ShowAfterDelay(() => { if (deathScreen != null) deathScreen.Show(data); }));
        }

        /// <summary>엔딩 처리. endingId 는 EndingData.Id</summary>
        public void TriggerEnding(string endingId)
        {
            if (HasEnded) return;
            EndingData data = FindEnding(endingId);
            if (data == null) { Debug.LogWarning($"[DeathAndEnding] 등록되지 않은 엔딩 id: {endingId}", this); return; }

            HasEnded = true;
            GameFlow.SetEnded();
            EventBus.RaiseEnding(endingId);
            EndingTriggered?.Invoke(data);

            float real = GameClock.Instance != null ? GameClock.Instance.RealSeconds : 0f;
            StartCoroutine(ShowAfterDelay(() => { if (endingScreen != null) endingScreen.Show(data, scoreSystem, real); }));
        }

        private System.Collections.IEnumerator ShowAfterDelay(Action show)
        {
            // timeScale 은 건드리지 않고(연출 유지) 실시간으로 대기
            yield return new WaitForSecondsRealtime(screenDelay);
            show?.Invoke();
        }

        private DeathReasonData FindReason(DeathReason reason)
        {
            if (deathReasons != null)
                foreach (DeathReasonData d in deathReasons) if (d != null && d.Reason == reason) return d;
            return fallbackReason;
        }

        private EndingData FindEnding(string id)
        {
            if (endings != null)
                foreach (EndingData e in endings) if (e != null && e.Id == id) return e;
            return null;
        }
    }
}
