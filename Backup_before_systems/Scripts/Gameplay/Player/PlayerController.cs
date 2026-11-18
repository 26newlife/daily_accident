using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 1인칭 이동/시점. CharacterController 기반: WASD(이동), Shift(달리기), Ctrl(웅크리기), Space(점프), 마우스·오른쪽 스틱(시점).
    /// 감도 / Y축 반전은 GameSettings 값을 읽고 OnChanged 에 반응한다.
    /// 웅크리면 캡슐 높이와 카메라 높이가 부드럽게 낮아진다 (머리 위가 막혀 있으면 일어서지 않는다).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private GameInput input;
        [SerializeField] private Transform cameraRoot;      // 피치(상하) 회전 + 눈높이

        [Header("이동")]
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private float sprintSpeed = 3.8f;
        [SerializeField] private float crouchSpeed = 1.0f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float gravity = -20f;

        [Header("점프")]
        [SerializeField, Tooltip("점프 높이(m). 0.6~0.8 권장")] private float jumpHeight = 0.6f;
        [SerializeField, Tooltip("바닥에서 떨어진 뒤에도 점프가 허용되는 시간(초)")] private float coyoteTime = 0.1f;
        [SerializeField, Tooltip("착지 직전에 누른 점프 입력을 기억하는 시간(초)")] private float jumpBufferTime = 0.1f;
        [SerializeField, Tooltip("이 속도(m/s) 이상으로 떨어져 착지하면 흔들림/소리")] private float landMinSpeed = 3.5f;
        [SerializeField] private float landShakeAmplitude = 0.02f;
        [SerializeField] private float landShakeDuration = 0.15f;
        [SerializeField, Tooltip("착지 발소리 슬롯 (클립은 나중에)")] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] landClips;

        [Header("자세")]
        [SerializeField] private float standHeight = 1.75f;
        [SerializeField] private float crouchHeight = 1.05f;
        [SerializeField, Tooltip("캡슐 꼭대기에서 눈까지의 거리")] private float eyeBelowTop = 0.15f;
        [SerializeField, Tooltip("카메라(눈높이) 로컬 Y 미세 조정(m). 음수 = 낮춤. 서 있을 때 이 값만큼, 웅크릴 때는 키 비율(crouchHeight/standHeight)만큼 같이 보정된다. CharacterController/상호작용 거리/점프에는 영향 없음")] private float cameraHeightOffset = -0.06f;
        [SerializeField] private float postureSpeed = 8f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("시점")]
        [SerializeField, Tooltip("마우스 1 count 당 각도 × 감도")] private float mouseDegreesPerCount = 0.05f;
        [SerializeField, Tooltip("스틱 최대 입력 시 초당 각도 × 감도")] private float gamepadDegreesPerSecond = 45f;
        [SerializeField] private float maxPitch = 85f;

        [Header("어지러움 (가스 중독 등)")]
        [SerializeField] private float dizzyRollDegrees = 4f;
        [SerializeField] private float dizzyPitchDegrees = 2f;
        [SerializeField] private float dizzyFrequency = 0.7f;

        [Header("카메라 흔들림 (기침 등)")]
        [SerializeField] private float shakeFrequency = 28f;

        private CharacterController controller;
        private float yaw, pitch, verticalVelocity;
        private Vector3 planarVelocity;
        private float currentHeight;
        private float mouseSensitivity, gamepadSensitivity;
        private bool invertY;
        private float shakeTime, shakeDuration, shakeAmplitude, dizziness;
        private float coyoteTimer, jumpBufferTimer, airFallSpeed;
        private bool grounded, standRequested;

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public Transform CameraRoot => cameraRoot;
        public bool IsGrounded => grounded;
        /// <summary>true 이면 이동/시점/자세 입력을 무시한다 (문구멍 등 다른 곳이 카메라를 제어할 때). 호흡기·시간 등 다른 시스템은 계속 동작한다.</summary>
        public bool ControlSuspended { get; set; }
        /// <summary>점프가 실행된 순간 (호흡기 체력 소모 등에서 구독)</summary>
        public event Action Jumped;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            currentHeight = standHeight;
            ApplyHeight(currentHeight);
            LoadSettings();
        }

        private void OnEnable()
        {
            GameSettings.OnChanged += LoadSettings;
            if (input != null) input.JumpPressed += OnJumpPressed;
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= LoadSettings;
            if (input != null) input.JumpPressed -= OnJumpPressed;
        }

        /// <summary>점프 입력. 일시정지/종료/팝업(커서 해제) 중에는 무시, 웅크린 상태에서는 일어나기만 한다.</summary>
        private void OnJumpPressed()
        {
            if (GameFlow.GameplayBlocked || GameFlow.CursorFree || ControlSuspended) return;
            if (IsCrouching) { standRequested = true; return; }
            jumpBufferTimer = jumpBufferTime;
        }

        private void LoadSettings()
        {
            mouseSensitivity = GameSettings.MouseSensitivity;
            gamepadSensitivity = GameSettings.GamepadSensitivity;
            invertY = GameSettings.InvertY;
        }

        private void Update()
        {
            UpdateCursor();
            if (GameFlow.GameplayBlocked || ControlSuspended) return;

            if (!GameFlow.CursorFree) UpdateLook();
            ApplyCameraRotation();
            UpdatePosture();
            UpdateMovement();
            UpdateShake();
        }

        private void UpdateCursor()
        {
            bool free = GameFlow.CursorFree;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        // ───────── 시점 ─────────
        private void UpdateLook()
        {
            Vector2 raw = input.Look;
            Vector2 delta = input.LookIsGamepad
                ? raw * (gamepadSensitivity * gamepadDegreesPerSecond * Time.deltaTime)
                : raw * (mouseSensitivity * mouseDegreesPerCount);

            yaw += delta.x;
            pitch += invertY ? delta.y : -delta.y;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>어지러움 강도(0~1). 카메라가 천천히 기울며 흔들린다</summary>
        public void SetDizziness(float value) => dizziness = Mathf.Clamp01(value);

        private void ApplyCameraRotation()
        {
            if (cameraRoot == null) return;
            float t = Time.time * dizzyFrequency * Mathf.PI * 2f;
            float roll = Mathf.Sin(t) * dizzyRollDegrees * dizziness;
            float bob = Mathf.Sin(t * 0.63f + 1.3f) * dizzyPitchDegrees * dizziness;
            cameraRoot.localRotation = Quaternion.Euler(pitch + bob, 0f, roll);
        }

        // ───────── 자세 ─────────
        private void UpdatePosture()
        {
            if (!input.CrouchHeld) standRequested = false;     // 웅크리기 키를 떼면 다시 웅크릴 수 있다
            bool wantCrouch = input.CrouchHeld && !standRequested;
            if (!wantCrouch && IsCrouching && !CanStandUp()) wantCrouch = true; // 머리 위가 막혀 있으면 계속 웅크림
            IsCrouching = wantCrouch;

            float target = IsCrouching ? crouchHeight : standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, target, postureSpeed * Time.deltaTime);
            ApplyHeight(currentHeight);
        }

        /// <summary>현재 캡슐 높이에서의 카메라 로컬 Y. 보정값은 키 비율에 맞춰 함께 줄어 서기/웅크리기 전환이 어색하지 않다</summary>
        private float EyeY(float height) => height - eyeBelowTop + cameraHeightOffset * (height / Mathf.Max(0.01f, standHeight));

        private void ApplyHeight(float height)
        {
            if (controller == null) return;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraRoot != null)
            {
                Vector3 p = cameraRoot.localPosition;
                p.y = EyeY(height);
                cameraRoot.localPosition = p;
            }
        }

        private bool CanStandUp()
        {
            float radius = controller.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (currentHeight - radius);
            float distance = standHeight - currentHeight + 0.02f;
            return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        // ───────── 이동 ─────────
        private void UpdateMovement()
        {
            Vector2 move = input.Move;
            IsSprinting = input.SprintHeld && !IsCrouching && move.y > 0.1f;

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 wish = (transform.right * move.x + transform.forward * move.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            planarVelocity = Vector3.Lerp(planarVelocity, wish * speed, 1f - Mathf.Exp(-acceleration * Time.deltaTime));

            float dt = Time.deltaTime;
            coyoteTimer = grounded ? coyoteTime : coyoteTimer - dt;
            jumpBufferTimer -= dt;

            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

            // 점프: 버퍼 입력 + 코요테 시간 이내 + 서 있는 상태
            if (jumpBufferTimer > 0f && coyoteTimer > 0f && !IsCrouching)
            {
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumpBufferTimer = 0f;
                coyoteTimer = 0f;
                grounded = false;
                Jumped?.Invoke();
            }
            verticalVelocity += gravity * dt;
            if (!grounded) airFallSpeed = Mathf.Min(airFallSpeed, verticalVelocity);

            Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
            CollisionFlags flags = controller.Move(velocity * dt);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // 천장에 머리를 부딪힘
            bool wasGrounded = grounded;
            grounded = (flags & CollisionFlags.Below) != 0;
            if (grounded && !wasGrounded) OnLanded();
            if (grounded) airFallSpeed = 0f;
        }

        private void OnLanded()
        {
            if (-airFallSpeed < landMinSpeed) return;
            Shake(landShakeAmplitude, landShakeDuration);
            if (footstepSource != null && landClips != null && landClips.Length > 0)
                footstepSource.PlayOneShot(landClips[UnityEngine.Random.Range(0, landClips.Length)]);
        }

        // ───────── 흔들림 ─────────
        /// <summary>카메라를 잠깐 흔든다 (기침 등)</summary>
        public void Shake(float amplitude, float duration)
        {
            shakeAmplitude = amplitude;
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeTime = shakeDuration;
        }

        private void UpdateShake()
        {
            if (cameraRoot == null) return;
            Vector3 p = cameraRoot.localPosition;
            float baseY = EyeY(currentHeight);
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float k = Mathf.Clamp01(shakeTime / shakeDuration);
                p.y = baseY + Mathf.Sin(Time.time * shakeFrequency) * shakeAmplitude * k;
                p.x = Mathf.Cos(Time.time * shakeFrequency * 0.7f) * shakeAmplitude * 0.5f * k;
            }
            else { p.x = 0f; p.y = baseY; }
            cameraRoot.localPosition = p;
        }

        /// <summary>순간이동 (스폰/연출용). CharacterController 를 잠깐 꺼서 적용한다.</summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            controller.enabled = false;
            transform.position = position;
            yaw = yawDegrees;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            controller.enabled = true;
        }
    }
}
