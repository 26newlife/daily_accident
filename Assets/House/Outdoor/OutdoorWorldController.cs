using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 창밖 풍경 총괄. (1) 플레이어가 창문 가까이 있을 때만 보행자를 상세 갱신(거리 LOD),
    /// (2) 창문을 열면 바깥 환경음이 커지도록 3D 오디오 볼륨 조절, (3) 폭발 이벤트(EventBus.Explosion) 때
    /// 8층 불꽃/연기, 보행자 반응, 맞은편 동 주민, 사이렌을 시작한다. Act 4 용 소방차 도착 훅(OnFireTruckArrive)도 제공한다.
    /// </summary>
    public class OutdoorWorldController : MonoBehaviour
    {
        [Serializable]
        public struct AmbientSlot
        {
            [Tooltip("환경음 AudioSource (새소리/차 소리/아이들 소리/개 짖는 소리 — 클립은 나중에 연결)")] public AudioSource source;
            [Range(0f, 1f)] public float baseVolume;
        }

        [Header("참조")]
        [SerializeField] private Transform player;
        [SerializeField] private WindowViewZone[] windowZones;
        [SerializeField] private PedestrianSpawner spawner;
        [SerializeField] private FacadeResident[] residents;
        [SerializeField] private OutdoorFireEffect fire;
        [SerializeField] private VehicleController[] movingVehicles;
        [SerializeField, Tooltip("폭발 지점 (8층 외벽)")] private Transform explosionPoint;

        [Header("환경음 (창문을 열면 커진다)")]
        [SerializeField] private AmbientSlot[] ambientSlots;
        [SerializeField, Range(0f, 1f)] private float closedVolumeScale = 0.2f;
        [SerializeField, Range(0f, 1f)] private float openVolumeScale = 1f;
        [SerializeField] private float volumeSmoothing = 2.5f;
        [SerializeField] private string[] windowIds = { "window_living", "window_bedroom", "window_kitchen", "door_balcony" };

        [Header("폭발 반응")]
        [SerializeField] private Vector2 npcReactDelay = new Vector2(0.4f, 3.5f);
        [SerializeField] private Vector2 residentReactDelay = new Vector2(2f, 9f);
        [SerializeField] private float vehicleStopSeconds = 25f;
        [SerializeField, Min(0f)] private float sirenDelay = 14f;
        [SerializeField] private AudioSource sirenSource;

        [Header("긴급 차량 (사이렌 대사에 맞춰 도로를 지나간다)")]
        [SerializeField, Tooltip("단지 앞 도로(Road_A) 차선의 z 좌표")] private float roadLaneZ = 32.5f;
        [SerializeField, Tooltip("정차 지점에서 출발/퇴장 지점까지 거리(m)")] private float roadRunLength = 170f;
        [SerializeField, Min(5f)] private float emergencySpeed = 17f;

        private readonly List<EmergencyVehicle> emergencyVehicles = new List<EmergencyVehicle>();
        private readonly HashSet<string> openWindows = new HashSet<string>();
        private float volumeScale;
        private bool exploded;

        public static OutdoorWorldController Instance { get; private set; }

        /// <summary>Act 4: 소방차/구급차 도착 시 호출. 지금은 이벤트만 발행한다 (구현 예정)</summary>
        public event Action FireTruckArrived;
        public bool HasExploded => exploded;

        private void Awake()
        {
            Instance = this;
            volumeScale = closedVolumeScale;
            if (player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; PedestrianNPC.DetailActive = false; }

        private void OnEnable()
        {
            EventBus.Explosion += OnExplosion;
            EventBus.ScenarioEventRaised += OnScenarioEvent;
            EventBus.EmergencyVehicleRequested += DispatchEmergency;
        }

        private void OnDisable()
        {
            EventBus.Explosion -= OnExplosion;
            EventBus.ScenarioEventRaised -= OnScenarioEvent;
            EventBus.EmergencyVehicleRequested -= DispatchEmergency;
        }

        private void Update()
        {
            // 거리 LOD: 창문 가까이 있을 때만 보행자 상세 애니메이션
            bool near = false;
            if (player != null && windowZones != null)
                foreach (WindowViewZone z in windowZones) if (z != null && z.IsPlayerNear(player.position)) { near = true; break; }
            PedestrianNPC.DetailActive = near;

            // 환경음: 열린 창이 하나라도 있으면 크게
            float target = openWindows.Count > 0 ? openVolumeScale : closedVolumeScale;
            volumeScale = Mathf.MoveTowards(volumeScale, target, Time.deltaTime * volumeSmoothing * 0.5f);
            if (ambientSlots != null)
                foreach (AmbientSlot s in ambientSlots)
                    if (s.source != null) s.source.volume = s.baseVolume * volumeScale * (exploded ? 0.5f : 1f);   // 폭발 후에는 일상 소음이 잦아든다

            // 사이렌: 창문을 닫으면 작게, 열면 크게 들린다 (환경음과 같은 배율)
            for (int i = emergencyVehicles.Count - 1; i >= 0; i--)
            {
                if (emergencyVehicles[i] == null) { emergencyVehicles.RemoveAt(i); continue; }
                emergencyVehicles[i].SetVolumeScale(Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(closedVolumeScale, openVolumeScale, volumeScale)));
            }
        }

        private void OnScenarioEvent(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.Toggled) return;
            foreach (string id in windowIds)
                if (id == e.SourceId) { if (e.State) openWindows.Add(id); else openWindows.Remove(id); }
        }

        // ───────── 폭발 ─────────
        private void OnExplosion()
        {
            if (exploded) return;
            exploded = true;
            if (fire != null) fire.Ignite();

            Vector3 point = explosionPoint != null ? explosionPoint.position : (fire != null ? fire.transform.position : Vector3.zero);
            if (spawner != null)
                foreach (PedestrianNPC npc in spawner.Spawned)
                    if (npc != null) npc.React(point, UnityEngine.Random.Range(npcReactDelay.x, npcReactDelay.y));
            if (residents != null)
                foreach (FacadeResident r in residents) if (r != null) r.React(UnityEngine.Random.Range(residentReactDelay.x, residentReactDelay.y));
            if (movingVehicles != null)
                foreach (VehicleController v in movingVehicles) if (v != null) v.StopForSeconds(vehicleStopSeconds);
            if (sirenSource != null) Invoke(nameof(StartSiren), sirenDelay);
        }

        private void StartSiren()
        {
            if (sirenSource != null && sirenSource.clip != null) sirenSource.Play();
        }

        // ───────── 긴급 차량 ─────────
        /// <summary>
        /// 구급차/소방차를 단지 앞 도로로 보낸다. 플레이어 집 앞에서 멀리 떨어진 도로 끝에서 출발해 건물 앞을 지나간다
        /// (stopAtBuilding 이면 건물 앞에 서서 경광등만 켜 둔다). Both 는 소방차 뒤로 구급차가 몇 초 늦게 따라온다.
        /// </summary>
        public void DispatchEmergency(EmergencyKind kind, bool stopAtBuilding)
        {
            if (kind == EmergencyKind.None) return;
            if (kind == EmergencyKind.Both)
            {
                SpawnEmergency(EmergencyKind.FireTruck, stopAtBuilding, 0f);
                SpawnEmergency(EmergencyKind.Ambulance, stopAtBuilding, 3.5f);
            }
            else SpawnEmergency(kind, stopAtBuilding, 0f);
        }

        private void SpawnEmergency(EmergencyKind kind, bool stopAtBuilding, float delay)
        {
            if (delay > 0f) { StartCoroutine(SpawnLater(kind, stopAtBuilding, delay)); return; }

            Material template = null;
            float roadY = float.NaN;
            if (movingVehicles != null)
                foreach (VehicleController v in movingVehicles)
                {
                    if (v == null) continue;
                    roadY = v.transform.position.y;
                    Renderer r = v.GetComponentInChildren<Renderer>();
                    if (r != null) template = r.sharedMaterial;
                    break;
                }
            if (float.IsNaN(roadY))
            {
                GameObject road = GameObject.Find("Road_A");
                roadY = road != null ? road.transform.position.y + 0.05f : (player != null ? player.position.y - 27f : 0f);
            }

            float x = player != null ? player.position.x : 0f;
            x = Mathf.Clamp(x, -60f, 60f);
            Vector3 a = new Vector3(x - roadRunLength, roadY, roadLaneZ);
            Vector3 b = new Vector3(x + roadRunLength, roadY, roadLaneZ);
            Vector3 stop = new Vector3(x + (kind == EmergencyKind.FireTruck ? 0f : -9f), roadY, roadLaneZ);   // 구급차는 소방차 뒤에 선다
            EmergencyVehicle ev = EmergencyVehicle.Spawn(kind, a, b, stop, stopAtBuilding, template, emergencySpeed);
            emergencyVehicles.Add(ev);
        }

        private System.Collections.IEnumerator SpawnLater(EmergencyKind kind, bool stopAtBuilding, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnEmergency(kind, stopAtBuilding, 0f);
        }

        // ───────── Act 4 훅 ─────────
        /// <summary>소방차/구급차 도착: 건물 앞 도로에 소방차와 구급차가 사이렌을 울리며 와서 선다</summary>
        public void OnFireTruckArrive()
        {
            DispatchEmergency(EmergencyKind.Both, true);
            FireTruckArrived?.Invoke();
        }
    }
}
