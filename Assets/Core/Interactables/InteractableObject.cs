using System;
using System.Collections.Generic;
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
        /// <summary>
        /// 시나리오가 집의 중립 오브젝트 동작을 바꾸는 오버라이드 (HouseController.ReplaceInteractionBehavior).
        /// 지정하지 않은 항목은 오브젝트의 기본 동작을 그대로 쓴다.
        /// </summary>
        public sealed class InteractionOverride
        {
            public Action<PlayerInteractor> onInteract;
            public Func<string> prompt;
            public Func<bool> canInteract;
            public Func<float> hold;
        }

        private static readonly List<InteractableObject> registry = new List<InteractableObject>();
        public static IReadOnlyList<InteractableObject> Registry => registry;

        /// <summary>id 로 활성 상호작용 오브젝트 찾기 (비활성은 제외)</summary>
        public static InteractableObject FindById(string id)
        {
            foreach (InteractableObject i in registry) if (i != null && i.id == id) return i;
            return null;
        }

        protected virtual void OnEnable() { if (!registry.Contains(this)) registry.Add(this); }
        protected virtual void OnDisable() { registry.Remove(this); }

        public InteractionOverride Override { get; set; }

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

        /// <summary>HUD/상호작용 입력이 쓰는 최종 프롬프트 (오버라이드 우선)</summary>
        public string DisplayPrompt() => Override != null && Override.prompt != null ? Override.prompt() : GetPrompt();
        /// <summary>최종 길게 누르기 시간 (오버라이드 우선)</summary>
        public float EffectiveHold => Override != null && Override.hold != null ? Override.hold() : HoldSeconds;
        /// <summary>오버라이드까지 반영한 상호작용 가능 여부</summary>
        public bool CanInteractNow(PlayerInteractor interactor) => (Override == null || Override.canInteract == null || Override.canInteract()) && CanInteract(interactor);

        public void TryInteract(PlayerInteractor interactor)
        {
            if (!CanInteractNow(interactor)) return;
            if (Override != null && Override.onInteract != null)
            {
                Override.onInteract(interactor);   // 시나리오가 동작을 대신 정의 (이벤트는 그대로 발행)
                Publish(ScenarioEventType.Interacted);
                return;
            }
            OnInteract(interactor);
        }

        protected abstract void OnInteract(PlayerInteractor interactor);

        /// <summary>이 오브젝트의 id 로 ScenarioEvent 발행</summary>
        protected void Publish(ScenarioEventType type, bool state = false) => EventBus.Raise(type, id, state);

        protected void SetPromptText(string value) => prompt = value;
    }
}
