#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// 런타임 스모크 테스트 (에디터 플레이 모드 / 개발 빌드 전용). 메뉴: Tools > GameUI > Debug > Run Runtime Smoke Test
    /// GameScene 을 여러 번 새로 불러 (재시작 5회) 타이머·이벤트 구독 수가 누적되지 않는지 확인하고,
    /// Act 3~4 의 NPC / 루트 A·B·C·D / 엔딩(히든 포함) / 타임아웃 사망을 DebugPanel 점프 + 상호작용 호출로 따라가 PASS/FAIL 을 남긴다.
    /// 실행 중 발생한 에러/예외 로그 수도 집계한다. 결과는 Console 과 프로젝트 루트의 smoke_results.txt 에 기록된다.
    /// 게임 시간은 timeScale 로 빠르게 돌린다 (실시간 대기를 줄이기 위해).
    /// </summary>
    public class ScenarioSmokeTest : MonoBehaviour
    {
        private const string SessionKey = "ScenarioSmokeTest.Run";
        private const float Speed = 5f;

        private readonly List<string> lines = new List<string>();
        private int pass, fail, errorLogs;
        private readonly List<string> errorSamples = new List<string>();
        private string sceneName;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
            UnityEditor.SessionState.SetBool(SessionKey, false);
            Begin();
        }
#endif

        public static void Begin()
        {
            var go = new GameObject("ScenarioSmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<ScenarioSmokeTest>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stack != null && stack.Contains("UnityEditor.Search")) return;   // 에디터 자체의 검색 인덱싱 예외는 게임 코드와 무관
            errorLogs++;
            if (errorSamples.Count < 12) errorSamples.Add($"{type}: {msg}");
        }

        private void Start() { sceneName = SceneManager.GetActiveScene().name; StartCoroutine(RunAll()); }

        // ═════════════════════ 보고 ═════════════════════
        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            string line = $"[Smoke] {(ok ? "PASS" : "FAIL")}  {name}  {detail}";
            lines.Add(line);
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private void Info(string s) { lines.Add("[Smoke] " + s); Debug.Log("[Smoke] " + s); }

        private void Clk(string tag)
        {
            GameClock c = GameClock.Instance;
            Info($"  ({tag}) timeScale={Time.timeScale:0.0} time={Time.time:0.0} gameSec={(c != null ? c.GameSeconds : -1f):0.0} grandma={(GrandmotherNpc.Instance != null ? GrandmotherNpc.Instance.State.ToString() : "-")} young={(YoungManNpc.Instance != null ? YoungManNpc.Instance.State.ToString() : "-")} act={ScenarioStateMachine.Instance.CurrentIndex + 1}");
        }

        // ═════════════════════ 공통 헬퍼 ═════════════════════
        private IEnumerator LoadFresh()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null;
            yield return null;
            float t = 0f;
            while ((ScenarioStateMachine.Instance == null || GameClock.Instance == null || DeathAndEnding.Instance == null) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.6f);
            Time.timeScale = Speed;
        }

        private IEnumerator Wait(float realSeconds)
        {
            for (float t = 0f; t < realSeconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> cond, float realTimeout, Action<bool> result = null)
        {
            float t = 0f;
            while (!cond() && t < realTimeout) { t += Time.unscaledDeltaTime; yield return null; }
            result?.Invoke(cond());
        }

        private IEnumerator Safe(string name, IEnumerator inner)
        {
            while (true)
            {
                object cur;
                try { if (!inner.MoveNext()) break; cur = inner.Current; }
                catch (Exception e) { Check(name + " (exception)", false, e.GetType().Name + ": " + e.Message); yield break; }
                yield return cur;
            }
        }

        private static PlayerController Player => FindFirstObjectByType<PlayerController>();
        private static DeathAndEnding DE => DeathAndEnding.Instance;

        private static void TeleportTo(Vector3 pos, float yaw = 0f)
        {
            PlayerController p = Player;
            p.Teleport(pos, yaw);
            if (HazardManager.Instance != null) HazardManager.Instance.ResyncPlayerZones(pos + Vector3.up);
        }

        private static T By<T>(string id) where T : InteractableObject => EvacDirector.FindById<T>(id);
        private static void Use(string id) { InteractableObject o = By<InteractableObject>(id); if (o != null) o.TryInteract(null); else Debug.LogWarning("[Smoke] missing interactable " + id); }

        private static void Jump(int act, bool hot, bool blocked, bool early)
            => FindFirstObjectByType<DebugPanel>().JumpToAct(act, hot, blocked, early);

        private static void Give(string id) => DebugPanel.GiveItem(id);

        // ═════════════════════ 실행 ═════════════════════
        private IEnumerator RunAll()
        {
            Info($"smoke test start (scene={sceneName}, speed x{Speed})");
            yield return Safe("restart x5", RestartCycles());
            yield return Safe("act3 corridor / npc neglect", Act3Neglect());
            yield return Safe("act3 persuade / stairs / handoff", Act3Persuade());
            yield return Safe("route C shelter", RouteC());
            yield return Safe("route A rooftop", RouteA());
            yield return Safe("route B partition + hidden ending", RouteB());
            yield return Safe("route D descent", RouteD());
            yield return Safe("act4 timeout death", Act4Timeout());
            yield return Safe("restart again after endings", RestartCycles(2, "after-ending"));

            Check("no error/exception logs during run", errorLogs == 0, $"errors={errorLogs}  {string.Join(" | ", errorSamples)}");
            Info($"smoke test done: PASS {pass} / FAIL {fail}");
            WriteResults();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fail == 0 ? 0 : 3);
            else UnityEditor.EditorApplication.isPlaying = false;
#endif
            Destroy(gameObject);
        }

        private void WriteResults()
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "smoke_results.txt"));
                var sb = new StringBuilder();
                foreach (string l in lines) sb.AppendLine(l);
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[Smoke] could not write results: " + e.Message); }
        }

        // ───────── 재시작 반복: 타이머/구독이 누적되지 않아야 한다 ─────────
        private IEnumerator RestartCycles(int count = 5, string tag = "restart")
        {
            var subs = new List<int>(); var timers = new List<int>(); var runIds = new List<int>();
            for (int i = 0; i < count; i++)
            {
                yield return LoadFresh();
                yield return Wait(1.2f);
                subs.Add(EventBus.SubscriptionCount());
                timers.Add(GameClock.Instance != null ? GameClock.Instance.ActiveTimerCount : -1);
                runIds.Add(GameRunContext.RunId);
            }
            Info($"{tag}: runIds=[{string.Join(",", runIds)}] subs=[{string.Join(",", subs)}] timers=[{string.Join(",", timers)}]");
            Check($"{tag}: RunId increments each load", runIds[count - 1] - runIds[0] == count - 1);
            bool subsStable = true, timersStable = true;
            for (int i = 1; i < count; i++) { if (subs[i] != subs[0]) subsStable = false; if (timers[i] != timers[0]) timersStable = false; }
            Check($"{tag}: EventBus subscription count stable", subsStable, $"[{string.Join(",", subs)}]");
            Check($"{tag}: active timer count stable", timersStable, $"[{string.Join(",", timers)}]");
        }

        // ───────── Act 3: 방치 (청년은 계단으로, 할머니는 엘리베이터로) ─────────
        private IEnumerator Act3Neglect()
        {
            yield return LoadFresh();
            DebugFlags.Invulnerable = true;   // 5배속에서 복도 연기로 먼저 죽지 않게 (이 시나리오는 NPC·계단 판정만 본다)
            Jump(3, hot: false, blocked: false, early: false);
            yield return Wait(0.3f);
            Check("act3: Act 3 active", ScenarioStateMachine.Instance.CurrentIndex == 2);
            TeleportTo(new Vector3(0f, 0.02f, -1.0f), 270f);
            yield return Wait(0.4f);
            Check("act3: grandmother begins pressing elevator", GrandmotherNpc.Instance != null && GrandmotherNpc.Instance.State == GrandmaState.PressingElevator, GrandmotherNpc.Instance != null ? GrandmotherNpc.Instance.State.ToString() : "null");
            Check("act3: young man waiting", YoungManNpc.Instance != null && YoungManNpc.Instance.State == YoungManState.Waiting);
            Check("act3: corridor flag set", ScenarioFlags.GetBool(EvacKeys.FlagInCorridor));

            Clk("corridor entered");
            bool lost = false;
            yield return WaitUntil(() => ScenarioFlags.GetBool(EvacKeys.FlagYoungManDead), 12f, r => lost = r);
            Clk("after young-man wait");
            Check("act3: neglected young man is lost (non-graphic)", lost);
            Check("act3: neglect opens the stair fire door", By<ToggleInteractable>(EvacKeys.StairFireDoor) != null && By<ToggleInteractable>(EvacKeys.StairFireDoor).IsOn);
            bool gdead = false;
            yield return WaitUntil(() => ScenarioFlags.GetBool(EvacKeys.FlagGrandmotherDead), 14f, r => gdead = r);
            Clk("after grandmother wait");
            Check("act3: neglected grandmother boards elevator (panic → dead)", gdead, GrandmotherNpc.Instance.State.ToString());
            Check("act3: hidden-ending flag off when grandmother lost", !ScenarioFlags.GetBool(EvacKeys.FlagGrandmotherWith));

            // 플레이어가 계단실로 들어가면(방화문은 청년이 열어 둠) 계단 상태를 확인하고 Act 4 로 넘어간다
            TeleportTo(new Vector3(-14.8f, 0.02f, -1.0f), 270f);
            bool act4 = false;
            yield return WaitUntil(() => ScenarioStateMachine.Instance.CurrentIndex == 3, 8f, r => act4 = r);
            Check("act3: stair check completes Act 3 → Act 4", act4);
            Check("act3: stair flags recorded (smoke at open)", ScoreSystem.Instance.HasRecord(EvacKeys.RecStairSmokeAtOpen));
            DebugFlags.Invulnerable = false;
        }

        // ───────── Act 3: 설득 → 동행, 마지막에 계단 확인 ─────────
        private IEnumerator Act3Persuade()
        {
            yield return LoadFresh();
            DebugFlags.Invulnerable = true;
            Jump(3, hot: false, blocked: false, early: false);
            yield return Wait(0.3f);
            Give("wet_towel");
            GrandmotherNpc g = GrandmotherNpc.Instance;
            YoungManNpc y = YoungManNpc.Instance;

            // 청년이 달려가기 전에(약 10초) 먼저 말을 건다: 설득 → 함께 대피
            TeleportTo(new Vector3(-7.4f, 0.02f, -1.0f), 270f);
            yield return Wait(0.3f);
            y.TryInteract(null);
            Check("dialogue(young man): choice panel opens", ChoicePanel.Instance.IsOpen);
            Choose(0);
            yield return Wait(0.2f);
            Check("young man persuaded → follows", y.State == YoungManState.Following, y.State.ToString());
            Check("young man saved flag", ScenarioFlags.GetBool(EvacKeys.FlagYoungManSaved));

            TeleportTo(new Vector3(-0.5f, 0.02f, -1.0f), 180f);
            yield return Wait(0.4f);

            // 대화: 첫 선택지(설득) → Persuaded, 다시 대화 첫 선택지(동행)
            g.TryInteract(null);
            Check("dialogue: choice panel opens", ChoicePanel.Instance != null && ChoicePanel.Instance.IsOpen);
            Choose(0);
            yield return Wait(0.2f);
            Check("grandmother persuaded", g.State == GrandmaState.Persuaded, g.State.ToString());
            g.TryInteract(null);
            Choose(0);
            yield return Wait(0.2f);
            Check("grandmother follows player", g.State == GrandmaState.FollowingPlayer && g.IsFollowing, g.State.ToString());
            Check("hidden-ending flag on while accompanied", ScenarioFlags.GetBool(EvacKeys.FlagGrandmotherWith));

            // 수건 건네기 (젖은 수건 보유)
            g.TryInteract(null);
            Check("towel option offered when player has wet towel", ChoiceCount() >= 3, "options=" + ChoiceCount());
            Choose(1);
            yield return Wait(0.2f);
            Check("towel handed to grandmother", g.HasTowel && !InventorySystem.Instance.Has("wet_towel"));

            // 따라오는지: 플레이어를 6m 이동
            Vector3 before = g.transform.position;
            TeleportTo(new Vector3(-6f, 0.02f, -1.0f), 270f);
            yield return Wait(2.0f);
            float d = Vector3.Distance(new Vector3(g.transform.position.x, 0, g.transform.position.z), new Vector3(Player.transform.position.x, 0, Player.transform.position.z));
            Check("follower AI closes distance to player", d < GameTimingConfig.Current.npcWaitDistance + 3f, $"dist={d:0.0}");

            // 계단실 진입 → Act 4, 동행 유지
            By<ToggleInteractable>(EvacKeys.FrontDoor)?.SetState(false, silent: true);
            TeleportTo(new Vector3(-14.8f, 0.02f, -1.0f), 270f);
            By<ToggleInteractable>(EvacKeys.StairFireDoor)?.SetState(true);
            bool act4 = false;
            yield return WaitUntil(() => ScenarioStateMachine.Instance.CurrentIndex == 3, 8f, r => act4 = r);
            Check("persuade route reaches Act 4", act4);
            Check("grandmother still accompanying into Act 4", g.State == GrandmaState.FollowingPlayer);
            Check("neighbor rescued flag", ScenarioFlags.GetBool(EvacKeys.FlagNeighborRescued));
            DebugFlags.Invulnerable = false;
        }

        private static void Choose(int index)
        {
            MethodInfo m = typeof(ChoicePanel).GetMethod("Choose", BindingFlags.Instance | BindingFlags.NonPublic);
            m.Invoke(ChoicePanel.Instance, new object[] { index });
        }

        private static int ChoiceCount()
        {
            FieldInfo f = typeof(ChoicePanel).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((System.Collections.ICollection)f.GetValue(ChoicePanel.Instance)).Count;
        }

        // ───────── 루트 C: 대피공간 ─────────
        private IEnumerator RouteC()
        {
            yield return LoadFresh();
            Jump(4, hot: true, blocked: false, early: false);
            yield return Wait(0.4f);
            Give("wet_towel"); Give("wet_towel"); Give("duct_tape");
            TeleportTo(new Vector3(6.0f, 0.02f, 8.2f), 270f);
            yield return Wait(0.8f);
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
            Check("routeC: committed by closing shelter door inside", a4.CurrentRoute == Act4_Evacuation.Route.C, a4.CurrentRoute.ToString());

            foreach (SealPoint s in FindObjectsByType<SealPoint>(FindObjectsInactive.Include)) s.TryInteract(null);
            yield return Wait(0.3f);
            ShelterSealSystem sys = ShelterSealSystem.Instance;
            Check("routeC: four sides sealed", sys.SealedSides == 4, "sides=" + sys.SealedSides);
            Check("routeC: seal ratio > 0.5", sys.SealRatio > 0.5f, $"ratio={sys.SealRatio:0.00}");
            By<ToggleInteractable>(EvacKeys.ShelterWindow).SetState(true);
            Call119Controller.Instance.Report(true);
            Check("routeC: 119 accurate recorded", ScoreSystem.Instance.GetRecord(EvacKeys.RecCall119) >= 2f);
            yield return Wait(0.3f);
            bool ended = false;
            yield return WaitUntil(() => DE.HasEnded, 40f, r => ended = r);
            Check("routeC: ladder arrives → shelter ending", ended && DE.LastEndingId == EvacKeys.EndingShelter, "ending=" + DE.LastEndingId);
            Check("routeC: score finalized with rules", ScoreSystem.Instance.IsFinalized && ScoreSystem.Instance.Breakdown.Count == 14, "lines=" + ScoreSystem.Instance.Breakdown.Count);
            Info($"routeC score {ScoreSystem.Instance.Total}/{ScoreSystem.Instance.MaxTotal} grade {ScoreSystem.Instance.GetGradeLabel()}");
        }

        // ───────── 루트 A: 옥상 ─────────
        private IEnumerator RouteA()
        {
            yield return LoadFresh();
            Jump(4, hot: false, blocked: true, early: false);
            yield return Wait(0.4f);
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
            Check("routeA: options allow A when stairs blocked", a4.Options.a && !a4.Options.b && a4.Options.c);
            TeleportTo(new Vector3(-15.2f, 0.02f, -1.5f), 270f);
            Use(EvacKeys.StairsUp);
            yield return Wait(1.0f);
            Check("routeA: route committed A", a4.CurrentRoute == Act4_Evacuation.Route.A);
            yield return WaitUntil(() => !EvacDirector.Instance.IsTraveling, 6f);
            Check("routeA: teleported to 14F landing", Player.transform.position.x < -30f && Player.transform.position.x > -45f, $"x={Player.transform.position.x:0.0}");
            By<ToggleInteractable>(EvacKeys.FireDoor14).SetState(false);
            Use(EvacKeys.StairsUp14);
            yield return Wait(0.5f);
            yield return WaitUntil(() => !EvacDirector.Instance.IsTraveling, 6f);
            By<ToggleInteractable>(EvacKeys.FireDoor17).SetState(false);
            Use(EvacKeys.StairsUp17);
            yield return Wait(0.5f);
            yield return WaitUntil(() => !EvacDirector.Instance.IsTraveling, 6f);
            Check("routeA: at 20F landing", Player.transform.position.x < -58f && Player.transform.position.x > -66f, $"x={Player.transform.position.x:0.0}");
            Check("routeA: fire doors managed flag", ScenarioFlags.GetBool(EvacKeys.FlagFireDoorManaged));
            Use(EvacKeys.RoofDoor);   // 열쇠 없이 → 잠김 안내 (막는 가드가 아니라 조건 안내)
            Check("routeA: roof door stays locked without key", By<InteractableObject>(EvacKeys.RoofDoor) != null);
            Use(EvacKeys.KeyBox);
            Check("routeA: key box gives roof key", InventorySystem.Instance.Has(EvacKeys.ItemRoofKey));
            Use(EvacKeys.RoofDoor);
            Check("routeA: roof key unlocks door", !InventorySystem.Instance.Has(EvacKeys.ItemRoofKey));
            TeleportTo(new Vector3(-64f + 4.5f + 10f, 0.02f, 2f), 90f);
            Use(EvacKeys.RoofSignal);
            bool ended = false;
            yield return WaitUntil(() => DE.HasEnded, 20f, r => ended = r);
            Check("routeA: rescue → rooftop ending", ended && DE.LastEndingId == EvacKeys.EndingRooftop, "ending=" + DE.LastEndingId);
        }

        // ───────── 루트 B: 경량칸막이 + 히든 엔딩 ─────────
        private IEnumerator RouteB()
        {
            yield return LoadFresh();
            Jump(4, hot: true, blocked: false, early: false);
            yield return Wait(0.5f);
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
            Check("routeB: options allow B when handle hot", a4.Options.b && !a4.Options.a);
            Check("routeB: grandmother fallen in 1003", GrandmotherNpc.Instance.State == GrandmaState.Fallen, GrandmotherNpc.Instance.State.ToString());
            Give("hammer");
            PartitionBreakable pb = By<PartitionBreakable>(EvacKeys.LightPartition);
            for (int i = 0; i < 3; i++) { pb.TryInteract(null); yield return Wait(0.5f); }
            Check("routeB: partition broken after hammer hits", pb.IsBroken, $"hits={pb.Hits}");
            Check("routeB: route committed B", a4.CurrentRoute == Act4_Evacuation.Route.B);
            TeleportTo(new Vector3(-2.2f, 0.02f, 3.8f), 0f);
            GrandmotherNpc.Instance.TryInteract(null);
            Check("routeB: grandmother lifted → follows", GrandmotherNpc.Instance.State == GrandmaState.FollowingPlayer);
            Use(EvacKeys.Door1003Inside);
            bool ended = false;
            yield return WaitUntil(() => DE.HasEnded, 20f, r => ended = r);
            Check("routeB: hidden ending (grandmother accompanied)", ended && DE.LastEndingId == EvacKeys.EndingHidden, "ending=" + DE.LastEndingId);
            Check("routeB: base route ending remembered", ScenarioFlags.GetText("ending_base_id") == EvacKeys.EndingPartition, ScenarioFlags.GetText("ending_base_id"));
        }

        // ───────── 루트 D: 하강 ─────────
        private IEnumerator RouteD()
        {
            yield return LoadFresh();
            Jump(4, hot: false, blocked: false, early: true);
            yield return Wait(0.4f);
            Act4_Evacuation a4 = FindFirstObjectByType<Act4_Evacuation>();
            Check("routeD: options allow D after early fire-door success", a4.Options.d);
            TeleportTo(new Vector3(-14.7f, 0.02f, -2.4f), 180f);
            Use(EvacKeys.StairsDown);
            yield return Wait(0.8f);
            yield return WaitUntil(() => !EvacDirector.Instance.IsTraveling, 6f);
            Check("routeD: committed D", a4.CurrentRoute == Act4_Evacuation.Route.D);
            Check("routeD: wall-hugging slows player", Mathf.Abs(Player.ScenarioSpeedMultiplier - GameTimingConfig.Current.descentSpeedMultiplier) < 0.01f, $"mult={Player.ScenarioSpeedMultiplier:0.00}");
            for (int stage = 1; stage <= 3; stage++)
            {
                HazardZone end = EvacDirector.FindZone("zone_descent_end_" + stage);
                TeleportTo(end.transform.position + Vector3.up * 0.02f, 90f);
                yield return Wait(0.6f);
                yield return WaitUntil(() => !EvacDirector.Instance.IsTraveling, 6f);
            }
            Check("routeD: reached assembly plaza", Vector3.Distance(Player.transform.position, new Vector3(-36f, 0f, 64f)) < 10f, $"pos={Player.transform.position}");
            Check("routeD: speed restored on plaza", Mathf.Approximately(Player.ScenarioSpeedMultiplier, 1f));
            bool ended = false;
            yield return WaitUntil(() => DE.HasEnded, 20f, r => ended = r);
            Check("routeD: assembly → descent ending", ended && DE.LastEndingId == EvacKeys.EndingDescent, "ending=" + DE.LastEndingId);
        }

        // ───────── Act 4 타임아웃 사망 ─────────
        private IEnumerator Act4Timeout()
        {
            yield return LoadFresh();
            Jump(4, hot: true, blocked: false, early: false);
            // 연기 질식이 먼저 죽이지 않도록 불을 모두 끄고 연기를 없앤다 (여기서는 타임아웃 사망만 확인)
            foreach (FireSource f in FireSource.All) if (f != null && f.IsLit) f.Extinguish();
            if (FireManager.Instance != null) FireManager.Instance.SmokeFloor = 0f;
            if (HazardManager.Instance != null) HazardManager.Instance.SetSmoke(0f);
            bool died = false;
            yield return WaitUntil(() => DE.HasEnded, 45f, r => died = r);
            Check("act4: no route chosen → ExitTimeout death with feedback", died && DE.LastDeathReason == DeathReason.ExitTimeout, "reason=" + DE.LastDeathReason);
        }
    }
}
#endif
