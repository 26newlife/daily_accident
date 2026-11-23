using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Prim = GameUI.EditorTools.Interior.Prim;
using Object = UnityEngine.Object;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools.Outdoor
{
    /// <summary>
    /// 창밖 풍경(배산임수 아파트 단지) 절차적 생성기. Tools > GameUI > Build Outdoor World.
    /// 좌표계: 1004호 10층 바닥 y=0, 베란다 방향(+z)이 단지 정면(남향), -z 가 건물 뒤(북쪽, 배산), +x 가 옆 동 방향.
    /// 3겹 구조: 근경(0~80m: 지면/조경/보행자/차량/새) → 중경(80~400m: 아파트 동 14개 + 낮은 건물) → 원경(산/하천/하늘/구름).
    /// 실행할 때마다 OutdoorWorld 루트를 지우고 다시 만든다 (중복 생성 없음). 모든 지오메트리는 프리미티브/절차적 텍스처다.
    /// </summary>
    public static partial class OutdoorWorldBuilder
    {
        public const string RootName = "OutdoorWorld";
        // ───────── 치수 상수 (m) ─────────
        public const float FloorHeight = 3f;                 // 층고
        public const float GroundY = -28.5f;                 // 10층 바닥(y=0) 기준 1층 지면 (약 28.5m 아래 = 높이감)
        public const float BayWidth = 3.3f;                  // 외벽 텍스처 한 칸 폭
        private static readonly Color HazeColor = new Color(0.72f, 0.80f, 0.90f);   // 먼 곳일수록 이 색으로 (대기 원근)
        private static readonly string[] TowerTints = { "F2E6D0", "D6E2EE", "EBD5CC", "E6E6E6" };

        private static Transform root, nearField;
        private static System.Random rng;
        private static GameObject prefabTreeRound, prefabTreePine, prefabLamp, prefabBench, prefabCar, prefabPed, prefabDog, prefabBird;
        private static Texture2D facadeA, facadeB;
        private static readonly List<Transform> residentsAnchor = new List<Transform>();
        private static Transform frontTower;
        private static float frontTowerDepth;

        // ═════════════════════════ 진입점 ═════════════════════════
        [MenuItem("Tools/GameUI/Build Outdoor World")]
        public static void BuildMenu()
        {
            GameObject gp = GameObject.Find("Gameplay");
            Build(gp != null ? gp.transform : null);
            Scene();
            Debug.Log("[Outdoor] 창밖 풍경 생성 완료. 씬을 저장하세요 (Ctrl+S).");
        }

        private static void Scene()
        {
            var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (sc.IsValid()) EditorSceneManager.MarkSceneDirty(sc);
        }

        /// <summary>parent(보통 Gameplay) 아래에 OutdoorWorld 를 새로 만든다. parent 가 null 이면 씬 루트</summary>
        public static void Build(Transform parent)
        {
            OM.ResetCache();
            rng = new System.Random(1004);

            // 이전 결과 정리 (어디에 있든 OutdoorWorld 이름의 루트를 모두 지운다)
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == RootName) Object.DestroyImmediate(go);

            root = new GameObject(RootName).transform;
            if (parent != null) root.SetParent(parent, false);
            nearField = Group("NearField", root);

            prefabTreeRound = OutdoorPrefabs.TreeRound(); prefabTreePine = OutdoorPrefabs.TreePine();
            prefabLamp = OutdoorPrefabs.LampPost(); prefabBench = OutdoorPrefabs.Bench();
            prefabCar = OutdoorPrefabs.Car(); prefabPed = OutdoorPrefabs.Pedestrian();
            prefabDog = OutdoorPrefabs.Dog(); prefabBird = OutdoorPrefabs.Bird();
            facadeA = OM.SaveTexture("facade_a", BuildingFacadeGenerator.CreateFacadeTexture(11));
            facadeB = OM.SaveTexture("facade_b", BuildingFacadeGenerator.CreateFacadeTexture(27, litRatio: 0.1f));

            BuildSky();
            BuildGround();
            BuildNearField();
            BuildSelfFacade();
            BuildTowers();
            BuildLowBuildings();
            BuildMountains();
            BuildRiver();
            BuildClouds();
            BuildPedestrians();
            BuildVehicles();
            BuildBirds();
            Transform fireRoot = BuildFireEffect();
            BuildController(fireRoot);
            BuildWindowSignal();

            ApplyShadowAndStatic();
            Debug.Log($"[Outdoor] 렌더러 {root.GetComponentsInChildren<Renderer>(true).Length}개 생성 (정적 배칭 대상 포함)");
        }

        // ═════════════════════════ 공통 헬퍼 ═════════════════════════
        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        private static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>수평 사각형 지면 (y 고정, 단색 재질)</summary>
        private static void Flat(Transform parent, string name, float x0, float x1, float z0, float z1, float y, Material mat)
            => Prim.Quad(parent, name, new Vector3(x0, y, z0), Vector3.right, x1 - x0, Vector3.forward, z1 - z0, Vector3.up, mat, 1f);

        private static GameObject Spawn(GameObject prefab, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static float DistanceFromOrigin(float x, float z) => Mathf.Sqrt(x * x + z * z);

        // ═════════════════════════ 하늘 ═════════════════════════
        private static void BuildSky()
        {
            Shader sh = Shader.Find("Skybox/Procedural");
            if (sh == null) { Debug.LogWarning("[Outdoor] Skybox/Procedural 셰이더를 찾지 못했습니다."); return; }
            GameUI.EditorTools.Interior.InteriorMaterialFactory.EnsureFolder(OM.Dir);
            string path = OM.Dir + "/Out_Sky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(sh) { name = "Out_Sky" }; AssetDatabase.CreateAsset(mat, path); }
            // 아침: 낮은 해, 맑고 옅은 하늘, 지평선 쪽은 뿌옇게
            mat.SetFloat("_SunSize", 0.05f); mat.SetFloat("_SunSizeConvergence", 4f);
            mat.SetFloat("_AtmosphereThickness", 1.15f); mat.SetFloat("_Exposure", 1.25f);
            mat.SetColor("_SkyTint", new Color(0.46f, 0.58f, 0.78f)); mat.SetColor("_GroundColor", new Color(0.62f, 0.66f, 0.62f));
            EditorUtility.SetDirty(mat);
            RenderSettings.skybox = mat;
        }

        // ═════════════════════════ 지면 / 도로 ═════════════════════════
        private static void BuildGround()
        {
            Transform g = Group("Ground", root);
            Material grassFar = OM.Lit("GrassFar", OM.Hex("4B7A3F"), 0.05f), grass = OM.Lit("Grass", OM.Hex("5E9A47"), 0.05f);
            Material walk = OM.Lit("Sidewalk", OM.Hex("C2BFB6"), 0.2f), asphalt = OM.Lit("Asphalt", OM.Hex("3A3B3E"), 0.15f);
            Material line = OM.Lit("RoadLine", OM.Hex("E8E1C0"), 0.2f), lot = OM.Lit("ParkingLot", OM.Hex("4A4B4F"), 0.15f);
            Material sand = OM.Lit("Sand", OM.Hex("C9B58A"), 0.05f), park = OM.Lit("ParkGrass", OM.Hex("6FA854"), 0.05f);
            float y = GroundY;

            Flat(g, "GroundFar", -1500f, 1500f, -650f, 300f, y - 0.06f, grassFar);
            Flat(g, "LawnNear", -220f, 220f, 9.4f, 250f, y, grass);
            Flat(g, "Sidewalk_S1", -220f, 220f, 12f, 16f, y + 0.03f, walk);
            Flat(g, "Sidewalk_S2", -220f, 220f, 24f, 28f, y + 0.03f, walk);
            Flat(g, "Connector_W", -62f, -58f, 12f, 28f, y + 0.03f, walk);
            Flat(g, "Connector_E", 58f, 62f, 12f, 28f, y + 0.03f, walk);
            Flat(g, "Road_A", -160f, 160f, 30f, 38f, y + 0.04f, asphalt);
            Flat(g, "Road_A_Line", -160f, 160f, 33.9f, 34.1f, y + 0.05f, line);
            Flat(g, "Sidewalk_S3", -160f, 160f, 38f, 42f, y + 0.03f, walk);
            Flat(g, "Road_B", 120f, 128f, 38f, 250f, y + 0.04f, asphalt);
            Flat(g, "Road_C", -160f, 128f, 236f, 244f, y + 0.04f, asphalt);
            Flat(g, "Avenue", -1200f, 1200f, 250f, 264f, y + 0.04f, asphalt);
            Flat(g, "Avenue_Line", -1200f, 1200f, 256.9f, 257.1f, y + 0.05f, line);
            for (int i = 0; i < 8; i++) Flat(g, "Crosswalk", -9f + i * 2.4f, -8f + i * 2.4f, 250.5f, 263.5f, y + 0.05f, line);
            Flat(g, "ParkingLot", 20f, 112f, 42f, 64f, y + 0.04f, lot);
            Flat(g, "Playground_Sand", -92f, -48f, 44f, 68f, y + 0.04f, sand);
            Flat(g, "RiversidePark", -1200f, 1200f, 264f, 300f, y + 0.01f, park);
            Flat(g, "RiversidePath", -1200f, 1200f, 280f, 283f, y + 0.03f, walk);
        }

        // ═════════════════════════ 근경 (단지 조경/시설) ═════════════════════════
        private static void BuildNearField()
        {
            Transform t = nearField;
            // 가로수: 보도 양쪽에 일렬 + 잔디밭에 흩어서. 창에 가까운(|x|<70, z<45) 나무는 바람에 흔들린다
            for (float x = -200f; x <= 200f; x += 13f)
            {
                PlaceTree(t, x + Rand(-2f, 2f), 18.5f + Rand(-0.5f, 0.5f));
                PlaceTree(t, x + 6f + Rand(-2f, 2f), 29f + Rand(-0.5f, 0.5f));
            }
            for (int i = 0; i < 40; i++) PlaceTree(t, Rand(-140f, 140f), Rand(44f, 230f), true);

            // 가로등
            for (float x = -192f; x <= 192f; x += 24f) { Spawn(prefabLamp, t, new Vector3(x, GroundY, 15.2f), 0f); Spawn(prefabLamp, t, new Vector3(x + 12f, GroundY, 38.8f), 180f); }

            // 벤치 (보행자 앉는 자리)
            foreach (float x in new[] { -40f, 0f, 40f }) Spawn(prefabBench, t, new Vector3(x, GroundY, 20f), 180f);

            // 화단 / 산울타리 (건물 앞)
            Material hedge = OM.Lit("Hedge", OM.Hex("2F6B35"), 0.1f);
            for (float x = -30f; x < 40f; x += 5.5f) Prim.Box(t, "Hedge", new Vector3(x, GroundY + 0.35f, 10.8f), new Vector3(4.6f, 0.7f, 1.2f), hedge);

            // 자전거 거치대 (U자 5개)
            Material rack = OM.Lit("BikeRack", OM.Hex("5A5F66"), 0.4f, 0.5f);
            for (int i = 0; i < 5; i++) Prim.Box(t, "BikeRack", new Vector3(-24f + i * 0.9f, GroundY + 0.45f, 20.5f), new Vector3(0.06f, 0.9f, 0.7f), rack);

            // 분리수거장 (지붕 + 색색의 수거함)
            Material shed = OM.Lit("RecycleShed", OM.Hex("8A8F94"), 0.3f);
            Prim.Box(t, "RecycleRoof", new Vector3(-120f, GroundY + 2.4f, 18f), new Vector3(9f, 0.15f, 3.5f), shed);
            for (int i = 0; i < 4; i++) { Prim.Box(t, "RecycleBin", new Vector3(-123f + i * 2.1f, GroundY + 0.6f, 18f), new Vector3(1.6f, 1.2f, 1.2f), OM.Lit("Bin" + i, OM.Hex(new[] { "2E7D32", "1565C0", "F9A825", "6D4C41" }[i]), 0.3f)); }
            foreach (float x in new[] { -124.5f, -115.5f }) Prim.Cyl(t, "RecyclePost", new Vector3(x, GroundY + 1.2f, 16.4f), 0.08f, 2.4f, shed);

            // 놀이터: 미끄럼틀, 그네, 모래놀이 테두리
            Material play = OM.Lit("PlayRed", OM.Hex("D84315"), 0.3f), playBlue = OM.Lit("PlayBlue", OM.Hex("1E88E5"), 0.3f);
            Prim.Box(t, "SlideTower", new Vector3(-80f, GroundY + 1.0f, 52f), new Vector3(1.4f, 2.0f, 1.4f), play);
            Prim.Box(t, "SlideRamp", new Vector3(-80f, GroundY + 1.0f, 54.2f), new Vector3(0.7f, 0.08f, 3.2f), playBlue, new Vector3(35f, 0f, 0f));
            foreach (float x in new[] { -66f, -61f })
                Prim.Cyl(t, "SwingPost", new Vector3(x, GroundY + 1.2f, 58f), 0.07f, 2.4f, play);
            Prim.Box(t, "SwingBeam", new Vector3(-63.5f, GroundY + 2.4f, 58f), new Vector3(5.4f, 0.12f, 0.12f), play);
            for (int i = 0; i < 2; i++) Prim.Box(t, "SwingSeat", new Vector3(-64.5f + i * 2f, GroundY + 0.6f, 58f), new Vector3(0.5f, 0.05f, 0.3f), playBlue);

            // 경비실 + 깃발 + 단지 입구 차단기
            Material gh = OM.Lit("GuardHouse", OM.Hex("D9D3C4"), 0.2f), roofRed = OM.Lit("GuardRoof", OM.Hex("8D3B2E"), 0.2f);
            Prim.Box(t, "GuardHouse", new Vector3(140f, GroundY + 1.4f, 26f), new Vector3(3f, 2.8f, 3f), gh);
            Prim.Box(t, "GuardRoof", new Vector3(140f, GroundY + 2.95f, 26f), new Vector3(3.6f, 0.3f, 3.6f), roofRed);
            Prim.Box(t, "GuardWindow", new Vector3(140f, GroundY + 1.6f, 24.45f), new Vector3(2.0f, 1.0f, 0.05f), OM.Lit("GuardGlass", OM.Hex("5C7C96"), 0.9f, 0.1f));
            Prim.Cyl(t, "FlagPole", new Vector3(146f, GroundY + 4.5f, 26f), 0.05f, 9f, OM.Lit("Pole", OM.Hex("B0B4B8"), 0.6f, 0.6f));
            GameObject flag = Prim.Box(t, "Flag", new Vector3(147f, GroundY + 8.2f, 26f), new Vector3(2f, 1.3f, 0.03f), OM.Lit("Flag", OM.Hex("D32F2F"), 0.1f));
            flag.AddComponent<FlagWave>();
            Prim.Box(t, "GateBarrierPost", new Vector3(150f, GroundY + 0.6f, 29f), new Vector3(0.4f, 1.2f, 0.4f), OM.Lit("BarrierPost", OM.Hex("F5F5F5"), 0.3f));
            Prim.Box(t, "GateBarrierArm", new Vector3(150f, GroundY + 1.15f, 33.5f), new Vector3(0.12f, 0.12f, 8.4f), OM.Lit("BarrierArm", OM.Hex("E53935"), 0.3f));
        }

        private static void PlaceTree(Transform parent, float x, float z, bool allowPine = false)
        {
            bool pine = allowPine && rng.NextDouble() < 0.35;
            GameObject tree = Spawn(pine ? prefabTreePine : prefabTreeRound, parent, new Vector3(x, GroundY, z), Rand(0f, 360f), Rand(0.8f, 1.25f));
            if (Mathf.Abs(x) < 70f && z < 45f) tree.AddComponent<TreeSway>();
        }

        // ═════════════════════════ 우리 동 외벽 (아래층 8층 포함) ═════════════════════════
        private static void BuildSelfFacade()
        {
            // 1004호가 있는 동: 10층 바닥(y=-0.3) 아래로 지면까지의 외벽. 베란다 아래를 내려다보면 층별 창/발코니가 보인다.
            float x0 = -18f, x1 = 10f, z0 = -4f, z1 = 9.4f, top = -0.3f;
            var go = new GameObject("SelfBuildingFacade");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, GroundY, (z0 + z1) * 0.5f);
            Mesh mesh = BuildingFacadeGenerator.CreateTowerMesh("SelfFacade", x1 - x0, z1 - z0, top - GroundY, BayWidth, FloorHeight);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = OM.Lit("SelfFacade", OM.Hex("EADFCB"), 0.1f, 0f, facadeA);
        }

        // ═════════════════════════ 중경: 아파트 동 14개 ═════════════════════════
        private struct TowerDef
        {
            public string label; public float x, z, w, d; public int floors; public float yaw; public int tint; public bool front;
            public TowerDef(string l, float x, float z, float w, float d, int floors, float yaw, int tint, bool front = false)
            { label = l; this.x = x; this.z = z; this.w = w; this.d = d; this.floors = floors; this.yaw = yaw; this.tint = tint; this.front = front; }
        }

        private static readonly TowerDef[] Towers =
        {
            // 맞은편 근접 줄 (거리 70~110m)
            new TowerDef("101동", -70f, 92f, 46f, 16f, 18, 6f, 0),
            new TowerDef("102동", 6f, 104f, 54f, 15f, 22, -4f, 1, true),    // 정면 (주민 반응 연출 대상)
            new TowerDef("103동", 78f, 86f, 44f, 16f, 17, 4f, 2),
            new TowerDef("104동", 150f, 108f, 50f, 16f, 20, -8f, 0),
            // 안쪽 줄 (거리 170~220m)
            new TowerDef("105동", -150f, 172f, 48f, 16f, 19, 10f, 3),
            new TowerDef("106동", -60f, 186f, 52f, 15f, 24, -4f, 1),
            new TowerDef("107동", 30f, 180f, 46f, 16f, 16, 7f, 2),
            new TowerDef("108동", 112f, 196f, 50f, 15f, 21, -6f, 3),
            // 옆 단지 (주방 창=동쪽에서 보이는 측면 동)
            new TowerDef("109동", 92f, 4f, 16f, 48f, 18, 0f, 0),
            new TowerDef("110동", 118f, -72f, 16f, 50f, 22, 0f, 2),
            // 건물 뒤(북쪽)
            new TowerDef("111동", -30f, -150f, 56f, 16f, 20, 180f, 1),
            new TowerDef("112동", 80f, -190f, 50f, 16f, 25, 176f, 3),
            // 하천 건너 먼 단지 (원경, 안개 낀 느낌)
            new TowerDef("201동", -80f, 540f, 54f, 18f, 26, 0f, 1),
            new TowerDef("202동", 120f, 560f, 52f, 18f, 23, 0f, 0),
        };

        private static void BuildTowers()
        {
            Transform t = Group("Towers", root);
            Material roofMat = OM.Lit("Roof", OM.Hex("8C8F94"), 0.2f);
            int idx = 0;
            foreach (TowerDef d in Towers)
            {
                float height = d.floors * FloorHeight, dist = DistanceFromOrigin(d.x, d.z);
                Color tint = BuildingFacadeGenerator.Atmosphere(OM.Hex(TowerTints[d.tint]), HazeColor, dist);
                Material m = OM.Lit("Tower_" + d.label, tint, 0.1f + Mathf.Clamp01(dist / 900f) * 0.0f, 0f, idx % 2 == 0 ? facadeA : facadeB);

                var tr = new GameObject(d.label).transform;
                tr.SetParent(t, false);
                tr.position = new Vector3(d.x, GroundY, d.z); tr.rotation = Quaternion.Euler(0f, d.yaw, 0f);
                var body = new GameObject("Body");
                body.transform.SetParent(tr, false);
                body.AddComponent<MeshFilter>().sharedMesh = BuildingFacadeGenerator.CreateTowerMesh(d.label, d.w, d.d, height, BayWidth, FloorHeight);
                body.AddComponent<MeshRenderer>().sharedMaterial = m;

                Prim.Box(tr, "Roof", new Vector3(0, height + 0.3f, 0), new Vector3(d.w + 0.6f, 0.6f, d.d + 0.6f), roofMat);
                Prim.Box(tr, "RoofHouse", new Vector3(-d.w * 0.2f, height + 1.8f, 0), new Vector3(d.w * 0.22f, 2.4f, d.d * 0.5f), roofMat);       // 옥탑
                Prim.Cyl(tr, "LightningRod", new Vector3(d.w * 0.3f, height + 3.5f, 0), 0.05f, 5f, OM.Lit("Rod", OM.Hex("B0B4B8"), 0.6f, 0.6f));

                // 동 번호판 (정면 = -z 면). 큰 글자라 멀리서도 읽힌다
                if (d.z > 0f) AddTowerLabel(tr, d.label, new Vector3(0f, height - 4.5f, -d.d * 0.5f - 0.15f), dist);   // Scene 뷰 전용 DebugLabel
                if (TowerNumberOnFacade) PaintTowerNumber(m, d.label);

                if (d.front) { frontTower = tr; frontTowerDepth = d.d; }
                idx++;
            }
        }

        /// <summary>
        /// 동 번호 표시 훅. 게임 화면에는 동 번호를 띄우지 않는다 (공중에 떠 있는 TextMesh 는 제거).
        /// Scene 뷰에서만 DebugLabel 로 보이며, 나중에 외벽 텍스처에 번호를 그리고 싶으면 TowerNumberOnFacade 를 켜고 PaintTowerNumber 를 구현한다.
        /// </summary>
        private const bool TowerNumberOnFacade = false;

        private static void AddTowerLabel(Transform tower, string text, Vector3 localPos, float dist)
        {
            var go = new GameObject("DebugLabel_" + text);
            go.transform.SetParent(tower, false);
            go.transform.localPosition = localPos;
            go.AddComponent<DebugLabel>().Set(text, new Color(1f, 0.92f, 0.3f));
        }

        /// <summary>외벽 텍스처에 동 번호를 그리는 자리 (현재 미사용). 구현 시 재질의 베이스 텍스처에 번호 스텐실을 합성한다</summary>
        private static void PaintTowerNumber(Material facadeMaterial, string label) { }

        private static void BuildLowBuildings()
        {
            Transform t = Group("LowBuildings", root);
            (string name, float x, float z, float w, float d, int floors, string tint)[] defs =
            {
                ("어린이집", -40f, 214f, 24f, 14f, 3, "F4D58D"),
                ("상가", 62f, 222f, 40f, 16f, 4, "D9C7B0"),
                ("학교", -150f, 232f, 70f, 22f, 5, "E8DCC8"),
                ("주민센터", 150f, 150f, 30f, 18f, 3, "D4D9DE"),
            };
            Material roofMat = OM.Lit("RoofLow", OM.Hex("9A6E5B"), 0.2f);
            foreach (var d in defs)
            {
                float dist = DistanceFromOrigin(d.x, d.z);
                Material m = OM.Lit("Low_" + d.name, BuildingFacadeGenerator.Atmosphere(OM.Hex(d.tint), HazeColor, dist), 0.1f, 0f, facadeB);
                var tr = new GameObject(d.name).transform;
                tr.SetParent(t, false); tr.position = new Vector3(d.x, GroundY, d.z);
                var body = new GameObject("Body"); body.transform.SetParent(tr, false);
                body.AddComponent<MeshFilter>().sharedMesh = BuildingFacadeGenerator.CreateTowerMesh(d.name, d.w, d.d, d.floors * FloorHeight, BayWidth, FloorHeight);
                body.AddComponent<MeshRenderer>().sharedMaterial = m;
                Prim.Box(tr, "Roof", new Vector3(0, d.floors * FloorHeight + 0.25f, 0), new Vector3(d.w + 0.8f, 0.5f, d.d + 0.8f), roofMat);
            }
            // 버스정류장 + 신호등 (사거리: 단지 도로와 대로)
            Material shelter = OM.Lit("BusShelter", OM.Hex("5F8FA8"), 0.7f, 0.2f), pole = OM.Lit("Pole", OM.Hex("B0B4B8"), 0.6f, 0.6f);
            Prim.Box(t, "BusStopRoof", new Vector3(40f, GroundY + 2.6f, 266f), new Vector3(4f, 0.12f, 1.6f), shelter);
            foreach (float x in new[] { 38.2f, 41.8f }) Prim.Box(t, "BusStopPost", new Vector3(x, GroundY + 1.3f, 266f), new Vector3(0.08f, 2.6f, 0.08f), pole);
            foreach (float x in new[] { -14f, 14f })
            {
                Prim.Cyl(t, "TrafficPole", new Vector3(x, GroundY + 2.6f, 248f), 0.07f, 5.2f, pole);
                Prim.Box(t, "TrafficLight", new Vector3(x, GroundY + 5.1f, 248f), new Vector3(0.35f, 0.9f, 0.3f), OM.Lit("TrafficBox", OM.Hex("2A2D32"), 0.4f));
                Prim.Sph(t, "TrafficGreen", new Vector3(x, GroundY + 4.8f, 247.8f), Vector3.one * 0.22f, OM.Lit("TLGreen", OM.Hex("43A047"), 0.6f));
            }
        }
    }
}
