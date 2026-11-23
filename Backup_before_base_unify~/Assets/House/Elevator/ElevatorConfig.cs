using UnityEngine;

namespace GameGameplay
{
    /// <summary>엘리베이터 정상 작동 시간 설정 (집 공용, Assets/Data/ElevatorConfig.asset). 시나리오 위험 수치는 시나리오 폴더의 ElevatorHazardConfig 에 둔다</summary>
    [CreateAssetMenu(fileName = "ElevatorConfig", menuName = "GameGameplay/Elevator Config", order = 19)]
    public class ElevatorConfig : ScriptableObject
    {
        [Min(0.5f), Tooltip("호출 후 도착까지 (약 3~5초)")] public float arrivalDelay = 4f;
        [Min(0.1f), Tooltip("문이 열리거나 닫히는 시간")] public float doorMoveSeconds = 0.8f;
        [Min(1f), Tooltip("도착 후 아무도 타지 않으면 문이 닫히기까지")] public float doorOpenHoldSeconds = 8f;
        [Min(0.1f), Tooltip("층 표시가 한 층 바뀌는 시간 (약 0.6~0.8초)")] public float floorTravelTime = 0.7f;
        [Min(0.1f), Tooltip("도착 후 복도로 돌아가기까지")] public float normalExitDelay = 1.6f;
        [Min(0.1f), Tooltip("탑승/하차 화면 전환(페이드) 길이")] public float boardFadeSeconds = 0.9f;
        [Range(0f, 0.1f), Tooltip("이동 중 미세 진동")] public float travelShakeAmplitude = 0.012f;
    }
}
