using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 화재 총괄: 불의 크기를 연기 농도(전역 + 불이 난 방)와 온도로 환산하고, 불에 너무 가까이 있으면 화상 사망 판정을 한다.
    /// 불이 모두 꺼지면 "fire_all_out" 이벤트를 발행한다. 진압 후 연기가 얼마나 남는지는 SmokeFloor 로 조절한다.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        [Header("연기")]
        [SerializeField, Min(0f), Tooltip("불 크기 합 1.0 당 전역(집 전체) 연기 농도")] private float globalSmokePerIntensity = 0.35f;
        [SerializeField, Min(0f), Tooltip("불 크기 1.0 당 불이 난 방의 연기 농도")] private float roomSmokePerIntensity = 0.9f;
        [SerializeField, Min(0.001f)] private float smokeRisePerSecond = 0.035f;
        [SerializeField, Min(0.001f)] private float smokeClearPerSecond = 0.05f;

        [Header("열 / 화상")]
        [SerializeField] private float ambientTemperature = 24f;
        [SerializeField] private float temperaturePerIntensity = 45f;
        [SerializeField, Min(0.5f), Tooltip("불 가까이 이 시간(초) 머물면 화상 사망")] private float burnDeathSeconds = 5f;
        [SerializeField, Min(1f)] private float burnRecoveryMultiplier = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("이 불 크기 미만이면 열기를 느끼지 않는다")] private float burnMinIntensity = 0.2f;
        [SerializeField] private string heatWarning = "열기가 얼굴을 덮친다. 더 가까이 가면 위험하다.";
        [SerializeField, Min(1f)] private float warningInterval = 3f;

        private readonly Dictionary<HazardZone, float> zoneSmoke = new Dictionary<HazardZone, float>();
        private float globalSmoke;
        private float burnExposure;
        private float warningTimer;
        private bool everLit, allOutRaised;

        public static FireManager Instance { get; private set; }

        /// <summary>불이 다 꺼진 뒤에도 남는 연기 농도 (진압 결과에 따라 Act 가 설정)</summary>
        public float SmokeFloor { get; set; }
        public float TotalIntensity { get; private set; }
        public int ActiveApartmentFires { get; private set; }
        public float BurnExposure01 => Mathf.Clamp01(burnExposure / burnDeathSeconds);

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (GameFlow.GameplayBlocked) return;
            float dt = Time.deltaTime;

            // 집계
            float total = 0f;
            int active = 0;
            var roomTarget = new Dictionary<HazardZone, float>();
            foreach (FireSource f in FireSource.All)
            {
                if (!f.IsLit) continue;
                if (f.CountsForApartment) { total += f.Intensity; active++; everLit = true; }
                if (f.Zone != null)
                {
                    roomTarget.TryGetValue(f.Zone, out float cur);
                    roomTarget[f.Zone] = cur + f.Intensity * roomSmokePerIntensity;
                }
            }
            TotalIntensity = total;
            ActiveApartmentFires = active;

            UpdateSmoke(total, roomTarget, dt);
            UpdateHeat(dt);
            CheckAllOut(active);
        }

        private void UpdateSmoke(float total, Dictionary<HazardZone, float> roomTarget, float dt)
        {
            HazardManager hazard = HazardManager.Instance;
            if (hazard == null) return;

            float target = Mathf.Clamp01(total * globalSmokePerIntensity + (everLit ? SmokeFloor : 0f));
            globalSmoke = Mathf.MoveTowards(globalSmoke, target, (target > globalSmoke ? smokeRisePerSecond : smokeClearPerSecond) * dt);
            if (!Mathf.Approximately(hazard.SmokeLevel, globalSmoke)) hazard.SetSmoke(globalSmoke);

            foreach (var kv in roomTarget) if (!zoneSmoke.ContainsKey(kv.Key)) zoneSmoke[kv.Key] = 0f;
            var keys = new List<HazardZone>(zoneSmoke.Keys);
            foreach (HazardZone z in keys)
            {
                roomTarget.TryGetValue(z, out float t);
                t = Mathf.Clamp01(t);
                float cur = zoneSmoke[z];
                cur = Mathf.MoveTowards(cur, t, (t > cur ? smokeRisePerSecond * 1.5f : smokeClearPerSecond) * dt);
                zoneSmoke[z] = cur;
                z.SetValues(z.GasLevel, cur, ambientTemperature + temperaturePerIntensity * Mathf.Clamp01(t));
            }

            hazard.SetTemperature(ambientTemperature + temperaturePerIntensity * Mathf.Clamp01(total));
        }

        private void UpdateHeat(float dt)
        {
            if (player == null) return;
            Vector3 p = player.transform.position + Vector3.up * 1.0f;

            float closeness = 0f;
            foreach (FireSource f in FireSource.All)
            {
                if (!f.IsLit || f.Intensity < burnMinIntensity) continue;
                float radius = f.HeatRadius * Mathf.Lerp(0.5f, 1f, f.Intensity);
                float d = Vector3.Distance(p, f.transform.position);
                if (d < radius) closeness = Mathf.Max(closeness, 1f - d / radius);
            }

            if (closeness > 0f)
            {
                burnExposure += dt * (0.4f + closeness);
                warningTimer -= dt;
                if (BurnExposure01 > 0.3f && warningTimer <= 0f) { warningTimer = warningInterval; EventBus.ShowScent(heatWarning); }
                if (burnExposure >= burnDeathSeconds)
                {
                    burnExposure = 0f;
                    if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(DeathReason.Burn);
                    else EventBus.RaisePlayerDied(DeathReason.Burn);
                }
            }
            else burnExposure = Mathf.Max(0f, burnExposure - dt * burnRecoveryMultiplier);
        }

        private void CheckAllOut(int active)
        {
            if (active > 0) { allOutRaised = false; return; }
            if (!everLit || allOutRaised) return;
            allOutRaised = true;
            EventBus.Raise(ScenarioEventType.Custom, "fire_all_out", true);
        }
    }
}
