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

        public float Value => value;
        public float MaxValue => maxValue;
        public float Normalized => maxValue > 0f ? value / maxValue : 0f;
        public bool IsDead { get; private set; }
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

        private void Update()
        {
            if (IsDead || GameFlow.GameplayBlocked) return;
            float dt = Time.deltaTime;

            float smoke = HazardManager.Instance != null ? HazardManager.Instance.PlayerSmoke : 0f;
            IsInSmoke = smoke > smokeThreshold;

            float before = value;
            if (IsInSmoke)
            {
                float mult = 1f;
                if (player != null && player.IsCrouching) mult *= crouchMultiplier;
                if (inventory != null && inventory.IsEquipped(wetTowelItem)) mult *= wetTowelMultiplier;
                CurrentMultiplier = mult;

                value = Mathf.Max(0f, value - smoke * drainPerSecondAtFullSmoke * mult * dt);
                timeSinceDamage = 0f;
                UpdateCough(dt);
            }
            else
            {
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

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            EventBus.RaisePlayerDied(deathReason);
        }
    }
}
