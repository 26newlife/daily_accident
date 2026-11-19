using System;
using GameCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 도어 스코프 시점 제어. 카메라를 렌즈 위치로 0.4초 동안 옮기고, 시점은 ±15° 로 제한하며,
    /// 어안렌즈(Lens Distortion) + 비네트(임시 Volume) + 원형 마스크 UI 로 문구멍 느낌을 낸다.
    /// 복도 상황은 실제 복도 지오메트리(조명 LightGroup, FireSource 불빛/연기 파티클)를 그대로 보여 주고,
    /// 연기 농도는 HazardZone("zone_corridor")의 값으로 안개를 만든다. 보는 동안에도 게임 시간과 호흡기 게이지는 정상적으로 흐른다.
    /// 나오기: E / ESC / 우클릭 / 게임패드 B.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PeepholeCameraController : MonoBehaviour
    {
        [Serializable]
        public struct CorridorNpc
        {
            [Tooltip("복도에 서 있는 NPC 실루엣 (평소엔 꺼 둔다)")] public GameObject silhouette;
            [Tooltip("이 ScenarioFlags 키가 true 이면 보인다 (Act 3 에서 설정)")] public string flagKey;
        }

        [Header("참조")]
        [SerializeField] private PlayerController player;
        [SerializeField] private GameInput input;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Canvas canvas;

        [Header("시점")]
        [SerializeField, Min(0.05f)] private float enterTime = 0.4f;
        [SerializeField, Min(0.05f)] private float exitTime = 0.3f;
        [SerializeField, Range(1f, 45f)] private float lookLimitDegrees = 15f;
        [SerializeField, Range(40f, 140f)] private float peepholeFov = 105f;
        [SerializeField, Tooltip("들어온 직후 이 시간(초)은 나가기 입력을 무시 (들어올 때 누른 E 가 바로 나가게 하는 것 방지)")] private float exitGuardSeconds = 0.35f;
        [SerializeField] private float mouseDegreesPerCount = 0.04f;
        [SerializeField] private float gamepadDegreesPerSecond = 30f;

        [Header("렌즈 효과 (URP Volume, 후처리가 꺼져 있어도 마스크 UI 는 동작)")]
        [SerializeField, Range(-1f, 0f)] private float lensDistortion = -0.55f;
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.5f;
        [SerializeField, Range(0.2f, 0.5f)] private float maskRadiusOfHeight = 0.46f;
        [SerializeField, Range(0.01f, 0.2f)] private float maskSoftness = 0.05f;

        [Header("복도 상황")]
        [SerializeField] private string corridorZoneId = "zone_corridor";
        [SerializeField, Tooltip("연기 1.0 일 때의 안개 밀도 (Exponential Squared)")] private float maxFogDensity = 0.45f;
        [SerializeField] private Color smokeColor = new Color(0.16f, 0.15f, 0.14f);
        [SerializeField] private string corridorFireId = "fire_corridor";
        [SerializeField, Tooltip("복도 불이 붙어 있을 때 최소 안개 밀도 비율 (연기가 안 보여도 뿌옇게)")] private float fireHazeRatio = 0.25f;
        [SerializeField] private CorridorNpc[] corridorNpcs;

        public static bool IsActive { get; private set; }
        public static float LastExitTime { get; private set; } = -10f;

        /// <summary>새 판 시작 시 static 상태 초기화 (GameRunContext)</summary>
        public static void ResetRunState() { IsActive = false; LastExitTime = -10f; }

        private enum Phase { Idle, Entering, Looking, Exiting }

        private Phase phase = Phase.Idle;
        private DoorPeephole current;
        private Transform cameraRoot;
        private Vector3 savedLocalPos; private Quaternion savedLocalRot; private float savedFov; private bool savedPost;
        private Vector3 startPos, targetPos; private Quaternion startRot, targetRot;
        private float phaseTime, enteredAt;
        private float yawOffset, pitchOffset;
        private bool exitRequested;

        private Volume volume; private LensDistortion lens; private Vignette vignette;
        private CanvasGroup maskGroup; private RawImage maskImage; private Texture2D maskTexture; private float maskAspect;
        private FogState savedFog;

        private struct FogState { public bool on; public FogMode mode; public float density; public Color color; }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            BuildVolume();
            BuildMask();
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.InteractPressed += RequestExit; input.BackPressed += RequestExit; input.PausePressed += RequestExit;
        }

        private void OnDisable()
        {
            if (input != null) { input.InteractPressed -= RequestExit; input.BackPressed -= RequestExit; input.PausePressed -= RequestExit; }
            if (IsActive) ForceReset();
        }

        private void OnDestroy()
        {
            if (maskTexture != null) Destroy(maskTexture);
            if (volume != null && volume.sharedProfile != null) Destroy(volume.sharedProfile);
        }

        // ═════════════════════════ 진입 / 이탈 ═════════════════════════
        public void Enter(DoorPeephole peephole)
        {
            if (phase != Phase.Idle || peephole == null || viewCamera == null || player == null) return;
            cameraRoot = player.CameraRoot != null ? player.CameraRoot : viewCamera.transform;
            current = peephole;

            savedLocalPos = cameraRoot.localPosition; savedLocalRot = cameraRoot.localRotation;
            savedFov = viewCamera.fieldOfView;
            var urp = viewCamera.GetUniversalAdditionalCameraData();
            savedPost = urp.renderPostProcessing; urp.renderPostProcessing = true;
            savedFog = new FogState { on = RenderSettings.fog, mode = RenderSettings.fogMode, density = RenderSettings.fogDensity, color = RenderSettings.fogColor };

            startPos = cameraRoot.position; startRot = cameraRoot.rotation;
            targetPos = peephole.CameraPosition;
            targetRot = Quaternion.LookRotation(peephole.OutDirection, Vector3.up);
            yawOffset = pitchOffset = 0f;
            phase = Phase.Entering; phaseTime = 0f; enteredAt = Time.unscaledTime; exitRequested = false;
            IsActive = true;
            player.ControlSuspended = true;
            RebuildMaskIfNeeded();
        }

        private void RequestExit()
        {
            if (!IsActive || Time.unscaledTime - enteredAt < exitGuardSeconds) return;
            exitRequested = true;
        }

        private void BeginExit()
        {
            if (phase == Phase.Exiting || phase == Phase.Idle) return;
            startPos = cameraRoot.position; startRot = cameraRoot.rotation;
            phase = Phase.Exiting; phaseTime = 0f;
        }

        private void FinishExit()
        {
            cameraRoot.localPosition = savedLocalPos; cameraRoot.localRotation = savedLocalRot;
            ForceReset();
        }

        private void ForceReset()
        {
            if (viewCamera != null)
            {
                viewCamera.fieldOfView = savedFov > 1f ? savedFov : viewCamera.fieldOfView;
                viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = savedPost;
            }
            if (player != null) player.ControlSuspended = false;
            SetEffects(0f);
            SetCorridorNpcs(false);
            phase = Phase.Idle; current = null; IsActive = false; LastExitTime = Time.unscaledTime;
            // SmokeAtmosphere 가 다음 프레임에 안개를 다시 관리한다
        }

        // ═════════════════════════ 갱신 ═════════════════════════
        private void Update()
        {
            if (phase == Phase.Idle) return;
            if (GameFlow.IsEnded || GameFlow.IsPaused || GameFlow.IsPhoneOpen || current == null) { if (phase != Phase.Exiting) BeginExit(); }
            if (exitRequested && phase != Phase.Exiting) { exitRequested = false; BeginExit(); }

            float dt = Time.unscaledDeltaTime;
            switch (phase)
            {
                case Phase.Entering:
                {
                    phaseTime += dt;
                    float k = Smooth(phaseTime / enterTime);
                    cameraRoot.position = Vector3.Lerp(startPos, targetPos, k);
                    cameraRoot.rotation = Quaternion.Slerp(startRot, targetRot, k);
                    viewCamera.fieldOfView = Mathf.Lerp(savedFov, peepholeFov, k);
                    SetEffects(k);
                    if (phaseTime >= enterTime) phase = Phase.Looking;
                    break;
                }
                case Phase.Looking:
                    UpdateLook(dt);
                    cameraRoot.position = targetPos;
                    cameraRoot.rotation = targetRot * Quaternion.Euler(pitchOffset, yawOffset, 0f);
                    break;
                case Phase.Exiting:
                {
                    phaseTime += dt;
                    float k = Smooth(phaseTime / exitTime);
                    Vector3 homePos = player.transform.TransformPoint(savedLocalPos);
                    cameraRoot.position = Vector3.Lerp(startPos, homePos, k);
                    cameraRoot.rotation = Quaternion.Slerp(startRot, player.transform.rotation * savedLocalRot, k);
                    viewCamera.fieldOfView = Mathf.Lerp(peepholeFov, savedFov, k);
                    SetEffects(1f - k);
                    if (phaseTime >= exitTime) FinishExit();
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsActive) return;
            ApplyCorridorFog();
            SetCorridorNpcs(true);
        }

        private void UpdateLook(float dt)
        {
            Vector2 raw = input.Look;
            Vector2 delta = input.LookIsGamepad
                ? raw * (GameSettings.GamepadSensitivity * gamepadDegreesPerSecond * dt)
                : raw * (GameSettings.MouseSensitivity * mouseDegreesPerCount);
            yawOffset = Mathf.Clamp(yawOffset + delta.x, -lookLimitDegrees, lookLimitDegrees);
            pitchOffset = Mathf.Clamp(pitchOffset + (GameSettings.InvertY ? delta.y : -delta.y), -lookLimitDegrees, lookLimitDegrees);
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ═════════════════════════ 복도 상황 ═════════════════════════
        private void ApplyCorridorFog()
        {
            float smoke = 0f;
            HazardManager hz = HazardManager.Instance;
            HazardZone zone = hz != null ? hz.FindZone(corridorZoneId) : null;
            if (zone != null) smoke = zone.SmokeLevel;

            // 복도 불이 켜져 있으면 연기 값이 낮아도 어느 정도 뿌옇게 (불빛은 실제 FireSource 라이트가 낸다)
            foreach (FireSource f in FireSource.All)
                if (f != null && f.Id == corridorFireId && f.IsLit) smoke = Mathf.Max(smoke, fireHazeRatio * f.Intensity);

            bool on = smoke > 0.01f;
            RenderSettings.fog = on;
            if (on)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = smokeColor;
                RenderSettings.fogDensity = smoke * maxFogDensity;
            }
        }

        private void SetCorridorNpcs(bool allowShow)
        {
            if (corridorNpcs == null) return;
            foreach (CorridorNpc n in corridorNpcs)
                if (n.silhouette != null) n.silhouette.SetActive(allowShow && !string.IsNullOrEmpty(n.flagKey) && ScenarioFlags.GetBool(n.flagKey));
        }

        // ═════════════════════════ 렌즈 효과 ═════════════════════════
        private void BuildVolume()
        {
            var go = new GameObject("PeepholeVolume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100f; volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            lens = profile.Add<LensDistortion>(true);
            lens.intensity.Override(lensDistortion); lens.scale.Override(0.9f);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(vignetteIntensity); vignette.smoothness.Override(1f); vignette.rounded.Override(true);
            volume.sharedProfile = profile;
        }

        private void BuildMask()
        {
            if (canvas == null) return;
            var go = new GameObject("PeepholeMask", typeof(RectTransform), typeof(CanvasGroup), typeof(RawImage));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetSiblingIndex(Mathf.Min(1, canvas.transform.childCount - 1));   // HUD 보다 아래, 화면 효과보다 위
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            maskGroup = go.GetComponent<CanvasGroup>(); maskGroup.alpha = 0f; maskGroup.blocksRaycasts = false; maskGroup.interactable = false;
            maskImage = go.GetComponent<RawImage>(); maskImage.raycastTarget = false;
        }

        private void RebuildMaskIfNeeded()
        {
            if (maskImage == null) return;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            if (maskTexture != null && Mathf.Abs(aspect - maskAspect) < 0.01f) return;
            maskAspect = aspect;
            const int h = 288;
            int w = Mathf.Max(16, Mathf.RoundToInt(h * aspect));
            if (maskTexture != null) Destroy(maskTexture);
            maskTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            float radius = maskRadiusOfHeight * h, soft = maskSoftness * h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x - w * 0.5f) * (x - w * 0.5f) + (y - h * 0.5f) * (y - h * 0.5f));
                    float a = Mathf.Clamp01((d - (radius - soft)) / (soft * 2f));      // 원 안쪽 0 → 바깥 1
                    px[y * w + x] = new Color(0.01f, 0.01f, 0.012f, a);
                }
            maskTexture.SetPixels32(px); maskTexture.Apply();
            maskImage.texture = maskTexture;
        }

        private void SetEffects(float weight)
        {
            if (volume != null) volume.weight = weight;
            if (maskGroup != null) maskGroup.alpha = weight;
        }
    }
}
