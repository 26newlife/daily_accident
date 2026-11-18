using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>불의 종류 (물을 부었을 때의 결과가 다르다)</summary>
    public enum FireType
    {
        Normal = 0,       // 일반 가연물 (커튼, 소파) — 물이 통한다
        Oil = 1,          // 기름불 — 물을 부으면 불기둥 폭발
        Electrical = 2,   // 전기 화재 — 전기가 흐르는 동안 물을 부으면 감전
    }

    /// <summary>
    /// 불 1곳: 불꽃 파티클 + 열원 + 연기 발생. 시간이 지날수록 커지고(growth), 일정 크기를 넘으면
    /// spreadTargets 로 번진다. 소화기(Suppress)로 줄이거나 물(상호작용)로 끌 수 있고,
    /// 기름불/전기불에 물을 부으면 사망한다. 연기/열은 FireManager 가 집계한다.
    /// </summary>
    public class FireSource : InteractableObject
    {
        private static readonly List<FireSource> all = new List<FireSource>();
        public static IReadOnlyList<FireSource> All => all;

        [Header("불")]
        [SerializeField] private FireType fireType = FireType.Normal;
        [SerializeField, Range(0.05f, 1f)] private float startIntensity = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float maxIntensity = 1f;
        [SerializeField, Min(0f), Tooltip("초당 커지는 양 (소화기로 줄이기 전까지)")] private float growthPerSecond = 0.012f;
        [SerializeField, Tooltip("체크하면 아파트 내부 화재로 집계 (진압 완료 판정/연기). 복도 불은 해제")] private bool countsForApartment = true;

        [Header("번짐")]
        [SerializeField] private FireSource[] spreadTargets;
        [SerializeField, Range(0.1f, 1f)] private float spreadThreshold = 0.5f;
        [SerializeField, Min(1f)] private float spreadDelay = 20f;
        [SerializeField, Range(0.05f, 1f)] private float spreadStartIntensity = 0.25f;

        [Header("영향")]
        [SerializeField] private HazardZone zone;
        [SerializeField, Min(0.5f)] private float heatRadius = 2.2f;

        [Header("연출")]
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystem smokeFx;
        [SerializeField] private Light fireLight;
        [SerializeField] private AudioSource loopAudio;
        [SerializeField] private float flameRate = 40f;
        [SerializeField] private float smokeRate = 14f;
        [SerializeField] private float maxLightIntensity = 2.5f;
        [SerializeField] private Vector2 flameScaleRange = new Vector2(0.35f, 1.3f);

        [Header("물 붓기")]
        [SerializeField] private ItemData[] waterItems;
        [SerializeField, Range(0f, 1f), Tooltip("일반 불에 물을 부었을 때 줄어드는 양")] private float waterReduce = 0.5f;
        [SerializeField] private DeathReason oilWaterDeath = DeathReason.OilFireExplosion;
        [SerializeField] private DeathReason electricWaterDeath = DeathReason.ElectricShock;
        [SerializeField, Tooltip("{0} = 물 아이템 이름")] private string promptFormat = "{0} 붓기";
        [SerializeField, Tooltip("전기 화재: 이 오브젝트(플러그)가 제거되면 더 이상 전기가 흐르지 않는다")] private RemovableInteractable unplugSource;
        [SerializeField] private string waterOnNormalMessage = "물을 부어 불길이 한풀 꺾였다.";

        private float intensity;
        private float spreadTimer;
        private bool burnedOut;
        private bool electricalLive = true;
        private bool uncontrollable;
        private static bool uncontrollableAnnounced;
        private Collider hitCollider;
        private float flickerSeed;

        public FireType Type => fireType;
        public float Intensity => intensity;
        public bool IsLit => intensity > 0.01f;
        public bool CountsForApartment => countsForApartment;
        public HazardZone Zone => zone;
        public float HeatRadius => heatRadius;
        public bool BurnedOut => burnedOut;
        /// <summary>한계 크기에 도달해 소화기/물로 진압할 수 없는 상태 (GameTimingConfig.uncontrollableIntensity)</summary>
        public bool Uncontrollable => uncontrollable;
        /// <summary>전기가 흐르고 있는가 (멀티탭 플러그를 뽑으면 false)</summary>
        public bool ElectricalLive => electricalLive && (unplugSource == null || !unplugSource.IsRemoved);

        public event Action<FireSource> Ignited;
        public event Action<FireSource> Extinguished;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
            flickerSeed = UnityEngine.Random.value * 10f;
            ApplyVisual();
        }

        /// <summary>새 판 시작 시 static 상태 초기화 (GameRunContext)</summary>
        public static void ResetRunState() { uncontrollableAnnounced = false; all.Clear(); }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Update()
        {
            if (!IsLit || GameFlow.GameplayBlocked) return;

            GameTimingConfig timing = GameTimingConfig.Current;
            intensity = Mathf.Min(maxIntensity, intensity + growthPerSecond * timing.fireGrowthMultiplier * Time.deltaTime);
            if (!uncontrollable && countsForApartment && intensity >= timing.uncontrollableIntensity)
            {
                uncontrollable = true;   // 한 번 번지면 되돌릴 수 없다 → 수건 농성(2-B)으로 자연스럽게 유도
                if (!uncontrollableAnnounced) { uncontrollableAnnounced = true; EventBus.ShowMessage(timing.uncontrollableMessage); }
            }

            if (intensity >= spreadThreshold && spreadTargets != null && spreadTargets.Length > 0)
            {
                spreadTimer += Time.deltaTime * timing.fireSpreadMultiplier;
                if (spreadTimer >= spreadDelay)
                {
                    spreadTimer = 0f;
                    foreach (FireSource t in spreadTargets) if (t != null) t.Ignite(spreadStartIntensity);
                }
            }
            ApplyVisual();
        }

        // ───────── 불 상태 ─────────
        public void Ignite(float start = -1f)
        {
            if (burnedOut || IsLit) return;
            intensity = start > 0f ? start : startIntensity;
            spreadTimer = 0f;
            uncontrollable = false;
            ApplyVisual();
            Ignited?.Invoke(this);
            EventBus.Raise(ScenarioEventType.Custom, Id + "_ignited", true);
        }

        /// <summary>불 크기를 줄인다 (소화기/물). 0 이 되면 꺼지고 다시 붙지 않는다.</summary>
        public void Suppress(float amount)
        {
            if (!IsLit) return;
            if (uncontrollable) amount *= GameTimingConfig.Current.uncontrollableSuppressMultiplier;
            intensity -= amount;
            if (intensity <= 0.01f) Extinguish();
            else ApplyVisual();
        }

        public void Extinguish()
        {
            if (!IsLit && burnedOut) return;
            intensity = 0f;
            burnedOut = true;
            ApplyVisual();
            Extinguished?.Invoke(this);
            EventBus.Raise(ScenarioEventType.Custom, Id + "_extinguished", true);
        }

        public void SetLive(bool live) => electricalLive = live;

        private void ApplyVisual()
        {
            bool lit = IsLit;
            if (hitCollider != null) hitCollider.enabled = lit;

            float k = Mathf.Clamp01(intensity / Mathf.Max(0.01f, maxIntensity));
            if (flame != null)
            {
                flame.transform.localScale = Vector3.one * Mathf.Lerp(flameScaleRange.x, flameScaleRange.y, k);
                var em = flame.emission; em.rateOverTime = flameRate * k;
                if (lit && !flame.isPlaying) flame.Play();
                else if (!lit && flame.isPlaying) flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (smokeFx != null)
            {
                var em = smokeFx.emission; em.rateOverTime = smokeRate * k;
                if (lit && !smokeFx.isPlaying) smokeFx.Play();
                else if (!lit && smokeFx.isPlaying) smokeFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (fireLight != null)
            {
                fireLight.enabled = lit;
                fireLight.intensity = maxLightIntensity * k * (0.85f + 0.15f * Mathf.Sin(Time.time * 17f + flickerSeed));
            }
            if (loopAudio != null)
            {
                if (lit && !loopAudio.isPlaying && loopAudio.clip != null) loopAudio.Play();
                else if (!lit && loopAudio.isPlaying) loopAudio.Stop();
            }
        }

        // ───────── 상호작용: 물 붓기 ─────────
        private ItemData FindWater()
        {
            InventorySystem inv = InventorySystem.Instance;
            if (inv == null || waterItems == null) return null;
            foreach (ItemData w in waterItems) if (w != null && inv.Has(w.Id)) return w;
            return null;
        }

        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && IsLit && FindWater() != null;

        public override string GetPrompt()
        {
            ItemData w = FindWater();
            return w != null ? string.Format(promptFormat, w.DisplayName) : base.GetPrompt();
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            ItemData water = FindWater();
            if (water == null) return;

            Publish(ScenarioEventType.Interacted, true);   // 물 붓기 시도 (id = 이 불)

            if (fireType == FireType.Oil) { Die(oilWaterDeath); return; }
            if (fireType == FireType.Electrical && ElectricalLive) { Die(electricWaterDeath); return; }

            InventorySystem.Instance.Remove(water);
            EventBus.ShowMessage(waterOnNormalMessage);
            Suppress(waterReduce);
        }

        private static void Die(DeathReason reason)
        {
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.Die(reason);
            else EventBus.RaisePlayerDied(reason);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { all.Clear(); uncontrollableAnnounced = false; }
    }
}
