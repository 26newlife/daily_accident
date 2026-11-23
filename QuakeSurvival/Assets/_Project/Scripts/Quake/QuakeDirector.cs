using System;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 지진 타임라인(평상시 -> 전진 -> 준비 -> 본진 -> 종료)을 시간 순서대로 진행한다.
    /// 다른 시스템은 PhaseChanged 이벤트와 Intensity 값만 읽는다.
    /// </summary>
    public sealed class QuakeDirector : MonoBehaviour
    {
        [Header("단계별 길이(초)")]
        [SerializeField] private float calmDuration = 8f;
        [SerializeField] private float foreshockDuration = 8f;
        [SerializeField] private float prepareDuration = 60f;
        [SerializeField] private float mainShockDuration = 30f;
        [SerializeField] private float aftermathDuration = 25f;
        [SerializeField] private float aftershockDuration = 10f;

        [Header("흔들림 세기(0~1)")]
        [SerializeField] private float foreshockIntensity = 0.25f;
        [SerializeField] private float mainShockIntensity = 1f;
        [SerializeField] private float aftershockIntensity = 0.5f;
        [SerializeField] private float rampSeconds = 2f;

        public event Action<QuakePhase> PhaseChanged;

        public QuakePhase Phase { get; private set; } = QuakePhase.Calm;
        public float PhaseElapsed { get; private set; }

        /// <summary>현재 흔들림 세기. 단계 시작/끝에서 부드럽게 올라가고 내려간다.</summary>
        public float Intensity => PeakIntensity(Phase) * Envelope(PhaseElapsed, DurationOf(Phase));

        private void Start()
        {
            PhaseChanged?.Invoke(Phase);
        }

        private void Update()
        {
            if (Phase == QuakePhase.Tsunami)
            {
                PhaseElapsed += Time.deltaTime;
                return;
            }

            PhaseElapsed += Time.deltaTime;
            if (PhaseElapsed >= DurationOf(Phase)) EnterPhase(Phase + 1);
        }

        private void EnterPhase(QuakePhase next)
        {
            Phase = next;
            PhaseElapsed = 0f;
            Debug.Log($"[QuakeDirector] Phase -> {next}");
            PhaseChanged?.Invoke(next);
        }

        private float DurationOf(QuakePhase phase)
        {
            switch (phase)
            {
                case QuakePhase.Calm: return calmDuration;
                case QuakePhase.Foreshock: return foreshockDuration;
                case QuakePhase.Prepare: return prepareDuration;
                case QuakePhase.MainShock: return mainShockDuration;
                case QuakePhase.Aftermath: return aftermathDuration;
                case QuakePhase.Aftershock: return aftershockDuration;
                default: return float.PositiveInfinity;
            }
        }

        private float PeakIntensity(QuakePhase phase)
        {
            switch (phase)
            {
                case QuakePhase.Foreshock: return foreshockIntensity;
                case QuakePhase.MainShock: return mainShockIntensity;
                case QuakePhase.Aftershock: return aftershockIntensity;
                default: return 0f;
            }
        }

        private float Envelope(float elapsed, float duration)
        {
            if (float.IsInfinity(duration) || rampSeconds <= 0f) return 1f;
            float rampIn = Mathf.Clamp01(elapsed / rampSeconds);
            float rampOut = Mathf.Clamp01((duration - elapsed) / rampSeconds);
            return Mathf.Min(rampIn, rampOut);
        }
    }
}
