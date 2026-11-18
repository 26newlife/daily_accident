using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 인테리어용 URP Lit 머티리얼과 절차적 텍스처(마루 줄무늬, 체크무늬 타일)를 코드로 만든다.
    /// 머티리얼은 Assets/Materials/Interior/ 에 저장되며, 같은 이름이면 다시 만들지 않고 갱신해서 공유한다.
    /// 텍스처가 필요 없는 재질은 색상 + Smoothness/Metallic 차이로 구분한다.
    /// </summary>
    public static class InteriorMaterialFactory
    {
        public const string MaterialDir = "Assets/Materials/Interior";
        public const string TextureDir = "Assets/Materials/Interior/Textures";

        // 텍스처 1장이 덮는 실제 길이(m). 바닥 쿼드의 UV 를 이 값으로 나눠서 타일 크기를 맞춘다.
        public const float WoodTexMeters = 1.0f;
        public const float TileTexMeters = 0.6f;
        public const float BathTileTexMeters = 0.3f;

        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        public static void ResetCache() { cache.Clear(); texCache.Clear(); }

        // ───────── 구조 ─────────
        public static Material Wall => Solid("Wall", "F2EFE8", 0.06f);                 // 흰 벽지 톤
        public static Material CorridorWall => Solid("CorridorWall", "DDD6C8", 0.1f);  // 복도 벽 (살짝 베이지)
        public static Material Ceiling => Solid("Ceiling", "FAF8F3", 0.02f);
        public static Material Baseboard => Solid("Baseboard", "D8D0C2", 0.2f);       // 걸레받이: 벽보다 살짝 진하게
        public static Material CorridorBase => Solid("CorridorBase", "5F646B", 0.25f);
        public static Material DoorFrame => Solid("DoorFrame", "B9A07F", 0.3f);        // 문틀
        public static Material FireDoorFrame => Solid("FireDoorFrame", "4A545C", 0.5f, 0.5f);
        public static Material SlabConcrete => Solid("SlabConcrete", "8E8E91", 0.05f);
        public static Material Partition => Solid("Partition", "E7D6A9", 0.05f);       // 경량칸막이 (다른 벽과 구분)
        public static Material PartitionEdge => Solid("PartitionEdge", "B89B5C", 0.05f);

        // ───────── 바닥 (절차적 텍스처) ─────────
        public static Material FloorWood => Textured("FloorWood", WoodTexture(), Color.white, 0.35f);
        public static Material FloorTileKitchen => Textured("FloorTileKitchen", TileTexture("tile_kitchen", "D9D2C3", "CFC7B6", "A79F8E", 2), Color.white, 0.45f);
        public static Material FloorTileEntry => Textured("FloorTileEntry", TileTexture("tile_entry", "7F858B", "757B81", "4F5459", 2), Color.white, 0.5f);
        public static Material FloorTileUtility => Textured("FloorTileUtility", TileTexture("tile_utility", "9AA0A6", "90969C", "6E7378", 2), Color.white, 0.4f);
        public static Material FloorTileBath => Textured("FloorTileBath", TileTexture("tile_bath", "C9DCE3", "BBD0D9", "8FA9B4", 2), Color.white, 0.7f);
        public static Material WallTileBath => Textured("WallTileBath", TileTexture("tile_bathwall", "EEF4F6", "E4ECEF", "B9C7CC", 2), Color.white, 0.8f);
        public static Material FloorTileBalcony => Textured("FloorTileBalcony", TileTexture("tile_balcony", "B4A491", "A89887", "7D705F", 2), Color.white, 0.25f);
        public static Material FloorCorridor => Textured("FloorCorridor", TileTexture("tile_corridor", "A5A39E", "9B9994", "6D6B67", 2), Color.white, 0.55f);
        public static Material FloorShelter => Solid("FloorShelter", "8A8D91", 0.2f);

        // ───────── 가구 재질 ─────────
        public static Material Wood => Solid("Wood", "A67C52", 0.35f);
        public static Material WoodLight => Solid("WoodLight", "D3B88F", 0.35f);
        public static Material WoodDark => Solid("WoodDark", "5B4333", 0.35f);
        public static Material White => Solid("WhiteLaminate", "ECEAE4", 0.45f);
        public static Material FabricSofa => Solid("FabricSofa", "7E8CA0", 0.05f);
        public static Material FabricSofaDark => Solid("FabricSofaDark", "697689", 0.05f);
        public static Material Bedding => Solid("Bedding", "E6E9F2", 0.05f);
        public static Material Blanket => Solid("Blanket", "8FA6C9", 0.05f);
        public static Material Pillow => Solid("Pillow", "F6F4EE", 0.05f);
        public static Material Curtain => Solid("Curtain", "C9A592", 0.02f);
        public static Material CurtainSheer => Solid("CurtainSheer", "F1ECE2", 0.02f);
        public static Material Rug => Solid("Rug", "9AA7B8", 0.02f);
        public static Material RugBorder => Solid("RugBorder", "6F7C8E", 0.02f);
        public static Material Cloth => Solid("Cloth", "A58EC9", 0.02f);
        public static Material ClothAlt => Solid("ClothAlt", "E3A857", 0.02f);
        public static Material Metal => Solid("Metal", "B4B9C0", 0.65f, 0.85f);
        public static Material MetalDark => Solid("MetalDark", "3A3D43", 0.5f, 0.6f);
        public static Material Chrome => Solid("Chrome", "DADDE2", 0.9f, 1f);
        public static Material Plastic => Solid("Plastic", "EDEDED", 0.5f);
        public static Material PlasticBlack => Solid("PlasticBlack", "1C1C20", 0.45f);
        public static Material Rubber => Solid("Rubber", "2A2A2C", 0.1f);
        public static Material Ceramic => Solid("Ceramic", "F6F7F7", 0.85f);
        public static Material Stone => Solid("StoneTop", "CFCBC2", 0.55f);
        public static Material Screen => Solid("Screen", "0C1015", 0.92f);
        public static Material Mirror => Solid("Mirror", "C8D6DE", 0.98f, 1f);
        public static Material Glass => Create("Glass", new Color(0.7f, 0.85f, 1f, 0.2f), 0.95f, 0f, null, Vector2.one, true, null);
        public static Material BalconyGlass => Create("BalconyGlass", new Color(0.82f, 0.92f, 1f, 0.1f), 0.8f, 0f, null, Vector2.one, true, null);   // 풍경이 가려지지 않게 알파 0.1
        public static Material BalconyFrame => Solid("BalconyFrame", "C4C8CD", 0.45f, 0.5f);
        public static Material Paper => Solid("Paper", "F5F1E6", 0.05f);
        public static Material PostIt => Solid("PostIt", "FFE35B", 0.05f);
        public static Material Cardboard => Solid("Cardboard", "B28B5E", 0.05f);
        public static Material TerraCotta => Solid("TerraCotta", "B66A45", 0.1f);
        public static Material Soil => Solid("Soil", "3F3025", 0.02f);
        public static Material Leaf => Solid("Leaf", "3F7D3A", 0.2f);
        public static Material LeafLight => Solid("LeafLight", "6FA84A", 0.2f);
        public static Material Lampshade => Solid("Lampshade", "F7EAC9", 0.05f);
        public static Material Oil => Solid("Oil", "E9C46A", 0.9f);
        public static Material Gold => Solid("Gold", "D4A937", 0.7f, 0.9f);
        public static Material RedPaint => Solid("RedPaint", "C42B2B", 0.5f);
        public static Material GreenSign => Create("GreenSign", Hex("1FAA59"), 0.3f, 0f, null, Vector2.one, false, Hex("1FAA59") * 1.6f);
        public static Material GasPipe => Solid("GasPipe", "E1B30C", 0.4f, 0.3f);
        public static Material Orange => Solid("Orange", "E8833A", 0.05f);
        public static Material BookA => Solid("BookA", "8F3B3B", 0.1f);
        public static Material BookB => Solid("BookB", "3B5F8F", 0.1f);
        public static Material BookC => Solid("BookC", "3E7F5A", 0.1f);
        public static Material BookD => Solid("BookD", "C8A24B", 0.1f);
        public static Material ArtA => Solid("ArtA", "7FA6B8", 0.05f);
        public static Material ArtB => Solid("ArtB", "D9A66B", 0.05f);
        public static Material ArtC => Solid("ArtC", "A3B58B", 0.05f);
        public static Material WaterBlue => Solid("WaterBlue", "5BA4E6", 0.8f);

        // ───────── 생성 헬퍼 ─────────
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c);
            return c;
        }

        private static Material Solid(string name, string hex, float smoothness, float metallic = 0f)
            => Create(name, Hex(hex), smoothness, metallic, null, Vector2.one, false, null);

        private static Material Textured(string name, Texture2D tex, Color tint, float smoothness)
            => Create(name, tint, smoothness, 0f, tex, Vector2.one, false, null);

        private static Shader FindShader()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            return sh != null ? sh : Shader.Find("Standard");
        }

        private static Material Create(string name, Color color, float smoothness, float metallic, Texture2D tex, Vector2 tiling, bool transparent, Color? emission)
        {
            if (cache.TryGetValue(name, out Material cached) && cached != null) return cached;

            EnsureFolder(MaterialDir);
            string path = $"{MaterialDir}/Int_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(FindShader()) { name = "Int_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", tiling); }
                mat.mainTexture = tex;
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            cache[name] = mat;
            return mat;
        }

        // ───────── 절차적 텍스처 ─────────
        /// <summary>마루: 폭 0.2m 판재 5줄, 판마다 이음매 위치가 어긋나고 결/명암이 다르다 (1m x 1m 타일링)</summary>
        private static Texture2D WoodTexture()
        {
            return GetOrBuildTexture("wood_planks", 512, (px, size) =>
            {
                int plank = size / 5;
                Color baseA = Hex("B58A5C"), baseB = Hex("9C7348");
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int col = x / plank;
                        int shift = (int)(Hash(col, 7) * size);
                        int yy = (y + shift) % size;
                        int segLen = size / 2;
                        int seg = yy / segLen;                         // 판 한 장의 길이 = 0.5m
                        float tone = Hash(col, seg + 31);
                        Color c = Color.Lerp(baseA, baseB, tone);
                        float grain = Mathf.Sin((x * 0.35f) + Hash(col, seg) * 40f + Mathf.Sin(yy * 0.02f) * 3f) * 0.025f;
                        c = new Color(c.r + grain, c.g + grain, c.b + grain, 1f);
                        bool groove = x % plank < 2 || yy % segLen < 2;
                        if (groove) c *= 0.72f;
                        px[y * size + x] = c;
                    }
            }, srgb: true);
        }

        /// <summary>체크무늬 타일: cells x cells 칸 + 줄눈. 칸마다 아주 약간 색이 다르다</summary>
        private static Texture2D TileTexture(string key, string hexA, string hexB, string hexGrout, int cells)
        {
            Color a = Hex(hexA), b = Hex(hexB), grout = Hex(hexGrout);
            return GetOrBuildTexture(key, 256, (px, size) =>
            {
                int cell = size / cells;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int cx = x / cell, cy = y / cell;
                        bool checker = (cx + cy) % 2 == 0;
                        Color c = Color.Lerp(checker ? a : b, checker ? b : a, Hash(cx, cy) * 0.25f);
                        int lx = x % cell, ly = y % cell;
                        if (lx < 2 || ly < 2) c = grout;
                        px[y * size + x] = c;
                    }
            }, srgb: true);
        }

        private delegate void PixelFill(Color[] pixels, int size);

        private static Texture2D GetOrBuildTexture(string key, int size, PixelFill fill, bool srgb)
        {
            if (texCache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            EnsureFolder(TextureDir);
            string path = $"{TextureDir}/{key}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                fill(px, size);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled))
            {
                importer.sRGBTexture = srgb;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            texCache[key] = loaded;
            return loaded;
        }

        /// <summary>결정적 해시 (0~1)</summary>
        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
