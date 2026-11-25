using System.Collections.Generic;
using System.IO;
using GameCore;
using GameData;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 2(자연재해 — 지진·지진해일) 빌더. 원본 QuakeSurvival(Bumsoo 브랜치) 게임을 공통 베이스 집에 이식한 시나리오 씬과 데이터를 만든다.
    /// 집(GameScene 의 Apartment_1004_Base)은 그대로 두고, 시나리오 전용 오브젝트·데이터를 Additive 씬 S02_Disaster.unity 와
    /// Assets/Scenarios/S02_Disaster/Data 에 만든다. 비상계단(1~20층·옥상)은 집의 StairwellExtension 을 useWalkableStairwell 로 켠다.
    /// 메뉴: Tools > GameUI > Scenarios > Build S02 Disaster
    /// </summary>
    public static partial class MainMenuBuilder
    {
        public const string S02Dir = "Assets/Scenarios/S02_Disaster";
        public const string S02ScenePath = S02Dir + "/Scenes/S02_Disaster.unity";
        private const string S02DataDir = S02Dir + "/Data";
        private const string S02AudioDir = S02Dir + "/Audio";
        private const string S02FxDir = S02Dir + "/Fx";
        private const string S02ScenarioDataPath = "Assets/Core/Data/Scenarios/Scenario_02.asset";
        private const string QuakeSynthDir = "Assets/_Project/Audio/Synth";
        private const float S02Ground = -28.5f;

        [MenuItem("Tools/GameUI/Scenarios/Build S02 Disaster", priority = 302)]
        public static void BuildDisasterScenarioMenu()
        {
            if (!EditorUtility.DisplayDialog("S02 자연재해 빌드", "시나리오 2(지진·지진해일) 씬과 데이터를 만듭니다.\n기존 S02_Disaster 씬은 다시 만들어지고, 이미 있는 데이터 에셋(수치 조정 포함)은 유지됩니다.\n집(GameScene)과 시나리오 1·3 은 건드리지 않습니다.", "빌드", "취소")) return;
            BuildDisasterScenario();
        }

        public static void BuildDisasterScenario()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string d in new[] { S02Dir, S02Dir + "/Scenes", S02DataDir, S02AudioDir, S02FxDir }) EnsureFolder(d);
            CopyQuakeAudio();
            EnsureQuakeData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool hasBase = File.Exists(GameScenePath);
            Scene baseScene = default;
            if (hasBase) { baseScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single); Physics.SyncTransforms(); }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, hasBase ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            QuakeAssets a = ReloadQuakeAssets();
            BuildQuakeContent(scene, a);
            EditorSceneManager.SaveScene(scene, S02ScenePath);
            if (hasBase) { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(baseScene); }

            RegisterScene(S02ScenePath);
            LinkScenarioData(S02ScenarioDataPath, a.definition);
            var sd = AssetDatabase.LoadAssetAtPath<ScenarioData>(S02ScenarioDataPath);
            if (sd != null) { var so = SO(sd); SerializedProperty sp = so.FindProperty("standaloneSceneName"); if (sp != null) sp.stringValue = ""; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(sd); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[S02Builder] 시나리오 2(자연재해) 빌드 완료: " + S02ScenePath);
        }

        // ═════════════════════ 소리 (원본 합성음 복사 — 새 GUID) ═════════════════════
        private static readonly string[] QuakeClips =
        {
            "breaker_switch", "building_creak", "disaster_alert", "door_creak", "explosion", "fire_crackle_loop", "gas_valve_turn", "glass_crunch",
            "object_clatter", "ocean_roar_loop", "phone_bad_call_loop", "phone_text_sent", "quake_rumble_loop", "rain_inside_loop", "rain_outside_loop", "thunder", "tsunami_siren_loop",
        };

        private static void CopyQuakeAudio()
        {
            foreach (string c in QuakeClips)
            {
                string src = $"{QuakeSynthDir}/{c}.wav", dst = $"{S02AudioDir}/{c}.wav";
                if (File.Exists(dst)) continue;
                if (!File.Exists(src)) { Debug.LogWarning("[S02Builder] 원본 소리가 없습니다: " + src); continue; }
                File.Copy(src, dst);
                AssetDatabase.ImportAsset(dst);
            }
        }

        private static AudioClip QClip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{S02AudioDir}/{name}.wav");

        // ═════════════════════ 데이터 ═════════════════════
        private sealed class QuakeAssets
        {
            public QuakeConfig config;
            public ObjectiveData o1, o2, o3, o4;
            public DeathReasonData[] deaths;
            public DeathReasonData fallback;
            public EndingData[] endings;
            public ScoreRules rules;
            public EnvironmentPreset storm;
            public ScenarioDefinition definition;
            public Material fire, smoke, fireball, rain, debris, water, waveBody, waveCrest, grass, earth, crack, pit, sign, signText, cone, rubble;
        }

        private static readonly (string file, DeathReason reason, string title, string why, string correct)[] QuakeDeaths =
        {
            ("Death_FallingObjects", DeathReason.QuakeFallingObjects, "떨어진 물건에 맞았습니다", "흔들리는 동안, 머리 위를 막아 줄 튼튼한 무언가가 필요했다...", "흔들릴 때는 튼튼한 탁자 아래로 들어가 탁자 다리를 붙잡고 머리와 몸을 보호한다."),
            ("Death_GasExplosion", DeathReason.QuakeGasExplosion, "가스 폭발이 일어났습니다", "첫 흔들림이 멈췄을 때, 부엌에서 나던 냄새를 그냥 두지 말았어야 했다...", "흔들림이 멈추면 가스 밸브(중간 밸브·메인 밸브)를 잠가 화재를 막는다."),
            ("Death_ElectricFire", DeathReason.QuakeElectricFire, "누전으로 불이 났습니다", "천장에서 새는 빗물과 집 안의 전기... 더 큰 흔들림이 오기 전에 막을 방법이 있었다...", "흔들림이 멈추면 전기 차단기를 내려 누전과 화재를 막는다."),
            ("Death_TrappedInside", DeathReason.QuakeTrappedInside, "집 안에 갇혔습니다", "건물이 뒤틀리면 닫힌 것은 다시 열리지 않는다. 나갈 길을 미리 만들어 뒀어야 했다...", "흔들림이 멈추면 현관문을 열어 출구를 확보한다."),
            ("Death_StairFall", DeathReason.QuakeStairFall, "계단에서 굴러 떨어졌습니다", "흔들리는 계단 위에 그대로 서 있었다... 몸을 낮추고 버텼어야 했다...", "계단에서 흔들림을 만나면 벽 쪽으로 붙어 자세를 낮추고 난간을 잡은 채 흔들림이 멈추기를 기다린다."),
            ("Death_ElevatorTrapped", DeathReason.QuakeElevatorTrapped, "엘리베이터에 갇혔습니다", "지진이 나면 가장 먼저 끊기는 것은 전기다. 그 전기로 움직이는 상자에 몸을 맡기면 안 됐다...", "지진 중·지진 뒤에는 엘리베이터 대신 계단을 이용한다."),
            ("Death_FacadeDebris", DeathReason.QuakeFacadeDebris, "떨어진 외벽 조각에 맞았습니다", "건물 바로 옆은 흔들릴 때 가장 위험하다. 위에서 떨어지는 것이 닿지 않는 곳이 필요했다...", "건물 밖에서는 건물·담장·전봇대에서 떨어진 넓은 곳으로 대피한다."),
            ("Death_Tsunami", DeathReason.Tsunami, "지진해일에 휩쓸렸습니다", "강물이 이상하게 멀리 물러나 있었다... 물이 닿지 않을 곳을 찾아야 했다...", "지진해일 경보가 울리면 물가에서 먼 높은 곳(지정 대피소)으로 즉시 대피한다."),
            ("Death_BuildingExplosion", DeathReason.QuakeBuildingExplosion, "건물 폭발에 휘말렸습니다", "흔들림이 멈춘 뒤에도 무너져 가는 건물 안에 너무 오래 머물렀다... 서둘러 밖으로 나가야 했다...", "흔들림이 멈추면 계단으로 서둘러 건물 밖 넓은 곳으로 대피한다."),
            ("Death_ExplosionDebris", DeathReason.QuakeExplosionDebris, "날아온 잔해에 맞았습니다", "건물 가까이에 머물렀다... 무슨 일이 생겨도 닿지 않을 만큼 멀리, 넓은 곳으로 가야 했다...", "건물에서 충분히 떨어진 지진 옥외대피장소(공터·운동장)로 이동한다."),
            ("Death_Sinkhole", DeathReason.QuakeSinkhole, "꺼진 땅속으로 떨어졌습니다", "발밑에서 땅이 갈라지는 소리가 들렸다... 금이 가기 시작한 곳에서는 바로 벗어났어야 했다...", "땅이 울리고 금이 가는 곳에서는 즉시 벗어나 단단한 지면으로 돌아간다."),
            ("Death_UpperFloorCollapse", DeathReason.QuakeUpperFloorCollapse, "무너지는 위층에 깔렸습니다", "크게 흔들린 건물의 위층은 언제 무너질지 모른다... 대피는 언제나 아래로, 밖으로 향해야 했다...", "지진 대피는 계단을 이용해 아래로, 건물 밖으로 한다."),
            ("Death_FellWhileShaking", DeathReason.QuakeFellWhileShaking, "흔들리는 중에 넘어졌습니다", "땅이 울리는 동안 선 채로 돌아다녔다... 그 자리에서 몸을 낮추고 흔들림이 멈추기를 기다렸어야 했다...", "흔들리는 동안에는 이동하지 말고 그 자리에서 자세를 낮춰 몸을 보호한다."),
            ("Death_BuildingCollapse", DeathReason.QuakeBuildingCollapse, "손상된 건물이 무너졌습니다", "규모 6이 넘는 강진에 건물은 겉보기보다 크게 손상돼 있었다... 옥상으로 피하는 수직대피는 건물이 버틸 때만 통하는 최후의 수단이었다...", "강한 지진 뒤에는 손상된 건물 위로 올라가지 말고, 계단으로 내려가 건물 밖 넓은 곳을 거쳐 고지대 대피소로 대피한다."),
            ("Death_GroundCrack", DeathReason.QuakeGroundCrack, "갈라진 땅 틈에 빠졌습니다", "검게 벌어진 틈을 그대로 밟았다... 돌아가거나 뛰어넘었어야 했다...", "갈라진 땅은 밟지 말고 돌아가거나 안전하게 건너뛴다."),
        };

        private static void EnsureQuakeData()
        {
            Ensure<QuakeConfig>($"{S02DataDir}/QuakeConfig.asset", so => { });

            S02Obj("Obj_S02_A1", "s02_a1_prepare", "흔들림에 대비한다", "갑자기 흔들리기 시작했다. 주변을 살피고 다음 흔들림에 대비하자.");
            S02Obj("Obj_S02_A2", "s02_a2_survive", "큰 흔들림에서 살아남는다", "훨씬 큰 흔들림이 덮쳤다.");
            S02Obj("Obj_S02_A3", "s02_a3_evacuate", "건물 밖 안전한 곳으로 대피한다", "흔들림이 멈췄다. 이 건물 안은 더 이상 안전하지 않다.");
            S02Obj("Obj_S02_A4", "s02_a4_tsunami", "지진해일을 피해 높은 곳으로 대피한다", "지진해일 경보가 발령됐다.");

            int unknownIdx = System.Array.IndexOf(System.Enum.GetValues(typeof(DeathReason)), DeathReason.Unknown);
            foreach (var d in QuakeDeaths)
            {
                int idx = System.Array.IndexOf(System.Enum.GetValues(typeof(DeathReason)), d.reason);
                Ensure<DeathReasonData>($"{S02DataDir}/DeathReasons/{d.file}.asset", so =>
                {
                    so.FindProperty("reason").enumValueIndex = idx;
                    so.FindProperty("title").stringValue = d.title;
                    so.FindProperty("whyYouDied").stringValue = d.why;
                    so.FindProperty("correctAction").stringValue = d.correct;
                });
            }
            Ensure<DeathReasonData>($"{S02DataDir}/DeathReasons/Death_Unknown.asset", so =>
            {
                so.FindProperty("reason").enumValueIndex = unknownIdx;
                so.FindProperty("title").stringValue = "사망";
                so.FindProperty("whyYouDied").stringValue = "재난 속에서 목숨을 잃었다.";
                so.FindProperty("correctAction").stringValue = "흔들릴 때는 몸을 보호하고, 멈추면 가스·전기를 차단한 뒤 계단으로 건물 밖 넓은 곳으로 대피한다.";
            });

            S02Ending("Ending_Shelter", QuakeKeys.EndingShelter, "생존했습니다", "고지대 대피소", "뒷산 지진해일 대피소에 도착했다. 아래에서는 거센 물이 단지를 휩쓸고 지나간다... 가족이 걱정하고 있을 것이다.", true);
            S02Ending("Ending_Vertical", QuakeKeys.EndingVertical, "간신히 버텼습니다", "높은 곳에서 버팀", "대피소까지는 가지 못했지만, 물이 닿지 않는 높은 곳에서 해일이 지나가기를 버텼다.", true);
            S02Ending("Ending_Protected", QuakeKeys.EndingProtected, "간신히 살아남았습니다", "대피지역 덕분", "대피지역 안이라 간신히 무사했다... 하지만 몇 번이나 정말 위험한 순간이 있었다.", true);

            string rulesPath = $"{S02DataDir}/S02_ScoreRules.asset";
            var rules = AssetDatabase.LoadAssetAtPath<ScoreRules>(rulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<ScoreRules>();
                rules.rules = new[]
                {
                    Rule("cover_foreshock", "흔들릴 때는 탁자 아래로 들어가 몸을 보호한다 (첫 흔들림)", 10, QuakeKeys.FlagCoverForeshock, "첫 흔들림 때 몸을 보호했다.", "흔들리기 시작하면 바로 튼튼한 탁자 아래로 들어가 몸을 보호한다."),
                    Rule("gas", "흔들림이 멈추면 가스 밸브를 잠근다", 10, QuakeKeys.FlagGas, "가스 밸브를 잠갔다.", "흔들림이 멈추면 가스 밸브를 잠가 화재를 막는다."),
                    Rule("breaker", "흔들림이 멈추면 전기 차단기를 내린다", 10, QuakeKeys.FlagBreaker, "전기 차단기를 내렸다.", "흔들림이 멈추면 전기 차단기를 내려 누전을 막는다."),
                    Rule("door", "문을 열어 출구를 확보한다", 10, QuakeKeys.FlagDoor, "현관문을 열어 출구를 확보했다.", "건물이 뒤틀리기 전에 문을 열어 출구를 확보한다."),
                    Rule("shoes", "깨진 유리에 다치지 않도록 신발을 신는다", 5, QuakeKeys.FlagShoes, "신발을 신어 발을 보호했다.", "대피 전에 신발을 신어 깨진 유리로부터 발을 보호한다."),
                    Rule("no_elevator", "엘리베이터 대신 계단을 이용한다", 10, QuakeKeys.FlagNoElevator, "엘리베이터 대신 계단을 이용했다.", "지진 중에는 엘리베이터를 이용하지 않는다."),
                    Rule("cover_main", "큰 흔들림이 오면 그 자리에서 자세를 낮추고 몸을 보호한다 (본진)", 10, QuakeKeys.FlagCoverMainShock, "본진 때 몸을 보호했다.", "큰 흔들림에서는 그 자리에서 자세를 낮추고 머리를 보호한다."),
                    Rule("cover_after", "건물 밖에서는 건물과 떨어진 넓은 곳으로 대피한다 (여진)", 10, QuakeKeys.FlagCoverAftershock, "여진 때 건물에서 떨어진 넓은 곳에 있었다.", "건물 밖에서는 외벽에서 떨어진 넓은 곳으로 피한다."),
                    Rule("outdoor_site", "가까운 지진 옥외대피장소(단지 공터 등)로 이동한다", 10, QuakeKeys.FlagOutdoorSite, "지진 옥외대피장소로 이동했다.", "흔들림이 멈추면 가까운 지진 옥외대피장소로 이동한다."),
                    Rule("shelter", "지진해일 경보 시 물가에서 먼 높은 곳으로 대피한다", 15, QuakeKeys.FlagShelter, "고지대 지진해일 대피소로 대피했다.", "지진해일 경보가 울리면 즉시 지정된 고지대 대피소로 간다."),
                    Rule("no_calls", "대피 중에는 통화를 자제한다", 5, QuakeKeys.FlagNoCalls, "대피 중 통화를 자제했다.", "대피 중에는 통화를 자제하고 문자를 이용한다 (통신망 과부하)."),
                    Rule("no_prevented", "대피지역에 기대지 않고 스스로 위험을 피한다", 15, QuakeKeys.FlagNoPrevented, "위험한 순간 없이 대피했다.", "대피지역 안이라 간신히 넘긴 위험이 있었다. 행동요령을 지켜 위험 자체를 피하자."),
                    Rule("bag", "(보너스) 비상 가방을 챙긴다", 5, QuakeKeys.FlagBag, "비상 가방을 챙겼다.", ""),
                    Rule("neighbor", "(보너스) 다친 이웃과 함께 대피한다", 10, QuakeKeys.FlagNeighbor, "다친 이웃과 함께 대피했다.", ""),
                    Rule("family_text", "(보너스) 대피 후 가족에게 문자로 안부를 알린다", 5, QuakeKeys.FlagFamilyText, "대피 후 가족에게 문자로 안부를 알렸다.", ""),
                };
                rules.grades = new[]
                {
                    new ScoreRules.GradeStep { minRatio = 0f, label = "C" },
                    new ScoreRules.GradeStep { minRatio = 0.55f, label = "B" },
                    new ScoreRules.GradeStep { minRatio = 0.75f, label = "A" },
                    new ScoreRules.GradeStep { minRatio = 0.9f, label = "S" },
                };
                AssetDatabase.CreateAsset(rules, rulesPath);
            }

            EnsureFolder(S02DataDir + "/Presets");
            string presetPath = $"{S02DataDir}/Presets/Env_S02_RainStorm.asset";
            if (AssetDatabase.LoadAssetAtPath<EnvironmentPreset>(presetPath) == null)
            {
                var p = ScriptableObject.CreateInstance<EnvironmentPreset>();
                p.displayName = "폭우 (지진)"; p.timeOfDay = TimeOfDayKind.Evening; p.windowView = WindowViewMode.RainStorm;
                p.applySun = true; p.sunEuler = new Vector3(18f, -40f, 0f); p.sunIntensity = 0.35f; p.sunColor = new Color(0.62f, 0.66f, 0.74f);
                p.applyAmbient = true; p.ambientColor = new Color(0.17f, 0.19f, 0.23f);
                p.applyFog = true; p.fogColor = new Color(0.36f, 0.4f, 0.46f); p.fogDensity = 0.006f;
                p.applyTraffic = true; p.pedestrians = false; p.vehicles = false; p.trafficDensity = 0f;
                AssetDatabase.CreateAsset(p, presetPath);
            }
        }

        private static ScoreRules.Rule Rule(string id, string label, int pts, string flag, string good, string bad)
            => new ScoreRules.Rule { id = id, label = label, maxPoints = pts, kind = ScoreRules.Kind.FlagTrue, key = flag, goodText = good, badText = bad };

        private static void S02Obj(string file, string id, string title, string desc)
            => Ensure<ObjectiveData>($"{S02DataDir}/Objectives/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("isMain").boolValue = true;
            });

        private static void S02Ending(string file, string id, string name, string route, string desc, bool survival)
            => Ensure<EndingData>($"{S02DataDir}/Endings/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("endingName").stringValue = name;
                so.FindProperty("routeLabel").stringValue = route;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("isSurvival").boolValue = survival;
            });

        private static T S02Load<T>(string rel) where T : Object
        {
            string path = $"{S02DataDir}/{rel}";
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            if (o == null) Debug.LogError($"[S02Builder] 에셋을 읽지 못했습니다: {path}");
            return o;
        }

        private static QuakeAssets ReloadQuakeAssets()
        {
            var a = new QuakeAssets();
            a.config = S02Load<QuakeConfig>("QuakeConfig.asset");
            a.o1 = S02Load<ObjectiveData>("Objectives/Obj_S02_A1.asset"); a.o2 = S02Load<ObjectiveData>("Objectives/Obj_S02_A2.asset");
            a.o3 = S02Load<ObjectiveData>("Objectives/Obj_S02_A3.asset"); a.o4 = S02Load<ObjectiveData>("Objectives/Obj_S02_A4.asset");
            var deaths = new List<DeathReasonData>();
            foreach (var d in QuakeDeaths) deaths.Add(S02Load<DeathReasonData>($"DeathReasons/{d.file}.asset"));
            a.deaths = deaths.ToArray();
            a.fallback = S02Load<DeathReasonData>("DeathReasons/Death_Unknown.asset");
            a.endings = new[] { S02Load<EndingData>("Endings/Ending_Shelter.asset"), S02Load<EndingData>("Endings/Ending_Vertical.asset"), S02Load<EndingData>("Endings/Ending_Protected.asset") };
            a.rules = S02Load<ScoreRules>("S02_ScoreRules.asset");
            a.storm = S02Load<EnvironmentPreset>("Presets/Env_S02_RainStorm.asset");
            a.definition = S02Load<ScenarioDefinition>("S02_Definition.asset");
            UpdateS02Definition(a);
            QuakeMaterials(a);
            return a;
        }

        /// <summary>S02 정의를 플레이 가능 상태로 채운다 (스켈레톤에서 만들어진 에셋을 그대로 쓴다 — GUID 유지)</summary>
        private static void UpdateS02Definition(QuakeAssets a)
        {
            if (a.definition == null) return;
            var so = SO(a.definition);
            so.FindProperty("isPlayable").boolValue = true;
            so.FindProperty("targetPlayMinutes").floatValue = 8f;
            so.FindProperty("scenarioSceneName").stringValue = "S02_Disaster";
            so.FindProperty("startSpawnId").stringValue = "spawn_bedroom_bed";
            so.FindProperty("environmentPreset").objectReferenceValue = a.storm;
            so.FindProperty("description").stringValue = "폭우가 쏟아지는 저녁, 지진과 지진해일 속에서 살아남아라";
            SerializedProperty acts = so.FindProperty("actTypeNames");
            string[] names = { "Act1_Foreshock", "Act2_MainShock", "Act3_Evacuation", "Act4_Tsunami" };
            acts.arraySize = names.Length; for (int i = 0; i < names.Length; i++) acts.GetArrayElementAtIndex(i).stringValue = names[i];
            so.FindProperty("useRespiratoryGauge").boolValue = false;
            so.FindProperty("useGasHazard").boolValue = false;
            so.FindProperty("useFireProps").boolValue = true;      // 가스 메인 밸브·퓨즈콕(화재 소품 태그)을 쓴다. 불은 붙지 않는다 (FireManager 는 켜져 있어도 불씨가 없다)
            so.FindProperty("useElevatorHazard").boolValue = false; // S03 엘리베이터 위험 대신 S02 의 QuakeElevatorHazard
            so.FindProperty("useDoorPeephole").boolValue = true;
            so.FindProperty("useOutdoorTraffic").boolValue = false;
            so.FindProperty("useWalkableStairwell").boolValue = true;
            SetObjectArray(so.FindProperty("deathReasons"), a.deaths);
            so.FindProperty("fallbackDeath").objectReferenceValue = a.fallback;
            SetObjectArray(so.FindProperty("endings"), a.endings);
            so.FindProperty("scoreRules").objectReferenceValue = a.rules;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a.definition);
        }

        private static void QuakeMaterials(QuakeAssets a)
        {
            Texture2D dot = SoftDotTexture();
            a.fire = OM.Particle("S02_Fire", new Color(1f, 0.6f, 0.25f, 0.9f), dot, true);
            a.smoke = OM.Particle("S02_Smoke", new Color(0.3f, 0.29f, 0.28f, 0.8f), dot, false);
            a.rain = OM.Particle("S02_Rain", new Color(0.8f, 0.85f, 0.92f, 0.5f), dot, false);
            a.fireball = OM.Unlit("S02_Fireball", new Color(1f, 0.62f, 0.25f, 0.85f), null, true);
            a.debris = OM.Lit("S02_Debris", OM.Hex("8C8984"), 0.08f);
            a.water = GB("S02_Water", new Color(0.24f, 0.33f, 0.36f, 0.82f), false, true);
            a.waveBody = GB("S02_WaveBody", new Color(0.32f, 0.42f, 0.45f, 0.9f), false, true);
            a.waveCrest = OM.Lit("S02_WaveCrest", OM.Hex("E9EEF0"), 0.6f);
            a.grass = OM.Lit("S02_HillGrass", OM.Hex("4F7A3E"), 0.05f);
            a.earth = OM.Lit("S02_HillEarth", OM.Hex("6B5A44"), 0.05f);
            a.crack = OM.Unlit("S02_Crack", OM.Hex("0E0D0C"));
            a.pit = OM.Unlit("S02_Pit", OM.Hex("050505"));
            a.sign = OM.Lit("S02_Sign", OM.Hex("1F6FB2"), 0.3f);
            a.signText = OM.Lit("S02_SignPost", OM.Hex("C9CDD2"), 0.5f, 0.5f);
            a.cone = OM.Lit("S02_Cone", OM.Hex("F06A1D"), 0.3f);
            a.rubble = OM.Lit("S02_Rubble", OM.Hex("9B978F"), 0.05f);
        }

        private static Texture2D SoftDotTexture()
        {
            string path = $"{S02FxDir}/S02_SoftDot.asset";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t != null) return t;
            const int n = 64;
            t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "S02_SoftDot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply(true);
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        // ═════════════════════ 씬 내용 ═════════════════════
        private static float StairY(int floor) => (floor - 10) * 3f;

        /// <summary>집 콜라이더 기준으로 (x, z) 위의 표면 높이 (없으면 fallback)</summary>
        private static float SurfaceY(float x, float z, float fromY, float fallback)
            => Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out RaycastHit h, 4f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : fallback;

        private static void BuildQuakeContent(Scene scene, QuakeAssets a)
        {
            var contentGo = new GameObject("S02_Content");
            Transform content = contentGo.transform;
            Transform sys = Group("ScenarioSystems", content);
            Transform props = Group("Props", content);
            Transform outdoor = Group("Outdoor", content);

            // ── 시스템 ──
            GameObject s = sys.gameObject;
            var timeline = s.AddComponent<QuakeTimeline>(); SetObj(timeline, "config", a.config);
            var areas = s.AddComponent<QuakeAreas>();
            s.AddComponent<QuakeCoverTracker>();
            var checklist = s.AddComponent<QuakeChecklist>();
            var record = s.AddComponent<QuakeRecord>();
            var fx = s.AddComponent<QuakeFx>();
            WireObj(fx, ("fire", a.fire), ("smoke", a.smoke), ("fireball", a.fireball), ("rain", a.rain), ("debris", a.debris), ("explosionSound", QClip("explosion")), ("fireLoopSound", QClip("fire_crackle_loop")));
            var chain = s.AddComponent<QuakeChainDisaster>();
            var outcome = s.AddComponent<QuakeOutcome>(); SetObj(outcome, "chainDisaster", chain);
            var blast = s.AddComponent<QuakeBuildingExplosion>();
            var collapse = s.AddComponent<QuakeIndoorCollapse>();
            var tsunami = s.AddComponent<QuakeTsunami>();
            var rupture = s.AddComponent<QuakeGroundRupture>();
            s.AddComponent<QuakeShakeRule>();
            var glass = s.AddComponent<QuakeGlassInjury>();
            var upper = s.AddComponent<QuakeUpperCollapse>();
            var clues = s.AddComponent<QuakeClues>();
            var atmos = s.AddComponent<QuakeAtmosphere>();
            WireObj(atmos, ("rainOutside", QClip("rain_outside_loop")), ("rainInside", QClip("rain_inside_loop")), ("rumble", QClip("quake_rumble_loop")), ("siren", QClip("tsunami_siren_loop")),
                ("oceanRoar", QClip("ocean_roar_loop")), ("creak", QClip("building_creak")), ("clatter", QClip("object_clatter")), ("thunder", QClip("thunder")));
            var elevator = s.AddComponent<QuakeElevatorHazard>();

            // 대피소·옥외대피장소 범위
            Bounds shelterB = new Bounds(new Vector3(-15f, S02Ground + 15.5f, -99f), new Vector3(34f, 6f, 30f));
            Bounds siteB = new Bounds(new Vector3(-70f, S02Ground + 1.5f, 56f), new Vector3(44f, 4f, 24f));
            var aso = SO(areas);
            aso.FindProperty("shelter").boundsValue = shelterB;
            aso.FindProperty("outdoorSite").boundsValue = siteB;
            aso.ApplyModifiedPropertiesWithoutUndo();

            // 연쇄 폭발 경로 (10층 → 1층 계단참 → 출구) / 외벽 불
            var landings = new List<Vector3>();
            for (int f = 10; f >= 1; f--) landings.Add(new Vector3(-21f, StairY(f) + 1f, -1.6f));
            var cso = SO(chain);
            SetVectorArray(cso.FindProperty("stairLandings"), landings.ToArray());
            SetVectorArray(cso.FindProperty("facadeFires"), new[] { new Vector3(3.2f, 1.3f, 9.7f), new Vector3(6.9f, 1.3f, 3.5f) });
            cso.ApplyModifiedPropertiesWithoutUndo();

            // 건물 폭발: 아래층 경고 불 + 아래층 → 위층 폭발 지점 (남쪽·북쪽 외벽 번갈아)
            var bl = new List<Vector3>();
            for (int f = 2; f <= 20; f += 2) bl.Add(new Vector3(-4f + ((f / 2) % 2 == 0 ? 6f : -8f), StairY(f) + 1.5f, (f / 2) % 2 == 0 ? 9.9f : -4.5f));
            var bso = SO(blast);
            SetVectorArray(bso.FindProperty("warningFires"), new[] { new Vector3(-8f, StairY(3) + 1.5f, 9.9f), new Vector3(4f, StairY(4) + 1.5f, 9.9f), new Vector3(-12f, StairY(2) + 1.5f, -4.5f) });
            SetVectorArray(bso.FindProperty("blastPoints"), bl.ToArray());
            bso.ApplyModifiedPropertiesWithoutUndo();

            // ── 시나리오 소품 (집 안) ──
            BuildQuakeProps(props, a, collapse, glass, upper, checklist);

            // ── 다친 이웃 (6층 층참 구석) ──
            BuildNeighbor(props, a);

            // ── 몸 보호 구역 ──
            Transform covers = Group("CoverZones", content);
            Cover(covers, "Cover_Desk", new Vector3(0.75f, 0.5f, 6.41f), new Vector3(1.1f, 1.2f, 1.4f), true);
            Cover(covers, "Cover_CoffeeTable", new Vector3(4.85f, 0.5f, 5.98f), new Vector3(1.4f, 1.2f, 1.6f), true);
            Cover(covers, "Cover_Stairwell", new Vector3(-19.1f, 3.75f, -1.6f), new Vector3(10.4f, 65f, 3.6f), true);

            // ── 건물 밖: 균열/싱크홀 · 해일 · 길 안내판 ──
            // 강 · 지진 옥외대피장소 · 지진해일 대피소(뒷산)는 공통 바깥 풍경(OutdoorEvacLandmarks, 집 프리팹)에 있다. 여기서는 판정 범위(siteB/shelterB)만 쓴다
            BuildRupture(outdoor, a, rupture);
            BuildTsunami(outdoor, a, tsunami);
            BuildSignposts(outdoor, a);

            // ── 단서 ──
            BuildClues(clues);

            // ── 폰 화면 (대피 지도 + 가족 연락) ──
            QuakePhonePanel phone = BuildPhonePanel(content, a);

            // ── 부트스트랩 ──
            var boot = s.AddComponent<S02Bootstrap>();
            WireObj(boot, ("timeline", timeline), ("checklist", checklist), ("outcome", outcome), ("record", record), ("buildingExplosion", blast), ("indoorCollapse", collapse),
                ("tsunami", tsunami), ("rupture", rupture), ("glass", glass), ("upperCollapse", upper), ("clues", clues), ("phonePanel", phone), ("atmosphere", atmos), ("elevator", elevator));

            // ── Act ──
            Transform actsRoot = Group("Acts", sys);
            var act1 = AddAct<Act1_Foreshock>(actsRoot, "Act1_Foreshock", "흔들림의 시작", "폭우 속 저녁. 갑작스러운 흔들림과 재난문자, 다음 흔들림 대비.");
            var act2 = AddAct<Act2_MainShock>(actsRoot, "Act2_MainShock", "본진", "큰 흔들림, 정전, 창문 파손, 머리 위 낙하물.");
            var act3 = AddAct<Act3_Evacuation>(actsRoot, "Act3_Evacuation", "대피", "흔들림이 멈춘 뒤 계단으로 건물 밖 넓은 곳으로. 여진.");
            var act4 = AddAct<Act4_Tsunami>(actsRoot, "Act4_Tsunami", "지진해일", "지진해일 경보. 물이 닿지 않는 고지대 대피소로.");
            ActEnd(act1, a.o1, QuakePhase.MainShock, true);
            ActEnd(act2, a.o2, QuakePhase.Aftermath, true);
            ActEnd(act3, a.o3, QuakePhase.Tsunami, true);
            ActEnd(act4, a.o4, (QuakePhase)99, false);
            SetObj(act2, "glassBreak", QClip("glass_crunch"));

            // ── 루트 ──
            var rootGo = new GameObject("ScenarioSceneRoot");
            var sr = rootGo.AddComponent<ScenarioSceneRoot>();
            var rso = SO(sr);
            rso.FindProperty("contentRoot").objectReferenceValue = contentGo;
            SetObjectArray(rso.FindProperty("acts"), new ActBase[] { act1, act2, act3, act4 });
            SetObjectArray(rso.FindProperty("bootstraps"), new Component[] { boot });
            rso.ApplyModifiedPropertiesWithoutUndo();

            contentGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            SceneManager.MoveGameObjectToScene(contentGo, scene);
        }

        private static void ActEnd(ActBase act, ObjectiveData obj, QuakePhase endsAt, bool completeOnEnd)
        {
            var so = SO(act);
            so.FindProperty("objective").objectReferenceValue = obj;
            so.FindProperty("endsAt").intValue = (int)endsAt;
            so.FindProperty("completeObjectiveOnEnd").boolValue = completeOnEnd;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 집 안 소품 ═════════════════════
        private static void BuildQuakeProps(Transform props, QuakeAssets a, QuakeIndoorCollapse collapse, QuakeGlassInjury glass, QuakeUpperCollapse upper, QuakeChecklist checklist)
        {
            // 전기 차단기 (다용도실 남쪽 벽, 복도 쪽 벽 안쪽 면)
            var breakerGo = new GameObject("BreakerPanel");
            breakerGo.transform.SetParent(props, false);
            breakerGo.transform.position = new Vector3(5.5f, 1.65f, 0.17f);
            breakerGo.transform.localScale = Vector3.one * 1.6f;   // 흰 벽에 묻혀 눈에 안 띄어서 크게 (다용도실 문을 열면 정면 벽)
            Box(breakerGo.transform, "Panel", Vector3.zero, new Vector3(0.42f, 0.52f, 0.07f), GB("S02_BreakerBox", Hex("D8D9D6")));
            Box(breakerGo.transform, "Label", new Vector3(0f, 0.2f, 0.037f), new Vector3(0.3f, 0.06f, 0.005f), GB("S02_BreakerLabel", Hex("3A3F45")), false);
            var leverGo = Box(breakerGo.transform, "Lever", new Vector3(0f, 0.03f, 0.05f), new Vector3(0.08f, 0.16f, 0.05f), GB("S02_BreakerLever", Hex("2B2E33")), false);
            var breaker = breakerGo.AddComponent<ToggleInteractable>();
            var bso = SO(breaker);
            bso.FindProperty("id").stringValue = QuakeKeys.Breaker;
            bso.FindProperty("prompt").stringValue = "전기 차단기";
            bso.FindProperty("isOn").boolValue = true;
            bso.FindProperty("promptWhenOn").stringValue = "전기 차단기 내리기";
            bso.FindProperty("promptWhenOff").stringValue = "전기 차단기 올리기";
            bso.FindProperty("movingPart").objectReferenceValue = leverGo.transform;
            bso.FindProperty("onLocalPosition").vector3Value = new Vector3(0f, 0.03f, 0.05f);
            bso.FindProperty("offLocalPosition").vector3Value = new Vector3(0f, -0.07f, 0.05f);
            bso.FindProperty("moveDuration").floatValue = 0.25f;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // 비상 가방 (현관 신발장 위)
            float bagY = SurfaceY(3.88f, 1.1f, 2.3f, 1.0f);
            var bagGo = new GameObject("EmergencyBag");
            bagGo.transform.SetParent(props, false);
            bagGo.transform.position = new Vector3(3.88f, bagY, 1.1f);
            Box(bagGo.transform, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.34f, 0.34f, 0.2f), GB("S02_Bag", Hex("D9480F")));
            Box(bagGo.transform, "Strap", new Vector3(0f, 0.35f, 0f), new Vector3(0.22f, 0.04f, 0.04f), GB("S02_BagStrap", Hex("2B2E33")), false);
            var bag = bagGo.AddComponent<RemovableInteractable>();
            var bgso = SO(bag);
            bgso.FindProperty("id").stringValue = QuakeKeys.EmergencyBag;
            bgso.FindProperty("prompt").stringValue = "비상 가방 챙기기";
            bgso.FindProperty("message").stringValue = "비상 가방을 챙겼다. 물, 비상식량, 손전등, 구급약이 들어 있다.";
            bgso.FindProperty("holdSeconds").floatValue = 0.6f;
            bgso.ApplyModifiedPropertiesWithoutUndo();

            // 떨어지는 물건 (전진: 책 / 본진: 꽃병·상자)
            var fallers = new List<QuakeIndoorCollapse.Faller>();
            Material[] books = { GB("S02_BookA", Hex("8A4A44")), GB("S02_BookB", Hex("4A6580")), GB("S02_BookC", Hex("5C7F6A")) };
            float shelfTop = SurfaceY(6.03f, 7.13f, 2.38f, 1.8f);
            for (int i = 0; i < 3; i++)
                fallers.Add(Faller(props, $"Book_{i}", new Vector3(6.0f, shelfTop + 0.12f, 6.95f + i * 0.17f), new Vector3(0.2f, 0.24f, 0.05f), books[i], 0.6f, QuakePhase.Foreshock, 0.8f + i * 0.4f, new Vector3(-1.2f, 0f, 0f)));
            float tvTop = SurfaceY(2.84f, 6.25f, 2.3f, 0.5f);
            fallers.Add(Faller(props, "Vase", new Vector3(2.88f, tvTop + 0.16f, 6.25f), new Vector3(0.14f, 0.3f, 0.14f), GB("S02_Vase", Hex("6F8FA6")), 1.2f, QuakePhase.MainShock, 1.5f, new Vector3(1.0f, 0f, 0f)));
            float wardTop = SurfaceY(0.36f, 3.3f, 2.38f, 2.0f);
            fallers.Add(Faller(props, "StorageBox", new Vector3(0.4f, wardTop + 0.1f, 3.3f), new Vector3(0.45f, 0.2f, 0.35f), GB("S02_Box", Hex("B28B5E")), 4f, QuakePhase.MainShock, 4f, new Vector3(2.0f, 0f, 0f)));
            var colso = SO(collapse);
            SerializedProperty fp = colso.FindProperty("fallers");
            fp.arraySize = fallers.Count;
            for (int i = 0; i < fallers.Count; i++)
            {
                SerializedProperty e = fp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("body").objectReferenceValue = fallers[i].body;
                e.FindPropertyRelative("phase").intValue = (int)fallers[i].phase;
                e.FindPropertyRelative("delay").floatValue = fallers[i].delay;
                e.FindPropertyRelative("push").vector3Value = fallers[i].push;
            }
            colso.ApplyModifiedPropertiesWithoutUndo();

            // 깨진 유리 (현관 거울이 깨져 흩어짐 — 전진 때 나타남)
            Transform shardRoot = Group("GlassShards", props);
            Material shardMat = GB("S02_Shard", new Color(0.78f, 0.88f, 0.95f, 0.6f), false, true);
            var rng = new System.Random(202);
            var shardObjs = new List<Object>();
            for (int i = 0; i < 28; i++)
            {
                float x = 2.1f + (float)rng.NextDouble() * 1.25f, z = 0.55f + (float)rng.NextDouble() * 1.45f;
                float sz = 0.05f + (float)rng.NextDouble() * 0.09f;
                GameObject g = Box(shardRoot, "Shard", new Vector3(x, 0.012f, z), new Vector3(sz, 0.006f, sz * 0.7f), shardMat, false, new Vector3(0f, (float)rng.NextDouble() * 180f, 0f));
                shardObjs.Add(g);
            }
            var gso = SO(glass);
            SetObjectArray(gso.FindProperty("shards"), shardObjs.ToArray());
            SetVectorArray(gso.FindProperty("shardAreaCenters"), new[] { new Vector3(2.75f, 0.15f, 1.27f) });
            SetVectorArray(gso.FindProperty("shardAreaSizes"), new[] { new Vector3(1.4f, 0.5f, 1.55f) });
            gso.FindProperty("crunch").objectReferenceValue = QClip("glass_crunch");
            gso.ApplyModifiedPropertiesWithoutUndo();

            // 위층 붕괴 단서: 10~11층 반층참의 콘크리트 조각
            Transform rubble = Group("UpperCollapseClue", props);
            float my = StairY(10) + 1.5f;
            for (int i = 0; i < 7; i++)
            {
                float x = -23.6f + (float)rng.NextDouble() * 1.5f, z = -2.9f + (float)rng.NextDouble() * 2.6f, sz = 0.15f + (float)rng.NextDouble() * 0.25f;
                Box(rubble, "Rubble", new Vector3(x, my + sz * 0.3f, z), new Vector3(sz, sz * 0.6f, sz * 0.8f), a.rubble, false, new Vector3((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 180f, 0f));
            }
            Box(rubble, "WallCrack", new Vector3(-23.88f, my + 1.4f, -1.6f), new Vector3(0.02f, 1.6f, 0.06f), a.crack, false, new Vector3(18f, 0f, 0f));
            SetObj(upper, "clueRubble", rubble.gameObject);
            var uso = SO(upper);
            uso.FindProperty("deadlyY").floatValue = StairY(11) - 0.4f;
            uso.FindProperty("clueY").floatValue = StairY(10) + 0.9f;
            uso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static QuakeIndoorCollapse.Faller Faller(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float mass, QuakePhase phase, float delay, Vector3 push)
        {
            GameObject g = Box(parent, name, pos, size, mat, true);
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = mass; rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            return new QuakeIndoorCollapse.Faller { body = rb, phase = phase, delay = delay, push = push };
        }

        private static void BuildNeighbor(Transform props, QuakeAssets a)
        {
            var root = new GameObject("InjuredNeighbor");
            root.transform.SetParent(props, false);
            root.transform.position = new Vector3(-18.75f, StairY(6), -0.45f);
            root.transform.rotation = Quaternion.Euler(0f, 225f, 0f);
            GameObject body = Cyl(root.transform, "Body", new Vector3(0f, 0.5f, 0f), 0.25f, 1.0f, GB("S02_Neighbor", Hex("B07D62")), true);
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Box(root.transform, "Bandage", new Vector3(0.12f, 0.12f, 0.2f), new Vector3(0.12f, 0.12f, 0.3f), GB("S02_Bandage", Hex("F2F2F2")), false);
            var n = root.AddComponent<QuakeInjuredNeighbor>();
            var so = SO(n);
            so.FindProperty("id").stringValue = QuakeKeys.Neighbor;
            so.FindProperty("prompt").stringValue = "다친 이웃 부축하기";
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.FindProperty("holdSeconds").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Cover(Transform parent, string name, Vector3 pos, Vector3 size, bool crouch)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<QuakeCoverZone>();
            var so = SO(c);
            so.FindProperty("size").vector3Value = size;
            so.FindProperty("requireCrouch").boolValue = crouch;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 건물 밖 ═════════════════════
        private static void BuildRupture(Transform parent, QuakeAssets a, QuakeGroundRupture rupture)
        {
            Transform g = Group("GroundRupture", parent);
            var cracks = new List<(Transform pivot, QuakePhase phase, float delay, float open, Vector3 hazard)>();
            Transform Crack(string name, Vector3 start, float yaw, float length, float width)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(g, false);
                pivot.position = start; pivot.rotation = Quaternion.Euler(0f, yaw, 0f);
                pivot.localScale = new Vector3(length, 1f, 1f);
                Box(pivot, "Gap", new Vector3(0.5f, 0.012f, 0f), new Vector3(1f, 0.02f, width), a.crack, false);
                return pivot;
            }
            // 출구 서쪽 잔디밭을 가로지르는 균열 (북쪽 뒷산·남서쪽 공터 어느 쪽으로 가도 지나간다 — 점프로 넘는다)
            cracks.Add((Crack("Crack_West", new Vector3(-34f, S02Ground, -14f), -90f, 30f, 0.7f), QuakePhase.Aftermath, 4f, 2.5f, new Vector3(30f, 0.3f, 0.7f)));
            cracks.Add((Crack("Crack_Lawn", new Vector3(-64f, S02Ground, 30f), 0f, 16f, 0.6f), QuakePhase.Aftershock, 1f, 2f, new Vector3(16f, 0.3f, 0.6f)));
            cracks.Add((Crack("Crack_North", new Vector3(-28f, S02Ground, -38f), 0f, 24f, 0.65f), QuakePhase.Tsunami, 6f, 2.5f, new Vector3(24f, 0.3f, 0.65f)));

            var holes = new List<(Transform pit, QuakePhase phase, float delay, float radius)>();
            Transform Hole(string name, Vector3 c, float r)
            {
                var pit = new GameObject(name).transform;
                pit.SetParent(g, false);
                pit.position = c;
                Cyl(pit, "Pit", new Vector3(0f, 0.01f, 0f), r, 0.02f, a.pit, false);
                var rng = new System.Random(name.GetHashCode());
                for (int i = 0; i < 10; i++)
                {
                    float t = i / 10f * Mathf.PI * 2f, sz = 0.3f + (float)rng.NextDouble() * 0.5f;
                    Box(pit, "Rim", new Vector3(Mathf.Cos(t) * r * 1.05f, 0.1f, Mathf.Sin(t) * r * 1.05f), new Vector3(sz, 0.2f, sz * 0.8f), a.rubble, false, new Vector3(0f, t * Mathf.Rad2Deg, 10f));
                }
                return pit;
            }
            holes.Add((Hole("Sinkhole_NW", new Vector3(-30f, S02Ground, -28f), 2.6f), QuakePhase.Aftermath, 20f, 2.6f));
            holes.Add((Hole("Sinkhole_Path", new Vector3(-12f, S02Ground, -44f), 3f), QuakePhase.Tsunami, 12f, 3f));

            var so = SO(rupture);
            SerializedProperty cp = so.FindProperty("cracks");
            cp.arraySize = cracks.Count;
            for (int i = 0; i < cracks.Count; i++)
            {
                SerializedProperty e = cp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pivot").objectReferenceValue = cracks[i].pivot;
                e.FindPropertyRelative("phase").intValue = (int)cracks[i].phase;
                e.FindPropertyRelative("delay").floatValue = cracks[i].delay;
                e.FindPropertyRelative("openSeconds").floatValue = cracks[i].open;
                e.FindPropertyRelative("hazardSize").vector3Value = cracks[i].hazard;
            }
            SerializedProperty hp = so.FindProperty("sinkholes");
            hp.arraySize = holes.Count;
            for (int i = 0; i < holes.Count; i++)
            {
                SerializedProperty e = hp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("pit").objectReferenceValue = holes[i].pit;
                e.FindPropertyRelative("phase").intValue = (int)holes[i].phase;
                e.FindPropertyRelative("delay").floatValue = holes[i].delay;
                e.FindPropertyRelative("radius").floatValue = holes[i].radius;
            }
            so.FindProperty("rumble").objectReferenceValue = QClip("quake_rumble_loop");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTsunami(Transform parent, QuakeAssets a, QuakeTsunami tsunami)
        {
            Transform g = Group("Tsunami", parent);
            GameObject water = Box(g, "Water", new Vector3(0f, S02Ground - 15f, 900f), new Vector3(2400f, 30f, 1200f), a.water, false);
            water.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject body = Box(g, "WaveBody", new Vector3(0f, S02Ground, 300f), new Vector3(2400f, 1f, 4f), a.waveBody, false);
            body.SetActive(false);
            GameObject crest = Box(g, "WaveCrest", new Vector3(0f, S02Ground, 300f), new Vector3(2400f, 0.7f, 1.8f), a.waveCrest, false);
            crest.SetActive(false);
            var so = SO(tsunami);
            so.FindProperty("water").objectReferenceValue = water.transform;
            so.FindProperty("waveBody").objectReferenceValue = body.transform;
            so.FindProperty("waveCrest").objectReferenceValue = crest.transform;
            so.FindProperty("groundY").floatValue = S02Ground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSignposts(Transform parent, QuakeAssets a)
        {
            Transform g = Group("Signposts", parent);
            Color blue = new Color(0.12f, 0.45f, 0.75f), green = new Color(0.13f, 0.55f, 0.35f);
            Sign(g, a, "Sign_Exit_Site", new Vector3(-28.5f, S02Ground, 2.5f), 225f, "← 지진 옥외대피장소\n(놀이터 옆 공터)", blue);
            Sign(g, a, "Sign_Exit_Shelter", new Vector3(-28.5f, S02Ground, -6f), 270f, "지진해일 대피소 →\n(뒷산 고지대)", green);
            Sign(g, a, "Sign_Path_Shelter", new Vector3(-22f, S02Ground, -46f), 180f, "지진해일 대피소 ↑", green);
            Sign(g, a, "Sign_Lawn_Site", new Vector3(-46f, S02Ground, 26f), 225f, "← 지진 옥외대피장소", blue);
        }

        private static void Sign(Transform parent, QuakeAssets a, string name, Vector3 foot, float yaw, string text, Color color)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = foot; root.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(root, "Post", new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.08f), a.signText, true);
            Material m = OM.Lit("S02_Sign_" + ColorUtility.ToHtmlStringRGB(color), color, 0.3f);
            Box(root, "Board", new Vector3(0f, 2.1f, 0.06f), new Vector3(1.9f, 0.75f, 0.05f), m, false);
            var t = new GameObject("Text").AddComponent<TextMeshPro>();
            t.transform.SetParent(root, false);
            t.transform.localPosition = new Vector3(0f, 2.1f, 0.09f);
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR SDF.asset");
            if (font != null) t.font = font;
            t.text = text; t.fontSize = 2.2f; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(1.8f, 0.7f);
        }

        // ═════════════════════ 단서 ═════════════════════
        private static void BuildClues(QuakeClues clues)
        {
            var list = new (QuakePhase phase, float delay, QuakeClues.ClueKind kind, string text, string skip, string sound, Vector3 at, Vector3 spark)[]
            {
                (QuakePhase.Calm, 3f, QuakeClues.ClueKind.Subtitle, "(창밖으로 폭우가 쏟아진다. 불어난 강물이 거칠게 흐른다...)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Foreshock, 0.5f, QuakeClues.ClueKind.Subtitle, "(갑자기 바닥이 흔들리기 시작했다!)", "", "building_creak", new Vector3(3f, 1.5f, 4f), new Vector3(0f, -999f, 0f)),
                (QuakePhase.Prepare, 0.5f, QuakeClues.ClueKind.Alert, "[긴급재난문자] 인근 해역 규모 4.2 지진 발생\n낙하물로부터 몸 보호, 진동 멈춘 후 가까운 옥외대피장소로 대피하며 여진 주의", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Prepare, 6f, QuakeClues.ClueKind.Scent, "부엌 쪽에서 매캐한 가스 냄새가 난다...", QuakeKeys.ActGas, "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Prepare, 16f, QuakeClues.ClueKind.Subtitle, "(천장에서 빗물이 떨어진다. 콘센트에서 '탁' 하고 불꽃이 튀었다...)", QuakeKeys.ActBreaker, "breaker_switch", new Vector3(3.0f, 0.3f, 4.6f), new Vector3(3.0f, 0.3f, 4.6f)),
                (QuakePhase.Prepare, 28f, QuakeClues.ClueKind.Subtitle, "(현관 문틀이 삐걱거리는 소리가 난다...)", QuakeKeys.ActDoor, "door_creak", new Vector3(3f, 1.2f, 0.2f), new Vector3(0f, -999f, 0f)),
                (QuakePhase.Prepare, 50f, QuakeClues.ClueKind.Subtitle, "(바닥 아래에서 낮게 우르릉거리는 소리가 커진다...)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Aftermath, 1.5f, QuakeClues.ClueKind.Subtitle, "(흔들림이 멈췄다... 이 건물 안은 더 이상 안전하지 않다.)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                // {M} 이번 판 본진 규모, {DMG} 규모별 안내, {TS} 지진해일 경보/주의보 (QuakeClues.Fill 이 채운다)
                (QuakePhase.Aftermath, 5f, QuakeClues.ClueKind.Alert, "[긴급재난문자] 인근 해역 규모 {M} 지진 발생\n{DMG}", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Aftershock, 0.3f, QuakeClues.ClueKind.Subtitle, "(또 흔들린다! 여진이다!)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Aftershock, 2f, QuakeClues.ClueKind.Alert, "[긴급재난문자] 규모 {M} 지진 영향으로 지진해일 {TS} 발령\n강가·해안 접근 금지, 즉시 높은 곳으로 대피 바랍니다", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Tsunami, 2f, QuakeClues.ClueKind.Subtitle, "(멀리서 사이렌이 길게 울린다...)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Tsunami, 14f, QuakeClues.ClueKind.Subtitle, "(강물이 이상할 정도로 멀리 빠져나가고 있다...)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
                (QuakePhase.Tsunami, 32f, QuakeClues.ClueKind.Subtitle, "(강 쪽에서 굉음이 밀려온다!)", "", "", Vector3.zero, new Vector3(0f, -999f, 0f)),
            };
            var so = SO(clues);
            SerializedProperty p = so.FindProperty("clues");
            p.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                SerializedProperty e = p.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("phase").intValue = (int)list[i].phase;
                e.FindPropertyRelative("delay").floatValue = list[i].delay;
                e.FindPropertyRelative("kind").intValue = (int)list[i].kind;
                e.FindPropertyRelative("text").stringValue = list[i].text;
                e.FindPropertyRelative("skipIfDone").stringValue = list[i].skip;
                e.FindPropertyRelative("sound").objectReferenceValue = string.IsNullOrEmpty(list[i].sound) ? null : QClip(list[i].sound);
                e.FindPropertyRelative("soundAt").vector3Value = list[i].at;
                e.FindPropertyRelative("sparkAt").vector3Value = list[i].spark;
            }
            so.FindProperty("alertSound").objectReferenceValue = QClip("disaster_alert");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 폰 화면 (대피 지도 + 가족 연락) ═════════════════════
        private static QuakePhonePanel BuildPhonePanel(Transform parent, QuakeAssets a)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR SDF.asset");
            var canvasGo = new GameObject("S02_PhoneCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = UiRect("Panel", canvasGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(460f, 640f), new Vector2(0f, 0.5f));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            UiImage(panel, new Color(0.06f, 0.07f, 0.09f, 0.92f));
            UiText(panel, "Title", "대피 지도", font, 30, new Vector2(0f, -18f), new Vector2(420f, 44f), TextAlignmentOptions.Left, Color.white);

            RectTransform map = UiRect("Map", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(420f, 420f), new Vector2(0.5f, 1f));
            UiImage(map, new Color(0.18f, 0.24f, 0.18f, 1f));
            // 월드 → 지도: x -110~50, z -130(위, 북)~75(아래, 남)
            Vector4 wr = new Vector4(-110f, 50f, -130f, 75f);
            void Area(string n, float wx0, float wx1, float wz0, float wz1, Color c, string label)
            {
                float u0 = Mathf.InverseLerp(wr.x, wr.y, wx0), u1 = Mathf.InverseLerp(wr.x, wr.y, wx1);
                float v0 = 1f - Mathf.InverseLerp(wr.z, wr.w, wz1), v1 = 1f - Mathf.InverseLerp(wr.z, wr.w, wz0);
                RectTransform r = UiRect(n, map, new Vector2(u0, v0), new Vector2(u1, v1), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                UiImage(r, c);
                if (!string.IsNullOrEmpty(label)) UiText(r, "Label", label, font, 17, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
            }
            Area("River", -110f, 50f, 68f, 75f, new Color(0.2f, 0.42f, 0.65f), "강 (지진해일 주의)");
            Area("Road", -110f, 50f, 30f, 38f, new Color(0.3f, 0.3f, 0.32f), "");
            Area("Hill", -42f, 12f, -118f, -52f, new Color(0.3f, 0.5f, 0.25f), "");
            Area("Shelter", -32f, 2f, -114f, -84f, new Color(0.13f, 0.55f, 0.35f), "지진해일\n대피소");
            Area("Site", -92f, -48f, 44f, 68f, new Color(0.12f, 0.45f, 0.75f), "지진\n옥외대피장소");
            Area("Building", -24f, 10f, -4f, 9.4f, new Color(0.75f, 0.72f, 0.66f), "우리 동");
            UiText(map, "North", "N ↑", font, 18, new Vector2(-14f, -10f), new Vector2(60f, 26f), TextAlignmentOptions.Right, Color.white, false, new Vector2(1f, 1f));
            RectTransform you = UiRect("You", map, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));
            UiImage(you, new Color(0.95f, 0.2f, 0.2f));

            // 가족 연락 버튼
            RectTransform btn = UiRect("ContactButton", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(420f, 56f), new Vector2(0.5f, 0f));
            Image bimg = UiImage(btn, new Color(0.2f, 0.36f, 0.6f));
            var button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = bimg;
            TMP_Text blabel = UiText(btn, "Label", "가족에게 전화하기", font, 24, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, Color.white, true);
            TMP_Text status = UiText(panel, "Status", "", font, 18, new Vector2(0f, 20f), new Vector2(420f, 56f), TextAlignmentOptions.Center, new Color(0.85f, 0.88f, 0.92f), false, new Vector2(0.5f, 0f));

            var comp = canvasGo.AddComponent<QuakePhonePanel>();
            var so = SO(comp);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("mapArea").objectReferenceValue = map;
            so.FindProperty("youMarker").objectReferenceValue = you;
            so.FindProperty("contactButton").objectReferenceValue = button;
            so.FindProperty("contactLabel").objectReferenceValue = blabel;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("worldRect").vector4Value = wr;
            so.FindProperty("badCallLoop").objectReferenceValue = QClip("phone_bad_call_loop");
            so.FindProperty("textSent").objectReferenceValue = QClip("phone_text_sent");
            so.ApplyModifiedPropertiesWithoutUndo();
            return comp;
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        private static Image UiImage(RectTransform r, Color c)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        private static TMP_Text UiText(Transform parent, string name, string text, TMP_FontAsset font, float size, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align, Color color, bool stretch = false, Vector2? anchor = null)
        {
            Vector2 an = anchor ?? new Vector2(0.5f, 1f);
            RectTransform r = stretch
                ? UiRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f))
                : UiRect(name, parent, an, an, pos, sizeDelta, an);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        // ═════════════════════ 헬퍼 ═════════════════════
        private static void WireObj(Object target, params (string field, Object value)[] refs)
        {
            var so = SO(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[S02Builder] {target.GetType().Name}.{field} 필드를 찾지 못했습니다."); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVectorArray(SerializedProperty p, Vector3[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).vector3Value = values[i];
        }
    }
}
