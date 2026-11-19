using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 1~4 진행을 관리하는 상태머신. 한 번에 하나의 Act 만 활성화하고,
    /// EventBus 의 시나리오 이벤트를 현재 Act 로 전달한다.
    /// Act 를 추가하려면 ActBase 를 상속한 컴포넌트를 만들어 acts 배열에 순서대로 넣는다.
    /// </summary>
    public class ScenarioStateMachine : MonoBehaviour
    {
        [SerializeField] private ActBase[] acts;
        [SerializeField] private bool autoStart = true;
        [SerializeField, Min(0)] private int startActIndex;

        public static ScenarioStateMachine Instance { get; private set; }

        public ActBase Current { get; private set; }
        public int CurrentIndex { get; private set; } = -1;
        public int ActCount => acts != null ? acts.Length : 0;

        /// <summary>Act 가 바뀔 때 (이전, 새)</summary>
        public event Action<ActBase, ActBase> ActChanged;

        private void Awake() { Instance = this; }
        private void OnEnable() => EventBus.ScenarioEventRaised += OnScenarioEvent;
        private void OnDisable() => EventBus.ScenarioEventRaised -= OnScenarioEvent;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (autoStart) StartAct(startActIndex);
        }

        public void StartAct(int index)
        {
            if (acts == null || index < 0 || index >= acts.Length || acts[index] == null)
            {
                Debug.LogWarning($"[ScenarioStateMachine] 시작할 수 없는 Act 인덱스: {index}", this);
                return;
            }

            ActBase prev = Current;
            if (prev != null) prev.Exit();

            CurrentIndex = index;
            Current = acts[index];
            Current.Enter(this);
            ActChanged?.Invoke(prev, Current);
            EventBus.RaiseActChanged(index + 1);
        }

        /// <summary>다음 Act 로. 마지막이면 false</summary>
        public bool NextAct()
        {
            if (CurrentIndex + 1 >= ActCount) return false;
            StartAct(CurrentIndex + 1);
            return true;
        }

        private void OnScenarioEvent(ScenarioEvent e)
        {
            if (Current != null) Current.Dispatch(e);
        }
    }
}
