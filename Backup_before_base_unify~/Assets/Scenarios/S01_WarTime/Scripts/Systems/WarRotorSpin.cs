using UnityEngine;

namespace GameGameplay
{
    /// <summary>헬기 로터 회전 (순수 연출)</summary>
    public class WarRotorSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("초당 회전 각도")] private float degreesPerSecond = 1400f;

        private void Update() { transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self); }
    }
}
