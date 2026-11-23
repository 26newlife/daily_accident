using System.Collections.Generic;
using System.IO;
using GameCore;
using GameData;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 1(전시 상황 — 기만된 평화와 낙진) 빌더.
    /// 집(GameScene 의 Apartment_1004_Base)은 그대로 두고, 시나리오 전용 오브젝트·데이터를 Additive 씬 S01_WarTime.unity 와
    /// Assets/Scenarios/S01_WarTime/Data 에 만든다. Scenario_01.asset(메인 메뉴 슬롯)에 정의를 연결하고 Build Settings 에 등록한다.
    /// 메뉴: Tools > GameUI > Scenarios > Build S01 War Time
    /// </summary>
    public static partial class MainMenuBuilder
    {
        public const string S01Dir = "Assets/Scenarios/S01_WarTime";
        public const string S01ScenePath = S01Dir + "/Scenes/S01_WarTime.unity";
        private const string S01DataDir = S01Dir + "/Data";
        private const string S01ScenarioDataPath = "Assets/Core/Data/Scenarios/Scenario_01.asset";

        // 빌드 중 임시 상태
        private static bool s01HasBase;

        [MenuItem("Tools/GameUI/Scenarios/Build S01 War Time", priority = 301)]
        public static void BuildWarScenarioMenu()
        {
            if (!EditorUtility.DisplayDialog("S01 전시 상황 빌드", "시나리오 1(전시 상황) 씬과 데이터를 만듭니다.\n기존 S01_WarTime 씬은 다시 만들어지고, 이미 있는 데이터 에셋(수치 조정 포함)은 유지됩니다.\n집(GameScene)과 시나리오 3 은 건드리지 않습니다.", "빌드", "취소")) return;
            BuildWarScenario();
        }

        public static void BuildWarScenario()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(S01Dir); EnsureFolder(S01Dir + "/Scenes"); EnsureFolder(S01DataDir);
            EnsureWarData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WarAssets a = ReloadWarAssets();   // 에셋을 만든 뒤 Refresh 가 이전 인스턴스를 무효화하므로 경로로 다시 읽는다

            // 집(GameScene)을 열어 두고 그 위에 시나리오 씬을 Additive 로 만든다 (소품이 가구 속에 박히지 않게 빈 자리를 찾는다)
            s01HasBase = File.Exists(GameScenePath);
            Scene baseScene = default;
            if (s01HasBase)
            {
                baseScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
            }
            else Debug.LogWarning("[S01Builder] GameScene.unity 가 없어 소품을 기본 좌표에 놓습니다. 먼저 Build Apartment Scene 을 실행하세요.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, s01HasBase ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            a = ReloadWarAssets();   // 씬을 여닫으면 로드해 둔 에셋 인스턴스가 정리될 수 있어 다시 읽는다
            BuildWarContent(scene, a);
            EditorSceneManager.SaveScene(scene, S01ScenePath);
            if (s01HasBase) { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(baseScene); }

            RegisterScene(S01ScenePath);
            LinkScenarioData(S01ScenarioDataPath, Load<ScenarioDefinition>("S01_Definition.asset"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[S01Builder] 시나리오 1(전시 상황) 빌드 완료: " + S01ScenePath);
        }

        // ═════════════════════ 데이터 ═════════════════════
        private sealed class WarAssets
        {
            public WarConfig config;
            public ItemData gasMask, iodine;
            public ObjectiveData a1Radio, a1Supplies, a2Supplies, a2Radio, a2Observe, a2Gear, a3Hold, a3Visitors, a4Judge, a5Survive, a5Protect, a6Rescue;
            public DeathReasonData[] deaths;
            public EndingData[] endings;
            public ScoreRules rules;
            public EnvironmentPreset peace, night, dawn, fallout, clear;
            public ScenarioDefinition definition;
        }

        private static ItemData S01Item(string file, string id, string name, string desc, bool equippable, string hex, int uses)
            => Ensure<ItemData>($"{S01DataDir}/Items/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = name;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("equippable").boolValue = equippable;
                so.FindProperty("fallbackColor").colorValue = Hex(hex);
                so.FindProperty("maxUses").intValue = uses;
            });

        private static ObjectiveData S01Obj(string file, string id, string title, string desc, bool main)
            => Ensure<ObjectiveData>($"{S01DataDir}/Objectives/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("isMain").boolValue = main;
            });

        private static DeathReasonData S01Death(string file, DeathReason reason, string title, string why, string correct)
        {
            int index = System.Array.IndexOf(System.Enum.GetValues(typeof(DeathReason)), reason);
            return Ensure<DeathReasonData>($"{S01DataDir}/DeathReasons/{file}.asset", so =>
            {
                so.FindProperty("reason").enumValueIndex = index;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("whyYouDied").stringValue = why;
                so.FindProperty("correctAction").stringValue = correct;
            });
        }

        private static EndingData S01Ending(string file, string id, string name, string route, string desc, bool survival)
            => Ensure<EndingData>($"{S01DataDir}/Endings/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("endingName").stringValue = name;
                so.FindProperty("routeLabel").stringValue = route;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("isSurvival").boolValue = survival;
            });

        private static EnvironmentPreset S01Preset(string file, System.Action<EnvironmentPreset> init)
        {
            string path = $"{S01DataDir}/Presets/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<EnvironmentPreset>(path);
            if (p != null) return p;
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            EnsureFolder(S01DataDir + "/Presets");
            p = ScriptableObject.CreateInstance<EnvironmentPreset>();
            init(p);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        private static void EnsureWarData()
        {
            var a = new WarAssets();
            a.config = Ensure<WarConfig>($"{S01DataDir}/WarConfig.asset", so => { });

            // 이전 버전(요새화·물물교환)에서 만들었던 에셋 정리
            foreach (string old in new[] { "Items/Item_Plywood.asset", "Items/Item_Pipe.asset", "Objectives/Obj_A1_Valuables.asset", "Objectives/Obj_A2_Shelter.asset", "Presets/Env_War_Afternoon.asset" })
                if (File.Exists($"{S01DataDir}/{old}")) AssetDatabase.DeleteAsset($"{S01DataDir}/{old}");

            // 아이템 (소모품은 사용 횟수 포함)
            a.gasMask = S01Item("Item_GasMask", WarKeys.ItemGasMask, "방독면", "낙진 속 호흡 피폭을 크게 줄여 준다. 사용 키로 착용/해제.", true, "4B5563", 1);
            a.iodine = S01Item("Item_Iodine", WarKeys.ItemIodine, "요오드화 칼륨", "갑상선이 방사성 요오드를 흡수하지 못하게 막는 알약. 낙진이 오기 전에 사용(복용)해야 한다.", false, "F59E0B", 1);

            // 목표
            a.a1Radio = S01Obj("Obj_A1_Radio", "war_a1_radio", "배터리 라디오를 챙긴다", "책상 위의 배터리 라디오.", true);
            a.a1Supplies = S01Obj("Obj_A1_Supplies", "war_a1_supplies", "생수와 비상식량을 챙긴다", "주방의 생수와 통조림.", true);
            a.a2Supplies = S01Obj("Obj_A2_Supplies", "war_a2_supplies", "식량 6개 · 물 6개를 확보한다", "주방 수납장과 집 안 구석을 뒤져 먹을 것과 마실 것을 모은다.", true);
            a.a2Radio = S01Obj("Obj_A2_Radio", "war_a2_radio", "단파 라디오로 진짜 소식을 듣는다", "공식 방송만 믿어도 될까? 단파 주파수를 돌려 보자.", true);
            a.a2Observe = S01Obj("Obj_A2_Observe", "war_a2_observe", "창밖 상황을 살핀다", "거실 창을 열어 도로의 군용 차량 움직임을 확인한다.", false);
            a.a2Gear = S01Obj("Obj_A2_Gear", "war_a2_gear", "방독면과 요오드화 칼륨을 구한다", "민방위 장비함과 안방 약상자를 찾아보자.", false);
            a.a3Hold = S01Obj("Obj_A3_Hold", "war_a3_hold", "은신처에서 버틴다", "식량·물·정신력을 관리한다.", true);
            a.a3Visitors = S01Obj("Obj_A3_Visitors", "war_a3_visitors", "문을 두드리는 사람들에게 응답한다", "문틈으로 확인하고 합류 여부를 정한다.", false);
            a.a4Judge = S01Obj("Obj_A4_Judge", "war_a4_judge", "종전 방송의 진위를 판단한다", "단서가 충분하다면 동료를 설득하고 문을 지켜라.", true);
            a.a5Survive = S01Obj("Obj_A5_Survive", "war_a5_survive", "섬광과 충격파에서 살아남는다", "밖에 나가지 말고, 창에서 떨어져 몸을 낮춰라.", true);
            a.a5Protect = S01Obj("Obj_A5_Protect", "war_a5_protect", "낙진 속에서 피폭을 막는다", "방독면 착용, 창에서 먼 안쪽 방, 보온.", true);
            a.a6Rescue = S01Obj("Obj_A6_Rescue", "war_a6_rescue", "구조대를 기다린다", "확성기와 헬기 소리.", true);

            // 사망 사유 (교육 피드백)
            a.deaths = new[]
            {
                S01Death("Death_NuclearFlash", DeathReason.NuclearFlash, "섬광과 충격파",
                    "거짓 종전 방송을 믿고 밖으로 나갔다가 핵폭발의 섬광과 충격파에 그대로 노출되었다.",
                    "정보의 출처를 의심하자. 공식 방송이 전부는 아니다. 휴전 방송이 나와도 안전이 확인되기 전에는 방호 시설(실내)을 떠나지 않는다."),
                S01Death("Death_ShockwaveDebris", DeathReason.ShockwaveDebris, "충격파와 유리 파편",
                    "섬광이 지나간 뒤 충격파가 닥칠 때 창가에 서 있었다. 유리창이 산산이 깨지며 파편이 몸을 덮쳤다.",
                    "섬광이 보이면 즉시 창에서 떨어져 몸을 낮춘다. 충격파는 섬광보다 늦게 도착하며, 가장 먼저 창문을 부순다. 안쪽 방 벽 쪽이 가장 안전하다."),
                S01Death("Death_RadiationSickness", DeathReason.RadiationSickness, "급성 방사선 피폭",
                    "낙진 속에서 피폭 수치가 한도를 넘었다. 방독면 없이 오염된 공기를 마셨거나 깨진 창 가까이에서 버텼다.",
                    "낙진 속에서는 깨진 창에서 먼 안쪽 방으로 물러나고, 방독면(또는 젖은 수건)으로 호흡을 막으며, 낙진 전에 요오드화 칼륨을 복용하자. 오염 지역에서 먹고 마신 것은 오염되었을 수 있다."),
                S01Death("Death_Hypothermia", DeathReason.Hypothermia, "저체온증 (핵겨울)",
                    "핵폭발 뒤의 한파 속에서 체온이 크게 떨어졌다. 깨진 창 곁에서 보온 없이 버텼다.",
                    "한파에는 창에서 먼 안쪽 방에서 담요·옷 등으로 보온해 체온을 지키자. 미리 보온 용품을 챙겨 두자."),
                S01Death("Death_Starvation", DeathReason.Starvation, "식량·물 고갈",
                    "식량과 물이 바닥난 채 오래 버티다 쓰러졌다.",
                    "고립에 대비해 식량과 물을 넉넉히 확보하자. 합류 인원이 늘면 소비도 빨라진다. 오염되지 않은 물과 식량은 밀폐해서 보관한다."),
            };

            // 엔딩
            a.endings = new[]
            {
                S01Ending("Ending_War_True", WarKeys.EndingTrue, "진정한 종전", "진 엔딩",
                    "방송의 진위를 가려냈고, 문을 다시 잠가 모두를 지켰다. 낙진이 지나간 하늘 위로 구조 헬기가 내려온다. 은신처의 모든 사람이 함께 하늘 아래로 걸어 나간다.", true),
                S01Ending("Ending_War_Bitter", WarKeys.EndingBitter, "홀로 남은 구조", "씁쓸한 생존",
                    "당신은 구조되었다. 하지만 문 앞에서 돌려보낸 얼굴들과 문을 열고 나간 동료들의 뒷모습이 오래도록 지워지지 않는다. 살아남는다는 것은, 때로는 그런 일이다.", true),
                S01Ending("Ending_War_Bad", WarKeys.EndingBad, "속아 버린 평화", "배드 엔딩",
                    "거짓 종전 방송에 속아 밖으로 나갔거나, 낙진 관리에 실패했다. 평화를 선언한 방송은 사실 가장 위험한 순간을 알리는 신호였다.", false),
            };

            // 점수 규칙
            a.rules = Ensure<ScoreRules>($"{S01DataDir}/ScoreRules.asset", so =>
            {
                var rules = new[]
                {
                    R("clues", "기만 단서 수집", 15, ScoreRules.Kind.FlagTrue, WarKeys.FlagCluesEnough, 0f, 0, "", 0f, "단파 방송과 창밖 관찰로 종전 방송의 거짓을 간파했다.", "공식 방송만 듣지 말고 여러 출처를 비교하자. 단파 라디오와 창밖 관찰에서 모순을 찾을 수 있다."),
                    R("radio", "라디오 확보", 5, ScoreRules.Kind.FlagTrue, WarKeys.FlagHasRadio, 0f, 0, "", 0f, "배터리 라디오를 챙겼다.", "정보를 얻는 수단(배터리 라디오)을 가장 먼저 확보하자."),
                    R("stay_inside", "섣불리 나가지 않았다", 15, ScoreRules.Kind.RecordZero, WarKeys.RecOutside, 0f, 0, "", 0f, "종전 방송에도 은신처를 떠나지 않았다.", "방송이 평화를 선언해도 해제 확인 전에는 은신처를 떠나지 않는다."),
                    R("blast", "충격파 대피", 15, ScoreRules.Kind.FlagTrue, WarKeys.FlagSurvivedFlash, 0f, 0, "", 0f, "섬광 직후 창에서 떨어져 충격파를 피했다.", "섬광이 보이면 창에서 떨어져 몸을 낮춰야 한다. 충격파는 섬광보다 늦게 온다."),
                    R("iodine", "요오드화 칼륨 선복용", 10, ScoreRules.Kind.FlagTrue, WarKeys.FlagIodineBefore, 0f, 0, "", 0f, "낙진 전에 요오드화 칼륨을 복용했다.", "요오드화 칼륨은 피폭 전·직후에 복용해야 효과가 있다."),
                    R("mask", "방독면 착용", 10, ScoreRules.Kind.FlagTrue, WarKeys.FlagMaskOn, 0f, 0, "", 0f, "낙진 속에서 방독면을 착용했다.", "낙진 속에서는 방독면(또는 젖은 수건)으로 호흡을 막아야 한다."),
                    R("radiation", "낮은 피폭", 10, ScoreRules.Kind.RecordAtMost, WarKeys.RecRadiation, 30f, 5, "", 60f, "피폭을 낮게 유지했다.", "방독면·요오드화 칼륨·안쪽 방을 모두 활용하면 피폭이 크게 준다."),
                    R("no_loss", "동료를 잃지 않았다", 10, ScoreRules.Kind.RecordZero, WarKeys.RecLost, 0f, 0, "", 0f, "합류한 동료를 지켜 냈다.", "동료가 방송에 속아 나가지 않도록 근거를 모아 설득하자."),
                    R("no_refuse", "문을 두드린 사람을 외면하지 않았다", 5, ScoreRules.Kind.RecordZero, WarKeys.RecRefused, 0f, 0, "", 0f, "문 앞의 사람들을 모두 받아들였다.", "자원이 한정되어도 도움을 청하는 사람은 외면하기 어렵다. 균형을 찾아 보자."),
                    R("persuade", "설득 성공", 5, ScoreRules.Kind.FlagTrue, WarKeys.FlagPersuaded, 0f, 0, "", 0f, "동료들을 설득해 문을 지켰다.", "단서가 충분해야 설득할 수 있다."),
                };
                SerializedProperty arr = so.FindProperty("rules");
                arr.arraySize = rules.Length;
                for (int i = 0; i < rules.Length; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    ScoreRules.Rule r = rules[i];
                    e.FindPropertyRelative("id").stringValue = r.id; e.FindPropertyRelative("label").stringValue = r.label;
                    e.FindPropertyRelative("maxPoints").intValue = r.maxPoints; e.FindPropertyRelative("kind").enumValueIndex = (int)r.kind;
                    e.FindPropertyRelative("key").stringValue = r.key; e.FindPropertyRelative("threshold").floatValue = r.threshold;
                    e.FindPropertyRelative("partialPoints").intValue = r.partialPoints; e.FindPropertyRelative("partialKey").stringValue = r.partialKey;
                    e.FindPropertyRelative("partialThreshold").floatValue = r.partialThreshold;
                    e.FindPropertyRelative("goodText").stringValue = r.goodText; e.FindPropertyRelative("badText").stringValue = r.badText;
                }
                SerializedProperty gr = so.FindProperty("grades");
                string[] gl = { "C", "B", "A", "S" }; float[] gm = { 0f, 0.55f, 0.75f, 0.9f };
                gr.arraySize = gl.Length;
                for (int i = 0; i < gl.Length; i++) { SerializedProperty e = gr.GetArrayElementAtIndex(i); e.FindPropertyRelative("label").stringValue = gl[i]; e.FindPropertyRelative("minRatio").floatValue = gm[i]; }
            });

            // 환경 프리셋
            a.peace = S01Preset("Env_War_Peace", p => { p.displayName = "평화로운 오후"; p.timeOfDay = TimeOfDayKind.Day; p.applySun = true; p.sunEuler = new Vector3(55f, -30f, 0f); p.sunIntensity = 1.0f; p.sunColor = new Color(1f, 0.96f, 0.88f); p.applyTraffic = true; p.pedestrians = true; p.vehicles = true; p.trafficDensity = 1f; });
            a.night = S01Preset("Env_War_Night", p => { p.displayName = "전시 밤"; p.timeOfDay = TimeOfDayKind.Night; p.applySun = true; p.sunEuler = new Vector3(-25f, -30f, 0f); p.sunIntensity = 0.04f; p.sunColor = new Color(0.4f, 0.45f, 0.7f); p.applyAmbient = true; p.ambientColor = new Color(0.04f, 0.05f, 0.08f); p.applyTraffic = true; p.pedestrians = false; p.vehicles = false; p.trafficDensity = 0f; });
            a.dawn = S01Preset("Env_War_FalsePeace", p => { p.displayName = "거짓 평화의 아침"; p.timeOfDay = TimeOfDayKind.Morning; p.applySun = true; p.sunEuler = new Vector3(18f, -30f, 0f); p.sunIntensity = 0.9f; p.sunColor = new Color(1f, 0.85f, 0.65f); p.applyTraffic = true; p.pedestrians = false; p.vehicles = false; p.trafficDensity = 0.1f; });
            a.fallout = S01Preset("Env_War_Fallout", p => { p.displayName = "검은 비와 낙진"; p.timeOfDay = TimeOfDayKind.Day; p.windowView = WindowViewMode.DarkSmoky; p.applySun = true; p.sunEuler = new Vector3(35f, -30f, 0f); p.sunIntensity = 0.18f; p.sunColor = new Color(0.55f, 0.55f, 0.6f); p.applyAmbient = true; p.ambientColor = new Color(0.1f, 0.1f, 0.12f); p.applyFog = true; p.fogColor = new Color(0.18f, 0.18f, 0.2f); p.fogDensity = 0.01f; p.applyTraffic = true; p.pedestrians = false; p.vehicles = false; p.trafficDensity = 0f; });
            a.clear = S01Preset("Env_War_Rescue", p => { p.displayName = "잿빛 구조의 아침"; p.timeOfDay = TimeOfDayKind.Morning; p.windowView = WindowViewMode.Normal; p.applySun = true; p.sunEuler = new Vector3(25f, -30f, 0f); p.sunIntensity = 0.7f; p.sunColor = new Color(1f, 0.9f, 0.75f); p.applyAmbient = true; p.ambientColor = new Color(0.2f, 0.2f, 0.22f); p.applyTraffic = true; p.pedestrians = false; p.vehicles = false; p.trafficDensity = 0f; });

            // 시나리오 정의
            string defPath = $"{S01DataDir}/S01_Definition.asset";
            var def = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(defPath);
            if (def == null) { if (File.Exists(defPath)) AssetDatabase.DeleteAsset(defPath); def = ScriptableObject.CreateInstance<ScenarioDefinition>(); AssetDatabase.CreateAsset(def, defPath); }
            var d = SO(def);
            d.FindProperty("id").stringValue = "S01";
            d.FindProperty("displayName").stringValue = "전시상황";
            d.FindProperty("description").stringValue = "기만된 평화와 낙진 — 거짓 종전 선언에 속지 말고 핵폭발에서 살아남아라";
            d.FindProperty("isPlayable").boolValue = true;
            d.FindProperty("targetPlayMinutes").floatValue = 17f;
            d.FindProperty("scenarioSceneName").stringValue = "S01_WarTime";
            d.FindProperty("startSpawnId").stringValue = "spawn_living_room";
            d.FindProperty("environmentPreset").objectReferenceValue = a.peace;
            SetStrings(d.FindProperty("actTypeNames"), new[] { "Act1_CrackInDaily", "Act2_StormEve", "Act3_IsolatedArk", "Act4_FalsePeace", "Act5_FlashAndFallout", "Act6_TrueArmistice" });
            SetObjectArray(d.FindProperty("requiredItems"), new[] { a.gasMask, a.iodine });
            SetObjectArray(d.FindProperty("deathReasons"), a.deaths);
            d.FindProperty("fallbackDeath").objectReferenceValue = a.deaths[0];
            SetObjectArray(d.FindProperty("endings"), a.endings);
            d.FindProperty("scoreRules").objectReferenceValue = a.rules;
            // 기능 플래그: 전시 상황은 가스·화재 소품·엘리베이터 위험을 쓰지 않는다 (집 오브젝트는 그대로, 상태만 꺼진다)
            d.FindProperty("useRespiratoryGauge").boolValue = true;
            d.FindProperty("useGasHazard").boolValue = false;
            d.FindProperty("useFireProps").boolValue = false;
            d.FindProperty("useElevatorHazard").boolValue = false;
            d.FindProperty("useDoorPeephole").boolValue = true;
            d.FindProperty("useOutdoorTraffic").boolValue = true;
            d.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            a.definition = def;
        }

        private static T Load<T>(string rel) where T : Object
        {
            string path = $"{S01DataDir}/{rel}";
            var o = AssetDatabase.LoadAssetAtPath<T>(path);
            if (o == null) Debug.LogError($"[S01Builder] 에셋을 읽지 못했습니다: {path}");
            return o;
        }

        private static WarAssets ReloadWarAssets()
        {
            var a = new WarAssets();
            a.config = Load<WarConfig>("WarConfig.asset");
            a.gasMask = Load<ItemData>("Items/Item_GasMask.asset"); a.iodine = Load<ItemData>("Items/Item_Iodine.asset");
            a.a1Radio = Load<ObjectiveData>("Objectives/Obj_A1_Radio.asset"); a.a1Supplies = Load<ObjectiveData>("Objectives/Obj_A1_Supplies.asset");
            a.a2Supplies = Load<ObjectiveData>("Objectives/Obj_A2_Supplies.asset"); a.a2Radio = Load<ObjectiveData>("Objectives/Obj_A2_Radio.asset"); a.a2Observe = Load<ObjectiveData>("Objectives/Obj_A2_Observe.asset"); a.a2Gear = Load<ObjectiveData>("Objectives/Obj_A2_Gear.asset");
            a.a3Hold = Load<ObjectiveData>("Objectives/Obj_A3_Hold.asset"); a.a3Visitors = Load<ObjectiveData>("Objectives/Obj_A3_Visitors.asset");
            a.a4Judge = Load<ObjectiveData>("Objectives/Obj_A4_Judge.asset"); a.a5Survive = Load<ObjectiveData>("Objectives/Obj_A5_Survive.asset"); a.a5Protect = Load<ObjectiveData>("Objectives/Obj_A5_Protect.asset"); a.a6Rescue = Load<ObjectiveData>("Objectives/Obj_A6_Rescue.asset");
            a.deaths = new[] { Load<DeathReasonData>("DeathReasons/Death_NuclearFlash.asset"), Load<DeathReasonData>("DeathReasons/Death_ShockwaveDebris.asset"), Load<DeathReasonData>("DeathReasons/Death_RadiationSickness.asset"), Load<DeathReasonData>("DeathReasons/Death_Hypothermia.asset"), Load<DeathReasonData>("DeathReasons/Death_Starvation.asset") };
            a.endings = new[] { Load<EndingData>("Endings/Ending_War_True.asset"), Load<EndingData>("Endings/Ending_War_Bitter.asset"), Load<EndingData>("Endings/Ending_War_Bad.asset") };
            a.rules = Load<ScoreRules>("ScoreRules.asset");
            a.peace = Load<EnvironmentPreset>("Presets/Env_War_Peace.asset"); a.night = Load<EnvironmentPreset>("Presets/Env_War_Night.asset"); a.dawn = Load<EnvironmentPreset>("Presets/Env_War_FalsePeace.asset");
            a.fallout = Load<EnvironmentPreset>("Presets/Env_War_Fallout.asset"); a.clear = Load<EnvironmentPreset>("Presets/Env_War_Rescue.asset");
            a.definition = Load<ScenarioDefinition>("S01_Definition.asset");
            return a;
        }

        // ═════════════════════ 소품 배치 ═════════════════════
        private static readonly LayerMask AnyLayer = ~0;

        private static bool SpotFree(Vector3 center, Vector3 half)
            => !Physics.CheckBox(center, half, Quaternion.identity, AnyLayer, QueryTriggerInteraction.Ignore);

        private static bool InsideHome(Vector3 p) => p.x > 0.35f && p.x < 7.2f && p.z > 0.55f && p.z < 8.8f;

        // ───────── 고정 배치 (S01-01) ─────────
        // 소품·동행자 위치는 더 이상 빌드 때 빈 자리 탐색(FindSpot)에 맡기지 않는다 — 아래 표의 좌표가 정답이다.
        // 각 위치는 시나리오 씬의 앵커 s01_* (ScenarioAnchor)로도 만들어져, 시나리오 코드와 HouseController.FindAnchor 가 id 로 찾을 수 있다.
        // 좌표를 바꾸면 Tools > GameUI > Validate Apartment Clearance 와 S01 통로 점검으로 다시 검증할 것 (주 동선 0.9m 이상).
        private static readonly System.Collections.Generic.Dictionary<string, Vector3> S01FixedSpots = new System.Collections.Generic.Dictionary<string, Vector3>
        {
            { "BatteryRadio", new Vector3(4.00f, 0.01f, 5.00f) },
            { "RadioShelterSpot", new Vector3(6.00f, 0.01f, 7.60f) },
            { "WaterAndCans", new Vector3(5.50f, 0.01f, 4.30f) },   // 주방 북동쪽 구석 (예전 자동 탐색 결과 x=7.05 는 건물 밖이었다)
            { "PantryBox", new Vector3(5.50f, 0.01f, 2.20f) },
            { "MedKit", new Vector3(1.30f, 0.58f, 5.80f) },
            { "CivilDefenseBox", new Vector3(6.22f, 0.01f, 1.38f) },
            { "Blanket", new Vector3(1.60f, 0.01f, 6.80f) },
            { "WinterClothes", new Vector3(5.38f, 0.01f, 5.68f) },
            { "SteelPipe", new Vector3(3.40f, 0.01f, 2.00f) },
            { "Soldier", new Vector3(3.40f, 0.01f, 5.40f) },
            { "Child", new Vector3(4.30f, 0.01f, 5.80f) },
            { "Neighbor", new Vector3(4.90f, 0.01f, 4.80f) },
        };

        private static Transform s01Anchors;

        private static string S01AnchorId(string goName)
        {
            var sb = new System.Text.StringBuilder("s01_");
            for (int i = 0; i < goName.Length; i++)
            {
                char c = goName[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>고정 좌표(없으면 FindSpot)를 돌려주고, 시나리오 앵커 s01_* 를 만든다</summary>
        private static Vector3 ResolveSpot(string goName, Vector3 near, Vector3 size, bool preferSurface)
        {
            Vector3 foot = S01FixedSpots.TryGetValue(goName, out Vector3 fixedPos) ? fixedPos : FindSpot(near, size, preferSurface);
            if (s01Anchors != null)
            {
                var a = new GameObject(S01AnchorId(goName)).AddComponent<ScenarioAnchor>();
                a.transform.SetParent(s01Anchors, false);
                a.transform.position = foot;
                var aso = SO(a); aso.FindProperty("id").stringValue = S01AnchorId(goName); aso.FindProperty("description").stringValue = "시나리오 1 고정 배치: " + goName; aso.ApplyModifiedPropertiesWithoutUndo();
            }
            return foot;
        }

        /// <summary>near 주변에서 소품이 놓일 빈 자리(가구 위 또는 바닥)를 찾는다. 집이 없으면 near 그대로 바닥</summary>
        private static Vector3 FindSpot(Vector3 near, Vector3 size, bool preferSurface)
        {
            if (!s01HasBase) return new Vector3(near.x, 0.02f, near.z);
            Vector3 half = size * 0.5f;
            for (float r = 0f; r <= 2.6f; r += 0.25f)
            {
                int steps = r < 0.01f ? 1 : Mathf.Max(8, Mathf.RoundToInt(r * 14f));
                for (int k = 0; k < steps; k++)
                {
                    float ang = k * Mathf.PI * 2f / steps;
                    Vector3 p = near + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                    if (!InsideHome(p)) continue;
                    if (preferSurface && Physics.Raycast(new Vector3(p.x, 1.5f, p.z), Vector3.down, out RaycastHit hit, 1.6f, AnyLayer, QueryTriggerInteraction.Ignore)
                        && hit.normal.y > 0.9f && hit.point.y > 0.45f && hit.point.y < 1.1f)
                    {
                        Vector3 c = new Vector3(p.x, hit.point.y + half.y + 0.02f, p.z);
                        if (SpotFree(c, half * 0.9f)) return new Vector3(p.x, hit.point.y + 0.005f, p.z);
                    }
                    if (Physics.Raycast(new Vector3(p.x, 0.6f, p.z), Vector3.down, out RaycastHit fh, 0.7f, AnyLayer, QueryTriggerInteraction.Ignore) && fh.point.y < 0.12f && fh.normal.y > 0.9f)
                    {
                        Vector3 c = new Vector3(p.x, fh.point.y + half.y + 0.04f, p.z);
                        if (SpotFree(c, half * 0.9f)) return new Vector3(p.x, fh.point.y + 0.005f, p.z);
                    }
                }
            }
            Debug.LogWarning($"[S01Builder] {near} 주변에서 빈 자리를 찾지 못해 그 위치에 그대로 놓습니다.");
            return new Vector3(near.x, 0.02f, near.z);
        }

        private static T Prop<T>(Transform parent, string id, string goName, Vector3 anchorNear, Vector3 size, bool surface, Material mat, bool trigger = false) where T : InteractableObject
        {
            Vector3 foot = ResolveSpot(goName, anchorNear, size, surface);
            var root = new GameObject(goName);
            root.transform.SetParent(parent, false);
            root.transform.position = foot;
            // 상호작용 충돌체 (발 기준, 위로 size.y)
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, size.y * 0.5f, 0f); col.size = size; col.isTrigger = trigger;
            var body = Box(root.transform, "Body", new Vector3(0f, size.y * 0.5f, 0f), size, mat, false);
            var t = root.AddComponent<T>();
            var so = SO(t);
            so.FindProperty("id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return t;
        }

        private static void SetPrompt(Object o, string prompt, float hold)
        {
            var so = SO(o);
            if (so.FindProperty("prompt") != null) so.FindProperty("prompt").stringValue = prompt;
            if (so.FindProperty("holdSeconds") != null) so.FindProperty("holdSeconds").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObj(Object o, string field, Object value) { var so = SO(o); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }

        private static WarPickup MakePickup(Transform parent, string id, string goName, Vector3 near, Vector3 size, bool surface, Material mat, string prompt, string message, ItemData item, int food, int water, int medicine, bool blanket, int cost, float hold, bool weapon = false)
        {
            var p = Prop<WarPickup>(parent, id, goName, near, size, surface, mat, true);
            SetPrompt(p, prompt, hold);
            var so = SO(p);
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("food").intValue = food; so.FindProperty("water").intValue = water; so.FindProperty("medicine").intValue = medicine;
            so.FindProperty("blanket").boolValue = blanket; so.FindProperty("actionCost").intValue = cost; so.FindProperty("weapon").boolValue = weapon;
            so.FindProperty("message").stringValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        private static void SetFloat(Object o, string field, float v) { var so = SO(o); so.FindProperty(field).floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

        // ═════════════════════ 씬 ═════════════════════
        private static void BuildWarContent(Scene scene, WarAssets a)
        {
            // 스폰 id 가 이미 정해진 집 앵커 근처 좌표 (Framework 의 HouseAnchors 와 같은 값)
            Vector3 livingCenter = new Vector3(4.0f, 0f, 5.0f), kitchen = new Vector3(6.3f, 0f, 3.0f), bedroom = new Vector3(1.3f, 0f, 5.8f);
            Vector3 entrance = new Vector3(3.0f, 0f, 1.6f), utility = new Vector3(6.4f, 0f, 1.2f), shelter = new Vector3(6.0f, 0f, 7.6f);

            var contentGo = new GameObject("S01_Content");
            Transform content = contentGo.transform;
            Transform sys = Group("ScenarioSystems", content);
            Transform props = Group("Props", content);
            Transform people = Group("Survivors", content);
            s01Anchors = Group("Anchors", content);   // s01_* 앵커 (소품·동행자 고정 위치)

            // ── 시스템 ──
            var state = sys.gameObject.AddComponent<WarState>(); SetObj(state, "config", a.config);
            sys.gameObject.AddComponent<WarAudio>();
            var hudGo = new GameObject("WarHud"); hudGo.transform.SetParent(sys, false);
            var hud = hudGo.AddComponent<WarHud>(); hudGo.SetActive(false);   // 시나리오 시작 시 S01Bootstrap 이 켠다
            var boot = sys.gameObject.AddComponent<S01Bootstrap>();
            var bso = SO(boot); bso.FindProperty("hud").objectReferenceValue = hud; bso.ApplyModifiedPropertiesWithoutUndo();

            // ── 전쟁 발발 연출 (하늘 전환 / 연기 기둥 / 포격 섬광 / 전투기·헬기 편대) ──
            BuildOutbreak(sys, content);

            // ── 소품 ──
            Material olive = GB("War_Olive", Hex("55633A")), blue = GB("War_Blue", Hex("2E6FB7")), red = GB("War_Red", Hex("A63A3A")), grey = GB("War_Grey", Hex("5C6066")), tan = GB("War_Tan", Hex("B9A27C"));

            // Act 1/2 파밍
            var radio = Prop<WarRadio>(props, WarKeys.Radio, "BatteryRadio", livingCenter, new Vector3(0.3f, 0.18f, 0.12f), true, olive, true);
            Box(radio.transform, "Antenna", new Vector3(0.1f, 0.3f, 0f), new Vector3(0.012f, 0.24f, 0.012f), grey, false);
            var shelterSpot = new GameObject("RadioShelterSpot").transform; shelterSpot.SetParent(props, false); shelterSpot.position = ResolveSpot("RadioShelterSpot", shelter, new Vector3(0.3f, 0.18f, 0.12f), true);
            SetObj(radio, "shelterSpot", shelterSpot);
            MakePickup(props, WarKeys.PickupSupplies, "WaterAndCans", kitchen, new Vector3(0.4f, 0.3f, 0.3f), true, blue, "생수·비상식량 챙기기", "생수와 통조림을 챙겼다.", null, 4, 4, 0, false, 0, 0.8f);
            MakePickup(props, WarKeys.PickupPantry, "PantryBox", kitchen + new Vector3(-0.8f, 0f, -0.8f), new Vector3(0.45f, 0.3f, 0.35f), true, tan, "주방 수납장 뒤지기", "수납장 구석에서 라면과 통조림, 생수를 찾았다.", null, 3, 2, 0, false, a.config.actionCostSearch, 1.5f);
            MakePickup(props, WarKeys.PickupMedkit, "MedKit", bedroom, new Vector3(0.3f, 0.18f, 0.22f), true, red, "안방 약상자 열기", "약상자에서 진통제·붕대, 그리고 요오드화 칼륨을 찾았다.", a.iodine, 0, 0, 1, false, a.config.actionCostSearch, 1.5f);
            MakePickup(props, WarKeys.CivilDefenseBox, "CivilDefenseBox", utility, new Vector3(0.5f, 0.3f, 0.35f), false, olive, "낡은 민방위 장비함 열기", "아버지가 남겨 둔 민방위 장비함에서 방독면과 비상 약품을 찾았다.", a.gasMask, 0, 0, 2, false, a.config.actionCostSearch, 2.5f);
            MakePickup(props, WarKeys.PickupBlanket, "Blanket", bedroom + new Vector3(0.3f, 0f, 1.0f), new Vector3(0.5f, 0.12f, 0.4f), true, red, "담요 챙기기", "두꺼운 담요를 챙겼다. 한파에 도움이 될 것이다.", null, 0, 0, 0, true, a.config.actionCostSearch, 0.8f);
            var clothes = MakePickup(props, WarKeys.PickupClothes, "WinterClothes", livingCenter + new Vector3(1.2f, 0f, 0.5f), new Vector3(0.45f, 0.2f, 0.35f), true, grey, "겨울옷 챙기기", "두꺼운 겨울옷과 목도리를 챙겼다. 한파에 도움이 될 것이다.", null, 0, 0, 0, false, a.config.actionCostSearch, 0.8f);
            SetFloat(clothes, "comfort", a.config.comfortClothes);
            MakePickup(props, WarKeys.PickupPipe, "SteelPipe", entrance + new Vector3(0.9f, 0f, 0.4f), new Vector3(0.9f, 0.08f, 0.08f), false, grey, "쇠파이프 챙기기", "베란다 공구함의 쇠파이프를 챙겼다. 위협을 쫓아낼 때 쓸 수 있다.", null, 0, 0, 0, false, a.config.actionCostSearch, 1.0f, true);

            // ── 생존자 (합류 전에는 숨김) ──
            WarSurvivor soldier = MakeSurvivor(people, WarKeys.SurvivorSoldier, "Soldier", "군인", livingCenter + new Vector3(-0.6f, 0f, 0.4f), Hex("4B5320"));
            WarSurvivor child = MakeSurvivor(people, WarKeys.SurvivorChild, "Child", "아이", livingCenter + new Vector3(0.3f, 0f, 0.8f), Hex("E08E45"), 0.7f);
            WarSurvivor neighbor = MakeSurvivor(people, WarKeys.SurvivorNeighbor, "Neighbor", "이웃", livingCenter + new Vector3(0.9f, 0f, -0.2f), Hex("6A7FA3"));

            // ── Act ──
            Transform actsRoot = Group("Acts", sys);
            var act1 = AddAct<Act1_CrackInDaily>(actsRoot, "Act1_CrackInDaily", "일상 속의 균열", "재난문자와 사이렌, 짧은 파밍 튜토리얼.");
            var act2 = AddAct<Act2_StormEve>(actsRoot, "Act2_StormEve", "폭풍 전야의 대비", "집 안 수색(식량·약품·보온), 라디오 청취, 창밖 관찰.");
            var act3 = AddAct<Act3_IsolatedArk>(actsRoot, "Act3_IsolatedArk", "고립된 방주", "방문자 이벤트와 야간 외부인 위협.");
            var act4 = AddAct<Act4_FalsePeace>(actsRoot, "Act4_FalsePeace", "기만된 평화", "거짓 종전 방송과 설득.");
            var act5 = AddAct<Act5_FlashAndFallout>(actsRoot, "Act5_FlashAndFallout", "섬광과 낙진", "핵폭발과 낙진 생존.");
            var act6 = AddAct<Act6_TrueArmistice>(actsRoot, "Act6_TrueArmistice", "잿더미 위의 진짜 종전", "구조와 멀티 엔딩.");

            WireAct(act1, ("objectiveRadio", a.a1Radio), ("objectiveSupplies", a.a1Supplies));
            WireAct(act2, ("objectiveSupplies", a.a2Supplies), ("objectiveRadio", a.a2Radio), ("objectiveObserve", a.a2Observe), ("objectiveGear", a.a2Gear));
            WireAct(act3, ("objectiveHold", a.a3Hold), ("objectiveVisitors", a.a3Visitors), ("soldier", soldier), ("child", child), ("neighbor", neighbor), ("nightPreset", a.night));
            WireAct(act4, ("objectiveJudge", a.a4Judge), ("dawnPreset", a.dawn));
            var s4 = SO(act4); SetObjectArray(s4.FindProperty("survivors"), new[] { soldier, child, neighbor }); s4.ApplyModifiedPropertiesWithoutUndo();
            WireAct(act5, ("objectiveSurvive", a.a5Survive), ("objectiveProtect", a.a5Protect), ("falloutPreset", a.fallout));
            WireAct(act6, ("objectiveRescue", a.a6Rescue), ("clearPreset", a.clear));

            // ── 루트 ──
            var rootGo = new GameObject("ScenarioSceneRoot");
            var sr = rootGo.AddComponent<ScenarioSceneRoot>();
            var rso = SO(sr);
            rso.FindProperty("contentRoot").objectReferenceValue = contentGo;
            SetObjectArray(rso.FindProperty("acts"), new ActBase[] { act1, act2, act3, act4, act5, act6 });
            SetObjectArray(rso.FindProperty("bootstraps"), new Component[] { boot });
            rso.ApplyModifiedPropertiesWithoutUndo();

            contentGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(rootGo, scene);
            SceneManager.MoveGameObjectToScene(contentGo, scene);
        }

        private static void WireAct(ActBase act, params (string field, Object value)[] refs)
        {
            var so = SO(act);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[S01Builder] {act.GetType().Name}.{field} 필드를 찾지 못했습니다."); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 전쟁 발발 연출 · 편대 비행 ═════════════════════
        private const string PrefabDir = S01Dir + "/Prefabs";

        private static void BuildOutbreak(Transform sys, Transform content)
        {
            Vector3 w = new Vector3(3.2f, 1.2f, 7.3f);
            foreach (ToggleInteractable t in Object.FindObjectsByType<ToggleInteractable>(FindObjectsInactive.Include)) if (t.Id == "window_living") w = t.transform.position;

            WarAircraft jet = EnsureAircraftPrefab(true), heli = EnsureAircraftPrefab(false);

            // 비행 경로 (웨이포인트). 창(+z 방향) 앞 100~200m, 고도는 10층 바닥 기준 -8~26m
            Transform routes = Group("FlightRoutes", content);
            Transform jetA = Route(routes, "JetRoute_A", w, new Vector3(-650f, 14f, 170f), new Vector3(-220f, 10f, 135f), new Vector3(220f, 14f, 125f), new Vector3(680f, 26f, 160f));
            Transform jetB = Route(routes, "JetRoute_B", w, new Vector3(680f, 6f, 115f), new Vector3(250f, 9f, 98f), new Vector3(-250f, 12f, 92f), new Vector3(-680f, 18f, 105f));
            Transform heliA = Route(routes, "HeliRoute_A", w, new Vector3(540f, -4f, 210f), new Vector3(180f, -1f, 165f), new Vector3(-180f, 3f, 170f), new Vector3(-540f, 10f, 215f));
            Transform heliB = Route(routes, "HeliRoute_B", w, new Vector3(-540f, -8f, 190f), new Vector3(-170f, -5f, 150f), new Vector3(170f, -2f, 155f), new Vector3(540f, 6f, 195f));

            var go = new GameObject("WarOutbreak");
            go.transform.SetParent(sys, false);
            var fl = go.AddComponent<WarFlyovers>();
            var fso = SO(fl);
            fso.FindProperty("jetPrefab").objectReferenceValue = jet; fso.FindProperty("heliPrefab").objectReferenceValue = heli;
            SetObjectArray(fso.FindProperty("jetRoutes"), new[] { jetA, jetB }); SetObjectArray(fso.FindProperty("heliRoutes"), new[] { heliA, heliB });
            fso.ApplyModifiedPropertiesWithoutUndo();
            var ob = go.AddComponent<WarOutbreak>();
            SetObj(ob, "flyovers", fl);
        }

        private static Transform Route(Transform parent, string name, Vector3 origin, params Vector3[] offsets)
        {
            Transform r = Group(name, parent);
            for (int i = 0; i < offsets.Length; i++)
            {
                var wp = new GameObject("WP_" + (i + 1)).transform;
                wp.SetParent(r, false);
                wp.position = origin + offsets[i];
            }
            return r;
        }

        /// <summary>프로시저럴 전투기/헬기 프리팹 (기본 메시 조합). 이미 있으면 그대로 쓴다</summary>
        private static WarAircraft EnsureAircraftPrefab(bool jet)
        {
            EnsureFolder(PrefabDir);
            string path = $"{PrefabDir}/{(jet ? "S01_Fighter" : "S01_Helicopter")}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<WarAircraft>();

            Material grey = GB("War_JetGrey", Hex("6B7078")), dark = GB("War_JetDark", Hex("30343A")), glass = GB("War_CanopyGlass", Hex("3C5A78")), olive = GB("War_HeliOlive", Hex("4F5A3A")), blade = GB("War_Blade", Hex("1F2124"));
            var root = new GameObject(jet ? "S01_Fighter" : "S01_Helicopter");
            Transform t = root.transform;
            ParticleSystem burner = null;
            if (jet)
            {
                // 동체·기수·캐노피·주익·수평/수직 꼬리날개·엔진 노즐 (기체 앞쪽 = +z)
                Box(t, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(1.2f, 1.0f, 9.5f), grey, false);
                Box(t, "Nose", new Vector3(0f, -0.05f, 5.3f), new Vector3(0.7f, 0.6f, 2.2f), grey, false);
                Box(t, "NoseTip", new Vector3(0f, -0.05f, 6.5f), new Vector3(0.3f, 0.3f, 1.2f), dark, false);
                Box(t, "Canopy", new Vector3(0f, 0.62f, 1.6f), new Vector3(0.8f, 0.5f, 2.0f), glass, false);
                Box(t, "WingL", new Vector3(-3.1f, -0.1f, -0.8f), new Vector3(5.6f, 0.12f, 3.4f), grey, false, new Vector3(0f, 22f, 0f));
                Box(t, "WingR", new Vector3(3.1f, -0.1f, -0.8f), new Vector3(5.6f, 0.12f, 3.4f), grey, false, new Vector3(0f, -22f, 0f));
                Box(t, "TailPlaneL", new Vector3(-1.5f, 0f, -4.1f), new Vector3(2.4f, 0.1f, 1.5f), grey, false, new Vector3(0f, 18f, 0f));
                Box(t, "TailPlaneR", new Vector3(1.5f, 0f, -4.1f), new Vector3(2.4f, 0.1f, 1.5f), grey, false, new Vector3(0f, -18f, 0f));
                Box(t, "FinL", new Vector3(-0.55f, 1.0f, -3.9f), new Vector3(0.1f, 1.7f, 1.9f), grey, false, new Vector3(0f, 0f, 12f));
                Box(t, "FinR", new Vector3(0.55f, 1.0f, -3.9f), new Vector3(0.1f, 1.7f, 1.9f), grey, false, new Vector3(0f, 0f, -12f));
                var nozzle = Cyl(t, "Nozzle", new Vector3(0f, 0f, -4.9f), 0.45f, 0.9f, dark, false);
                nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var bgo = new GameObject("Afterburner");
                bgo.transform.SetParent(t, false); bgo.transform.localPosition = new Vector3(0f, 0f, -5.4f); bgo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                burner = bgo.AddComponent<ParticleSystem>();
                burner.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var m = burner.main; m.loop = true; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World;
                m.startLifetime = 0.35f; m.startSpeed = 6f; m.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.85f, 0.4f, 0.8f));
                var e = burner.emission; e.rateOverTime = 70f;
                var sh = burner.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.3f;
                var sz = burner.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
                var cl = burner.colorOverLifetime; cl.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                cl.color = g;
                bgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("War_Afterburner", new Color(1f, 0.6f, 0.2f, 1f), true);
            }
            else
            {
                // 동체·조종석·꼬리 붐·꼬리날개·스키드·메인/테일 로터(회전)
                Box(t, "Cabin", new Vector3(0f, 0f, 0.3f), new Vector3(2.1f, 2.0f, 4.0f), olive, false);
                Box(t, "NoseCone", new Vector3(0f, -0.2f, 2.6f), new Vector3(1.7f, 1.4f, 1.6f), olive, false, new Vector3(-8f, 0f, 0f));
                Box(t, "Windshield", new Vector3(0f, 0.35f, 2.95f), new Vector3(1.55f, 0.9f, 0.5f), glass, false, new Vector3(-30f, 0f, 0f));
                Box(t, "TailBoom", new Vector3(0f, 0.35f, -3.6f), new Vector3(0.55f, 0.65f, 5.4f), olive, false);
                Box(t, "TailFin", new Vector3(0f, 1.0f, -6.1f), new Vector3(0.12f, 1.6f, 1.3f), olive, false, new Vector3(18f, 0f, 0f));
                Box(t, "TailPlane", new Vector3(0f, 0.55f, -5.7f), new Vector3(1.8f, 0.08f, 0.6f), olive, false);
                foreach (float x in new[] { -0.9f, 0.9f })
                {
                    Box(t, "Skid", new Vector3(x, -1.5f, 0.3f), new Vector3(0.12f, 0.12f, 4.0f), dark, false);
                    Box(t, "StrutF", new Vector3(x, -1.2f, 1.4f), new Vector3(0.1f, 0.7f, 0.1f), dark, false);
                    Box(t, "StrutB", new Vector3(x, -1.2f, -0.8f), new Vector3(0.1f, 0.7f, 0.1f), dark, false);
                }
                Cyl(t, "RotorMast", new Vector3(0f, 1.25f, 0.2f), 0.14f, 0.6f, dark, false);
                var rotor = new GameObject("MainRotor").transform; rotor.SetParent(t, false); rotor.localPosition = new Vector3(0f, 1.6f, 0.2f);
                Box(rotor, "BladeA", Vector3.zero, new Vector3(10.5f, 0.05f, 0.4f), blade, false);
                Box(rotor, "BladeB", Vector3.zero, new Vector3(0.4f, 0.05f, 10.5f), blade, false);
                var spin = rotor.gameObject.AddComponent<WarRotorSpin>();
                var tail = new GameObject("TailRotor").transform; tail.SetParent(t, false); tail.localPosition = new Vector3(0.35f, 1.0f, -6.2f);
                Box(tail, "BladeA", Vector3.zero, new Vector3(0.05f, 1.8f, 0.2f), blade, false);
                Box(tail, "BladeB", Vector3.zero, new Vector3(0.05f, 0.2f, 1.8f), blade, false);
                var tspin = tail.gameObject.AddComponent<WarRotorSpin>();
                var rso = SO(spin); rso.FindProperty("axis").vector3Value = Vector3.up; rso.FindProperty("degreesPerSecond").floatValue = 1600f; rso.ApplyModifiedPropertiesWithoutUndo();
                var tso = SO(tspin); tso.FindProperty("axis").vector3Value = Vector3.right; tso.FindProperty("degreesPerSecond").floatValue = 2400f; tso.ApplyModifiedPropertiesWithoutUndo();
            }
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 1f;
            var ac = root.AddComponent<WarAircraft>();
            var aso = SO(ac);
            aso.FindProperty("kind").enumValueIndex = jet ? 0 : 1;
            aso.FindProperty("engineAudio").objectReferenceValue = audio;
            aso.FindProperty("afterburner").objectReferenceValue = burner;
            aso.FindProperty("dopplerLevel").floatValue = jet ? 5f : 2.5f;
            aso.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<WarAircraft>();
        }

        private static WarSurvivor MakeSurvivor(Transform parent, string id, string goName, string display, Vector3 near, Color color, float scale = 1f)
        {
            Vector3 foot = ResolveSpot(goName, near, new Vector3(0.6f, 1.7f * scale, 0.6f), false);
            var root = new GameObject(goName);
            root.transform.SetParent(parent, false);
            root.transform.position = foot;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f * scale, 0f); col.height = 1.7f * scale; col.radius = 0.28f * scale; col.isTrigger = true;   // 지나갈 수 있다
            var mat = GB("War_Person_" + goName, color);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body"; body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f * scale, 0f); body.transform.localScale = new Vector3(0.55f * scale, 0.85f * scale, 0.55f * scale);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = mat;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.62f * scale, 0f); head.transform.localScale = Vector3.one * 0.26f * scale;
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = GB("War_Skin", Hex("D9B08C"));
            var s = root.AddComponent<WarSurvivor>();
            var so = SO(s); so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = display; so.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return s;
        }
    }
}
