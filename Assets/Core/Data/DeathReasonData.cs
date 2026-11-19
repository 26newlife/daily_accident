using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>사망 원인별 교육 피드백 문구. Create > GameGameplay > Death Reason Data</summary>
    [CreateAssetMenu(fileName = "Death_New", menuName = "GameGameplay/Death Reason Data", order = 1)]
    public class DeathReasonData : ScriptableObject
    {
        [SerializeField] private DeathReason reason = DeathReason.Unknown;
        [SerializeField] private string title = "사망";
        [SerializeField, TextArea(2, 6)] private string whyYouDied;
        [SerializeField, TextArea(2, 6)] private string correctAction;

        public DeathReason Reason => reason;
        public string Title => title;
        /// <summary>왜 죽었는지</summary>
        public string WhyYouDied => whyYouDied;
        /// <summary>올바른 행동 안내</summary>
        public string CorrectAction => correctAction;
    }

}
