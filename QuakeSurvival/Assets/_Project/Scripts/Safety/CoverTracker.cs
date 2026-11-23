using System.Collections.Generic;
using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 흔들림 단계(전진/본진/여진)마다 플레이어가 CoverZone 안에서 몸을 보호한 시간 비율을 잰다.
    /// CoverZone은 "Ignore Raycast" 레이어에 두어 상호작용 레이와 분리한다.
    /// </summary>
    public sealed class CoverTracker : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private LayerMask coverMask = 1 << 2; // Ignore Raycast
        [SerializeField] private float probeRadius = 0.2f;
        [SerializeField] private float probeHeight = 0.3f;

        private readonly Collider[] hits = new Collider[16];
        private readonly Dictionary<QuakePhase, float> ratios = new Dictionary<QuakePhase, float>();
        private QuakePhase trackedPhase = QuakePhase.Calm;
        private float shakeTime;
        private float coveredTime;

        public bool IsCovered { get; private set; }

        /// <summary>가장 최근 흔들림 단계에서 보호한 시간 비율(0~1). 흔들림 전이면 1.</summary>
        public float CoveredRatio => shakeTime <= 0f ? 1f : coveredTime / shakeTime;

        /// <summary>특정 흔들림 단계의 보호 비율. 아직 겪지 않았으면 0.</summary>
        public float RatioOf(QuakePhase phase)
        {
            if (phase == trackedPhase) return CoveredRatio;
            return ratios.TryGetValue(phase, out float r) ? r : 0f;
        }

        private static bool IsShaking(QuakePhase phase) =>
            phase == QuakePhase.Foreshock || phase == QuakePhase.MainShock || phase == QuakePhase.Aftershock;

        private void Awake()
        {
            if (director == null || player == null)
            {
                Debug.LogError("[CoverTracker] director 또는 player가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            QuakePhase phase = director.Phase;
            if (!IsShaking(phase))
            {
                IsCovered = false;
                return;
            }

            if (phase != trackedPhase) BeginPhase(phase);

            IsCovered = CheckCovered();
            shakeTime += Time.deltaTime;
            if (IsCovered) coveredTime += Time.deltaTime;
        }

        private void BeginPhase(QuakePhase phase)
        {
            if (IsShaking(trackedPhase)) ratios[trackedPhase] = CoveredRatio;
            trackedPhase = phase;
            shakeTime = 0f;
            coveredTime = 0f;
        }

        private bool CheckCovered()
        {
            Vector3 probe = player.transform.position + Vector3.up * probeHeight;
            int count = Physics.OverlapSphereNonAlloc(probe, probeRadius, hits, coverMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].TryGetComponent(out CoverZone zone)) continue;
                if (!zone.RequireCrouch || player.IsCrouching) return true;
            }
            return false;
        }
    }
}
