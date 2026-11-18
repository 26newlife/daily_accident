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
        public float PlayerSmoke => Mathf.Max(smokeLevel, MaxOf(z => z.SmokeLevel));
        public float PlayerGas => Mathf.Max(gasLevel, MaxOf(z => z.GasLevel));
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
