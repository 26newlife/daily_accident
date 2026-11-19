using UnityEngine;

namespace GameGameplay
{
    /// <summary>바람에 흔들리는 가로수 (셰이더 없이 가벼운 회전 애니메이션)</summary>
    public class TreeSway : MonoBehaviour
    {
        [SerializeField] private float maxDegrees = 2.5f;
        [SerializeField] private float speed = 0.8f;
        private float phase;
        private Quaternion home;
        private void Awake() { phase = Random.value * 10f; home = transform.localRotation; }
        private void Update()
        {
            float t = Time.time * speed + phase;
            transform.localRotation = home * Quaternion.Euler(Mathf.Sin(t) * maxDegrees, 0f, Mathf.Sin(t * 0.7f + 1f) * maxDegrees);
        }
    }
}
