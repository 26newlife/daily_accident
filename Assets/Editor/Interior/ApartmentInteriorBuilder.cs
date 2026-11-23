using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using F = GameUI.EditorTools.Interior.FurnitureFactory;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 1004호 + 10층 복도 인테리어 자동 생성기.
    /// 실행하면 Level/Interior 루트와 옛 그레이박스 장식(침대/소파 등)을 지우고 새로 만든다 (여러 번 실행해도 중복되지 않음).
    /// 기존 상호작용 오브젝트(Level/Interactables), FireSource, HazardZone, LightGroup 은 건드리지 않고
    /// 그 위치를 피해서 주변 가구만 배치한다. 방별 배치 코드는 ApartmentInteriorBuilder.*.cs 로 분리되어 있다.
    /// </summary>
    public static partial class ApartmentInteriorBuilder
    {
        public const string RootName = "Interior";
        private const float FloorY = 0.012f;          // 시각용 바닥 높이 (슬래브 위 1.2cm)
        private const float BaseboardHeight = 0.08f;
        private const float DoorJamb = 0.06f;
        private const float WallThickInterior = 0.17f, WallThickExterior = 0.27f; // 문틀이 벽보다 1cm 튀어나오게
        private static readonly Vector2 ApartmentCenter = new Vector2(3.3f, 3.7f);

        // 옛 그레이박스 빌더가 만든 "상호작용 대상이 아닌" 장식 박스 이름. 새 인테리어가 대체한다.
        private static readonly string[] LegacyDecor =
        {
            "Bed", "Bed_Head", "Desk", "Nightstand", "Curtain", "TV_Cabinet", "Sofa", "Sofa_Back", "CoffeeTable",
            "Counter", "Fridge", "WashingMachine", "Toilet", "Sink", "ShoeCabinet_Body",
            "FlowerPot_2", "Balcony_Storage", "Shelter_Bench", "FireBox_Corridor",
        };

        // 새 가구가 대신 그려주므로 렌더러만 숨기는 상호작용 오브젝트 (콜라이더/컴포넌트/id 는 그대로 유지)
        private static readonly string[] HiddenStandIns = { "bed_bedroom", "closet_bedroom", "tv_living", "shoe_cabinet" };

        private static Transform level, structure, legacyFurniture, interactables, root;

        // ═════════════════════════ 진입점 ═════════════════════════
        [MenuItem("Tools/GameUI/Build Apartment Interior")]
        public static void BuildInteriorMenu()
        {
            Transform lv = FindLevel();
            if (lv == null)
            {
                EditorUtility.DisplayDialog("Build Apartment Interior",
                    "열려 있는 씬에서 Level 오브젝트를 찾지 못했습니다.\nGameScene 을 연 뒤 다시 실행하거나, Tools > GameUI > Build Apartment Scene 을 먼저 실행하세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("Build Apartment Interior",
                    "기존 Interior 와 옛 그레이박스 장식을 지우고 인테리어를 새로 생성합니다.\n상호작용 오브젝트/불/조명 그룹은 유지됩니다. 계속할까요?", "생성", "취소"))
                return;

            Build(lv);
            EditorSceneManager.MarkSceneDirty(lv.gameObject.scene);
            Debug.Log("[Interior] 인테리어 생성 완료. 씬을 저장하세요 (Ctrl+S).");

            // 생성 직후 통로/접근성 자동 검증 (결과는 Console + 씬 뷰 표시)
            try { Debug.Log(ApartmentClearanceValidator.RunAndSummarize()); SceneView.RepaintAll(); }
            catch (System.Exception e) { Debug.LogWarning("[Interior] 자동 검증을 건너뜁니다: " + e.Message + " (Tools > GameUI > Validate Apartment Clearance 로 직접 실행하세요)"); }
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Interior.ApartmentInteriorBuilder.BuildInteriorBatch (GameScene 이 열려 있어야 함)</summary>
        public static void BuildInteriorBatch()
        {
            Transform lv = FindLevel();
            if (lv == null) { Debug.LogError("[Interior] Level 을 찾지 못했습니다."); return; }
            Build(lv);
            EditorSceneManager.MarkSceneDirty(lv.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static Transform FindLevel()
        {
            GameObject g = GameObject.Find("Gameplay/Level");
            if (g == null) g = GameObject.Find("Level");
            return g != null ? g.transform : null;
        }

        /// <summary>Level 루트(Structure/Furniture/Interactables/Lights/HazardZones 를 가진 객체)에 인테리어를 생성한다</summary>
        public static void Build(Transform levelRoot)
        {
            level = levelRoot;
            structure = level.Find("Structure");
            legacyFurniture = level.Find("Furniture");
            interactables = level.Find("Interactables");
            if (structure == null || interactables == null)
            {
                Debug.LogError("[Interior] Level/Structure 또는 Level/Interactables 가 없습니다. 아파트 씬 빌더로 만든 Level 인지 확인하세요.");
                return;
            }

            M.ResetCache();
            F.ResetCache();
            InteriorTags.EnsureFlammableTag();

            CleanupPrevious();
            root = new GameObject(RootName).transform;
            root.SetParent(level, false);

            BuildBalconyFullGlass();  // 베란다 외벽 개구부를 바닥~천장 통유리로 (SkinStructure 보다 먼저: 걸레받이/재질 계산에 반영)
            SkinStructure();          // 벽/천장 재질, 걸레받이, 문틀/창틀, 바닥 마감
            BuildBedroom();
            BuildLiving();
            BuildKitchen();
            BuildUtility();
            BuildBathroom();
            BuildEntry();
            BuildBalcony();
            BuildShelter();
            BuildCorridor();
            HideStandIns();
            BuildLighting();          // 아침 햇빛, 창가 조명, 환경광, 반사 프로브
            BuildSafetyColliders();   // 점프로 창밖으로 나가지 못하게 막는 투명 콜라이더
            MarkStaticRecursive(root);

            // 인테리어만 다시 만들면 가구(Interior)가 원래 값으로 재생성된다 → 확정 보정(B-02·화재 소품 태그·치우기 소품)을 다시 적용한다.
            // 집 루트(Apartment_1004_Base)가 이미 있을 때만 (씬 전체 빌드 중에는 FinalizeFramework 가 적용한다)
            Transform houseRoot = level.parent;
            if (houseRoot != null && houseRoot.name == "Apartment_1004_Base") BaseHouseFixups.Apply(houseRoot);

            Debug.Log($"[Interior] 생성 완료: 프리팹 {FurnitureFactory.PrefabDir}, 머티리얼 {M.MaterialDir}");
        }

        // ═════════════════════════ 정리 ═════════════════════════
        private static void CleanupPrevious()
        {
            Transform old = level.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            if (legacyFurniture != null)
                foreach (string n in LegacyDecor)
                {
                    Transform t;
                    while ((t = legacyFurniture.Find(n)) != null) Object.DestroyImmediate(t.gameObject);
                }

            // 옛 시각용 바닥 패치 (Floor_Bedroom 등). FloorSlab(콜라이더)은 유지
            var patches = new List<GameObject>();
            foreach (Transform c in structure) if (c.name.StartsWith("Floor_")) patches.Add(c.gameObject);
            foreach (GameObject p in patches) Object.DestroyImmediate(p);
        }

        private static void HideStandIns()
        {
            foreach (string id in HiddenStandIns)
            {
                Transform t = interactables.Find(id);
                if (t == null) continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }

        // ═════════════════════════ 헬퍼 ═════════════════════════
        private static Transform Room(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        private static GameObject Furn(string prefab, Transform room, float x, float z, float yaw = 0f, float y = 0f, string name = null)
            => F.Place(prefab, room, x, z, yaw, y, name);

        private static GameObject Furn(string prefab, Transform room, Vector3 pos, float yaw, Vector3 scale, string name = null)
            => F.Place(prefab, room, pos, yaw, name, scale);

        /// <summary>가연물 소품을 기존 FireSource 와 연결 (표시용 슬롯)</summary>
        private static void LinkFire(GameObject go, string fireId)
        {
            Transform fire = interactables.Find(fireId);
            var fl = go.GetComponent<Flammable>();
            if (fire == null || fl == null) return;
            var fs = fire.GetComponent<FireSource>();
            if (fs == null) return;
            var so = new SerializedObject(fl);
            so.FindProperty("linkedFireId").stringValue = fs.Id;   // 집은 시나리오 오브젝트를 직접 참조하지 않는다 (id 로만 연결)
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Interactable(string id) => interactables.Find(id);

        /// <summary>
        /// 배치를 바꾸면서 상호작용 오브젝트를 새 위치로 옮긴다 (id/컴포넌트/자식은 그대로, 위치만 변경).
        /// 절대 좌표를 대입하므로 여러 번 실행해도 같은 결과다.
        /// </summary>
        private static void MoveInteractable(string id, Vector3 position)
        {
            Transform t = Interactable(id);
            if (t != null) t.position = position;
            else Debug.LogWarning("[Interior] 이동할 상호작용 오브젝트를 찾지 못했습니다: " + id);
        }

        /// <summary>방화문처럼 열림 방향만 바꿔야 할 때 (닫힌 위치/id 는 그대로)</summary>
        private static void SetDoorOpenYaw(string id, float yawDegrees)
        {
            Transform t = Interactable(id);
            var tog = t != null ? t.GetComponent<ToggleInteractable>() : null;
            if (tog == null) return;
            var so = new SerializedObject(tog);
            so.FindProperty("onLocalEuler").vector3Value = new Vector3(0f, yawDegrees, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>벽에 붙이는 프리팹: 벽 안쪽 면(wallFace)에 두께 절반만큼 띄워 배치. facing 은 방 안쪽을 향하는 yaw</summary>
        private static GameObject OnWall(string prefab, Transform room, Vector3 pos, float yaw, string name = null)
            => F.Place(prefab, room, pos, yaw, name);

        /// <summary>
        /// 상호작용 대상(옷장 등)의 "겉모습 대체" 가구는 프리팹 콜라이더를 끈다.
        /// PlayerInteractor 는 레이캐스트가 처음 맞힌 콜라이더에서 InteractableObject 를 찾으므로,
        /// 새 가구 콜라이더가 기존 상호작용 콜라이더보다 앞에 있으면 상호작용이 가려진다.
        /// 물리적 막힘은 기존 상호작용 오브젝트의 콜라이더가 그대로 담당한다.
        /// </summary>
        private static void UseInteractableCollider(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private static void MakeDynamic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        private static void MarkStaticRecursive(Transform t)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags);
        }

        private static void SetArray(SerializedProperty prop, Object[] items)
        {
            prop.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ═════════════════════════ 구조 마감 ═════════════════════════
        private static void SkinStructure()
        {
            var baseboards = Group("Baseboards", root);
            var frames = Group("Frames", root);
            var floors = Group("Floors", root);

            foreach (Transform c in structure)
            {
                if (c.name == "FloorSlab") { SetMat(c, M.SlabConcrete); continue; }
                if (c.name == "CeilingSlab") { SetMat(c, M.Ceiling); continue; }
                if (!c.name.StartsWith("Wall_")) continue;

                bool corridor = c.name.StartsWith("Wall_Corridor") || c.name.StartsWith("Wall_Stair");
                foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
                {
                    if (r.name == BalconyGlassName) continue;   // 통유리는 벽 재질로 덮지 않는다
                    r.sharedMaterial = corridor ? M.CorridorWall : M.Wall;
                    Bounds b = r.bounds;
                    // 바닥에서 시작하는 벽 조각에만 걸레받이 (문/창문 개구부는 조각이 끊겨 있어 자동으로 비워진다)
                    if (b.min.y > 0.02f || b.size.y < 0.25f) continue;
                    bool thinX = b.size.x < b.size.z;
                    var size = new Vector3(b.size.x + (thinX ? 0.03f : 0f), BaseboardHeight, b.size.z + (thinX ? 0f : 0.03f));
                    Prim.Box(baseboards, "Baseboard", new Vector3(b.center.x, BaseboardHeight * 0.5f, b.center.z), size,
                        corridor ? M.CorridorBase : M.Baseboard);
                }
            }

            BuildFrames(frames);
            BuildFloors(floors);
        }

        private static void SetMat(Transform t, Material m)
        {
            var r = t.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = m;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ───────── 바닥 (방별 재질, UV 가 미터 단위라 타일 크기가 방 크기와 무관) ─────────
        private static void BuildFloors(Transform parent)
        {
            Floor(parent, "Bedroom", 0f, 2.6f, 2.4f, 7.4f, M.FloorWood, M.WoodTexMeters);
            Floor(parent, "LivingHall", 2.6f, 6.6f, 4.6f, 7.4f, M.FloorWood, M.WoodTexMeters);
            Floor(parent, "Hall", 2.6f, 3.9f, 2.4f, 4.6f, M.FloorWood, M.WoodTexMeters);
            Floor(parent, "Kitchen", 3.9f, 6.6f, 2.4f, 4.6f, M.FloorWood, M.WoodTexMeters);   // 참고 이미지: 주방도 마루로 이어진다
            Floor(parent, "Entry", 1.9f, 4.1f, 0f, 2.4f, M.FloorTileEntry, M.TileTexMeters);
            Floor(parent, "Bathroom", 0f, 1.9f, 0f, 2.4f, M.FloorTileBath, M.BathTileTexMeters);
            Floor(parent, "Utility", 4.1f, 6.6f, 0f, 2.4f, M.FloorTileUtility, M.TileTexMeters);
            Floor(parent, "Balcony", 0f, 5.4f, 7.4f, 9.0f, M.FloorTileBalcony, M.TileTexMeters);
            Floor(parent, "Shelter", 5.4f, 6.6f, 7.4f, 9.0f, M.FloorShelter, M.TileTexMeters);
            Floor(parent, "Corridor", -14f, 9.5f, -2.2f, 0f, M.FloorCorridor, M.TileTexMeters);
        }

        private static void Floor(Transform parent, string name, float x0, float x1, float z0, float z1, Material mat, float tileMeters)
            => Prim.Quad(parent, "Floor_" + name, new Vector3(x0, FloorY, z0), Vector3.right, x1 - x0, Vector3.forward, z1 - z0, Vector3.up, mat, tileMeters);

        // ───────── 문틀 / 창틀 (기존 문·창문 오브젝트의 닫힌 상태 크기에서 자동 계산) ─────────
        private static void BuildFrames(Transform parent)
        {
            // id → (문짝/유리 자식 경로, 벽 두께, 창틀 여부, 재질)
            FrameFor(parent, "door_front", "Hinge/Visual", WallThickExterior, false, M.FrontDoorFrame);
            FrameFor(parent, "door_bedroom", "Hinge/Visual", WallThickInterior, false, M.DoorFrame);
            FrameFor(parent, "door_bathroom", "Hinge/Visual", WallThickInterior, false, M.DoorFrame);
            FrameFor(parent, "door_utility", "Hinge/Visual", WallThickInterior, false, M.DoorFrame);
            FrameFor(parent, "fire_door_shelter", "Hinge/Visual", WallThickInterior, false, M.FireDoorFrame);
            FrameFor(parent, "fire_door_stairs", "Hinge/Visual", WallThickExterior, false, M.FireDoorFrame);
            FrameFor(parent, "door_balcony", "Pane", WallThickExterior, false, M.DoorFrame);
            FrameFor(parent, "window_bedroom", "Pane", WallThickExterior, true, M.DoorFrame);
            FrameFor(parent, "window_living", "Pane", WallThickExterior, true, M.DoorFrame);
            FrameFor(parent, "window_kitchen", "Pane", WallThickExterior, true, M.DoorFrame);
            FrameFor(parent, "elevator_door", "Visual", WallThickInterior, false, M.FireDoorFrame);

            // 방화문/현관문은 두껍고 묵직하게 보이도록 문짝 두께를 2배로 (id/컴포넌트는 그대로)
            foreach (string id in new[] { "fire_door_shelter", "fire_door_stairs", "door_front" })
            {
                Transform hinge = Interactable(id)?.Find("Hinge");
                Transform vis = hinge != null ? hinge.Find("Visual") : null;
                if (vis == null) continue;
                Vector3 s = vis.localScale;
                if (s.x < s.z) s.x *= 2f; else s.z *= 2f;
                vis.localScale = s;
                AddFireDoorDetails(hinge, vis);
            }
        }

        private static void FrameFor(Transform parent, string id, string childPath, float thick, bool sill, Material mat)
        {
            Transform t = Interactable(id);
            Transform target = t != null ? t.Find(childPath) : null;
            var r = target != null ? target.GetComponent<Renderer>() : null;
            if (r == null) return;
            MakeFrame(parent, id, r.bounds, thick, sill, mat);
        }

        private static void MakeFrame(Transform parent, string name, Bounds b, float thick, bool sill, Material mat)
        {
            bool thinX = b.size.x < b.size.z;               // 벽이 z 방향으로 뻗으면 x 가 얇은 축
            float width = thinX ? b.size.z : b.size.x;
            float j = DoorJamb;
            Vector3 P(float along, float y) => thinX ? new Vector3(b.center.x, y, b.center.z + along) : new Vector3(b.center.x + along, y, b.center.z);
            Vector3 S(float len, float h, float t) => thinX ? new Vector3(t, h, len) : new Vector3(len, h, t);

            float jambH = b.size.y + j;
            float yc = b.min.y + jambH * 0.5f;
            Prim.Box(parent, name + "_JambL", P(-(width * 0.5f + j * 0.5f), yc), S(j, jambH, thick), mat);
            Prim.Box(parent, name + "_JambR", P(width * 0.5f + j * 0.5f, yc), S(j, jambH, thick), mat);
            Prim.Box(parent, name + "_Header", P(0f, b.max.y + j * 0.5f), S(width + j * 2f, j, thick), mat);
            if (sill) Prim.Box(parent, name + "_Sill", P(0f, b.min.y - 0.02f), S(width + j * 2f + 0.06f, 0.04f, thick + 0.12f), mat);
        }

        /// <summary>방화문 디테일: 하단 철판(킥플레이트) + 도어클로저 (문과 함께 움직이므로 Hinge 자식, 정적 아님)</summary>
        private static void AddFireDoorDetails(Transform hinge, Transform visual)
        {
            Vector3 s = visual.localScale;
            bool thinX = s.x < s.z;
            float thick = thinX ? s.x : s.z;
            float len = thinX ? s.z : s.x;
            Vector3 centerXZ = new Vector3(visual.localPosition.x, 0f, visual.localPosition.z);

            GameObject plate = Prim.Box(hinge, "KickPlate", centerXZ + Vector3.up * 0.16f,
                thinX ? new Vector3(thick * 1.1f, 0.3f, len - 0.08f) : new Vector3(len - 0.08f, 0.3f, thick * 1.1f), M.Metal);
            GameObject closer = Prim.Box(hinge, "DoorCloser", centerXZ + new Vector3(thinX ? thick * 0.5f + 0.03f : 0f, 1.98f, thinX ? 0f : thick * 0.5f + 0.03f),
                new Vector3(0.18f, 0.06f, 0.06f), M.MetalDark);
            MakeDynamic(plate); MakeDynamic(closer);
        }
    }
}
