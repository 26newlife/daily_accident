using System.Text;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>엘리베이터 호출 결과 종류</summary>
    public enum ElevatorOutcome
    {
        ButtonBurn = 0,       // 버튼/패널이 뜨거워 화상
        NoResponse = 1,       // 정전·고장으로 무반응
        ArriveFireball = 2,   // 도착 → 문이 열리며 불길이 덮침
        ArriveSmoke = 3,      // 도착 → 문이 열리며 유독가스
        ArriveSafe = 4,       // 도착 (극히 낮은 확률). 탑승하면 여전히 승강로 연기로 위험
    }

    /// <summary>
    /// 화재 중 엘리베이터 사용 시 결과의 확률 가중치와 수치. 모든 값은 Inspector 에서 조절한다 (Assets/Data/ElevatorHazardConfig.asset).
    /// 위험도 배율 = 1 + 연기 × smokeBoost + 불 근접 × fireBoost + 경과 분 × minuteBoost + (누른 횟수-1) × pressBoost
    /// 위험 결과(화상/화염/가스)의 가중치에 이 배율을 곱하고, 안전 결과는 배율로 나눈다. 무반응은 그대로.
    /// </summary>
    [CreateAssetMenu(fileName = "ElevatorHazardConfig", menuName = "GameGameplay/Elevator Hazard Config", order = 20)]
    public class ElevatorHazardConfig : ScriptableObject
    {
        [Header("기본 가중치 (화재 중)")]
        [Min(0f)] public float weightButtonBurn = 30f;
        [Min(0f)] public float weightNoResponse = 35f;
        [Min(0f)] public float weightArriveFireball = 16f;
        [Min(0f)] public float weightArriveSmoke = 16f;
        [Min(0f), Tooltip("거의 없는 값으로 둔다 — 화재 중에는 쓰면 안 된다는 교육 목적")] public float weightArriveSafe = 1f;

        [Header("위험도 배율")]
        [Min(0f), Tooltip("복도 연기 농도 1.0 당 추가 배율")] public float smokeBoost = 1.5f;
        [Min(0f), Tooltip("복도 불에 가까울수록(0~1) 추가 배율")] public float fireBoost = 1.0f;
        [Min(0f), Tooltip("화재 시작 후 1분 경과당 추가 배율")] public float minuteBoost = 0.5f;
        [Min(0f), Tooltip("같은 버튼을 한 번 더 누를 때마다 추가 배율")] public float pressBoost = 0.35f;
        [Min(0.1f), Tooltip("불 근접 판정 반경(m). 복도 불에서 이 거리 안이면 1.0")] public float fireNearRadius = 6f;

        [Header("평상시 (화재 전) 동작")]
        [Min(0f)] public float normalArriveSeconds = 2.5f;
        [Min(0f)] public float normalDoorOpenSeconds = 7f;
        public string normalArriveMessage = "엘리베이터가 도착했다.";
        public string normalRideMessage = "엘리베이터를 한 바퀴 타고 같은 층으로 돌아왔다.";

        [Header("결과: 버튼 과열 화상")]
        [Min(0f), Tooltip("Burn 수치(0~100)에 더해지는 양")] public float burnAmount = 45f;
        public string burnMessage = "버튼이 불에 달군 듯 뜨겁다! 손이 타는 듯 아프다.";

        [Header("결과: 무반응")]
        public string noResponseMessage = "버튼을 눌러도 불이 들어오지 않는다. 정전된 것 같다.";

        [Header("결과: 도착")]
        [Min(0f)] public float arriveSeconds = 3.5f;
        public string arriveHintMessage = "어디선가 도착 소리가 났다.";
        [Min(0f), Tooltip("문이 열린 뒤 사망까지 (실시간 초)")] public float fireballDeathDelay = 0.5f;
        [Min(0f)] public float smokeDeathDelay = 2.5f;
        public string fireballMessage = "문이 열리자 뜨거운 불길이 쏟아져 나온다!";
        public string smokeMessage = "문이 열리자 검은 연기가 한꺼번에 쏟아져 나온다!";
        public string safeMessage = "엘리베이터 문이 열렸다. 안은 어둡고 매캐하다.";
        [Min(0f)] public float safeDoorOpenSeconds = 6f;

        [Header("탑승")]
        [Min(0f), Tooltip("탑승 후 갇혀 질식하기까지 (실시간 초)")] public float rideDeathSeconds = 5f;
        public string rideMessage = "문이 닫히자 불이 꺼지고 엘리베이터가 멈췄다. 승강로로 연기가 밀려든다...";

        /// <summary>가중치를 굴려 결과를 정한다. details 에는 [Risk] 로그용 설명이 담긴다.</summary>
        public ElevatorOutcome Roll(float smoke01, float fire01, float minutesSinceFire, int pressCount, out string details)
        {
            float risk = 1f + smoke01 * smokeBoost + fire01 * fireBoost + minutesSinceFire * minuteBoost + Mathf.Max(0, pressCount - 1) * pressBoost;
            float[] w =
            {
                weightButtonBurn * risk,
                weightNoResponse,
                weightArriveFireball * risk,
                weightArriveSmoke * risk,
                weightArriveSafe / risk,
            };

            float total = 0f;
            foreach (float v in w) total += v;
            ElevatorOutcome result = ElevatorOutcome.NoResponse;
            if (total > 0f)
            {
                float r = Random.value * total, acc = 0f;
                for (int i = 0; i < w.Length; i++)
                {
                    acc += w[i];
                    if (r <= acc) { result = (ElevatorOutcome)i; break; }
                }
            }

            var sb = new StringBuilder();
            sb.Append($"smoke={smoke01:0.00} fire={fire01:0.00} min={minutesSinceFire:0.0} press={pressCount} risk×{risk:0.00} | ");
            for (int i = 0; i < w.Length; i++) sb.Append($"{(ElevatorOutcome)i}={(total > 0f ? w[i] / total * 100f : 0f):0}% ");
            details = sb.ToString();
            return result;
        }
    }
}
