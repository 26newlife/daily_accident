using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 상호작용 가능한 오브젝트의 추상 베이스. 고유 id 를 가지며, 상호작용하면 ScenarioEvent 를 EventBus 로 발행한다.
    /// 외형(그레이박스)은 자식 오브젝트로 분리해 두므로 나중에 모델로 교체해도 이 컴포넌트는 그대로 쓴다.
    /// 콜라이더는 이 오브젝트 또는 자식에 있으면 된다 (PlayerInteractor 가 부모 방향으로 찾는다).
    /// </summary>
    public abstract class InteractableObject : MonoBehaviour
    {
        [Header("상호작용")]
        [SerializeField] private string id = "interactable_id";
        [Tooltip("HUD 에 [키] 와 함께 표시되는 문구 (예: 가스 밸브 잠그기)")]
        [SerializeField] private string prompt = "상호작용";
        [SerializeField] private bool interactable = true;
        [Tooltip("0 = 즉시, 0보다 크면 이 시간(초)만큼 상호작용 키를 길게 눌러야 한다")]
        [SerializeField, Min(0f)] private float holdSeconds;

        public string Id => id;
        public bool Interactable { get => interactable; set => interactable = value; }

        /// <summary>길게 눌러야 하는 시간(초). 0 이면 즉시 동작</summary>
        public virtual float HoldSeconds => GameTimingConfig.ResolveHold(id, holdSeconds);   // GameTimingConfig.holdOverrides 가 우선

        /// <summary>현재 상태에 맞는 프롬프트 문구</summary>
        public virtual string GetPrompt() => prompt;

        /// <summary>상호작용 가능한가 (false 면 프롬프트도 숨김)</summary>
        public virtual bool CanInteract(PlayerInteractor interactor) => interactable && isActiveAndEnabled;

        public void TryInteract(PlayerInteractor interactor)
        {
            if (CanInteract(interactor)) OnInteract(interactor);
        }

        protected abstract void OnInteract(PlayerInteractor interactor);

        /// <summary>이 오브젝트의 id 로 ScenarioEvent 발행</summary>
        protected void Publish(ScenarioEventType type, bool state = false) => EventBus.Raise(type, id, state);

        protected void SetPromptText(string value) => prompt = value;
    }
}
