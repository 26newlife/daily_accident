using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 플레이 중 기록(조치, 몸 보호 비율, 엘리베이터, 부상, 이웃, 연락, 대피장소)을 모아 클리어 시점에 점수 플래그로 쓴다 (원본 EvacuationRecord 이식).
    /// 점수 환산·등급·잘한 점/아쉬운 점은 공통 ScoreSystem + S02 ScoreRules 에셋이 한다 (행정안전부 「지진 국민행동요령」 기준 문구).
    /// </summary>
    public class QuakeRecord : MonoBehaviour
    {
        public static QuakeRecord Instance { get; private set; }

        public bool ElevatorCalled { get; set; }
        public bool Injured { get; set; }
        public bool NeighborFollowing { get; set; }
        public bool FamilyContacted { get; set; }
        public int CallsDuringEvacuation { get; private set; }
        public bool ReachedOutdoorSite { get; private set; }

        private void Awake() { Instance = this; }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (QuakeAreas.Instance != null) QuakeAreas.Instance.AreaChanged -= OnArea;
        }

        public void Hook()
        {
            if (QuakeAreas.Instance != null) { QuakeAreas.Instance.AreaChanged -= OnArea; QuakeAreas.Instance.AreaChanged += OnArea; }
        }

        private void OnArea(QuakeArea a)
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (a == QuakeArea.OutdoorSite && tl != null && tl.Phase < QuakePhase.Tsunami && !ReachedOutdoorSite)
            {
                ReachedOutdoorSite = true;
                if (ScoreSystem.Instance != null) ScoreSystem.Instance.Record("s02_outdoor_site", 1f);
            }
        }

        /// <summary>부상·이웃 부축에 따른 이동 속도 배율을 플레이어에 적용한다 (두 배율을 곱한다)</summary>
        public void ApplySpeed()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || tl.Player == null) return;
            tl.Player.ScenarioSpeedMultiplier = (Injured ? tl.Config.injuredSpeed : 1f) * (NeighborFollowing ? tl.Config.neighborCarrySpeed : 1f);
        }

        /// <summary>흔들림이 시작된 뒤(대피 중) 걸려 온/건 통화</summary>
        public void CountCall()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl != null && tl.Phase >= QuakePhase.Foreshock) CallsDuringEvacuation++;
        }

        /// <summary>클리어 직전: 규칙마다 ScenarioFlags 를 쓴다 (ScoreRules 의 FlagTrue 키)</summary>
        public void WriteScoreFlags(QuakeClearKind kind)
        {
            QuakeChecklist c = QuakeChecklist.Instance;
            QuakeCoverTracker cover = QuakeCoverTracker.Instance;
            float th = QuakeTimeline.Instance != null ? QuakeTimeline.Instance.Config.requiredCoverRatio : 0.7f;
            bool Covered(QuakePhase p) => cover != null && cover.RatioOf(p) >= th;
            bool Done(string a) => c != null && c.IsDone(a);

            ScenarioFlags.Set(QuakeKeys.FlagCoverForeshock, Covered(QuakePhase.Foreshock));
            ScenarioFlags.Set(QuakeKeys.FlagGas, Done(QuakeKeys.ActGas));
            ScenarioFlags.Set(QuakeKeys.FlagBreaker, Done(QuakeKeys.ActBreaker));
            ScenarioFlags.Set(QuakeKeys.FlagDoor, Done(QuakeKeys.ActDoor));
            ScenarioFlags.Set(QuakeKeys.FlagShoes, Done(QuakeKeys.ActShoes) && !Injured);
            ScenarioFlags.Set(QuakeKeys.FlagNoElevator, !ElevatorCalled);
            ScenarioFlags.Set(QuakeKeys.FlagCoverMainShock, Covered(QuakePhase.MainShock));
            ScenarioFlags.Set(QuakeKeys.FlagCoverAftershock, Covered(QuakePhase.Aftershock));
            ScenarioFlags.Set(QuakeKeys.FlagOutdoorSite, ReachedOutdoorSite);
            ScenarioFlags.Set(QuakeKeys.FlagShelter, kind == QuakeClearKind.Shelter);
            ScenarioFlags.Set(QuakeKeys.FlagNoCalls, CallsDuringEvacuation == 0);
            ScenarioFlags.Set(QuakeKeys.FlagNoPrevented, QuakeOutcome.Instance == null || QuakeOutcome.Instance.PreventedDeaths.Count == 0);
            ScenarioFlags.Set(QuakeKeys.FlagBag, Done(QuakeKeys.ActBag));
            ScenarioFlags.Set(QuakeKeys.FlagNeighbor, NeighborFollowing);
            ScenarioFlags.Set(QuakeKeys.FlagFamilyText, FamilyContacted);
            Debug.Log($"[Quake] 점수 기록: 클리어={kind} 엘리베이터={ElevatorCalled} 부상={Injured} 이웃={NeighborFollowing} 통화={CallsDuringEvacuation} 문자={FamilyContacted} 옥외대피장소={ReachedOutdoorSite}");
        }
    }
}
