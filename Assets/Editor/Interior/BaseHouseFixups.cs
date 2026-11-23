using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 공통 베이스 집(Apartment_1004_Base)의 "확정 보정" — 빌더가 다시 돌아도 유지되도록 코드로 보관한다.
    ///   1) 화재용 소품에 FireHazardProp 태그를 붙인다 (useFireProps=false 시나리오에서 숨기기 위한 표식. id·오브젝트는 그대로)
    ///   2) B-02: 통로·접근성 보정 (모두 "절대 좌표" 지정이라 여러 번 실행해도 결과가 같다)
    ///        주방 : 전자레인지(+부속 3) z -0.10, coffee_pot·pickup_water_bottle 을 싱크대 위로, 조리대 깊이 0.52m
    ///        대피공간 문 : 벽 개구부 0.90 → 0.95m (Wall_Shelter_0 / _2 를 각 0.025m 깎음)
    ///   3) 시나리오 1 고정 배치 보정 (S01 씬에 s01_* 앵커가 없으면 만들고, 건물 밖이던 WaterAndCans 를 주방 안으로)
    /// 호출 위치: MainMenuBuilder.FinalizeFramework (집 프리팹 저장 직전) / 메뉴 Tools > GameUI > Base House > Apply Fixups.
    /// 값을 바꾸면 Docs/BaseHouseSpec.md 도 갱신하고 Validate Apartment Clearance 로 검증할 것.
    /// </summary>
    public static class BaseHouseFixups
    {
        private const string PrefabPath = "Assets/House/Prefabs/Apartment_1004_Base.prefab";
        private const string S01ScenePath = "Assets/Scenarios/S01_WarTime/Scenes/S01_WarTime.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private static readonly string[] IdTokens =
        {
            "gas_", "fuse", "multitap", "extinguisher", "towel", "frying", "handle_check", "duct_tape", "water_pot", "range_hood",
            "fire_curtain", "fire_sofa", "fire_pan", "fire_multitap", "fire_corridor", "clutter_", "smoke",
        };
        private static readonly string[] NameTokens = { "Extinguisher", "Hydrant", "FireAlarm" };

        // 대상(houseRoot 기준 경로) → 월드 위치. 한 번에 한 축만 바뀌는 보정이라 y 는 원래 값을 쓴다
        private static readonly (string path, Vector3 pos)[] Positions =
        {
            ("Level/Interactables/microwave", new Vector3(6.30f, 1.04f, 3.45f)),
            ("Level/Interior/Kitchen/MicrowaveGlass", new Vector3(6.10f, 1.05f, 3.40f)),
            ("Level/Interior/Kitchen/MicrowavePanel", new Vector3(6.10f, 1.04f, 3.56f)),
            ("Level/Interior/Kitchen/MicrowaveHandle", new Vector3(6.09f, 1.04f, 3.52f)),
            ("Level/Interactables/coffee_pot", new Vector3(4.52f, 1.02f, 4.34f)),
            ("Level/Interactables/pickup_water_bottle", new Vector3(5.02f, 1.01f, 4.34f)),
            ("Level/Interior/Kitchen/KitchenCounter", new Vector3(6.21f, 0f, 3.55f)),
            ("Level/Structure/Wall_Shelter/Wall_Shelter_0", new Vector3(5.40f, 1.20f, 7.5275f)),
            ("Level/Structure/Wall_Shelter/Wall_Shelter_2", new Vector3(5.40f, 1.20f, 8.8725f)),
        };

        private static readonly (string path, Vector3 scale)[] Scales =
        {
            ("Level/Interior/Kitchen/KitchenCounter", new Vector3(0.94f, 1f, 0.85f)),   // 깊이(로컬 z) 0.61 → 0.52m
            ("Level/Structure/Wall_Shelter/Wall_Shelter_0", new Vector3(0.15f, 2.4f, 0.395f)),
            ("Level/Structure/Wall_Shelter/Wall_Shelter_2", new Vector3(0.15f, 2.4f, 0.395f)),
        };

        // ═════════════════════════ 집 계층에 적용 ═════════════════════════
        /// <summary>houseRoot = Apartment_1004_Base (씬의 빌드 중 루트, 프리팹 편집 루트, 또는 프리팹 인스턴스)</summary>
        public static void Apply(Transform houseRoot)
        {
            int tagged = TagFireProps(houseRoot);
            int moved = ApplyGeometry(houseRoot);
            int removables = EnsureBalconyRemovables(houseRoot);
            int locks = RemoveWindowLocks(houseRoot);
            GameUI.EditorTools.Art.InteriorLookBuilder.EnsureVolumes(houseRoot);   // 후처리 Volume (HouseVolumes)
            int heroes = GameUI.EditorTools.Art.HeroObjects.Apply(houseRoot);      // ③ Hero 모델 (전자레인지·멀티탭). 위치 보정 뒤에 얹는다
            Debug.Log($"[BaseFixups] 화재 소품 태그 {tagged}개, 위치·크기 보정 {moved}건, 후처리 Volume 구성, 치우기 소품 {removables}개 확인, 창문 잠금 {locks}개 제거, Hero 모델 {heroes}개");
        }

        /// <summary>창문 잠금 기능 삭제: window_*_lock 오브젝트를 지우고 창문이 처음부터 열리게 한다 (이미 없으면 아무것도 안 함)</summary>
        public static int RemoveWindowLocks(Transform root)
        {
            int removed = 0;
            var targets = new List<GameObject>();
            foreach (LockInteractable l in root.GetComponentsInChildren<LockInteractable>(true))
                if (l.Id.StartsWith("window_")) targets.Add(l.gameObject);
            foreach (GameObject g in targets) { Object.DestroyImmediate(g); removed++; }

            foreach (ToggleInteractable t in root.GetComponentsInChildren<ToggleInteractable>(true))
            {
                if (!t.Id.StartsWith("window_")) continue;
                var so = new SerializedObject(t);
                SerializedProperty p = so.FindProperty("startLocked");
                if (p == null || !p.boolValue) continue;
                p.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                t.SetLocked(false);
            }
            return removed;
        }

        public static int TagFireProps(Transform root)
        {
            var seen = new HashSet<GameObject>();
            foreach (InteractableObject o in root.GetComponentsInChildren<InteractableObject>(true))
                if (IsFireId(o.Id)) seen.Add(o.gameObject);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                foreach (string n in NameTokens) if (t.name.Contains(n)) { seen.Add(t.gameObject); break; }
            foreach (GameObject g in seen) if (g.GetComponent<FireHazardProp>() == null) g.AddComponent<FireHazardProp>();
            return seen.Count;
        }

        /// <summary>
        /// 베란다 화분·짐 상자를 "치울 수 있는" 중립 오브젝트로 만든다 (id 는 S03 EvacKeys 와 같은 값 — 변경 금지).
        /// 인테리어를 다시 만들면 Planter/CrateStack 이 새로 생겨 이 컴포넌트가 사라지므로 여기서 다시 붙인다 (이미 있으면 값만 맞춘다).
        /// </summary>
        private static int EnsureBalconyRemovables(Transform root)
        {
            int n = 0;
            n += MakeRemovable(root, "Level/Interior/Balcony/Planter", "balcony_pot_2", "화분 치우기", "화분을 치웠다.", 1.0f);
            n += MakeRemovable(root, "Level/Interior/Balcony/CrateStack", "balcony_storage_box", "짐 상자 치우기", "짐 상자를 한쪽으로 밀어 두었다.", 1.4f);
            return n;
        }

        private static int MakeRemovable(Transform root, string path, string id, string prompt, string message, float hold)
        {
            Transform t = root.Find(path);
            if (t == null) { Debug.LogWarning("[BaseFixups] 치우기 소품 대상을 찾지 못했습니다: " + path); return 0; }
            var r = t.GetComponent<RemovableInteractable>() ?? t.gameObject.AddComponent<RemovableInteractable>();
            var so = new SerializedObject(r);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("message").stringValue = message;
            so.FindProperty("holdSeconds").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static bool IsFireId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (string t in IdTokens) if (id.Contains(t)) return true;
            return false;
        }

        private static int ApplyGeometry(Transform root)
        {
            int n = 0;
            foreach (var (path, pos) in Positions)
            {
                Transform t = root.Find(path);
                if (t == null) { Debug.LogWarning("[BaseFixups] 대상을 찾지 못했습니다: " + path); continue; }
                t.position = root.TransformPoint(pos); n++;
            }
            foreach (var (path, scale) in Scales)
            {
                Transform t = root.Find(path);
                if (t == null) { Debug.LogWarning("[BaseFixups] 대상을 찾지 못했습니다: " + path); continue; }
                t.localScale = scale; n++;
            }
            return n;
        }

        // ═════════════════════════ 기존 에셋에 적용 (메뉴 / 배치) ═════════════════════════
        [MenuItem("Tools/GameUI/Base House/Apply Fixups (Fire Tags + B-02 + S01 Layout)")]
        public static void ApplyToAssetsMenu()
        {
            if (!EditorUtility.DisplayDialog("베이스 보정 적용", "집 프리팹에 화재 소품 태그·B-02 보정을 적용하고, 시나리오 1 씬에 s01_* 앵커·WaterAndCans 위치를 맞춥니다.\n여러 번 실행해도 같은 결과입니다. 열려 있는 씬을 저장한 뒤 진행합니다.", "적용", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToAssets();
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Interior.BaseHouseFixups.ApplyToAssets</summary>
        public static void ApplyToAssets()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Apply(root.transform); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ApplyToGameScene();   // 씬의 집 인스턴스에도 (인테리어를 따로 다시 만들었다면 가구가 씬 쪽에 재생성돼 있다)
            ApplyS01Layout();
            Debug.Log(GameUI.EditorTools.Art.InteriorLookBuilder.ApplyAll());   // SSAO 보정 + Player 카메라 후처리 ON
            AssetDatabase.SaveAssets();
        }

        /// <summary>GameScene 의 Apartment_1004_Base 인스턴스에 같은 보정을 적용하고 저장한다 (이미 맞으면 변화 없음)</summary>
        private static void ApplyToGameScene()
        {
            Scene s = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject house = GameObject.Find("Gameplay/Apartment_1004_Base");
            if (house == null) { Debug.LogWarning("[BaseFixups] GameScene 에서 Apartment_1004_Base 를 찾지 못했습니다."); return; }
            Apply(house.transform);
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
        }

        // 시나리오 1 고정 위치 (MainMenuBuilder.S01.cs 의 S01FixedSpots 와 같은 값 — WaterAndCans 만 다르다)
        private static void ApplyS01Layout()
        {
            Scene s = EditorSceneManager.OpenScene(S01ScenePath, OpenSceneMode.Single);
            Transform content = null;
            foreach (GameObject r in s.GetRootGameObjects()) if (r.name == "S01_Content") content = r.transform;
            if (content == null) { Debug.LogWarning("[BaseFixups] S01_Content 를 찾지 못했습니다."); return; }
            Transform water = content.Find("Props/WaterAndCans");
            if (water != null) water.position = new Vector3(5.5f, 0.01f, 4.3f);
            Transform anchors = content.Find("Anchors");
            if (anchors == null) { anchors = new GameObject("Anchors").transform; anchors.SetParent(content, false); }
            int made = 0;
            foreach (string grp in new[] { "Props", "Survivors" })
            {
                Transform g = content.Find(grp);
                if (g == null) continue;
                foreach (Transform c in g)
                {
                    string id = AnchorId(c.name);
                    if (anchors.Find(id) != null) continue;
                    var a = new GameObject(id).AddComponent<ScenarioAnchor>();
                    a.transform.SetParent(anchors, false);
                    a.transform.position = c.position;
                    var so = new SerializedObject(a);
                    so.FindProperty("id").stringValue = id;
                    so.FindProperty("description").stringValue = "시나리오 1 고정 배치: " + c.name;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    made++;
                }
            }
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            Debug.Log($"[BaseFixups] S01 앵커 {made}개 생성, WaterAndCans 위치 보정");
        }

        private static string AnchorId(string goName)
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
    }
}
