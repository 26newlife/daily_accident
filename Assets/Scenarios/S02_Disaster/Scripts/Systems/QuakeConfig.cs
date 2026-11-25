using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2 수치 모음 (Assets/Scenarios/S02_Disaster/Data/QuakeConfig.asset). 빌더는 에셋이 이미 있으면 값을 덮어쓰지 않는다.
    /// 원본(QuakeSurvival)의 타임라인·판정 수치를 집(1004호, 20층 건물) 크기에 맞게 옮겼다.
    /// </summary>
    [CreateAssetMenu(fileName = "QuakeConfig", menuName = "GameGameplay/S02 Quake Config", order = 30)]
    public class QuakeConfig : ScriptableObject
    {
        [Header("단계별 길이(초)")]
        [Min(1f)] public float calmDuration = 15f;
        [Min(1f)] public float foreshockDuration = 8f;
        [Min(1f)] public float prepareDuration = 60f;
        [Min(1f)] public float mainShockDuration = 30f;
        [Min(1f), Tooltip("원본 25초 — 집(10층 + 복도) → 1층 출구 → 옥외대피장소 거리(약 170m)에 맞춰 늘림")] public float aftermathDuration = 50f;
        [Min(1f)] public float aftershockDuration = 10f;

        [Header("흔들림 세기(0~1)")]
        [Range(0f, 1f)] public float foreshockIntensity = 0.25f;
        [Range(0f, 1f)] public float mainShockIntensity = 1f;
        [Range(0f, 1f)] public float aftershockIntensity = 0.5f;
        [Min(0f)] public float rampSeconds = 2f;
        [Tooltip("세기 1일 때 카메라 흔들림 진폭(m)")] public float cameraShakeAmplitude = 0.07f;

        [Header("판정")]
        [Range(0f, 1f), Tooltip("흔들림 단계 동안 몸을 보호해야 하는 시간 비율")] public float requiredCoverRatio = 0.7f;
        [Tooltip("흔들림이 시작된 뒤 탁자 밑으로 가도록 움직여도 되는 시간")] public float shakeGraceSeconds = 2.5f;
        [Tooltip("흔들리는 동안 선 채로 움직여도 되는 거리(m)")] public float shakeAllowedDistance = 0.8f;
        [Tooltip("건물 외벽에서 이만큼 떨어져야 여진 때 안전한 넓은 곳")] public float facadeSafeDistance = 15f;
        [Tooltip("지진이 시작된 뒤 위층(11층 이상)으로 올라가면 무너지는 위층에 깔린다 (원본 규칙). 끄면 옥상까지 안전하다")] public bool upperFloorsCollapse = true;

        [Header("본진 규모")]
        [Range(0f, 1f), Tooltip("판마다 본진이 6점대(강진)일 확률. 나머지는 5점대. 5점대면 위층 붕괴가 없고 옥상에서 웅크려 버틸 수 있다")] public float strongQuakeChance = 0.7f;

        [Header("본진 뒤 건물 폭발 (본진 종료 기준, 초)")]
        public float buildingWarningDelay = 10f;
        public float buildingExplosionDelay = 70f;

        [Header("지진해일 (해일 단계 시작 기준, 초 / 월드 z, y)")]
        public float tsunamiDrawbackEnd = 30f;
        public float tsunamiArrivalEnd = 70f;
        public float tsunamiHoldEnd = 85f;
        public float waterCalmFrontZ = 300f;
        public float waterDrawbackFrontZ = 360f;
        public float waterPeakFrontZ = -75f;
        [Tooltip("지면(-28.5) 기준 최고 수위(m)")] public float waterPeakHeight = 5.5f;

        [Header("기타")]
        [Range(0.1f, 1f), Tooltip("맨발로 유리를 밟았을 때 이동 속도 배율")] public float injuredSpeed = 0.6f;
        [Range(0.1f, 1f), Tooltip("다친 이웃을 부축할 때 이동 속도 배율")] public float neighborCarrySpeed = 0.75f;
        [Min(0), Tooltip("대피지역 덕분에 넘긴 사고 1건당 감점 (점수 항목 '대피지역 덕분에 넘긴 사고가 없다')")] public int protectedPenalty = 15;
    }
}
