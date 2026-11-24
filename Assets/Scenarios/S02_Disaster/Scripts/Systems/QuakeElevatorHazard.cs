using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 위험 (IElevatorHazard): 지진이 시작된 뒤 엘리베이터를 타면 갇힌다 (원본 ElevatorCall 의 규칙).
    /// 호출만 해도 "엘리베이터 대신 계단" 수칙은 어긴 것으로 기록한다. 지진 전에는 정상 작동.
    /// </summary>
    public class QuakeElevatorHazard : MonoBehaviour, IElevatorHazard
    {
        public bool Dangerous => QuakeTimeline.Instance != null && QuakeTimeline.Instance.Phase >= QuakePhase.Foreshock;

        private ElevatorController ctl;

        public void Register()
        {
            // ElevatorController.Instance 는 판 정리(RunCleanup) 순서에 따라 비어 있을 수 있어 직접 찾는다 (S03 ElevatorHazardSystem 과 같은 방식)
            ctl = FindFirstObjectByType<ElevatorController>(FindObjectsInactive.Include);
            if (ctl != null) ctl.Hazard = this;
            else Debug.LogWarning("[Quake] ElevatorController 를 찾지 못해 엘리베이터 위험을 등록하지 못했습니다.");
        }

        public void Unregister() { if (ctl != null && ReferenceEquals(ctl.Hazard, this)) ctl.Hazard = null; }
        private void OnDestroy() => Unregister();

        public void OnCalled(ElevatorController c)
        {
            if (!Dangerous) return;
            if (QuakeRecord.Instance != null) QuakeRecord.Instance.ElevatorCalled = true;
        }

        public bool OnArrived(ElevatorController c) => false;

        public bool OnBoardAttempt(ElevatorController c)
        {
            if (!Dangerous) return false;
            if (QuakeRecord.Instance != null) QuakeRecord.Instance.ElevatorCalled = true;
            ElevatorController.PlayerShake(0.15f, 1.2f);
            EventBus.ShowSubtitle("(쿵! 엘리베이터가 덜컹 멈춰 섰다... 불이 꺼지고 문이 열리지 않는다.)", 4f);
            if (QuakeOutcome.Instance != null) QuakeOutcome.Instance.Kill(DeathReason.QuakeElevatorTrapped);
            return true;
        }

        public void OnBoarded(ElevatorController c) { }
        public void OnMoveStarted(ElevatorController c, float tripSeconds) { }
        public bool OnTripEnd(ElevatorController c) => false;
    }
}
