using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 단지 도로를 천천히 도는 자동차. 경로 지점을 따라 달리며 모서리에서 감속하고, 감속 중에는 브레이크등이 밝아진다.
    /// 헤드라이트는 항상 켜져 있다 (아침). 주차된 차량은 이 컴포넌트 없이 정적 오브젝트로 둔다.
    /// </summary>
    public class VehicleController : MonoBehaviour
    {
        [SerializeField] private Vector3[] path = new Vector3[0];
        [SerializeField, Min(0.5f)] private float cruiseSpeed = 5f;
        [SerializeField] private float cornerSpeed = 2.2f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float turnRate = 4f;
        [SerializeField] private Renderer[] brakeLights;
        [SerializeField] private Color brakeOff = new Color(0.35f, 0.04f, 0.04f);
        [SerializeField] private Color brakeOn = new Color(1f, 0.08f, 0.05f);
        [SerializeField, Tooltip("사이렌 반응: 폭발 후 이 시간 동안 정차")] private float stopAfterExplosion = 20f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private int index;
        private float speed, stopUntil;
        private MaterialPropertyBlock block;
        private bool braking;

        public void Configure(Vector3[] worldPath, float cruise)
        {
            path = worldPath; cruiseSpeed = cruise;
            index = Random.Range(0, Mathf.Max(1, path.Length));
            if (path.Length > 0) transform.position = path[index];
        }

        private void Awake() => block = new MaterialPropertyBlock();

        /// <summary>폭발이 나면 잠시 정차 (운전자들이 멈춰서 본다)</summary>
        public void StopForSeconds(float seconds) => stopUntil = Time.time + seconds;

        private void Update()
        {
            if (path == null || path.Length < 2) return;
            Vector3 target = path[index];
            Vector3 to = target - transform.position; to.y = 0f;

            float desired = cruiseSpeed;
            if (to.magnitude < 6f) desired = cornerSpeed;                    // 지점 근처에서 감속
            if (Time.time < stopUntil) desired = 0f;

            float before = speed;
            speed = Mathf.MoveTowards(speed, desired, acceleration * Time.deltaTime);
            SetBrake(speed < before - 0.001f || desired == 0f && speed > 0.01f);

            if (to.magnitude < 1.5f) { index = (index + 1) % path.Length; return; }
            Vector3 dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * turnRate);
            transform.position += transform.forward * (speed * Time.deltaTime);
        }

        private void SetBrake(bool on)
        {
            if (on == braking) return;
            braking = on;
            if (brakeLights == null) return;
            foreach (Renderer r in brakeLights)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, on ? brakeOn : brakeOff);
                r.SetPropertyBlock(block);
            }
        }
    }
}
