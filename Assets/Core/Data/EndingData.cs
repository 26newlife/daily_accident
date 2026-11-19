using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>엔딩 정의. Create > GameGameplay > Ending Data</summary>
    [CreateAssetMenu(fileName = "Ending_New", menuName = "GameGameplay/Ending Data", order = 2)]
    public class EndingData : ScriptableObject
    {
        [SerializeField] private string id = "ending_id";
        [SerializeField] private string endingName = "엔딩";
        [SerializeField, TextArea(2, 6)] private string description;
        [Tooltip("생존 엔딩인가 (표시 색/문구 구분용)")]
        [SerializeField] private bool isSurvival = true;
        [Tooltip("히든 엔딩인가 (엔딩 모음에서 발견 전에는 ??? 로 표시)")]
        [SerializeField] private bool isHidden;
        [Tooltip("이 엔딩의 루트 라벨 (예: 루트 A · 옥상). 엔딩 화면 소제목")]
        [SerializeField] private string routeLabel;

        public string Id => id;
        public string EndingName => endingName;
        public string Description => description;
        public bool IsSurvival => isSurvival;
        public bool IsHidden => isHidden;
        public string RouteLabel => routeLabel;
    }

}
