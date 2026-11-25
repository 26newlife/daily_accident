#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2(자연재해) 분기 플레이 테스트 (에디터 플레이 / 개발 빌드 전용).
    /// 메뉴: Tools > GameUI > Debug > Run S02 Branch Test. 결과는 Console 과 smoke_results_s02_branches.txt.
    /// 분기마다 씬을 새로 불러와, 플레이어를 순간이동·웅크림·상호작용으로 조작하고 실제 결과(사망 원인 / 엔딩)를 기대값과 비교한다.
    /// 흔들리는 단계에는 순간이동하지 않는다 (흔들림 중 이동 규칙에 걸리지 않게). 시간은 timeScale 로 빠르게 돌린다.
    /// </summary>
    public class S02BranchTest : MonoBehaviour
    {
        public const string SessionKey = "S02BranchTest.Run";
        private const float Speed = 6f;
        private readonly List<string> lines = new List<string>();
        private int pass, fail, errorLogs;
        private readonly List<string> errorSamples = new List<string>();
        private string sceneName;
        private int branchErrors;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!UnityEditor.SessionState.GetBool(SessionKey, false)) return;
            UnityEditor.SessionState.SetBool(SessionKey, false);
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<GameData.ScenarioData>("Assets/Core/Data/Scenarios/Scenario_02.asset");
            GameSession.SelectScenario(data);
            var go = new GameObject("S02BranchTest");
            DontDestroyOnLoad(go);
            go.AddComponent<S02BranchTest>();
        }
#endif

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (stack != null && stack.Contains("UnityEditor.Search")) return;
            errorLogs++; branchErrors++;
            if (errorSamples.Count < 20) errorSamples.Add($"{type}: {msg}");
        }

        private void Start() { sceneName = SceneManager.GetActiveScene().name; StartCoroutine(RunAll()); }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            string line = $"[S02Branch] {(ok ? "PASS" : "FAIL")}  {name}  {detail}";
            lines.Add(line);
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private void Info(string s) { lines.Add("[S02Branch]   · " + s); Debug.Log("[S02Branch]   · " + s); }

        // ═════════════════════ 공통 ═════════════════════
        private static QuakeTimeline TL => QuakeTimeline.Instance;
        private static DeathAndEnding DE => DeathAndEnding.Instance;
        private static bool Ended => DE != null && DE.HasEnded;
        private PlayerController player;
        private CharacterController cc;
        private Transform house;

        private IEnumerator RealWait(float real) { for (float t = 0f; t < real; t += Time.unscaledDeltaTime) yield return null; }

        private IEnumerator LoadFresh()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null; yield return null;
            float t = 0f;
            while ((ScenarioRunner.Instance == null || !ScenarioRunner.Instance.IsReady || DE == null || TL == null || !TL.Running) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return RealWait(0.3f);
            player = FindFirstObjectByType<PlayerController>();
            cc = player != null ? player.GetComponent<CharacterController>() : null;
            StairwellExtension ext = StairwellExtension.Find();
            house = ext != null && ext.transform.parent != null ? ext.transform.parent : (HouseController.Instance != null ? HouseController.Instance.transform : null);
            if (player != null) player.ControlSuspended = true;   // 입력 대신 테스트가 조작한다
            branchErrors = 0;
        }

        private static readonly FieldInfo CrouchField = typeof(PlayerController).GetField("<IsCrouching>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private void Crouch(bool on) { if (player != null && CrouchField != null) CrouchField.SetValue(player, on); }

        private Vector3 W(float x, float y, float z) => house != null ? house.TransformPoint(new Vector3(x, y, z)) : new Vector3(x, y, z);

        private void Tp(float x, float y, float z, float yaw = 0f)
        {
            if (player == null) return;
            player.Teleport(W(x, y, z), yaw);
            Physics.SyncTransforms();
        }

        /// <summary>중력으로 바닥에 내려앉힌다 (비탈 위 등 높이를 모르는 곳)</summary>
        private void Settle()
        {
            if (cc == null) return;
            for (int i = 0; i < 400; i++)
            {
                CollisionFlags f = cc.Move(Vector3.down * 0.1f);
                if ((f & CollisionFlags.Below) != 0) break;
            }
        }

        private static void Use(string id)
        {
            InteractableObject o = InteractableObject.FindById(id);
            if (o == null) { Debug.LogWarning("[S02Branch] 상호작용 없음: " + id); return; }
            o.TryInteract(null);
        }

        /// <summary>단계 phase 에 들어가고 delay 초(게임 시간)가 지날 때까지 (끝나면 바로 빠진다)</summary>
        private IEnumerator Until(QuakePhase phase, float delay = 0f)
        {
            while (!Ended && TL != null && !TL.Reached(phase, delay)) yield return null;
        }

        private void Checklist(bool gas = true, bool breaker = true, bool door = true, bool shoes = true, bool bag = true)
        {
            if (gas) Use(QuakeKeys.GasValveMain);
            if (breaker) Use(QuakeKeys.Breaker);
            if (door) Use(QuakeKeys.FrontDoor);
            if (shoes) Use(QuakeKeys.ShoeCabinet);
            if (bag) Use(QuakeKeys.EmergencyBag);
        }

        private void CoverAtDesk() { Tp(0.75f, 0.02f, 6.41f, 0f); Crouch(true); }
        private void StandInLiving() { Crouch(false); Tp(3.5f, 0.02f, 4.0f, 0f); }

        private string Result()
        {
            if (!Ended) return "(끝나지 않음)";
            return !string.IsNullOrEmpty(DE.LastEndingId) ? "엔딩 " + DE.LastEndingId : "사망 " + DE.LastDeathReason;
        }

        /// <summary>분기 하나: 씬 새로 불러오기 → 조작 스크립트 → 결과 대기 → 기대값 비교</summary>
        private IEnumerator Branch(string name, Func<IEnumerator> script, string expected, Action after = null, float magnitude = 6.7f)
        {
            string only = Environment.GetEnvironmentVariable("S02BRANCH_ONLY");   // 예: "F,H" — 이 글자로 시작하는 분기만
            if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), name.Substring(0, 1)) < 0) yield break;
            QuakeTimeline.ForcedMagnitude = magnitude;   // 기본은 6점대 강진 (기존 분기 기대값 기준)
            yield return LoadFresh();
            if (player == null || TL == null) { Check(name, false, "준비 실패 (player/timeline 없음)"); yield break; }
            Time.timeScale = Speed;
            Coroutine run = StartCoroutine(script());
            float limit = 0f;
            foreach (QuakePhase p in Enum.GetValues(typeof(QuakePhase))) if (p != QuakePhase.Tsunami) limit += TL.DurationOf(p);
            limit += TL.Config.tsunamiHoldEnd + 30f;
            float t0 = Time.time;
            while (!Ended && Time.time - t0 < limit) yield return null;
            StopCoroutine(run);
            yield return RealWait(0.3f);
            string got = Result();
            Check(name, got == expected, $"기대={expected} / 실제={got} (규모 {TL?.Magnitude:0.0}, 단계 {TL?.Phase} +{TL?.PhaseElapsed:0.0}s, 구역 {QuakeAreas.Instance?.Current})" + (branchErrors > 0 ? $" / 에러로그 {branchErrors}" : ""));
            after?.Invoke();
            Time.timeScale = 1f;
        }

        // ═════════════════════ 전체 ═════════════════════
        private IEnumerator RunAll()
        {
            yield return RealWait(0.5f);

            // ── 클리어 3종 ──
            float riseBeforeTsunami = 0f, riseDrawback = 0f; int collapsed = 0, collapseTargets = 0;
            yield return Branch("A. 모범 플레이 → 뒷산 대피소", IdealRun, "엔딩 " + QuakeKeys.EndingShelter, ScoreReport);
            yield return Branch("L. 옥외대피장소에 머묾 → 해일 (대피지역이라 간신히 생존)", () => StayAt(-70f, -28.45f, 56f, true, r => riseBeforeTsunami = r, r => riseDrawback = r), "엔딩 " + QuakeKeys.EndingProtected,
                () => { QuakeWaveCollapse wc = FindFirstObjectByType<QuakeWaveCollapse>(); if (wc != null) { collapseTargets = CountTargets(wc); collapsed = CountCollapsed(wc); } });
            yield return Branch("M. 뒷산 비탈(대피소 밖) 높은 곳 → 해일 (높은 곳에서 버팀)", () => StayAt(-15f, -10f, -66f, false, null, null), "엔딩 " + QuakeKeys.EndingVertical);

            // ── 사망 분기 ──
            yield return Branch("B. 본진 때 몸 보호 안 함", NoMainCover, "사망 " + DeathReason.QuakeFallingObjects);
            yield return Branch("C. 가스 밸브 안 잠금", () => Prepared(gas: false), "사망 " + DeathReason.QuakeGasExplosion);
            yield return Branch("D. 차단기 안 내림", () => Prepared(breaker: false), "사망 " + DeathReason.QuakeElectricFire);
            yield return Branch("E. 현관문 안 열고 집에 머묾", () => Prepared(door: false), "사망 " + DeathReason.QuakeTrappedInside);
            yield return Branch("F. 첫 흔들림 중 선 채로 걸어 다님", WalkWhileShaking, "사망 " + DeathReason.QuakeFellWhileShaking);
            yield return Branch("G. 지진 뒤 엘리베이터 탑승", ElevatorRide, "사망 " + DeathReason.QuakeElevatorTrapped);
            yield return Branch("H. 조치 다 하고 집에서 여진까지 몸 보호하며 머묾 → 건물 폭발", StayHomeCovered, "사망 " + DeathReason.QuakeBuildingExplosion);
            yield return Branch("I. 본진 뒤 계단으로 위층(11층) 올라감", ClimbUp, "사망 " + DeathReason.QuakeUpperFloorCollapse);
            yield return Branch("J. 여진 때 건물 바로 옆(외벽 7m)", () => StayAt(-30f, -28.45f, -1.6f, true, null, null), "사망 " + DeathReason.QuakeFacadeDebris);
            yield return Branch("K. 건물에서 떨어진 바깥 평지(대피장소 아님) → 해일", () => StayAt(-40f, -28.45f, 10f, true, null, null), "사망 " + DeathReason.Tsunami);

            // ── 규모별 옥상 수직대피 ──
            string alertWeak = "", alertStrong = "";
            yield return Branch("N. [6점대] 지진 전 옥상에 올라가 웅크려 버팀 → 건물 붕괴", RooftopFromStart, "사망 " + DeathReason.QuakeBuildingCollapse,
                () => alertStrong = QuakeClues.Fill("{M} / 지진해일 {TS}", TL), 6.4f);
            yield return Branch("O. [5점대] 지진 전 옥상에 올라가 웅크려 버팀 → 높은 곳에서 버팀", RooftopFromStart, "엔딩 " + QuakeKeys.EndingVertical,
                () => alertWeak = QuakeClues.Fill("{M} / 지진해일 {TS}", TL), 5.4f);
            yield return Branch("P. [5점대] 본진 뒤 계단으로 위층 거쳐 옥상 → 높은 곳에서 버팀", ClimbToRoof, "엔딩 " + QuakeKeys.EndingVertical, null, 5.8f);
            yield return Branch("Q. [6점대] 본진 뒤 계단으로 옥상 가려다 → 위층 붕괴", ClimbToRoof, "사망 " + DeathReason.QuakeUpperFloorCollapse, null, 6.0f);
            yield return Branch("R. [5점대] 옥상에서 서 있음 (웅크리지 않음) → 본진 판정 사망", RooftopStanding, "사망 " + DeathReason.QuakeFallingObjects, null, 5.0f);
            yield return Branch("S. [5점대] 집에 머묾 → 건물 폭발 (집 안은 여전히 위험)", StayHomeCovered, "사망 " + DeathReason.QuakeBuildingExplosion, null, 5.9f);
            yield return Branch("T. 가스 조치 안 하고 일찌감치 건물에서 멀리 나가 있음 → 폭발 사망 없음 (해일에 휩쓸림)", () => OutEarlyNoGas(-40f, -28.45f, 10f), "사망 " + DeathReason.Tsunami);
            yield return Branch("U. 가스 조치 안 하고 건물 바로 옆(외벽 7m)으로 나가 있음 → 가스 폭발 사망", () => OutEarlyNoGas(-30f, -28.45f, -1.6f), "사망 " + DeathReason.QuakeGasExplosion);
            Check("재난문자: 5점대는 지진해일 주의보", alertWeak == "5.4 / 지진해일 주의보", alertWeak);
            Check("재난문자: 6점대는 지진해일 경보", alertStrong == "6.4 / 지진해일 경보", alertStrong);
            MagnitudeOdds();

            // ── 연출 ──
            Check("강 수위: 해일 전까지 불어남 (+0.4m 이상)", riseBeforeTsunami >= 0.4f, $"+{riseBeforeTsunami:0.00}m");
            Check("강 수위: 해일 직전 물이 빠짐 (원래보다 낮음)", riseDrawback < 0f, $"{riseDrawback:0.00}m");
            Check("해일 붕괴: 대상 발견", collapseTargets > 0, $"{collapseTargets}개");
            Check("해일 붕괴: 실제로 무너짐", collapsed > 0, $"{collapsed}/{collapseTargets}개");

            Finish();
        }

        private static int CountTargets(QuakeWaveCollapse wc) => GetTargets(wc)?.Count ?? 0;
        private static int CountCollapsed(QuakeWaveCollapse wc)
        {
            IList list = GetTargets(wc); if (list == null) return 0;
            int n = 0;
            foreach (object o in list) { var f = o.GetType().GetField("startTime"); if (f != null && (float)f.GetValue(o) >= 0f) n++; }
            return n;
        }
        private static IList GetTargets(QuakeWaveCollapse wc) => typeof(QuakeWaveCollapse).GetField("targets", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(wc) as IList;

        /// <summary>규모 뽑기 2000번: 6점대 약 70%, 범위 5.0~6.9</summary>
        private void MagnitudeOdds()
        {
            QuakeTimeline.ForcedMagnitude = 0f;
            QuakeTimeline tl = FindFirstObjectByType<QuakeTimeline>();
            MethodInfo roll = typeof(QuakeTimeline).GetMethod("RollMagnitude", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tl == null || roll == null) { Check("규모 뽑기", false, "timeline/RollMagnitude 없음"); return; }
            int strong = 0, n = 2000; float min = 99f, max = 0f;
            for (int i = 0; i < n; i++) { float m = (float)roll.Invoke(tl, null); if (m >= 6f) strong++; min = Mathf.Min(min, m); max = Mathf.Max(max, m); }
            float ratio = strong / (float)n;
            Check("규모 뽑기: 6점대 약 70%", ratio > 0.65f && ratio < 0.75f, $"{ratio:P1}");
            Check("규모 뽑기: 5.0~6.9 범위", min >= 4.99f && max <= 6.91f, $"{min:0.0}~{max:0.0}");
        }

        private void Finish()
        {
            QuakeTimeline.ForcedMagnitude = 0f;
            Time.timeScale = 1f;
            Check("실행 중 에러 로그 없음", errorLogs == 0, errorLogs > 0 ? string.Join(" | ", errorSamples) : "");
            lines.Add($"[S02Branch] branch test done: PASS {pass} / FAIL {fail}");
            Debug.Log($"[S02Branch] branch test done: PASS {pass} / FAIL {fail}");
            try { File.WriteAllLines(Path.Combine(Application.dataPath, "..", "smoke_results_s02_branches.txt"), lines); } catch (Exception) { }
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fail == 0 ? 0 : 3);
            else UnityEditor.EditorApplication.isPlaying = false;
#endif
            Destroy(gameObject);
        }

        private void ScoreReport()
        {
            ScoreSystem s = ScoreSystem.Instance;
            if (s == null) { Info("ScoreSystem 없음"); return; }
            if (!s.IsFinalized) s.FinalizeScore(0f);
            Info($"점수 {s.Total}/{s.MaxTotal} 등급 {s.GetGradeLabel()}");
            foreach (ScoreSystem.Line l in s.Breakdown) if (l.earned < l.max) Info($"놓친 항목: {l.label} ({l.earned}/{l.max})");
            Check("A. 모범 플레이 등급 S", s.GetGradeLabel() == "S", s.GetGradeLabel());
        }

        // ═════════════════════ 분기 스크립트 ═════════════════════
        /// <summary>전진 탁자 밑 → 준비 시간에 조치 → 본진 탁자 밑 → 이웃 부축 → 옥외대피장소 → 해일 경보 뒤 뒷산 대피소</summary>
        private IEnumerator IdealRun()
        {
            yield return Until(QuakePhase.Calm, 2f);
            CoverAtDesk();
            yield return Until(QuakePhase.Prepare, 1f);
            Crouch(false);
            Checklist();
            yield return Until(QuakePhase.Prepare, 50f);
            CoverAtDesk();
            yield return Until(QuakePhase.Aftermath, 1f);
            Crouch(false);
            // 6층 층참의 다친 이웃 부축 (보너스)
            Use(QuakeKeys.Neighbor);
            Tp(-70f, -28.45f, 56f);   // 지진 옥외대피장소
            yield return Until(QuakePhase.Tsunami, 1f);
            Tp(-15f, -14.45f, -99f);  // 뒷산 지진해일 대피소
        }

        /// <summary>전진에는 보호, 조치는 다 했지만 본진 때 거실 한가운데 서 있음</summary>
        private IEnumerator NoMainCover()
        {
            yield return Until(QuakePhase.Calm, 2f);
            CoverAtDesk();
            yield return Until(QuakePhase.Prepare, 1f);
            Crouch(false);
            Checklist();
            yield return Until(QuakePhase.Prepare, 50f);
            StandInLiving();
        }

        /// <summary>두 번의 흔들림은 탁자 밑, 조치는 선택, 본진 뒤에도 집 안에 머묾</summary>
        private IEnumerator Prepared(bool gas = true, bool breaker = true, bool door = true)
        {
            yield return Until(QuakePhase.Calm, 2f);
            CoverAtDesk();
            yield return Until(QuakePhase.Prepare, 1f);
            Crouch(false);
            Checklist(gas, breaker, door);
            yield return Until(QuakePhase.Prepare, 50f);
            CoverAtDesk();
            yield return Until(QuakePhase.Aftermath, 1f);
            Crouch(false);
        }

        private IEnumerator WalkWhileShaking()
        {
            yield return Until(QuakePhase.Calm, 2f);
            StandInLiving();
            yield return Until(QuakePhase.Foreshock, 3.5f);   // 유예 2.5초 뒤
            Vector3 start = player.transform.position; float path = 0f;
            for (int i = 0; i < 5000 && !Ended && TL.Phase == QuakePhase.Foreshock; i++)
            {
                Vector3 before = player.transform.position;
                cc.Move(new Vector3(((int)(Time.time / 0.6f) % 2 == 0 ? 1.5f : -1.5f) * Time.deltaTime, -0.01f, 0f));   // 실제 걷는 속도로 좌우 왕복
                path += Vector3.Distance(before, player.transform.position);
                yield return null;
            }
            Info($"F 진단: 이동 경로 {path:0.00}m, 웅크림={player.IsCrouching}, 세기={TL.Intensity:0.00}, 단계={TL.Phase} +{TL.PhaseElapsed:0.0}s, cc.enabled={cc.enabled}");
        }

        private IEnumerator StayHomeCovered()
        {
            yield return Prepared();
            yield return Until(QuakePhase.Aftermath, 40f);
            CoverAtDesk();   // 여진 대비 (흔들리지 않는 단계에서 이동)
            yield return Until(QuakePhase.Tsunami, 1f);
            Crouch(false);
        }

        private IEnumerator ElevatorRide()
        {
            yield return Until(QuakePhase.Calm, 2f);
            CoverAtDesk();
            yield return Until(QuakePhase.Prepare, 1f);
            Crouch(false);
            Checklist();
            Tp(-8f, 0.02f, -1.0f, 0f);   // 10층 복도
            ElevatorController ctl = FindFirstObjectByType<ElevatorController>(FindObjectsInactive.Include);
            if (ctl == null) { Debug.LogError("[S02Branch] ElevatorController 없음"); yield break; }
            ctl.Call(player.transform);
            float t = Time.time;
            while (!Ended && ctl.State != ElevatorState.DoorOpen && Time.time - t < 20f) yield return null;
            ctl.Board();
        }

        private IEnumerator ClimbUp()
        {
            yield return Prepared();
            Tp(-18.8f, 0.05f, -0.85f, 0f);   // 10층 계단탑 층참 (북쪽 줄)
            Vector3[] path = { new Vector3(-23f, 0f, -0.85f), new Vector3(-23f, 0f, -2.4f), new Vector3(-18.8f, 0f, -2.4f), new Vector3(-18.8f, 0f, -0.85f) };
            float vVel = 0f;
            for (int lap = 0; lap < 2 && !Ended; lap++)
                foreach (Vector3 target in path)
                {
                    for (int i = 0; i < 600 && !Ended; i++)
                    {
                        Vector3 d = W(target.x, 0f, target.z) - cc.transform.position; d.y = 0f;
                        if (d.magnitude < 0.08f) break;
                        vVel = Mathf.Max(vVel - 20f * 0.02f, -10f);
                        CollisionFlags f = cc.Move(d.normalized * Mathf.Min(0.06f, d.magnitude) + Vector3.up * vVel * 0.02f);
                        if ((f & CollisionFlags.Below) != 0) vVel = -2f;
                        if (i % 4 == 0) yield return null;
                    }
                }
        }

        /// <summary>두 흔들림은 탁자 밑 + 조치, 본진 뒤 (x,y,z) 로 가서 끝까지 머문다</summary>
        private IEnumerator StayAt(float x, float y, float z, bool exactY, Action<float> riseBeforeTsunami, Action<float> riseDrawback)
        {
            yield return Prepared();
            Tp(x, y, z);
            if (!exactY) Settle();
            Transform water = FindRiverWater();
            yield return Until(QuakePhase.Aftershock, 9.5f);
            if (water != null) riseBeforeTsunami?.Invoke(water.localPosition.y - OriginalY(water));
            yield return Until(QuakePhase.Tsunami, TL != null ? TL.Config.tsunamiDrawbackEnd - 0.5f : 29f);
            if (water != null) riseDrawback?.Invoke(water.localPosition.y - OriginalY(water));
        }

        private IEnumerator RooftopFromStart()
        {
            yield return Until(QuakePhase.Calm, 2f);
            Checklist();
            Tp(-6f, 33.05f, 2f, 0f);   // 옥상 (지진 전에 올라감)
            Crouch(true);
        }

        /// <summary>조치 + 두 흔들림 보호 → 본진 뒤 계단으로 위층을 거쳐 옥상에 올라가 여진 동안 웅크린다</summary>
        private IEnumerator ClimbToRoof()
        {
            yield return ClimbUp();
            if (Ended) yield break;
            Tp(-6f, 33.05f, 2f, 0f);   // 옥상
            Crouch(true);
        }

        /// <summary>전진은 탁자 밑, 준비 시간 20초쯤에 가스는 안 잠그고 (x,y,z) 로 나가서 끝까지 머문다</summary>
        private IEnumerator OutEarlyNoGas(float x, float y, float z)
        {
            yield return Until(QuakePhase.Calm, 2f);
            CoverAtDesk();
            yield return Until(QuakePhase.Prepare, 1f);
            Crouch(false);
            Checklist(gas: false);
            yield return Until(QuakePhase.Prepare, 20f);
            Tp(x, y, z);
        }

        private IEnumerator RooftopStanding()
        {
            yield return Until(QuakePhase.Calm, 2f);
            Checklist();
            Tp(-6f, 33.05f, 2f, 0f);
            Crouch(false);
        }

        // 강 수위 측정: QuakeRiverRise 가 기억한 원래 높이를 읽는다
        private static Transform FindRiverWater()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == "EvacLandmarks") { Transform w = t.Find("River/Water"); if (w != null) return w; }
            return null;
        }
        private static float OriginalY(Transform water)
        {
            QuakeRiverRise r = FindFirstObjectByType<QuakeRiverRise>();
            if (r == null) return water.localPosition.y;
            var f = typeof(QuakeRiverRise).GetField("pieces", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(f?.GetValue(r) is Array arr)) return water.localPosition.y;
            foreach (object o in arr)
            {
                var tf = o.GetType().GetField("t"); var pf = o.GetType().GetField("basePos");   // 물을 물길에 맞게 두껍게 한 기준 위치
                if (tf != null && pf != null && (Transform)tf.GetValue(o) == water) return ((Vector3)pf.GetValue(o)).y;
            }
            return water.localPosition.y;
        }
    }
}
#endif
