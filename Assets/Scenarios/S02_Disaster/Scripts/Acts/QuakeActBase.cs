using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2 Act 공통: 지진 타임라인 단계에 맞춰 목표를 띄우고, 정해진 단계가 되면 다음 Act 로 넘어간다.
    /// 판정(생존·사망)은 QuakeOutcome 이 하고, Act 는 진행 표시와 단계별 연출만 맡는다.
    /// 목표 문구는 정답을 직접 알려주지 않는다 (원본 설계: 상황 묘사 위주).
    /// </summary>
    public abstract class QuakeActBase : ActBase
    {
        [SerializeField] protected ObjectiveData objective;
        [SerializeField, Tooltip("이 단계가 되면 Act 를 끝낸다")] private QuakePhase endsAt = QuakePhase.Tsunami;
        [SerializeField, Tooltip("Act 를 끝낼 때 목표를 완료로 표시한다")] private bool completeObjectiveOnEnd = true;

        protected QuakeTimeline Timeline => QuakeTimeline.Instance;

        protected override void OnEnter()
        {
            if (objective != null && ObjectiveSystem.Instance != null && ObjectiveSystem.Instance.Find(objective.Id) == null) ObjectiveSystem.Instance.Add(objective);
            if (Timeline != null) { Timeline.PhaseChanged -= OnPhase; Timeline.PhaseChanged += OnPhase; }
            if (QuakeOutcome.Instance != null) { QuakeOutcome.Instance.Decided -= OnDecided; QuakeOutcome.Instance.Decided += OnDecided; }
            if (Timeline != null && Timeline.Phase >= endsAt) Finish();
        }

        protected override void OnExit()
        {
            if (Timeline != null) Timeline.PhaseChanged -= OnPhase;
            if (QuakeOutcome.Instance != null) QuakeOutcome.Instance.Decided -= OnDecided;
        }

        private void OnDestroy() { OnExit(); }

        private void OnPhase(QuakePhase p)
        {
            if (!IsActive) return;
            OnQuakePhase(p);
            if (p >= endsAt) Finish();
        }

        private void OnDecided(DeathReason cause)
        {
            if (!IsActive || objective == null || ObjectiveSystem.Instance == null) return;
            ObjectiveSystem.Entry e = ObjectiveSystem.Instance.Find(objective.Id);
            if (e == null || e.State != ObjectiveState.Active) return;
            if (cause == DeathReason.Unknown) ObjectiveSystem.Instance.Complete(objective.Id); else ObjectiveSystem.Instance.Fail(objective.Id);
        }

        protected virtual void OnQuakePhase(QuakePhase p) { }

        private void Finish()
        {
            if (completeObjectiveOnEnd && objective != null && ObjectiveSystem.Instance != null)
            {
                ObjectiveSystem.Entry e = ObjectiveSystem.Instance.Find(objective.Id);
                if (e != null && e.State == ObjectiveState.Active) ObjectiveSystem.Instance.Complete(objective.Id);
            }
            CompleteAct();
        }
    }
}
