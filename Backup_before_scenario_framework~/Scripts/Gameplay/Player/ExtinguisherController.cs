using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 분말 소화기 사용. ① 인벤토리에서 소화기를 선택하고 사용키(숫자키/Q)로 "안전핀 뽑기" ② 소화기를 든 채 분사 버튼(마우스 왼쪽/RT)을 누르면
    /// 카메라 앞쪽 부채꼴 범위의 불이 줄어든다 (빗자루로 쓸듯 조준을 좌우로 움직여 여러 불길을 끈다). 분사량은 한정돼 있다.
    /// </summary>
    public class ExtinguisherController : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private ItemData extinguisherItem;

        [Header("분사")]
        [SerializeField, Min(1f)] private float sprayRange = 4f;
        [SerializeField, Min(0.1f)] private float sprayRadius = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("분사 중 초당 줄어드는 불 크기")] private float suppressPerSecond = 0.2f;
        [SerializeField, Min(1f), Tooltip("총 분사 가능 시간(초)")] private float chargeSeconds = 18f;
        [SerializeField] private ParticleSystem sprayFx;
        [SerializeField] private AudioSource sprayAudio;

        [Header("문구")]
        [SerializeField] private string pinPulledMessage = "안전핀을 뽑았다. 불길 아래쪽을 겨냥해 쓸듯이 분사하자.";
        [SerializeField] private string alreadyPulledMessage = "이미 안전핀을 뽑았다. 마우스 왼쪽 버튼(RT)으로 분사한다.";
        [SerializeField] private string pinNeededMessage = "안전핀을 먼저 뽑아야 한다. (소화기 슬롯 키)";
        [SerializeField] private string emptyMessage = "소화기가 비었다.";

        [Header("이벤트 id")]
        [SerializeField] private string pinPulledEventId = "extinguisher_pin_pulled";
        [SerializeField] private string emptyEventId = "extinguisher_empty";

        private float charge;
        private float messageCooldown;
        private bool wasSpraying;

        public bool PinPulled { get; private set; }
        public float Charge01 => ChargeTotal > 0f ? charge / ChargeTotal : 0f;

        // 분사 세기/총량은 GameTimingConfig 가 우선한다 (진압 약 15~20초)
        private float SuppressRate => GameTimingConfig.Current.extinguisherSuppressPerSecond;
        private float ChargeTotal => GameTimingConfig.Current.extinguisherChargeSeconds;

        private void Awake() { charge = ChargeTotal; }
        private void OnEnable() => EventBus.ScenarioEventRaised += OnScenarioEvent;
        private void OnDisable() => EventBus.ScenarioEventRaised -= OnScenarioEvent;

        private void OnScenarioEvent(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.ItemUsed || extinguisherItem == null || e.SourceId != extinguisherItem.Id) return;
            if (!PinPulled)
            {
                PinPulled = true;
                EventBus.ShowMessage(pinPulledMessage);
                EventBus.Raise(ScenarioEventType.Custom, pinPulledEventId, true);
            }
            else EventBus.ShowMessage(alreadyPulledMessage);
        }

        private bool HoldingExtinguisher
            => inventory != null && extinguisherItem != null && inventory.GetItem(inventory.SelectedIndex) == extinguisherItem;

        private void Update()
        {
            messageCooldown -= Time.deltaTime;
            bool want = input != null && input.SprayHeld && HoldingExtinguisher && !GameFlow.GameplayBlocked && !GameFlow.CursorFree;

            if (!want) { SetSpray(false); return; }

            if (!PinPulled)
            {
                if (!wasSpraying && messageCooldown <= 0f) { EventBus.ShowMessage(pinNeededMessage); messageCooldown = 2f; }
                wasSpraying = true;
                SetSpray(false);
                return;
            }
            wasSpraying = true;

            if (charge <= 0f)
            {
                SetSpray(false);
                if (messageCooldown <= 0f) { EventBus.ShowMessage(emptyMessage); messageCooldown = 3f; EventBus.Raise(ScenarioEventType.Custom, emptyEventId, true); }
                return;
            }

            SetSpray(true);
            charge -= Time.deltaTime;
            SprayFires(Time.deltaTime);
        }

        private void LateUpdate() { if (input != null && !input.SprayHeld) wasSpraying = false; }

        private void SprayFires(float dt)
        {
            if (viewCamera == null) return;
            Transform cam = viewCamera.transform;
            RaycastHit[] hits = Physics.SphereCastAll(cam.position, sprayRadius, cam.forward, sprayRange, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit h in hits)
            {
                FireSource fire = h.collider.GetComponentInParent<FireSource>();
                if (fire != null && fire.IsLit) fire.Suppress(SuppressRate * dt);
            }
        }

        private void SetSpray(bool on)
        {
            if (sprayFx != null)
            {
                if (on && !sprayFx.isPlaying) sprayFx.Play();
                else if (!on && sprayFx.isPlaying) sprayFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (sprayAudio != null)
            {
                if (on && !sprayAudio.isPlaying && sprayAudio.clip != null) sprayAudio.Play();
                else if (!on && sprayAudio.isPlaying) sprayAudio.Stop();
            }
        }
    }
}
