using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>한자리에 고정된 비(창밖 풍경용). 실내 창문 너머로 폭우가 보이게 한다.</summary>
    public sealed class StaticRain : MonoBehaviour
    {
        [SerializeField] private Material rainMaterial;
        [SerializeField] private Vector2 area = new Vector2(30f, 14f);
        [SerializeField] private float rate = 1200f;
        [SerializeField] private Color color = new Color(0.7f, 0.75f, 0.85f, 0.45f);

        private void Start()
        {
            if (rainMaterial == null)
            {
                Debug.LogError("[StaticRain] rainMaterial이 연결되지 않았습니다.", this);
                return;
            }
            var spec = new ParticleFactory.Spec(color, 1.4f, 22f, 0.04f, 0f, 1f);
            ParticleSystem ps = ParticleFactory.Loop(transform, rainMaterial, spec, rate);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, area.y, 0.1f); // x축 90도 회전 후 수평 판
            shape.rotation = new Vector3(90f, 0f, 0f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
        }
    }
}
