using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>점수 항목 정의 (예: 119 신고, 가스 차단). Create > GameGameplay > Score Category</summary>
    [CreateAssetMenu(fileName = "Score_New", menuName = "GameGameplay/Score Category", order = 3)]
    public class ScoreCategoryData : ScriptableObject
    {
        [SerializeField] private string id = "score_id";
        [SerializeField] private string label = "항목";
        [SerializeField, Min(0)] private int maxPoints = 10;

        public string Id => id;
        public string Label => label;
        public int MaxPoints => maxPoints;
    }

}
