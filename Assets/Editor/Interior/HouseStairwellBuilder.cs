using System.Collections.Generic;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 공통 베이스 집에 "걸어서 오르내리는 비상계단"을 덧붙인다 (기존 10층 계단실 입구를 서쪽으로 연장한 계단탑).
    ///   · 계단탑: x -24.15 ~ -18 (건물 서쪽 외벽 바깥), z -3.33 ~ 0.13. 1~20층 꺾인 계단(층마다 10단 x 2, 단 높이 0.15 / 너비 0.25) + 옥상층
    ///   · 층 바닥 높이 y = (층 - 10) x 3. 1층 = -27, 옥상 = 33. 1층 아래 반층참(y -28.5 = 지면)의 서쪽 벽에 건물 출구
    ///   · 10층은 기존 계단실(블록아웃)과 이어진다: 켜면 서쪽 벽·블록아웃 계단 8단·걸레받이를 숨긴다 (StairwellExtension.stubObjects)
    ///   · 11~20층 건물 외벽(창문 무늬) + 옥상 슬래브·난간, 1층 지면 콜라이더, 우리 동·주변 동 콜라이더 (건물 밖을 걸어 다닐 수 있게)
    /// 모든 오브젝트는 "WalkableStairwell" 루트 아래에 있고 평소 비활성이다 (ScenarioDefinition.useWalkableStairwell 로 켠다).
    /// 기존 오브젝트·id 는 바꾸지 않는다. 여러 번 실행해도 결과가 같다 (루트를 지우고 다시 만든다).
    /// 호출: BaseHouseFixups.Apply (빌더가 집을 다시 만들어도 유지) / 메뉴 Tools > GameUI > Base House > Build Walkable Stairwell.
    /// </summary>
    public static class HouseStairwellBuilder
    {
        public const string RootName = "WalkableStairwell";
        private const string PrefabPath = "Assets/House/Prefabs/Apartment_1004_Base.prefab";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string MeshDir = "Assets/House/Generated";
        private const string FontPath = "Assets/Fonts/NotoSansKR SDF.asset";

        // ───────── 치수 (월드 m, 10층 바닥 y = 0) ─────────
        public const float FloorH = 3f;
        public const int HomeFloor = 10, TopFloor = 20, RoofFloor = 21;
        public const float GroundY = -28.5f;
        public static float Y(int floor) => (floor - HomeFloor) * FloorH;
        public static float RoofY => Y(RoofFloor);

        private const float X0 = -23.9f, X1 = -18f;          // 계단탑 내부 서쪽 / 동쪽(건물 외벽)
        private const float Z0 = -3.08f, Z1 = -0.13f;        // 내부 남쪽 / 북쪽 (기존 계단실 내부와 같은 폭)
        private const float WallT = 0.25f;
        private const float LandE = -19.4f;                  // 층참 서쪽 끝 (층참 = -19.4 ~ -18)
        private const float MidE = -21.9f;                   // 반층참 동쪽 끝 (반층참 = -23.9 ~ -21.9)
        private const float RiseStep = 0.15f, RunStep = 0.25f;
        private const int Steps = 10;
        private const float NorthLaneZ0 = -1.55f, SouthLaneZ1 = -1.66f;   // 북쪽 줄(올라가며 서쪽) / 남쪽 줄(올라가며 동쪽)
        private const float EastWallX0 = -18.2f;             // 층 출입문 벽 (외벽 앞)
        private const float PenthouseH = 2.7f;

        // 건물(우리 동) 외곽 — OutdoorWorldBuilder.BuildSelfFacade 와 같은 값
        private const float BX0 = -18f, BX1 = 10f, BZ0 = -4f, BZ1 = 9.4f;
        private const float UpperBottom = 2.7f;             // 10층 천장 슬래브 윗면

        private static Transform root;
        private static readonly List<Light> lights = new List<Light>();
        private static readonly List<Renderer> lamps = new List<Renderer>();
        private static Material concrete, wallMat, stepMat, railMat, doorMat, signMat, lampMat, roofMat, parapetMat, outerMat, darkMat;
        private static TMP_FontAsset font;
        private static Mesh flightMesh;

        // ═════════════════════════ 진입점 ═════════════════════════
        [MenuItem("Tools/GameUI/Base House/Build Walkable Stairwell")]
        public static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("비상계단 만들기", "집 프리팹에 1~20층 + 옥상 비상계단(1층 출구 포함)을 만듭니다.\n평소에는 꺼져 있고 useWalkableStairwell 이 켜진 시나리오에서만 보입니다.\n여러 번 실행해도 같은 결과입니다.", "만들기", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToAssets();
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Interior.HouseStairwellBuilder.ApplyToAssets</summary>
        public static void ApplyToAssets()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Build(prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Stairwell] 집 프리팹에 비상계단 확장을 저장했습니다: " + PrefabPath);
        }

        /// <summary>houseRoot = Apartment_1004_Base (프리팹 편집 루트 또는 빌드 중인 씬 루트)</summary>
        public static void Build(Transform houseRoot)
        {
            if (houseRoot == null) return;
            // 씬의 프리팹 인스턴스에서는 자식을 지울 수 없다 — 프리팹 에셋에 만들면 인스턴스에 그대로 반영된다
            if (PrefabUtility.IsPartOfPrefabInstance(houseRoot.gameObject))
            {
                if (houseRoot.GetComponentInChildren<StairwellExtension>(true) == null)
                    Debug.LogWarning("[Stairwell] 씬의 집 프리팹 인스턴스에는 직접 만들지 않습니다. Tools > GameUI > Base House > Build Walkable Stairwell 로 프리팹에 만드세요.");
                return;
            }
            Transform level = houseRoot.Find("Level");
            if (level == null) { Debug.LogWarning("[Stairwell] Level 을 찾지 못해 비상계단을 만들지 않습니다."); return; }

            Transform old = houseRoot.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var host = houseRoot.GetComponentInChildren<StairwellExtension>(true);
            if (host != null) Object.DestroyImmediate(host);
            // 스크립트 GUID 가 바뀌어 깨진 이전 컴포넌트(Missing Script)가 Level 에 남아 있으면 지운다 (정상 컴포넌트는 건드리지 않는다)
            int broken = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(level.gameObject);
            if (broken > 0) Debug.Log($"[Stairwell] Level 의 깨진 컴포넌트 {broken}개 제거");

            lights.Clear(); lamps.Clear();
            Materials(houseRoot);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            flightMesh = EnsureFlightMesh();

            var rootGo = new GameObject(RootName);
            root = rootGo.transform;
            root.SetParent(houseRoot, false);
            Transform shaft = Group("Shaft", root);
            Transform outside = Group("Outside", root);

            BuildShaftWalls(shaft);
            for (int f = 1; f <= RoofFloor; f++) BuildFloorLanding(shaft, f);
            for (int f = 0; f <= TopFloor; f++) BuildMidLanding(shaft, f);
            for (int f = 1; f <= TopFloor; f++) BuildFlight(shaft, $"Flight_{f}F_Up", true, Y(f));            // 층참 → 반층참 (북쪽 줄)
            for (int f = 0; f <= TopFloor; f++) BuildFlight(shaft, $"Flight_{f}F_Mid_Up", false, Y(f) + 1.5f); // 반층참 → 위층 층참 (남쪽 줄)
            BuildJunctionFillers(shaft);
            BuildUpperBuilding(outside, houseRoot);
            BuildGroundAndColliders(outside, houseRoot);

            // 컴포넌트 (집 HouseController 와 같은 오브젝트가 아닌, 항상 켜져 있는 레벨 아래에 둔다)
            var ext = level.gameObject.AddComponent<StairwellExtension>();
            var so = new SerializedObject(ext);
            so.FindProperty("extensionRoot").objectReferenceValue = rootGo;
            SetArray(so.FindProperty("stubObjects"), StubObjects(level));
            SetArray(so.FindProperty("shaftLights"), lights.ToArray());
            SetArray(so.FindProperty("lampRenderers"), lamps.ToArray());
            so.FindProperty("floorHeight").floatValue = FloorH;
            so.FindProperty("homeFloor").intValue = HomeFloor;
            so.FindProperty("topFloor").intValue = TopFloor;
            so.FindProperty("groundY").floatValue = GroundY;
            so.FindProperty("roofY").floatValue = RoofY;
            so.ApplyModifiedPropertiesWithoutUndo();

            rootGo.SetActive(false);
            Debug.Log($"[Stairwell] 비상계단 확장 생성: 계단 {2 * TopFloor + 1}구간, 조명 {lights.Count}개, 숨길 입구 오브젝트 {so.FindProperty("stubObjects").arraySize}개");
        }

        // ═════════════════════════ 기존 입구 (켜면 숨김) ═════════════════════════
        private static Object[] StubObjects(Transform level)
        {
            var list = new List<Object>();
            void Add(string path) { Transform t = level.Find(path); if (t != null) list.Add(t.gameObject); else Debug.LogWarning("[Stairwell] 입구 오브젝트를 찾지 못했습니다: Level/" + path); }
            Add("Structure/Wall_StairWest");
            Add("Structure/EmergencyStairs");
            // 서쪽 벽 걸레받이 (이름이 모두 Baseboard 라 위치로 찾는다)
            Transform boards = level.Find("Interior/Baseboards");
            if (boards != null)
                foreach (Transform b in boards)
                {
                    Renderer r = b.GetComponent<Renderer>();
                    if (r == null) continue;
                    Bounds bb = r.bounds;
                    if (bb.max.x < -17.3f && bb.min.x > -17.7f && bb.size.z > 2f) list.Add(b.gameObject);
                }
            return list.ToArray();
        }

        // ═════════════════════════ 계단탑 벽 ═════════════════════════
        private static void BuildShaftWalls(Transform p)
        {
            float bottom = GroundY - 0.3f, top = RoofY + PenthouseH;
            float wx0 = X0 - WallT;
            // 서쪽 벽: 1층 출구(반층참 0 = 지면) 자리만 비운다
            const float doorZ0 = -2.35f, doorZ1 = -0.85f, doorTop = GroundY + 2.1f;
            Solid(p, "Wall_West_S", new Vector3(wx0, bottom, Z0 - WallT), new Vector3(X0, top, doorZ0), outerMat);
            Solid(p, "Wall_West_N", new Vector3(wx0, bottom, doorZ1), new Vector3(X0, top, Z1 + WallT), outerMat);
            Solid(p, "Wall_West_Lintel", new Vector3(wx0, doorTop, doorZ0), new Vector3(X0, top, doorZ1), outerMat);
            Solid(p, "Wall_South", new Vector3(wx0, bottom, Z0 - WallT), new Vector3(X1, top, Z0), outerMat);
            Solid(p, "Wall_North", new Vector3(wx0, bottom, Z1), new Vector3(X1, top, Z1 + WallT), outerMat);
            Solid(p, "Roof_Cap", new Vector3(wx0, top, Z0 - WallT), new Vector3(X1, top + 0.3f, Z1 + WallT), roofMat);
            Solid(p, "Pit", new Vector3(X0, bottom, Z0), new Vector3(X1, GroundY - 0.1f, Z1), concrete);

            // 동쪽(건물 쪽) 벽: 10층(기존 계단실과 이어지는 곳)과 옥상 출입구만 비운다
            Solid(p, "Wall_East_Lower", new Vector3(EastWallX0, bottom, Z0), new Vector3(X1, Y(HomeFloor), Z1), wallMat);
            Solid(p, "Wall_East_Upper", new Vector3(EastWallX0, 2.4f, Z0), new Vector3(X1, RoofY, Z1), wallMat);
            const float roofDoorZ0 = -2.0f, roofDoorZ1 = -0.9f;
            float ry = RoofY;
            Solid(p, "Wall_East_Roof_S", new Vector3(EastWallX0, ry, Z0), new Vector3(X1, top, roofDoorZ0), wallMat);
            Solid(p, "Wall_East_Roof_N", new Vector3(EastWallX0, ry, roofDoorZ1), new Vector3(X1, top, Z1), wallMat);
            Solid(p, "Wall_East_Roof_Lintel", new Vector3(EastWallX0, ry + 2.1f, roofDoorZ0), new Vector3(X1, top, roofDoorZ1), wallMat);
            // 출입구 표지
            Visual(p, "ExitSign_Ground", new Vector3(X0 + 0.04f, doorTop + 0.2f, (doorZ0 + doorZ1) * 0.5f), new Vector3(0.05f, 0.22f, 0.7f), signMat);
            Label(p, "Label_Exit", "출구 EXIT", new Vector3(X0 + 0.08f, doorTop + 0.2f, (doorZ0 + doorZ1) * 0.5f), 90f, 0.16f, Color.white);
            Visual(p, "ExitSign_Roof", new Vector3(EastWallX0 - 0.04f, ry + 2.3f, (roofDoorZ0 + roofDoorZ1) * 0.5f), new Vector3(0.05f, 0.22f, 0.7f), signMat);
            Label(p, "Label_Roof", "옥상 ROOF", new Vector3(EastWallX0 - 0.08f, ry + 2.3f, (roofDoorZ0 + roofDoorZ1) * 0.5f), -90f, 0.16f, Color.white);
        }

        // ═════════════════════════ 층참 / 반층참 ═════════════════════════
        private static void BuildFloorLanding(Transform p, int floor)
        {
            float y = Y(floor);
            Transform g = Group($"Landing_{floor}F", p);
            float east = floor == HomeFloor ? X1 : EastWallX0;
            Solid(g, "Slab", new Vector3(LandE, y - 0.2f, Z0), new Vector3(east, y, Z1), concrete);
            if (floor != HomeFloor && floor != RoofFloor)
            {
                // 다른 층 복도로 가는 방화문 (닫혀 있음 — 장식)
                Visual(g, "FireDoor", new Vector3(EastWallX0 - 0.03f, y + 1.05f, -1.1f), new Vector3(0.06f, 2.1f, 1.0f), doorMat);
                Visual(g, "FireDoor_Handle", new Vector3(EastWallX0 - 0.08f, y + 1.0f, -0.75f), new Vector3(0.05f, 0.04f, 0.14f), railMat);
                Visual(g, "ExitSign", new Vector3(EastWallX0 - 0.03f, y + 2.3f, -1.1f), new Vector3(0.05f, 0.18f, 0.6f), signMat);
            }
            string label = floor == RoofFloor ? "옥상" : floor + "F";
            Label(g, "FloorLabel", label, new Vector3(EastWallX0 - 0.07f, y + 1.6f, -2.35f), -90f, 0.45f, new Color(0.15f, 0.75f, 0.4f));
            Label(g, "FloorLabel_Stair", label, new Vector3(LandE + 0.2f, y + 1.7f, Z1 - 0.03f), 180f, 0.32f, new Color(0.15f, 0.75f, 0.4f));
            AddLamp(g, new Vector3((LandE + X1) * 0.5f, y + 2.55f, -1.6f));
        }

        private static void BuildMidLanding(Transform p, int floor)
        {
            float y = Y(floor) + 1.5f;
            Transform g = Group($"MidLanding_{floor}F", p);
            Solid(g, "Slab", new Vector3(X0, y - 0.2f, Z0), new Vector3(MidE, y, Z1), concrete);
            AddLamp(g, new Vector3((X0 + MidE) * 0.5f, y + 2.55f, -1.6f));
        }

        /// <summary>계단 한 구간. north = 층참에서 서쪽으로 오르는 북쪽 줄, 아니면 반층참에서 동쪽으로 오르는 남쪽 줄. baseY = 시작 높이</summary>
        private static void BuildFlight(Transform p, string name, bool north, float baseY)
        {
            float zc = north ? (NorthLaneZ0 + Z1) * 0.5f : (Z0 + SouthLaneZ1) * 0.5f;
            float width = north ? Z1 - NorthLaneZ0 : SouthLaneZ1 - Z0;
            float startX = north ? LandE : MidE;    // 아래쪽 끝
            var g = new GameObject(name).transform;
            g.SetParent(p, false);
            g.localPosition = new Vector3(startX, baseY, zc);
            g.localRotation = Quaternion.Euler(0f, north ? 180f : 0f, 0f);   // 메시는 +x 로 올라간다

            var vis = new GameObject("Steps", typeof(MeshFilter), typeof(MeshRenderer));
            vis.transform.SetParent(g, false);
            vis.transform.localScale = new Vector3(1f, 1f, width);
            vis.GetComponent<MeshFilter>().sharedMesh = flightMesh;
            vis.GetComponent<MeshRenderer>().sharedMaterial = stepMat;

            // 걷기용 경사 콜라이더 (계단 코 높이 선)
            float run = RunStep * Steps, rise = RiseStep * Steps;
            float len = Mathf.Sqrt(run * run + rise * rise), ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(g, false);
            ramp.transform.localPosition = new Vector3(run * 0.5f, rise * 0.5f + 0.02f, 0f);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            var bc = ramp.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, -0.06f, 0f);
            bc.size = new Vector3(len + 0.05f, 0.12f, width);

            // 두 줄 사이 난간벽 (떨어지지 않게). 남쪽 줄은 로컬 +z 가 가운데(z -1.6), 북쪽 줄은 180° 돌아 있어 역시 로컬 +z 가 가운데다
            float railZ = width * 0.5f + 0.05f;
            var rail = Prim.Box(g, "Balustrade", new Vector3(run * 0.5f, rise * 0.5f + 0.5f, railZ), new Vector3(len, 1.0f, 0.08f), railMat, new Vector3(0f, 0f, ang));
            rail.AddComponent<BoxCollider>();
            Prim.Box(g, "Handrail", new Vector3(run * 0.5f, rise * 0.5f + 1.02f, railZ), new Vector3(len, 0.05f, 0.12f), darkMat, new Vector3(0f, 0f, ang));
        }

        /// <summary>층참·반층참 가장자리의 난간 끝 (두 줄 사이 틈으로 떨어지지 않게)</summary>
        private static void BuildJunctionFillers(Transform p)
        {
            // 계단 구간 사이(x -21.9 ~ -19.4) 가운데 틈 z -1.66 ~ -1.55 은 난간벽이 막는다.
            // 1층 아래: 지면 반층참(출구)에서 북쪽 줄 계단 밑 빈 공간으로 들어가지 않게 채운다
            Solid(p, "UnderStair_1F", new Vector3(MidE, GroundY - 0.3f, NorthLaneZ0 - 0.11f), new Vector3(EastWallX0, Y(1) - 0.22f, Z1), concrete);
            // 10층: 기존 계단실 서쪽 벽 모서리 자리 (벽을 숨기면 생기는 틈)
            Solid(p, "Filler_10F_North", new Vector3(X1, 0f, Z1), new Vector3(-17.63f, 2.4f, 0.13f), wallMat);
            Solid(p, "Filler_10F_South", new Vector3(X1, 0f, -3.33f), new Vector3(-17.63f, 2.4f, Z0), wallMat);
        }

        // ═════════════════════════ 11~20층 외벽 + 옥상 ═════════════════════════
        private static void BuildUpperBuilding(Transform p, Transform houseRoot)
        {
            Transform g = Group("UpperBuilding", p);
            float height = RoofY - 0.3f - UpperBottom;
            var facade = new GameObject("UpperFacade", typeof(MeshFilter), typeof(MeshRenderer));
            facade.transform.SetParent(g, false);
            facade.transform.localPosition = new Vector3((BX0 + BX1) * 0.5f, UpperBottom, (BZ0 + BZ1) * 0.5f);
            facade.GetComponent<MeshFilter>().sharedMesh = SaveMesh(BuildingFacadeGenerator.CreateTowerMesh("UpperFacade", BX1 - BX0, BZ1 - BZ0, height, 3.3f, FloorH), "UpperFacade");
            Material facadeMat = null;
            Transform self = houseRoot.Find("OutdoorWorld/SelfBuildingFacade");
            if (self != null && self.GetComponent<Renderer>() != null) facadeMat = self.GetComponent<Renderer>().sharedMaterial;
            facade.GetComponent<MeshRenderer>().sharedMaterial = facadeMat != null ? facadeMat : outerMat;
            var col = facade.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.size = new Vector3(BX1 - BX0, height, BZ1 - BZ0);

            // 옥상 슬래브 + 난간 (가장자리 1.1m)
            Transform roof = Group("Roof", g);
            float ry = RoofY;
            Solid(roof, "RoofSlab", new Vector3(BX0, ry - 0.3f, BZ0), new Vector3(BX1, ry, BZ1), roofMat);
            const float pt = 0.2f, ph = 1.1f;
            Solid(roof, "Parapet_S", new Vector3(BX0, ry, BZ0), new Vector3(BX1, ry + ph, BZ0 + pt), parapetMat);
            Solid(roof, "Parapet_N", new Vector3(BX0, ry, BZ1 - pt), new Vector3(BX1, ry + ph, BZ1), parapetMat);
            Solid(roof, "Parapet_E", new Vector3(BX1 - pt, ry, BZ0), new Vector3(BX1, ry + ph, BZ1), parapetMat);
            // 서쪽: 계단탑(z -3.33 ~ 0.13)이 붙은 곳은 비운다
            Solid(roof, "Parapet_W_S", new Vector3(BX0, ry, BZ0), new Vector3(BX0 + pt, ry + ph, Z0 - WallT), parapetMat);
            Solid(roof, "Parapet_W_N", new Vector3(BX0, ry, Z1 + WallT), new Vector3(BX0 + pt, ry + ph, BZ1), parapetMat);
            // 옥상 소품: 물탱크, 환기구, 실외기, 안테나, 헬기 표시
            Material tank = OM.Lit("Roof_Tank", OM.Hex("D7DADF"), 0.4f, 0.2f);
            Solid(roof, "WaterTank", new Vector3(4f, ry, 5.5f), new Vector3(8f, ry + 2.4f, 8.6f), tank);
            Solid(roof, "Vent_A", new Vector3(-6f, ry, 6.6f), new Vector3(-5f, ry + 0.9f, 7.6f), darkMat);
            Solid(roof, "Vent_B", new Vector3(-2f, ry, -2.5f), new Vector3(-1.2f, ry + 0.8f, -1.7f), darkMat);
            Solid(roof, "Outdoor_Units", new Vector3(-12f, ry, 6.6f), new Vector3(-9f, ry + 1.0f, 7.8f), tank);
            Prim.Cyl(roof, "Antenna", new Vector3(7.5f, ry + 3f, -2.6f), 0.05f, 6f, railMat);
            Visual(roof, "Helipad_H", new Vector3(-6f, ry + 0.01f, 1.8f), new Vector3(4.4f, 0.01f, 0.5f), OM.Lit("Roof_Paint", OM.Hex("E5C04A"), 0.2f));
            Visual(roof, "Helipad_H_L", new Vector3(-7.95f, ry + 0.01f, 1.8f), new Vector3(0.5f, 0.01f, 3.6f), OM.Lit("Roof_Paint", OM.Hex("E5C04A"), 0.2f));
            Visual(roof, "Helipad_H_R", new Vector3(-4.05f, ry + 0.01f, 1.8f), new Vector3(0.5f, 0.01f, 3.6f), OM.Lit("Roof_Paint", OM.Hex("E5C04A"), 0.2f));
        }

        // ═════════════════════════ 지면 / 건물 콜라이더 ═════════════════════════
        private static void BuildGroundAndColliders(Transform p, Transform houseRoot)
        {
            Transform g = Group("Colliders", p);
            // 지면 (창밖 풍경의 잔디·보도 높이). 넓게 깔아 건물 밖 어디서든 걸을 수 있게 한다
            Collider(g, "Ground", new Vector3(-400f, GroundY - 1f, -400f), new Vector3(400f, GroundY, 400f));
            // 우리 동 아래층 (외벽 메시는 콜라이더가 없다)
            Collider(g, "SelfBuilding_Lower", new Vector3(BX0, GroundY, BZ0), new Vector3(BX1, -0.3f, BZ1));
            // 출구 앞 계단참(외부 포치) — 시각용 바닥 패치
            Visual(p, "ExitPorch", new Vector3(X0 - WallT - 1.5f, GroundY + 0.01f, -1.6f), new Vector3(3f, 0.02f, 3.4f), concrete);
            Visual(p, "ExitCanopy", new Vector3(X0 - WallT - 1f, GroundY + 2.5f, -1.6f), new Vector3(2f, 0.12f, 2.6f), roofMat);

            // 창밖 아파트 동·저층 건물 (렌더러 경계로 상자 콜라이더)
            int n = 0;
            foreach (string grp in new[] { "OutdoorWorld/Towers", "OutdoorWorld/LowBuildings" })
            {
                Transform t = houseRoot.Find(grp);
                if (t == null) continue;
                foreach (Transform b in t)
                {
                    Renderer[] rs = b.GetComponentsInChildren<Renderer>(true);
                    if (rs.Length == 0) continue;
                    Bounds bb = rs[0].bounds;
                    foreach (Renderer r in rs) if (r.gameObject.name == "Body" || r is MeshRenderer) bb.Encapsulate(r.bounds);
                    if (bb.size.y < 4f || bb.min.y > GroundY + 2f) continue;
                    Collider(g, "Bldg_" + b.name, new Vector3(bb.min.x, GroundY, bb.min.z), new Vector3(bb.max.x, Mathf.Min(bb.max.y, GroundY + 120f), bb.max.z));
                    n++;
                }
            }
            Debug.Log($"[Stairwell] 주변 건물 콜라이더 {n}개");
        }

        // ═════════════════════════ 조명 ═════════════════════════
        private static void AddLamp(Transform parent, Vector3 pos)
        {
            GameObject lamp = Visual(parent, "Lamp", pos, new Vector3(0.36f, 0.05f, 0.36f), lampMat);
            lamps.Add(lamp.GetComponent<Renderer>());
            var lg = new GameObject("Light");
            lg.transform.SetParent(parent, false);
            lg.transform.localPosition = pos + Vector3.down * 0.12f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 5.5f; l.intensity = 1.1f; l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            lights.Add(l);
        }

        // ═════════════════════════ 메시 ═════════════════════════
        /// <summary>계단 10단 (+x 방향으로 오름, 원점 = 아래쪽 끝 바닥, 폭 1 — 스케일로 맞춘다)</summary>
        private static Mesh EnsureFlightMesh()
        {
            var parts = new List<CombineInstance>();
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (int i = 0; i < Steps; i++)
            {
                float x0 = i * RunStep, x1 = x0 + RunStep;
                float top = (i + 1) * RiseStep, bottom = i * RiseStep - 0.22f;
                var m = Matrix4x4.TRS(new Vector3((x0 + x1) * 0.5f, (top + bottom) * 0.5f, 0f), Quaternion.identity, new Vector3(RunStep, top - bottom, 1f));
                parts.Add(new CombineInstance { mesh = cube, transform = m });
            }
            var mesh = new Mesh { name = "StairFlight10" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return SaveMesh(mesh, "StairFlight10");
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            InteriorMaterialFactory.EnsureFolder(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ═════════════════════════ 재질 ═════════════════════════
        private static void Materials(Transform houseRoot)
        {
            concrete = OM.Lit("Stair_Concrete", OM.Hex("9A9B9E"), 0.08f);
            wallMat = OM.Lit("Stair_Wall", OM.Hex("D7D3CA"), 0.06f);
            stepMat = OM.Lit("Stair_Steps", OM.Hex("A6A7A9"), 0.12f);
            railMat = OM.Lit("Stair_Rail", OM.Hex("B9BDC3"), 0.6f, 0.7f);
            darkMat = OM.Lit("Stair_Dark", OM.Hex("2F3236"), 0.35f, 0.3f);
            doorMat = OM.Lit("Stair_FireDoor", OM.Hex("6B7480"), 0.4f, 0.4f);
            roofMat = OM.Lit("Stair_Roof", OM.Hex("7E8185"), 0.1f);
            parapetMat = OM.Lit("Stair_Parapet", OM.Hex("C9C2B4"), 0.08f);
            outerMat = OM.Lit("Stair_Outer", OM.Hex("D9D0BE"), 0.08f);
            signMat = Emissive("Stair_ExitSign", OM.Hex("1FAA59"), OM.Hex("1FAA59") * 2f);
            lampMat = Emissive("Stair_Lamp", OM.Hex("F4F1E8"), new Color(1f, 0.95f, 0.85f) * 1.6f);
        }

        private static Material Emissive(string name, Color color, Color emission)
        {
            Material m = OM.Lit(name, color, 0.3f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ═════════════════════════ 헬퍼 ═════════════════════════
        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>min~max 상자 (렌더러 + 콜라이더)</summary>
        private static GameObject Solid(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
        {
            GameObject go = Visual(parent, name, (min + max) * 0.5f, max - min, mat);
            go.AddComponent<BoxCollider>();
            return go;
        }

        /// <summary>중심·크기 상자 (렌더러만)</summary>
        private static GameObject Visual(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
            => Prim.Box(parent, name, Local(parent, center), Abs(size), mat);

        /// <summary>보이지 않는 상자 콜라이더</summary>
        private static void Collider(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Local(parent, (min + max) * 0.5f);
            go.AddComponent<BoxCollider>().size = Abs(max - min);
        }

        // 그룹은 모두 원점·무회전이라 월드 = 로컬 (집 루트가 원점이 아니어도 houseRoot 기준 좌표가 된다)
        private static Vector3 Local(Transform parent, Vector3 houseLocal) => houseLocal - LocalOffset(parent);

        private static Vector3 LocalOffset(Transform t)
        {
            Vector3 o = Vector3.zero;
            while (t != null && t != root) { o += t.localPosition; t = t.parent; }
            return o;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void Label(Transform parent, string name, string text, Vector3 pos, float yaw, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Local(parent, pos);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size * 10f;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        }

        private static void SetArray(SerializedProperty p, Object[] items)
        {
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
