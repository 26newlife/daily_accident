using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 창밖 아파트 동의 외벽 메시/텍스처를 절차적으로 만드는 도우미 (에디터 빌더가 사용, 런타임에서도 호출 가능).
    /// 메시의 UV 는 "미터 / (칸 수 x 칸 크기)" 라서 텍스처 1장이 가로 tileBays 칸 x 세로 tileFloors 층을 덮고 반복된다.
    /// </summary>
    public static class BuildingFacadeGenerator
    {
        /// <summary>사방 외벽 메시 (지붕 제외). 원점 = 바닥 중앙, 높이는 +Y</summary>
        public static Mesh CreateTowerMesh(string name, float width, float depth, float height, float bayWidth, float floorHeight, int tileBays = 8, int tileFloors = 8)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();

            void Side(Vector3 origin, Vector3 along, float length, Vector3 outward)
            {
                int i = verts.Count;
                Vector3 up = Vector3.up * height;
                Vector3 a = along.normalized * length;
                verts.Add(origin); verts.Add(origin + a); verts.Add(origin + a + up); verts.Add(origin + up);
                float u = length / (bayWidth * tileBays), v = height / (floorHeight * tileFloors);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(u, 0)); uvs.Add(new Vector2(u, v)); uvs.Add(new Vector2(0, v));
                // 앞면 법선 = cross(b-a, c-a). 바깥을 향하게 감김 선택
                bool ok = Vector3.Dot(Vector3.Cross(a, up), outward) >= 0f;
                if (ok) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            float hx = width * 0.5f, hz = depth * 0.5f;
            Side(new Vector3(-hx, 0, -hz), Vector3.right, width, Vector3.back);     // 남쪽 면 (-z)
            Side(new Vector3(hx, 0, -hz), Vector3.forward, depth, Vector3.right);   // 동쪽 면
            Side(new Vector3(hx, 0, hz), Vector3.left, width, Vector3.forward);     // 북쪽 면
            Side(new Vector3(-hx, 0, hz), Vector3.back, depth, Vector3.left);       // 서쪽 면

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 외벽 텍스처 (cells x cells 칸). 칸마다 창문 + 발코니 난간, 일부는 불이 켜져 있고(아침이라 소수), 일부는 빨래/에어컨 실외기가 있다.
        /// 벽 색은 밝은 회색이라 머티리얼 색(_BaseColor)을 곱해서 동마다 다른 외벽 색을 낸다.
        /// </summary>
        public static Texture2D CreateFacadeTexture(int seed, int cells = 8, int cellPx = 64, float litRatio = 0.16f)
        {
            int size = cells * cellPx;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            var rng = new System.Random(seed);
            Color wall = new Color(0.93f, 0.92f, 0.89f), wallDark = new Color(0.82f, 0.81f, 0.79f);
            for (int i = 0; i < px.Length; i++) px[i] = wall;

            Color[] laundry = { new Color(0.9f, 0.4f, 0.4f), new Color(0.4f, 0.6f, 0.9f), new Color(0.95f, 0.9f, 0.6f), new Color(0.95f, 0.95f, 0.95f) };
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    int x0 = cx * cellPx, y0 = cy * cellPx;
                    bool lit = rng.NextDouble() < litRatio;
                    bool curtain = !lit && rng.NextDouble() < 0.3;
                    // 창문 (셀의 가로 20~80%, 세로 30~82%)
                    int wx0 = x0 + (int)(cellPx * 0.2f), wx1 = x0 + (int)(cellPx * 0.8f);
                    int wy0 = y0 + (int)(cellPx * 0.3f), wy1 = y0 + (int)(cellPx * 0.82f);
                    for (int y = wy0; y < wy1; y++)
                        for (int x = wx0; x < wx1; x++)
                        {
                            float t = (y - wy0) / (float)(wy1 - wy0);
                            Color glass = Color.Lerp(new Color(0.16f, 0.22f, 0.30f), new Color(0.42f, 0.55f, 0.68f), t);   // 위쪽은 하늘이 비친다
                            if (lit) glass = Color.Lerp(new Color(1f, 0.88f, 0.6f), new Color(0.95f, 0.75f, 0.45f), t);
                            else if (curtain && x > (wx0 + wx1) / 2) glass = new Color(0.85f, 0.8f, 0.7f);
                            px[y * size + x] = glass;
                        }
                    // 창틀 가운데 세로살
                    int mid = (wx0 + wx1) / 2;
                    for (int y = wy0; y < wy1; y++) px[y * size + mid] = wallDark;
                    // 발코니 난간 (창 아래 띠)
                    int ry0 = y0 + (int)(cellPx * 0.08f), ry1 = y0 + (int)(cellPx * 0.3f);
                    for (int y = ry0; y < ry1; y++)
                        for (int x = x0 + 3; x < x0 + cellPx - 3; x++)
                            px[y * size + x] = (y == ry1 - 1 || y == ry0) ? new Color(0.45f, 0.47f, 0.5f) : Color.Lerp(wall, new Color(0.7f, 0.74f, 0.78f), 0.5f);
                    // 빨래
                    if (rng.NextDouble() < 0.12)
                        for (int k = 0; k < 3; k++)
                        {
                            Color c = laundry[rng.Next(laundry.Length)];
                            int lx = x0 + (int)(cellPx * (0.22f + 0.2f * k));
                            for (int y = ry0 + 1; y < ry1 - 1; y++) for (int x = lx; x < lx + 6; x++) px[y * size + x] = c;
                        }
                    // 에어컨 실외기
                    if (rng.NextDouble() < 0.15)
                        for (int y = ry0 + 1; y < ry1 - 1; y++)
                            for (int x = x0 + (int)(cellPx * 0.62f); x < x0 + (int)(cellPx * 0.85f); x++) px[y * size + x] = new Color(0.78f, 0.8f, 0.82f);
                    // 층 경계선
                    for (int x = x0; x < x0 + cellPx; x++) px[y0 * size + x] = wallDark;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// 안개(대기) 색상: 멀수록 하늘색(haze) 쪽으로 섞는다. 씬 포그는 SmokeAtmosphere 가 매 프레임 끄므로 재질 색으로 원근감을 낸다.
        /// </summary>
        public static Color Atmosphere(Color baseColor, Color haze, float distance, float fullHazeDistance = 900f)
            => Color.Lerp(baseColor, haze, Mathf.Clamp01(distance / fullHazeDistance) * 0.85f);
    }
}
