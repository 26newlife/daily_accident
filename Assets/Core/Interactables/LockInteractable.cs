using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 창문/문의 개별 잠금장치. 대상 ToggleInteractable 의 Locked 상태를 바꾼다. (창문마다 각각 하나씩 배치)
    /// </summary>
    public class LockInteractable : InteractableObject
    {
        [SerializeField] private ToggleInteractable target;
        [SerializeField] private bool startLocked = true;
        [SerializeField] private string promptWhenLocked = "잠금 풀기";
        [SerializeField] private string promptWhenUnlocked = "잠그기";

        public bool IsLocked { get; private set; }

        private void Start() => SetLocked(startLocked, silent: true);

        public override string GetPrompt() => IsLocked ? promptWhenLocked : promptWhenUnlocked;

        protected override void OnInteract(PlayerInteractor interactor) => SetLocked(!IsLocked);

        public void SetLocked(bool locked, bool silent = false)
        {
            IsLocked = locked;
            if (target != null) target.SetLocked(locked);
            if (!silent) Publish(ScenarioEventType.LockChanged, locked);
        }
    }
}
