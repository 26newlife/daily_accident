using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>지진 시나리오 진행 단계 (원본 QuakePhase 와 같은 순서)</summary>
    public enum QuakePhase
    {
        Calm = 0,        // 평상시
        Foreshock = 1,   // 전진 (약한 흔들림)
        Prepare = 2,     // 전진 뒤 준비 시간
        MainShock = 3,   // 본진
        Aftermath = 4,   // 본진 직후 (대피)
        Aftershock = 5,  // 여진 + 지진해일 경보
        Tsunami = 6,     // 지진해일 도달까지 (끝은 QuakeTsunami 가 판정)
    }

    /// <summary>
    /// 지진 타임라인 (평상시 → 전진 → 준비 → 본진 → 본진 직후 → 여진 → 지진해일). 원본 QuakeDirector 이식.
    /// 다른 시스템은 PhaseChanged 와 Intensity 만 읽는다. 흔들림은 집 API(카메라 + 가구 떨림)로 표현한다.
    /// 시나리오 씬이 켜질 때(OnScenarioLoaded) Begin 으로 시작한다. 결과가 나면(게임 종료) 멈춘다.
    /// </summary>
    public class QuakeTimeline : MonoBehaviour
    {
        [SerializeField] private QuakeConfig config;

        public static QuakeTimeline Instance { get; private set; }
        public QuakeConfig Config => config;
        public QuakePhase Phase { get; private set; } = QuakePhase.Calm;
        public float PhaseElapsed { get; private set; }
        public bool Running { get; private set; }

        /// <summary>(새 단계) — 단계가 바뀔 때. Begin 직후 Calm 으로 한 번 호출된다</summary>
        public event Action<QuakePhase> PhaseChanged;

        /// <summary>현재 흔들림 세기 (0~1, 단계 시작/끝에서 부드럽게)</summary>
        public float Intensity => Running ? PeakIntensity(Phase) * Envelope(PhaseElapsed, DurationOf(Phase)) : 0f;
        /// <summary>폭발 등 순간 충격 (흔들림에 더해진다, 시간이 지나면 줄어든다)</summary>
        public float Impulse { get; private set; }
        public static bool IsShakingPhase(QuakePhase p) => p == QuakePhase.Foreshock || p == QuakePhase.MainShock || p == QuakePhase.Aftershock;

        private PlayerController player;
        private float shakeTick;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Begin()
        {
            Phase = QuakePhase.Calm;
            PhaseElapsed = 0f;
            Running = true;
            ScenarioFlags.SetNumber(QuakeKeys.FlagPhase, (float)Phase);
            PhaseChanged?.Invoke(Phase);
        }

        public void Stop() { Running = false; Impulse = 0f; }

        public void AddImpulse(float amount) => Impulse = Mathf.Clamp(Impulse + amount, 0f, 1.5f);

        public PlayerController Player
        {
            get { if (player == null) player = FindFirstObjectByType<PlayerController>(); return player; }
        }

        private void Update()
        {
            if (!Running) return;
            if (GameFlow.IsEnded) { Stop(); return; }
            float dt = Time.deltaTime;
            PhaseElapsed += dt;
            if (Phase != QuakePhase.Tsunami && PhaseElapsed >= DurationOf(Phase)) EnterPhase(Phase + 1);
            Impulse = Mathf.MoveTowards(Impulse, 0f, 2.5f * dt);
            DriveShake(dt);
        }

        /// <summary>디버그·테스트용: 다음 단계로 바로 넘긴다</summary>
        public void Skip() { if (Running && Phase != QuakePhase.Tsunami) EnterPhase(Phase + 1); }

        private void EnterPhase(QuakePhase next)
        {
            Phase = next;
            PhaseElapsed = 0f;
            ScenarioFlags.SetNumber(QuakeKeys.FlagPhase, (float)next);
            Debug.Log($"[Quake] Phase -> {next}");
            if (HouseController.Instance != null && IsShakingPhase(next))
                HouseController.Instance.TriggerStructureShake(next == QuakePhase.MainShock ? StructureShakePreset.Heavy : next == QuakePhase.Aftershock ? StructureShakePreset.Medium : StructureShakePreset.Light);
            PhaseChanged?.Invoke(next);
        }

        /// <summary>흔들리는 동안 카메라를 계속 흔들고, 집 가구 떨림을 이어 준다 (집 API 는 지속 시간 기반이라 주기적으로 다시 건다)</summary>
        private void DriveShake(float dt)
        {
            float amount = Intensity + Impulse;
            if (amount <= 0.01f) return;
            PlayerController p = Player;
            if (p != null) p.Shake(config.cameraShakeAmplitude * amount, 0.25f);
            shakeTick -= dt;
            if (shakeTick <= 0f && Intensity > 0.15f && HouseController.Instance != null)
            {
                shakeTick = 1.2f;
                HouseController.Instance.TriggerStructureShake(Intensity > 0.7f ? StructureShakePreset.Heavy : Intensity > 0.35f ? StructureShakePreset.Medium : StructureShakePreset.Light);
            }
        }

        public float DurationOf(QuakePhase phase)
        {
            switch (phase)
            {
                case QuakePhase.Calm: return config.calmDuration;
                case QuakePhase.Foreshock: return config.foreshockDuration;
                case QuakePhase.Prepare: return config.prepareDuration;
                case QuakePhase.MainShock: return config.mainShockDuration;
                case QuakePhase.Aftermath: return config.aftermathDuration;
                case QuakePhase.Aftershock: return config.aftershockDuration;
                default: return float.PositiveInfinity;
            }
        }

        private float PeakIntensity(QuakePhase phase)
        {
            switch (phase)
            {
                case QuakePhase.Foreshock: return config.foreshockIntensity;
                case QuakePhase.MainShock: return config.mainShockIntensity;
                case QuakePhase.Aftershock: return config.aftershockIntensity;
                default: return 0f;
            }
        }

        private float Envelope(float elapsed, float duration)
        {
            if (float.IsInfinity(duration) || config.rampSeconds <= 0f) return 1f;
            float rampIn = Mathf.Clamp01(elapsed / config.rampSeconds);
            float rampOut = Mathf.Clamp01((duration - elapsed) / config.rampSeconds);
            return Mathf.Min(rampIn, rampOut);
        }

        /// <summary>단계 기준 시각에 도달했는지 (그 단계를 이미 지났으면 true)</summary>
        public bool Reached(QuakePhase phase, float delay)
        {
            if (!Running) return false;
            if (Phase != phase) return Phase > phase;
            return PhaseElapsed >= delay;
        }
    }
}
