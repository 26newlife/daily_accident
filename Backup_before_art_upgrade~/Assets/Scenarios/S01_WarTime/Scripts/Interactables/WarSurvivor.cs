using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 은신처에 합류한 생존자(군인/아이/이웃). 평소에는 숨겨져 있다가 합류하면 나타난다.
    /// Act 4 에서 설득에 실패하면 문 쪽으로 걸어 나가 사라진다 (이동은 deltaTime 보간이며 게임플레이 타이밍과 무관한 연출).
    /// </summary>
    public class WarSurvivor : InteractableObject
    {
        [SerializeField, Tooltip("대화 시 표시 이름")] private string displayName = "생존자";
        public enum State { Hidden, Inside, Leaving, Gone }

        public static event Action<WarSurvivor> Talked;
        public State Current { get; private set; } = State.Hidden;
        public string DisplayName => displayName;

        private Vector3 homePos;
        private Vector3 target;
        private float speed;
        private Action arrived;

        private void Awake() { homePos = transform.position; }

        public override string GetPrompt() => $"{displayName}에게 말 걸기";
        public override bool CanInteract(PlayerInteractor interactor) => base.CanInteract(interactor) && Current == State.Inside;

        public void Appear()
        {
            Current = State.Inside;
            gameObject.SetActive(true);
            transform.position = homePos;
        }

        public void WalkTo(Vector3 worldTarget, float walkSpeed, Action onArrived)
        {
            if (Current != State.Inside) return;
            Current = State.Leaving; target = worldTarget; speed = walkSpeed; arrived = onArrived;
        }

        public void Stop() { if (Current == State.Leaving) { Current = State.Inside; arrived = null; } }

        public void Vanish() { Current = State.Gone; gameObject.SetActive(false); }

        private void Update()
        {
            if (Current != State.Leaving) return;
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.04f) { Action a = arrived; arrived = null; Vanish(); a?.Invoke(); return; }
            transform.position += d.normalized * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 8f * Time.deltaTime);
        }

        protected override void OnInteract(PlayerInteractor interactor) { Talked?.Invoke(this); Publish(ScenarioEventType.Interacted); }
    }
}
