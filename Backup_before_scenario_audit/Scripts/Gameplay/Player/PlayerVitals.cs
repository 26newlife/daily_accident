using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 호흡기 체력(기침 게이지, 0~100). 연기 농도 × 자세(서기/웅크리기) × 젖은 수건 착용 여부로 감소 속도가 정해지고,
    /// 연기가 없으면 천천히 회복한다. 0이 되면 질식 사망(EventBus.PlayerDied).
    /// 화면 효과는 VitalsScreenEffect 가, 기침 소리는 coughSource/coughClips 슬롯이 담당한다.
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private PlayerController player;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private ItemData wetTowelItem;

        [Header("게이지")]
        [SerializeField] private float maxValue = 100f;
        [SerializeField, Tooltip("연기 농도 1.0 에서 서 있을 때 초당 감소량")] private float drainPerSecondAtFullSmoke = 12f;
        [SerializeField, Range(0f, 0.5f), Tooltip("이 농도 이하의 연기는 영향 없음")] private float smokeThreshold = 0.05f;
        [SerializeField, Tooltip("웅크렸을 때 감소 배율 (연기는 위로 모이므로 낮은 자세가 유리)")] private float crouchMultiplier = 0.4f;
        [SerializeField, Tooltip("젖은 수건 착용 시 감소 배율")] private float wetTowelMultiplier = 0.35f;
        [SerializeField] private float recoveryPerSecond = 6f;
        [SerializeField] private float recoveryDelay = 2f;
        [SerializeField] private DeathReason deathReason = DeathReason.Suffocation;

        [Header("위험 행동 가속 (연기 속)")]
        [SerializeField, Min(1f), Tooltip("연기 속에서 달릴 때 감소 배율")] private float sprintMultiplier = 1.6f;
        [SerializeField, Min(0f), Tooltip("젖은 수건 없이 연기 속에 머문 시간(초)당 늘어나는 배율")] private float unprotectedRampPerSecond = 0.012f;
        [SerializeField, Min(1f), Tooltip("수건 없는 장시간 노출 배율의 상한")] private float unprotectedRampMax = 1.4f;

        [Header("화상 (0 ~ burnMax)")]
        [SerializeField, Min(1f)] private float burnMax = 100f;
        [SerializeField, Min(0f), Tooltip("불에서 벗어난 뒤 초당 회복량")] private float burnRecoveryPerSecond = 0.8f;
        [SerializeField, Min(0f), Tooltip("마지막 화상 후 회복이 시작되기까지(초)")] private float burnRecoveryDelay = 6f;
        [SerializeField, Range(0f, 0.8f), Tooltip("화상 최대일 때 이동 속도 감소 비율")] private float burnSpeedPenaltyAtFull = 0.3f;
        [SerializeField, Min(0f), Tooltip("화상이 높을수록 불 근처에서 화상이 쌓이는 속도 증가 (최대 1 + 이 값)")] private float burnFireAmplify = 1.0f;
        [SerializeField, Range(0f, 0.2f), Tooltip("화상 높을 때 손떨림(카메라 흔들림) 세기")] private float burnShakeAmplitude = 0.03f;
        [SerializeField] private DeathReason burnDeathReason = DeathReason.BurnInjury;

        [Header("점프")]
        [SerializeField, Min(0f), Tooltip("연기 농도 1.0 에서 점프 1회당 추가로 깎이는 호흡기 체력. 연기가 없으면 소모 없음")] private float jumpExtraDrainAtFullSmoke = 6f;

        [Header("기침")]
        [SerializeField, Range(0f, 1f), Tooltip("게이지 비율이 이 값 이하이고 연기 속이면 기침")] private float coughBelowRatio = 0.7f;
        [SerializeField] private Vector2 coughInterval = new Vector2(2f, 5f);
        [SerializeField] private AudioSource coughSource;      // 소리 슬롯 (클립은 나중에)
        [SerializeField] private AudioClip[] coughClips;
        [SerializeField] private float coughShakeAmplitude = 0.04f;
        [SerializeField] private float coughShakeDuration = 0.35f;

        private float value;
        private float timeSinceDamage = 999f;
        private float nextCoughTimer;
        private float burn, burnSinceDamage = 999f, burnShakeTimer, unprotectedTime;

        public float Value => value;
        public float MaxValue => maxValue;
        public float Normalized => maxValue > 0f ? value / maxValue : 0f;
        public bool IsDead { get; private set; }
        /// <summary>화상 수치 (0 ~ MaxBurn). 한도에 닿으면 BurnInjury 사망</summary>
        public float Burn => burn;
        public float MaxBurn => burnMax;
        public float Burn01 => burnMax > 0f ? Mathf.Clamp01(burn / burnMax) : 0f;
        /// <summary>현재 연기 영향을 받고 있는가 (HUD 게이지 표시 조건)</summary>
        public bool IsInSmoke { get; private set; }
        /// <summary>현재 체감 감소 배율(자세 × 수건). 디버그/HUD 용</summary>
        public float CurrentMultiplier { get; private set; } = 1f;

        public event Action Changed;
        public event Action Coughed;

        private void Awake()
        {
            value = maxValue;
            ScheduleCough();
        }

        private void OnEnable() { if (player != null) player.Jumped += OnPlayerJumped; }
        private void OnDisable() { if (player != null) player.Jumped -= OnPlayerJumped; }

        /// <summary>연기 속에서 점프하면 숨이 더 차서 호흡기 체력이 추가로 닳는다 (연기가 짙을수록 큼)</summary>
        private void OnPlayerJumped()
        {
            if (IsDead || !IsInSmoke) return;
            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            float mult = 1f;
            if (inventory != null && inventory.IsEquipped(wetTowelItem)) mult *= wetTowelMultiplier;
            Modify(-smoke * jumpExtraDrainAtFullSmoke * mult);
            timeSinceDamage = 0f;
        }

        private void Update()
        {
            if (IsDead || GameFlow.GameplayBlocked) return;
            float dt = Time.deltaTime;
            UpdateBurn(dt);
            if (IsDead) return;

            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            IsInSmoke = smoke > smokeThreshold;

            float before = value;
            if (IsInSmoke)
            {
                float mult = 1f;
                bool towel = inventory != null && inventory.IsEquipped(wetTowelItem);
                if (player != null && player.IsCrouching) mult *= crouchMultiplier;
                else if (player != null && player.IsSprinting) mult *= sprintMultiplier;   // 연기 속에서 달리면 숨이 훨씬 빨리 찬다
                if (towel) mult *= wetTowelMultiplier;
                else { unprotectedTime += dt; mult *= Mathf.Min(unprotectedRampMax, 1f + unprotectedTime * unprotectedRampPerSecond); }   // 수건 없이 오래 있을수록 가속
                CurrentMultiplier = mult;

                value = Mathf.Max(0f, value - smoke * drainPerSecondAtFullSmoke * GameTimingConfig.Current.vitalsDrainMultiplier * mult * dt);
                timeSinceDamage = 0f;
                UpdateCough(dt);
            }
            else
            {
                unprotectedTime = Mathf.Max(0f, unprotectedTime - dt * 2f);
                timeSinceDamage += dt;
                if (timeSinceDamage >= recoveryDelay) value = Mathf.Min(maxValue, value + recoveryPerSecond * dt);
            }

            if (!Mathf.Approximately(before, value)) Changed?.Invoke();
            if (value <= 0f) Die();
        }

        private void UpdateCough(float dt)
        {
            if (Normalized > coughBelowRatio) return;
            nextCoughTimer -= dt;
            if (nextCoughTimer > 0f) return;

            ScheduleCough();
            if (coughSource != null && coughClips != null && coughClips.Length > 0)
                coughSource.PlayOneShot(coughClips[UnityEngine.Random.Range(0, coughClips.Length)]);
            if (player != null) player.Shake(coughShakeAmplitude, coughShakeDuration);
            Coughed?.Invoke();
        }

        private void ScheduleCough() => nextCoughTimer = UnityEngine.Random.Range(coughInterval.x, coughInterval.y);

        /// <summary>게이지를 직접 변경 (아이템/연출용). 음수면 감소</summary>
        public void Modify(float amount)
        {
            if (IsDead) return;
            value = Mathf.Clamp(value + amount, 0f, maxValue);
            Changed?.Invoke();
            if (value <= 0f) Die();
        }

        // ───────── 화상 ─────────
        /// <summary>화상 수치를 직접 올린다 (뜨거운 버튼 등). 한도에 닿으면 사망</summary>
        public void AddBurn(float amount)
        {
            if (IsDead || amount <= 0f) return;
            burn = Mathf.Min(burnMax, burn + amount);
            burnSinceDamage = 0f;
            if (player != null) player.Shake(0.06f, 0.5f);
            Changed?.Invoke();
            Debug.Log($"[Risk] burn +{amount:0.#} → {burn:0.#}/{burnMax:0}");
            if (burn >= burnMax) DieWith(burnDeathReason);
        }

        /// <summary>불길 근접 누적 화상. 이미 화상이 높으면 더 빨리 쌓인다</summary>
        public void AddFireBurn(float amount)
        {
            if (IsDead || amount <= 0f) return;
            burn = Mathf.Min(burnMax, burn + amount * (1f + Burn01 * burnFireAmplify));
            burnSinceDamage = 0f;
            Changed?.Invoke();
            if (burn >= burnMax) DieWith(burnDeathReason);
        }

        private void UpdateBurn(float dt)
        {
            burnSinceDamage += dt;
            if (burn > 0f && burnSinceDamage >= burnRecoveryDelay)
            {
                burn = Mathf.Max(0f, burn - burnRecoveryPerSecond * dt);
                Changed?.Invoke();
            }
            if (player != null)
            {
                player.ExternalSpeedMultiplier = 1f - burnSpeedPenaltyAtFull * Burn01;   // 화상 → 이동 속도 감소
                if (Burn01 > 0.3f)
                {
                    burnShakeTimer -= dt;
                    if (burnShakeTimer <= 0f) { burnShakeTimer = 0.7f; player.Shake(burnShakeAmplitude * Burn01, 0.4f); }   // 손 떨림
                }
            }
        }

        private void Die() => DieWith(deathReason);

        private void DieWith(DeathReason reason)
        {
            if (IsDead) return;
            IsDead = true;
            EventBus.RaisePlayerDied(reason);
        }
    }
}
