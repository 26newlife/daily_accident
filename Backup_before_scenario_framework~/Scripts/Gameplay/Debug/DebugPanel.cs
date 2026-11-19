using GameCore;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
#endif

namespace GameGameplay
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>개발용 전역 플래그 (에디터/개발 빌드 전용)</summary>
    public static class DebugFlags
    {
        /// <summary>true 면 사망 판정을 무시한다 (테스트용). 호흡기 게이지·화상도 한계에서 멈춘다</summary>
        public static bool Invulnerable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Invulnerable = false;
    }
#endif

    /// <summary>
    /// 개발용 디버그 패널 (F10). 에디터/개발 빌드(UNITY_EDITOR || DEVELOPMENT_BUILD)에서만 동작하고,
    /// 릴리스 빌드에서는 본문이 컴파일되지 않아 빈 컴포넌트가 된다 (씬의 스크립트 참조가 깨지지 않는다).
    ///   Act 점프(1~4) / 루트 강제(A~D) / 아이템 지급 / 손잡이·계단 상태 강제 / 화재·연기·가스 슬라이더 /
    ///   호흡기 회복 / NPC 상태 강제 / 무적 토글 / 활성 타이머·이벤트 구독 수 표시(재시작 누수 점검).
    /// 패널이 열려 있는 동안은 커서가 풀리고 이동·시점이 멈춘다 (게임 시간은 계속 흐른다).
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool open;
        private Vector2 scroll;
        private bool handleHot = true, stairBlocked, earlyDoor;
        private float fireSize = 0.5f, smoke, gas;
        private GUIStyle small;
        private int baselineSubs = -1, baselineTimers = -1;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame) SetOpen(!open);
            if (baselineSubs < 0 && Time.timeSinceLevelLoad > 1f)
            {
                baselineSubs = EventBus.SubscriptionCount();
                baselineTimers = GameClock.Instance != null ? GameClock.Instance.ActiveTimerCount : 0;
            }
        }

        private void SetOpen(bool value)
        {
            open = value;
            GameFlow.SetModalOpen(value);
        }

        private void OnDestroy() { if (open) GameFlow.SetModalOpen(false); }

        private void OnGUI()
        {
            if (!open) return;
            if (small == null) small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUILayout.BeginArea(new Rect(12, 12, 400, Screen.height - 24), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("DEBUG PANEL (F10)  - dev only");

            Header();
            ActJump();
            RouteAndState();
            Items();
            Sliders();
            Npcs();
            Misc();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ───────── 상태 표시 ─────────
        private void Header()
        {
            var sm = ScenarioStateMachine.Instance;
            GameClock clock = GameClock.Instance;
            int subs = EventBus.SubscriptionCount();
            int timers = clock != null ? clock.ActiveTimerCount : 0;
            var sb = new StringBuilder();
            sb.AppendLine($"Run #{GameRunContext.RunId}  t={(clock != null ? clock.GameSeconds : 0f):0.0}s  Act={(sm != null ? sm.CurrentIndex + 1 : 0)}  step={(sm != null && sm.Current != null ? sm.Current.CurrentStepId : "-")}");
            sb.AppendLine($"Timers active={timers} (baseline {baselineTimers})   EventBus subs={subs} (baseline {baselineSubs})   RunCleanup pending={RunCleanup.PendingCount}");
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
            sb.AppendLine($"Route={ScenarioFlags.GetText(EvacKeys.FlagRoute, "-")}  handleHot={ScenarioFlags.GetBool(EvacKeys.FlagHandleHot)}  stairBlocked={ScenarioFlags.GetBool(EvacKeys.FlagStairDownBlocked)}  early={ScenarioFlags.GetBool(EvacKeys.FlagEarlyFireDoor)}");
            sb.AppendLine($"Options: {(a4 != null ? a4.Options.ToString() : RouteResolver.Evaluate().ToString())}");
            PlayerVitals v = FindFirstObjectByType<PlayerVitals>();
            HazardManager hz = HazardManager.Instance;
            if (v != null) sb.AppendLine($"Breath={v.Value:0}/{v.MaxValue:0}  Burn={v.Burn:0}  Smoke(player)={(hz != null ? hz.PlayerSmoke : 0f):0.00}  Gas={(hz != null ? hz.PlayerGas : 0f):0.00}");
            GrandmotherNpc g = GrandmotherNpc.Instance;
            YoungManNpc y = YoungManNpc.Instance;
            sb.AppendLine($"Grandmother={(g != null ? g.State.ToString() : "-")}  YoungMan={(y != null ? y.State.ToString() : "-")}");
            GUILayout.Label(sb.ToString(), small);
        }

        // ───────── Act 점프 ─────────
        private void ActJump()
        {
            GUILayout.Label("Act jump");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 4; i++) if (GUILayout.Button($"Act {i}")) JumpToAct(i);
            GUILayout.EndHorizontal();
        }

        /// <summary>스모크 테스트/외부 호출용: 손잡이·계단 상태를 지정해 점프</summary>
        public void JumpToAct(int act, bool hot, bool blocked, bool early)
        {
            handleHot = hot; stairBlocked = blocked; earlyDoor = early;
            JumpToAct(act);
        }

        public void JumpToAct(int act)
        {
            ScenarioStateMachine sm = ScenarioStateMachine.Instance;
            if (sm == null) return;
            if (GameClock.Instance != null) GameClock.Instance.ClearSchedule();   // 이전 Act 의 예약 타이머가 뒤늦게 Act 를 건너뛰지 않게
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
            sm.StartAct(act - 1);
            Debug.Log($"[Debug] jumped to Act {act} (handleHot={handleHot}, stairBlocked={stairBlocked}, early={earlyDoor})");
        }

        // ───────── 루트 / 손잡이 / 계단 ─────────
        private void RouteAndState()
        {
            GUILayout.Label("Handle / stairwell state");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Handle HOT")) { handleHot = true; ScenarioFlags.Set(EvacKeys.FlagHandleHot, true); SetHandle(true); }
            if (GUILayout.Button("Handle NORMAL")) { handleHot = false; ScenarioFlags.Set(EvacKeys.FlagHandleHot, false); SetHandle(false); }
            GUILayout.EndHorizontal();
            stairBlocked = GUILayout.Toggle(stairBlocked, "stairwell_down_blocked (next jump)");
            earlyDoor = GUILayout.Toggle(earlyDoor, "early fire-door success (next jump)");
            if (GUILayout.Button("Apply stair flags now"))
            {
                ScenarioFlags.Set(EvacKeys.FlagStairDownBlocked, stairBlocked);
                ScenarioFlags.Set(EvacKeys.FlagEarlyFireDoor, earlyDoor);
            }

            GUILayout.Label("Force route (Act 4)");
            GUILayout.BeginHorizontal();
            foreach (Act4_Evacuation.Route r in new[] { Act4_Evacuation.Route.A, Act4_Evacuation.Route.B, Act4_Evacuation.Route.C, Act4_Evacuation.Route.D })
                if (GUILayout.Button(r.ToString())) ForceRoute(r);
            GUILayout.EndHorizontal();
        }

        private void ForceRoute(Act4_Evacuation.Route r)
        {
            ScenarioStateMachine sm = ScenarioStateMachine.Instance;
            if (sm == null) return;
            if (sm.CurrentIndex != 3) JumpToAct(4);
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
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

        // ───────── 아이템 ─────────
        private void Items()
        {
            GUILayout.Label("Give item");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wet towel")) Give("wet_towel");
            if (GUILayout.Button("Towel")) Give("towel");
            if (GUILayout.Button("Extinguisher")) Give("extinguisher");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Duct tape")) Give("duct_tape");
            if (GUILayout.Button("Roof key")) Give("roof_key");
            if (GUILayout.Button("Hammer")) Give("hammer");
            if (GUILayout.Button("Water")) Give("water_bottle");
            GUILayout.EndHorizontal();
        }

        public static void GiveItem(string itemId) => Give(itemId);

        private static void Give(string itemId)
        {
            InventorySystem inv = InventorySystem.Instance;
            if (inv == null) return;
            foreach (ItemData item in Resources.FindObjectsOfTypeAll<ItemData>())
                if (item.Id == itemId) { if (!inv.TryAdd(item)) EventBus.ShowMessage("[Debug] inventory full"); return; }
            Debug.LogWarning($"[Debug] ItemData '{itemId}' is not loaded in this scene.");
        }

        // ───────── 슬라이더 ─────────
        private void Sliders()
        {
            GUILayout.Label("Environment");
            float f = Slider("Fire size", fireSize, 0f, 1f);
            if (!Mathf.Approximately(f, fireSize)) { fireSize = f; SetFireSize(f); }
            float s = Slider("Smoke (global)", smoke, 0f, 1f);
            if (!Mathf.Approximately(s, smoke)) { smoke = s; if (HazardManager.Instance != null) HazardManager.Instance.SetSmoke(s); }
            float g = Slider("Gas", gas, 0f, 1f);
            if (!Mathf.Approximately(g, gas)) { gas = g; if (HazardManager.Instance != null) HazardManager.Instance.SetGas(g); }
        }

        private static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:0.00}", GUILayout.Width(130));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return v;
        }

        private static void SetFireSize(float size)
        {
            foreach (FireSource f in FireSource.All)
            {
                if (f == null || !f.CountsForApartment) continue;
                if (size <= 0.01f) { if (f.IsLit) f.Extinguish(); continue; }
                if (!f.IsLit) f.Ignite(size);
                f.DebugSetIntensity(size);
            }
        }

        // ───────── NPC ─────────
        private void Npcs()
        {
            GUILayout.Label("NPC state");
            GrandmotherNpc g = GrandmotherNpc.Instance;
            if (g != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Grandma: Follow")) g.ForceState(GrandmaState.FollowingPlayer);
                if (GUILayout.Button("Grandma: Leave (panic)")) g.ForceState(GrandmaState.Panic);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Grandma: Pressing")) g.ForceState(GrandmaState.PressingElevator);
                if (GUILayout.Button("Grandma: Fallen")) g.ForceState(GrandmaState.Fallen);
                GUILayout.EndHorizontal();
            }
            YoungManNpc y = YoungManNpc.Instance;
            if (y != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("YoungMan: Follow")) y.ForceState(YoungManState.Following);
                if (GUILayout.Button("YoungMan: Run (leave)")) y.ForceState(YoungManState.Running);
                GUILayout.EndHorizontal();
            }
        }

        // ───────── 기타 ─────────
        private void Misc()
        {
            GUILayout.Label("Elevator (fire mode)");
            GUILayout.BeginHorizontal();
            ElevatorController ec = ElevatorController.Instance;
            if (GUILayout.Button("Scenario A") && ec != null) ec.DebugForceScenario(ElevatorScenario.A);
            if (GUILayout.Button("Scenario B") && ec != null) ec.DebugForceScenario(ElevatorScenario.B);
            GUILayout.Label(ec != null ? $"{ec.State} / {ec.Scenario} / F{ec.CurrentFloor}" : "-", small);
            GUILayout.EndHorizontal();
            GUILayout.Label("Player");
            if (GUILayout.Button("Refill breath / clear burn"))
            {
                PlayerVitals v = FindFirstObjectByType<PlayerVitals>();
                if (v != null) { v.Modify(v.MaxValue); v.DebugClearBurn(); }
            }
            DebugFlags.Invulnerable = GUILayout.Toggle(DebugFlags.Invulnerable, "Invulnerable (test only)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("119 correct")) { if (Call119Controller.Instance != null) Call119Controller.Instance.Report(true); }
            if (GUILayout.Button("Clear collection")) EndingCollection.ClearAll();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Trigger ending (rooftop)")) EvacDirector.Ending(EvacKeys.EndingRooftop);
        }
#endif
    }
}
