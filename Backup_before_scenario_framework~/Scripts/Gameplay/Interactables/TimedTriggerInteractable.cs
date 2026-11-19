using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>길게 누르는 시간을 GameTimingConfig 에서 읽는 TriggerInteractable (열쇠함 유리 깨기, 구조 신호 손 흔들기 등)</summary>
    public class TimedTriggerInteractable : TriggerInteractable
    {
        public enum HoldSource
        {
            None = 0,
            RoofKeyBox = 1,    // GameTimingConfig.roofKeyBoxHoldSeconds
            RoofSignal = 2,    // GameTimingConfig.roofSignalHoldSeconds
            Seal = 3,          // GameTimingConfig.sealHoldSeconds
            Lift = 4,          // GameTimingConfig.npcLiftHoldSeconds
        }

        [Header("길게 누르기 (GameTimingConfig)")]
        [SerializeField] private HoldSource holdSource = HoldSource.None;

        public override float HoldSeconds
        {
            get
            {
                GameTimingConfig t = GameTimingConfig.Current;
                switch (holdSource)
                {
                    case HoldSource.RoofKeyBox: return t.roofKeyBoxHoldSeconds;
                    case HoldSource.RoofSignal: return t.roofSignalHoldSeconds;
                    case HoldSource.Seal: return t.sealHoldSeconds;
                    case HoldSource.Lift: return t.npcLiftHoldSeconds;
                    default: return base.HoldSeconds;
                }
            }
        }
    }
}
