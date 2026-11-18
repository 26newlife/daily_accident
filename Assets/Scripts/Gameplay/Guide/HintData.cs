using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>힌트가 표시되는 조건</summary>
    public enum HintTrigger
    {
        FirstMove = 0,          // 처음 이동 입력
        FirstAim = 1,           // 상호작용 대상을 처음 조준
        FirstAimHold = 2,       // 길게 눌러야 하는 대상을 처음 조준
        AfterSeconds = 3,       // 플레이 시작 후 N초 (triggerValue)
        GasSmell = 4,           // 가스 냄새를 처음 감지 (체감 가스 농도 > triggerValue)
        BroadcastHeard = 5,     // 첫 자막(긴급 방송) 이후 triggerValue 초
        SmokeEntered = 6,       // 체감 연기 농도 > triggerValue
        VitalsShown = 7,        // 호흡기 게이지가 처음 표시될 때
        ItemAcquiredAny = 8,    // 첫 아이템 획득
        ItemAcquiredId = 9,     // 특정 아이템 id 획득 (triggerParam)
        NearInteractable = 10,  // 상호작용 오브젝트 id(triggerParam)에 triggerValue(m) 이내로 접근
        ScenarioEvent = 11,     // 시나리오 이벤트 (scenarioType + triggerParam 의 id 목록 ';' 구분 + eventState)
    }

    /// <summary>
    /// 튜토리얼 힌트 1건. 문구는 조작/시스템 설명만 담고 위험 행동의 "정답"은 쓰지 않는다 (교육용 게임이므로).
    /// 문구 안의 {interact} {phone} {crouch} {sprint} {jump} 는 현재 입력 장치(키보드/패드)의 버튼 이름으로 바뀐다.
    /// </summary>
    [CreateAssetMenu(fileName = "Hint_New", menuName = "Game/Hint Data")]
    public class HintData : ScriptableObject
    {
        [SerializeField] private string id = "hint_id";
        [SerializeField, TextArea(1, 3)] private string text;
        [SerializeField] private HintTrigger trigger = HintTrigger.FirstMove;
        [SerializeField, Tooltip("트리거 보조값: 초(AfterSeconds/BroadcastHeard), 농도(GasSmell/SmokeEntered), 거리 m(NearInteractable)")] private float triggerValue = 1f;
        [SerializeField, Tooltip("대상 id (NearInteractable/ItemAcquiredId/ScenarioEvent). 여러 개는 ';' 로 구분")] private string triggerParam;
        [SerializeField] private ScenarioEventType scenarioType = ScenarioEventType.Toggled;
        [SerializeField, Tooltip("ScenarioEvent 의 State 조건 (true = 켜짐/열림)")] private bool eventState = true;
        [SerializeField, Tooltip("체크 해제 = 처음 1회만 표시")] private bool repeat;
        [SerializeField, Min(0f), Tooltip("표시 전 대기 시간(초)")] private float delay;
        [SerializeField, Min(1f)] private float duration = 3f;
        [SerializeField, Tooltip("여러 힌트가 대기 중일 때 큰 값이 먼저")] private int priority;
        [SerializeField, Tooltip("체크 = Easy 난이도에서만 표시 (상황 판단에 가까운 힌트). 해제 = Normal 이상에서도 표시")] private bool easyOnly;

        public string Id => id;
        public string Text => text;
        public HintTrigger Trigger => trigger;
        public float TriggerValue => triggerValue;
        public string TriggerParam => triggerParam;
        public ScenarioEventType ScenarioType => scenarioType;
        public bool EventState => eventState;
        public bool Repeat => repeat;
        public float Delay => delay;
        public float Duration => duration;
        public int Priority => priority;
        public bool EasyOnly => easyOnly;

        /// <summary>현재 난이도에서 이 힌트를 보여줄 수 있는가</summary>
        public bool IsAllowedAt(HintLevel level)
        {
            switch (level)
            {
                case HintLevel.Easy: return true;
                case HintLevel.Normal: return !easyOnly;
                default: return false;
            }
        }
    }
}
