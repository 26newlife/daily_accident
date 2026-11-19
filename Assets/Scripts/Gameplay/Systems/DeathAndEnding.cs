using System;
using System.Collections.Generic;
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

        public IReadOnlyList<DeathReasonData> DeathReasons => deathReasons;
        public IReadOnlyList<EndingData> Endings => endings;
        /// <summary>마지막으로 처리한 사망 사유 / 엔딩 id (디버그·감사 도구용)</summary>
        public DeathReason LastDeathReason { get; private set; }
        public string LastEndingId { get; private set; }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugFlags.Invulnerable) { Debug.Log($"[Debug] invulnerable: death ignored ({reason})"); return; }
#endif
            HasEnded = true;
            GameFlow.SetEnded();

            LastDeathReason = reason;
            DeathReasonData data = FindReason(reason);
            EndingCollection.Add(EndingCollection.DeathId(reason));   // 사망 엔딩 모음에 기록 (영구)
            Died?.Invoke(data);
            string collection = EndingCollection.BuildText(deathReasons, null);
            StartCoroutine(ShowAfterDelay(() => { if (deathScreen != null) deathScreen.Show(data, collection); }));
        }

        /// <summary>엔딩 처리. endingId 는 EndingData.Id</summary>
        public void TriggerEnding(string endingId)
        {
            if (HasEnded) return;
            string resolvedId = ResolveEndingId(endingId);
            EndingData data = FindEnding(resolvedId) ?? FindEnding(endingId);
            if (data == null) { Debug.LogWarning($"[DeathAndEnding] 등록되지 않은 엔딩 id: {endingId}", this); return; }
            if (data.Id != endingId) ScenarioFlags.SetText("ending_base_id", endingId);   // 히든으로 바뀐 경우 원래 루트 엔딩 id

            HasEnded = true;
            GameFlow.SetEnded();
            LastEndingId = data.Id;
            EventBus.RaiseEnding(data.Id);
            EndingTriggered?.Invoke(data);

            float real = GameClock.Instance != null ? GameClock.Instance.RealSeconds : 0f;
            if (scoreSystem != null)
            {
                scoreSystem.Record(EvacKeys.RecEndingTime, real);
                scoreSystem.FinalizeScore(real);   // 항목별 점수 / 등급 / 잘한 점·아쉬운 점
            }
            EndingCollection.Add(EndingCollection.EndingId(data.Id));
            string collection = EndingCollection.BuildText(deathReasons, endings);
            StartCoroutine(ShowAfterDelay(() => { if (endingScreen != null) endingScreen.Show(data, scoreSystem, real, collection); }));
        }

        /// <summary>
        /// 히든 엔딩 판정: 생존 엔딩에서 할머니를 동행·구출한 상태(EvacKeys.FlagGrandmotherWith)면 히든 엔딩으로 바꾼다.
        /// 히든 엔딩 에셋이 등록돼 있지 않으면 원래 엔딩을 그대로 쓴다.
        /// </summary>
        public string ResolveEndingId(string endingId)
        {
            if (endingId == EvacKeys.EndingHidden) return endingId;
            if (ScenarioFlags.GetBool(EvacKeys.FlagGrandmotherWith) && FindEnding(EvacKeys.EndingHidden) != null) return EvacKeys.EndingHidden;
            return endingId;
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
