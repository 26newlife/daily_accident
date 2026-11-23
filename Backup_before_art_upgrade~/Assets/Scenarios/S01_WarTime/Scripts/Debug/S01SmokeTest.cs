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
    /// 시나리오 1(전시 상황) 런타임 스모크 테스트 (에디터 플레이 / 개발 빌드 전용).
    /// 메뉴: Tools > GameUI > Debug > Run S01 Smoke Test. 결과는 Console 과 smoke_results_s01.txt 에 기록된다.
    ///   · 진 엔딩 경로(전 Act 를 선택·상호작용 호출로 진행) / 충격파 사망 / 섬광 사망 / 씁쓸한 엔딩 / 피폭 사망
    ///   · 재시작마다 구독·타이머가 누적되지 않는지
    /// 게임 시간은 timeScale 로 빠르게 돌린다.
    /// </summary>
    public class S01SmokeTest : MonoBehaviour
    {
        private const string SessionKey = "S01SmokeTest.Run";
        private const float Speed = 6f;
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
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<GameData.ScenarioData>("Assets/Core/Data/Scenarios/Scenario_01.asset");
            GameSession.SelectScenario(data);
            var go = new GameObject("S01SmokeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<S01SmokeTest>();
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
            string line = $"[S01Smoke] {(ok ? "PASS" : "FAIL")}  {name}  {detail}";
            lines.Add(line);
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private void Info(string s) { lines.Add("[S01Smoke] " + s); Debug.Log("[S01Smoke] " + s); }

        private IEnumerator Wait(float real) { for (float t = 0f; t < real; t += Time.unscaledDeltaTime) yield return null; }
        private IEnumerator WaitUntil(Func<bool> c, float timeout) { float t = 0f; while (!c() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; } }

        private IEnumerator LoadFresh()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
            yield return null; yield return null;
            float t = 0f;
            while ((ScenarioRunner.Instance == null || !ScenarioRunner.Instance.IsReady || GameClock.Instance == null || DeathAndEnding.Instance == null) && t < 25f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Wait(0.5f);
            Time.timeScale = Speed;
        }

        private static WarState W => WarState.Instance;
        private static ScenarioStateMachine M => ScenarioStateMachine.Instance;
        private static DeathAndEnding DE => DeathAndEnding.Instance;
        private static PlayerController Player => FindFirstObjectByType<PlayerController>();
        private static void Use(string id) { InteractableObject o = InteractableObject.FindById(id); if (o != null) o.TryInteract(null); else Debug.LogWarning("[S01Smoke] missing interactable " + id); }
        private static object Call(object target, string method, params object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null) throw new MissingMethodException(target.GetType().Name + "." + method);
            return m.Invoke(target, args);
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        private static ActBase ActAt(int i) => ScenarioRunner.Instance.Root.Acts[i];
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

        // ═════════════════════ 전체 ═════════════════════
        private IEnumerator RunAll()
        {
            yield return Wait(0.5f);
            int baseline = -1;
            yield return Safe("진 엔딩 경로", FullTrueRoute());
            baseline = EventBus.SubscriptionCount();
            yield return Safe("충격파 사망(창가에 서 있음)", DeathRoute(false, DeathReason.ShockwaveDebris));
            yield return Safe("섬광 사망(밖으로 나감)", DeathRoute(true, DeathReason.NuclearFlash));
            yield return Safe("씁쓸한 엔딩(동료 이탈)", BitterRoute());
            yield return Safe("피폭 사망(방독면·요오드 없음)", RadiationRoute());
            yield return Safe("한파 사망", ColdRoute());
            yield return LoadFresh();
            Check("재시작 후 구독 수 유지", EventBus.SubscriptionCount() <= baseline + 2, $"baseline={baseline} now={EventBus.SubscriptionCount()}");
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Check("실행 중 에러 로그 없음", errorLogs == 0, errorLogs > 0 ? string.Join(" | ", errorSamples) : "");
            lines.Add($"[S01Smoke] smoke test done: PASS {pass} / FAIL {fail}");
            Debug.Log($"[S01Smoke] smoke test done: PASS {pass} / FAIL {fail}");
            try { File.WriteAllLines(Path.Combine(Application.dataPath, "..", "smoke_results_s01.txt"), lines); } catch (Exception) { }
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(fail == 0 ? 0 : 3);
            else UnityEditor.EditorApplication.isPlaying = false;
#endif
            Destroy(gameObject);
        }

        // ═════════════════════ 진 엔딩 경로 ═════════════════════
        private IEnumerator FullTrueRoute()
        {
            yield return LoadFresh();
            Check("시나리오 S01 로드", ScenarioRunner.Instance.Definition != null && ScenarioRunner.Instance.Definition.Id == "S01", ScenarioRunner.Instance.Definition != null ? ScenarioRunner.Instance.Definition.Id : "null");
            Check("Act 6개", ScenarioRunner.Instance.Root.Acts.Length == 6);
            Check("WarState/HUD", W != null && WarHud.Instance != null);

            // 격리: 시나리오 3(화재) 소품·시스템이 없어야 한다
            CheckIsolation();

            // Act 1 — 파밍 튜토리얼 + 전쟁 발발 연출
            Check("Act 1 진행 중", M.CurrentIndex == 0);
            Check("전쟁 발발 전: 평화로운 하늘", WarOutbreak.Instance != null && !WarOutbreak.Instance.Started);
            Use(WarKeys.Radio); Use(WarKeys.PickupSupplies);
            yield return WaitUntil(() => WarOutbreak.Instance.Started, 8f);
            Check("재난문자 직후 전쟁 발발 연출 시작", WarOutbreak.Instance.Started);
            yield return WaitUntil(() => M.CurrentIndex == 1, 12f);
            Check("Act 1 → 2 (필수품 확보)", M.CurrentIndex == 1);
            Check("라디오·식량 확보", W.HasRadio && W.Food >= 4 && W.Water >= 4, $"food={W.Food} water={W.Water}");
            yield return WaitUntil(() => WarOutbreak.Instance.SkyTransitionDone, 20f);
            Check("하늘·조명 전시 색감 전환 완료", WarOutbreak.Instance.SkyTransitionDone && RenderSettings.sun != null && RenderSettings.sun.intensity < 0.7f && RenderSettings.sun.color.r > RenderSettings.sun.color.b, $"sun={(RenderSettings.sun != null ? RenderSettings.sun.intensity : -1f):0.00}");
            Check("연기 기둥 생성", WarOutbreak.Instance.PlumeCount == W.Config.plumeCount && WarOutbreak.Instance.PlumeCount > 0, "plumes=" + WarOutbreak.Instance.PlumeCount);
            WarFlyovers fly = FindFirstObjectByType<WarFlyovers>();
            yield return WaitUntil(() => fly.FlightsSpawned >= 2 || WarOutbreak.Instance.FlashCount >= 1 && fly.FlightsSpawned >= 1, 40f);
            Check("전투기·헬기 편대 비행 스폰", fly.FlightsSpawned >= 1, "flights=" + fly.FlightsSpawned);
            yield return WaitUntil(() => WarOutbreak.Instance.FlashCount >= 1, 30f);
            Check("포격 섬광", WarOutbreak.Instance.FlashCount >= 1, "flashes=" + WarOutbreak.Instance.FlashCount);
            WarAircraft any = FindFirstObjectByType<WarAircraft>();
            Check("비행 기체 사운드(도플러)", any != null && any.GetComponentInChildren<AudioSource>(true) != null && any.GetComponentInChildren<AudioSource>(true).dopplerLevel > 1f);

            // Act 2 — 대비 (요새화·물물교환 없음, 집 안 수색)
            Use(WarKeys.CivilDefenseBox); Use(WarKeys.PickupPantry); Use(WarKeys.PickupMedkit); Use(WarKeys.PickupPipe); Use(WarKeys.PickupBlanket); Use(WarKeys.PickupClothes);
            Check("민방위 장비함: 방독면", InventorySystem.Instance.Find(WarKeys.ItemGasMask) != null);
            Check("약상자: 요오드화 칼륨", InventorySystem.Instance.Find(WarKeys.ItemIodine) != null);
            Check("식량·물·약품·무기·보온 확보", W.Food >= 7 && W.Water >= 6 && W.Medicine >= 3 && W.HasWeapon && W.Comfort >= 30f, $"food={W.Food} water={W.Water} med={W.Medicine} comfort={W.Comfort}");
            WarRadio radio = FindFirstObjectByType<WarRadio>();
            radio.Tune(0); radio.Tune(1); radio.Tune(2);
            WarWindows.OnWindowOpened("window_living");
            Check("단파 청취·창밖 관찰로 기만 단서 확보", W.DeceptionCluesCount >= 3, $"clues={W.DeceptionCluesCount}");
            InventorySystem inv = InventorySystem.Instance;
            for (int i = 0; i < inv.MaxSlots; i++)
            {
                ItemData it = inv.GetItem(i);
                if (it != null && (it.Id == WarKeys.ItemIodine || it.Id == WarKeys.ItemGasMask)) inv.Use(i);
            }
            Check("요오드 복용(낙진 전) / 방독면 착용", W.IodineTaken && W.IodineBeforeFallout && W.GasMaskWorn);
            W.AddFood(8); W.AddWater(8);   // 테스트 전용: 동료 3명 + 낙진 구간 소비를 감당할 만큼 모았다고 가정
            M.NextAct();   // Act 2 의 타이머/행동력 종료는 별도 경로 — 여기서는 전환만 확인
            Check("Act 3 진입", M.CurrentIndex == 2);

            // Act 3 — 방문자 / 약탈자
            yield return Wait(0.2f);
            for (int round = 0; round < 4; round++)
            {
                Act3_IsolatedArk a3 = (Act3_IsolatedArk)ActAt(2);
                yield return WaitUntil(() => Field<int>(a3, "pendingVisitor") >= 0 || M.CurrentIndex != 2, 40f);
                int pending = Field<int>(a3, "pendingVisitor");
                if (M.CurrentIndex != 2 || pending < 0) break;
                if (pending < 3) Call(a3, "Accept", pending); else Call(a3, "Defend");
                yield return Wait(0.3f);
            }
            Check("방문자 3명 합류", W.Joined == 3 && W.Refused == 0, $"joined={W.Joined} refused={W.Refused}");
            Check("합류 NPC 등장", FindObjectsByType<WarSurvivor>(FindObjectsInactive.Exclude).Length == 3);
            yield return WaitUntil(() => M.CurrentIndex == 3, 80f);
            Check("Act 4 진입 (기만된 평화)", M.CurrentIndex == 3);

            // Act 4 — 설득
            Act4_FalsePeace a4 = (Act4_FalsePeace)ActAt(3);
            yield return WaitUntil(() => ScenarioFlags.GetBool(WarKeys.FlagPeaceBroadcast), 20f);
            Check("거짓 종전 방송", ScenarioFlags.GetBool(WarKeys.FlagPeaceBroadcast));
            Check("단서 충분 → 설득 가능", W.CluesEnough);
            Call(a4, "Persuade");
            Check("설득 성공", ScenarioFlags.GetBool(WarKeys.FlagPersuaded));
            yield return WaitUntil(() => M.CurrentIndex == 4, 40f);
            Check("Act 5 진입 (섬광)", M.CurrentIndex == 4);

            // Act 5 — 섬광/낙진
            yield return WaitUntil(() => W.Phase == WarPhase.Fallout || GameFlow.IsEnded, 20f);
            Check("섬광·충격파 생존 → 낙진 시작", W.Phase == WarPhase.Fallout && ScenarioFlags.GetBool(WarKeys.FlagSurvivedFlash) && !GameFlow.IsEnded);
            yield return WaitUntil(() => M.CurrentIndex == 5 || GameFlow.IsEnded, 60f);
            Check("낙진 피폭 관리 성공", !GameFlow.IsEnded && W.Radiation < 60f && W.BodyTemp > 10f, $"rad={W.Radiation:0.0} temp={W.BodyTemp:0.0} food={W.Food} water={W.Water}");

            // Act 6 — 엔딩
            yield return WaitUntil(() => DE.HasEnded, 30f);
            Check("진 엔딩", DE.LastEndingId == WarKeys.EndingTrue, "ending=" + DE.LastEndingId);
        }

        // ═════════════════════ 사망 경로 ═════════════════════
        private void CheckIsolation()
        {
            bool s03Loaded = SceneManager.GetSceneByName("S03_ApartmentFire").isLoaded;
            Check("격리: 시나리오 3 씬 미로드", !s03Loaded);
            Check("격리: FireSource/가스 누출/화재 관리자 없음", FindObjectsByType<FireSource>(FindObjectsInactive.Include).Length == 0 && FindObjectsByType<GasLeakController>(FindObjectsInactive.Include).Length == 0 && FindObjectsByType<FireManager>(FindObjectsInactive.Include).Length == 0);
            int active = 0, total = 0; string first = "";
            foreach (FireHazardProp p in FireHazardProp.FindAllIncludingInactive())
            {
                total++;
                if (p.gameObject.activeInHierarchy) { active++; if (first.Length == 0) first = p.name; }
            }
            Check("격리: 화재 소품(FireHazardProp) 활성 0개", total > 0 && active == 0, total == 0 ? "태그된 소품이 없음 (베이스 태그 누락)" : active > 0 ? "활성: " + first : "숨김 " + total + "개");
            Check("격리: 요새화·물물교환 오브젝트 없음", FindObjectsByType<InteractableObject>(FindObjectsInactive.Include).Length > 0 && InteractableObject.FindById("war_barter") == null && InteractableObject.FindById("war_station_rain") == null && InteractableObject.FindById("war_station_toilet") == null && InteractableObject.FindById("war_door_barricade") == null);
        }

        private IEnumerator DeathRoute(bool goOutside, DeathReason expected)
        {
            yield return LoadFresh();
            if (goOutside) Player.Teleport(new Vector3(0.5f, 0.02f, -1.1f), 270f);   // 거짓 종전 방송에 속아 복도로 나갔다
            else
            {
                // 창가에 서 있다 (웅크리지 않음)
                InteractableObject win = InteractableObject.FindById("window_living");
                Vector3 p = win.transform.position; p.y = 0.02f; p.z -= 0.7f;
                Player.Teleport(p, 0f);
            }
            M.StartAct(4);   // Act 5 로 바로 점프
            yield return WaitUntil(() => DE.HasEnded, 30f);
            Check($"사망 판정 {expected}", DE.HasEnded && DE.LastDeathReason == expected, "reason=" + DE.LastDeathReason);
        }

        private IEnumerator BitterRoute()
        {
            yield return LoadFresh();
            W.Join(WarKeys.SurvivorChild);
            W.LoseSurvivor();
            M.StartAct(5);
            yield return WaitUntil(() => DE.HasEnded, 40f);
            Check("씁쓸한 생존 엔딩", DE.LastEndingId == WarKeys.EndingBitter, "ending=" + DE.LastEndingId);
        }

        private IEnumerator RadiationRoute()
        {
            yield return LoadFresh();
            M.SetActs(new ActBase[0]);   // Act 진행이 단계를 덮어쓰지 않게 멈춘다
            W.AddFood(30); W.AddWater(30); W.AddComfort(100f);   // 피폭만 따로 검증 (한파·아사가 먼저 오지 않게)
            W.MarkAllWindowsBroken();
            W.SetPhase(WarPhase.Fallout);
            yield return WaitUntil(() => DE.HasEnded, 60f);
            Check("피폭 사망", DE.HasEnded && DE.LastDeathReason == DeathReason.RadiationSickness, $"reason={DE.LastDeathReason} rad={W.Radiation:0.0}");
        }

        private IEnumerator ColdRoute()
        {
            yield return LoadFresh();
            M.SetActs(new ActBase[0]);
            W.AddFood(20); W.AddWater(20);
            W.MarkAllWindowsBroken();
            W.SetPhase(WarPhase.Fallout);
            // 피폭보다 체온이 먼저 한계에 닿도록 피폭을 억제한다 (테스트 전용)
            float savedRate = W.Config.baseRadiationPerSecond;
            W.Config.baseRadiationPerSecond = 0f;
            yield return WaitUntil(() => DE.HasEnded, 90f);
            W.Config.baseRadiationPerSecond = savedRate;   // 에셋 값 복원 (에디터에서 실행해도 사용자 설정이 바뀌지 않게)
            Check("한파 사망", DE.HasEnded && DE.LastDeathReason == DeathReason.Hypothermia, $"reason={DE.LastDeathReason} temp={W.BodyTemp:0.0}");
        }
    }
}
#endif
