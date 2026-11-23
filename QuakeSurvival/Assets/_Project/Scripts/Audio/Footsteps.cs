using QuakeSurvival.Player;
using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>걸은 거리만큼 발소리를 낸다. 숙이면 보폭이 짧고 소리가 작다.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Footsteps : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private AudioClip step;
        [SerializeField] private float strideLength = 0.7f;
        [SerializeField] private float volume = 0.35f;

        private CharacterController controller;
        private Vector3 lastPosition;
        private float travelled;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (player == null || step == null)
            {
                Debug.LogError("[Footsteps] player 또는 step이 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            if (!controller.isGrounded) return;

            delta.y = 0f;
            if (delta.sqrMagnitude > 1f)
            {
                travelled = 0f;
                return;
            }
            travelled += delta.magnitude;
            float stride = player.IsCrouching ? strideLength * 0.6f : (player.IsRunning ? strideLength * 1.4f : strideLength);
            if (travelled < stride) return;

            travelled = 0f;
            float v = player.IsCrouching ? volume * 0.4f : (player.IsRunning ? volume * 1.3f : volume);
            SoundPlayer.Play2D(step, AudioCategory.Sfx, v, 0.12f);
        }
    }
}
