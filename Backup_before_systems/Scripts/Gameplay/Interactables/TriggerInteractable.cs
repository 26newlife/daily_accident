using GameCore;
using UnityEngine;
using UnityEngine.Events;

namespace GameGameplay
{
    /// <summary>
    /// 동작이 아직 없는 범용 상호작용 (자리표시용). 누르면 Interacted 이벤트와 UnityEvent 만 발행하고,
    /// message 가 있으면 HUD 에 안내 문구를 띄운다. 다음 단계에서 Act 로직이 이 id 에 반응하게 만든다.
    /// </summary>
    public class TriggerInteractable : InteractableObject
    {
        [SerializeField, Tooltip("상호작용 시 HUD 에 띄울 문구 (비워두면 표시 안 함)")] private string message;
        [SerializeField] private bool oneShot;
        [SerializeField] private UnityEvent onInteracted;

        private bool used;

        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && !(oneShot && used);

        protected override void OnInteract(PlayerInteractor interactor)
        {
            used = true;
            EventBus.ShowMessage(message);
            onInteracted?.Invoke();
            Publish(ScenarioEventType.Interacted);
        }
    }
}
