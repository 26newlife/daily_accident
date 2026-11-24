using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QuakeSurvival.Interaction
{
    /// <summary>
    /// 카메라 정면으로 레이를 쏴서 상호작용 대상을 찾고, F키로 실행한다.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float reach = 2.0f;
        [SerializeField] private LayerMask interactMask = Physics.DefaultRaycastLayers;

        /// <summary>바라보는 대상이 바뀔 때 안내 문구를 전달한다. 대상이 없으면 빈 문자열.</summary>
        public event Action<string> PromptChanged;

        /// <summary>지금 표시 중이어야 할 안내 문구. UI가 다시 켜질 때 동기화용.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        public bool InputEnabled { get; set; } = true;

        private IInteractable current;
        private Collider cachedCollider;
        private IInteractable cachedTarget;

        private void Awake()
        {
            if (viewCamera == null)
            {
                Debug.LogError("[Interactor] viewCamera가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            current = null;
            PublishPrompt(string.Empty);
        }

        private void Update()
        {
            current = InputEnabled ? FindTarget() : null;
            PublishPrompt(current != null ? current.Prompt : string.Empty);

            var keyboard = Keyboard.current;
            if (current == null || keyboard == null) return;
            if (keyboard.fKey.wasPressedThisFrame) current.Interact();
        }

        private IInteractable FindTarget()
        {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, interactMask, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            IInteractable target = ResolveTarget(hit.collider);
            return target != null && target.CanInteract ? target : null;
        }

        private IInteractable ResolveTarget(Collider hitCollider)
        {
            if (hitCollider == cachedCollider) return cachedTarget;
            cachedCollider = hitCollider;
            cachedTarget = hitCollider.GetComponentInParent<IInteractable>();
            return cachedTarget;
        }

        private void PublishPrompt(string prompt)
        {
            if (prompt == CurrentPrompt) return;
            CurrentPrompt = prompt;
            PromptChanged?.Invoke(prompt);
        }
    }
}
