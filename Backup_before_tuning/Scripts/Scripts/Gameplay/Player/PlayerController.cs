using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 1?몄묶 ?대룞/?쒖젏. CharacterController 湲곕컲: WASD(?대룞), Shift(?щ━湲?, Ctrl(?낇겕由ш린), Space(?먰봽), 留덉슦?ㅒ룹삤瑜몄そ ?ㅽ떛(?쒖젏).
    /// 媛먮룄 / Y異?諛섏쟾? GameSettings 媛믪쓣 ?쎄퀬 OnChanged ??諛섏쓳?쒕떎.
    /// ?낇겕由щ㈃ 罹≪뒓 ?믪씠? 移대찓???믪씠媛 遺?쒕읇寃???븘吏꾨떎 (癒몃━ ?꾧? 留됲? ?덉쑝硫??쇱뼱?쒖? ?딅뒗??.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("李몄“")]
        [SerializeField] private GameInput input;
        [SerializeField] private Transform cameraRoot;      // ?쇱튂(?곹븯) ?뚯쟾 + ?덈넂??

        [Header("?대룞")]
        [SerializeField] private float walkSpeed = 1.8f;
        [SerializeField] private float sprintSpeed = 3.8f;
        [SerializeField] private float crouchSpeed = 1.0f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float gravity = -20f;

        [Header("?먰봽")]
        [SerializeField, Tooltip("?먰봽 ?믪씠(m). 0.6~0.8 沅뚯옣")] private float jumpHeight = 0.6f;
        [SerializeField, Tooltip("諛붾떏?먯꽌 ?⑥뼱吏??ㅼ뿉???먰봽媛 ?덉슜?섎뒗 ?쒓컙(珥?")] private float coyoteTime = 0.1f;
        [SerializeField, Tooltip("李⑹? 吏곸쟾???꾨Ⅸ ?먰봽 ?낅젰??湲곗뼲?섎뒗 ?쒓컙(珥?")] private float jumpBufferTime = 0.1f;
        [SerializeField, Tooltip("???띾룄(m/s) ?댁긽?쇰줈 ?⑥뼱??李⑹??섎㈃ ?붾뱾由??뚮━")] private float landMinSpeed = 3.5f;
        [SerializeField] private float landShakeAmplitude = 0.02f;
        [SerializeField] private float landShakeDuration = 0.15f;
        [SerializeField, Tooltip("李⑹? 諛쒖냼由??щ’ (?대┰? ?섏쨷??")] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] landClips;

        [Header("?먯꽭")]
        [SerializeField] private float standHeight = 1.75f;
        [SerializeField] private float crouchHeight = 1.05f;
        [SerializeField, Tooltip("罹≪뒓 瑗??湲곗뿉???덇퉴吏??嫄곕━")] private float eyeBelowTop = 0.15f;
        [SerializeField] private float postureSpeed = 8f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("?쒖젏")]
        [SerializeField, Tooltip("留덉슦??1 count ??媛곷룄 횞 媛먮룄")] private float mouseDegreesPerCount = 0.05f;
        [SerializeField, Tooltip("?ㅽ떛 理쒕? ?낅젰 ??珥덈떦 媛곷룄 횞 媛먮룄")] private float gamepadDegreesPerSecond = 45f;
        [SerializeField] private float maxPitch = 85f;

        [Header("?댁??ъ? (媛??以묐룆 ??")]
        [SerializeField] private float dizzyRollDegrees = 4f;
        [SerializeField] private float dizzyPitchDegrees = 2f;
        [SerializeField] private float dizzyFrequency = 0.7f;

        [Header("移대찓???붾뱾由?(湲곗묠 ??")]
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
        /// <summary>true ?대㈃ ?대룞/?쒖젏/?먯꽭 ?낅젰??臾댁떆?쒕떎 (?꾩뼱 ?ㅼ퐫????移대찓?쇰? ?ㅻⅨ 怨녹씠 ?쒖뼱????. ?명씉湲걔룹떆媛????ㅻⅨ ?쒖뒪?쒖? 怨꾩냽 ?숈옉?쒕떎.</summary>
        public bool ControlSuspended { get; set; }
        /// <summary>?먰봽媛 ?ㅽ뻾???쒓컙 (?명씉湲?泥대젰 ?뚮え ?깆뿉??援щ룆)</summary>
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

        /// <summary>?먰봽 ?낅젰. ?쇱떆?뺤?/醫낅즺/?앹뾽(而ㅼ꽌 ?댁젣) 以묒뿉??臾댁떆, ?낇겕由??곹깭?먯꽌???쇱뼱?섍린留??쒕떎.</summary>
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

        // ????????? ?쒖젏 ?????????
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

        /// <summary>?댁??ъ? 媛뺣룄(0~1). 移대찓?쇨? 泥쒖쿇??湲곗슱硫??붾뱾由곕떎</summary>
        public void SetDizziness(float value) => dizziness = Mathf.Clamp01(value);

        private void ApplyCameraRotation()
        {
            if (cameraRoot == null) return;
            float t = Time.time * dizzyFrequency * Mathf.PI * 2f;
            float roll = Mathf.Sin(t) * dizzyRollDegrees * dizziness;
            float bob = Mathf.Sin(t * 0.63f + 1.3f) * dizzyPitchDegrees * dizziness;
            cameraRoot.localRotation = Quaternion.Euler(pitch + bob, 0f, roll);
        }

        // ????????? ?먯꽭 ?????????
        private void UpdatePosture()
        {
            if (!input.CrouchHeld) standRequested = false;     // ?낇겕由ш린 ?ㅻ? ?쇰㈃ ?ㅼ떆 ?낇겕由????덈떎
            bool wantCrouch = input.CrouchHeld && !standRequested;
            if (!wantCrouch && IsCrouching && !CanStandUp()) wantCrouch = true; // 癒몃━ ?꾧? 留됲? ?덉쑝硫?怨꾩냽 ?낇겕由?
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

        // ????????? ?대룞 ?????????
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

            // ?먰봽: 踰꾪띁 ?낅젰 + 肄붿슂???쒓컙 ?대궡 + ???덈뒗 ?곹깭
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

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // 泥쒖옣??癒몃━瑜?遺?ろ옒
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

        // ????????? ?붾뱾由??????????
        /// <summary>移대찓?쇰? ?좉퉸 ?붾뱺??(湲곗묠 ??</summary>
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

        /// <summary>?쒓컙?대룞 (?ㅽ룿/?곗텧??. CharacterController 瑜??좉퉸 爰쇱꽌 ?곸슜?쒕떎.</summary>
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

