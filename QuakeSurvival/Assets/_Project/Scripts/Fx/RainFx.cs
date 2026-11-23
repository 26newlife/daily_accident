using QuakeSurvival.Art;
using QuakeSurvival.Safety;
using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>플레이어 머리 위에서 폭우를 내린다. 건물 밖에 있을 때만 켠다.</summary>
    public sealed class RainFx : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private ZoneSensor zones;
        [SerializeField] private Material rainMaterial;
        [SerializeField] private float height = 12f;
        [SerializeField] private float rate = 1500f;

        private ParticleSystem rain;

        private void Awake()
        {
            if (player == null || zones == null || rainMaterial == null)
            {
                Debug.LogError("[RainFx] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            rain = CreateRain();
        }

        private void OnEnable()
        {
            if (zones != null) zones.AreaChanged += OnArea;
        }

        private void OnDisable()
        {
            if (zones != null) zones.AreaChanged -= OnArea;
        }

        private void LateUpdate()
        {
            transform.position = player.position + Vector3.up * height;
        }

        private void OnArea(PlayerArea area)
        {
            bool outdoors = area.IsOutdoors();
            var emission = rain.emission;
            emission.enabled = outdoors;
        }

        private ParticleSystem CreateRain()
        {
            var spec = new ParticleFactory.Spec(Palette.Rain, 0.9f, 22f, 0.04f, 0f, 1f);
            ParticleSystem ps = ParticleFactory.Loop(transform, rainMaterial, spec, rate);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            // 스케일은 회전 전에 적용되므로, x축 90도 회전 후 수평 판이 되려면 z가 얇아야 한다
            shape.scale = new Vector3(24f, 24f, 0.1f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1f;

            var emission = ps.emission;
            emission.enabled = false;
            return ps;
        }
    }
}
