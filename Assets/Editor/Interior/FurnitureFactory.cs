using System;
using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools.Interior
{
    /// <summary>Unity 기본 프리미티브 생성 헬퍼 (콜라이더는 기본 제거 — 필요한 것만 따로 붙인다)</summary>
    public static class Prim
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Vector3 euler = default)
            => Make(PrimitiveType.Cube, parent, name, center, size, mat, euler);

        /// <summary>세로(Y)축 원기둥. radius/height 는 m 단위</summary>
        public static GameObject Cyl(Transform parent, string name, Vector3 center, float radius, float height, Material mat, Vector3 euler = default)
            => Make(PrimitiveType.Cylinder, parent, name, center, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, euler);

        public static GameObject Sph(Transform parent, string name, Vector3 center, Vector3 diameters, Material mat)
            => Make(PrimitiveType.Sphere, parent, name, center, diameters, mat, default);

        private static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 scale, Material mat, Vector3 euler)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>임의 방향 쿼드 (UV = 미터 / tileMeters, 타일 텍스처용). 법선이 normalHint 쪽을 향한다.</summary>
        public static GameObject Quad(Transform parent, string name, Vector3 origin, Vector3 uDir, float width, Vector3 vDir, float height,
            Vector3 normalHint, Material mat, float tileMeters)
        {
            var mesh = new Mesh { name = name };
            Vector3 u = uDir.normalized * width, v = vDir.normalized * height;
            mesh.vertices = new[] { origin, origin + u, origin + u + v, origin + v };
            mesh.uv = new[]
            {
                Vector2.zero, new Vector2(width / tileMeters, 0f),
                new Vector2(width / tileMeters, height / tileMeters), new Vector2(0f, height / tileMeters)
            };
            // 유니티는 삼각형 (a,b,c) 의 앞면 법선이 cross(b-a, c-a) 방향이다. 힌트와 같은 쪽을 보도록 감김을 고른다.
            bool faceHint = Vector3.Dot(Vector3.Cross(u, v), normalHint) >= 0f;
            mesh.triangles = faceHint ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }

    /// <summary>
    /// 가구/소품 프리팹 팩토리. 프리미티브 조합으로 만든 가구를 Assets/Prefabs/Furniture/ 에 프리팹으로 저장한다.
    /// 규칙: 피벗 = 바닥 중앙, 로컬 +Z = 앞면(사용자가 접근하는 쪽), 단위 m.
    /// 나중에 실제 모델로 교체할 때는 같은 이름의 프리팹을 덮어쓰면 된다 (배치 코드는 이름으로 불러온다).
    /// </summary>
    public static class FurnitureFactory
    {
        public const string PrefabDir = "Assets/Prefabs/Furniture";

        /// <summary>프리팹 한 개를 만드는 도우미: 자식 프리미티브 + 선택적 BoxCollider 목록</summary>
        public sealed class FB
        {
            public readonly GameObject Root;
            public readonly List<(Vector3 center, Vector3 size)> Colliders = new List<(Vector3, Vector3)>();
            public FB(string name) { Root = new GameObject(name); }

            public GameObject Box(string n, float x, float y, float z, float w, float h, float d, Material m, Vector3 euler = default)
                => Prim.Box(Root.transform, n, new Vector3(x, y, z), new Vector3(w, h, d), m, euler);
            public GameObject Cyl(string n, float x, float y, float z, float r, float h, Material m, Vector3 euler = default)
                => Prim.Cyl(Root.transform, n, new Vector3(x, y, z), r, h, m, euler);
            public GameObject Sph(string n, float x, float y, float z, float dx, float dy, float dz, Material m)
                => Prim.Sph(Root.transform, n, new Vector3(x, y, z), new Vector3(dx, dy, dz), m);
            /// <summary>단순 BoxCollider 추가 (모서리는 1cm 안쪽으로 줄여 끼임 방지)</summary>
            public void Col(float x, float y, float z, float w, float h, float d)
                => Colliders.Add((new Vector3(x, y, z), new Vector3(w, h, d)));
        }

        private sealed class Recipe
        {
            public Action<FB> build;
            public bool autoCollider;   // Col() 호출이 없으면 전체 바운즈로 BoxCollider 1개
            public bool flammable;
        }

        private static readonly Dictionary<string, Recipe> recipes = new Dictionary<string, Recipe>();
        private static readonly Dictionary<string, GameObject> built = new Dictionary<string, GameObject>();
        private const float ColliderShrink = 0.01f;

        public static void ResetCache() => built.Clear();

        private static void R(string name, bool autoCollider, bool flammable, Action<FB> build)
            => recipes[name] = new Recipe { build = build, autoCollider = autoCollider, flammable = flammable };

        // ═════════════════════════ 프리팹 로드/생성 ═════════════════════════
        public static GameObject GetPrefab(string name)
        {
            if (built.TryGetValue(name, out GameObject cached) && cached != null) return cached;
            if (!recipes.TryGetValue(name, out Recipe r)) throw new ArgumentException("알 수 없는 가구 프리팹: " + name);

            M.EnsureFolder(PrefabDir);
            var fb = new FB(name);
            r.build(fb);

            if (fb.Colliders.Count > 0)
            {
                foreach (var (c, s) in fb.Colliders)
                {
                    var bc = fb.Root.AddComponent<BoxCollider>();
                    bc.center = c;
                    bc.size = new Vector3(Mathf.Max(0.02f, s.x - ColliderShrink * 2f), Mathf.Max(0.02f, s.y - ColliderShrink), Mathf.Max(0.02f, s.z - ColliderShrink * 2f));
                }
            }
            else if (r.autoCollider)
            {
                Bounds b = default; bool first = true;
                foreach (Renderer rd in fb.Root.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = rd.bounds; first = false; } else b.Encapsulate(rd.bounds);
                }
                if (!first)
                {
                    var bc = fb.Root.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = new Vector3(Mathf.Max(0.02f, b.size.x - ColliderShrink * 2f), Mathf.Max(0.02f, b.size.y - ColliderShrink), Mathf.Max(0.02f, b.size.z - ColliderShrink * 2f));
                }
            }

            if (r.flammable)
            {
                fb.Root.AddComponent<Flammable>();
                InteriorTags.EnsureFlammableTag();
                fb.Root.tag = Flammable.TagName;
            }

            // 정적 오브젝트: 배칭/GI/반사 프로브에 포함 (같은 머티리얼을 공유하므로 정적 배칭이 잘 먹는다)
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform t in fb.Root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

            string path = $"{PrefabDir}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(fb.Root, path);
            Object.DestroyImmediate(fb.Root);
            built[name] = prefab;
            return prefab;
        }

        /// <summary>프리팹을 씬에 배치. 위치는 월드 좌표, yaw 는 Y축 회전(도): 0 → 앞면이 +Z(북), 90 → +X(동), 180 → -Z, 270 → -X</summary>
        public static GameObject Place(string prefab, Transform parent, float x, float z, float yaw = 0f, float y = 0f, string name = null, float scale = 1f)
            => Place(prefab, parent, new Vector3(x, y, z), yaw, name, new Vector3(scale, scale, scale));

        public static GameObject Place(string prefab, Transform parent, Vector3 pos, float yaw, string name = null, Vector3? scale = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(GetPrefab(prefab), parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (!string.IsNullOrEmpty(name)) go.name = name;
            return go;
        }

        /// <summary>등록된 모든 프리팹을 미리 생성 (메뉴에서 단독 실행용)</summary>
        public static void BuildAll()
        {
            AssetDatabase.StartAssetEditing();
            try { foreach (string n in recipes.Keys) GetPrefab(n); }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        // ═════════════════════════ 레시피 등록 ═════════════════════════
        static FurnitureFactory()
        {
            RegisterBedroom();
            RegisterLiving();
            RegisterKitchen();
            RegisterService();
            RegisterOutdoor();
            RegisterProps();
        }

        // ───────── 침실 ─────────
        private static void RegisterBedroom()
        {
            // 침대 1.5 x 2.0 (머리판은 -Z, 발치가 +Z). 매트리스 윗면 0.50, 이불 윗면 0.57
            R("Bed", false, false, f =>
            {
                f.Box("Frame", 0, 0.11f, 0, 1.5f, 0.22f, 2.0f, M.Wood);
                f.Box("Mattress", 0, 0.36f, 0, 1.44f, 0.28f, 1.92f, M.Bedding);
                f.Box("Blanket", 0, 0.535f, 0.30f, 1.48f, 0.07f, 1.30f, M.Blanket);
                f.Box("BlanketFold", 0, 0.575f, -0.36f, 1.48f, 0.05f, 0.14f, M.Pillow);
                f.Box("Pillow_L", -0.37f, 0.56f, -0.78f, 0.56f, 0.12f, 0.34f, M.Pillow);
                f.Box("Pillow_R", 0.37f, 0.56f, -0.78f, 0.56f, 0.12f, 0.34f, M.Pillow);
                f.Box("Headboard", 0, 0.45f, -1.04f, 1.56f, 0.9f, 0.08f, M.Wood);
                f.Box("Footboard", 0, 0.3f, 1.03f, 1.5f, 0.38f, 0.06f, M.Wood);
                f.Col(0, 0.29f, 0, 1.5f, 0.58f, 2.0f);        // 매트리스 높이만 올라설 수 있게
                f.Col(0, 0.45f, -1.04f, 1.56f, 0.9f, 0.08f);  // 머리판
            });
            // 이불/베개/옷가지는 불이 붙을 수 있으므로 별도 소품 프리팹으로도 제공
            R("Nightstand", true, false, f =>
            {
                f.Box("Body", 0, 0.26f, 0, 0.4f, 0.52f, 0.4f, M.Wood);
                f.Box("Top", 0, 0.505f, 0, 0.44f, 0.03f, 0.44f, M.WoodLight);
                f.Box("Drawer", 0, 0.32f, 0.201f, 0.34f, 0.2f, 0.01f, M.WoodLight);
                f.Box("Handle", 0, 0.32f, 0.215f, 0.1f, 0.02f, 0.02f, M.Metal);
            });
            R("TableLamp", false, false, f =>
            {
                f.Cyl("Base", 0, 0.01f, 0, 0.07f, 0.02f, M.MetalDark);
                f.Cyl("Stem", 0, 0.13f, 0, 0.012f, 0.22f, M.Metal);
                f.Cyl("Shade", 0, 0.3f, 0, 0.11f, 0.15f, M.Lampshade);
            });
            R("Wardrobe", true, false, f =>   // 문 2개. 폭 1.3 x 깊이 0.6 x 높이 2.0
            {
                f.Box("Body", 0, 1.0f, 0, 1.3f, 2.0f, 0.6f, M.Wood);
                f.Box("Door_L", -0.33f, 0.98f, 0.305f, 0.63f, 1.9f, 0.02f, M.WoodLight);
                f.Box("Door_R", 0.33f, 0.98f, 0.305f, 0.63f, 1.9f, 0.02f, M.WoodLight);
                f.Box("Handle_L", -0.04f, 1.0f, 0.325f, 0.02f, 0.3f, 0.025f, M.Metal);
                f.Box("Handle_R", 0.04f, 1.0f, 0.325f, 0.02f, 0.3f, 0.025f, M.Metal);
                f.Box("Plinth", 0, 0.02f, 0.02f, 1.28f, 0.04f, 0.58f, M.WoodDark);
            });
            R("Desk", true, false, f =>       // 폭 0.75 x 깊이 0.55 x 높이 0.74, 앞면 = 앉는 쪽
            {
                f.Box("Top", 0, 0.72f, 0, 0.75f, 0.04f, 0.55f, M.WoodLight);
                f.Box("Leg_FL", -0.35f, 0.35f, 0.24f, 0.04f, 0.7f, 0.04f, M.MetalDark);
                f.Box("Leg_FR", 0.35f, 0.35f, 0.24f, 0.04f, 0.7f, 0.04f, M.MetalDark);
                f.Box("Leg_BL", -0.35f, 0.35f, -0.24f, 0.04f, 0.7f, 0.04f, M.MetalDark);
                f.Box("Leg_BR", 0.35f, 0.35f, -0.24f, 0.04f, 0.7f, 0.04f, M.MetalDark);
                f.Box("Drawer", 0.2f, 0.58f, 0, 0.3f, 0.2f, 0.5f, M.Wood);
            });
            // 벽걸이 에어컨 (참고 이미지: 창/벽 위쪽). 폭 0.8 x 깊이 0.2 x 높이 0.28, 피벗 = 아랫면, 앞면 +Z, 콜라이더 없음
            R("AirConditioner", false, false, f =>
            {
                f.Box("Body", 0, 0.14f, 0, 0.8f, 0.28f, 0.2f, M.White);
                f.Box("Vent", 0, 0.03f, 0.08f, 0.7f, 0.04f, 0.06f, M.Plastic);
                f.Box("Slit", 0, 0.1f, 0.101f, 0.6f, 0.006f, 0.002f, M.MetalDark);
            });
            // 검은 암 스탠드 (참고 이미지: 책상 위)
            R("DeskLamp", false, false, f =>
            {
                f.Cyl("Base", 0, 0.01f, 0, 0.07f, 0.02f, M.PlasticBlack);
                f.Box("Arm", 0, 0.2f, -0.02f, 0.015f, 0.38f, 0.015f, M.PlasticBlack, new Vector3(-12f, 0f, 0f));
                f.Cyl("Head", 0, 0.4f, 0.04f, 0.06f, 0.1f, M.PlasticBlack, new Vector3(70f, 0f, 0f));
            });
            R("DeskChair", true, false, f =>
            {
                f.Box("Seat", 0, 0.46f, 0, 0.42f, 0.06f, 0.42f, M.PlasticBlack);
                f.Box("Back", 0, 0.72f, -0.2f, 0.4f, 0.42f, 0.05f, M.PlasticBlack);
                f.Cyl("Stem", 0, 0.24f, 0, 0.025f, 0.4f, M.Metal);
                f.Cyl("Base", 0, 0.03f, 0, 0.26f, 0.04f, M.MetalDark);
            });
            R("Monitor", false, false, f =>   // 화면이 +Z
            {
                f.Box("Base", 0, 0.008f, 0, 0.22f, 0.016f, 0.16f, M.PlasticBlack);
                f.Box("Stand", 0, 0.1f, -0.02f, 0.04f, 0.18f, 0.03f, M.PlasticBlack);
                f.Box("Bezel", 0, 0.3f, 0, 0.46f, 0.28f, 0.025f, M.PlasticBlack);
                f.Box("Screen", 0, 0.3f, 0.0135f, 0.43f, 0.25f, 0.003f, M.Screen);
            });
            R("Keyboard", false, false, f =>
            {
                f.Box("Body", 0, 0.01f, 0, 0.4f, 0.02f, 0.13f, M.PlasticBlack);
                f.Box("Keys", 0, 0.022f, 0, 0.37f, 0.006f, 0.1f, M.MetalDark);
            });
        }

        // ───────── 거실 ─────────
        private static void RegisterLiving()
        {
            // 소파 2.0m 폭. 앞면 +Z. 좌석 윗면 0.45, 등받이 0.85
            R("Sofa", false, false, f =>
            {
                f.Box("Base", 0, 0.12f, 0, 2.0f, 0.2f, 0.9f, M.WoodDark);
                f.Box("Seat_L", -0.64f, 0.32f, 0.1f, 0.64f, 0.2f, 0.68f, M.FabricSofa);
                f.Box("Seat_C", 0, 0.32f, 0.1f, 0.64f, 0.2f, 0.68f, M.FabricSofa);
                f.Box("Seat_R", 0.64f, 0.32f, 0.1f, 0.64f, 0.2f, 0.68f, M.FabricSofa);
                f.Box("Back", 0, 0.6f, -0.34f, 2.0f, 0.5f, 0.22f, M.FabricSofaDark);
                f.Box("Arm_L", -0.92f, 0.38f, 0, 0.16f, 0.5f, 0.9f, M.FabricSofaDark);
                f.Box("Arm_R", 0.92f, 0.38f, 0, 0.16f, 0.5f, 0.9f, M.FabricSofaDark);
                f.Col(0, 0.23f, 0.05f, 2.0f, 0.46f, 0.8f);
                f.Col(0, 0.6f, -0.34f, 2.0f, 0.5f, 0.22f);
            });
            R("Cushion", false, true, f =>
            {
                f.Box("Body", 0, 0.14f, 0, 0.4f, 0.28f, 0.1f, M.Curtain, new Vector3(-12f, 0, 0));
            });
            R("CoffeeTable", true, false, f =>    // 폭 1.0 x 깊이 0.6 x 높이 0.42
            {
                f.Box("Top", 0, 0.4f, 0, 1.0f, 0.04f, 0.6f, M.Wood);
                f.Box("Shelf", 0, 0.12f, 0, 0.9f, 0.02f, 0.5f, M.WoodDark);
                f.Box("Leg_FL", -0.46f, 0.19f, 0.26f, 0.04f, 0.38f, 0.04f, M.WoodDark);
                f.Box("Leg_FR", 0.46f, 0.19f, 0.26f, 0.04f, 0.38f, 0.04f, M.WoodDark);
                f.Box("Leg_BL", -0.46f, 0.19f, -0.26f, 0.04f, 0.38f, 0.04f, M.WoodDark);
                f.Box("Leg_BR", 0.46f, 0.19f, -0.26f, 0.04f, 0.38f, 0.04f, M.WoodDark);
            });
            R("TVStand", true, false, f =>    // 폭 1.6 x 깊이 0.4 x 높이 0.5
            {
                f.Box("Body", 0, 0.25f, 0, 1.6f, 0.5f, 0.4f, M.WoodDark);
                f.Box("Front_L", -0.4f, 0.26f, 0.201f, 0.74f, 0.36f, 0.01f, M.Wood);
                f.Box("Front_R", 0.4f, 0.26f, 0.201f, 0.74f, 0.36f, 0.01f, M.Wood);
                f.Box("Handle_L", -0.04f, 0.26f, 0.215f, 0.02f, 0.12f, 0.02f, M.Metal);
                f.Box("Handle_R", 0.04f, 0.26f, 0.215f, 0.02f, 0.12f, 0.02f, M.Metal);
            });
            R("TV", false, false, f =>        // 피벗 = 거치대 바닥. 화면 아래쪽이 +0.15
            {
                f.Box("Base", 0, 0.01f, 0, 0.36f, 0.02f, 0.2f, M.PlasticBlack);
                f.Box("Neck", 0, 0.09f, -0.01f, 0.06f, 0.16f, 0.04f, M.PlasticBlack);
                f.Box("Bezel", 0, 0.45f, 0, 1.06f, 0.6f, 0.035f, M.PlasticBlack);
                f.Box("Screen", 0, 0.45f, 0.0185f, 1.0f, 0.56f, 0.003f, M.Screen);
            });
            R("Bookshelf", true, false, f =>  // 폭 1.0 x 깊이 0.3 x 높이 1.8
            {
                f.Box("Side_L", -0.485f, 0.9f, 0, 0.03f, 1.8f, 0.3f, M.Wood);
                f.Box("Side_R", 0.485f, 0.9f, 0, 0.03f, 1.8f, 0.3f, M.Wood);
                f.Box("Back", 0, 0.9f, -0.14f, 1.0f, 1.8f, 0.02f, M.WoodDark);
                float[] shelfY = { 0.03f, 0.42f, 0.78f, 1.14f, 1.5f, 1.785f };
                foreach (float y in shelfY) f.Box("Shelf", 0, y, 0.0f, 0.94f, 0.025f, 0.28f, M.WoodLight);
                Material[] books = { M.BookA, M.BookB, M.BookC, M.BookD };
                for (int row = 0; row < 4; row++)
                {
                    float baseY = shelfY[row] + 0.0125f, x = -0.43f;
                    int i = 0;
                    while (x < 0.36f)
                    {
                        float w = 0.025f + ((i * 7 + row * 3) % 4) * 0.01f;
                        float h = 0.2f + ((i * 5 + row * 2) % 5) * 0.025f;
                        f.Box("Book", x + w * 0.5f, baseY + h * 0.5f, 0.0f, w, h, 0.2f, books[(i + row) % books.Length]);
                        x += w + 0.004f; i++;
                    }
                }
            });
            R("FloorLamp", true, false, f =>
            {
                f.Cyl("Base", 0, 0.01f, 0, 0.14f, 0.02f, M.MetalDark);
                f.Cyl("Pole", 0, 0.78f, 0, 0.012f, 1.52f, M.Metal);
                f.Cyl("Shade", 0, 1.6f, 0, 0.17f, 0.26f, M.Lampshade);
            });
            R("Plant", false, false, f =>
            {
                f.Cyl("Pot", 0, 0.15f, 0, 0.17f, 0.3f, M.TerraCotta);
                f.Cyl("Soil", 0, 0.295f, 0, 0.15f, 0.01f, M.Soil);
                f.Cyl("Trunk", 0, 0.55f, 0, 0.018f, 0.5f, M.WoodDark);
                f.Sph("Leaf_1", 0, 0.95f, 0, 0.45f, 0.38f, 0.45f, M.Leaf);
                f.Sph("Leaf_2", 0.14f, 0.75f, 0.05f, 0.3f, 0.26f, 0.3f, M.LeafLight);
                f.Sph("Leaf_3", -0.13f, 0.72f, -0.06f, 0.3f, 0.26f, 0.3f, M.Leaf);
                f.Col(0, 0.3f, 0, 0.34f, 0.6f, 0.34f);   // 화분은 계단턱(0.3m) 위로 올라서지 못하게 0.6m 높이
            });
            R("DiningTable", true, false, f =>   // 폭 1.0 x 깊이 0.6 x 높이 0.75
            {
                f.Box("Top", 0, 0.73f, 0, 1.0f, 0.04f, 0.6f, M.WoodLight);
                f.Box("Leg_FL", -0.45f, 0.355f, 0.25f, 0.05f, 0.71f, 0.05f, M.Wood);
                f.Box("Leg_FR", 0.45f, 0.355f, 0.25f, 0.05f, 0.71f, 0.05f, M.Wood);
                f.Box("Leg_BL", -0.45f, 0.355f, -0.25f, 0.05f, 0.71f, 0.05f, M.Wood);
                f.Box("Leg_BR", 0.45f, 0.355f, -0.25f, 0.05f, 0.71f, 0.05f, M.Wood);
            });
            R("DiningChair", true, false, f =>   // 앉는 사람이 +Z 를 본다
            {
                f.Box("Seat", 0, 0.44f, 0, 0.4f, 0.04f, 0.4f, M.Wood);
                f.Box("Back", 0, 0.7f, -0.19f, 0.4f, 0.42f, 0.03f, M.Wood);
                f.Box("Leg_FL", -0.17f, 0.21f, 0.17f, 0.035f, 0.42f, 0.035f, M.WoodDark);
                f.Box("Leg_FR", 0.17f, 0.21f, 0.17f, 0.035f, 0.42f, 0.035f, M.WoodDark);
                f.Box("Leg_BL", -0.17f, 0.35f, -0.17f, 0.035f, 0.7f, 0.035f, M.WoodDark);
                f.Box("Leg_BR", 0.17f, 0.35f, -0.17f, 0.035f, 0.7f, 0.035f, M.WoodDark);
            });
            R("CurtainPanel", false, true, f =>   // 폭 0.55 x 높이 2.05 주름 커튼 (불붙는 대상이라 가연물)
            {
                for (int i = 0; i < 6; i++)
                    f.Box("Pleat", -0.23f + i * 0.092f, 1.03f, (i % 2 == 0) ? 0.02f : -0.02f, 0.09f, 2.05f, 0.025f, i % 2 == 0 ? M.Curtain : M.Curtain);
            });
            R("Rug", false, true, f =>
            {
                f.Box("Border", 0, 0.006f, 0, 1f, 0.012f, 1f, M.RugBorder);
                f.Box("Inner", 0, 0.0135f, 0, 0.86f, 0.012f, 0.86f, M.Rug);
            });
        }

        // ───────── 주방 ─────────
        private static void RegisterKitchen()
        {
            // 주방 하부장: 길이 1.8 x 깊이 0.6 x 높이 0.9, 앞면 +Z (싱크대 + 가스레인지가 올라갈 줄)
            R("KitchenCounter", true, false, f =>
            {
                f.Box("Body", 0, 0.42f, -0.01f, 1.78f, 0.8f, 0.56f, M.White);
                f.Box("Toe", 0, 0.04f, 0.0f, 1.74f, 0.08f, 0.5f, M.MetalDark);
                f.Box("Top", 0, 0.88f, 0.0f, 1.8f, 0.04f, 0.62f, M.CounterDark);
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.66f + i * 0.44f;
                    f.Box("Door", x, 0.44f, 0.285f, 0.42f, 0.74f, 0.02f, M.White);
                    f.Box("Handle", x, 0.74f, 0.305f, 0.12f, 0.02f, 0.02f, M.Metal);
                }
            });
            // 싱크대: 길이 0.9 x 깊이 0.55 x 높이 0.9 + 수도꼭지 (뒤쪽 -Z 에 붙임)
            R("SinkUnit", true, false, f =>
            {
                f.Box("Body", 0, 0.42f, 0, 0.88f, 0.8f, 0.53f, M.White);
                f.Box("Toe", 0, 0.04f, 0, 0.84f, 0.08f, 0.48f, M.MetalDark);
                f.Box("Top", 0, 0.88f, 0, 0.9f, 0.04f, 0.55f, M.CounterDark);
                f.Box("Basin", -0.1f, 0.904f, 0.02f, 0.5f, 0.008f, 0.36f, M.PlasticBlack);
                f.Box("Drain", -0.1f, 0.909f, 0.02f, 0.06f, 0.004f, 0.06f, M.MetalDark);
                f.Cyl("TapStem", 0.2f, 0.99f, -0.16f, 0.015f, 0.2f, M.Chrome);
                f.Box("TapSpout", 0.2f, 1.09f, -0.06f, 0.025f, 0.025f, 0.22f, M.Chrome);
                f.Box("TapHandle", 0.26f, 0.95f, -0.16f, 0.07f, 0.02f, 0.02f, M.Chrome);
                for (int i = 0; i < 2; i++)
                    f.Box("Door", -0.22f + i * 0.44f, 0.44f, 0.275f, 0.42f, 0.74f, 0.02f, M.White);
            });
            // 벽선반 + 식기건조대 (참고 이미지: 싱크대 위 진녹색 브래킷 선반). 폭 0.9 x 깊이 0.2, 피벗 = 선반 윗면, 콜라이더 없음
            R("DishShelf", false, false, f =>
            {
                f.Box("Board", 0, -0.015f, 0, 0.9f, 0.03f, 0.2f, M.WoodLight);
                f.Box("Bracket_L", -0.35f, -0.12f, -0.07f, 0.03f, 0.18f, 0.05f, M.FrontDoorFrame);
                f.Box("Bracket_R", 0.35f, -0.12f, -0.07f, 0.03f, 0.18f, 0.05f, M.FrontDoorFrame);
                f.Box("RackBase", -0.1f, 0.015f, 0, 0.5f, 0.02f, 0.17f, M.MetalDark);
                for (int i = 0; i < 5; i++) f.Box("Plate", -0.3f + i * 0.1f, 0.12f, 0, 0.012f, 0.2f, 0.17f, M.Ceramic);
                f.Cyl("Bowl", 0.28f, 0.06f, 0, 0.1f, 0.1f, M.Ceramic);
            });
            R("UpperCabinet", true, false, f =>   // 폭 0.95 x 깊이 0.35 x 높이 0.4, 피벗 = 아랫면
            {
                f.Box("Body", 0, 0.2f, 0, 0.95f, 0.4f, 0.35f, M.White);
                for (int i = 0; i < 2; i++) f.Box("Door", -0.235f + i * 0.47f, 0.2f, 0.176f, 0.46f, 0.36f, 0.015f, M.WoodLight);
            });
            R("Fridge", true, false, f =>          // 0.7 x 0.6 x 1.8 상냉장 하냉동 구분
            {
                f.Box("Body", 0, 0.9f, 0, 0.7f, 1.8f, 0.6f, M.Metal);
                f.Box("Seam", 0, 0.62f, 0.301f, 0.69f, 0.012f, 0.004f, M.MetalDark);
                f.Box("HandleUp", 0.28f, 1.25f, 0.32f, 0.025f, 0.4f, 0.025f, M.Chrome);
                f.Box("HandleDn", 0.28f, 0.8f, 0.32f, 0.025f, 0.25f, 0.025f, M.Chrome);
            });
            R("TrashBin", true, false, f =>
            {
                f.Cyl("Body", 0, 0.24f, 0, 0.14f, 0.48f, M.PlasticBlack);
                f.Cyl("Lid", 0, 0.495f, 0, 0.15f, 0.03f, M.Plastic);
            });
        }

        // ───────── 다용도실 / 화장실 / 현관 ─────────
        private static void RegisterService()
        {
            R("WashingMachine", true, false, f =>   // 0.6 x 0.6 x 0.9, 앞면 +Z
            {
                f.Box("Body", 0, 0.45f, 0, 0.6f, 0.9f, 0.6f, M.Plastic);
                f.Box("Panel", 0, 0.82f, 0.301f, 0.56f, 0.12f, 0.004f, M.MetalDark);
                f.Cyl("DoorRing", 0, 0.42f, 0.305f, 0.22f, 0.02f, M.Chrome, new Vector3(90f, 0, 0));
                f.Cyl("DoorGlass", 0, 0.42f, 0.31f, 0.18f, 0.02f, M.Glass, new Vector3(90f, 0, 0));
            });
            R("UtilityShelf", true, false, f =>     // 철제 선반 0.6 x 0.3 x 1.8 (세제/박스)
            {
                foreach (float x in new[] { -0.28f, 0.28f })
                    foreach (float z in new[] { -0.13f, 0.13f })
                        f.Box("Post", x, 0.9f, z, 0.03f, 1.8f, 0.03f, M.Metal);
                float[] ys = { 0.05f, 0.5f, 0.95f, 1.4f, 1.78f };
                foreach (float y in ys) f.Box("Shelf", 0, y, 0, 0.6f, 0.025f, 0.3f, M.Metal);
                f.Cyl("Detergent_1", -0.18f, 0.66f, 0.02f, 0.06f, 0.3f, M.WaterBlue);
                f.Cyl("Detergent_2", -0.04f, 0.64f, 0.02f, 0.05f, 0.26f, M.Orange);
                f.Box("Box_1", 0.14f, 0.62f, 0.0f, 0.2f, 0.22f, 0.22f, M.Cardboard);
                f.Box("Box_2", -0.05f, 1.09f, 0.0f, 0.4f, 0.26f, 0.24f, M.Cardboard);
                f.Box("Box_3", 0.12f, 0.2f, 0.0f, 0.3f, 0.26f, 0.24f, M.Cardboard);
            });
            R("Boiler", true, false, f =>           // 벽걸이 보일러 0.5 x 0.3 x 0.7, 피벗 = 아랫면
            {
                f.Box("Body", 0, 0.35f, 0, 0.5f, 0.7f, 0.3f, M.Plastic);
                f.Box("Panel", 0, 0.5f, 0.151f, 0.24f, 0.12f, 0.004f, M.Screen);
                f.Cyl("Knob", 0.15f, 0.5f, 0.16f, 0.025f, 0.02f, M.MetalDark, new Vector3(90f, 0, 0));
                f.Cyl("PipeA", -0.12f, -0.05f, 0, 0.02f, 0.14f, M.GasPipe);
                f.Cyl("PipeB", 0.0f, -0.05f, 0, 0.02f, 0.14f, M.WaterBlue);
                f.Cyl("PipeC", 0.12f, -0.05f, 0, 0.02f, 0.14f, M.RedPaint);
            });
            R("DryingRack", true, false, f =>       // 빨래건조대 (접이식) 0.8 x 0.55 x 1.0
            {
                foreach (float z in new[] { -0.24f, 0.24f })
                {
                    f.Box("Leg_L", -0.38f, 0.5f, z, 0.025f, 1.0f, 0.025f, M.Metal);
                    f.Box("Leg_R", 0.38f, 0.5f, z, 0.025f, 1.0f, 0.025f, M.Metal);
                }
                for (int i = 0; i < 5; i++) f.Box("Bar", -0.3f + i * 0.15f, 0.98f, 0, 0.02f, 0.02f, 0.52f, M.Metal);
                f.Box("Cloth_1", -0.2f, 0.76f, 0.1f, 0.28f, 0.42f, 0.01f, M.Cloth);
                f.Box("Cloth_2", 0.08f, 0.8f, -0.08f, 0.26f, 0.34f, 0.01f, M.ClothAlt);
                f.Box("Cloth_3", 0.3f, 0.82f, 0.05f, 0.2f, 0.3f, 0.01f, M.Pillow);
            });
            R("Toilet", true, false, f =>           // 0.38 x 0.65, 탱크가 -Z
            {
                f.Box("Tank", 0, 0.6f, -0.24f, 0.38f, 0.4f, 0.17f, M.Ceramic);
                f.Box("Bowl", 0, 0.2f, 0.0f, 0.34f, 0.4f, 0.42f, M.Ceramic);
                f.Box("Seat", 0, 0.42f, 0.04f, 0.37f, 0.035f, 0.46f, M.Plastic);
                f.Box("Lid", 0, 0.45f, -0.03f, 0.35f, 0.03f, 0.36f, M.Plastic);
                f.Cyl("Flush", 0.1f, 0.81f, -0.24f, 0.025f, 0.02f, M.Chrome);
            });
            R("Vanity", true, false, f =>            // 세면대장 0.6 x 0.4 x 0.8
            {
                f.Box("Body", 0, 0.36f, 0, 0.58f, 0.72f, 0.38f, M.White);
                f.Box("Top", 0, 0.78f, 0, 0.6f, 0.04f, 0.4f, M.Ceramic);
                f.Box("Door", 0, 0.38f, 0.195f, 0.54f, 0.64f, 0.015f, M.WoodLight);
            });
            R("Bathtub", true, false, f =>           // 폭 0.7 x 길이 1.4 x 높이 0.55
            {
                f.Box("Outer", 0, 0.275f, 0, 0.7f, 0.55f, 1.4f, M.Ceramic);
                f.Box("Inner", 0, 0.548f, 0, 0.6f, 0.012f, 1.3f, M.WaterBlue);
                f.Box("Rim", 0, 0.545f, 0, 0.7f, 0.01f, 1.4f, M.Plastic);
            });
            R("ShoeCabinet", true, false, f =>       // 폭 1.0 x 깊이 0.4 x 높이 1.1, 앞면 +Z
            {
                f.Box("Body", 0, 0.55f, 0, 1.0f, 1.1f, 0.4f, M.Wood);
                for (int i = 0; i < 2; i++)
                {
                    f.Box("Door", -0.245f + i * 0.49f, 0.55f, 0.205f, 0.47f, 1.05f, 0.015f, M.WoodLight);
                    for (int s = 0; s < 6; s++) f.Box("Slat", -0.245f + i * 0.49f, 0.18f + s * 0.16f, 0.216f, 0.4f, 0.015f, 0.008f, M.Wood);
                    f.Box("Handle", -0.06f + i * 0.12f, 0.6f, 0.225f, 0.02f, 0.2f, 0.02f, M.Metal);
                }
            });
            R("UmbrellaStand", true, false, f =>
            {
                f.Cyl("Body", 0, 0.22f, 0, 0.1f, 0.44f, M.MetalDark);
                f.Cyl("Umbrella_1", -0.03f, 0.55f, 0.0f, 0.012f, 0.7f, M.Rubber);
                f.Cyl("Umbrella_2", 0.04f, 0.5f, 0.02f, 0.012f, 0.6f, M.RedPaint);
            });
            R("StandMirror", false, false, f =>       // 전신거울 0.5 x 1.6 (벽에 기댐, 앞면 +Z)
            {
                f.Box("Frame", 0, 0.9f, 0, 0.52f, 1.64f, 0.035f, M.WoodDark);
                f.Box("Glass", 0, 0.9f, 0.019f, 0.44f, 1.56f, 0.004f, M.Mirror);
            });
            R("ShoePair", false, false, f =>
            {
                f.Box("Shoe_L", -0.06f, 0.04f, 0, 0.09f, 0.08f, 0.26f, M.Rubber);
                f.Box("Shoe_R", 0.06f, 0.04f, 0.01f, 0.09f, 0.08f, 0.26f, M.Rubber);
                f.Box("Sole_L", -0.06f, 0.008f, 0, 0.095f, 0.016f, 0.27f, M.Plastic);
                f.Box("Sole_R", 0.06f, 0.008f, 0.01f, 0.095f, 0.016f, 0.27f, M.Plastic);
            });
        }

        // ───────── 베란다 / 대피공간 / 복도 ─────────
        private static void RegisterOutdoor()
        {
            R("OutdoorUnit", true, false, f =>       // 에어컨 실외기 0.8 x 0.3 x 0.55
            {
                f.Box("Body", 0, 0.275f, 0, 0.8f, 0.55f, 0.3f, M.Plastic);
                f.Cyl("Fan", 0.12f, 0.275f, 0.152f, 0.19f, 0.01f, M.MetalDark, new Vector3(90f, 0, 0));
                f.Box("Grill", -0.3f, 0.275f, 0.152f, 0.1f, 0.4f, 0.01f, M.Metal);
                f.Cyl("Pipe_1", -0.3f, 0.05f, -0.18f, 0.012f, 0.1f, M.Metal);
                f.Cyl("Pipe_2", -0.25f, 0.05f, -0.18f, 0.012f, 0.1f, M.Metal);
            });
            R("Planter", true, false, f =>           // 베란다 화분대 0.8 x 0.25 x 0.45
            {
                f.Box("Box", 0, 0.12f, 0, 0.8f, 0.24f, 0.25f, M.TerraCotta);
                f.Box("Soil", 0, 0.245f, 0, 0.76f, 0.01f, 0.21f, M.Soil);
                for (int i = 0; i < 4; i++) f.Sph("Leaf", -0.28f + i * 0.19f, 0.36f + (i % 2) * 0.05f, 0, 0.2f, 0.22f, 0.2f, i % 2 == 0 ? M.Leaf : M.LeafLight);
            });
            R("CrateStack", true, false, f =>        // 짐 상자 쌓임 0.6 x 0.4 x 0.75
            {
                f.Box("Box_1", 0, 0.18f, 0, 0.6f, 0.36f, 0.4f, M.Cardboard);
                f.Box("Box_2", 0.05f, 0.5f, 0, 0.45f, 0.28f, 0.36f, M.Cardboard);
                f.Box("Crate", -0.1f, 0.76f, 0, 0.3f, 0.22f, 0.3f, M.Plastic);
            });
            R("ShelterShelf", true, false, f =>      // 비상용품 선반 1.0 x 0.35 x 1.8
            {
                f.Box("Side_L", -0.49f, 0.9f, 0, 0.03f, 1.8f, 0.35f, M.Metal);
                f.Box("Side_R", 0.49f, 0.9f, 0, 0.03f, 1.8f, 0.35f, M.Metal);
                float[] ys = { 0.03f, 0.6f, 1.2f, 1.78f };
                foreach (float y in ys) f.Box("Shelf", 0, y, 0, 0.96f, 0.025f, 0.35f, M.Metal);
                for (int i = 0; i < 4; i++) f.Cyl("Water", -0.36f + i * 0.1f, 0.2f, 0.05f, 0.04f, 0.3f, M.WaterBlue);    // 생수
                f.Box("FirstAid", 0.1f, 0.7f, 0, 0.3f, 0.2f, 0.2f, M.Pillow);
                f.Box("FirstAidCross", 0.1f, 0.7f, 0.101f, 0.1f, 0.1f, 0.004f, M.RedPaint);
                f.Cyl("Flashlight", -0.3f, 0.7f, 0.0f, 0.03f, 0.2f, M.MetalDark, new Vector3(0, 0, 90f));
                f.Box("Blanket", -0.2f, 1.3f, 0, 0.4f, 0.14f, 0.28f, M.Orange);
                f.Box("Masks", 0.25f, 1.28f, 0, 0.22f, 0.1f, 0.16f, M.Plastic);
                f.Box("Radio", 0.2f, 1.9f, 0, 0.2f, 0.1f, 0.1f, M.PlasticBlack);
            });
            R("VentWindow", false, false, f =>       // 환기창 0.5 x 0.3 (루버)
            {
                f.Box("Frame", 0, 0, 0, 0.5f, 0.3f, 0.04f, M.Metal);
                for (int i = 0; i < 5; i++) f.Box("Louver", 0, -0.1f + i * 0.05f, 0.015f, 0.44f, 0.012f, 0.03f, M.MetalDark, new Vector3(25f, 0, 0));
            });
            R("HydrantBox", true, false, f =>        // 소화전함 0.7 x 0.18 x 0.9 (복도 벽 매입)
            {
                f.Box("Body", 0, 0.45f, 0, 0.7f, 0.9f, 0.18f, M.RedPaint);
                f.Box("Window", 0, 0.5f, 0.092f, 0.5f, 0.6f, 0.006f, M.Glass);
                f.Cyl("HoseReel", 0, 0.5f, 0.05f, 0.18f, 0.05f, M.Pillow, new Vector3(90f, 0, 0));
                f.Box("Label", 0, 0.85f, 0.095f, 0.34f, 0.08f, 0.004f, M.Pillow);
            });
            R("ElevatorPanel", false, false, f =>    // 호출 버튼 판넬
            {
                f.Box("Plate", 0, 0.15f, 0, 0.14f, 0.3f, 0.025f, M.Metal);
                f.Box("Up", 0, 0.2f, 0.016f, 0.05f, 0.05f, 0.012f, M.Gold);
                f.Box("Down", 0, 0.1f, 0.016f, 0.05f, 0.05f, 0.012f, M.Gold);
            });
            R("DoorLock", false, false, f =>         // 디지털 도어락
            {
                f.Box("Body", 0, 0.0f, 0, 0.075f, 0.2f, 0.035f, M.MetalDark);
                for (int r = 0; r < 4; r++) for (int c = 0; c < 3; c++)
                    f.Box("Key", -0.02f + c * 0.02f, 0.06f - r * 0.025f, 0.02f, 0.014f, 0.014f, 0.006f, M.Pillow);
            });
        }

        // ───────── 소품 (콜라이더 없음) ─────────
        private static void RegisterProps()
        {
            R("FrameLandscape", false, false, f =>
            {
                f.Box("Frame", 0, 0, 0, 0.5f, 0.35f, 0.025f, M.WoodDark);
                f.Box("Art", 0, 0, 0.0135f, 0.44f, 0.29f, 0.004f, M.ArtA);
                f.Box("Sun", 0.1f, 0.05f, 0.016f, 0.08f, 0.08f, 0.004f, M.ArtB);
                f.Box("Hill", -0.05f, -0.07f, 0.016f, 0.3f, 0.1f, 0.004f, M.ArtC);
            });
            R("FramePortrait", false, false, f =>
            {
                f.Box("Frame", 0, 0, 0, 0.3f, 0.4f, 0.025f, M.WoodLight);
                f.Box("Art", 0, 0, 0.0135f, 0.25f, 0.35f, 0.004f, M.ArtB);
                f.Box("Shape", 0, -0.04f, 0.016f, 0.14f, 0.14f, 0.004f, M.ArtA);
            });
            R("WallClock", false, false, f =>
            {
                f.Cyl("Rim", 0, 0, 0, 0.17f, 0.03f, M.MetalDark, new Vector3(90f, 0, 0));
                f.Cyl("Face", 0, 0, 0.016f, 0.15f, 0.01f, M.Pillow, new Vector3(90f, 0, 0));
                f.Box("HourHand", 0, 0.03f, 0.024f, 0.012f, 0.07f, 0.004f, M.PlasticBlack);
                f.Box("MinuteHand", 0.03f, 0.0f, 0.024f, 0.09f, 0.008f, 0.004f, M.PlasticBlack, new Vector3(0, 0, 30f));
            });
            R("Calendar", false, true, f =>
            {
                f.Box("Page", 0, 0, 0, 0.26f, 0.36f, 0.006f, M.Paper);
                f.Box("Header", 0, 0.13f, 0.004f, 0.26f, 0.08f, 0.004f, M.RedPaint);
                f.Box("Grid", 0, -0.04f, 0.004f, 0.22f, 0.2f, 0.002f, M.ArtC);
            });
            R("PostIt", false, true, f => f.Box("Note", 0, 0, 0, 0.075f, 0.075f, 0.002f, M.PostIt));
            R("Book", false, true, f =>
            {
                f.Box("Cover", 0, 0.015f, 0, 0.18f, 0.03f, 0.25f, M.BookB);
                f.Box("Pages", 0.005f, 0.015f, 0.0f, 0.165f, 0.024f, 0.24f, M.Paper);
            });
            R("BookStack", false, true, f =>
            {
                f.Box("B1", 0, 0.015f, 0, 0.2f, 0.03f, 0.27f, M.BookA);
                f.Box("B2", 0.01f, 0.045f, 0.0f, 0.18f, 0.03f, 0.25f, M.BookC, new Vector3(0, 8f, 0));
                f.Box("B3", -0.01f, 0.075f, 0.0f, 0.17f, 0.03f, 0.23f, M.BookD, new Vector3(0, -6f, 0));
            });
            R("Cup", false, false, f =>
            {
                f.Cyl("Body", 0, 0.045f, 0, 0.04f, 0.09f, M.Ceramic);
                f.Cyl("Coffee", 0, 0.088f, 0, 0.034f, 0.004f, M.WoodDark);
                f.Box("Handle", 0.05f, 0.05f, 0, 0.02f, 0.05f, 0.012f, M.Ceramic);
            });
            R("Remote", false, false, f =>
            {
                f.Box("Body", 0, 0.01f, 0, 0.05f, 0.02f, 0.18f, M.PlasticBlack);
                f.Box("Buttons", 0, 0.021f, 0.02f, 0.035f, 0.004f, 0.1f, M.Metal);
            });
            R("Newspaper", false, true, f =>
            {
                f.Box("Sheet_1", 0, 0.006f, 0, 0.32f, 0.012f, 0.42f, M.Paper);
                f.Box("Sheet_2", 0.01f, 0.016f, 0.01f, 0.3f, 0.008f, 0.4f, M.Pillow, new Vector3(0, 6f, 0));
                f.Box("Headline", 0, 0.021f, 0.12f, 0.24f, 0.002f, 0.06f, M.PlasticBlack);
            });
            R("PhoneCharger", false, false, f =>
            {
                f.Box("Phone", 0, 0.005f, 0, 0.07f, 0.01f, 0.145f, M.PlasticBlack);
                f.Box("Screen", 0, 0.0105f, 0, 0.062f, 0.002f, 0.135f, M.Screen);
                f.Box("Cable_1", 0, 0.003f, -0.12f, 0.008f, 0.006f, 0.1f, M.Plastic);
                f.Box("Cable_2", 0.04f, 0.003f, -0.18f, 0.008f, 0.006f, 0.1f, M.Plastic, new Vector3(0, 40f, 0));
            });
            R("HungClothes", false, true, f =>        // 의자 등받이에 걸친 옷
            {
                f.Box("Body", 0, 0, 0, 0.4f, 0.5f, 0.03f, M.Cloth);
                f.Box("Sleeve_L", -0.22f, -0.2f, 0.03f, 0.1f, 0.35f, 0.03f, M.Cloth);
                f.Box("Sleeve_R", 0.22f, -0.2f, 0.03f, 0.1f, 0.35f, 0.03f, M.Cloth);
            });
            R("Towel", false, true, f => f.Box("Folded", 0, 0.04f, 0, 0.3f, 0.08f, 0.2f, M.Pillow));
            R("HangingShirt", false, true, f =>
            {
                f.Box("Body", 0, -0.25f, 0, 0.36f, 0.5f, 0.012f, M.Cloth);
                f.Box("Sleeve_L", -0.22f, -0.1f, 0, 0.12f, 0.2f, 0.012f, M.Cloth);
                f.Box("Sleeve_R", 0.22f, -0.1f, 0, 0.12f, 0.2f, 0.012f, M.Cloth);
                f.Box("Hanger", 0, 0.01f, 0, 0.3f, 0.01f, 0.01f, M.Metal);
            });
            R("SensorLight", false, false, f =>
            {
                f.Cyl("Housing", 0, 0, 0, 0.06f, 0.03f, M.Plastic, new Vector3(90f, 0, 0));
                f.Cyl("Lens", 0, 0, 0.018f, 0.035f, 0.008f, M.Lampshade, new Vector3(90f, 0, 0));
            });
            R("Soap", false, false, f =>
            {
                f.Box("Dish", 0, 0.01f, 0, 0.1f, 0.02f, 0.07f, M.Ceramic);
                f.Box("Soap", 0, 0.03f, 0, 0.07f, 0.025f, 0.05f, M.ClothAlt);
            });
            R("ToothbrushCup", false, false, f =>
            {
                f.Cyl("Cup", 0, 0.05f, 0, 0.035f, 0.1f, M.Glass);
                f.Cyl("Brush_1", -0.01f, 0.1f, 0, 0.005f, 0.14f, M.WaterBlue);
                f.Cyl("Brush_2", 0.01f, 0.1f, 0, 0.005f, 0.14f, M.RedPaint);
            });
        }
    }

    /// <summary>Unity Tag 자동 등록</summary>
    public static class InteriorTags
    {
        public static void EnsureFlammableTag()
        {
            foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
                if (t == Flammable.TagName) return;
            UnityEditorInternal.InternalEditorUtility.AddTag(Flammable.TagName);
        }
    }
}
