using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>인벤토리 아이템 정의. Create > GameGameplay > Item Data</summary>
    [CreateAssetMenu(fileName = "Item_New", menuName = "GameGameplay/Item Data", order = 0)]
    public class ItemData : ScriptableObject
    {
        [SerializeField] private string id = "item_id";
        [SerializeField] private string displayName = "아이템";
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("착용/장비 가능한 아이템인가 (예: 젖은 수건). 사용키로 착용·해제가 토글된다.")]
        [SerializeField] private bool equippable;
        [Tooltip("아이콘이 없을 때 슬롯에 표시할 색 / 머리글자")]
        [SerializeField] private Color fallbackColor = new Color(0.45f, 0.6f, 0.8f, 1f);

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public bool Equippable => equippable;
        public Color FallbackColor => fallbackColor;
    }

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

    /// <summary>엔딩 정의. Create > GameGameplay > Ending Data</summary>
    [CreateAssetMenu(fileName = "Ending_New", menuName = "GameGameplay/Ending Data", order = 2)]
    public class EndingData : ScriptableObject
    {
        [SerializeField] private string id = "ending_id";
        [SerializeField] private string endingName = "엔딩";
        [SerializeField, TextArea(2, 6)] private string description;
        [Tooltip("생존 엔딩인가 (표시 색/문구 구분용)")]
        [SerializeField] private bool isSurvival = true;

        public string Id => id;
        public string EndingName => endingName;
        public string Description => description;
        public bool IsSurvival => isSurvival;
    }

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

    /// <summary>목표 정의. Create > GameGameplay > Objective Data</summary>
    [CreateAssetMenu(fileName = "Objective_New", menuName = "GameGameplay/Objective Data", order = 4)]
    public class ObjectiveData : ScriptableObject
    {
        [SerializeField] private string id = "objective_id";
        [SerializeField] private string title = "목표";
        [SerializeField, TextArea(1, 3)] private string description;
        [Tooltip("메인 목표인가 (아니면 서브 목표)")]
        [SerializeField] private bool isMain = true;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public bool IsMain => isMain;
    }
}
