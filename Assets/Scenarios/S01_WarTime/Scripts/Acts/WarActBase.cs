using System;
using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>시나리오 1 Act 공통 도우미: 상태/설정 접근, 안전한 타이머(Act 가 끝나면 콜백 무시), 목표 처리</summary>
    public abstract class WarActBase : ActBase
    {
        protected WarState War => WarState.Instance;
        protected WarConfig Cfg => WarState.Instance != null ? WarState.Instance.Config : null;
        protected static float Now => WarTime.Now;

        /// <summary>지금부터 seconds 뒤에 실행. Act 가 이미 끝났거나 게임이 끝났으면 무시</summary>
        protected void At(float seconds, Action action, string label = null)
        {
            int actIndex = Machine != null ? Machine.CurrentIndex : -1;
            WarTime.After(seconds, () =>
            {
                if (!IsActive || GameFlow.IsEnded) return;
                if (Machine != null && Machine.CurrentIndex != actIndex) return;
                action();
            }, label);
        }

        protected static void AddObjective(ObjectiveData d) { if (d != null && ObjectiveSystem.Instance != null) ObjectiveSystem.Instance.Add(d); }
        protected static void CompleteObjective(ObjectiveData d)
        {
            if (d == null || ObjectiveSystem.Instance == null) return;
            ObjectiveSystem.Entry e = ObjectiveSystem.Instance.Find(d.Id);
            if (e != null && e.State == ObjectiveState.Active) ObjectiveSystem.Instance.Complete(d.Id);
        }

        protected static void Beat(GameTimingConfig.TimedBeat b)
        {
            switch (b.kind)
            {
                case GameTimingConfig.BeatKind.Scent: EventBus.ShowScent(b.text); break;
                case GameTimingConfig.BeatKind.Message: EventBus.ShowMessage(b.text); break;
                default: EventBus.ShowSubtitle(b.text, b.seconds); break;
            }
        }

        protected static void ApplyPreset(EnvironmentPreset p)
        {
            if (p != null && HouseServices.Environment != null) HouseServices.Environment.ApplyEnvironmentPreset(p);
            if (p != null && WarOutbreak.Instance != null) WarOutbreak.Instance.MatchSkyToPreset(p);   // 하늘 재질도 같은 분위기로
            if (HouseController.Instance != null) HouseController.Instance.RefreshBlackout();           // 정전 중이면 프리셋이 밝혀 놓은 주변광을 다시 어둡게
        }

        protected static bool PlayerIsOutside(float outsideZ)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();
            return p != null && p.transform.position.z < outsideZ;
        }
    }
}
