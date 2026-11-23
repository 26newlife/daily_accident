using UnityEngine;
using UnityEngine.InputSystem;

namespace QuakeSurvival.Player
{
    /// <summary>
    /// 1인칭 이동: WASD 이동, 마우스 시점, Shift 달리기(체력 제한), Space 점프(서 있고 땅에 닿아 있을 때),
    /// Ctrl 숙이기(한 번 누르면 숙이고, 다시 누르면 일어선다).
    /// CharacterController 기반이라 물리 Rigidbody 없이 벽 충돌을 처리한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private Stamina stamina = new Stamina();
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float standHeight = 1.7f;
        // 탁자 아래(0.72m)에 들어가려면 crouchHeight + skinWidth가 그보다 작아야 한다
        [SerializeField] private float crouchHeight = 0.55f;
        [SerializeField] private float heightLerpSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;
        [Tooltip("점프 높이(m). 갈라진 땅 틈을 뛰어넘을 수 있을 만큼.")]
        [SerializeField] private float jumpHeight = 0.9f;
        [SerializeField] private LayerMask ceilingMask = Physics.DefaultRaycastLayers;

        private const float MaxPitch = 85f;
        private const float GroundStick = -2f;
        private const float CeilingSkin = 0.02f;
        private const float HeightSnap = 0.005f;

        private readonly Collider[] ceilingHits = new Collider[8];
        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;
        private float speedMultiplier = 1f;
        private bool inputEnabled = true;
        private bool crouchToggled;
        private readonly System.Collections.Generic.Dictionary<string, float> speedModifiers =
            new System.Collections.Generic.Dictionary<string, float>();

        public bool IsCrouching { get; private set; }

        /// <summary>발이 땅에 닿아 있는지(점프 중이거나 떨어지는 중이면 false).</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        /// <summary>Shift로 달리는 중(앞으로 움직이고, 숙이지 않았고, 체력이 남아 있을 때).</summary>
        public bool IsRunning { get; private set; }

        /// <summary>달리기 체력 비율(0~1). HUD 막대에 쓴다.</summary>
        public float StaminaRatio => stamina.Ratio;

        public bool StaminaExhausted => stamina.Exhausted;

        /// <summary>false면 이동/시점 입력을 막고 커서를 푼다(메뉴, 연출, 사망 화면).</summary>
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                ApplyCursorLock(value && isActiveAndEnabled);
            }
        }

        /// <summary>
        /// 이동 속도 배율을 이유별로 설정한다(예: "injury" 0.6, "neighbor" 0.75). 모든 배율을 곱해서 쓴다.
        /// 1을 넣으면 해당 이유를 없앤다.
        /// </summary>
        public void SetSpeedModifier(string reason, float multiplier)
        {
            float value = Mathf.Clamp01(multiplier);
            if (Mathf.Approximately(value, 1f)) speedModifiers.Remove(reason);
            else speedModifiers[reason] = value;

            float product = 1f;
            foreach (float m in speedModifiers.Values) product *= m;
            speedMultiplier = product;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraRoot == null)
            {
                Debug.LogError("[FirstPersonController] cameraRoot가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable() => ApplyCursorLock(inputEnabled);

        private void OnDisable() => ApplyCursorLock(false);

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool canRead = inputEnabled && keyboard != null && mouse != null;

            if (canRead && keyboard.ctrlKey.wasPressedThisFrame) crouchToggled = !crouchToggled;
            if (canRead) IsCrouching = ResolveCrouch(crouchToggled);
            UpdateHeight();

            if (!canRead)
            {
                IsRunning = false;
                stamina.Tick(false, Time.deltaTime);
                MoveWithVelocity(Vector3.zero);
                return;
            }

            Look(mouse.delta.ReadValue());
            Vector2 move = ReadMoveInput(keyboard);
            IsRunning = keyboard.shiftKey.isPressed && move.y > 0.1f && !IsCrouching && stamina.CanRun;
            stamina.Tick(IsRunning, Time.deltaTime);
            if (keyboard.spaceKey.wasPressedThisFrame) TryJump();
            MoveWithVelocity(HorizontalVelocity(move));
        }

        private static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// 일어서기를 골라도 머리 위가 막혀 있으면(탁자 아래 등) 계속 숙인 상태를 유지하고,
        /// 막힌 곳을 벗어나면 자동으로 일어선다.
        /// </summary>
        private bool ResolveCrouch(bool wantsCrouch)
        {
            if (wantsCrouch) return true;
            if (!IsCrouching) return false;
            return IsCeilingBlocked();
        }

        /// <summary>
        /// 지금 키에서 선 키까지의 공간에 다른 물체가 있는지 검사한다.
        /// 플레이어 자신의 콜라이더는 항상 겹치므로 결과에서 제외해야 한다.
        /// </summary>
        private bool IsCeilingBlocked()
        {
            float radius = controller.radius - CeilingSkin;
            Vector3 bottom = transform.position + Vector3.up * (controller.height - radius);
            Vector3 top = transform.position + Vector3.up * (standHeight + controller.skinWidth - radius);
            if (top.y <= bottom.y) return false;

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, ceilingHits, ceilingMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (ceilingHits[i] != controller) return true;
            }
            return false;
        }

        private static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private void Look(Vector2 delta)
        {
            Vector2 scaled = delta * mouseSensitivity;
            transform.Rotate(Vector3.up, scaled.x);
            pitch = Mathf.Clamp(pitch - scaled.y, -MaxPitch, MaxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private Vector3 HorizontalVelocity(Vector2 input)
        {
            float baseSpeed = IsCrouching ? crouchSpeed : (IsRunning ? runSpeed : walkSpeed);
            float speed = baseSpeed * speedMultiplier;
            return (transform.right * input.x + transform.forward * input.y) * speed;
        }

        private void TryJump()
        {
            if (IsCrouching || !controller.isGrounded) return;
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
        }

        private void MoveWithVelocity(Vector3 horizontal)
        {
            verticalVelocity = NextVerticalVelocity();
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private float NextVerticalVelocity()
        {
            if (controller.isGrounded && verticalVelocity < 0f) return GroundStick;
            return verticalVelocity + gravity * Time.deltaTime;
        }

        private void UpdateHeight()
        {
            float target = IsCrouching ? crouchHeight : standHeight;
            float height = Mathf.Lerp(controller.height, target, heightLerpSpeed * Time.deltaTime);
            if (Mathf.Abs(height - target) < HeightSnap) height = target;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            Vector3 camPos = cameraRoot.localPosition;
            cameraRoot.localPosition = new Vector3(camPos.x, height - 0.1f, camPos.z);
        }
    }
}
