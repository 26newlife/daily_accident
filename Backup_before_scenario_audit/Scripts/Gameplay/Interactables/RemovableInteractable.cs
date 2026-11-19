using GameCore;
using UnityEngine;
using UnityEngine.Events;

namespace GameGameplay
{
    /// <summary>
    /// 치우거나 뽑을 수 있는 오브젝트 (멀티탭 플러그, 종이/옷가지 등). 동작하면 오브젝트가 사라지고(파괴 처리)
    /// Interacted 이벤트(State = true)를 발행한다. holdSeconds 를 주면 길게 눌러야 한다.
    /// </summary>
    public class RemovableInteractable : InteractableObject
    {
        [SerializeField] private string message;
        [SerializeField, Tooltip("true 면 Destroy, false 면 SetActive(false)")] private bool destroyOnRemove;
        [SerializeField] private UnityEvent onRemoved;

        public bool IsRemoved { get; private set; }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            IsRemoved = true;
            EventBus.ShowMessage(message);
            onRemoved?.Invoke();
            Publish(ScenarioEventType.Interacted, true);
            if (destroyOnRemove) Destroy(gameObject); else gameObject.SetActive(false);
        }
    }
}
