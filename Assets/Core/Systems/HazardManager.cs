using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 환경 위험 상태 총괄. 전역 값(GasLevel / SmokeLevel / Temperature)과 구역(HazardZone)별 값을 가진다.
    /// 플레이어가 체감하는 값은 "전역과 현재 들어가 있는 구역들 중 가장 높은 값"이다.
    /// (지금은 변수와 이벤트만 정의 — 값을 올리는 연출은 Act 로직이 담당)
    /// </summary>
    public class HazardManager : MonoBehaviour
    {
        [Header("전역 초기값")]
        [SerializeField, Range(0f, 1f)] private float gasLevel;
        [SerializeField, Range(0f, 1f)] private float smokeLevel;
        [SerializeField] private float temperature = 24f;
        [SerializeField, Tooltip("정상 실내 온도 (섭씨)")] private float ambientTemperature = 24f;

        private readonly List<HazardZone> allZones = new List<HazardZone>();
        private readonly List<HazardZone> playerZones = new List<HazardZone>();

        public static HazardManager Instance { get; private set; }

        public float GasLevel => gasLevel;
        public float SmokeLevel => smokeLevel;
        public float Temperature => temperature;
        public float AmbientTemperature => ambientTemperature;
        public IReadOnlyList<HazardZone> Zones => allZones;
        public IReadOnlyList<HazardZone> PlayerZones => playerZones;

        /// <summary>전역 값이 바뀔 때</summary>
        public event Action Changed;
        /// <summary>플레이어가 들어가 있는 구역이 바뀔 때</summary>
        public event Action PlayerZonesChanged;

        // ───────── 플레이어가 체감하는 값 ─────────
        public float PlayerSmoke
        {
            get
            {
                bool crouching = Player != null && Player.IsCrouching;
                float zoneMax = 0f;
                bool isolated = false;
                foreach (HazardZone z in playerZones)
                {
                    float v = z.SmokeLevel * (crouching ? z.CrouchSmokeScale : 1f);
                    if (z.Isolated) isolated = true;
                    zoneMax = Mathf.Max(zoneMax, v);
                }
                return isolated ? zoneMax : Mathf.Max(smokeLevel, zoneMax);   // 격리 구역(대피공간 등) 안에서는 전역 연기를 무시
            }
        }

        private PlayerController player;
        private PlayerController Player { get { if (player == null) player = FindFirstObjectByType<PlayerController>(); return player; } }

        /// <summary>플레이어가 들어가 있는 구역 중 호흡기 사망 사유를 따로 지정한 구역의 사유 (없으면 null)</summary>
        public DeathReason? PlayerSuffocationReason
        {
            get
            {
                foreach (HazardZone z in playerZones) if (z.SuffocationReason != DeathReason.Suffocation && z.SmokeLevel > 0.05f) return z.SuffocationReason;
                return null;
            }
        }
        /// <summary>플레이어가 체감하는 가스. 연기와 같이 격리 구역(계단탑·옥상·건물 밖·대피공간 등) 안에서는 전역(집 안) 가스를 무시한다</summary>
        public float PlayerGas
        {
            get
            {
                bool isolated = false;
                foreach (HazardZone z in playerZones) if (z.Isolated) { isolated = true; break; }
                float zoneMax = MaxOf(z => z.GasLevel);
                return isolated ? zoneMax : Mathf.Max(gasLevel, zoneMax);
            }
        }
        public float PlayerTemperature => Mathf.Max(temperature, MaxOf(z => z.Temperature));

        private float MaxOf(Func<HazardZone, float> selector)
        {
            float v = 0f;
            foreach (HazardZone z in playerZones) v = Mathf.Max(v, selector(z));
            return v;
        }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetGas(float value) { gasLevel = Mathf.Clamp01(value); Changed?.Invoke(); }
        public void SetSmoke(float value) { smokeLevel = Mathf.Clamp01(value); Changed?.Invoke(); }
        public void SetTemperature(float value) { temperature = value; Changed?.Invoke(); }

        public HazardZone FindZone(string zoneId) => allZones.Find(z => z.ZoneId == zoneId);

        /// <summary>월드 좌표에서의 연기 농도 (NPC 용). 격리 구역 안이면 그 구역 값만, 아니면 전역과 구역 중 큰 값</summary>
        public float SmokeAt(Vector3 worldPoint)
        {
            float zoneMax = 0f;
            bool isolated = false;
            foreach (HazardZone z in allZones)
            {
                if (!z.Contains(worldPoint)) continue;
                if (z.Isolated) isolated = true;
                zoneMax = Mathf.Max(zoneMax, z.SmokeLevel);
            }
            return isolated ? zoneMax : Mathf.Max(smokeLevel, zoneMax);
        }

        /// <summary>
        /// 순간이동(Teleport) 뒤 플레이어가 들어가 있는 구역을 위치 기준으로 다시 맞춘다.
        /// CharacterController 를 껐다 켜는 동안 트리거 Exit/Enter 가 누락돼 이전 구역이 남는 것을 막는다.
        /// </summary>
        public void ResyncPlayerZones(Vector3 playerCenter)
        {
            bool changed = false;
            for (int i = playerZones.Count - 1; i >= 0; i--)
            {
                HazardZone z = playerZones[i];
                if (z != null && z.Contains(playerCenter)) continue;
                playerZones.RemoveAt(i);
                changed = true;
                if (z != null) EventBus.Raise(ScenarioEventType.ZoneExited, z.ZoneId);
            }
            foreach (HazardZone z in allZones)
            {
                if (playerZones.Contains(z) || !z.Contains(playerCenter)) continue;
                playerZones.Add(z);
                changed = true;
                EventBus.Raise(ScenarioEventType.ZoneEntered, z.ZoneId);
            }
            if (changed) PlayerZonesChanged?.Invoke();
        }

        /// <summary>구역 값이 바뀌었음을 알림 (HazardZone 이 호출)</summary>
        public void NotifyZoneValuesChanged() => Changed?.Invoke();

        internal void Register(HazardZone zone) { if (!allZones.Contains(zone)) allZones.Add(zone); }
        internal void Unregister(HazardZone zone) { allZones.Remove(zone); if (playerZones.Remove(zone)) PlayerZonesChanged?.Invoke(); }

        internal void PlayerEntered(HazardZone zone)
        {
            if (playerZones.Contains(zone)) return;
            playerZones.Add(zone);
            PlayerZonesChanged?.Invoke();
            EventBus.Raise(ScenarioEventType.ZoneEntered, zone.ZoneId);
        }

        internal void PlayerExited(HazardZone zone)
        {
            if (!playerZones.Remove(zone)) return;
            PlayerZonesChanged?.Invoke();
            EventBus.Raise(ScenarioEventType.ZoneExited, zone.ZoneId);
        }
    }
}
