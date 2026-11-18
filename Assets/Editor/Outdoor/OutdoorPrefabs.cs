using System.Collections.Generic;
using System.IO;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Prim = GameUI.EditorTools.Interior.Prim;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools.Outdoor
{
    /// <summary>창밖 풍경용 URP 머티리얼 (Assets/Materials/Outdoor/) — 색상 + Smoothness 로 구분하고 같은 재질은 공유한다</summary>
    public static class OutdoorMaterials
    {
        public const string Dir = "Assets/Materials/Outdoor";
        public const string TexDir = "Assets/Materials/Outdoor/Textures";
        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void ResetCache() => cache.Clear();
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c); return c; }

        public static Material Lit(string name, Color color, float smooth = 0.1f, float metal = 0f, Texture2D tex = null)
            => Make(name, "Universal Render Pipeline/Lit", color, smooth, metal, tex, false);

        public static Material Unlit(string name, Color color, Texture2D tex = null, bool transparent = false)
            => Make(name, "Universal Render Pipeline/Unlit", color, 0f, 0f, tex, transparent);

        public static Material Particle(string name, Color color, Texture2D tex, bool additive)
        {
            string key = "P_" + name;
            if (cache.TryGetValue(key, out Material c) && c != null) return c;
            Material m = LoadOrCreate(key, Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m); cache[key] = m; return m;
        }

        private static Material Make(string name, string shaderName, Color color, float smooth, float metal, Texture2D tex, bool transparent)
        {
            if (cache.TryGetValue(name, out Material cached) && cached != null) return cached;
            Material m = LoadOrCreate(name, Shader.Find(shaderName) ?? Shader.Find("Standard"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m); cache[name] = m; return m;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            GameUI.EditorTools.Interior.InteriorMaterialFactory.EnsureFolder(Dir);
            string path = $"{Dir}/Out_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader) { name = "Out_" + name }; AssetDatabase.CreateAsset(mat, path); }
            return mat;
        }

        /// <summary>PNG 로 저장하고 불러온다 (이미 있으면 재사용)</summary>
        public static Texture2D SaveTexture(string file, Texture2D tex, bool srgb = true, bool mip = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            GameUI.EditorTools.Interior.InteriorMaterialFactory.EnsureFolder(TexDir);
            string path = $"{TexDir}/{file}.png";
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
            }
            Object.DestroyImmediate(tex);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != wrap || imp.mipmapEnabled != mip))
            {
                imp.sRGBTexture = srgb; imp.wrapMode = wrap; imp.mipmapEnabled = mip; imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    /// <summary>
    /// 창밖 풍경용 프리팹 팩토리 (Assets/Prefabs/Outdoor/). 프리미티브 조합이며 나중에 실제 모델로 교체하기 쉽게 프리팹으로 분리한다.
    /// </summary>
    public static class OutdoorPrefabs
    {
        public const string Dir = "Assets/Prefabs/Outdoor";
        public static readonly string[] CarColors = { "C0392B", "2C3E50", "ECF0F1", "7F8C8D", "1F2A44", "F1C40F", "27AE60", "BDC3C7", "8E44AD", "D35400" };

        private static GameObject Save(GameObject root, string name)
        {
            GameUI.EditorTools.Interior.InteriorMaterialFactory.EnsureFolder(Dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material M(string n, string hex, float s = 0.15f, float m = 0f) => OutdoorMaterials.Lit(n, OutdoorMaterials.Hex(hex), s, m);

        // ───────── 나무 ─────────
        public static GameObject TreeRound()
        {
            var r = new GameObject("Tree_Round").transform;
            Prim.Cyl(r, "Trunk", new Vector3(0, 1.4f, 0), 0.16f, 2.8f, M("Trunk", "5B4330"));
            Prim.Sph(r, "Canopy", new Vector3(0, 3.8f, 0), new Vector3(3.4f, 2.8f, 3.4f), M("Leaf", "3F7D3A"));
            Prim.Sph(r, "Canopy2", new Vector3(0.7f, 3.2f, 0.3f), new Vector3(2.2f, 1.9f, 2.2f), M("LeafLight", "5E9A47"));
            return Save(r.gameObject, "Tree_Round");
        }

        public static GameObject TreePine()
        {
            var r = new GameObject("Tree_Pine").transform;
            Prim.Cyl(r, "Trunk", new Vector3(0, 1.0f, 0), 0.15f, 2.0f, M("Trunk", "5B4330"));
            for (int i = 0; i < 3; i++)
                Prim.Sph(r, "Layer", new Vector3(0, 2.4f + i * 1.3f, 0), new Vector3(2.6f - i * 0.7f, 2.2f, 2.6f - i * 0.7f), M("Pine", "2D5E3A"));
            return Save(r.gameObject, "Tree_Pine");
        }

        // ───────── 가로등 / 벤치 ─────────
        public static GameObject LampPost()
        {
            var r = new GameObject("LampPost").transform;
            Prim.Cyl(r, "Pole", new Vector3(0, 2.6f, 0), 0.07f, 5.2f, M("LampMetal", "3A3D43", 0.5f, 0.6f));
            Prim.Box(r, "Head", new Vector3(0.35f, 5.25f, 0), new Vector3(0.9f, 0.14f, 0.35f), M("LampHead", "D8D8D0", 0.3f));
            return Save(r.gameObject, "LampPost");
        }

        public static GameObject Bench()
        {
            var r = new GameObject("Bench").transform;
            Material wood = M("BenchWood", "8A6A48", 0.2f);
            Prim.Box(r, "Seat", new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.08f, 0.45f), wood);
            Prim.Box(r, "Back", new Vector3(0, 0.8f, -0.2f), new Vector3(1.6f, 0.4f, 0.06f), wood);
            foreach (float x in new[] { -0.7f, 0.7f }) Prim.Box(r, "Leg", new Vector3(x, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.4f), M("BenchMetal", "3A3D43", 0.5f, 0.5f));
            return Save(r.gameObject, "Bench");
        }

        // ───────── 자동차 (앞이 +Z) ─────────
        public static GameObject Car()
        {
            var r = new GameObject("Car_Sedan").transform;
            Prim.Box(r, "Car_Body", new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.7f, 4.3f), M("CarPaint0", CarColors[0], 0.6f, 0.3f));
            Prim.Box(r, "Cabin", new Vector3(0, 1.17f, -0.1f), new Vector3(1.6f, 0.55f, 2.2f), M("CarGlass", "1A2733", 0.9f, 0.1f));
            foreach (float x in new[] { -0.88f, 0.88f })
                foreach (float z in new[] { -1.35f, 1.35f })
                    Prim.Cyl(r, "Wheel", new Vector3(x, 0.33f, z), 0.33f, 0.22f, M("Tire", "17181A", 0.1f), new Vector3(0, 0, 90f));
            var head = M("Headlight", "FFF6D6", 0.8f);
            foreach (float x in new[] { -0.6f, 0.6f }) Prim.Box(r, "Headlight", new Vector3(x, 0.7f, 2.16f), new Vector3(0.3f, 0.14f, 0.05f), head);
            var brake = OutdoorMaterials.Lit("BrakeLight", new Color(0.35f, 0.04f, 0.04f), 0.5f);
            var brakes = new List<Renderer>();
            foreach (float x in new[] { -0.62f, 0.62f }) brakes.Add(Prim.Box(r, "Brakelight", new Vector3(x, 0.72f, -2.16f), new Vector3(0.3f, 0.12f, 0.05f), brake).GetComponent<Renderer>());
            var vc = r.gameObject.AddComponent<VehicleController>();
            var so = new SerializedObject(vc);
            var arr = so.FindProperty("brakeLights");
            arr.arraySize = brakes.Count;
            for (int i = 0; i < brakes.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = brakes[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            vc.enabled = false;     // 주차 차량은 꺼 둔다 (움직이는 차만 켠다)
            return Save(r.gameObject, "Car_Sedan");
        }

        // ───────── 보행자 (프리미티브 + 절차적 걷기) ─────────
        public static GameObject Pedestrian()
        {
            var root = new GameObject("Pedestrian").transform;
            Material shirt = M("Shirt0", "4A78B8"), pants = M("Pants0", "2E3440"), skin = M("Skin0", "E0B58F"), hair = M("Hair0", "2B2118");

            var body = new GameObject("Body").transform; body.SetParent(root, false);
            Prim.Cyl(body, "Shirt_Torso", new Vector3(0, 1.17f, 0), 0.2f, 0.6f, shirt);
            Transform head = Prim.Sph(body, "Skin_Head", new Vector3(0, 1.62f, 0), Vector3.one * 0.26f, skin).transform;
            Prim.Sph(head, "Hair", new Vector3(0, 0.12f, -0.02f), new Vector3(1.05f, 0.7f, 1.05f), hair);
            Transform lArm = Pivot(body, "ArmL", new Vector3(-0.27f, 1.42f, 0)), rArm = Pivot(body, "ArmR", new Vector3(0.27f, 1.42f, 0));
            foreach (Transform a in new[] { lArm, rArm })
            {
                Prim.Box(a, "Shirt_Arm", new Vector3(0, -0.22f, 0), new Vector3(0.1f, 0.44f, 0.1f), shirt);
                Prim.Sph(a, "Skin_Hand", new Vector3(0, -0.47f, 0), Vector3.one * 0.1f, skin);
            }
            Transform lLeg = Pivot(root, "LegL", new Vector3(-0.1f, 0.85f, 0)), rLeg = Pivot(root, "LegR", new Vector3(0.1f, 0.85f, 0));
            foreach (Transform l in new[] { lLeg, rLeg }) Prim.Box(l, "Pants_Leg", new Vector3(0, -0.42f, 0), new Vector3(0.14f, 0.84f, 0.15f), pants);

            Prim.Box(body, "Bag", new Vector3(0, 1.2f, -0.17f), new Vector3(0.28f, 0.34f, 0.12f), M("Bag", "8A5A3A", 0.2f)).SetActive(false);
            var stroller = new GameObject("Stroller").transform; stroller.SetParent(root, false);
            Prim.Box(stroller, "Frame", new Vector3(0, 0.6f, 0.75f), new Vector3(0.55f, 0.35f, 0.8f), M("Stroller", "5B6E8C", 0.3f));
            Prim.Box(stroller, "Handle", new Vector3(0, 1.0f, 0.38f), new Vector3(0.5f, 0.05f, 0.05f), M("BenchMetal", "3A3D43", 0.5f, 0.5f));
            foreach (float x in new[] { -0.25f, 0.25f }) Prim.Cyl(stroller, "Wheel", new Vector3(x, 0.15f, 0.75f), 0.15f, 0.05f, M("Tire", "17181A", 0.1f), new Vector3(0, 0, 90f));
            stroller.gameObject.SetActive(false);

            var npc = root.gameObject.AddComponent<PedestrianNPC>();
            var so = new SerializedObject(npc);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("leftArm").objectReferenceValue = lArm; so.FindProperty("rightArm").objectReferenceValue = rArm;
            so.FindProperty("leftLeg").objectReferenceValue = lLeg; so.FindProperty("rightLeg").objectReferenceValue = rLeg;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root.gameObject, "Pedestrian");
        }

        public static GameObject Dog()
        {
            var r = new GameObject("Dog").transform;
            Material fur = M("Fur", "B08A5E", 0.1f);
            Prim.Box(r, "Body", new Vector3(0, 0.3f, 0), new Vector3(0.2f, 0.22f, 0.5f), fur);
            Prim.Box(r, "Head", new Vector3(0, 0.42f, 0.32f), new Vector3(0.16f, 0.16f, 0.2f), fur);
            Prim.Box(r, "Tail", new Vector3(0, 0.38f, -0.3f), new Vector3(0.05f, 0.05f, 0.2f), fur, new Vector3(-30f, 0, 0));
            foreach (float x in new[] { -0.07f, 0.07f }) foreach (float z in new[] { -0.18f, 0.18f }) Prim.Box(r, "Leg", new Vector3(x, 0.1f, z), new Vector3(0.05f, 0.2f, 0.05f), fur);
            r.gameObject.AddComponent<DogFollower>();
            return Save(r.gameObject, "Dog");
        }

        public static GameObject Bird()
        {
            var r = new GameObject("Bird").transform;
            Material dark = M("Bird", "2A2A2E", 0.2f);
            Prim.Sph(r, "Body", Vector3.zero, new Vector3(0.18f, 0.14f, 0.35f), dark);
            Transform l = Pivot(r, "WingL", new Vector3(-0.07f, 0.03f, 0)), rr = Pivot(r, "WingR", new Vector3(0.07f, 0.03f, 0));
            Prim.Box(l, "Wing", new Vector3(-0.22f, 0, 0), new Vector3(0.44f, 0.02f, 0.16f), dark);
            Prim.Box(rr, "Wing", new Vector3(0.22f, 0, 0), new Vector3(0.44f, 0.02f, 0.16f), dark);
            var b = r.gameObject.AddComponent<BirdFlyer>();
            var so = new SerializedObject(b);
            so.FindProperty("leftWing").objectReferenceValue = l; so.FindProperty("rightWing").objectReferenceValue = rr;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(r.gameObject, "Bird");
        }

        /// <summary>관절 피벗 (자식 큐브를 아래로 늘어뜨려 어깨/엉덩이 회전이 되게 한다)</summary>
        private static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false); t.localPosition = pos;
            return t;
        }
    }
}
