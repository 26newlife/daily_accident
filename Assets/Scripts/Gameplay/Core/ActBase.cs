using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오의 한 막(Act). 각 Act는 이 클래스를 상속해 별도 파일로 만든다.
    /// 하위 이벤트(단계)는 string id 로 관리하고, 진행은 ScenarioStateMachine 이 담당한다.
    ///
    /// 구현 방법: OnEnter 에서 목표/위험/시계 예약을 설정 → OnScenarioEvent 에서 상호작용에 반응 →
    ///           끝나면 CompleteAct() 를 호출하면 상태머신이 다음 Act 로 넘긴다.
    /// </summary>
    public abstract class ActBase : MonoBehaviour
    {
        [Header("Act 정보")]
        [SerializeField] private string actTitle = "Act";
        [SerializeField, TextArea(1, 3)] private string actDescription;
        [Tooltip("이 Act 의 하위 단계 id 목록 (진행 순서). 비워두면 단계 없이 동작")]
        [SerializeField] private string[] stepIds;

        protected ScenarioStateMachine Machine { get; private set; }

        public string ActTitle => actTitle;
        public string ActDescription => actDescription;
        public bool IsActive { get; private set; }
        public string CurrentStepId { get; private set; }

        /// <summary>단계가 바뀔 때 (이전 id, 새 id)</summary>
        public event Action<string, string> StepChanged;

        protected virtual void Awake() { enabled = false; }

        internal void Enter(ScenarioStateMachine machine)
        {
            Machine = machine;
            IsActive = true;
            enabled = true;
            CurrentStepId = stepIds != null && stepIds.Length > 0 ? stepIds[0] : null;
            OnEnter();
        }

        internal void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            OnExit();
            enabled = false;
        }

        internal void Dispatch(ScenarioEvent e)
        {
            if (IsActive) OnScenarioEvent(e);
        }

        /// <summary>Act 시작 시 (목표 추가, 위험도 설정, 시계 예약 등)</summary>
        protected virtual void OnEnter() { }
        /// <summary>Act 종료 시 (정리)</summary>
        protected virtual void OnExit() { }
        /// <summary>활성 Act 가 받는 시나리오 이벤트</summary>
        protected virtual void OnScenarioEvent(ScenarioEvent e) { }

        /// <summary>하위 단계로 이동. 목록에 없는 id 도 허용(자유 형식 단계)</summary>
        protected void GoToStep(string stepId)
        {
            string prev = CurrentStepId;
            CurrentStepId = stepId;
            StepChanged?.Invoke(prev, stepId);
        }

        /// <summary>stepIds 목록의 다음 단계로 이동. 마지막이면 false</summary>
        protected bool NextStep()
        {
            if (stepIds == null) return false;
            int i = Array.IndexOf(stepIds, CurrentStepId);
            if (i < 0 || i + 1 >= stepIds.Length) return false;
            GoToStep(stepIds[i + 1]);
            return true;
        }

        /// <summary>이 Act 를 끝내고 다음 Act 로</summary>
        protected void CompleteAct()
        {
            if (Machine != null) Machine.NextAct();
        }
    }
}
