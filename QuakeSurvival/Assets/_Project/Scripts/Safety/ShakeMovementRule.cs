using QuakeSurvival.Player;
using QuakeSurvival.Quake;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 땅이 흔들리는 동안(전진, 본진, 여진) 선 채로 돌아다니면 넘어져 사망한다.
    /// - 흔들림이 시작된 뒤 graceSeconds 동안은 가까운 탁자 밑으로 가도록 움직여도 된다.
    /// - 그 뒤에는 서서(걷기, 달리기, 점프) 움직인 거리가 allowedDistance를 넘으면 사망.
    /// - allowCrouchMove가 켜져 있으면 숙인 채 기어가는 것은 허용한다(행동요령: 자세를 낮추고 몸 보호).
    /// 대피지역 안에서는 OutcomeEvaluator가 사망 대신 감점으로 바꾼다.
    /// </summary>
    public sealed class ShakeMovementRule : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private float graceSeconds = 2.5f;
        [SerializeField] private float allowedDistance = 0.8f;
        [SerializeField] private float minIntensity = 0.1f;
        [SerializeField] private bool allowCrouchMove = true;

        private Vector3 lastPosition;
        private float movedWhileShaking;

        private void Awake()
        {
            if (director == null || player == null || evaluator == null)
            {
                Debug.LogError("[ShakeMovementRule] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhase;
            if (player != null) lastPosition = player.transform.position;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
        }

        // 흔들림 단계마다 이동 거리를 새로 센다
        private void OnPhase(QuakePhase phase) => movedWhileShaking = 0f;

        private void Update()
        {
            Vector3 position = player.transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;

            if (!IsShaking()) return;
            if (director.PhaseElapsed < graceSeconds) return;
            if (allowCrouchMove && player.IsCrouching) return; // 숙이면 점프가 막혀 있어 계단에서 잠깐 뜨는 것도 허용

            movedWhileShaking += new Vector2(delta.x, delta.z).magnitude;
            if (movedWhileShaking > allowedDistance)
            {
                movedWhileShaking = 0f;
                evaluator.Kill(DeathCause.FellWhileShaking);
            }
        }

        private bool IsShaking()
        {
            QuakePhase phase = director.Phase;
            bool shakingPhase = phase == QuakePhase.Foreshock || phase == QuakePhase.MainShock || phase == QuakePhase.Aftershock;
            return shakingPhase && director.Intensity >= minIntensity;
        }
    }
}
