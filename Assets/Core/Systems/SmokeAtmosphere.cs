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

        [Header("천장 연기층 (연기는 천장부터 내려온다)")]
        [SerializeField] private bool ceilingLayer = true;
        [SerializeField, Min(1f), Tooltip("천장 높이(m)")] private float ceilingHeight = 2.4f;
        [SerializeField, Min(0.1f), Tooltip("연기 1.0 일 때 연기층 아래면이 내려오는 깊이(m). 1.6 이면 바닥에서 0.8m")] private float layerMaxDrop = 1.6f;
        [SerializeField] private Color layerColor = new Color(0.08f, 0.08f, 0.09f, 0.7f);

        private float density;
        private Transform layer;
        private Renderer layerRenderer;
        private float layerY;
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

        private void OnDestroy()
        {
            if (layer != null) Destroy(layer.gameObject);
        }

        /// <summary>플레이어 머리 위에 떠서 연기 농도에 따라 내려오는 어두운 판. 천장이 없는 곳(옥상·집결지)이거나 연기가 옅으면 숨긴다. 웅크리면 판 아래로 들어가 시야가 트인다</summary>
        private void UpdateCeilingLayer(float smoke)
        {
            if (!ceilingLayer || player == null) return;
            if (layer == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "SmokeCeilingLayer";
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(14f, 14f, 1f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 아래를 향한 판
                layerRenderer = go.GetComponent<Renderer>();
                layerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                layerRenderer.receiveShadows = false;
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null) layerRenderer.material = new Material(sh);
                layer = go.transform;
            }

            Vector3 p = player.transform.position;
            bool hasCeiling = Physics.Raycast(p + Vector3.up * 1.0f, Vector3.up, out RaycastHit hit, 3.2f, ~0, QueryTriggerInteraction.Ignore);
            float ceilY = hasCeiling ? hit.point.y : p.y + ceilingHeight;
            bool show = hasCeiling && smoke > 0.08f;
            layerRenderer.enabled = show;
            if (!show) return;

            float target = ceilY - Mathf.Clamp01(smoke) * layerMaxDrop;
            layerY = Mathf.Lerp(layerY <= 0f ? ceilY : layerY, target, Time.deltaTime * 1.5f);
            layer.position = new Vector3(p.x, layerY, p.z);
            Color c = layerColor; c.a = Mathf.Lerp(0.15f, layerColor.a, Mathf.Clamp01(smoke * 1.5f));
            if (layerRenderer.material.HasProperty("_Color")) layerRenderer.material.color = c;
        }

        private void Update()
        {
            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            UpdateCeilingLayer(smoke);
            float posture = player != null && player.IsCrouching ? crouchFactor : 1f;
            density = Mathf.Lerp(density, smoke * posture * maxDensity, Time.deltaTime * smoothing);

            bool on = density > 0.003f;
            RenderSettings.fog = on;
            if (!on) return;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = smokeColor;
            RenderSettings.fogDensity = density;
        }
    }
}
