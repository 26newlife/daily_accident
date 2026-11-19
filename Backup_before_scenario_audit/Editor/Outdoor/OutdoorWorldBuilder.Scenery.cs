using GameGameplay;
using UnityEditor;
using UnityEngine;
using Prim = GameUI.EditorTools.Interior.Prim;
using OM = GameUI.EditorTools.Outdoor.OutdoorMaterials;

namespace GameUI.EditorTools.Outdoor
{
    public static partial class OutdoorWorldBuilder
    {
        // ═════════════════════════ 원경: 산 (배산) ═════════════════════════
        /// <summary>능선 산 메시: 폭(x) x 깊이(z). v=0(-z쪽)이 높고 v=1(+z쪽)로 완만하게 내려오는 사면. 노이즈로 능선이 울퉁불퉁</summary>
        private static Mesh MountainMesh(string name, float width, float depth, float height, int seed, int nx = 64, int nz = 14)
        {
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx, v = j / (float)nz;
                    float ridge = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 0.98f + 0.01f)), 0.55f);                 // 양 끝으로 낮아지는 능선
                    float noise = 0.65f + 0.55f * Mathf.PerlinNoise(u * 7f + seed, v * 2.5f + seed * 0.37f)
                                  + 0.22f * Mathf.PerlinNoise(u * 21f + seed * 1.7f, v * 6f);
                    float slope = 1f - Mathf.SmoothStep(0.05f, 0.98f, v);                                                      // 앞(+z)쪽으로 낮아짐
                    float h = height * ridge * noise * Mathf.Lerp(0.25f, 1f, slope) * slope;
                    verts[j * (nx + 1) + i] = new Vector3((u - 0.5f) * width, h, (v - 0.5f) * depth);
                    uvs[j * (nx + 1) + i] = new Vector2(u * 8f, v * 3f);
                }
            var tris = new int[nx * nz * 6];
            int k = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + (nx + 1), d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d;   // 위(+y)를 향하는 앞면
                }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void Mountain(Transform parent, string name, Vector3 pos, float yaw, float w, float d, float h, int seed, string hex, float distance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = MountainMesh(name, w, d, h, seed);
            Color c = BuildingFacadeGenerator.Atmosphere(OM.Hex(hex), HazeColor, distance, 1100f);
            go.AddComponent<MeshRenderer>().sharedMaterial = OM.Lit("Mtn_" + name, c, 0.05f);
        }

        private static void BuildMountains()
        {
            Transform t = Group("Mountains", root);
            float baseY = GroundY - 4f;
            // 배산: 건물 뒤(북쪽, -z). 사면이 +z(건물 쪽)로 내려온다. 숲이 짙은 능선
            Mountain(t, "BackRidge_Near", new Vector3(0f, baseY, -430f), 0f, 1700f, 360f, 260f, 3, "2F5B3A", 430f);
            Mountain(t, "BackRidge_Far", new Vector3(-250f, baseY, -820f), 0f, 2200f, 420f, 420f, 9, "46705A", 820f);
            // 옆(동쪽, +x) 능선: 사면이 -x 쪽으로 내려온다 (주방 창에서 산 일부가 보임)
            Mountain(t, "EastRidge", new Vector3(560f, baseY, 20f), -90f, 900f, 260f, 170f, 5, "3C6B45", 560f);
            // 임수 건너편: 낮은 산 (사면이 -z 쪽, 베란다 정면에서 보임). 안개로 푸르스름하게
            Mountain(t, "FarHills_Near", new Vector3(0f, baseY, 760f), 180f, 1800f, 300f, 120f, 12, "5B8A5E", 760f);
            Mountain(t, "FarHills_Far", new Vector3(150f, baseY, 1050f), 180f, 2400f, 360f, 230f, 17, "6F93A8", 1050f);
        }

        // ═════════════════════════ 임수: 하천 ═════════════════════════
        private static void BuildRiver()
        {
            Transform t = Group("River", root);
            float wy = GroundY - 0.9f;
            // 물: 높은 Smoothness + 살짝 금속성으로 하늘/해 반사 느낌
            Material water = OM.Lit("Water", new Color(0.24f, 0.42f, 0.52f), 0.96f, 0.15f);
            Material bank = OM.Lit("RiverBank", OM.Hex("6B5E48"), 0.05f);
            Material farBank = OM.Lit("FarBank", BuildingFacadeGenerator.Atmosphere(OM.Hex("6FA854"), HazeColor, 450f, 1100f), 0.05f);
            Flat(t, "Water", -1800f, 1800f, 300f, 400f, wy, water);
            Prim.Quad(t, "BankWall", new Vector3(-1800f, wy, 300f), Vector3.right, 3600f, Vector3.up, 0.9f, Vector3.forward, bank, 1f);
            Flat(t, "FarBank", -1800f, 1800f, 400f, 700f, GroundY - 0.4f, farBank);
            Flat(t, "FarBankPath", -1800f, 1800f, 412f, 415f, GroundY - 0.35f, OM.Lit("FarPath", OM.Hex("B8B09A"), 0.1f));
        }

        // ═════════════════════════ 하늘: 구름 ═════════════════════════
        private static void BuildClouds()
        {
            Transform t = Group("Clouds", root);
            const int w = 256, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - w * 0.5f) / (w * 0.5f), ny = (y - h * 0.5f) / (h * 0.5f);
                    float blob = Mathf.Clamp01(1f - (nx * nx * 0.9f + ny * ny * 2.4f));
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.08f) * 0.6f + 0.4f;
                    float a = Mathf.SmoothStep(0f, 1f, blob * n * 1.6f);
                    px[y * w + x] = new Color(1f, 1f, 1f, a * 0.85f);
                }
            tex.SetPixels(px); tex.Apply();
            Texture2D cloudTex = OM.SaveTexture("cloud", tex, true, true, TextureWrapMode.Clamp);
            Material mat = OM.Unlit("Cloud", new Color(1f, 0.99f, 0.97f, 1f), cloudTex, true);
            for (int i = 0; i < 9; i++)
            {
                float sx = Rand(250f, 700f), sy = sx * 0.4f;
                float cx = Rand(-1000f, 1000f), cy = Rand(160f, 320f), cz = Rand(500f, 1100f);
                GameObject q = Prim.Quad(t, "Cloud", new Vector3(cx - sx * 0.5f, cy, cz), Vector3.right, sx, Vector3.up, sy, Vector3.back, mat, 1f);
                // 쿼드 UV 는 0~1 로 덮어야 하므로 타일 크기를 크기에 맞춘다
                var mf = q.GetComponent<MeshFilter>();
                mf.sharedMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                var drift = q.AddComponent<CloudDrift>();
                var so = new SerializedObject(drift);
                so.FindProperty("velocity").vector3Value = new Vector3(Rand(0.8f, 2.4f), 0f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
