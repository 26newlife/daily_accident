using System.Collections.Generic;
using System.IO;
using GameUI.EditorTools.Art;
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

        public static void ResetCache() { cache.Clear(); texCache.Clear(); mapped.Clear(); oak = grain = plaster = weave = leather = steel = marble = null; }

        // ───────── 구조 ─────────
        public static Material Wall => Pbr(Solid("Wall", "EBE6DC", 0.06f), Plaster, Vector2.one, 0.0f);   // 따뜻한 오프화이트 플라스터 (큰 면은 UV 가 늘어나 노멀을 쓰지 않는다)
        public static Material CorridorWall => Pbr(Solid("CorridorWall", "D9D2C4", 0.1f), Plaster, Vector2.one, 0.0f);  // 복도 벽 (살짝 베이지)
        public static Material Ceiling => Pbr(Solid("Ceiling", "F6F3EC", 0.02f), Plaster, Vector2.one, 0.0f);
        public static Material Baseboard => Pbr(Solid("Baseboard", "D2BC9A", 0.25f), WoodGrain, Vector2.one, 0.2f);       // 걸레받이: 밝은 우드
        public static Material CorridorBase => Solid("CorridorBase", "5F646B", 0.25f);
        public static Material DoorFrame => Solid("DoorFrame", "B9A07F", 0.3f);        // 문틀
        public static Material FireDoorFrame => Solid("FireDoorFrame", "4A545C", 0.5f, 0.5f);
        public static Material FrontDoorFrame => Solid("FrontDoorFrame", "27382F", 0.4f, 0.2f);   // 현관문 틀: 진녹색 (참고 이미지)
        public static Material CounterDark => Solid("CounterDark", "26272A", 0.55f);               // 싱크대 상판: 검정 인조석
        public static Material SlabConcrete => Solid("SlabConcrete", "8E8E91", 0.05f);
        public static Material Partition => Solid("Partition", "E7D6A9", 0.05f);       // 경량칸막이 (다른 벽과 구분)
        public static Material PartitionEdge => Solid("PartitionEdge", "B89B5C", 0.05f);

        // ───────── 바닥 (절차적 텍스처) ─────────
        public static Material FloorWood => Pbr(Textured("FloorWood", WoodTexture(), Color.white, 0.35f), OakPlanks, Vector2.one, 0.9f);   // 밝은 오크 마루 (PBR)
        public static Material FloorTileKitchen => Textured("FloorTileKitchen", TileTexture("tile_kitchen", "D9D2C3", "CFC7B6", "A79F8E", 2), Color.white, 0.45f);
        public static Material FloorTileEntry => Textured("FloorTileEntry", TileTexture("tile_entry_light", "C9CCCF", "C0C3C6", "9EA2A6", 2), Color.white, 0.5f);
        public static Material FloorTileUtility => Textured("FloorTileUtility", TileTexture("tile_utility", "9AA0A6", "90969C", "6E7378", 2), Color.white, 0.4f);
        public static Material FloorTileBath => Textured("FloorTileBath", TileTexture("tile_bath", "C9DCE3", "BBD0D9", "8FA9B4", 2), Color.white, 0.7f);
        public static Material WallTileBath => Textured("WallTileBath", TileTexture("tile_bathwall", "EEF4F6", "E4ECEF", "B9C7CC", 2), Color.white, 0.8f);
        public static Material FloorTileBalcony => Textured("FloorTileBalcony", TileTexture("tile_balcony", "B4A491", "A89887", "7D705F", 2), Color.white, 0.25f);
        public static Material FloorCorridor => Textured("FloorCorridor", TileTexture("tile_corridor", "A5A39E", "9B9994", "6D6B67", 2), Color.white, 0.55f);
        public static Material FloorShelter => Solid("FloorShelter", "8A8D91", 0.2f);

        // ───────── 가구 재질 ─────────
        public static Material Wood => Pbr(Solid("Wood", "C79E69", 0.35f), WoodGrain, Vector2.one, 0.22f);               // 오크
        public static Material WoodLight => Pbr(Solid("WoodLight", "DFC59B", 0.35f), WoodGrain, Vector2.one, 0.22f);
        public static Material WoodDark => Pbr(Solid("WoodDark", "6E5240", 0.35f), WoodGrain, Vector2.one, 0.22f);          // 월넛
        public static Material White => Solid("WhiteLaminate", "EFEDE7", 0.45f);
        public static Material FabricSofa => Pbr(Solid("FabricSofa", "8E9096", 0.05f), FabricWeave, new Vector2(4, 4), 0.45f);
        public static Material FabricSofaDark => Pbr(Solid("FabricSofaDark", "74777D", 0.05f), FabricWeave, new Vector2(4, 4), 0.45f);
        public static Material Bedding => Pbr(Solid("Bedding", "E9E4D8", 0.05f), FabricWeave, new Vector2(4, 4), 0.4f);
        public static Material Blanket => Pbr(Solid("Blanket", "4F6B8A", 0.05f), FabricWeave, new Vector2(4, 4), 0.45f);   // 슬레이트 블루 이불
        public static Material Pillow => Pbr(Solid("Pillow", "EFE9DC", 0.05f), FabricWeave, new Vector2(4, 4), 0.4f);
        public static Material Curtain => Pbr(Solid("Curtain", "B8A890", 0.02f), FabricWeave, new Vector2(3, 3), 0.35f);       // 그레이지
        public static Material CurtainSheer => Solid("CurtainSheer", "F1ECE2", 0.02f);
        public static Material Rug => Pbr(Solid("Rug", "B3AA9A", 0.02f), FabricWeave, new Vector2(3, 3), 0.45f);
        public static Material RugBorder => Pbr(Solid("RugBorder", "8F8575", 0.02f), FabricWeave, new Vector2(3, 3), 0.45f);
        public static Material Cloth => Solid("Cloth", "8E88A3", 0.02f);
        public static Material ClothAlt => Solid("ClothAlt", "D1A862", 0.02f);
        public static Material Metal => Pbr(Solid("Metal", "B9BDC3", 0.65f, 0.85f), SteelBrushed, Vector2.one, 0.1f);   // 브러시드 스틸
        public static Material MetalDark => Pbr(Solid("MetalDark", "2B2D31", 0.5f, 0.6f), SteelBrushed, Vector2.one, 0.1f);   // 블랙 금속(다리·프레임)
        public static Material Chrome => Solid("Chrome", "DADDE2", 0.9f, 1f);
        public static Material Plastic => Solid("Plastic", "EDEDED", 0.5f);
        public static Material PlasticBlack => Solid("PlasticBlack", "19191B", 0.45f);
        public static Material Rubber => Solid("Rubber", "2A2A2C", 0.1f);
        public static Material Ceramic => Solid("Ceramic", "F6F7F7", 0.85f);
        public static Material Stone => Pbr(Solid("StoneTop", "DAD6CE", 0.55f), MarbleWhite, Vector2.one, 0.1f);          // 대리석 상판
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
        public static Material Orange => Solid("Orange", "D98A55", 0.05f);
        public static Material BookA => Solid("BookA", "8A4A44", 0.1f);
        public static Material BookB => Solid("BookB", "4A6580", 0.1f);
        public static Material BookC => Solid("BookC", "5C7F6A", 0.1f);
        public static Material BookD => Solid("BookD", "C2A867", 0.1f);
        public static Material ArtA => Solid("ArtA", "8DA7B3", 0.05f);
        public static Material ArtB => Solid("ArtB", "CDA878", 0.05f);
        public static Material ArtC => Solid("ArtC", "A9B596", 0.05f);
        public static Material WaterBlue => Solid("WaterBlue", "5BA4E6", 0.8f);

        // ───────── PBR 세트 (절차적, Editor/Art/PbrTextureFactory) ─────────
        private static PbrTextureFactory.Set oak, grain, plaster, weave, leather, steel, marble;
        private static PbrTextureFactory.Set OakPlanks => oak ?? (oak = PbrTextureFactory.OakPlanks());
        private static PbrTextureFactory.Set WoodGrain => grain ?? (grain = PbrTextureFactory.WoodGrain());
        private static PbrTextureFactory.Set Plaster => plaster ?? (plaster = PbrTextureFactory.Plaster());
        private static PbrTextureFactory.Set FabricWeave => weave ?? (weave = PbrTextureFactory.FabricWeave());
        private static PbrTextureFactory.Set LeatherGrain => leather ?? (leather = PbrTextureFactory.LeatherGrain());
        private static PbrTextureFactory.Set SteelBrushed => steel ?? (steel = PbrTextureFactory.SteelBrushed());
        private static PbrTextureFactory.Set MarbleWhite => marble ?? (marble = PbrTextureFactory.MarbleWhite());

        /// <summary>가죽 (소파·의자용, Hero 모델에서 사용)</summary>
        public static Material Leather => Pbr(Solid("Leather", "6B5242", 0.3f), LeatherGrain, new Vector2(3, 3), 0.9f);

        private static readonly HashSet<string> mapped = new HashSet<string>();

        /// <summary>머티리얼에 PBR 맵(알베도·노멀·마스크)을 입힌다. 이미 입힌 머티리얼은 건너뛴다</summary>
        private static Material Pbr(Material m, PbrTextureFactory.Set s, Vector2 tiling, float normalScale)
        {
            if (m == null || s == null || s.albedo == null || mapped.Contains(m.name)) return m;
            m.SetTexture("_BaseMap", s.albedo); m.mainTexture = s.albedo;
            m.SetTexture("_BumpMap", s.normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", s.mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", s.mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_OcclusionStrength", 1f);
            m.SetFloat("_Smoothness", 1f);        // 스무스니스는 마스크 알파가 정한다
            m.SetTextureScale("_BaseMap", tiling); m.SetTextureOffset("_BaseMap", Vector2.zero);
            EditorUtility.SetDirty(m);
            mapped.Add(m.name);
            return m;
        }

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
