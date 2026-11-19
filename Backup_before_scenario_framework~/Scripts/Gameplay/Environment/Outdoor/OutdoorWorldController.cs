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
        }

        private void OnDisable()
        {
            EventBus.Explosion -= OnExplosion;
            EventBus.ScenarioEventRaised -= OnScenarioEvent;
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

        // ───────── Act 4 훅 ─────────
        /// <summary>소방차/구급차 도착 연출의 진입점 (현재는 빈 이벤트)</summary>
        public void OnFireTruckArrive() => FireTruckArrived?.Invoke();
    }
}
