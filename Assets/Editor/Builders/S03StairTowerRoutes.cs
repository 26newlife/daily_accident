using System.Collections.Generic;
using GameGameplay;
using GameUI.EditorTools.Interior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 3 루트 A(옥상)·D(1층 하강)를 집의 실제 계단탑(HouseStairwellBuilder)에서 진행하도록 S03 씬을 고친다.
    ///   · 압축 연출용 별도 구역(14·17·20층 계단참, 옥상 세트, 하강 3구간, 집결지 바닥·경계)을 지운다
    ///   · 14·17층 열린 방화문 → 계단탑 14·17층 층참 동쪽 벽 (장식용 방화문 자리, 계단탑 안쪽으로 열림)
    ///   · 옥상 출입문(잠김)·비상 열쇠함 → 계단탑 옥상층(y 33) 출입구 / 북쪽 벽
    ///   · 구조 신호·헬기·고가사다리 → 실제 옥상 (남쪽 = 도로 쪽 난간)
    ///   · 하강 연기 구역 3개 → 계단탑 9~7 / 6~4 / 3~1층, 계단참 연기 구역 → 13~15 / 16~18 / 19층~옥상층
    ///   · 외부 집결지(소방차·구급차·주민) → 1층 출구 서쪽 잔디밭
    ///   · 계단탑 전체 / 옥상 / 건물 밖은 격리 구역 — 집 안 화재 연기(전역)를 마시지 않는다
    ///   · 복도 할머니·청년, 집결지 주민의 모습을 사람 형태(NpcLook)로 바꾼다
    /// 여러 번 실행해도 결과가 같다 (좌표는 절대값, 만든 소품은 지우고 다시 만든다).
    /// 호출: MainMenuBuilder.BuildApartmentScene 마지막 (빌더를 다시 돌려도 유지) / 메뉴 Tools > GameUI > Scenarios > S03 Stair Tower Routes.
    /// </summary>
    public static class S03StairTowerRoutes
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string S03ScenePath = "Assets/Scenarios/S03_ApartmentFire/Scenes/S03_ApartmentFire.unity";
        private const float G = EvacTower.GroundY;
        private const string PropsName = "TowerRouteProps";

        [MenuItem("Tools/GameUI/Scenarios/S03 Stair Tower Routes (Apply)", priority = 303)]
        public static void Menu()
        {
            if (!EditorUtility.DisplayDialog("S03 계단탑 루트", "시나리오 3 의 옥상(A)·1층 하강(D) 루트를 집의 실제 계단탑에서 진행하도록 S03 씬을 고칩니다.\n여러 번 실행해도 같은 결과입니다.", "적용", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToAssets();
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.S03StairTowerRoutes.ApplyToAssets</summary>
        public static void ApplyToAssets()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Scene s3 = EditorSceneManager.OpenScene(S03ScenePath, OpenSceneMode.Additive);
            Apply(s3);
            EditorSceneManager.MarkSceneDirty(s3);
            EditorSceneManager.SaveScene(s3);
            EditorSceneManager.CloseScene(s3, true);
            AssetDatabase.SaveAssets();
        }

        public static void Apply(Scene s3)
        {
            var all = new List<Transform>();
            foreach (GameObject r in s3.GetRootGameObjects()) all.AddRange(r.GetComponentsInChildren<Transform>(true));
            Transform Find(string name) { foreach (Transform t in all) if (t != null && t.name == name) return t; return null; }
            int removed = 0;
            void Kill(Transform t) { if (t != null) { Object.DestroyImmediate(t.gameObject); removed++; } }

            Transform evac = Find("EvacWorld");
            if (evac == null) { Debug.LogWarning("[S03Tower] EvacWorld 를 찾지 못했습니다 — S03 씬이 맞는지 확인하세요."); return; }

            // ── 1) 압축 연출용 별도 구역 지우기 ──
            foreach (string g in new[] { "Landing14", "Landing17", "Landing20", "Descent1", "Descent2", "Descent3" }) Kill(evac.Find(g));
            foreach (Transform t in all.ToArray())
            {
                if (t == null) continue;
                string n = t.name;
                if (n.StartsWith("L14_") || n.StartsWith("L17_") || n.StartsWith("L20_") || n.StartsWith("D1_") || n.StartsWith("D2_") || n.StartsWith("D3_")) Kill(t);
            }
            foreach (string id in new[] { EvacKeys.StairsUp14, EvacKeys.StairsUp17 }) Kill(Find(id));
            for (int i = 1; i <= 3; i++) Kill(Find("Zone_zone_descent_end_" + i));
            foreach (string s in new[] { "Spawn_Landing14", "Spawn_Landing17", "Spawn_Landing20", "Spawn_Descent1", "Spawn_Descent2", "Spawn_Descent3" }) Kill(Find(s));
            Transform roof = evac.Find("Roof");
            if (roof != null)
                foreach (Transform c in ChildList(roof)) if (c.name != "Helicopter" && c.name != "RoofLadder") Kill(c);
            Transform plaza = evac.Find("AssemblyPlaza");
            if (plaza != null)
                foreach (Transform c in ChildList(plaza)) if (c.name == "Ground" || c.name == "GrassPatch" || c.name == "Bound") Kill(c);
            all.RemoveAll(t => t == null);

            Transform props = evac.Find(PropsName);
            if (props != null) Object.DestroyImmediate(props.gameObject);
            props = new GameObject(PropsName).transform;
            props.SetParent(evac, false);
            props.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // 아래 좌표는 월드(집 원점) 기준
            Material dark = OM.Lit("S03_DoorwayDark", OM.Hex("0B0B0D"), 0.05f);
            Material red = OM.Lit("S03_TruckRed", OM.Hex("DC2626"), 0.35f);
            Material metal = OM.Lit("S03_TruckMetal", OM.Hex("B8BEC6"), 0.6f, 0.6f);

            // ── 2) 14·17층 열린 방화문: 계단탑 층참 동쪽 벽 (x -18.2), 문 폭 z -1.08 ~ -0.18, 경첩은 북쪽 벽 옆 — 열면 북쪽 벽에 붙어 길을 막지 않는다 ──
            foreach ((string id, int floor) in new[] { (EvacKeys.FireDoor14, 14), (EvacKeys.FireDoor17, 17) })
            {
                float y = EvacTower.FloorY(floor);
                Transform door = Find(id);
                if (door != null)
                {
                    door.SetPositionAndRotation(new Vector3(-18.24f, y, -0.18f), Quaternion.Euler(0f, 180f, 0f));
                }
                else Debug.LogWarning("[S03Tower] 방화문 없음: " + id);
                // 문 너머: 연기 찬 어두운 복도 (장식용 방화문은 S03Bootstrap 이 숨긴다)
                Prim.Box(props, $"DoorwayDark_{floor}F", new Vector3(-18.205f, y + 1.05f, -0.63f), new Vector3(0.01f, 2.1f, 0.92f), dark);
            }

            // ── 3) 옥상 출입문(잠김) + 비상 열쇠함: 계단탑 옥상층 (y 33). 출입구 = 동쪽 벽 z -2.0 ~ -0.9 ──
            float ry = EvacTower.RoofY;
            Transform roofDoor = Find(EvacKeys.RoofDoor);
            if (roofDoor != null) roofDoor.SetPositionAndRotation(new Vector3(-18.1f, ry, -1.45f), Quaternion.identity);
            Transform keyBox = Find(EvacKeys.KeyBox);
            if (keyBox != null) keyBox.SetPositionAndRotation(new Vector3(-18.85f, ry + 1.3f, -0.21f), Quaternion.identity);   // 북쪽 벽, 유리는 남쪽(층참)을 향함

            // ── 4) 실제 옥상: 구조 신호(도로 쪽 난간), 헬기, 고가사다리 ──
            Transform signal = Find(EvacKeys.RoofSignal);
            if (signal != null) signal.SetPositionAndRotation(new Vector3(-2f, ry + 0.9f, 8.3f), Quaternion.identity);
            Transform roofZone = Find("Zone_zone_roof");
            SetZone(roofZone, new Vector3(-17.8f, ry, -3.8f), new Vector3(9.8f, ry + 12f, 9.2f));
            SetIsolated(roofZone, true);   // 옥상은 바깥 공기 — 집 안 화재 연기(전역)를 마시지 않는다
            Transform heliStart = Find("HeliStart"), heliEnd = Find("HeliEnd");
            if (heliStart != null) heliStart.position = new Vector3(70f, ry + 45f, 110f);
            if (heliEnd != null) heliEnd.position = new Vector3(-3f, ry + 7f, 3f);
            Transform heli = roof != null ? roof.Find("Helicopter") : null;
            if (heli != null && heliStart != null) heli.position = heliStart.position;
            Transform ladder = roof != null ? roof.Find("RoofLadder") : null;
            if (ladder != null)
            {
                Transform pivot = ladder.Find("Pivot");
                if (pivot != null)
                {
                    // 지면(도로 쪽 잔디)에서 옥상 난간까지 61.8m. yaw -90: 로컬 -x(바스켓 쪽) = 건물 쪽(-z)
                    float len = ry + 0.8f - G;
                    pivot.SetPositionAndRotation(new Vector3(-2f, G + 0f, 10.0f), Quaternion.Euler(0f, -90f, 0f));
                    Transform rail = pivot.Find("Rail"), basket = pivot.Find("Basket");
                    if (rail != null) { rail.localPosition = new Vector3(0f, len * 0.5f, 0f); rail.localScale = new Vector3(0.18f, len, 0.9f); }
                    if (basket != null) basket.localPosition = new Vector3(-0.6f, len, 0f);
                }
                Transform oldTruck = ladder.Find("LadderTruck");
                if (oldTruck != null) Object.DestroyImmediate(oldTruck.gameObject);
                var truck = new GameObject("LadderTruck").transform;
                truck.SetParent(ladder, false);
                truck.SetPositionAndRotation(new Vector3(-2f, G, 14f), Quaternion.identity);
                Prim.Box(truck, "Cab", new Vector3(0f, 1.3f, 4.2f), new Vector3(2.6f, 2.4f, 2.2f), red);
                Prim.Box(truck, "Body", new Vector3(0f, 1.4f, 0.4f), new Vector3(2.6f, 2.6f, 5.6f), red);
                Prim.Box(truck, "Turntable", new Vector3(0f, 2.9f, -3.2f), new Vector3(1.6f, 0.4f, 1.6f), metal);
            }

            // ── 5) 연기 구역: 계단참(루트 A) / 하강(루트 D) — 계단탑 내부 전체 폭 ──
            const float tx0 = -24.0f, tx1 = -18.0f, tz0 = -3.2f, tz1 = 0.0f;
            SetZone(Find("Zone_" + EvacKeys.ZoneLanding14), new Vector3(tx0, 9.0f, tz0), new Vector3(tx1, 18.0f, tz1));
            SetZone(Find("Zone_" + EvacKeys.ZoneLanding17), new Vector3(tx0, 18.0f, tz0), new Vector3(tx1, 27.0f, tz1));
            SetZone(Find("Zone_" + EvacKeys.ZoneLanding20), new Vector3(tx0, 27.0f, tz0), new Vector3(tx1, ry + 2.7f, tz1));
            SetZone(Find("Zone_" + EvacKeys.ZoneDescent1), new Vector3(tx0, -9.5f, tz0), new Vector3(tx1, -0.5f, tz1));
            SetZone(Find("Zone_" + EvacKeys.ZoneDescent2), new Vector3(tx0, -18.5f, tz0), new Vector3(tx1, -9.5f, tz1));
            SetZone(Find("Zone_" + EvacKeys.ZoneDescent3), new Vector3(tx0, G - 0.4f, tz0), new Vector3(tx1, -18.5f, tz1));

            // 계단탑 전체 / 건물 밖 전체: 격리 구역(기본 연기 0). 집 안 화재 연기(전역)는 계단탑·바깥으로 퍼지지 않는다.
            // 계단탑 안의 연기는 위의 계단참(루트 A)·하강(루트 D) 구역이 정한다 (겹치면 큰 값).
            Transform zoneParent = roofZone != null ? roofZone.parent : evac;
            SetIsolated(EnsureZone(zoneParent, "zone_tower_air", new Vector3(-24.15f, G - 0.5f, -3.33f), new Vector3(-18.0f, ry + 2.7f, 0.13f)), true);
            SetIsolated(EnsureZone(zoneParent, "zone_outside_air", new Vector3(-500f, G - 2f, -500f), new Vector3(500f, G + 20f, 500f)), true);

            // ── 6) 외부 집결지: 1층 출구(x -24.4, z -1.6) 서쪽 잔디밭 ──
            SetZone(Find("Zone_" + EvacKeys.ZoneAssembly), new Vector3(-50f, G, -10f), new Vector3(-30f, G + 6f, 8f));
            if (plaza != null)
            {
                Transform t;
                if ((t = plaza.Find("FireTruck")) != null) t.SetPositionAndRotation(new Vector3(-44f, G, 12f), Quaternion.identity);
                if ((t = plaza.Find("Ambulance")) != null) t.SetPositionAndRotation(new Vector3(-35f, G, 12.5f), Quaternion.identity);
                if ((t = plaza.Find("PlazaLight")) != null) t.position = new Vector3(-40f, G + 6f, -1f);
                Transform crowd = plaza.Find("Residents");
                if (crowd != null)
                {
                    int i = 0;
                    foreach (Transform r in crowd) { r.position = new Vector3(-46f + (i % 4) * 1.3f, G, -6f + (i / 4) * 1.4f); r.rotation = Quaternion.Euler(0f, 60f, 0f); i++; }
                }
                int c = 0;
                foreach (Transform k in ChildList(plaza))
                    if (k.name.StartsWith("Cone_")) { k.position = new Vector3(-34f + c * 1.6f, G + 0.25f, 5f); c++; }
            }
            Transform plazaSpawn = Find("Spawn_Plaza");
            if (plazaSpawn != null) plazaSpawn.SetPositionAndRotation(new Vector3(-38f, G + 0.02f, -1f), Quaternion.Euler(0f, 270f, 0f));

            int looks = ApplyNpcLooks(Find("NPC_Grandmother"), Find("NPC_YoungMan"), plaza != null ? plaza.Find("Residents") : null);
            Debug.Log($"[S03Tower] 루트 A·D 를 계단탑으로 옮겼습니다 (별도 구역 오브젝트 {removed}개 제거, 주민 모습 {looks}명 교체)");
        }

        /// <summary>복도 할머니·청년과 집결지 주민의 그레이박스 모습을 사람 형태(NpcLook)로 바꾼다</summary>
        private static int ApplyNpcLooks(Transform grandmother, Transform youngMan, Transform residents)
        {
            int n = 0;
            GameObject Replace(Transform owner, NpcLook.Style style)
            {
                Transform old = owner.Find("Visual");
                bool active = old == null || old.gameObject.activeSelf;
                if (old != null) Object.DestroyImmediate(old.gameObject);
                GameObject v = NpcLook.Build(owner, style);
                v.SetActive(active);
                n++;
                return v;
            }
            if (grandmother != null)
            {
                GameObject v = Replace(grandmother, NpcLook.Grandmother);
                var gm = grandmother.GetComponent<GrandmotherNpc>();
                if (gm != null)
                {
                    var so = new SerializedObject(gm);
                    so.FindProperty("visualRoot").objectReferenceValue = v.transform;
                    so.FindProperty("arm").objectReferenceValue = v.transform.Find("Arm");
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (youngMan != null)
            {
                GameObject v = Replace(youngMan, NpcLook.YoungMan);
                var ym = youngMan.GetComponent<YoungManNpc>();
                if (ym != null)
                {
                    var so = new SerializedObject(ym);
                    so.FindProperty("visualRoot").objectReferenceValue = v.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (residents != null)
            {
                int i = 0;
                foreach (Transform r in ChildList(residents)) Replace(r, NpcLook.Resident(i++));
            }
            return n;
        }

        private static void SetZone(Transform zone, Vector3 min, Vector3 max)
        {
            if (zone == null) { Debug.LogWarning("[S03Tower] 구역 없음 (min " + min + ")"); return; }
            zone.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            zone.localScale = Vector3.one;
            var box = zone.GetComponent<BoxCollider>();
            if (box != null) { box.center = Vector3.zero; box.size = max - min; }
        }

        /// <summary>구역이 없으면 만든다 (BoxCollider 트리거 + HazardZone), 있으면 범위만 다시 맞춘다</summary>
        private static Transform EnsureZone(Transform parent, string zoneId, Vector3 min, Vector3 max)
        {
            Transform t = parent.Find("Zone_" + zoneId);
            if (t == null)
            {
                var go = new GameObject("Zone_" + zoneId);
                go.transform.SetParent(parent, false);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                var z = go.AddComponent<HazardZone>();
                var so = new SerializedObject(z);
                so.FindProperty("zoneId").stringValue = zoneId;
                so.ApplyModifiedPropertiesWithoutUndo();
                t = go.transform;
            }
            SetZone(t, min, max);
            return t;
        }

        private static void SetIsolated(Transform zone, bool isolated)
        {
            HazardZone z = zone != null ? zone.GetComponent<HazardZone>() : null;
            if (z == null) return;
            var so = new SerializedObject(z);
            so.FindProperty("isolated").boolValue = isolated;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<Transform> ChildList(Transform t)
        {
            var list = new List<Transform>();
            foreach (Transform c in t) list.Add(c);
            return list;
        }
    }
}
