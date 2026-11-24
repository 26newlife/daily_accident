using UnityEngine;

namespace GameData
{
    /// <summary>
    /// 시나리오 1개의 정보를 담는 데이터 에셋.
    /// 새 시나리오는 Project 창에서 Create > GameData > Scenario Data 로 만들고
    /// ScenarioDatabase 목록에 추가하면 팝업에 슬롯이 자동으로 생긴다.
    /// </summary>
    [CreateAssetMenu(fileName = "Scenario_New", menuName = "GameData/Scenario Data", order = 0)]
    public class ScenarioData : ScriptableObject
    {
        [SerializeField] private string id = "scenario_id";
        [SerializeField] private string displayName = "???";
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private bool isUnlocked;
        [Tooltip("선택 시 메인 배경으로 교체될 이미지. 비워두면 기존 배경을 유지한다.")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite thumbnail;
        [Tooltip("이 시나리오의 실행 정의 (씬·시작 위치·환경). 비어 있으면 START GAME 시 안내 후 메인 메뉴로 돌아간다.")]
        [SerializeField] private ScenarioDefinition definition;
        [Tooltip("비어 있지 않으면 START GAME 시 GameScene 대신 이 씬을 단독으로 로드한다 (독립 실행형 시나리오용, Build Settings 등록 필요).")]
        [SerializeField] private string standaloneSceneName;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public bool IsUnlocked => isUnlocked;
        public Sprite BackgroundSprite => backgroundSprite;
        public Sprite Thumbnail => thumbnail;
        public ScenarioDefinition Definition => definition;
        public string StandaloneSceneName => standaloneSceneName;
    }
}
