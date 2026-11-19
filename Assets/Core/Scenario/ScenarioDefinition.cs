using GameGameplay;
using UnityEngine;

namespace GameData
{
    /// <summary>
    /// 시나리오 1개의 "실행 정의" (ScriptableObject). 메인 메뉴용 ScenarioData 가 이 에셋을 가리키고,
    /// HomeBase 씬의 ScenarioRunner 가 이 정의를 읽어 시나리오 씬을 Additive 로 로드한다.
    /// 새 시나리오는 Tools > GameUI > Scenarios > Create New Scenario 마법사로 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDefinition_New", menuName = "GameData/Scenario Definition", order = 2)]
    public class ScenarioDefinition : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string id = "S00";
        [SerializeField] private string displayName = "시나리오";
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite thumbnail;
        [SerializeField, Tooltip("플레이 가능한가 (false 면 로드하지 않고 안내 후 메인 메뉴로 돌아간다)")] private bool isPlayable = true;
        [SerializeField, Min(1f), Tooltip("목표 플레이 시간(분) — 템포 설계용 문서 값")] private float targetPlayMinutes = 7f;

        [Header("시나리오 씬 (Additive)")]
        [SerializeField, Tooltip("Build Settings 에 등록된 시나리오 씬 이름 (예: S03_ApartmentFire)")] private string scenarioSceneName;
        [SerializeField, Tooltip("시작 스폰 id (집의 SpawnPoint). 예: spawn_bedroom_bed")] private string startSpawnId = "spawn_bedroom_bed";
        [SerializeField] private EnvironmentPreset environmentPreset;

        [Header("진행")]
        [SerializeField, Tooltip("Act 구성 문서용 (실제 Act 목록은 시나리오 씬의 ScenarioSceneRoot.acts)")] private string[] actTypeNames;
        [SerializeField, Tooltip("이 시나리오가 사용하는 아이템 (검증용: 에셋이 존재하는지 확인)")] private ItemData[] requiredItems;

        [Header("시나리오 전용 데이터 (시나리오 폴더에서 관리)")]
        [SerializeField] private DeathReasonData[] deathReasons;
        [SerializeField] private DeathReasonData fallbackDeath;
        [SerializeField] private EndingData[] endings;
        [SerializeField, Tooltip("히든 엔딩 id / 그 조건이 되는 ScenarioFlags 키 (비우면 없음)")] private string hiddenEndingId;
        [SerializeField] private string hiddenEndingFlag;
        [SerializeField] private ScoreRules scoreRules;
        [SerializeField] private HintData[] hints;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite BackgroundSprite => backgroundSprite;
        public Sprite Thumbnail => thumbnail;
        public bool IsPlayable => isPlayable;
        public float TargetPlayMinutes => targetPlayMinutes;
        public string ScenarioSceneName => scenarioSceneName;
        public string StartSpawnId => startSpawnId;
        public EnvironmentPreset EnvironmentPreset => environmentPreset;
        public string[] ActTypeNames => actTypeNames;
        public ItemData[] RequiredItems => requiredItems;
        public DeathReasonData[] DeathReasons => deathReasons;
        public DeathReasonData FallbackDeath => fallbackDeath;
        public EndingData[] Endings => endings;
        public string HiddenEndingId => hiddenEndingId;
        public string HiddenEndingFlag => hiddenEndingFlag;
        public ScoreRules ScoreRules => scoreRules;
        public HintData[] Hints => hints;
    }
}
