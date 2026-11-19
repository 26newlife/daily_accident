#if UNITY_EDITOR || DEVELOPMENT_BUILD
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 3 전용 디버그 패널 항목 (F10): 손잡이 뜨거움/정상, 계단 연기, 루트 A~D 강제, NPC 상태, 엘리베이터 시나리오 A/B 강제, 119.
    /// Act 점프 시 폭발·정전·불·플래그 같은 선행 조건을 채운다. S03Bootstrap 이 등록한다.
    /// </summary>
    public class S03DebugExtension : IDebugExtension
    {
        private bool handleHot = true, stairBlocked, earlyDoor;

        public void OnBeforeJump(int act)
        {
            float now = GameClock.Instance != null ? GameClock.Instance.GameSeconds : 0f;
            if (act >= 2)
            {
                if (GasLeakController.Instance != null) GasLeakController.Instance.StopLeak();
                if (HazardManager.Instance != null) HazardManager.Instance.SetGas(0f);
                ScenarioFlags.Set(ElevatorHazardSystem.FireModeFlag, true);
                LightGroup.SetAllOn(false);
                LightGroup exit = LightGroup.Find("exit");
                if (exit != null) exit.SetOn(true);
                if (ScoreSystem.Instance != null && !ScoreSystem.Instance.HasRecord(EvacKeys.RecExplosionTime)) ScoreSystem.Instance.Record(EvacKeys.RecExplosionTime, now);
            }
            if (act >= 3)
            {
                ScenarioFlags.Set("act2_handle_checked", true);
                ScenarioFlags.Set(EvacKeys.FlagHandleHot, handleHot);
                SetHandle(handleHot);
                foreach (string id in new[] { "fire_curtain", "fire_pan" }) { FireSource f = FindFire(id); if (f != null && !f.IsLit) f.Ignite(); }
            }
            if (act >= 4)
            {
                ScenarioFlags.Set(EvacKeys.FlagInCorridor, !handleHot);
                ScenarioFlags.Set(EvacKeys.FlagStairDownBlocked, stairBlocked);
                ScenarioFlags.Set(EvacKeys.FlagEarlyFireDoor, earlyDoor);
                ScenarioFlags.Set(EvacKeys.FlagFireDoorManaged, earlyDoor);
            }
        }

        /// <summary>스모크 테스트/외부 호출용: 손잡이·계단 상태를 지정해 점프</summary>
        public void JumpToAct(int act, bool hot, bool blocked, bool early)
        {
            handleHot = hot; stairBlocked = blocked; earlyDoor = early;
            DebugPanel panel = Object.FindFirstObjectByType<DebugPanel>();
            if (panel != null) panel.JumpToAct(act);
        }

        public string StatusLine()
        {
            var sb = new System.Text.StringBuilder();
            Act4_Evacuation a4 = Object.FindFirstObjectByType<Act4_Evacuation>();
            sb.AppendLine($"Route={ScenarioFlags.GetText(EvacKeys.FlagRoute, "-")}  handleHot={ScenarioFlags.GetBool(EvacKeys.FlagHandleHot)}  stairBlocked={ScenarioFlags.GetBool(EvacKeys.FlagStairDownBlocked)}  early={ScenarioFlags.GetBool(EvacKeys.FlagEarlyFireDoor)}");
            sb.AppendLine($"Options: {(a4 != null ? a4.Options.ToString() : RouteResolver.Evaluate().ToString())}");
            sb.Append($"Grandmother={(GrandmotherNpc.Instance != null ? GrandmotherNpc.Instance.State.ToString() : "-")}  YoungMan={(YoungManNpc.Instance != null ? YoungManNpc.Instance.State.ToString() : "-")}");
            return sb.ToString();
        }

        public void OnPanelGUI(GUIStyle small)
        {
            GUILayout.Label("S03: handle / stairwell state");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Handle HOT")) { handleHot = true; ScenarioFlags.Set(EvacKeys.FlagHandleHot, true); SetHandle(true); }
            if (GUILayout.Button("Handle NORMAL")) { handleHot = false; ScenarioFlags.Set(EvacKeys.FlagHandleHot, false); SetHandle(false); }
            GUILayout.EndHorizontal();
            stairBlocked = GUILayout.Toggle(stairBlocked, "stairwell_down_blocked (next jump)");
            earlyDoor = GUILayout.Toggle(earlyDoor, "early fire-door success (next jump)");
            if (GUILayout.Button("Apply stair flags now")) { ScenarioFlags.Set(EvacKeys.FlagStairDownBlocked, stairBlocked); ScenarioFlags.Set(EvacKeys.FlagEarlyFireDoor, earlyDoor); }

            GUILayout.Label("Force route (Act 4)");
            GUILayout.BeginHorizontal();
            foreach (Act4_Evacuation.Route r in new[] { Act4_Evacuation.Route.A, Act4_Evacuation.Route.B, Act4_Evacuation.Route.C, Act4_Evacuation.Route.D })
                if (GUILayout.Button(r.ToString())) ForceRoute(r);
            GUILayout.EndHorizontal();

            GUILayout.Label("NPC state");
            GrandmotherNpc g = GrandmotherNpc.Instance;
            if (g != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Grandma: Follow")) g.ForceState(GrandmaState.FollowingPlayer);
                if (GUILayout.Button("Grandma: Panic")) g.ForceState(GrandmaState.Panic);
                if (GUILayout.Button("Pressing")) g.ForceState(GrandmaState.PressingElevator);
                if (GUILayout.Button("Fallen")) g.ForceState(GrandmaState.Fallen);
                GUILayout.EndHorizontal();
            }
            YoungManNpc y = YoungManNpc.Instance;
            if (y != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("YoungMan: Follow")) y.ForceState(YoungManState.Following);
                if (GUILayout.Button("YoungMan: Run")) y.ForceState(YoungManState.Running);
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Elevator (fire mode)");
            GUILayout.BeginHorizontal();
            ElevatorHazardSystem hz = ElevatorHazardSystem.Instance;
            ElevatorController ec = ElevatorController.Instance;
            if (GUILayout.Button("Scenario A") && hz != null) hz.DebugForceScenario(ElevatorScenario.A);
            if (GUILayout.Button("Scenario B") && hz != null) hz.DebugForceScenario(ElevatorScenario.B);
            GUILayout.Label(ec != null ? $"{ec.State} / {(hz != null ? hz.Scenario.ToString() : "-")} / F{ec.CurrentFloor}" : "-", small);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("119 correct") && Call119Controller.Instance != null) Call119Controller.Instance.Report(true);
            if (GUILayout.Button("Trigger ending (rooftop)")) EvacDirector.Ending(EvacKeys.EndingRooftop);
            GUILayout.EndHorizontal();
        }

        private static void ForceRoute(Act4_Evacuation.Route r)
        {
            ScenarioStateMachine sm = ScenarioStateMachine.Instance;
            if (sm == null) return;
            DebugPanel panel = Object.FindFirstObjectByType<DebugPanel>();
            if (sm.CurrentIndex != 3 && panel != null) panel.JumpToAct(4);
            Act4_Evacuation a4 = Object.FindFirstObjectByType<Act4_Evacuation>();
            if (a4 != null) a4.DebugForceRoute(r);
        }

        private static FireSource FindFire(string id)
        {
            foreach (FireSource f in FireSource.All) if (f != null && f.Id == id) return f;
            return null;
        }

        private static void SetHandle(bool hot)
        {
            FireSource f = FindFire("fire_corridor");
            if (hot && f != null && !f.IsLit) f.Ignite();
            else if (!hot && f != null && f.IsLit) f.Extinguish();
        }
    }
}
#endif
