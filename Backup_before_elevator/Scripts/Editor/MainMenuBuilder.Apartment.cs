using System;
using System.IO;
using GameCore;
using GameData;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 3 "아파트 화재" 게임 씬 생성기.  메뉴: Tools > GameUI > Build Apartment Scene
    /// - GameScene.unity 를 새로 만든다 (기존 파일은 덮어씀. 이전 임시 씬이었다면 그대로 교체)
    /// - 데이터 에셋(아이템/사망 원인/엔딩/점수/목표)은 "없을 때만" 생성한다 → 문구를 수정해도 재실행 시 보존
    /// - 플레이어 프리팹: Assets/Prefabs/Gameplay/Player.prefab (이미 있으면 덮어씀)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string ItemDir = "Assets/Data/Items";
        private const string DeathDir = "Assets/Data/DeathReasons";
        private const string EndingDir = "Assets/Data/Endings";
        private const string ScoreDir = "Assets/Data/Score";
        private const string ObjectiveDir = "Assets/Data/Objectives";
        private const string GameplayPrefabDir = "Assets/Prefabs/Gameplay";
        private const string PlayerPrefabPath = "Assets/Prefabs/Gameplay/Player.prefab";
        private const string ApartmentScenarioPath = "Assets/Data/Scenarios/Scenario_03.asset";

        // 플레이어 치수 (Scale 은 항상 1). 실제 성인 기준: 키 1.75 / 눈높이 1.6 / 반지름 0.28
        private const float PlayerHeight = 1.75f, PlayerRadius = 0.28f, PlayerEyeBelowTop = 0.15f, PlayerFov = 78f, PlayerCameraHeightOffset = -0.06f;
        private const float PlayerWalk = 1.8f, PlayerSprint = 3.8f, PlayerCrouchSpeed = 1.0f, PlayerJump = 0.6f, PlayerInteractDistance = 2.0f;

        private static readonly Vector3 PlayerStart = new Vector3(1.7f, 0.02f, 4.0f);

        // ═════════════════════════ 진입점 ═════════════════════════
        [MenuItem("Tools/GameUI/Build Apartment Scene")]
        public static void BuildApartmentMenu()
        {
            if (!EditorUtility.DisplayDialog("Build Apartment Scene",
                    "GameScene.unity(아파트 화재 1004호)와 Player 프리팹을 다시 생성합니다(덮어쓰기).\n" +
                    "데이터 에셋(아이템/사망 원인/엔딩 등)은 이미 있으면 유지됩니다. 계속할까요?", "생성", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildApartmentScene();
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.MainMenuBuilder.BuildApartmentBatch</summary>
        public static void BuildApartmentBatch()
        {
            BuildApartmentScene();
            AssetDatabase.SaveAssets();
        }

        public static void BuildApartmentScene()
        {
            if (!File.Exists(PrefabPath) || !File.Exists(MainMenuScenePath))
            {
                Debug.LogError("[ApartmentBuilder] 먼저 Tools > GameUI > Build Main Menu 를 실행하세요 (버튼 프리팹/UI 텍스처가 필요합니다).");
                return;
            }

            EnsureFolder("Assets/Scenes");
            EnsureFolder(GameplayPrefabDir);
            EnsureKoreanFont();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene 이 로드돼 있던 에셋을 언로드하므로, 이후 모든 에셋은 씬 생성 "뒤에" 만들거나 경로로 다시 불러온다
            GenAssets g = GenerateAssets();
            TMP_FontAsset font = FindFont();
            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ScenarioData scenario = AssetDatabase.LoadAssetAtPath<ScenarioData>(ApartmentScenarioPath);
            if (scenario == null) Debug.LogWarning("[ApartmentBuilder] Scenario_03 에셋을 찾지 못했습니다. GameSceneEntry 의 requiredScenario 를 직접 연결하세요.");

            GameplayAssets data = EnsureGameplayData();

            // 환경
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.18f); // 꺼진 방은 어둡다 → 손전등이 필요
            RenderSettings.fog = false;

            // 월드
            lvData = data;
            Transform level = BuildApartmentLevel(data.towel, data.extinguisher, data.key, data.tape, data.water);
            BuildEvacWorld(level);   // Act 3~4 월드: 복도 NPC, 계단참/옥상, 계단 하강, 집결지, 1003호, 대피공간 밀봉, 망치
            GameObject playerPrefab = BuildPlayerPrefab(data.towel);

            var gameplay = new GameObject("Gameplay").transform;
            level.SetParent(gameplay, true);

            // 창밖 풍경 (배산임수 아파트 단지). 단독 재생성은 Tools > GameUI > Build Outdoor World
            GameUI.EditorTools.Outdoor.OutdoorWorldBuilder.Build(gameplay);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetParent(gameplay, false);
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);

            // 시스템
            Systems sys = BuildSystems(gameplay, data, player);

            // 이벤트 시스템 + UI
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            BuildGameplayUI(gameplay, g, font, buttonPrefab, player, sys, data);

            // 씬 진입 관문 (선택한 시나리오 검사 → "준비 중" 안내)
            BuildSceneEntry(gameplay, scenario, g, font);

            EditorSceneManager.SaveScene(scene, GameScenePath);
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ApartmentBuilder] 완료: GameScene(아파트 화재 1004호), Player 프리팹, 데이터 에셋이 생성되었습니다.");
        }

        // ═════════════════════════ 데이터 에셋 ═════════════════════════
        private struct GameplayAssets
        {
            public ItemData towel, extinguisher, key, tape, water, towelDry, waterPot, hammer, roofKey;
            public ObjectiveData[] evacObjectives;
            public ScoreRules scoreRules;
            public EndingData hiddenEnding;
            public DeathReasonData[] deaths;
            public DeathReasonData fallback;
            public EndingData[] endings;
            public ScoreCategoryData[] scores;
            public ObjectiveData[] objectives;
            public System.Collections.Generic.Dictionary<string, ObjectiveData> act1, act2;
        }

        /// <summary>없을 때만 생성 (이미 있으면 사용자가 수정한 내용을 보존)</summary>
        private static T Ensure<T>(string path, Action<SerializedObject> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            // 파일은 있는데 읽히지 않는 에셋(예전 빌더가 남긴 깨진 자리표시)은 CreateAsset 이 덮어쓰지 못해 씬 참조가 {fileID: 0} 으로 끊어진다.
            // 지우고 새로 만든다 (사용자가 직접 쓴 내용은 읽히는 에셋이므로 이 경로를 타지 않는다)
            if (File.Exists(path))
            {
                Debug.LogWarning($"[ApartmentBuilder] 읽을 수 없는 에셋을 지우고 다시 만듭니다: {path}");
                AssetDatabase.DeleteAsset(path);
            }

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            init(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ItemData EnsureItem(string file, string id, string name, string desc, bool equippable, string colorHex)
            => Ensure<ItemData>($"{ItemDir}/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = name;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("equippable").boolValue = equippable;
                so.FindProperty("fallbackColor").colorValue = Hex(colorHex);
            });

        private static DeathReasonData EnsureDeath(string file, DeathReason reason, string title, string why, string correct)
        {
            int index = Array.IndexOf(Enum.GetValues(typeof(DeathReason)), reason);
            var asset = Ensure<DeathReasonData>($"{DeathDir}/{file}.asset", so =>
            {
                so.FindProperty("reason").enumValueIndex = index;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("whyYouDied").stringValue = why;
                so.FindProperty("correctAction").stringValue = correct;
            });

            // 복구: 예전 빌더 실행이 남긴 "빈 자리표시 에셋"(reason=Unknown, 교육 문구 없음)은 사망 화면이 일반 문구로만 나오는 원인이다.
            // 문구가 둘 다 비어 있을 때만 채운다 — 사용자가 직접 고친 문구는 건드리지 않는다.
            if (reason != DeathReason.Unknown)
            {
                var fix = new SerializedObject(asset);
                bool blank = string.IsNullOrEmpty(fix.FindProperty("whyYouDied").stringValue) && string.IsNullOrEmpty(fix.FindProperty("correctAction").stringValue);
                if (blank)
                {
                    fix.FindProperty("reason").enumValueIndex = index;
                    fix.FindProperty("title").stringValue = title;
                    fix.FindProperty("whyYouDied").stringValue = why;
                    fix.FindProperty("correctAction").stringValue = correct;
                    fix.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    Debug.Log($"[ApartmentBuilder] 비어 있던 사망 사유 에셋 복구: {file} → {reason}");
                }
            }
            return asset;
        }

        private static EndingData EnsureEnding(string file, string id, string name, string desc, bool survival)
            => Ensure<EndingData>($"{EndingDir}/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("endingName").stringValue = name;
                so.FindProperty("description").stringValue = desc;
                so.FindProperty("isSurvival").boolValue = survival;
            });

        private static ScoreCategoryData EnsureScore(string file, string id, string label, int max)
            => Ensure<ScoreCategoryData>($"{ScoreDir}/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("label").stringValue = label;
                so.FindProperty("maxPoints").intValue = max;
            });

        private static ObjectiveData EnsureObjective(string file, string id, string title, bool main)
            => Ensure<ObjectiveData>($"{ObjectiveDir}/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("title").stringValue = title;
                so.FindProperty("isMain").boolValue = main;
            });

        private static GameplayAssets EnsureGameplayData()
        {
            var d = new GameplayAssets
            {
                towel = EnsureItem("Item_WetTowel", "wet_towel", "젖은 수건", "물에 적신 수건. 입과 코를 막으면 연기를 덜 들이마신다.", true, "38BDF8"),
                extinguisher = EnsureItem("Item_Extinguisher", "extinguisher", "소화기", "초기 화재를 진압할 때 쓴다.", false, "DC2626"),
                key = EnsureItem("Item_Key", "key", "열쇠", "어딘가의 열쇠.", false, "FACC15"),
                tape = EnsureItem("Item_DuctTape", "duct_tape", "청테이프", "문틈을 막는 데 쓸 수 있다.", false, "9CA3AF"),
                water = EnsureItem("Item_WaterBottle", "water_bottle", "생수병", "마실 물. 수건을 적실 때도 쓸 수 있다.", false, "60A5FA"),
                towelDry = EnsureItem("Item_Towel", "towel", "수건", "마른 수건. 세면대에서 물에 적시면 연기를 막는 데 쓸 수 있다.", false, "E5E7EB"),
                waterPot = EnsureItem("Item_WaterPot", "water_pot", "물 담긴 냄비", "물이 가득 담긴 냄비.", false, "93C5FD"),
            };

            d.fallback = EnsureDeath("Death_Unknown", DeathReason.Unknown, "사망", "위험한 상황에서 목숨을 잃었습니다.", "침착하게 상황을 파악하고, 안전한 경로로 대피하세요.");
            d.deaths = new[]
            {
                EnsureDeath("Death_Suffocation", DeathReason.Suffocation, "질식 사망",
                    "화재 사망자의 대부분은 불길이 아니라 연기와 유독가스 때문에 숨집니다. 연기를 오래 들이마시면 의식을 잃고 질식하게 됩니다.",
                    "연기는 위로 모이므로 몸을 낮추고, 젖은 수건으로 코와 입을 막은 채 이동하세요. 연기가 심한 경로는 피하고 대피공간이나 안전한 경로를 선택합니다."),
                EnsureDeath("Death_GasExplosion", DeathReason.GasExplosion, "가스 폭발",
                    "가스가 새는 곳에서 불꽃이나 전기 스위치를 조작하면 폭발할 수 있습니다.",
                    "가스 냄새가 나면 즉시 가스 밸브를 잠그고 창문을 열어 환기하세요. 전등·가전 스위치는 건드리지 말고, 안전한 곳으로 이동해 신고합니다."),
                EnsureDeath("Death_Burn", DeathReason.Burn, "화상",
                    "불길이나 고온의 열기에 직접 노출되어 치명적인 화상을 입었습니다.",
                    "불이 번진 곳에는 접근하지 말고, 문손잡이가 뜨거우면 열지 마세요. 다른 경로나 대피공간으로 이동합니다."),
                EnsureDeath("Death_Fall", DeathReason.Fall, "추락",
                    "무리하게 창밖으로 탈출하려다 추락했습니다.",
                    "건물 밖으로 뛰어내리지 말고 옥상·경량칸막이·대피공간 등 정해진 경로를 이용하세요. 구조를 기다릴 때는 창가에서 위치를 알립니다."),
                EnsureDeath("Death_Poisoning", DeathReason.Poisoning, "가스 중독",
                    "밀폐된 공간에서 새어 나온 가스를 들이마셔 의식을 잃었습니다.",
                    "가스 냄새가 나면 즉시 밸브를 잠그고, 창문을 열어 환기한 뒤 바깥 공기가 있는 곳으로 이동하세요."),
                EnsureDeath("Death_GasIgnition", DeathReason.GasIgnition, "가스 폭발",
                    "가스가 새는 상황에서는 전기 스위치의 작은 불꽃도 폭발을 일으킬 수 있습니다.",
                    "전기 기구를 조작하지 말고 밸브를 잠근 뒤 환기하세요."),
                EnsureDeath("Death_MultitapFire", DeathReason.MultitapFire, "멀티탭 화재",
                    "과열된 멀티탭의 전원 스위치를 누르면 접점에서 스파크가 튀어 불이 붙을 수 있습니다. 주변에 종이나 옷가지, 가스가 있으면 더욱 위험합니다.",
                    "전원 스위치를 누르지 말고 코드(플러그) 본체를 잡고 조심스럽게 뽑으세요. 주변의 가연물도 미리 치워 둡니다."),
                EnsureDeath("Death_OilFire", DeathReason.OilFireExplosion, "기름불 폭발",
                    "뜨거운 식용유에 물을 붓자 물이 순식간에 끓어 기름과 함께 폭발적으로 튀며 불기둥이 솟았습니다.",
                    "식용유 불에는 절대 물을 붓지 마세요. 가스 밸브를 잠근 뒤 젖은 수건이나 뚜껑으로 덮어 공기를 차단하거나 소화기를 사용합니다."),
                EnsureDeath("Death_ElectricShock", DeathReason.ElectricShock, "감전 사망",
                    "전기가 흐르는 멀티탭의 불에 물을 붓자 물을 타고 전류가 흘러 감전되었습니다.",
                    "전기 화재에는 물을 쓰지 마세요. 가능하면 플러그를 먼저 뽑고, 분말 소화기로 진압하거나 거리를 두고 대피합니다."),
                EnsureDeath("Death_DoorBackdraft", DeathReason.DoorBackdraft, "복도 화염 유입",
                    "뜨거워진 현관문을 확인 없이 열자 산소가 유입되며 복도의 불길과 열기가 한꺼번에 밀려들었습니다.",
                    "문을 열기 전에 문손잡이와 문틈을 손등으로 확인하세요. 뜨겁다면 열지 말고 문틈을 막아 연기를 차단한 뒤 대피공간이나 다른 경로를 이용합니다."),
                EnsureDeath("Death_ElevatorFireball", DeathReason.ElevatorFireball, "엘리베이터 화염",
                    "화재 중 엘리베이터는 정전·고장으로 멈추거나, 열기와 불길이 승강로를 타고 이동합니다. 문이 열리는 순간 화염이 덮쳤습니다.",
                    "화재 시 엘리베이터는 절대 사용하지 마세요. 반드시 비상계단(방화문)으로 대피하고, 계단이 막혔다면 대피공간이나 구조를 기다릴 수 있는 곳을 찾습니다."),
                EnsureDeath("Death_ElevatorSmoke", DeathReason.ElevatorSmoke, "엘리베이터 질식",
                    "화재 중 엘리베이터는 정전으로 갇히기 쉽고, 승강로는 굴뚝처럼 연기와 유독가스를 끌어올립니다. 좁은 칸 안에서 연기를 들이마셔 질식했습니다.",
                    "화재 시 엘리베이터는 타지 말고 계단으로 대피하세요. 이미 갇혔다면 비상호출 버튼으로 알리고 몸을 낮춰 젖은 수건으로 코와 입을 막습니다."),
                EnsureDeath("Death_BurnInjury", DeathReason.BurnInjury, "심한 화상",
                    "뜨거워진 버튼이나 불길에 반복해서 닿아 치명적인 화상을 입었습니다. 화재 현장에서는 금속·문·버튼도 매우 뜨거울 수 있습니다.",
                    "불길과 뜨거운 물체에는 접근하지 마세요. 손등으로 먼저 온도를 확인하고, 뜨겁다면 다른 경로를 선택합니다."),
                EnsureDeath("Death_FallFromHeight", DeathReason.FallFromHeight, "추락 사고",
                    "높은 층에서의 뛰어내림은 매우 위험합니다. 구조대를 기다리거나 안전한 대피 경로를 이용하세요.",
                    "창밖이나 난간 밖으로 나가지 말고 옥상·경량칸막이·대피공간 등 정해진 경로를 이용하세요. 구조를 기다릴 때는 창가에서 위치를 알립니다."),
            };

            d.endings = new[]
            {
                EnsureEnding("Ending_Rooftop", "ending_rooftop", "옥상 대피", "(작성 예정) 옥상으로 올라가 구조를 기다렸다.", true),
                EnsureEnding("Ending_Partition", "ending_partition", "경량칸막이 대피", "(작성 예정) 경량칸막이를 부수고 옆집으로 대피했다.", true),
                EnsureEnding("Ending_Shelter", "ending_shelter", "대피공간 농성", "(작성 예정) 대피공간에서 구조대를 기다렸다.", true),
                EnsureEnding("Ending_Descent", "ending_descent", "1층 하강 탈출", "(작성 예정) 완강기로 1층까지 내려왔다.", true),
            };

            d.scores = new[]
            {
                EnsureScore("Score_Call119", "score_call119", "119 신고", 25),
                EnsureScore("Score_GasShutoff", "score_gas_shutoff", "가스 차단", 25),
                EnsureScore("Score_Breathing", "score_breathing", "호흡 보호", 25),
                EnsureScore("Score_SafeRoute", "score_safe_route", "안전한 경로 선택", 25),
            };

            d.objectives = new ObjectiveData[0];
            d.act1 = new System.Collections.Generic.Dictionary<string, ObjectiveData>
            {
                ["morning"] = EnsureObjective("Act1_Morning", "act1_morning", "아침 준비하기", true),
                ["alarm"] = EnsureObjective("Act1_Alarm", "act1_alarm", "알람 끄기", false),
                ["wash"] = EnsureObjective("Act1_Wash", "act1_wash", "세수하기", false),
                ["drink"] = EnsureObjective("Act1_Drink", "act1_drink", "물 마시기", false),
                ["phone"] = EnsureObjective("Act1_Phone", "act1_phone", "스마트폰 확인하기", false),
                ["smell"] = EnsureObjective("Act1_Smell", "act1_smell", "이상한 냄새의 원인 찾기", false),
                ["gas"] = EnsureObjective("Act1_GasResponse", "act1_gas_response", "가스 누출에 대응하기", true),
                ["flash"] = EnsureObjective("Act1_Flashlight", "act1_flashlight", "스마트폰 손전등 켜기", false),
                ["fuse"] = EnsureObjective("Act1_Fuse", "act1_fuse", "주방 중간 밸브(퓨즈콕) 잠그기", false),
                ["main"] = EnsureObjective("Act1_MainValve", "act1_main_valve", "다용도실 메인 가스 밸브 잠그기", false),
                ["vent"] = EnsureObjective("Act1_Ventilate", "act1_ventilate", "거실·주방·안방 창문을 열어 환기하기", false),
                ["multitap"] = EnsureObjective("Act1_Multitap", "act1_multitap", "과열된 멀티탭 처리하기", false),
            };
            d.act2 = new System.Collections.Generic.Dictionary<string, ObjectiveData>
            {
                ["main"] = EnsureObjective("Act2_Main", "act2_main", "화재를 진압하거나 차단하고 탈출 준비하기", true),
                ["ext"] = EnsureObjective("Act2_Extinguisher", "act2_extinguisher", "소화기를 찾아 안전핀 뽑기", false),
                ["suppress"] = EnsureObjective("Act2_Suppress", "act2_suppress", "거실·주방 불길 진압하기", false),
                ["towel"] = EnsureObjective("Act2_Towel", "act2_towel", "화장실에서 수건을 적셔 입과 코 막기", false),
                ["bottle"] = EnsureObjective("Act2_Bottle", "act2_bottle", "생수병 챙기기", false),
                ["handle"] = EnsureObjective("Act2_Handle", "act2_handle", "현관문 손잡이 온도 확인하기", false),
            };
            EnsureEvacData(ref d);   // Act 3~4: 망치/옥상 열쇠, 새 사망 사유, 엔딩 문구·히든 엔딩, 목표, ScoreRules
            return d;
        }

        // ═════════════════════════ 플레이어 프리팹 ═════════════════════════
        private static GameObject BuildPlayerPrefab(ItemData towel)
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            var cc = root.AddComponent<CharacterController>();
            cc.height = PlayerHeight; cc.radius = PlayerRadius; cc.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            cc.slopeLimit = 45f; cc.stepOffset = 0.3f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;

            var input = root.AddComponent<GameInput>();
            var controller = root.AddComponent<PlayerController>();
            var interactor = root.AddComponent<PlayerInteractor>();
            var vitals = root.AddComponent<PlayerVitals>();
            var inventory = root.AddComponent<InventorySystem>();
            var extinguisher = root.AddComponent<ExtinguisherController>();

            var camRoot = new GameObject("CameraRoot").transform;
            camRoot.SetParent(root.transform, false);
            camRoot.localPosition = new Vector3(0f, PlayerHeight - PlayerEyeBelowTop + PlayerCameraHeightOffset, 0f);   // 눈높이 1.6 에서 6cm 낮춤

            var camGo = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camRoot, false);
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = PlayerFov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1800f;   // 창밖 산/하천/먼 동까지
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Hex("8FB4D9");

            // 손전등 (스마트폰 UI 에서 켜고 끈다)
            var flashGo = new GameObject("Flashlight", typeof(Light));
            flashGo.transform.SetParent(camGo.transform, false);
            var flash = flashGo.GetComponent<Light>();
            flash.type = LightType.Spot; flash.range = 14f; flash.spotAngle = 60f; flash.innerSpotAngle = 35f;
            flash.intensity = 4f; flash.shadows = LightShadows.None; flash.enabled = false;

            // 기침 소리 슬롯 (클립은 비워둠)
            var coughGo = new GameObject("CoughAudio", typeof(AudioSource));
            coughGo.transform.SetParent(root.transform, false);
            var cough = coughGo.GetComponent<AudioSource>();
            cough.playOnAwake = false; cough.spatialBlend = 0f;

            // 착지 발소리 슬롯 (클립은 비워둠)
            var stepGo = new GameObject("FootstepAudio", typeof(AudioSource));
            stepGo.transform.SetParent(root.transform, false);
            var step = stepGo.GetComponent<AudioSource>();
            step.playOnAwake = false; step.spatialBlend = 0f;

            var so = SO(controller);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("cameraRoot").objectReferenceValue = camRoot;
            so.FindProperty("footstepSource").objectReferenceValue = step;
            so.FindProperty("standHeight").floatValue = PlayerHeight;
            so.FindProperty("crouchHeight").floatValue = 1.05f;
            so.FindProperty("eyeBelowTop").floatValue = PlayerEyeBelowTop;
            so.FindProperty("cameraHeightOffset").floatValue = PlayerCameraHeightOffset;
            so.FindProperty("walkSpeed").floatValue = PlayerWalk;
            so.FindProperty("sprintSpeed").floatValue = PlayerSprint;
            so.FindProperty("crouchSpeed").floatValue = PlayerCrouchSpeed;
            so.FindProperty("jumpHeight").floatValue = PlayerJump;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = SO(interactor);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("interactDistance").floatValue = PlayerInteractDistance;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = SO(vitals);
            so.FindProperty("player").objectReferenceValue = controller;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("wetTowelItem").objectReferenceValue = towel;
            so.FindProperty("coughSource").objectReferenceValue = cough;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 소화기 분사 파티클 (카메라 앞 오른쪽 아래)
            ParticleSystem spray = MakeParticles(camGo.transform, "ExtinguisherSpray", new Vector3(0.12f, -0.25f, 0.5f), Vector3.zero, ParticleMat("Powder", new Color(0.95f, 0.95f, 0.95f, 0.55f), false));
            var sm = spray.main;
            sm.startLifetime = 0.7f; sm.startSpeed = 7f; sm.gravityModifier = 0.05f;
            sm.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            sm.startColor = new Color(0.95f, 0.95f, 0.95f, 0.55f);
            var sem = spray.emission; sem.rateOverTime = 140f;
            var ssh = spray.shape; ssh.shapeType = ParticleSystemShapeType.Cone; ssh.angle = 10f; ssh.radius = 0.02f;
            var ssz = spray.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var sprayAudioGo = new GameObject("SprayAudio", typeof(AudioSource));
            sprayAudioGo.transform.SetParent(camGo.transform, false);
            var sprayAudio = sprayAudioGo.GetComponent<AudioSource>();
            sprayAudio.playOnAwake = false; sprayAudio.loop = true; sprayAudio.spatialBlend = 0f;

            so = SO(extinguisher);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("extinguisherItem").objectReferenceValue = lvData.extinguisher;
            so.FindProperty("sprayFx").objectReferenceValue = spray;
            so.FindProperty("sprayAudio").objectReferenceValue = sprayAudio;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = SO(inventory);
            so.FindProperty("input").objectReferenceValue = input;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ═════════════════════════ 시스템 ═════════════════════════
        private struct Systems
        {
            public GameClock clock;
            public ObjectiveSystem objectives;
            public ScoreSystem score;
            public DeathAndEnding deathAndEnding;
            public HazardManager hazard;
        }

        private static Systems BuildSystems(Transform parent, GameplayAssets d, GameObject player)
        {
            var root = new GameObject("Systems");
            root.transform.SetParent(parent, false);

            var s = new Systems
            {
                clock = root.AddComponent<GameClock>(),
                objectives = root.AddComponent<ObjectiveSystem>(),
                score = root.AddComponent<ScoreSystem>(),
                deathAndEnding = root.AddComponent<DeathAndEnding>(),
                hazard = root.AddComponent<HazardManager>(),
            };
            WireTiming(root, s.clock);   // 전체 템포 설정 + F9 타이밍 오버레이
            WireElevator(root);          // 엘리베이터 위험 확률 설정
            var machine = root.AddComponent<ScenarioStateMachine>();
            var fireManager = root.AddComponent<FireManager>();
            var atmosphere = root.AddComponent<SmokeAtmosphere>();
            var playerCtrl = player.GetComponent<PlayerController>();
            var fmSo = SO(fireManager); fmSo.FindProperty("player").objectReferenceValue = playerCtrl; fmSo.ApplyModifiedPropertiesWithoutUndo();
            var atSo = SO(atmosphere); atSo.FindProperty("player").objectReferenceValue = playerCtrl; atSo.ApplyModifiedPropertiesWithoutUndo();
            var gas = root.AddComponent<GasLeakController>();
            ConfigureGas(gas);

            // Act 4개 (Act 1~2: 본 구현 / Act 3~4: 복도·이웃, 루트 A~D — MainMenuBuilder.Evac.*.cs 에서 연결)
            var actsRoot = new GameObject("Acts").transform;
            actsRoot.SetParent(root.transform, false);
            var act1 = AddAct<Act1_MorningSigns>(actsRoot, "Act1_MorningSigns", "아침의 징후", "평범한 아침, 가스 냄새 등 이상 징후를 발견한다.");
            ActBase a1 = act1;
            var act2 = AddAct<Act2_BlastAndSmoke>(actsRoot, "Act2_BlastAndSmoke", "8층의 폭발과 연쇄 화재", "아래층 폭발로 인한 2차 화재를 진압하거나 차단하고 탈출을 준비한다.");
            ActBase a2 = act2;
            ActBase a3 = AddAct<Act3_FireSpreads>(actsRoot, "Act3_FireSpreads", "번지는 화재", "연쇄 화재, 대피 경로를 판단한다.");
            ActBase a4 = AddAct<Act4_Evacuation>(actsRoot, "Act4_Evacuation", "대피", "옥상 / 경량칸막이 / 대피공간 / 1층 하강 중 탈출 루트를 선택한다.");
            ConfigureAct1(act1, gas, d, root.transform);
            ConfigureAct2(act2, gas, fireManager, d, root.transform, player);
            ConfigureEvacSystems(root.transform, (Act3_FireSpreads)a3, (Act4_Evacuation)a4, d, player, s.score);
            var mso = SO(machine);
            SetObjectArray(mso.FindProperty("acts"), new[] { a1, a2, a3, a4 });
            mso.FindProperty("autoStart").boolValue = true;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var oso = SO(s.objectives);
            SetObjectArray(oso.FindProperty("initialObjectives"), d.objectives);
            oso.ApplyModifiedPropertiesWithoutUndo();

            var sso = SO(s.score);
            SetObjectArray(sso.FindProperty("categories"), d.scores);
            var grades = sso.FindProperty("grades");
            string[] gl = { "D", "C", "B", "A" };
            int[] gm = { 0, 40, 60, 85 };
            grades.arraySize = gl.Length;
            for (int i = 0; i < gl.Length; i++)
            {
                SerializedProperty e = grades.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("minScore").intValue = gm[i];
                e.FindPropertyRelative("label").stringValue = gl[i];
            }
            sso.ApplyModifiedPropertiesWithoutUndo();

            return s;
        }

        // ───────── Act 1 / 가스 연결 ─────────
        private static T Obj<T>(string id) where T : Component
        {
            Transform t = lvInteract.Find(id);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static void SetStrings(SerializedProperty p, string[] values)
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static ToggleInteractable[] WindowSet()
            => new[] { Obj<ToggleInteractable>(Ids.WindowLiving), Obj<ToggleInteractable>(Ids.WindowKitchen), Obj<ToggleInteractable>(Ids.WindowBedroom) };

        private static void ConfigureGas(GasLeakController gas)
        {
            var so = SO(gas);
            SetObjectArray(so.FindProperty("valves"), new[] { Obj<ValveInteractable>(Ids.FuseCock), Obj<ValveInteractable>(Ids.GasValveMain) });
            SetObjectArray(so.FindProperty("windows"), WindowSet());
            so.FindProperty("leakAudio").objectReferenceValue = lvLeakAudio;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAct1(Act1_MorningSigns act, GasLeakController gas, GameplayAssets d, Transform systemsRoot)
        {
            // 긴급 방송 오디오 슬롯 (2D, 클립은 비워둠)
            var annGo = new GameObject("AnnouncementAudio", typeof(AudioSource));
            annGo.transform.SetParent(systemsRoot, false);
            var ann = annGo.GetComponent<AudioSource>();
            ann.playOnAwake = false; ann.spatialBlend = 0f;

            var so = SO(act);
            so.FindProperty("gas").objectReferenceValue = gas;
            so.FindProperty("fuseCock").objectReferenceValue = Obj<ValveInteractable>(Ids.FuseCock);
            so.FindProperty("mainValve").objectReferenceValue = Obj<ValveInteractable>(Ids.GasValveMain);
            SetObjectArray(so.FindProperty("windows"), WindowSet());

            so.FindProperty("alarmId").stringValue = Ids.AlarmClock;
            so.FindProperty("washId").stringValue = Ids.WashFace;
            so.FindProperty("drinkId").stringValue = Ids.DrinkWater;
            so.FindProperty("multitapSwitchId").stringValue = Ids.MultitapSwitch;
            so.FindProperty("multitapPlugId").stringValue = Ids.MultitapPlug;
            SetStrings(so.FindProperty("clutterIds"), new[] { Ids.Clutter1, Ids.Clutter2, Ids.Clutter3 });
            // 전기/화기: 전등 스위치, 커피포트, 전자레인지, 주방 후드, 가스레인지
            SetStrings(so.FindProperty("dangerIds"), new[] { Ids.SwitchBedroom, Ids.SwitchLiving, Ids.CoffeePot, Ids.Microwave, Ids.RangeHood, Ids.GasRange });

            ObjectiveData O(string k) => d.act1[k];
            so.FindProperty("objMorning").objectReferenceValue = O("morning");
            so.FindProperty("objAlarm").objectReferenceValue = O("alarm");
            so.FindProperty("objWash").objectReferenceValue = O("wash");
            so.FindProperty("objDrink").objectReferenceValue = O("drink");
            so.FindProperty("objPhone").objectReferenceValue = O("phone");
            so.FindProperty("objSmell").objectReferenceValue = O("smell");
            so.FindProperty("objGasResponse").objectReferenceValue = O("gas");
            so.FindProperty("objFlashlight").objectReferenceValue = O("flash");
            so.FindProperty("objFuse").objectReferenceValue = O("fuse");
            so.FindProperty("objMainValve").objectReferenceValue = O("main");
            so.FindProperty("objVentilate").objectReferenceValue = O("vent");
            so.FindProperty("objMultitap").objectReferenceValue = O("multitap");

            so.FindProperty("alarmSource").objectReferenceValue = lvAlarmAudio;
            so.FindProperty("sparkAudio").objectReferenceValue = lvSparkAudio;
            so.FindProperty("announcementSource").objectReferenceValue = ann;
            so.FindProperty("sparkParticles").objectReferenceValue = lvSparks;

            so.FindProperty("smellText").stringValue = "희미한 가스 냄새가 난다";
            so.FindProperty("smellStrongText").stringValue = "가스 냄새가 점점 짙어진다. 머리가 어지럽다.";

            string[] cueText =
            {
                "긴급 안내 말씀드립니다. 현재 10층 라인 도시가스 배관 압력 이상으로 가스 누출이 감지되었습니다.",
                "세대 내 화기 및 전기 스위치 조작을 즉시 중단하시고",
                "창문을 열어 환기해 주십시오.",
            };
            float[] cueSeconds = { 6.5f, 4.5f, 3.5f };
            SerializedProperty cues = so.FindProperty("announcementCues");
            cues.arraySize = cueText.Length;
            for (int i = 0; i < cueText.Length; i++)
            {
                SerializedProperty e = cues.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("text").stringValue = cueText[i];
                e.FindPropertyRelative("seconds").floatValue = cueSeconds[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAct2(Act2_BlastAndSmoke act, GasLeakController gas, FireManager fires, GameplayAssets d, Transform systemsRoot, GameObject player)
        {
            var expGo = new GameObject("ExplosionAudio", typeof(AudioSource));
            expGo.transform.SetParent(systemsRoot, false);
            var exp = expGo.GetComponent<AudioSource>();
            exp.playOnAwake = false; exp.spatialBlend = 0f;

            var so = SO(act);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            so.FindProperty("gas").objectReferenceValue = gas;
            so.FindProperty("fireManager").objectReferenceValue = fires;
            SetObjectArray(so.FindProperty("initialFires"), new[] { fireCurtain, firePan });
            so.FindProperty("corridorFire").objectReferenceValue = fireCorridor;
            so.FindProperty("frontDoor").objectReferenceValue = Obj<ToggleInteractable>(Ids.FrontDoor);

            var glass = new System.Collections.Generic.List<GameObject>();
            Transform t = lvStructure.Find("Wall_BalconyNorth/balcony_glass"); if (t != null) glass.Add(t.gameObject);
            t = lvInteract.Find(Ids.BalconyDoor + "/Pane"); if (t != null) glass.Add(t.gameObject);
            t = lvInteract.Find(Ids.WindowLiving + "/Pane"); if (t != null) glass.Add(t.gameObject);
            SetObjectArray(so.FindProperty("glassToShatter"), glass.ToArray());
            SetObjectArray(so.FindProperty("brokenOpenings"), new[] { Obj<ToggleInteractable>(Ids.BalconyDoor), Obj<ToggleInteractable>(Ids.WindowLiving) });
            so.FindProperty("glassShards").objectReferenceValue = lvGlassShards;
            so.FindProperty("explosionAudio").objectReferenceValue = exp;

            so.FindProperty("handleCheckId").stringValue = Ids.DoorHandleCheck;
            so.FindProperty("frontDoorId").stringValue = Ids.FrontDoor;

            ObjectiveData O(string k) => d.act2[k];
            so.FindProperty("objMain").objectReferenceValue = O("main");
            so.FindProperty("objExtinguisher").objectReferenceValue = O("ext");
            so.FindProperty("objSuppress").objectReferenceValue = O("suppress");
            so.FindProperty("objTowel").objectReferenceValue = O("towel");
            so.FindProperty("objBottle").objectReferenceValue = O("bottle");
            so.FindProperty("objHandle").objectReferenceValue = O("handle");

            so.FindProperty("explosionText").stringValue = "쾅! 아래층에서 거대한 폭발이 일어났다. 건물이 흔들리고 불이 모두 꺼졌다.";
            so.FindProperty("fireText").stringValue = "깨진 베란다 창으로 불길이 들어와 커튼과 주방 팬에 옮겨붙었다!";
            so.FindProperty("smokeText").stringValue = "검은 연기가 천장부터 차오른다. 자세를 낮춰야 한다.";
            so.FindProperty("allOutText").stringValue = "불길을 모두 잡았다. 연기가 서서히 걷힌다.";
            so.FindProperty("corridorFireText").stringValue = "현관 밖에서 무언가 타는 소리가 들린다.";
            so.FindProperty("handleHotText").stringValue = "손등을 대자 문손잡이가 뜨겁다! 복도에 불길이 있다. 현관으로는 나갈 수 없다.";
            so.FindProperty("handleCoolText").stringValue = "문손잡이는 미지근하다. 복도에는 아직 불길이 번지지 않은 듯하다.";
            so.FindProperty("hotDoorLockedMessage").stringValue = "문손잡이가 뜨겁다. 열면 불길이 밀려든다.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T AddAct<T>(Transform parent, string name, string title, string desc) where T : ActBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var act = go.AddComponent<T>();
            var so = SO(act);
            so.FindProperty("actTitle").stringValue = title;
            so.FindProperty("actDescription").stringValue = desc;
            so.ApplyModifiedPropertiesWithoutUndo();
            return act;
        }

        // ═════════════════════════ 씬 진입 관문 ═════════════════════════
        private static void BuildSceneEntry(Transform gameplay, ScenarioData scenario, GenAssets g, TMP_FontAsset font)
        {
            // "준비 중" 안내 캔버스 (기본 비활성)
            var noticeGo = new GameObject("NoticeRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = noticeGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = noticeGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Image bg = CreateImage("Background", noticeGo.transform, g.white, Image.Type.Simple, true);
            Stretch(bg.rectTransform, 0);
            bg.color = Color.black;
            TMP_Text txt = CreateText("NoticeText", noticeGo.transform, "준비 중", font, 48, TextAlignmentOptions.Center);
            txt.textWrappingMode = TextWrappingModes.Normal;
            txt.color = TextMain;
            Stretch(txt.rectTransform, 120);

            var camGo = new GameObject("NoticeCamera", typeof(Camera));
            camGo.transform.SetParent(noticeGo.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            noticeGo.SetActive(false);

            var entryGo = new GameObject("SceneEntry", typeof(GameSceneEntry));
            var so = SO(entryGo.GetComponent<GameSceneEntry>());
            so.FindProperty("requiredScenario").objectReferenceValue = scenario;
            SetObjectArray(so.FindProperty("gameplayRoots"), new[] { gameplay.gameObject });
            so.FindProperty("noticeRoot").objectReferenceValue = noticeGo;
            so.FindProperty("noticeText").objectReferenceValue = txt;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════════ UI ═════════════════════════
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static TMP_Text GameText(string name, Transform parent, string text, TMP_FontAsset font, float size, TextAlignmentOptions align,
            Color color, bool wrap = false)
        {
            TMP_Text t = CreateText(name, parent, text, font, size, align);
            t.color = color;
            if (wrap) t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private static CanvasGroup GroupWithPanel(string name, Transform parent, GenAssets g, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color panelColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, pivot, pos, size);
            Image bg = CreateImage("Bg", go.transform, g.panel, Image.Type.Sliced, false);
            Stretch(bg.rectTransform, 0);
            bg.color = panelColor;
            return go.GetComponent<CanvasGroup>();
        }

        private static void BuildGameplayUI(Transform gameplay, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab, GameObject player, Systems sys, GameplayAssets data)
        {
            // Canvas
            var canvasGo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(gameplay, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform ct = canvasGo.transform;

            var input = player.GetComponent<GameInput>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var vitals = player.GetComponent<PlayerVitals>();
            var inventory = player.GetComponent<InventorySystem>();
            Light flashlight = player.GetComponentInChildren<Light>(true);

            // UI 효과음 슬롯
            var audioGo = new GameObject("UIAudio", typeof(AudioSource));
            audioGo.transform.SetParent(ct, false);
            var audio = audioGo.GetComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 0f;

            // ── 1) 호흡기 화면 효과 (가장 아래) ──
            Image fog = CreateImage("VitalsFog", ct, g.white, Image.Type.Simple, false);
            Stretch(fog.rectTransform, 0);
            fog.color = new Color(0.55f, 0.53f, 0.5f, 0f);
            Image vig = CreateImage("VitalsVignette", ct, null, Image.Type.Simple, false);
            Stretch(vig.rectTransform, 0);
            vig.color = new Color(0.8f, 0.05f, 0.05f, 0f);
            var fxGo = new GameObject("VitalsEffect", typeof(VitalsScreenEffect));
            fxGo.transform.SetParent(ct, false);
            var fxSo = SO(fxGo.GetComponent<VitalsScreenEffect>());
            fxSo.FindProperty("vitals").objectReferenceValue = vitals;
            fxSo.FindProperty("fogOverlay").objectReferenceValue = fog;
            fxSo.FindProperty("redVignette").objectReferenceValue = vig;
            fxSo.ApplyModifiedPropertiesWithoutUndo();

            // 가스 농도의 간접 표현 (노란 비네트 + 어지러움) — 수치는 표시하지 않는다
            Image gasVig = CreateImage("GasVignette", ct, null, Image.Type.Simple, false);
            Stretch(gasVig.rectTransform, 0);
            gasVig.color = new Color(0.95f, 0.85f, 0.25f, 0f);
            var gasFxGo = new GameObject("GasEffect", typeof(GasScreenEffect));
            gasFxGo.transform.SetParent(ct, false);
            var gasFxSo = SO(gasFxGo.GetComponent<GasScreenEffect>());
            gasFxSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            gasFxSo.FindProperty("yellowVignette").objectReferenceValue = gasVig;
            gasFxSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 2) HUD ──
            var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(HUD));
            hudGo.transform.SetParent(ct, false);
            Stretch((RectTransform)hudGo.transform, 0);
            Transform hud = hudGo.transform;

            Image crosshair = CreateImage("Crosshair", hud, g.radial, Image.Type.Simple, false);
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));

            CanvasGroup promptGroup = GroupWithPanel("Prompt", hud, g, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(620, 58), new Color(0, 0, 0, 0.55f));
            TMP_Text promptText = GameText("Text", promptGroup.transform, "[E] 상호작용", font, 30, TextAlignmentOptions.Center, TextMain);
            Stretch(promptText.rectTransform, 6);

            TMP_Text objectiveText = GameText("ObjectiveText", hud, string.Empty, font, 28, TextAlignmentOptions.TopLeft, TextMain, true);
            objectiveText.richText = true;
            Place(objectiveText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -48), new Vector2(760, 340));

            CanvasGroup banner = GroupWithPanel("ObjectiveBanner", hud, g, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(820, 80), WithA(PanelDark, 0.85f));
            TMP_Text bannerText = GameText("Text", banner.transform, string.Empty, font, 34, TextAlignmentOptions.Center, TextMain);
            bannerText.fontStyle = FontStyles.Bold;
            Stretch(bannerText.rectTransform, 8);

            TMP_Text clockText = GameText("ClockText", hud, "00:00", font, 40, TextAlignmentOptions.Right, TextMain);
            Place(clockText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -44), new Vector2(300, 56));

            // 호흡기 게이지
            var vitalsGo = new GameObject("VitalsGauge", typeof(RectTransform), typeof(CanvasGroup));
            vitalsGo.transform.SetParent(hud, false);
            Place((RectTransform)vitalsGo.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(520, 40));
            TMP_Text vitalsLabel = GameText("Label", vitalsGo.transform, "호흡", font, 24, TextAlignmentOptions.MidlineLeft, TextDim);
            Place(vitalsLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 40));
            Image vBg = CreateImage("BarBg", vitalsGo.transform, g.white, Image.Type.Simple, false);
            Place(vBg.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(400, 14));
            vBg.color = new Color(0, 0, 0, 0.55f);
            Image vFill = CreateImage("BarFill", vBg.transform, g.white, Image.Type.Filled, false);
            Stretch(vFill.rectTransform, 0);
            vFill.fillMethod = Image.FillMethod.Horizontal;
            vFill.fillOrigin = 0;
            vFill.fillAmount = 1f;

            // 인벤토리 바 (8칸)
            const int slotCount = 8;
            const float slotSize = 72f, slotGap = 8f;
            var invGo = new GameObject("InventoryBar", typeof(RectTransform));
            invGo.transform.SetParent(hud, false);
            Place((RectTransform)invGo.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(slotCount * (slotSize + slotGap), slotSize));
            var slotViews = new InventorySlotView[slotCount];
            for (int i = 0; i < slotCount; i++)
                slotViews[i] = BuildInventorySlot(invGo.transform, i, (i - (slotCount - 1) * 0.5f) * (slotSize + slotGap), slotSize, g, font);

            // 안내 문구
            CanvasGroup msgGroup = GroupWithPanel("Message", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 240), new Vector2(900, 60), new Color(0, 0, 0, 0.6f));
            TMP_Text msgText = GameText("Text", msgGroup.transform, string.Empty, font, 30, TextAlignmentOptions.Center, TextMain);
            Stretch(msgText.rectTransform, 6);

            // 길게 누르기 진행 바
            var holdGo = new GameObject("HoldBar", typeof(RectTransform), typeof(CanvasGroup));
            holdGo.transform.SetParent(hud, false);
            Place((RectTransform)holdGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(260, 10));
            Image holdBg = CreateImage("Bg", holdGo.transform, g.white, Image.Type.Simple, false);
            Stretch(holdBg.rectTransform, 0);
            holdBg.color = new Color(0, 0, 0, 0.6f);
            Image holdFill = CreateImage("Fill", holdGo.transform, g.white, Image.Type.Filled, false);
            Stretch(holdFill.rectTransform, 0);
            holdFill.fillMethod = Image.FillMethod.Horizontal;
            holdFill.fillOrigin = 0;
            holdFill.fillAmount = 0f;
            holdFill.color = Accent;

            // 감각 묘사 (냄새 등, 옅은 노란 글씨)
            var scentGo = new GameObject("Scent", typeof(RectTransform), typeof(CanvasGroup));
            scentGo.transform.SetParent(hud, false);
            Place((RectTransform)scentGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(1100, 50));
            TMP_Text scentText = GameText("Text", scentGo.transform, string.Empty, font, 30, TextAlignmentOptions.Center, new Color(0.96f, 0.88f, 0.42f));
            scentText.fontStyle = FontStyles.Italic;
            Stretch(scentText.rectTransform, 0);

            // 자막
            CanvasGroup subGroup = GroupWithPanel("Subtitle", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(1300, 96), new Color(0, 0, 0, 0.65f));
            TMP_Text subText = GameText("Text", subGroup.transform, string.Empty, font, 30, TextAlignmentOptions.Center, TextMain, true);
            Stretch(subText.rectTransform, 10);
            var subDisplay = subGroup.gameObject.AddComponent<SubtitleDisplay>();
            var subSo = SO(subDisplay);
            subSo.FindProperty("group").objectReferenceValue = subGroup;
            subSo.FindProperty("text").objectReferenceValue = subText;
            subSo.ApplyModifiedPropertiesWithoutUndo();

            var hudSo = SO(hudGo.GetComponent<HUD>());
            hudSo.FindProperty("interactor").objectReferenceValue = interactor;
            hudSo.FindProperty("vitals").objectReferenceValue = vitals;
            hudSo.FindProperty("inventory").objectReferenceValue = inventory;
            hudSo.FindProperty("objectives").objectReferenceValue = sys.objectives;
            hudSo.FindProperty("clock").objectReferenceValue = sys.clock;
            hudSo.FindProperty("crosshair").objectReferenceValue = crosshair;
            hudSo.FindProperty("promptGroup").objectReferenceValue = promptGroup;
            hudSo.FindProperty("promptText").objectReferenceValue = promptText;
            hudSo.FindProperty("objectiveText").objectReferenceValue = objectiveText;
            hudSo.FindProperty("bannerGroup").objectReferenceValue = banner;
            hudSo.FindProperty("bannerText").objectReferenceValue = bannerText;
            hudSo.FindProperty("clockText").objectReferenceValue = clockText;
            hudSo.FindProperty("vitalsGroup").objectReferenceValue = vitalsGo.GetComponent<CanvasGroup>();
            hudSo.FindProperty("vitalsFill").objectReferenceValue = vFill;
            SetObjectArray(hudSo.FindProperty("slotViews"), slotViews);
            hudSo.FindProperty("messageGroup").objectReferenceValue = msgGroup;
            hudSo.FindProperty("messageText").objectReferenceValue = msgText;
            hudSo.FindProperty("holdGroup").objectReferenceValue = holdGo.GetComponent<CanvasGroup>();
            hudSo.FindProperty("holdFill").objectReferenceValue = holdFill;
            hudSo.FindProperty("scentGroup").objectReferenceValue = scentGo.GetComponent<CanvasGroup>();
            hudSo.FindProperty("scentText").objectReferenceValue = scentText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // ── 3) 스마트폰 ──
            BuildPhone(ct, g, font, buttonPrefab, input, sys.clock, flashlight);

            // ── 3.5) 가이드(힌트/조작 카드/목표 상세/목표 화살표) + 도어 스코프 ──
            ControlsGuideCard controlsCard = BuildGuideAndPeephole(gameplay, ct, g, font, player, sys);

            // ── 4) 일시정지 메뉴 + 설정 팝업 ──
            PopupParts pause = BuildPopupFrame("PauseMenu", ct, g, font, "PAUSED", "일시정지", new Vector2(640, 600), false, 0.7f);
            string[] pauseLabels = { "계속하기", "조작법", "설정", "메인 메뉴로" };
            var pauseButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject b = InstantiateButtonRaw(buttonPrefab, pause.window, $"Btn_Pause_{i + 1}", pauseLabels[i], new Vector2(480, 80), 32);
                Anchor((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170 - i * 96));
                pauseButtons[i] = b.GetComponent<Button>();
            }
            SettingsPanel settings = BuildSettingsPopup(ct, g, font, buttonPrefab);

            var pauseMenu = pause.root.gameObject.AddComponent<PauseMenu>();
            var pso = SO(pauseMenu);
            pso.FindProperty("input").objectReferenceValue = input;
            pso.FindProperty("panel").objectReferenceValue = pause.root;
            pso.FindProperty("resumeButton").objectReferenceValue = pauseButtons[0];
            pso.FindProperty("controlsButton").objectReferenceValue = pauseButtons[1];
            pso.FindProperty("controlsCard").objectReferenceValue = controlsCard;
            pso.FindProperty("settingsButton").objectReferenceValue = pauseButtons[2];
            pso.FindProperty("mainMenuButton").objectReferenceValue = pauseButtons[3];
            pso.FindProperty("settingsPopup").objectReferenceValue = settings;
            pso.ApplyModifiedPropertiesWithoutUndo();

            // ── 5) 사망 / 엔딩 화면 (최상단) ──
            // 폭발 섬광 (사망/엔딩 화면보다 아래)
            Image flashImg = CreateImage("ScreenFlash", ct, g.white, Image.Type.Simple, false);
            Stretch(flashImg.rectTransform, 0);
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            var flash = flashImg.gameObject.AddComponent<ScreenFlash>();
            var flashSo = SO(flash);
            flashSo.FindProperty("image").objectReferenceValue = flashImg;
            flashSo.ApplyModifiedPropertiesWithoutUndo();

            DeathScreen deathScreen = BuildDeathScreen(ct, g, font, buttonPrefab);
            EndingScreen endingScreen = BuildEndingScreen(ct, g, font, buttonPrefab);
            AddEndingScreenExtras(deathScreen, endingScreen, font);
            BuildChoicePanel(ct, g, font, buttonPrefab);   // NPC 대화 / 119 위치 선택 (사망·엔딩 화면보다 아래)

            var deSo = SO(sys.deathAndEnding);
            SetObjectArray(deSo.FindProperty("deathReasons"), data.deaths);
            deSo.FindProperty("fallbackReason").objectReferenceValue = data.fallback;
            SetObjectArray(deSo.FindProperty("endings"), data.endings);
            deSo.FindProperty("deathScreen").objectReferenceValue = deathScreen;
            deSo.FindProperty("endingScreen").objectReferenceValue = endingScreen;
            deSo.FindProperty("scoreSystem").objectReferenceValue = sys.score;
            deSo.ApplyModifiedPropertiesWithoutUndo();

            AssignAudioToButtons(ct, audio);
        }

        private static InventorySlotView BuildInventorySlot(Transform parent, int index, float x, float size, GenAssets g, TMP_FontAsset font)
        {
            Image bg = CreateImage($"Slot_{index + 1}", parent, g.panel, Image.Type.Sliced, false);
            Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(size, size));
            bg.color = new Color(0, 0, 0, 0.35f);

            Image icon = CreateImage("Icon", bg.transform, null, Image.Type.Simple, false);
            Stretch(icon.rectTransform, 14);
            icon.preserveAspect = true;
            icon.enabled = false;

            TMP_Text initial = GameText("Initial", bg.transform, string.Empty, font, 34, TextAlignmentOptions.Center, Color.white);
            initial.fontStyle = FontStyles.Bold;
            Stretch(initial.rectTransform, 0);

            TMP_Text number = GameText("Number", bg.transform, (index + 1).ToString(), font, 20, TextAlignmentOptions.TopLeft, TextDim);
            Place(number.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -4), new Vector2(30, 26));

            Image selected = CreateImage("Selected", bg.transform, g.border, Image.Type.Sliced, false);
            Stretch(selected.rectTransform, -3);
            selected.color = Accent;
            selected.gameObject.SetActive(false);

            Image equipped = CreateImage("Equipped", bg.transform, g.radial, Image.Type.Simple, false);
            Place(equipped.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(18, 18));
            equipped.color = new Color(0.4f, 0.9f, 0.5f);
            equipped.gameObject.SetActive(false);

            var view = bg.gameObject.AddComponent<InventorySlotView>();
            var so = SO(view);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("numberLabel").objectReferenceValue = number;
            so.FindProperty("initialLabel").objectReferenceValue = initial;
            so.FindProperty("selectedMark").objectReferenceValue = selected.gameObject;
            so.FindProperty("equippedMark").objectReferenceValue = equipped.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void BuildPhone(Transform ct, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab, GameInput input, GameClock clock, Light flashlight)
        {
            var phoneGo = new GameObject("PhonePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(PhoneUI));
            phoneGo.transform.SetParent(ct, false);
            var prt = (RectTransform)phoneGo.transform;
            Place(prt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(420, 780));

            Image bg = CreateImage("Body", prt, g.panel, Image.Type.Sliced, true);
            Stretch(bg.rectTransform, 0);
            bg.color = new Color(0.03f, 0.04f, 0.06f, 0.95f);
            Image border = CreateImage("Border", prt, g.border, Image.Type.Sliced, false);
            Stretch(border.rectTransform, 0);
            border.color = PanelBorder;

            TMP_Text clockText = GameText("Clock", prt, "00:00", font, 72, TextAlignmentOptions.Center, TextMain);
            Place(clockText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(380, 96));

            // 손전등
            GameObject flashBtn = InstantiateButtonRaw(buttonPrefab, prt, "Btn_Flashlight", "손전등  OFF", new Vector2(360, 76), 28);
            Anchor((RectTransform)flashBtn.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150));
            Image indicator = CreateImage("Indicator", flashBtn.transform, g.radial, Image.Type.Simple, false);
            Place(indicator.rectTransform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(26, 26));
            indicator.color = new Color(0.3f, 0.3f, 0.3f);
            TMP_Text flashLabel = flashBtn.transform.Find("Label").GetComponent<TMP_Text>();

            // 문자 메시지
            TMP_Text header = GameText("MessageHeader", prt, "문자 메시지", font, 24, TextAlignmentOptions.MidlineLeft, TextDim);
            Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(360, 36));

            var listGo = new GameObject("MessageList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(prt, false);
            Place((RectTransform)listGo.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -292), new Vector2(360, 300));
            var vlg = listGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;

            TMP_Text template = GameText("MessageRowTemplate", listGo.transform, "보낸 사람\n내용", font, 22, TextAlignmentOptions.TopLeft, TextMain, true);
            template.richText = true;
            template.gameObject.SetActive(false);

            TMP_Text empty = GameText("EmptyLabel", prt, "새 메시지가 없습니다", font, 24, TextAlignmentOptions.Center, TextDim);
            Place(empty.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(360, 40));

            // 119
            TMP_Text status = GameText("CallStatus", prt, string.Empty, font, 24, TextAlignmentOptions.Center, TextDim);
            Place(status.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 142), new Vector2(360, 36));
            GameObject callBtn = InstantiateButtonRaw(buttonPrefab, prt, "Btn_Call119", "119 신고", new Vector2(360, 80), 30);
            Anchor((RectTransform)callBtn.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44));
            ApplyStyle(callBtn, RedStyle());

            var phone = phoneGo.GetComponent<PhoneUI>();
            var so = SO(phone);
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("panel").objectReferenceValue = phoneGo.GetComponent<CanvasGroup>();
            so.FindProperty("panelRect").objectReferenceValue = prt;
            so.FindProperty("clockText").objectReferenceValue = clockText;
            so.FindProperty("flashlight").objectReferenceValue = flashlight;
            so.FindProperty("flashlightButton").objectReferenceValue = flashBtn.GetComponent<Button>();
            so.FindProperty("flashlightLabel").objectReferenceValue = flashLabel;
            so.FindProperty("flashlightIndicator").objectReferenceValue = indicator;
            so.FindProperty("messageListRoot").objectReferenceValue = listGo.transform;
            so.FindProperty("messageRowTemplate").objectReferenceValue = template;
            so.FindProperty("emptyLabel").objectReferenceValue = empty.gameObject;
            so.FindProperty("call119Button").objectReferenceValue = callBtn.GetComponent<Button>();
            so.FindProperty("callStatusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DeathScreen BuildDeathScreen(Transform ct, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            PopupParts p = BuildPopupFrame("DeathScreen", ct, g, font, "사망", "교육 피드백", new Vector2(1400, 860), false, 0.9f);
            TMP_Text title = p.window.Find("Title").GetComponent<TMP_Text>();
            title.color = new Color(1f, 0.42f, 0.42f);

            TMP_Text h1 = GameText("Header_Why", p.window, "왜 사망했나", font, 30, TextAlignmentOptions.MidlineLeft, Accent);
            Place(h1.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -170), new Vector2(600, 44));
            TMP_Text why = GameText("WhyText", p.window, string.Empty, font, 34, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(why.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -220), new Vector2(1288, 230));

            TMP_Text h2 = GameText("Header_Correct", p.window, "올바른 행동", font, 30, TextAlignmentOptions.MidlineLeft, new Color(0.4f, 0.9f, 0.5f));
            Place(h2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -470), new Vector2(600, 44));
            TMP_Text act = GameText("CorrectActionText", p.window, string.Empty, font, 34, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(act.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -520), new Vector2(1288, 230));

            GameObject restart = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_Restart", "다시 시작", new Vector2(320, 76), 30);
            GameObject menu = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_MainMenu", "메인 메뉴", new Vector2(320, 76), 30);
            Anchor((RectTransform)restart.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56 - 336, 44));
            Anchor((RectTransform)menu.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 44));

            var screen = p.root.gameObject.AddComponent<DeathScreen>();
            var so = SO(screen);
            so.FindProperty("group").objectReferenceValue = p.root;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("whyText").objectReferenceValue = why;
            so.FindProperty("correctActionText").objectReferenceValue = act;
            so.FindProperty("restartButton").objectReferenceValue = restart.GetComponent<Button>();
            so.FindProperty("mainMenuButton").objectReferenceValue = menu.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        private static EndingScreen BuildEndingScreen(Transform ct, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            PopupParts p = BuildPopupFrame("EndingScreen", ct, g, font, "ENDING", "엔딩", new Vector2(1400, 860), false, 0.9f);
            TMP_Text name = p.window.Find("Title").GetComponent<TMP_Text>();

            TMP_Text desc = GameText("Description", p.window, string.Empty, font, 26, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(desc.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -150), new Vector2(1288, 120));

            TMP_Text h = GameText("Header_Score", p.window, "구조 점수", font, 28, TextAlignmentOptions.MidlineLeft, Accent);
            Place(h.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -280), new Vector2(600, 40));
            TMP_Text score = GameText("ScoreText", p.window, string.Empty, font, 22, TextAlignmentOptions.TopLeft, TextMain, true);
            score.enableAutoSizing = true; score.fontSizeMin = 14; score.fontSizeMax = 22;
            Place(score.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -326), new Vector2(820, 420));

            TMP_Text time = GameText("TimeText", p.window, string.Empty, font, 30, TextAlignmentOptions.TopRight, TextMain);
            Place(time.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -326), new Vector2(460, 50));

            GameObject restart = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_Restart", "다시 시작", new Vector2(320, 76), 30);
            GameObject menu = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_MainMenu", "메인 메뉴", new Vector2(320, 76), 30);
            Anchor((RectTransform)restart.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56 - 336, 44));
            Anchor((RectTransform)menu.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 44));

            var screen = p.root.gameObject.AddComponent<EndingScreen>();
            var so = SO(screen);
            so.FindProperty("group").objectReferenceValue = p.root;
            so.FindProperty("endingNameText").objectReferenceValue = name;
            so.FindProperty("descriptionText").objectReferenceValue = desc;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("timeText").objectReferenceValue = time;
            so.FindProperty("restartButton").objectReferenceValue = restart.GetComponent<Button>();
            so.FindProperty("mainMenuButton").objectReferenceValue = menu.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }
    }
}
