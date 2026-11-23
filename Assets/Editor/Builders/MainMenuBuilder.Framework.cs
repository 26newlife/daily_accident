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
    /// 시나리오 프레임워크 빌드: 집 에셋(Apartment_1004_Base 프리팹) + HomeBase(GameScene) + 시나리오 3 Additive 씬 분리.
    /// Build Apartment Scene 은 지금까지와 같은 코드로 전체를 한 번에 만든 뒤, 마지막에 시나리오 전용 오브젝트를
    /// S03_ApartmentFire.unity 로 옮기고(ScenarioSceneSplitter) 집을 프리팹으로 묶는다. 집의 외형·치수·배치·id 는 그대로다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        public const string S03Dir = "Assets/Scenarios/S03_ApartmentFire";
        public const string S03ScenePath = S03Dir + "/Scenes/S03_ApartmentFire.unity";
        public const string HousePrefabPath = "Assets/House/Prefabs/Apartment_1004_Base.prefab";
        private const string PresetDir = "Assets/House/Data/EnvironmentPresets";

        public static void EnsureFolderPublic(string path) => EnsureFolder(path);

        // 시나리오 3 전용 오브젝트가 만들어지는 그룹 (분리 후 S03 씬의 내용물)
        private static Transform s3Root, s3Structure, s3Furniture, s3Interact, s3Zones, scenarioSystemsRoot;
        private static ActBase[] s3Acts;
        // 빌드 중 임시로 가리켜 두는 집 그룹 (시나리오 월드를 만들 때 lv* 를 시나리오 그룹으로 바꿔 끼운다)
        private static Transform hStructure, hFurniture, hInteract, hZones;

        private static void BeginScenarioContent()
        {
            s3Root = new GameObject("S03_Content").transform;
            s3Structure = Group("Structure", s3Root);
            s3Furniture = Group("Furniture", s3Root);
            s3Interact = Group("Interactables", s3Root);
            s3Zones = Group("HazardZones", s3Root);
            scenarioSystemsRoot = Group("ScenarioSystems", s3Root);
            s3Acts = null;
        }

        private static void SwapToScenarioGroups()
        {
            hStructure = lvStructure; hFurniture = lvFurniture; hInteract = lvInteract; hZones = lvZones;
            lvStructure = s3Structure; lvFurniture = s3Furniture; lvInteract = s3Interact; lvZones = s3Zones;
        }

        private static void RestoreHouseGroups()
        {
            lvStructure = hStructure; lvFurniture = hFurniture; lvInteract = hInteract; lvZones = hZones;
        }

        // ═════════════════════ 집 수준(중립) 변환 ═════════════════════
        /// <summary>
        /// 베란다 화분·짐 상자는 "치울 수 있는" 중립 오브젝트(기본 동작)다. 시나리오 없이도 치워진다 (id 변경 없음).
        /// (화분 flower_pot 은 기존 TriggerInteractable → RemovableInteractable 로 교체, Planter/CrateStack 은 컴포넌트 추가)
        /// </summary>
        private static void BuildHouseRemovables(Transform level)
        {
            Transform pot = lvInteract.Find(Ids.FlowerPot);
            if (pot != null)
            {
                var trig = pot.GetComponent<TriggerInteractable>();
                if (trig != null) Object.DestroyImmediate(trig);
                MakeRemovable(pot.gameObject, Ids.FlowerPot, "화분 치우기", "화분을 한쪽으로 치웠다.", 1.0f);
            }
            Transform interior = level.Find("Interior/Balcony");
            Transform planter = interior != null ? interior.Find("Planter") : null;
            Transform crate = interior != null ? interior.Find("CrateStack") : null;
            if (planter != null) MakeRemovable(planter.gameObject, EvacKeys.FlowerPot2, "화분 치우기", "화분을 치웠다.", 1.0f);
            if (crate != null) MakeRemovable(crate.gameObject, EvacKeys.StorageBox, "짐 상자 치우기", "짐 상자를 한쪽으로 밀어 두었다.", 1.4f);
        }

        // ═════════════════════ 집 프레임워크 (앵커 · 스폰 · HouseController) ═════════════════════
        private static Transform BuildHouseRoot(Transform gameplay, Transform level)
        {
            var houseRoot = new GameObject("Apartment_1004_Base").transform;
            houseRoot.SetParent(gameplay, false);
            level.SetParent(houseRoot, true);
            foreach (Transform t in gameplay)
                if (t.name == "OutdoorWorld") { t.SetParent(houseRoot, true); break; }

            // 스폰 포인트 (집 안 통로 위 위치. 침실 시작 위치는 기존 PlayerStart 와 같다)
            Transform spawns = Group("HouseSpawns", houseRoot);
            AddSpawn(spawns, "spawn_bedroom_bed", PlayerStart, 0f);
            AddSpawn(spawns, "spawn_living_room", new Vector3(3.2f, 0.02f, 3.4f), 0f);
            AddSpawn(spawns, "spawn_entrance", new Vector3(3.0f, 0.02f, 1.2f), 0f);
            AddSpawn(spawns, "spawn_corridor", new Vector3(0.5f, 0.02f, -1.1f), 270f);
            AddSpawn(spawns, "spawn_balcony", new Vector3(2.7f, 0.02f, 8.2f), 0f);
            AddSpawn(spawns, "spawn_shelter", new Vector3(6.0f, 0.02f, 8.2f), 270f);

            // 앵커 (이펙트·NPC·소품 배치용 이름 붙은 위치)
            Transform anchors = Group("HouseAnchors", houseRoot);
            AddAnchor(anchors, "anchor_living_center", new Vector3(4.0f, 1.0f, 5.0f), "거실 중앙");
            AddAnchor(anchors, "anchor_kitchen_stove", new Vector3(6.3f, 1.0f, 3.0f), "주방 가스레인지");
            AddAnchor(anchors, "anchor_balcony_window", new Vector3(2.7f, 1.5f, 8.9f), "베란다 창");
            AddAnchor(anchors, "anchor_living_window", new Vector3(3.2f, 1.4f, 7.3f), "거실 창");
            AddAnchor(anchors, "anchor_bedroom_window", new Vector3(1.3f, 1.4f, 7.3f), "안방 창");
            AddAnchor(anchors, "anchor_entrance_door", new Vector3(3.0f, 1.0f, 0.15f), "현관문 안쪽");
            AddAnchor(anchors, "anchor_corridor_elevator", new Vector3(-1.5f, 1.0f, -2.0f), "복도 엘리베이터 문 앞");
            AddAnchor(anchors, "anchor_stairwell_door", new Vector3(-14.0f, 1.0f, -1.1f), "비상계단 방화문");
            AddAnchor(anchors, "anchor_shelter_door", new Vector3(5.4f, 1.0f, 8.2f), "대피공간 방화문");
            AddAnchor(anchors, "anchor_utility_valve", new Vector3(6.4f, 1.2f, 1.2f), "다용도실 가스 밸브");

            // HouseController (집 공개 API)
            var hcGo = new GameObject("HouseController");
            hcGo.transform.SetParent(houseRoot, false);
            var hc = hcGo.AddComponent<HouseController>();
            var amb = new GameObject("HouseAmbient", typeof(AudioSource));
            amb.transform.SetParent(hcGo.transform, false);
            var ambSrc = amb.GetComponent<AudioSource>(); ambSrc.playOnAwake = false; ambSrc.spatialBlend = 0f;
            var so = SO(hc);
            so.FindProperty("ambientSource").objectReferenceValue = ambSrc;
            Transform interior = level.Find("Interior");
            if (interior != null)
            {
                var rooms = new List<Transform>();
                foreach (Transform r in interior) rooms.Add(r);
                SerializedProperty arr = so.FindProperty("shakeRoots");
                arr.arraySize = rooms.Count;
                for (int i = 0; i < rooms.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return houseRoot;
        }

        private static void AddSpawn(Transform parent, string id, Vector3 pos, float yaw)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var sp = go.AddComponent<SpawnPoint>();
            var so = SO(sp); so.FindProperty("id").stringValue = id; so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddAnchor(Transform parent, string id, Vector3 pos, string desc)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var a = go.AddComponent<ScenarioAnchor>();
            var so = SO(a); so.FindProperty("id").stringValue = id; so.FindProperty("description").stringValue = desc; so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═════════════════════ 환경 프리셋 / 시나리오 정의 ═════════════════════
        private static EnvironmentPreset EnsurePreset(string file, System.Action<EnvironmentPreset> init)
        {
            EnsureFolder(PresetDir);
            string path = $"{PresetDir}/{file}.asset";
            var p = AssetDatabase.LoadAssetAtPath<EnvironmentPreset>(path);
            if (p != null) return p;
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            p = ScriptableObject.CreateInstance<EnvironmentPreset>();
            init(p);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        /// <summary>기본 제공 환경 프리셋 4종 (평상시 아침 = 기존 장면 그대로)</summary>
        public static EnvironmentPreset[] EnsureEnvironmentPresets()
        {
            return new[]
            {
                EnsurePreset("Env_NormalMorning", p => { p.displayName = "평상시 아침"; p.timeOfDay = TimeOfDayKind.Morning; }),
                EnsurePreset("Env_NormalNight", p => { p.displayName = "평상시 밤"; p.timeOfDay = TimeOfDayKind.Night; p.applySun = true; p.sunEuler = new Vector3(-20f, -30f, 0f); p.sunIntensity = 0.05f; p.sunColor = new Color(0.4f, 0.5f, 0.8f); p.applyAmbient = true; p.ambientColor = new Color(0.05f, 0.06f, 0.1f); p.applyTraffic = true; p.trafficDensity = 0.3f; }),
                EnsurePreset("Env_RainyDay", p => { p.displayName = "비 오는 낮"; p.timeOfDay = TimeOfDayKind.Day; p.windowView = WindowViewMode.RainStorm; p.applySun = true; p.sunEuler = new Vector3(60f, -30f, 0f); p.sunIntensity = 0.45f; p.sunColor = new Color(0.7f, 0.75f, 0.8f); p.applyAmbient = true; p.ambientColor = new Color(0.22f, 0.24f, 0.28f); p.applyTraffic = true; p.pedestrians = false; p.trafficDensity = 0.4f; }),
                EnsurePreset("Env_SmokySky", p => { p.displayName = "연기 낀 하늘"; p.timeOfDay = TimeOfDayKind.Day; p.windowView = WindowViewMode.DarkSmoky; p.applySun = true; p.sunEuler = new Vector3(40f, -30f, 0f); p.sunIntensity = 0.3f; p.sunColor = new Color(0.8f, 0.55f, 0.35f); p.applyAmbient = true; p.ambientColor = new Color(0.18f, 0.14f, 0.12f); p.applyTraffic = true; p.pedestrians = false; p.trafficDensity = 0.2f; }),
            };
        }

        private static ScenarioDefinition EnsureS03Definition(GameplayAssets d, HintData[] scenarioHints, EnvironmentPreset preset)
        {
            EnsureFolder(S03Dir + "/Data");
            string path = S03Dir + "/Data/S03_Definition.asset";
            var def = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (def == null)
            {
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                def = ScriptableObject.CreateInstance<ScenarioDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            var so = SO(def);
            so.FindProperty("id").stringValue = "S03";
            so.FindProperty("displayName").stringValue = "아파트 화재";
            so.FindProperty("description").stringValue = "고층 아파트 화재에서 탈출하라";
            so.FindProperty("isPlayable").boolValue = true;
            so.FindProperty("targetPlayMinutes").floatValue = 7f;
            so.FindProperty("scenarioSceneName").stringValue = "S03_ApartmentFire";
            so.FindProperty("startSpawnId").stringValue = "spawn_bedroom_bed";
            so.FindProperty("environmentPreset").objectReferenceValue = preset;
            SetStrings(so.FindProperty("actTypeNames"), new[] { "Act1_MorningSigns", "Act2_BlastAndSmoke", "Act3_FireSpreads", "Act4_Evacuation" });
            SetObjectArray(so.FindProperty("requiredItems"), new[] { d.towel, d.towelDry, d.extinguisher, d.tape, d.water, d.hammer, d.roofKey });
            SetObjectArray(so.FindProperty("deathReasons"), d.deaths);
            so.FindProperty("fallbackDeath").objectReferenceValue = d.fallback;
            SetObjectArray(so.FindProperty("endings"), d.endings);
            so.FindProperty("hiddenEndingId").stringValue = EvacKeys.EndingHidden;
            so.FindProperty("hiddenEndingFlag").stringValue = EvacKeys.FlagGrandmotherWith;
            so.FindProperty("scoreRules").objectReferenceValue = d.scoreRules;
            SetObjectArray(so.FindProperty("hints"), scenarioHints);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void LinkScenarioData(string scenarioDataPath, ScenarioDefinition def)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScenarioData>(scenarioDataPath);
            if (data == null) { Debug.LogWarning($"[Framework] ScenarioData 를 찾지 못했습니다: {scenarioDataPath}"); return; }
            var so = SO(data);
            so.FindProperty("definition").objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        private static void RegisterScene(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ═════════════════════ 마무리: 집 프리팹 + 시나리오 씬 분리 ═════════════════════
        private static void FinalizeFramework(Scene baseScene, Transform gameplay, Transform level, GameplayAssets d, ScenarioDefinition[] unused = null)
        {
            // 1) 집 루트(HouseController·앵커·스폰 포함)로 묶기
            Transform houseRoot = BuildHouseRoot(gameplay, level);

            // 2) 시나리오 정의 + 환경 프리셋
            EnvironmentPreset[] presets = EnsureEnvironmentPresets();
            ScenarioDefinition def = EnsureS03Definition(d, s3Hints, presets[0]);
            LinkScenarioData(ApartmentScenarioPath, def);

            // 3) ScenarioRunner (HomeBase)
            GameObject systems = FindSystemsRoot();
            var runner = systems.AddComponent<ScenarioRunner>();
            var rso = SO(runner);
            rso.FindProperty("defaultDefinition").objectReferenceValue = def;
            rso.FindProperty("machine").objectReferenceValue = systems.GetComponent<ScenarioStateMachine>();
            rso.FindProperty("deathAndEnding").objectReferenceValue = systems.GetComponent<DeathAndEnding>();
            rso.FindProperty("scoreSystem").objectReferenceValue = systems.GetComponent<ScoreSystem>();
            rso.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            rso.ApplyModifiedPropertiesWithoutUndo();

            // 4) 시나리오 3 씬 분리
            var sceneRootGo = new GameObject("ScenarioSceneRoot");
            var sr = sceneRootGo.AddComponent<ScenarioSceneRoot>();
            var sso = SO(sr);
            sso.FindProperty("contentRoot").objectReferenceValue = s3Root.gameObject;
            SetObjectArray(sso.FindProperty("acts"), s3Acts);
            SetObjectArray(sso.FindProperty("bootstraps"), new Component[] { scenarioSystemsRoot.GetComponent<S03Bootstrap>() });
            sso.ApplyModifiedPropertiesWithoutUndo();

            var extras = new List<GameObject>();
            foreach (FireSource f in Object.FindObjectsByType<FireSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)) extras.Add(f.gameObject);
            foreach (string n in new[] { "MultitapSparks", "GlassShards", "AlarmAudio", "SparkAudio", "LeakAudio", "ElevatorHallFx" })
                foreach (Transform t in level.GetComponentsInChildren<Transform>(true)) if (t.name == n) extras.Add(t.gameObject);

            EditorSceneManager.SaveScene(baseScene, GameScenePath);   // 저장되지 않은 씬에는 Additive 씬을 만들 수 없다
            ScenarioSceneSplitter.Result res = ScenarioSceneSplitter.Split(baseScene, s3Root.gameObject, sceneRootGo, extras, S03ScenePath);
            Debug.Log($"[Framework] S03 씬 분리: 오브젝트 {res.objectsMoved}개, 집 참조 바인딩 {res.bindings.Count}건");
            if (res.reverseRefs.Count > 0)
            {
                Debug.LogError($"[Framework] 집/베이스가 시나리오 오브젝트를 참조하고 있습니다 ({res.reverseRefs.Count}건). 집은 시나리오를 몰라야 합니다:\n  " + string.Join("\n  ", res.reverseRefs));
            }
            RegisterScene(S03ScenePath);

            // 4.5) 확정 보정 (화재 소품 태그 + B-02 통로 보정) — 빌더를 다시 돌려도 유지되도록 프리팹 저장 직전에 적용한다
            GameUI.EditorTools.Interior.BaseHouseFixups.Apply(houseRoot);

            // 5) 집 프리팹 (시나리오 오브젝트가 빠진 뒤 저장 — 씬의 외부 참조는 유지된다)
            EnsureFolder("Assets/House/Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(houseRoot.gameObject, HousePrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[Framework] 집 프리팹 저장: {HousePrefabPath} ({(prefab != null ? "OK" : "FAIL")})");
        }

        private static GameObject FindSystemsRoot()
        {
            var clock = Object.FindFirstObjectByType<GameClock>();
            return clock != null ? clock.gameObject : null;
        }

        private static HintData[] s3Hints = new HintData[0];
    }
}
