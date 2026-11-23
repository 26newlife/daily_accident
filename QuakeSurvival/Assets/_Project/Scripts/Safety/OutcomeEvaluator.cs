using System;
using System.Collections.Generic;
using QuakeSurvival.Quake;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 생존/사망을 판정한다.
    /// - 위험 구역(엘리베이터 등) 진입: 즉시 사망.
    /// - 본진 종료: 몸 보호 -> (가스/차단기: 연쇄 폭발 연출 후 사망) -> 출구 확보(집 안일 때).
    /// - 여진 종료: 몸 보호(건물 밖이면 외벽에서 떨어진 넓은 곳).
    /// - 지진해일: 휩쓸리면 사망. 대피소 도착 또는 높은 곳에서 버티면 클리어.
    /// - 대피지역(학교 운동장, 고지대 대피소) 안에서는 어떤 사고로도 죽지 않는다(무적).
    ///   대신 막아 준 사망 원인을 기록하고, 결과 화면에서 감점한다.
    /// </summary>
    public sealed class OutcomeEvaluator : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private SafetyChecklist checklist;
        [SerializeField] private CoverTracker cover;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private ChainDisaster chainDisaster;
        [SerializeField] private TsunamiController tsunami;
        [SerializeField, Range(0f, 1f)] private float requiredCoverRatio = 0.7f;

        /// <summary>최종 결과. DeathCause.None이면 클리어(ClearKind 참고).</summary>
        public event Action<DeathCause> Decided;

        /// <summary>대피지역이라 죽지 않고 넘긴 사고가 생길 때마다(원인당 한 번).</summary>
        public event Action<DeathCause> DeathPrevented;

        public ClearKind ClearKind { get; private set; } = ClearKind.None;

        /// <summary>대피지역 덕분에 넘긴 사망 원인들(결과 감점용).</summary>
        public IReadOnlyList<DeathCause> PreventedDeaths => prevented;

        private readonly List<DeathCause> prevented = new List<DeathCause>();
        private bool resolved;
        private bool pending;

        private void OnEnable()
        {
            if (director == null || checklist == null || cover == null || zones == null
                || chainDisaster == null || tsunami == null)
            {
                Debug.LogError("[OutcomeEvaluator] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            director.PhaseChanged += OnPhase;
            zones.AreaChanged += OnArea;
            zones.HazardEntered += Fail;
            tsunami.Engulfed += OnEngulfed;
            tsunami.Passed += OnTsunamiPassed;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
            if (zones != null)
            {
                zones.AreaChanged -= OnArea;
                zones.HazardEntered -= Fail;
            }
            if (tsunami != null)
            {
                tsunami.Engulfed -= OnEngulfed;
                tsunami.Passed -= OnTsunamiPassed;
            }
        }

        private void OnPhase(QuakePhase phase)
        {
            if (resolved || pending) return;
            if (phase == QuakePhase.Aftermath) EvaluateMainShock();
            else if (phase == QuakePhase.Tsunami) EvaluateAftershock();
        }

        private void EvaluateMainShock()
        {
            // 본진 판정 때의 가스/차단기 상태로 결과와 점수를 확정한다
            checklist.Lock(SafetyChecklist.GasValve, SafetyChecklist.Breaker);
            PlayerArea area = zones.CurrentArea;
            Log($"main shock cover={cover.CoveredRatio:0.00} area={area}");
            if (!IsCovered())
            {
                Fail(area == PlayerArea.Stairwell ? DeathCause.StairFall : DeathCause.FallingObjects);
                if (resolved) return;
            }

            DeathCause fire = FireCause();
            if (fire != DeathCause.None)
            {
                // 대피지역 안이면 연쇄 폭발 연출 없이 바로 감점만 한다
                if (IsProtected())
                {
                    Fail(fire);
                }
                else
                {
                    pending = true;
                    chainDisaster.Play(fire, () =>
                    {
                        pending = false;
                        Fail(fire);
                    });
                    return;
                }
            }

            if (area == PlayerArea.Apartment && !checklist.IsDone(SafetyChecklist.Door)) Fail(DeathCause.TrappedInside);
        }

        private void EvaluateAftershock()
        {
            PlayerArea area = zones.CurrentArea;
            Log($"aftershock cover={cover.CoveredRatio:0.00} area={area}");
            if (IsCovered())
            {
                if (area == PlayerArea.Shelter) Clear(ClearKind.Shelter);
                return;
            }

            switch (area)
            {
                case PlayerArea.Outside:
                case PlayerArea.OutdoorSite: Fail(DeathCause.FacadeDebris); break;
                case PlayerArea.Stairwell: Fail(DeathCause.StairFall); break;
                default: Fail(DeathCause.FallingObjects); break;
            }
        }

        private void OnArea(PlayerArea area)
        {
            if (area == PlayerArea.Shelter && director.Phase == QuakePhase.Tsunami) Clear(ClearKind.Shelter);
        }

        /// <summary>판정 시점과 상관없이 바로 죽는 사고(머리 위 낙하물, 폭발, 잔해)를 알린다.</summary>
        public void Kill(DeathCause cause)
        {
            if (resolved || pending || cause == DeathCause.None) return;
            Fail(cause);
        }

        // 해일은 한 번 덮치면 끝나므로, 대피지역에서 버틴 경우 그대로 '간신히 생존'으로 클리어한다
        private void OnEngulfed()
        {
            if (!IsProtected())
            {
                Resolve(DeathCause.Tsunami);
                return;
            }
            Prevent(DeathCause.Tsunami);
            pending = false; // 해일이 마지막 판정이므로 연출 대기 중이어도 결과를 낸다
            Clear(ClearKind.Protected);
        }

        private void OnTsunamiPassed() => Clear(ClearKind.Vertical);

        private bool IsCovered() => cover.CoveredRatio >= requiredCoverRatio;

        private bool IsProtected() => zones.CurrentArea.IsEvacuationSite();

        /// <summary>모든 사망은 여기를 거친다. 대피지역 안이면 죽지 않고 감점 기록만 남긴다.</summary>
        private void Fail(DeathCause cause)
        {
            if (resolved || cause == DeathCause.None) return;
            if (!IsProtected())
            {
                Resolve(cause);
                return;
            }
            Prevent(cause);
        }

        private void Prevent(DeathCause cause)
        {
            if (prevented.Contains(cause)) return;
            prevented.Add(cause);
            Log($"Prevented (evacuation site): {cause}");
            DeathPrevented?.Invoke(cause);
        }

        private DeathCause FireCause()
        {
            if (!checklist.IsDone(SafetyChecklist.GasValve)) return DeathCause.GasExplosion;
            if (!checklist.IsDone(SafetyChecklist.Breaker)) return DeathCause.ElectricFire;
            return DeathCause.None;
        }

        private void Clear(ClearKind kind)
        {
            if (resolved || pending) return;
            ClearKind = kind;
            Resolve(DeathCause.None);
        }

        private void Resolve(DeathCause cause)
        {
            if (resolved) return;
            resolved = true;
            pending = false;
            Log($"Resolved: {cause} clear={ClearKind}");
            Decided?.Invoke(cause);
        }

        private static void Log(string message)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log("[OutcomeEvaluator] " + message);
#endif
        }
    }
}
