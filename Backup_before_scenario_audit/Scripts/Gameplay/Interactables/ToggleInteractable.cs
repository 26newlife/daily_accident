using GameCore;
using UnityEngine;
using UnityEngine.Events;

namespace GameGameplay
{
    /// <summary>
    /// 켜짐/꺼짐(열림/닫힘) 2상태 상호작용: 스위치, 창문, 문 등.
    /// movingPart 를 지정하면 꺼짐 포즈 ↔ 켜짐 포즈로 부드럽게 움직인다(위치 / 회전). 잠금(Locked) 중에는 상태가 바뀌지 않는다.
    /// </summary>
    public class ToggleInteractable : InteractableObject
    {
        [Header("상태")]
        [SerializeField] private bool isOn;
        [SerializeField] private bool startLocked;
        [SerializeField, Tooltip("현재 켜짐일 때 표시할 문구 (예: 창문 닫기)")] private string promptWhenOn = "닫기";
        [SerializeField, Tooltip("현재 꺼짐일 때 표시할 문구 (예: 창문 열기)")] private string promptWhenOff = "열기";
        [SerializeField, Tooltip("잠긴 상태에서 상호작용했을 때 안내 문구")] private string lockedMessage = "잠겨 있다.";

        [Header("움직이는 부분 (선택)")]
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 offLocalPosition;
        [SerializeField] private Vector3 onLocalPosition;
        [SerializeField] private Vector3 offLocalEuler;
        [SerializeField] private Vector3 onLocalEuler;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.5f;

        [Header("연동")]
        [Tooltip("상태가 바뀔 때 함께 켜고 끌 조명 묶음 (조명 스위치용)")]
        [SerializeField] private LightGroup[] linkedLights;
        [SerializeField] private UnityEvent<bool> toggled;

        private float blend; // 0 = 꺼짐 포즈, 1 = 켜짐 포즈

        public bool IsOn => isOn;
        public bool IsLocked { get; private set; }

        /// <summary>시나리오 이벤트 종류 (하위 클래스가 바꿀 수 있다)</summary>
        protected virtual ScenarioEventType ChangeEventType => ScenarioEventType.Toggled;

        protected virtual void Awake()
        {
            IsLocked = startLocked;
            blend = isOn ? 1f : 0f;
            ApplyPose();
        }

        protected virtual void Update()
        {
            float target = isOn ? 1f : 0f;
            if (Mathf.Approximately(blend, target)) return;
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime / moveDuration);
            ApplyPose();
        }

        public override string GetPrompt() => isOn ? promptWhenOn : promptWhenOff;

        /// <summary>잠겨 있을 때는 바로 "잠겨 있다" 안내가 나오도록 길게 누르기를 요구하지 않는다</summary>
        public override float HoldSeconds => IsLocked ? 0f : base.HoldSeconds;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (IsLocked)
            {
                EventBus.ShowMessage(lockedMessage);
                Publish(ScenarioEventType.Interacted);
                return;
            }
            SetState(!isOn);
        }

        /// <summary>상태를 직접 지정 (스크립트/시나리오용). silent 면 이벤트를 발행하지 않는다.</summary>
        public void SetState(bool on, bool silent = false)
        {
            if (isOn == on) return;
            isOn = on;
            if (linkedLights != null)
                foreach (LightGroup g in linkedLights) if (g != null) g.SetOn(on);
            toggled?.Invoke(on);
            if (!silent) Publish(ChangeEventType, on);
        }

        public void SetLocked(bool locked) => IsLocked = locked;

        /// <summary>잠긴 상태에서 상호작용했을 때의 안내 문구를 바꾼다 (예: 문손잡이가 뜨거울 때)</summary>
        public void SetLockedMessage(string message) => lockedMessage = message;

        private void ApplyPose()
        {
            if (movingPart == null) return;
            movingPart.localPosition = Vector3.Lerp(offLocalPosition, onLocalPosition, blend);
            movingPart.localRotation = Quaternion.Slerp(Quaternion.Euler(offLocalEuler), Quaternion.Euler(onLocalEuler), blend);
        }
    }
}
