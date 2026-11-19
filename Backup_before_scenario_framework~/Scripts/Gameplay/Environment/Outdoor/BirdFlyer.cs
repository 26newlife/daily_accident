using UnityEngine;

namespace GameGameplay
{
    /// <summary>가끔 날아가는 새: 중심 주위를 큰 원으로 돌며 날개를 퍼덕인다</summary>
    public class BirdFlyer : MonoBehaviour
    {
        [SerializeField] private Vector3 center = Vector3.zero;
        [SerializeField] private float radius = 40f;
        [SerializeField] private float angularSpeed = 0.25f;
        [SerializeField] private float bobAmplitude = 2f;
        [SerializeField] private Transform leftWing, rightWing;
        [SerializeField] private float flapSpeed = 14f, flapDegrees = 35f;
        private float phase;

        public void Configure(Vector3 c, float r, float speed) { center = c; radius = r; angularSpeed = speed; }
        private void Awake() { phase = Random.value * 6.28f; }

        private void Update()
        {
            float a = Time.time * angularSpeed + phase;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 1.7f) * bobAmplitude, Mathf.Sin(a) * radius);
            Vector3 vel = pos - transform.position;
            transform.position = pos;
            if (vel.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
            float f = Mathf.Sin(Time.time * flapSpeed + phase) * flapDegrees;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, f);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -f);
        }
    }
}
