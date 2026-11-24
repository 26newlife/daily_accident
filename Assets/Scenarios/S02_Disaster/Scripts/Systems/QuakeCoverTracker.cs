using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 흔들림 단계(전진/본진/여진)마다 몸을 보호한 시간 비율을 잰다 (원본 CoverTracker 이식).
    /// 건물 밖에서는 외벽에서 충분히 떨어진 넓은 곳이면 웅크리지 않아도 보호로 친다.
    /// </summary>
    public class QuakeCoverTracker : MonoBehaviour
    {
        private readonly Dictionary<QuakePhase, float> ratios = new Dictionary<QuakePhase, float>();
        private QuakePhase trackedPhase = QuakePhase.Calm;
        private float shakeTime, coveredTime;

        public static QuakeCoverTracker Instance { get; private set; }
        public bool IsCovered { get; private set; }
        /// <summary>가장 최근 흔들림 단계에서 보호한 시간 비율 (흔들림 전이면 1)</summary>
        public float CoveredRatio => shakeTime <= 0f ? 1f : coveredTime / shakeTime;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>특정 흔들림 단계의 보호 비율 (아직 겪지 않았으면 0)</summary>
        public float RatioOf(QuakePhase phase)
        {
            if (phase == trackedPhase) return CoveredRatio;
            return ratios.TryGetValue(phase, out float r) ? r : 0f;
        }

        private void Update()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || !tl.Running || !QuakeTimeline.IsShakingPhase(tl.Phase)) { IsCovered = false; return; }
            if (tl.Phase != trackedPhase) BeginPhase(tl.Phase);
            IsCovered = Check(tl.Player);
            shakeTime += Time.deltaTime;
            if (IsCovered) coveredTime += Time.deltaTime;
        }

        private void BeginPhase(QuakePhase phase)
        {
            if (QuakeTimeline.IsShakingPhase(trackedPhase)) ratios[trackedPhase] = CoveredRatio;
            trackedPhase = phase;
            shakeTime = 0f; coveredTime = 0f;
        }

        private bool Check(PlayerController p)
        {
            if (p == null) return false;
            Vector3 probe = p.transform.position + Vector3.up * 0.3f;
            foreach (QuakeCoverZone z in QuakeCoverZone.All)
                if (z.Bounds.Contains(probe) && (!z.RequireCrouch || p.IsCrouching)) return true;
            QuakeAreas areas = QuakeAreas.Instance;
            if (areas == null) return false;
            if (areas.Current.IsEvacuationSite()) return true;   // 대피장소는 건물과 떨어진 넓은 곳
            if (areas.Current == QuakeArea.Outside)
                return areas.DistanceToBuilding(probe) >= QuakeTimeline.Instance.Config.facadeSafeDistance;   // 외벽에서 떨어진 넓은 곳
            return false;
        }
    }
}
