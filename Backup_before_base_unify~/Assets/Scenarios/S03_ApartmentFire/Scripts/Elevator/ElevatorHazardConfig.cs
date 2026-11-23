using UnityEngine;

namespace GameGameplay
{
    /// <summary>화재 후 엘리베이터 사망 시나리오 (호출 버튼을 처음 누르는 순간 한 판에 한 번 결정)</summary>
    public enum ElevatorScenario
    {
        None = 0,   // 아직 결정 전 / 평상시(화재 전) 정상 작동
        A = 1,      // 탑승 후 이동 중 "펑" → 급정지 → 연기 유입 → 질식사
        B = 2,      // 문이 열리는 순간 불꽃·연기 폭발 (문 앞이면 즉사)
    }

    /// <summary>
    /// 엘리베이터 설정 (Assets/Data/ElevatorHazardConfig.asset). 모든 시간·확률·세기는 여기서 조절한다.
    /// 클립 슬롯은 ElevatorController 의 AudioSource 에 직접 연결 (비워도 에러 없이 동작).
    /// 이전 버전의 확률 항목(버튼 화상·무반응 등)은 이 두 시나리오로 대체되어 삭제되었다.
    /// </summary>
    [CreateAssetMenu(fileName = "ElevatorHazardConfig", menuName = "GameGameplay/Elevator Hazard Config", order = 20)]
    public class ElevatorHazardConfig : ScriptableObject
    {
        [Header("시나리오 가중치 (화재 후, 호출 첫 순간에 한 번 결정)")]
        [Min(0f)] public float weightScenarioA = 50f;
        [Min(0f)] public float weightScenarioB = 50f;

        [Header("시나리오 A — 이동 중 정지·질식")]
        [Min(0.5f), Tooltip("이동 시작 후 '펑' 까지 (약 3~5초). 짧은 이동이면 이동 시간의 60%로 줄어든다")] public float popDelay = 4f;
        [Min(0.5f), Tooltip("급정지 후 연기가 들어오기까지 (약 4~6초)")] public float smokeDelay = 5f;
        [Min(1f), Tooltip("연기 유입 시작 후 질식 사망까지 (약 15~20초)")] public float suffocationTime = 17f;
        [Min(1f), Tooltip("젖은 수건 착용 시 질식 시간 배율 (조금만 지연)")] public float towelSuffocationMultiplier = 1.3f;
        [Min(1f), Tooltip("탑승만 하고 층을 고르지 않았을 때 같은 현상이 시작되기까지 (약 8~10초)")] public float idleTimeout = 9f;
        [Range(0f, 0.3f)] public float popShakeAmplitude = 0.2f;
        [Min(0.1f)] public float popShakeSeconds = 1.2f;
        [Range(0f, 1f), Tooltip("연기 유입 시 붉은 비네트 세기")] public float smokeRedFlashAlpha = 0.28f;

        [Header("시나리오 B — 문 열림 폭발")]
        [Min(0.5f), Tooltip("문 앞 이 반경(m) 안이면 즉사")] public float blastRadius = 2f;
        [Min(0f), Tooltip("폭발 후 즉사까지 (실시간 초, 섬광 연출)")] public float blastDeathDelay = 0.4f;
        [Range(0.1f, 1f), Tooltip("반경 밖일 때 복도에 생기는 불의 초기 크기")] public float blastFireIntensity = 0.7f;
        [Min(0f), Tooltip("반경의 2배 안이면 더해지는 화상 수치")] public float blastBurnAmount = 30f;
        [Min(0f), Tooltip("반경 밖에서도 깎이는 호흡기 게이지")] public float blastBreathLoss = 15f;
        [Range(0f, 0.3f)] public float blastShakeAmplitude = 0.18f;
        [Min(0.1f), Tooltip("NPC 가 피해를 입는 반경 배율")] public float blastNpcRadiusMultiplier = 1.5f;
    }
}
