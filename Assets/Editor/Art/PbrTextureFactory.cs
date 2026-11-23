using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// 인테리어용 절차적 PBR 텍스처 세트 생성기 (외부 에셋·유료 텍스처를 쓰지 않고 코드로 만든다).
    /// 한 세트 = Albedo(sRGB) + Normal(NormalMap) + Mask(R 메탈릭 / G 오클루전 / A 스무스니스 — URP Lit 의 _MetallicGlossMap·_OcclusionMap 공용).
    /// 파일: Assets/Materials/Interior/Textures/Pbr/{키}_albedo.png / _normal.png / _mask.png (타일링, 512~1024).
    /// 같은 키의 파일이 이미 있으면 다시 만들지 않는다 (결정적 해시 기반이라 다시 만들어도 같은 결과). 재생성하려면 파일을 지운다.
    /// </summary>
    public static class PbrTextureFactory
    {
        public const string Dir = "Assets/Materials/Interior/Textures/Pbr";

        public sealed class Set { public Texture2D albedo, normal, mask; }

        /// <summary>한 점의 재질 값. 높이(0~1)는 노멀 맵 생성에 쓰인다</summary>
        private struct Texel { public Color albedo; public float height, smooth, metal, ao; }
        private delegate Texel Sampler(float u, float v, int x, int y, int size);

        // ═════════════════════════ 공개 세트 ═════════════════════════
        /// <summary>밝은 오크 마루: 폭 0.12m 판재(1m 에 8줄), 판 길이 0.55~1m 로 이음매가 어긋남, 판마다 색 차이, 길이 방향 결, 이음매 홈</summary>
        public static Set OakPlanks() => Build("oak_planks", 1024, (u, v, x, y, n) =>
        {
            const int cols = 8;
            float cu = u * cols; int col = Mathf.FloorToInt(cu); float fu = cu - col;          // 판 폭 방향
            float offset = H(col, 3);                                                          // 열마다 이음매 위치가 다름
            float hl = H(col, 9);
            int k = hl < 0.4f ? 1 : (hl < 0.8f ? 2 : 3);                                       // 판 길이 = 1/k m (1.0 / 0.5 / 0.33 — 타일링이 이어지도록 정수 분할)
            float len = 1f / k;
            float pv = (v + offset) * k; int seg = Mathf.FloorToInt(pv); float fv = pv - seg;  // 판 길이 방향
            int segm = ((seg % k) + k) % k;                                                    // 타일 경계에서 같은 판이 이어지도록 k 로 나눈 나머지
            float tone = H(col * 31 + segm, segm + 7 + k * 11);
            Color a = Hex("E4CBA2"), b = Hex("D2B284");
            Color c = Color.Lerp(a, b, tone * 0.8f);
            // 결: 판 길이 방향으로 길게 늘어난 노이즈 + 연륜 줄무늬
            float grain = Fbm(u * 60f + col * 13.1f, v * 3f + segm * 5.3f + col * 7.7f, 4, 60, 3);
            float ring = Mathf.Sin((fu * 9f + grain * 4f + tone * 6f) * Mathf.PI) * 0.5f + 0.5f;
            float g = (grain - 0.5f) * 0.18f + (ring - 0.5f) * 0.07f;
            c = new Color(c.r + g, c.g + g * 0.95f, c.b + g * 0.85f, 1f);
            // 이음매 홈 (판 사이 · 판 끝) + 모서리 살짝 둥글게
            float edgeX = Mathf.Min(fu, 1f - fu) * (1f / cols) * 1.0f;                          // m 단위 거리 (텍스처 1m)
            float edgeY = Mathf.Min(fv, 1f - fv) * len;
            float groove = Mathf.Clamp01(1f - Mathf.Min(edgeX, edgeY) / 0.0022f);               // 약 2mm 폭
            float h = 0.55f + (grain - 0.5f) * 0.10f - groove * 0.5f;
            c *= 1f - groove * 0.35f;
            return new Texel { albedo = c, height = h, smooth = Mathf.Lerp(0.46f, 0.30f, grain * 0.6f) * (1f - groove * 0.8f), metal = 0f, ao = 1f - groove * 0.7f };
        }, 0.9f);

        /// <summary>가구용 중성 나뭇결 (색은 머티리얼 틴트로 입힌다): 길이(U)방향 결, 연륜</summary>
        public static Set WoodGrain() => Build("wood_grain", 512, (u, v, x, y, n) =>
        {
            float warp = Fbm(u * 4f, v * 2f, 3, 4, 2) * 2.2f;
            float rings = Mathf.Sin((v * 14f + warp * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            float fine = Fbm(u * 80f, v * 5f, 4, 80, 5);
            float tone = 0.88f + (rings - 0.5f) * 0.07f + (fine - 0.5f) * 0.08f;
            return new Texel { albedo = new Color(tone, tone * 0.985f, tone * 0.96f, 1f), height = 0.5f + (fine - 0.5f) * 0.35f + (rings - 0.5f) * 0.1f, smooth = 0.38f + fine * 0.06f, metal = 0f, ao = 1f };
        }, 0.6f);

        /// <summary>벽 플라스터/도장: 거의 흰색에 아주 미세한 얼룩과 요철</summary>
        public static Set Plaster() => Build("plaster", 512, (u, v, x, y, n) =>
        {
            float m = Fbm(u * 6f, v * 6f, 4, 6, 6);
            float fine = Fbm(u * 70f, v * 70f, 2, 70, 70);
            float t = 0.965f + (m - 0.5f) * 0.05f + (fine - 0.5f) * 0.025f;
            return new Texel { albedo = new Color(t, t, t * 0.995f, 1f), height = 0.5f + (fine - 0.5f) * 0.6f + (m - 0.5f) * 0.2f, smooth = 0.06f, metal = 0f, ao = 1f };
        }, 0.35f);

        /// <summary>직물: 날실·씨실이 위아래로 교차하는 평직 (가로세로 64올). 색은 틴트로</summary>
        public static Set FabricWeave() => Build("fabric_weave", 512, (u, v, x, y, n) =>
        {
            const int threads = 64;
            float tu = u * threads, tv = v * threads;
            int iu = Mathf.FloorToInt(tu), iv = Mathf.FloorToInt(tv);
            float fu = tu - iu, fv = tv - iv;
            bool warpOver = ((iu + iv) & 1) == 0;                                              // 위에 놓인 올
            float yarn = warpOver ? Mathf.Sin(fu * Mathf.PI) : Mathf.Sin(fv * Mathf.PI);       // 올의 단면 높이
            float cross = warpOver ? Mathf.Sin(fv * Mathf.PI) : Mathf.Sin(fu * Mathf.PI);
            float h = Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(yarn * (0.55f + 0.45f * cross)));
            float noise = (H(iu, iv) - 0.5f) * 0.10f;
            float t = Mathf.Lerp(0.72f, 1f, h) + noise;
            return new Texel { albedo = new Color(t, t, t, 1f), height = h, smooth = 0.05f, metal = 0f, ao = Mathf.Lerp(0.7f, 1f, h) };
        }, 0.9f);

        /// <summary>가죽: 보로노이 셀 주름(자갈 무늬)</summary>
        public static Set LeatherGrain() => Build("leather_grain", 512, (u, v, x, y, n) =>
        {
            Vector2 vc = Voronoi(u * 36f, v * 36f, 36);
            float edge = vc.y - vc.x;                                                          // 셀 경계에서 0
            float h = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge * 2.2f));
            float t = Mathf.Lerp(0.78f, 1f, h) + (Fbm(u * 90f, v * 90f, 2, 90, 90) - 0.5f) * 0.06f;
            return new Texel { albedo = new Color(t, t, t, 1f), height = h, smooth = Mathf.Lerp(0.25f, 0.42f, h), metal = 0f, ao = Mathf.Lerp(0.75f, 1f, h) };
        }, 0.7f);

        /// <summary>브러시드 스틸: 가로 방향으로 길게 늘어진 미세 스크래치. 메탈릭 1</summary>
        public static Set SteelBrushed() => Build("steel_brushed", 512, (u, v, x, y, n) =>
        {
            float streak = Fbm(u * 3f, v * 220f, 3, 3, 220);
            float fine = Fbm(u * 8f, v * 500f, 2, 8, 500);
            float t = 0.80f + (streak - 0.5f) * 0.16f + (fine - 0.5f) * 0.06f;
            return new Texel { albedo = new Color(t, t, t * 1.01f, 1f), height = 0.5f + (streak - 0.5f) * 0.18f + (fine - 0.5f) * 0.1f, smooth = 0.5f + (streak - 0.5f) * 0.07f, metal = 1f, ao = 1f };
        }, 0.35f);

        /// <summary>흰 대리석: 뒤틀린 노이즈로 만든 맥</summary>
        public static Set MarbleWhite() => Build("marble_white", 512, (u, v, x, y, n) =>
        {
            float warp = Fbm(u * 3f, v * 3f, 4, 3, 3);
            float vein = Mathf.Abs(Mathf.Sin((u * 2f + v * 1f + warp * 2.6f) * Mathf.PI));
            float vein2 = Mathf.Abs(Mathf.Sin((u * 5f - v * 3f + warp * 3.4f) * Mathf.PI));
            float lines = Mathf.Pow(1f - vein, 14f) * 0.35f + Mathf.Pow(1f - vein2, 20f) * 0.2f;
            float t = 0.93f - lines + (Fbm(u * 18f, v * 18f, 3, 18, 18) - 0.5f) * 0.04f;
            return new Texel { albedo = new Color(t, t, t * 0.985f, 1f), height = 0.5f + (Fbm(u * 40f, v * 40f, 2, 40, 40) - 0.5f) * 0.05f, smooth = 0.72f - lines * 0.3f, metal = 0f, ao = 1f };
        }, 0.1f);

        // ═════════════════════════ 생성 · 저장 ═════════════════════════
        private static Set Build(string key, int size, Sampler sampler, float normalStrength)
        {
            Directory.CreateDirectory(Dir);
            string pa = $"{Dir}/{key}_albedo.png", pn = $"{Dir}/{key}_normal.png", pm = $"{Dir}/{key}_mask.png";
            if (!(File.Exists(pa) && File.Exists(pn) && File.Exists(pm)))
            {
                var tex = new Texel[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        tex[y * size + x] = sampler((x + 0.5f) / size, (y + 0.5f) / size, x, y, size);

                var alb = new Color32[size * size]; var nrm = new Color32[size * size]; var msk = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        alb[i] = tex[i].albedo;
                        // 노멀: 높이 맵의 중앙 차분 (타일링을 위해 가장자리는 래핑)
                        float hl = tex[y * size + (x + size - 1) % size].height, hr = tex[y * size + (x + 1) % size].height;
                        float hd = tex[((y + size - 1) % size) * size + x].height, hu = tex[((y + 1) % size) * size + x].height;
                        var nv = new Vector3((hl - hr) * normalStrength * size * 0.02f, (hd - hu) * normalStrength * size * 0.02f, 1f).normalized;
                        nrm[i] = new Color32((byte)((nv.x * 0.5f + 0.5f) * 255f), (byte)((nv.y * 0.5f + 0.5f) * 255f), (byte)((nv.z * 0.5f + 0.5f) * 255f), 255);
                        msk[i] = new Color32((byte)(Mathf.Clamp01(tex[i].metal) * 255f), (byte)(Mathf.Clamp01(tex[i].ao) * 255f), 0, (byte)(Mathf.Clamp01(tex[i].smooth) * 255f));
                    }
                Save(pa, alb, size); Save(pn, nrm, size); Save(pm, msk, size);
                AssetDatabase.ImportAsset(pa); AssetDatabase.ImportAsset(pn); AssetDatabase.ImportAsset(pm);
                Configure(pa, false, false); Configure(pn, true, false); Configure(pm, false, true);
            }
            return new Set
            {
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(pa),
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(pn),
                mask = AssetDatabase.LoadAssetAtPath<Texture2D>(pm),
            };
        }

        private static void Save(string path, Color32[] px, int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.SetPixels32(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        private static void Configure(string path, bool normalMap, bool linear)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normalMap && !linear;
            imp.alphaSource = linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 1024;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        // ═════════════════════════ 노이즈 ═════════════════════════
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }

        /// <summary>결정적 해시 (0~1)</summary>
        private static float H(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static int Wrap(int i, int period) => period <= 0 ? i : ((i % period) + period) % period;

        /// <summary>주기 (perX, perY) 로 래핑되는 값 노이즈 → 텍스처를 타일링해도 이음선이 없다</summary>
        private static float ValueNoise(float x, float y, int perX, int perY)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            int x0 = Wrap(xi, perX), x1 = Wrap(xi + 1, perX), y0 = Wrap(yi, perY), y1 = Wrap(yi + 1, perY);
            float a = H(x0, y0), b = H(x1, y0), c = H(x0, y1), d = H(x1, y1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>fBm (0~1 근처, 주기 래핑). 입력 x,y 는 (u*perX, v*perY) 꼴로 넣는다 — 옥타브마다 주파수·주기가 2배</summary>
        private static float Fbm(float x, float y, int octaves, int perX, int perY)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++) { sum += ValueNoise(x, y, perX, perY) * amp; norm += amp; x *= 2f; y *= 2f; perX *= 2; perY *= 2; amp *= 0.5f; }
            return sum / norm;
        }

        /// <summary>보로노이: (가장 가까운 점까지 거리, 두 번째로 가까운 점까지 거리). period 칸마다 래핑</summary>
        private static Vector2 Voronoi(float x, float y, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j;
                    int wx = ((cx % period) + period) % period, wy = ((cy % period) + period) % period;
                    float px = cx + H(wx, wy * 3 + 1), py = cy + H(wx * 5 + 2, wy);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
            return new Vector2(d1, d2);
        }
    }
}
