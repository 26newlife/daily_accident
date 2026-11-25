#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2(자연재해) 런타임 스모크 테스트 (에디터 플레이 / 개발 빌드 전용).
    /// 메뉴: Tools > GameUI > Debug > Run S02 Smoke Test. 결과는 Console 과 smoke_results_s02.txt 에 기록된다.
    ///   · S02 가 시나리오 1·3 과 같은 집(GameScene)에서 침실 스폰으로 시작하는지
    ///   · 비상계단 확장이 켜지고 10층 입구 블록아웃이 숨겨지는지
    ///   · 플레이어 CharacterController 로 실제로 걸어서 10층 → 1층 출구 → 건물 밖 지면, 10층 → 옥상까지 갈 수 있는지
    ///   · 타임라인을 잠깐 돌려 에러 로그가 없는지
    /// 걷기 구간은 지진 타임라인을 멈춘 채 진행한다 (흔들림·사망 판정과 무관하게 길만 검사).
    /// </summary>
    public class S02SmokeTest : MonoBehaviour
    {
        private const string SessionKey = "S02SmokeTest.Run";
        public const string ScenarioKey = "S02SmokeTest.Scenario";
        private bool quake = true;   // S02 이면 지진 전용 검사(구역 판정·Act·타임라인)도 한다
        private string scenarioId = "S02";
        private const float WalkSpeed = 3f, StepDt = 0.02f, Gravity = -20f;
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
            // 다른 시나리오(S01·S03)의 비상계단·바깥 동선만 검사할 때는 ScenarioKey 에 ScenarioData 경로를 넣는다
            string path = UnityEditor.SessionState.GetString(ScenarioKey, "");
            UnityEditor.SessionState.EraseString(ScenarioKey);
            if (string.IsNullOrEmpty(path)) path = "Assets/Core/Data/Scenarios/Scenario_02.asset";
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<GameData.ScenarioData>(path);
            GameSession.SelectScenario(data);
            var go = new GameObject("S02SmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<S02SmokeTest>();
        }
#endif

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stack != null && stack.Contains("UnityEditor.Search")) return;
            errorLogs++;
            if (errorSamples.Count < 12) errorSamples.Add($"{type}: {msg}");
        }

        private void Start() { sceneName = SceneManager.GetActiveScene().name; StartCoroutine(RunAll()); }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            string line = $"[S02Smoke] {(ok ? "PASS" : "FAIL")}  {name}  {detail}";
            lines.Add(line);
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private void Info(string s) { lines.Add("[S02Smoke] " + s); Debug.Log("[S02Smoke] " + s); }

        private IEnumerator Wait(float real) { for (float t = 0f; t < real; t += Time.unscaledDeltaTime) yield return null; }

        private IEnumerator LoadFresh()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null; yield return null;
            float t = 0f;
            while ((ScenarioRunner.Instance == null || !ScenarioRunner.Instance.IsReady || DeathAndEnding.Instance == null) && t < 25f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.5f);
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

        // ═════════════════════ 전체 ═════════════════════
        private IEnumerator RunAll()
        {
            yield return Wait(0.5f);
            yield return Safe("시작 상태", StartState());
            yield return Safe("10층 → 1층 출구 → 지면", WalkDown());
            yield return Safe("10층 → 옥상", WalkUp());
            if (quake) yield return Safe("타임라인 진행", TimelineRun());
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Check("실행 중 에러 로그 없음", errorLogs == 0, errorLogs > 0 ? string.Join(" | ", errorSamples) : "");
            lines.Add($"[S02Smoke] smoke test done: PASS {pass} / FAIL {fail}");
            Debug.Log($"[S02Smoke] smoke test done: PASS {pass} / FAIL {fail}");
            string file = quake ? "smoke_results_s02.txt" : $"smoke_results_stairs_{scenarioId.ToLowerInvariant()}.txt";
            try { File.WriteAllLines(Path.Combine(Application.dataPath, "..", file), lines); } catch (Exception) { }
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fail == 0 ? 0 : 3);
            else UnityEditor.EditorApplication.isPlaying = false;
#endif
            Destroy(gameObject);
        }

        // ═════════════════════ 시작 상태 ═════════════════════
        private IEnumerator StartState()
        {
            yield return LoadFresh();
            ScenarioRunner r = ScenarioRunner.Instance;
            string expected = GameSession.SelectedScenario != null && GameSession.SelectedScenario.Definition != null ? GameSession.SelectedScenario.Definition.Id : "S02";
            scenarioId = expected;
            quake = expected == "S02";
            Check($"시나리오 {expected} 로드", r.Definition != null && r.Definition.Id == expected, r.Definition != null ? r.Definition.Id : "null " + r.LastError);
            Check("같은 집(GameScene)에서 시작", SceneManager.GetActiveScene().name == "GameScene" && HouseController.Instance != null, SceneManager.GetActiveScene().name);
            if (quake)
            {
                Check("S02 씬 Additive 로드", r.ScenarioScene.IsValid() && r.ScenarioScene.name == "S02_Disaster", r.ScenarioScene.IsValid() ? r.ScenarioScene.name : "invalid");
                Check("Act 4개", r.Root != null && r.Root.Acts != null && r.Root.Acts.Length == 4, r.Root != null && r.Root.Acts != null ? r.Root.Acts.Length.ToString() : "null");
            }

            PlayerController p = Player;
            Transform bed = HouseController.Instance != null ? HouseController.Instance.transform.Find("HouseSpawns/spawn_bedroom_bed") : null;
            if (bed == null) bed = GameObject.Find("spawn_bedroom_bed")?.transform;
            if (p != null && bed != null)
                Check("방(침실 스폰) 안에서 시작", Vector3.Distance(p.transform.position, bed.position) < 0.8f, $"player={p.transform.position:F2} spawn={bed.position:F2}");
            QuakeAreas areas = QuakeAreas.Instance;
            if (quake) Check("구역 판정기", areas != null);
            if (p != null && areas != null)
                Check("구역 = Apartment", areas.Classify(p.transform.position) == QuakeArea.Apartment, p.transform.position.ToString("F2"));

            StairwellExtension ext = StairwellExtension.Find();
            Check("비상계단 확장 존재", ext != null);
            if (ext == null) yield break;
            Check("비상계단 켜짐", ext.IsWalkable);
            Transform level = ext.transform;
            Transform wall = level.Find("Structure/Wall_StairWest"), stub = level.Find("Structure/EmergencyStairs");
            Check("10층 입구 블록아웃 숨김", wall != null && !wall.gameObject.activeInHierarchy && stub != null && !stub.gameObject.activeInHierarchy);
            Transform root = ext.transform.parent != null ? ext.transform.parent.Find("WalkableStairwell") : null;
            Check("계단탑 활성", root != null && root.gameObject.activeInHierarchy);
        }

        // ═════════════════════ 걷기 ═════════════════════
        private Transform house;
        private CharacterController cc;
        private float vVel;
        private string stuckInfo;

        private bool PrepareWalk()
        {
            if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.Stop();
            PlayerController p = Player;
            StairwellExtension ext = StairwellExtension.Find();
            if (p == null || ext == null || ext.transform.parent == null) { Check("걷기 준비", false, "player/stairwell 없음"); return false; }
            house = ext.transform.parent;
            cc = p.GetComponent<CharacterController>();
            p.ControlSuspended = true;   // 입력 대신 테스트가 직접 CharacterController 를 움직인다
            // 10층 기존 계단실 안 (방화문 안쪽)
            p.Teleport(house.TransformPoint(new Vector3(-15.6f, 0.05f, -1.6f)), -90f);
            vVel = 0f;
            Physics.SyncTransforms();
            return true;
        }

        private Vector3 LocalPos => house.InverseTransformPoint(cc.transform.position);

        /// <summary>집 로컬 (x, z) 로 걸어간다 (중력 포함). 실패하면 false 와 stuckInfo</summary>
        private bool WalkTo(float x, float z, int maxSteps = 900)
        {
            Vector3 target = house.TransformPoint(new Vector3(x, 0f, z));
            float best = float.MaxValue; int sinceBest = 0;
            for (int i = 0; i < maxSteps; i++)
            {
                Vector3 pos = cc.transform.position;
                Vector3 d = target - pos; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.08f) return true;
                if (dist < best - 0.01f) { best = dist; sinceBest = 0; }
                else if (++sinceBest > 120) { stuckInfo = $"막힘 at {LocalPos:F2} → ({x:F2}, {z:F2}) 근처: {Blockers()}"; return false; }
                Vector3 step = d / dist * Mathf.Min(WalkSpeed * StepDt, dist);
                vVel += Gravity * StepDt;
                CollisionFlags f = cc.Move(step + Vector3.up * vVel * StepDt);
                if ((f & CollisionFlags.Below) != 0) vVel = -2f;
                if (LocalPos.y < -40f) { stuckInfo = $"추락 at {LocalPos:F2}"; return false; }
            }
            stuckInfo = $"시간 초과 at {LocalPos:F2}";
            return false;
        }

        private string Blockers()
        {
            Vector3 c = cc.transform.position;
            var names = new List<string>();
            foreach (Collider col in Physics.OverlapCapsule(c + Vector3.up * 0.2f, c + Vector3.up * (cc.height - 0.2f), cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform == cc.transform) continue;
                Transform t = col.transform; string n = t.name;
                for (int k = 0; k < 2 && t.parent != null; k++) { t = t.parent; n = t.name + "/" + n; }
                names.Add(n);
            }
            return string.Join(", ", names);
        }

        private bool WalkPath(string label, params Vector2[] pts)
        {
            foreach (Vector2 v in pts)
                if (!WalkTo(v.x, v.y)) { Check(label, false, stuckInfo); return false; }
            return true;
        }

        private static float FloorY(int f) => (f - 10) * 3f;

        private IEnumerator WalkDown()
        {
            if (!PrepareWalk()) yield break;
            yield return null;
            if (!WalkPath("10층 계단실 → 계단탑 층참", new Vector2(-18.8f, -1.6f), new Vector2(-18.8f, -2.4f))) yield break;
            Check("10층 층참 도착", Mathf.Abs(LocalPos.y - FloorY(10)) < 0.25f, LocalPos.ToString("F2"));
            for (int f = 10; f >= 2; f--)
            {
                // 층참(남쪽 줄) → 반층참 (내려감) → 북쪽 줄 → 아래층 층참 (6층 층참 북동쪽 구석의 다친 이웃은 피해서 지나간다)
                if (!WalkPath($"{f}층 → {f - 1}층", new Vector2(-23f, -2.4f), new Vector2(-23f, -0.85f), new Vector2(-19.25f, -0.85f), new Vector2(-18.9f, -1.6f), new Vector2(-18.8f, -2.4f))) yield break;
                if (Mathf.Abs(LocalPos.y - FloorY(f - 1)) > 0.25f) { Check($"{f - 1}층 층참 높이", false, LocalPos.ToString("F2")); yield break; }
                yield return null;
            }
            Check("1층 층참 도착", Mathf.Abs(LocalPos.y - FloorY(1)) < 0.25f, LocalPos.ToString("F2"));
            if (!WalkPath("1층 → 지면 반층참 → 출구", new Vector2(-23f, -2.4f), new Vector2(-23f, -1.6f), new Vector2(-27f, -1.6f), new Vector2(-32f, -1.6f))) yield break;
            yield return null;
            Vector3 lp = LocalPos;
            Check("건물 밖 지면 도착 (y -28.5)", Mathf.Abs(lp.y - (-28.5f)) < 0.25f, lp.ToString("F2"));
            QuakeAreas areas = QuakeAreas.Instance;
            if (areas != null) Check("구역 = Outside", areas.Classify(cc.transform.position) == QuakeArea.Outside, areas.Classify(cc.transform.position).ToString());
            // 지진 옥외대피장소 (놀이터 옆 공터)
            bool ok = WalkTo(-45f, 30f, 3000) && WalkTo(-70f, 50f, 3000);
            Check("옥외대피장소까지 걷기", ok && Mathf.Abs(LocalPos.y - (-28.5f)) < 0.3f, ok ? LocalPos.ToString("F2") : stuckInfo);
            if (ok && areas != null) Check("구역 = OutdoorSite", areas.Classify(cc.transform.position) == QuakeArea.OutdoorSite, areas.Classify(cc.transform.position).ToString());
            // 강: 난간이 막아 물로 들어가지 못한다
            if (ok)
            {
                WalkTo(-70f, 74f, 800);
                Check("강 난간이 막음", LocalPos.z < 68f, LocalPos.ToString("F2"));
            }
            yield return null;
            // 지진해일 대피소 (뒷산 비탈을 올라 언덕 위 쉼터)
            ok = WalkTo(-40f, 20f, 3000) && WalkTo(-35f, -10f, 3000) && WalkTo(-15f, -48f, 3000) && WalkTo(-15f, -99f, 3000);
            Check("뒷산 대피소까지 걷기 (y -14.5)", ok && Mathf.Abs(LocalPos.y - (-14.5f)) < 0.3f, ok ? LocalPos.ToString("F2") : stuckInfo);
            if (ok && areas != null) Check("구역 = Shelter", areas.Classify(cc.transform.position) == QuakeArea.Shelter, areas.Classify(cc.transform.position).ToString());
        }

        private IEnumerator WalkUp()
        {
            yield return LoadFresh();
            if (!PrepareWalk()) yield break;
            yield return null;
            if (!WalkPath("10층 계단실 → 계단탑 층참", new Vector2(-18.8f, -1.6f), new Vector2(-18.8f, -0.85f))) yield break;
            for (int f = 10; f <= 20; f++)
            {
                // 층참(북쪽 줄) → 반층참 (올라감) → 남쪽 줄 → 위층 층참
                if (!WalkPath($"{f}층 → {f + 1}층", new Vector2(-23f, -0.85f), new Vector2(-23f, -2.4f), new Vector2(-18.8f, -2.4f), new Vector2(-18.8f, -0.85f))) yield break;
                if (Mathf.Abs(LocalPos.y - FloorY(f + 1)) > 0.25f) { Check($"{f + 1}층 층참 높이", false, LocalPos.ToString("F2")); yield break; }
                yield return null;
            }
            Check("옥상층 층참 도착 (y 33)", Mathf.Abs(LocalPos.y - 33f) < 0.25f, LocalPos.ToString("F2"));
            if (scenarioId == "S03")
            {
                // 시나리오 3: 옥상 출입문은 잠겨 있다 (루트 A — 비상 열쇠함의 열쇠로 연다). 문이 막는지만 본다
                WalkTo(-14f, -1.45f, 400);
                Check("S03: 잠긴 옥상 출입문이 막음", LocalPos.x < -18.1f, LocalPos.ToString("F2"));
                yield break;
            }
            if (!WalkPath("옥상 출입구 → 옥상", new Vector2(-18.8f, -1.45f), new Vector2(-14f, -1.45f), new Vector2(-6f, 2f))) yield break;
            yield return null;
            Vector3 lp = LocalPos;
            Check("옥상 도착 (y 33)", Mathf.Abs(lp.y - 33f) < 0.25f, lp.ToString("F2"));
            QuakeAreas areas = QuakeAreas.Instance;
            if (areas != null) Check("구역 = Rooftop", areas.Classify(cc.transform.position) == QuakeArea.Rooftop, areas.Classify(cc.transform.position).ToString());
            // 난간 밖으로 떨어지지 않는지 (동쪽 끝까지)
            WalkTo(12f, 2f, 800);
            Check("옥상 난간이 막음", LocalPos.x < 10f && Mathf.Abs(LocalPos.y - 33f) < 0.3f, LocalPos.ToString("F2"));
        }

        // ═════════════════════ 타임라인 ═════════════════════
        private IEnumerator TimelineRun()
        {
            yield return LoadFresh();
            QuakeTimeline tl = QuakeTimeline.Instance;
            Check("타임라인 시작", tl != null && tl.Running, tl != null ? tl.Phase.ToString() : "null");
            if (tl == null) yield break;
            Time.timeScale = 4f;
            QuakePhase start = tl.Phase;
            yield return Wait(4f);
            Info($"타임라인 단계 {start} → {tl.Phase}, 종료={DeathAndEnding.Instance != null && DeathAndEnding.Instance.HasEnded}");
            Time.timeScale = 1f;
        }
    }
}
#endif
