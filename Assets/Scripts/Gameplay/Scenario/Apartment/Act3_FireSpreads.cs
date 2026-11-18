using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 3 — 번지는 화재 (연쇄 화재, 대피 경로를 판단한다).
    /// 지금은 빈 껍데기.
    /// </summary>
    public class Act3_FireSpreads : ActBase
    {
        protected override void OnEnter() { Debug.Log($"[Act] 시작: {ActTitle}", this); }
        protected override void OnExit() { }
        protected override void OnScenarioEvent(ScenarioEvent e) { }
    }
}
