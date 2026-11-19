using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    public enum WindowState { Open = 0, Closed = 1, Broken = 2 }
    public enum DoorState { Open = 0, Closed = 1, Locked = 2, Unlocked = 3 }
    public enum StructureShakePreset { Light = 0, Medium = 1, Heavy = 2 }

    /// <summary>
    /// 집 에셋(1004호 + 베란다 + 10층 복도 + 엘리베이터 + 비상계단 입구 + 창밖 풍경)의 공개 API 퍼사드.
    /// 시나리오는 집 내부 오브젝트를 직접 건드리지 않고 이 API 로 조작한다. 집은 시나리오 내용을 모른다 (화재·폭발·가스·NPC 로직 없음).
    ///   조명 / 창문·문 / 공기·위험 상태(구역) / 연출(흔들림·이펙트·소리) / 앵커·스폰 / 상호작용 오브젝트 제어 / 환경 프리셋
    /// 모든 대상은 기존 id 로 찾으며(id 는 변경 금지), 없으면 경고 로그만 남긴다.
    /// </summary>
    public class HouseController : MonoBehaviour, IHouseEnvironment, ISpawnProvider
    {
        [Header("환경")]
        [SerializeField] private Light sun;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField, Range(0f, 0.3f)] private float blackoutAmbient = 0.03f;
        [SerializeField, Tooltip("구조물 흔들림 때 함께 떨리는 가구/소품 루트 (비우면 흔들림은 카메라만)")] private Transform[] shakeRoots;

        [Header("구조물 흔들림 프리셋 (카메라 세기 / 소품 진폭(m) / 지속(초))")]
        [SerializeField] private Vector3 shakeLight = new Vector3(0.04f, 0.004f, 1.5f);
        [SerializeField] private Vector3 shakeMedium = new Vector3(0.09f, 0.01f, 3f);
        [SerializeField] private Vector3 shakeHeavy = new Vector3(0.18f, 0.02f, 5f);

        public static HouseController Instance { get; private set; }

        private readonly Dictionary<string, ScenarioAnchor> anchors = new Dictionary<string, ScenarioAnchor>();
        private readonly Dictionary<string, SpawnPoint> spawns = new Dictionary<string, SpawnPoint>();
        private readonly Dictionary<string, bool> doorHot = new Dictionary<string, bool>();
        private readonly List<Transform> shakeTargets = new List<Transform>();
        private readonly List<Vector3> shakeOrigins = new List<Vector3>();
        private float shakeLeft, shakeAmp;
        private Color savedAmbient; private bool blackout;

        public IReadOnlyCollection<string> AnchorIds => anchors.Keys;
        public IReadOnlyCollection<string> SpawnIds => spawns.Keys;

        private void Awake()
        {
            Instance = this;
            foreach (ScenarioAnchor a in FindObjectsByType<ScenarioAnchor>(FindObjectsInactive.Include)) anchors[a.Id] = a;
            foreach (SpawnPoint s in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include)) spawns[s.Id] = s;
            if (sun == null) sun = RenderSettings.sun;
        }

        private void OnEnable() { HouseServices.Register(this, this); }
        private void OnDisable() { HouseServices.Clear(); }
        private void OnDestroy() { if (Instance == this) Instance = null; RestoreShake(); }

        // ═════════════════════ 조명 ═════════════════════
        public void SetLightGroup(string id, bool on)
        {
            LightGroup g = LightGroup.Find(id);
            if (g != null) g.SetOn(on); else Warn("조명 그룹", id);
        }

        /// <summary>정전: 모든 조명 소등 + 주변광을 어둡게 (해제하면 원래대로)</summary>
        public void SetBlackout(bool on)
        {
            if (on == blackout) return;
            blackout = on;
            LightGroup.SetAllOn(!on);
            if (on) { savedAmbient = RenderSettings.ambientLight; RenderSettings.ambientLight = new Color(blackoutAmbient, blackoutAmbient, blackoutAmbient * 1.1f); }
            else RenderSettings.ambientLight = savedAmbient;
        }

        /// <summary>비상구 유도등 ("exit" 조명 그룹)</summary>
        public void SetEmergencyLights(bool on) => SetLightGroup("exit", on);

        // ═════════════════════ 창문 · 문 ═════════════════════
        public void SetWindowState(string id, WindowState state)
        {
            var t = Find<ToggleInteractable>(id);
            if (t == null) { Warn("창문", id); return; }
            if (state == WindowState.Open) { t.SetLocked(false); t.SetState(true); }
            else if (state == WindowState.Closed) t.SetState(false);
            else
            {
                Transform pane = t.transform.Find("Pane");
                Transform target = pane != null ? pane : t.transform;
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (Collider c in target.GetComponentsInChildren<Collider>()) c.enabled = false;
                t.Interactable = false;
            }
        }

        public void SetDoorState(string id, DoorState state)
        {
            var t = Find<ToggleInteractable>(id);
            if (t == null) { Warn("문", id); return; }
            switch (state)
            {
                case DoorState.Open: t.SetState(true); break;
                case DoorState.Closed: t.SetState(false); break;
                case DoorState.Locked: t.SetLocked(true); break;
                case DoorState.Unlocked: t.SetLocked(false); break;
            }
        }

        /// <summary>문 손잡이 온도 (뜨거움/정상). 상태를 보관하고 Custom 이벤트 "door_temperature:id"(State = 뜨거움)를 발행한다 — 해석은 시나리오가 한다</summary>
        public void SetDoorTemperature(string id, bool hot)
        {
            doorHot[id] = hot;
            EventBus.Raise(ScenarioEventType.Custom, "door_temperature:" + id, hot);
        }

        public bool IsDoorHot(string id) => doorHot.TryGetValue(id, out bool h) && h;

        // ═════════════════════ 공기 · 위험 상태 (구역) ═════════════════════
        public void SetZoneSmoke(string zoneId, float level) { HazardZone z = Zone(zoneId); if (z != null) z.SetSmoke(level); }
        public void SetZoneGas(string zoneId, float level) { HazardZone z = Zone(zoneId); if (z != null) z.SetGas(level); }
        public void SetZoneTemperature(string zoneId, float celsius) { HazardZone z = Zone(zoneId); if (z != null) z.SetValues(z.GasLevel, z.SmokeLevel, celsius); }
        /// <summary>침수 등 확장용 훅: 구역 수위(0~1)</summary>
        public void SetZoneWater(string zoneId, float level) { HazardZone z = Zone(zoneId); if (z != null) z.SetWater(level); }

        private HazardZone Zone(string zoneId)
        {
            HazardZone z = HazardManager.Instance != null ? HazardManager.Instance.FindZone(zoneId) : null;
            if (z == null) Warn("구역", zoneId);
            return z;
        }

        // ═════════════════════ 연출 ═════════════════════
        public void ShakeCamera(float intensity, float duration)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) p.Shake(intensity, duration);
        }

        /// <summary>지진 등 구조물 흔들림 (카메라 + 가구/소품 떨림). 프리셋 값은 Inspector 에서 조절</summary>
        public void TriggerStructureShake(StructureShakePreset preset)
        {
            Vector3 v = preset == StructureShakePreset.Light ? shakeLight : preset == StructureShakePreset.Medium ? shakeMedium : shakeHeavy;
            ShakeCamera(v.x, v.z);
            if (shakeTargets.Count == 0 && shakeRoots != null)
                foreach (Transform root in shakeRoots)
                    if (root != null) foreach (Transform c in root) { shakeTargets.Add(c); shakeOrigins.Add(c.localPosition); }
            shakeAmp = v.y; shakeLeft = v.z;
        }

        private void Update()
        {
            if (shakeLeft <= 0f) return;
            if (GameFlow.GameplayBlocked) return;
            shakeLeft -= Time.deltaTime;
            float k = Mathf.Clamp01(shakeLeft / 1.5f);
            for (int i = 0; i < shakeTargets.Count; i++)
                if (shakeTargets[i] != null) shakeTargets[i].localPosition = shakeOrigins[i] + Random.insideUnitSphere * shakeAmp * k;
            if (shakeLeft <= 0f) RestoreShake();
        }

        private void RestoreShake()
        {
            for (int i = 0; i < shakeTargets.Count; i++) if (shakeTargets[i] != null) shakeTargets[i].localPosition = shakeOrigins[i];
            shakeLeft = 0f;
        }

        public GameObject SpawnEffectAt(string anchorId, GameObject prefab)
        {
            ScenarioAnchor a = FindAnchor(anchorId);
            if (a == null || prefab == null) { if (a == null) Warn("앵커", anchorId); return null; }
            return Instantiate(prefab, a.transform.position, a.transform.rotation, a.transform);
        }

        public void PlaySoundAt(string anchorId, AudioClip clip, float volume = 1f)
        {
            ScenarioAnchor a = FindAnchor(anchorId);
            if (a == null) { Warn("앵커", anchorId); return; }
            if (clip != null) AudioSource.PlayClipAtPoint(clip, a.transform.position, volume);
        }

        // ═════════════════════ 앵커 · 스폰 ═════════════════════
        public ScenarioAnchor FindAnchor(string id) => anchors.TryGetValue(id, out var a) ? a : null;
        public SpawnPoint FindSpawn(string id) => spawns.TryGetValue(id, out var s) ? s : null;

        public bool TryGetSpawn(string spawnId, out Vector3 position, out float yaw)
        {
            SpawnPoint s = FindSpawn(spawnId);
            position = s != null ? s.transform.position : Vector3.zero;
            yaw = s != null ? s.transform.eulerAngles.y : 0f;
            return s != null;
        }

        /// <summary>플레이어를 스폰 포인트로 옮긴다 (페이드 없음, 시나리오 중 순간이동용은 EvacDirector.Travel 사용)</summary>
        public bool TeleportPlayerTo(string spawnId)
        {
            if (!TryGetSpawn(spawnId, out Vector3 pos, out float yaw)) { Warn("스폰", spawnId); return false; }
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p == null) return false;
            p.Teleport(pos, yaw);
            if (HazardManager.Instance != null) HazardManager.Instance.ResyncPlayerZones(pos + Vector3.up);
            return true;
        }

        // ═════════════════════ 상호작용 오브젝트 제어 ═════════════════════
        public void SetInteractableEnabled(string id, bool enabled)
        {
            InteractableObject o = FindInteractable(id);
            if (o != null) o.Interactable = enabled; else Warn("상호작용 오브젝트", id);
        }

        public void SetInteractableLabel(string id, string text)
        {
            InteractableObject o = FindInteractable(id);
            if (o == null) { Warn("상호작용 오브젝트", id); return; }
            if (o.Override == null) o.Override = new InteractableObject.InteractionOverride();
            o.Override.prompt = () => text;
        }

        /// <summary>집 오브젝트의 기본 동작을 시나리오 핸들러로 대체한다 (이벤트는 그대로 발행). null 이면 기본 동작으로 복원</summary>
        public void ReplaceInteractionBehavior(string id, System.Action<PlayerInteractor> handler)
        {
            InteractableObject o = FindInteractable(id);
            if (o == null) { Warn("상호작용 오브젝트", id); return; }
            if (handler == null) { if (o.Override != null) o.Override.onInteract = null; return; }
            if (o.Override == null) o.Override = new InteractableObject.InteractionOverride();
            o.Override.onInteract = handler;
        }

        /// <summary>프롬프트·길게 누르기·가능 여부까지 포함한 전체 오버라이드</summary>
        public void ReplaceInteraction(string id, InteractableObject.InteractionOverride behavior)
        {
            InteractableObject o = FindInteractable(id);
            if (o == null) { Warn("상호작용 오브젝트", id); return; }
            o.Override = behavior;
        }

        private InteractableObject FindInteractable(string id)
        {
            InteractableObject o = InteractableObject.FindById(id);
            if (o != null) return o;
            foreach (InteractableObject i in FindObjectsByType<InteractableObject>(FindObjectsInactive.Include)) if (i.Id == id) return i;
            return null;
        }

        private static T Find<T>(string id) where T : InteractableObject
        {
            foreach (T t in FindObjectsByType<T>(FindObjectsInactive.Include)) if (t.Id == id) return t;
            return null;
        }

        private static void Warn(string kind, string id) => Debug.LogWarning($"[House] {kind} '{id}' 을(를) 찾지 못했습니다.");

        // ═════════════════════ 환경 프리셋 ═════════════════════
        public void ApplyEnvironmentPreset(EnvironmentPreset p)
        {
            if (p == null) return;
            if (p.applySun && sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.intensity = p.sunIntensity;
                sun.color = p.sunColor;
            }
            if (p.applyAmbient) { RenderSettings.ambientLight = p.ambientColor; savedAmbient = p.ambientColor; }
            if (p.applyFog || p.windowView != WindowViewMode.Normal)
            {
                bool smoky = p.windowView == WindowViewMode.DarkSmoky, rain = p.windowView == WindowViewMode.RainStorm;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = p.applyFog ? p.fogColor : (smoky ? new Color(0.12f, 0.1f, 0.09f) : new Color(0.45f, 0.5f, 0.55f));
                RenderSettings.fogDensity = p.applyFog ? p.fogDensity : (smoky ? 0.012f : rain ? 0.008f : 0.002f);
            }
            if (p.applyTraffic) ApplyTraffic(p.pedestrians, p.vehicles, p.trafficDensity);
            if (ambientSource != null && p.ambientSound != null)
            {
                ambientSource.clip = p.ambientSound; ambientSource.volume = p.ambientVolume; ambientSource.loop = true; ambientSource.Play();
            }
            Debug.Log($"[House] 환경 프리셋 적용: {p.displayName} ({p.timeOfDay}, {p.windowView})");
        }

        /// <summary>보행자·차량 on/off 와 밀도(활성 비율)</summary>
        public void ApplyTraffic(bool pedestrians, bool vehicles, float density)
        {
            int i = 0;
            foreach (PedestrianNPC n in FindObjectsByType<PedestrianNPC>(FindObjectsInactive.Include))
                n.gameObject.SetActive(pedestrians && (i++ % 10) < Mathf.RoundToInt(density * 10f));
            i = 0;
            foreach (VehicleController v in FindObjectsByType<VehicleController>(FindObjectsInactive.Include))
                v.gameObject.SetActive(vehicles && (i++ % 10) < Mathf.RoundToInt(density * 10f));
        }
    }
}
