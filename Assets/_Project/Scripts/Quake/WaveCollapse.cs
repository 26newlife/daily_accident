using QuakeSurvival.Fx;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 해일 앞면이 닿으면 무너지는 물체(건물, 차, 나무, 가로등).
    /// 묶인 대상들을 한 덩어리로 기울이고(tilt), 가라앉히고(sink), 내륙 쪽으로 밀어(push) 무너지는 모습을 만든다.
    /// 시작할 때 먼지/물보라를 뿜는다. 처음 자세는 Awake에서 기억한다.
    /// </summary>
    public sealed class WaveCollapse : MonoBehaviour
    {
        [SerializeField] private TsunamiController tsunami;
        [SerializeField] private FxLibrary fx;
        [SerializeField] private Transform[] targets = new Transform[0];
        [SerializeField] private Vector3 pivot;
        [Tooltip("해일 앞면(FrontZ)이 이 z 이하로 오면 무너지기 시작한다(보통 물체의 바다 쪽 면).")]
        [SerializeField] private float triggerZ;
        [SerializeField] private Vector3 tiltEuler = new Vector3(-12f, 0f, 0f);
        [SerializeField] private float sink = 5f;
        [SerializeField] private Vector3 push = new Vector3(0f, 0f, -4f);
        [SerializeField] private float duration = 6f;
        [SerializeField] private float dustScale = 1f;

        private Vector3[] startPositions;
        private Quaternion[] startRotations;
        private float startTime = -1f;

        public bool HasCollapsed => startTime >= 0f;

        private void Awake()
        {
            if (tsunami == null || fx == null || !fx.IsValid)
            {
                Debug.LogError("[WaveCollapse] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            startPositions = new Vector3[targets.Length];
            startRotations = new Quaternion[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                startPositions[i] = targets[i].position;
                startRotations[i] = targets[i].rotation;
            }
        }

        private void Update()
        {
            if (!HasCollapsed)
            {
                if (tsunami.IsArriving && tsunami.FrontZ <= triggerZ) Begin();
                return;
            }

            float t = Mathf.Clamp01((Time.time - startTime) / Mathf.Max(0.01f, duration));
            float eased = t * t * (3f - 2f * t);
            Apply(eased);
            if (t >= 1f) enabled = false;
        }

        private void Begin()
        {
            startTime = Time.time;
            FreezePhysics();
            Debug.Log($"[WaveCollapse] {name} collapses (front={tsunami.FrontZ:0.0})");
            var dust = new GameObject("CollapseDust").transform;
            dust.position = pivot + Vector3.up * 2f * dustScale;
            var spec = new ParticleFactory.Spec(new Color(0.55f, 0.55f, 0.52f, 0.7f), 7f, 5f * dustScale, 7f * dustScale, -0.02f, 4f * dustScale);
            ParticleFactory.Burst(dust, fx.Smoke, spec, Mathf.RoundToInt(30 * dustScale));
            Destroy(dust.gameObject, 12f);
        }

        /// <summary>
        /// 무너지는 동안 묶음 전체를 매 프레임 옮기므로, 안의 Rigidbody는 고정(kinematic)해 같이 움직이게 하고
        /// 충돌체는 끈다(움직이는 정적 충돌체를 매 프레임 다시 계산하지 않도록). 물에 잠긴 곳이라 충돌이 필요 없다.
        /// </summary>
        private void FreezePhysics()
        {
            foreach (Transform t in targets)
            {
                if (t == null) continue;
                foreach (var body in t.GetComponentsInChildren<Rigidbody>())
                {
                    body.isKinematic = true;
                    body.interpolation = RigidbodyInterpolation.None;
                }
                foreach (var col in t.GetComponentsInChildren<Collider>())
                {
                    if (!col.isTrigger) col.enabled = false;
                }
            }
        }

        private void Apply(float k)
        {
            Quaternion r = Quaternion.Euler(tiltEuler * k);
            Vector3 offset = push * k + Vector3.down * (sink * k);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].SetPositionAndRotation(pivot + r * (startPositions[i] - pivot) + offset, r * startRotations[i]);
            }
        }
    }
}
