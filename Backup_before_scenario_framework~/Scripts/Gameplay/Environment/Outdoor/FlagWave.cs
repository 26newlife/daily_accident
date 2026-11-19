using UnityEngine;

namespace GameGameplay
{
    /// <summary>펄럭이는 깃발 (경비실/단지 입구)</summary>
    public class FlagWave : MonoBehaviour
    {
        [SerializeField] private float degrees = 12f, speed = 4f;
        private Quaternion home;
        private void Awake() => home = transform.localRotation;
        private void Update() => transform.localRotation = home * Quaternion.Euler(0f, Mathf.Sin(Time.time * speed) * degrees, 0f);
    }
}
