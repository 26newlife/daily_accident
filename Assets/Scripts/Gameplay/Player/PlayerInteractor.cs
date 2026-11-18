using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 화면 중앙 레이캐스트로 상호작용 대상을 찾는다 (기본 2.5m). 대상이 바뀌면 TargetChanged 이벤트를 발행하고,
    /// HUD 가 Current.GetPrompt() 로 "[E] 가스 밸브 잠그기" 같은 프롬프트를 그린다.
    /// HoldSeconds 가 있는 대상은 키를 길게 눌러야 동작하며 진행도는 HoldProgress(0~1)로 HUD 에 표시된다.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.5f)] private float interactDistance = 2.0f;
        [SerializeField] private LayerMask interactMask = ~0;

        private InteractableObject holdTarget;
        private float holdTime;

        public InteractableObject Current { get; private set; }
        public float InteractDistance => interactDistance;
        /// <summary>길게 누르기 진행도 (0~1, 누르는 중이 아니면 0)</summary>
        public float HoldProgress { get; private set; }

        /// <summary>바라보는 대상이 바뀔 때 (null = 대상 없음)</summary>
        public event Action<InteractableObject> TargetChanged;

        private void OnEnable() { if (input != null) input.InteractPressed += OnInteractPressed; }
        private void OnDisable() { if (input != null) input.InteractPressed -= OnInteractPressed; }

        private void Update()
        {
            InteractableObject found = null;
            if (!GameFlow.GameplayBlocked && !GameFlow.CursorFree && viewCamera != null && !PeepholeCameraController.IsActive)
            {
                Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
                {
                    found = hit.collider.GetComponentInParent<InteractableObject>();
                    if (found != null && !found.CanInteract(this)) found = null;
                }
            }

            if (found != Current)
            {
                Current = found;
                TargetChanged?.Invoke(Current);
            }
            UpdateHold();
        }

        private void OnInteractPressed()
        {
            if (GameFlow.GameplayBlocked || GameFlow.CursorFree || Current == null) return;
            if (Current.HoldSeconds > 0f) { holdTarget = Current; holdTime = 0f; }
            else Current.TryInteract(this);
        }

        private void UpdateHold()
        {
            if (holdTarget == null) { HoldProgress = 0f; return; }

            bool cancel = !input.InteractHeld || Current != holdTarget || GameFlow.GameplayBlocked || GameFlow.CursorFree;
            if (cancel) { holdTarget = null; HoldProgress = 0f; return; }

            holdTime += Time.deltaTime;
            HoldProgress = Mathf.Clamp01(holdTime / Mathf.Max(0.01f, holdTarget.HoldSeconds));
            if (holdTime < holdTarget.HoldSeconds) return;

            InteractableObject done = holdTarget;
            holdTarget = null;
            HoldProgress = 0f;
            done.TryInteract(this);   // 다시 하려면 키를 한 번 더 눌러야 한다
        }
    }
}
