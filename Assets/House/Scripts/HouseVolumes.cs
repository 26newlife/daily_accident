using UnityEngine;
using UnityEngine.Rendering;

namespace GameGameplay
{
    /// <summary>
    /// 집의 후처리 Volume 두 장 (베이스 프리팹 안, 시나리오 로직 없음).
    ///   · day   : 평상시 실내 (뉴트럴 톤매핑, 은은한 블룸·비네트, 따뜻한 화이트 밸런스) — 항상 weight 1
    ///   · smoke : 연기·재난 분위기 (채도·노출 낮춤, 비네트 강화) — weight 0~1 로 블렌드
    /// 정전·소등·화재는 조명(LightGroup)과 환경광으로 연출하므로 이 Volume 과 충돌하지 않는다.
    /// HouseController.ApplyEnvironmentPreset 이 창밖 모드(DarkSmoky)에 맞춰 SetSmoke 를 호출한다.
    /// </summary>
    public class HouseVolumes : MonoBehaviour
    {
        [SerializeField] private Volume day;
        [SerializeField] private Volume smoke;
        [SerializeField, Min(0.1f), Tooltip("연기 Volume 이 목표 weight 로 가는 속도 (1/초)")] private float blendSpeed = 0.6f;

        public static HouseVolumes Instance { get; private set; }
        private float target;

        private void Awake() { Instance = this; if (smoke != null) smoke.weight = 0f; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>0 = 평상시, 1 = 연기 분위기 (서서히 전환)</summary>
        public void SetSmoke(float weight01) { target = Mathf.Clamp01(weight01); }

        private void Update()
        {
            if (smoke == null) return;
            smoke.weight = Mathf.MoveTowards(smoke.weight, target, blendSpeed * Time.unscaledDeltaTime);
        }
    }
}
