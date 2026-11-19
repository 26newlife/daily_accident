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

    /// <summary>
    /// 시나리오가 디버그 패널(F10)에 항목을 추가하는 확장 지점. 시나리오 어셈블리에서 구현하고
    /// DebugPanel.Register 로 등록한다 (판이 끝나면 자동으로 비워진다).
    /// </summary>
    public interface IDebugExtension
    {
        /// <summary>Act 점프 직전 선행 조건(플래그, 불, 조명 등)을 채운다</summary>
        void OnBeforeJump(int act);
        /// <summary>패널 상단 상태 줄에 덧붙일 문구</summary>
        string StatusLine();
        /// <summary>IMGUI 항목 그리기</summary>
        void OnPanelGUI(GUIStyle small);
    }
#endif

    /// <summary>
    /// 개발용 디버그 패널 (F10, 공용). 에디터/개발 빌드(UNITY_EDITOR || DEVELOPMENT_BUILD)에서만 동작하고,
    /// 릴리스 빌드에서는 본문이 컴파일되지 않아 빈 컴포넌트가 된다 (씬의 스크립트 참조가 깨지지 않는다).
    ///   Act 점프 / 아이템 지급 / 연기·가스·불 슬라이더 / 호흡기 회복 / 무적 / 활성 타이머·이벤트 구독 수(재시작 누수 점검).
    /// 시나리오 전용 항목은 IDebugExtension 으로 추가한다. 패널이 열려 있는 동안은 커서가 풀리고 이동·시점이 멈춘다.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly List<IDebugExtension> extensions = new List<IDebugExtension>();
        public static void Register(IDebugExtension e) { if (e != null && !extensions.Contains(e)) extensions.Add(e); }
        public static void Unregister(IDebugExtension e) { extensions.Remove(e); }

        private bool open;
        private Vector2 scroll;
        private float fireSize = 0.5f, smoke, gas;
        private GUIStyle small;
        private int baselineSubs = -1, baselineTimers = -1;

        private void OnEnable() { RunCleanup.Register(extensions.Clear); }

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

        private void SetOpen(bool value) { open = value; GameFlow.SetModalOpen(value); }
        private void OnDestroy() { if (open) GameFlow.SetModalOpen(false); }

        private void OnGUI()
        {
            if (!open) return;
            if (small == null) small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            GUILayout.BeginArea(new Rect(12, 12, 400, Screen.height - 24), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("DEBUG PANEL (F10)  - dev only");
            Header();
            GUILayout.Label("Act jump");
            GUILayout.BeginHorizontal();
            ScenarioStateMachine sm = ScenarioStateMachine.Instance;
            int n = sm != null ? Mathf.Max(1, sm.ActCount) : 4;
            for (int i = 1; i <= n; i++) if (GUILayout.Button($"Act {i}")) JumpToAct(i);
            GUILayout.EndHorizontal();
            foreach (IDebugExtension e in extensions) e.OnPanelGUI(small);
            Items();
            Sliders();
            Misc();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void Header()
        {
            var sm = ScenarioStateMachine.Instance;
            GameClock clock = GameClock.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"Run #{GameRunContext.RunId}  t={(clock != null ? clock.GameSeconds : 0f):0.0}s  Act={(sm != null ? sm.CurrentIndex + 1 : 0)}  step={(sm != null && sm.Current != null ? sm.Current.CurrentStepId : "-")}");
            sb.AppendLine($"Timers active={(clock != null ? clock.ActiveTimerCount : 0)} (baseline {baselineTimers})   EventBus subs={EventBus.SubscriptionCount()} (baseline {baselineSubs})   RunCleanup pending={RunCleanup.PendingCount}");
            PlayerVitals v = FindFirstObjectByType<PlayerVitals>();
            HazardManager hz = HazardManager.Instance;
            if (v != null) sb.AppendLine($"Breath={v.Value:0}/{v.MaxValue:0}  Burn={v.Burn:0}  Smoke(player)={(hz != null ? hz.PlayerSmoke : 0f):0.00}  Gas={(hz != null ? hz.PlayerGas : 0f):0.00}");
            foreach (IDebugExtension e in extensions) { string s = e.StatusLine(); if (!string.IsNullOrEmpty(s)) sb.AppendLine(s); }
            GUILayout.Label(sb.ToString(), small);
        }

        /// <summary>Act 로 점프. 예약 타이머는 비우고, 등록된 확장이 선행 조건을 채운다</summary>
        public void JumpToAct(int act)
        {
            ScenarioStateMachine sm = ScenarioStateMachine.Instance;
            if (sm == null) return;
            if (GameClock.Instance != null) GameClock.Instance.ClearSchedule();   // 이전 Act 의 예약 타이머가 뒤늦게 Act 를 건너뛰지 않게
            foreach (IDebugExtension e in extensions) e.OnBeforeJump(act);
            sm.StartAct(act - 1);
            Debug.Log($"[Debug] jumped to Act {act}");
        }

        // ───────── 아이템 ─────────
        private void Items()
        {
            GUILayout.Label("Give item (id)");
            GUILayout.BeginHorizontal();
            foreach (string id in new[] { "wet_towel", "towel", "extinguisher", "duct_tape", "hammer", "roof_key", "water_bottle" })
                if (GUILayout.Button(id.Replace('_', ' '))) Give(id);
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

        private void Misc()
        {
            GUILayout.Label("Player");
            if (GUILayout.Button("Refill breath / clear burn"))
            {
                PlayerVitals v = FindFirstObjectByType<PlayerVitals>();
                if (v != null) { v.Modify(v.MaxValue); v.DebugClearBurn(); }
            }
            DebugFlags.Invulnerable = GUILayout.Toggle(DebugFlags.Invulnerable, "Invulnerable (test only)");
            if (GUILayout.Button("Clear ending collection")) EndingCollection.ClearAll();
        }
#endif
    }
}
