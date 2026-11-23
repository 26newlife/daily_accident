using System.IO;
using GameData;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 새 시나리오의 "빈 뼈대"를 만든다 (공통 베이스 위에 얹는 Additive 시나리오 폴더 구조).
    ///   Assets/Scenarios/S0X_Name/{Data, Scenes, Scripts, Prefabs}
    ///   Data/S0X_Definition.asset  — ScenarioDefinition (isPlayable = false: 내용이 채워질 때까지 메인 메뉴에서 시작되지 않는다)
    ///   Scenes/S0X_Name.unity      — ScenarioSceneRoot + 비활성 내용물 루트(S0X_Content: ScenarioSystems / Acts / Props / Anchors / Npcs / Effects)
    ///   Scripts/                   — Act·시스템 스크립트를 추가할 자리 (이때 Game.Scenarios.S01.asmdef 를 복사해 asmdef 를 만든다)
    /// 집(Apartment_1004_Base)은 건드리지 않는다. 시나리오 전용 오브젝트는 이 씬에만 두고, 상태 변화는 HouseController API 로 한다.
    /// 내용(Act·소품·NPC·이펙트)은 담당자가 채운다. 씬을 Build Settings 에 넣는 것도 isPlayable 을 켤 때 한다.
    /// </summary>
    public static class ScenarioSkeletonBuilder
    {
        [MenuItem("Tools/GameUI/Scenarios/Create Skeleton - S02 Disaster (자연재해)", priority = 310)]
        public static void CreateS02Menu()
        {
            if (!EditorUtility.DisplayDialog("S02 자연재해 뼈대", "Assets/Scenarios/S02_Disaster 에 빈 시나리오 뼈대(정의·씬)를 만듭니다.\n이미 있으면 덮어쓰지 않고 건너뜁니다. 집은 건드리지 않습니다.", "만들기", "취소")) return;
            CreateS02();
        }

        public static void CreateS02() => Create("S02", "S02_Disaster", "자연재해", "지진·침수 등 자연재해에서 살아남아라", "Assets/Core/Data/Scenarios/Scenario_02.asset");

        public static void Create(string id, string folderName, string displayName, string description, string scenarioDataPath)
        {
            string dir = "Assets/Scenarios/" + folderName;
            foreach (string sub in new[] { "", "/Data", "/Scenes", "/Scripts", "/Prefabs" }) EnsureFolder(dir + sub);

            // 1) asmdef 는 만들지 않는다 — 스크립트가 없는 asmdef 는 콘솔 경고를 낸다.
            //    Act·시스템 스크립트를 추가할 때 Game.Scenarios.S01.asmdef 를 복사해 Game.Scenarios.<id>.asmdef 로 만든다 (Game.Core, Game.House 참조).

            // 2) ScenarioDefinition (플레이 불가 상태로)
            string defPath = $"{dir}/Data/{id}_Definition.asset";
            var def = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ScenarioDefinition>();
                AssetDatabase.CreateAsset(def, defPath);
                var so = new SerializedObject(def);
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("description").stringValue = description;
                so.FindProperty("isPlayable").boolValue = false;
                so.FindProperty("targetPlayMinutes").floatValue = 7f;
                so.FindProperty("scenarioSceneName").stringValue = folderName;
                so.FindProperty("startSpawnId").stringValue = "spawn_bedroom_bed";
                so.FindProperty("environmentPreset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnvironmentPreset>("Assets/House/Data/EnvironmentPresets/Env_NormalMorning.asset");
                // 기능 플래그: 담당자가 시나리오에 맞게 조정한다 (화재·가스·엘리베이터 위험은 기본으로 끈다)
                so.FindProperty("useRespiratoryGauge").boolValue = false;
                so.FindProperty("useGasHazard").boolValue = false;
                so.FindProperty("useFireProps").boolValue = false;
                so.FindProperty("useElevatorHazard").boolValue = false;
                so.FindProperty("useDoorPeephole").boolValue = true;
                so.FindProperty("useOutdoorTraffic").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(def);
            }

            // 3) 씬 뼈대 (ScenarioSceneRoot + 비활성 내용물 루트)
            string scenePath = $"{dir}/Scenes/{folderName}.unity";
            if (!File.Exists(scenePath))
            {
                Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var content = new GameObject(id + "_Content");
                foreach (string g in new[] { "ScenarioSystems", "Acts", "Props", "Anchors", "Npcs", "Effects" })
                    new GameObject(g).transform.SetParent(content.transform, false);
                var rootGo = new GameObject("ScenarioSceneRoot");
                var root = rootGo.AddComponent<ScenarioSceneRoot>();
                var rso = new SerializedObject(root);
                rso.FindProperty("contentRoot").objectReferenceValue = content;
                rso.ApplyModifiedPropertiesWithoutUndo();
                content.SetActive(false);   // 베이스 참조 바인딩을 복원한 뒤 ScenarioRunner 가 켠다
                SceneManager.MoveGameObjectToScene(rootGo, s);
                SceneManager.MoveGameObjectToScene(content, s);
                EditorSceneManager.SaveScene(s, scenePath);
                EditorSceneManager.CloseScene(s, true);
            }

            // 4) 메인 메뉴 데이터(ScenarioData)와 연결
            var data = AssetDatabase.LoadAssetAtPath<ScenarioData>(scenarioDataPath);
            if (data != null)
            {
                var dso = new SerializedObject(data);
                SerializedProperty p = dso.FindProperty("definition");
                if (p != null && p.objectReferenceValue == null) { p.objectReferenceValue = def; dso.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Skeleton] {folderName} 뼈대 생성: {dir} (isPlayable=false). 내용을 채운 뒤 isPlayable 을 켜고 씬을 Build Settings 에 등록하세요.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
