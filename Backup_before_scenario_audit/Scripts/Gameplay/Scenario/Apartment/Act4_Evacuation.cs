using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 4 — 대피 (옥상 / 경량칸막이 / 대피공간 농성 / 1층 하강 중 탈출 루트를 선택한다).
    /// 지금은 빈 껍데기.
    /// </summary>
    public class Act4_Evacuation : ActBase
    {
        protected override void OnEnter() { Debug.Log($"[Act] 시작: {ActTitle}", this); }
        protected override void OnExit() { }
        protected override void OnScenarioEvent(ScenarioEvent e) { }
    }
}
