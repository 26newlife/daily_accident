using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>클리어 방식</summary>
    public enum QuakeClearKind
    {
        None = 0,
        Shelter = 1,    // 고지대(뒷산) 대피소 도착
        Vertical = 2,   // 대피소에 못 갔지만 물이 닿지 않는 높은 곳에서 버팀
        Protected = 3,  // 대피지역(무적 구역) 안이라 죽을 위기를 넘김 (결과에서 감점)
    }

    /// <summary>
    /// 생존/사망 판정 (원본 OutcomeEvaluator 이식). 사망은 EventBus.RaisePlayerDied(DeathReason.Quake*) 로,
    /// 클리어는 점수 플래그를 기록한 뒤 DeathAndEnding.TriggerEnding(엔딩 id) 으로 넘긴다 (결과 화면은 공통 UI).
    ///   · 본진 종료: 몸 보호 → (가스/차단기: 연쇄 폭발 연출 뒤 사망) → 출구 확보(집 안일 때)
    ///   · 여진 종료: 몸 보호 (건물 밖이면 외벽에서 떨어진 넓은 곳). 대피소에서 버텼으면 클리어
    ///   · 지진해일: 휩쓸리면 사망. 대피소 도착 또는 높은 곳에서 버티면 클리어
    ///   · 대피지역(옥외대피장소·대피소) 안에서는 어떤 사고로도 죽지 않는다 — 대신 막아 준 사망 원인을 기록해 감점한다
    /// </summary>
    public class QuakeOutcome : MonoBehaviour
    {
        [SerializeField] private QuakeChainDisaster chainDisaster;

        public static QuakeOutcome Instance { get; private set; }
        public QuakeClearKind ClearKind { get; private set; } = QuakeClearKind.None;
        public IReadOnlyList<DeathReason> PreventedDeaths => prevented;
        public bool Resolved => resolved;
        public DeathReason LastDeath { get; private set; } = DeathReason.Unknown;

        /// <summary>결과가 났을 때 (사망 원인, Unknown 이면 클리어)</summary>
        public event Action<DeathReason> Decided;
        /// <summary>대피지역이라 죽지 않고 넘긴 사고 (원인당 한 번)</summary>
        public event Action<DeathReason> DeathPrevented;

        private readonly List<DeathReason> prevented = new List<DeathReason>();
        private bool resolved, pending;

        private QuakeTimeline Timeline => QuakeTimeline.Instance;
        private QuakeAreas Areas => QuakeAreas.Instance;
        private QuakeCoverTracker Cover => QuakeCoverTracker.Instance;
        private QuakeChecklist Checklist => QuakeChecklist.Instance;

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unhook();
        }

        public void Hook()
        {
            Unhook();
            if (Timeline != null) Timeline.PhaseChanged += OnPhase;
            if (Areas != null) Areas.AreaChanged += OnArea;
        }

        private void Unhook()
        {
            if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase;
            if (QuakeAreas.Instance != null) QuakeAreas.Instance.AreaChanged -= OnArea;
        }

        private void OnPhase(QuakePhase phase)
        {
            if (resolved || pending) return;
            if (phase == QuakePhase.Aftermath) EvaluateMainShock();
            else if (phase == QuakePhase.Tsunami) EvaluateAftershock();
        }

        private void EvaluateMainShock()
        {
            Checklist.Lock(QuakeKeys.ActGas, QuakeKeys.ActBreaker);   // 본진 판정 때의 가스/차단기 상태로 결과와 점수를 확정
            QuakeArea area = Areas.Current;
            Debug.Log($"[Quake] main shock cover={Cover.CoveredRatio:0.00} area={area}");
            if (!IsCovered())
            {
                Fail(area == QuakeArea.Stairwell ? DeathReason.QuakeStairFall : DeathReason.QuakeFallingObjects);
                if (resolved) return;
            }

            DeathReason fire = FireCause();
            if (fire != DeathReason.Unknown)
            {
                if (IsProtected()) Fail(fire);   // 대피지역 안이면 연쇄 폭발 연출 없이 감점만
                else if (IsFarFromBuilding())
                {
                    // 현실성: 불과 폭발은 건물 안(과 그 바로 근처)에서만 사람을 해친다. 건물 밖으로 충분히 떨어졌으면 화를 면한다 (조치를 안 한 감점은 그대로)
                    Debug.Log($"[Quake] 조치 안 한 화재({fire}) — 건물에서 멀리 떨어져 있어 사망하지 않음");
                    EventBus.ShowSubtitle("(뒤쪽 건물 안에서 쾅 하는 폭음이 울린다... 일찍 빠져나와 화를 면했다.)", 5f);
                }
                else if (chainDisaster != null)
                {
                    pending = true;
                    chainDisaster.Play(fire, () => { pending = false; Fail(fire); });
                    return;
                }
                else Fail(fire);
            }

            if (area == QuakeArea.Apartment && !Checklist.IsDone(QuakeKeys.ActDoor)) Fail(DeathReason.QuakeTrappedInside);
        }

        private void EvaluateAftershock()
        {
            QuakeArea area = Areas.Current;
            Debug.Log($"[Quake] aftershock cover={Cover.CoveredRatio:0.00} area={area}");
            if (IsCovered())
            {
                if (area == QuakeArea.Shelter) Clear(QuakeClearKind.Shelter);
                return;
            }
            switch (area)
            {
                case QuakeArea.Outside:
                case QuakeArea.OutdoorSite:
                case QuakeArea.Rooftop: Fail(DeathReason.QuakeFacadeDebris); break;
                case QuakeArea.Stairwell: Fail(DeathReason.QuakeStairFall); break;
                default: Fail(DeathReason.QuakeFallingObjects); break;
            }
        }

        private void OnArea(QuakeArea area)
        {
            if (area == QuakeArea.Shelter && Timeline != null && Timeline.Phase == QuakePhase.Tsunami) Clear(QuakeClearKind.Shelter);
        }

        /// <summary>판정 시점과 상관없이 바로 죽는 사고 (낙하물, 폭발, 잔해, 싱크홀 …)</summary>
        public void Kill(DeathReason cause)
        {
            if (resolved || pending || cause == DeathReason.Unknown) return;
            Fail(cause);
        }

        /// <summary>해일이 덮침. 대피지역 안이면 간신히 생존(감점)으로 클리어</summary>
        public void OnEngulfed()
        {
            if (resolved) return;
            if (!IsProtected()) { Resolve(DeathReason.Tsunami); return; }
            Prevent(DeathReason.Tsunami);
            pending = false;
            Clear(QuakeClearKind.Protected);
        }

        /// <summary>해일이 지나갔고 플레이어는 물에 잠기지 않았다 → 높은 곳에서 버팀</summary>
        public void OnTsunamiPassed() => Clear(QuakeClearKind.Vertical);

        private bool IsCovered() => Cover.CoveredRatio >= Timeline.Config.requiredCoverRatio;
        /// <summary>건물 밖 지면에서 외벽으로부터 안전거리(QuakeConfig.facadeSafeDistance) 이상 떨어져 있는가 (옥상·건물 안은 해당 없음)</summary>
        private bool IsFarFromBuilding()
        {
            QuakeArea a = Areas != null ? Areas.Current : QuakeArea.Apartment;
            if (a != QuakeArea.Outside && a != QuakeArea.OutdoorSite && a != QuakeArea.Shelter) return false;
            return Areas.DistanceToBuilding(Timeline.Player.transform.position) >= Timeline.Config.facadeSafeDistance;
        }

        private bool IsProtected() => Areas != null && Areas.Current.IsEvacuationSite();

        private DeathReason FireCause()
        {
            if (!Checklist.IsDone(QuakeKeys.ActGas)) return DeathReason.QuakeGasExplosion;
            if (!Checklist.IsDone(QuakeKeys.ActBreaker)) return DeathReason.QuakeElectricFire;
            return DeathReason.Unknown;
        }

        /// <summary>모든 사망은 여기를 거친다. 대피지역 안이면 죽지 않고 감점 기록만 남긴다</summary>
        private void Fail(DeathReason cause)
        {
            if (resolved || cause == DeathReason.Unknown) return;
            if (!IsProtected()) { Resolve(cause); return; }
            Prevent(cause);
        }

        private void Prevent(DeathReason cause)
        {
            if (prevented.Contains(cause)) return;
            prevented.Add(cause);
            Debug.Log($"[Quake] 대피지역이라 넘김: {cause}");
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("s02_prevented");
            DeathPrevented?.Invoke(cause);
            EventBus.ShowSubtitle("(대피지역 안이라 간신히 무사했다... 하지만 정말 위험한 순간이었다)", 4f);
        }

        private void Clear(QuakeClearKind kind)
        {
            if (resolved || pending) return;
            ClearKind = kind;
            Resolve(DeathReason.Unknown);
        }

        private void Resolve(DeathReason cause)
        {
            if (resolved) return;
            resolved = true;
            pending = false;
            LastDeath = cause;
            Debug.Log($"[Quake] Resolved: death={cause} clear={ClearKind}");
            Decided?.Invoke(cause);
            if (cause != DeathReason.Unknown)
            {
                if (Timeline != null) Timeline.Stop();
                EventBus.RaisePlayerDied(cause);
                return;
            }
            // 대피소 도착: 원본처럼 잠깐 여유를 두어 가족에게 문자를 보낼 수 있게 한다 (해일 연출은 계속, 더는 죽지 않는다)
            if (ClearKind == QuakeClearKind.Shelter && shelterGraceSeconds > 0f) { StartCoroutine(EndAfterGrace()); return; }
            FinishClear();
        }

        [SerializeField, Tooltip("대피소 도착 뒤 결과 화면까지 여유 (가족에게 문자 보내기)")] private float shelterGraceSeconds = 12f;

        private System.Collections.IEnumerator EndAfterGrace()
        {
            EventBus.ShowSubtitle("(대피소에 도착했다... 가족이 걱정하고 있을 것이다. [Tab] 휴대폰)", 6f);
            float t = 0f;
            float sentAt = -1f;
            while (t < shelterGraceSeconds)
            {
                t += Time.deltaTime;
                if (sentAt < 0f && QuakeRecord.Instance != null && QuakeRecord.Instance.FamilyContacted) sentAt = t;
                if (sentAt >= 0f && t - sentAt > 2f) break;   // 문자를 보냈으면 2초 뒤 결과로
                yield return null;
            }
            FinishClear();
        }

        private void FinishClear()
        {
            if (Timeline != null) Timeline.Stop();
            if (QuakeRecord.Instance != null) QuakeRecord.Instance.WriteScoreFlags(kind: ClearKind);
            string ending = ClearKind == QuakeClearKind.Shelter ? QuakeKeys.EndingShelter : ClearKind == QuakeClearKind.Vertical ? QuakeKeys.EndingVertical : QuakeKeys.EndingProtected;
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.TriggerEnding(ending);
        }

        /// <summary>디버그·테스트: 상태 초기화 (재시작은 씬을 다시 불러오므로 보통 필요 없다)</summary>
        public void ResetState() { resolved = false; pending = false; ClearKind = QuakeClearKind.None; prevented.Clear(); LastDeath = DeathReason.Unknown; }
    }
}
