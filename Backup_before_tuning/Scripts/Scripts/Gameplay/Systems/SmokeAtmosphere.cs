using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 연기 시야 효과: 플레이어가 체감하는 연기 농도에 맞춰 안개(RenderSettings.fog)를 짙게 만든다.
    /// 연기는 천장부터 차오르므로 웅크리면 시야가 더 트인다 (crouchFactor).
    /// </summary>
    public class SmokeAtmosphere : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Color smokeColor = new Color(0.07f, 0.07f, 0.08f);
        [SerializeField, Min(0f), Tooltip("연기 농도 1.0 에서의 안개 밀도 (Exponential Squared)")] private float maxDensity = 0.3f;
        [SerializeField, Range(0.1f, 1f)] private float crouchFactor = 0.55f;
        [SerializeField, Min(0.1f)] private float smoothing = 2.5f;

        private float density;
        private bool previousFog;
        private Color previousColor;
        private FogMode previousMode;
        private float previousDensity;

        private void OnEnable()
        {
            previousFog = RenderSettings.fog; previousColor = RenderSettings.fogColor;
            previousMode = RenderSettings.fogMode; previousDensity = RenderSettings.fogDensity;
        }

        private void OnDisable()
        {
            RenderSettings.fog = previousFog; RenderSettings.fogColor = previousColor;
            RenderSettings.fogMode = previousMode; RenderSettings.fogDensity = previousDensity;
        }

        private void Update()
        {
            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            float posture = player != null && player.IsCrouching ? crouchFactor : 1f;
            density = Mathf.Lerp(density, smoke * posture * maxDensity, Time.unscaledDeltaTime * smoothing);

            bool on = density > 0.003f;
            RenderSettings.fog = on;
            if (!on) return;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = smokeColor;
            RenderSettings.fogDensity = density;
        }
    }
}
