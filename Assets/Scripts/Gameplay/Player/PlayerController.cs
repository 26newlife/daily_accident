using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 1인칭 이동/시점. CharacterController 기반: WASD(이동), Shift(달리기), Ctrl(웅크리기), 마우스·오른쪽 스틱(시점).
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
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float sprintSpeed = 5.2f;
        [SerializeField] private float crouchSpeed = 1.4f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float gravity = -20f;

        [Header("자세")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField, Tooltip("캡슐 꼭대기에서 눈까지의 거리")] private float eyeBelowTop = 0.12f;
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

        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public Transform CameraRoot => cameraRoot;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            currentHeight = standHeight;
            ApplyHeight(currentHeight);
            LoadSettings();
        }

        private void OnEnable() => GameSettings.OnChanged += LoadSettings;
        private void OnDisable() => GameSettings.OnChanged -= LoadSettings;

        private void LoadSettings()
        {
            mouseSensitivity = GameSettings.MouseSensitivity;
            gamepadSensitivity = GameSettings.GamepadSensitivity;
            invertY = GameSettings.InvertY;
        }

        private void Update()
        {
            UpdateCursor();
            if (GameFlow.GameplayBlocked) return;

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
            bool wantCrouch = input.CrouchHeld;
            if (!wantCrouch && IsCrouching && !CanStandUp()) wantCrouch = true; // 머리 위가 막혀 있으면 계속 웅크림
            IsCrouching = wantCrouch;

            float target = IsCrouching ? crouchHeight : standHeight;
            currentHeight = Mathf.MoveTowards(currentHeight, target, postureSpeed * Time.deltaTime);
            ApplyHeight(currentHeight);
        }

        private void ApplyHeight(float height)
        {
            if (controller == null) return;
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraRoot != null)
            {
                Vector3 p = cameraRoot.localPosition;
                p.y = height - eyeBelowTop;
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

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
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
            float baseY = currentHeight - eyeBelowTop;
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
