using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// Hero 모델용 절차적 메시 도구 (외부 에셋·패키지 없음).
    ///   · RoundedBox : 모서리를 둥글게 깎은 박스 (베벨 — 하이라이트가 모서리에서 살아난다)
    ///   · Lathe      : 단면(반지름, 높이)을 회전시킨 형상 (손잡이·다이얼·플러그 핀·받침)
    ///   · Tube       : 경로를 따라가는 가는 관 (전선·코드)
    /// 모든 형상은 같은 재질끼리 하나의 메시로 합쳐 드로우콜을 줄인다 (HeroBuilder).
    /// UV 는 미터 단위(면 투영)라 PBR 타일 텍스처가 크기에 맞게 보인다.
    /// 규칙: 단위 m, 피벗은 프리팹 중앙(호출자가 정한다), 로컬 +Z = 앞면.
    /// </summary>
    public sealed class HeroMeshData
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
        public int Triangles => t.Count / 3;

        /// <summary>다른 형상을 변환해 붙인다 (법선은 회전/균일 스케일 가정)</summary>
        public void Append(HeroMeshData o, Matrix4x4 m)
        {
            int b = v.Count;
            for (int i = 0; i < o.v.Count; i++)
            {
                v.Add(m.MultiplyPoint3x4(o.v[i]));
                n.Add(m.MultiplyVector(o.n[i]).normalized);
                uv.Add(o.uv[i]);
            }
            bool mirrored = m.determinant < 0f;   // 음수 스케일이면 감김을 뒤집는다
            for (int i = 0; i < o.t.Count; i += 3)
            {
                if (mirrored) { t.Add(b + o.t[i]); t.Add(b + o.t[i + 2]); t.Add(b + o.t[i + 1]); }
                else { t.Add(b + o.t[i]); t.Add(b + o.t[i + 1]); t.Add(b + o.t[i + 2]); }
            }
        }

        /// <summary>각 삼각형을 정점 법선과 비교해 거꾸로 감긴 것을 뒤집는다 (회전체·관 생성 후 한 번)</summary>
        public void FixWinding()
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 g = Vector3.Cross(b - a, c - a);
                Vector3 avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(g, avg) < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    public static class HeroMeshKit
    {
        // ═════════════════════════ 둥근 박스 ═════════════════════════
        /// <summary>size = 가로·세로·깊이(m), radius = 모서리 반지름, seg = 모서리 분할 수(1=각진 챔퍼, 2~3=둥근 모서리)</summary>
        public static HeroMeshData RoundedBox(Vector3 size, float radius, int seg = 2)
        {
            var d = new HeroMeshData();
            Vector3 h = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
            int segs = Mathf.Max(1, seg);
            var s = new float[3][];
            for (int a = 0; a < 3; a++) s[a] = Samples(h[a], r, segs);
            Vector3 lim = new Vector3(h.x - r, h.y - r, h.z - r);

            for (int a = 0; a < 3; a++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (a + 1) % 3, w = (a + 2) % 3;
                    int nu = s[u].Length, nw = s[w].Length;
                    int baseIdx = d.v.Count;
                    for (int i = 0; i < nu; i++)
                        for (int j = 0; j < nw; j++)
                        {
                            Vector3 p = Vector3.zero;
                            p[a] = sign * h[a]; p[u] = s[u][i]; p[w] = s[w][j];
                            Vector3 inner = new Vector3(Mathf.Clamp(p.x, -lim.x, lim.x), Mathf.Clamp(p.y, -lim.y, lim.y), Mathf.Clamp(p.z, -lim.z, lim.z));
                            Vector3 dir = p - inner;
                            Vector3 nrm, pos;
                            if (dir.sqrMagnitude < 1e-12f) { nrm = Vector3.zero; nrm[a] = sign; pos = p; }
                            else { nrm = dir.normalized; pos = inner + nrm * r; }
                            d.v.Add(pos); d.n.Add(nrm); d.uv.Add(new Vector2(p[u], p[w]));
                        }
                    for (int i = 0; i < nu - 1; i++)
                        for (int j = 0; j < nw - 1; j++)
                        {
                            int a0 = baseIdx + i * nw + j, a1 = a0 + 1, b0 = a0 + nw, b1 = b0 + 1;
                            // 유니티 앞면 법선 = cross(b-a, c-a). u×w = +축(a)
                            if (sign > 0) { d.t.Add(a0); d.t.Add(b0); d.t.Add(b1); d.t.Add(a0); d.t.Add(b1); d.t.Add(a1); }
                            else { d.t.Add(a0); d.t.Add(b1); d.t.Add(b0); d.t.Add(a0); d.t.Add(a1); d.t.Add(b1); }
                        }
                }
            return d;
        }

        private static float[] Samples(float h, float r, int segs)
        {
            var l = new List<float>();
            for (int i = 0; i <= segs; i++) l.Add(-h + r * i / segs);
            for (int i = 0; i <= segs; i++) l.Add(h - r + r * i / segs);
            return l.ToArray();
        }

        // ═════════════════════════ 회전체 ═════════════════════════
        /// <summary>
        /// 단면 점(반지름, 높이)을 Y 축으로 회전. 점은 아래→위, 안→밖 순서 (바닥 중심(0,y0)에서 시작하면 바닥이 막힌다).
        /// 꺾이는 모서리(40° 이상)는 법선을 나눠 각지게, 완만한 곳은 매끈하게 처리한다.
        /// </summary>
        public static HeroMeshData Lathe(IList<Vector2> prof, int sides = 20)
        {
            var d = new HeroMeshData();
            int segCount = prof.Count - 1;
            var sn = new Vector2[segCount];
            for (int k = 0; k < segCount; k++)
            {
                Vector2 tg = prof[k + 1] - prof[k];
                sn[k] = new Vector2(tg.y, -tg.x).normalized;   // 위로 올라가며 +r 이 바깥이면 바깥 법선
            }
            float rMax = 0f; foreach (Vector2 p in prof) rMax = Mathf.Max(rMax, p.x);
            float circ = 2f * Mathf.PI * Mathf.Max(rMax, 0.001f);
            float vAcc = 0f;
            for (int k = 0; k < segCount; k++)
            {
                Vector2 p0 = prof[k], p1 = prof[k + 1];
                Vector2 n0 = sn[k], n1 = sn[k];
                if (k > 0 && Vector2.Angle(sn[k], sn[k - 1]) < 40f) n0 = (sn[k] + sn[k - 1]).normalized;
                if (k < segCount - 1 && Vector2.Angle(sn[k], sn[k + 1]) < 40f) n1 = (sn[k] + sn[k + 1]).normalized;
                float segLen = (p1 - p0).magnitude;
                int b = d.v.Count;
                for (int j = 0; j <= sides; j++)
                {
                    float ang = 2f * Mathf.PI * j / sides;
                    float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    d.v.Add(new Vector3(p0.x * c, p0.y, p0.x * s)); d.n.Add(new Vector3(n0.x * c, n0.y, n0.x * s)); d.uv.Add(new Vector2(circ * j / sides, vAcc));
                    d.v.Add(new Vector3(p1.x * c, p1.y, p1.x * s)); d.n.Add(new Vector3(n1.x * c, n1.y, n1.x * s)); d.uv.Add(new Vector2(circ * j / sides, vAcc + segLen));
                }
                for (int j = 0; j < sides; j++)
                {
                    int a0 = b + j * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                    d.t.Add(a0); d.t.Add(a1); d.t.Add(b1); d.t.Add(a0); d.t.Add(b1); d.t.Add(b0);
                }
                vAcc += segLen;
            }
            d.FixWinding();
            return d;
        }

        /// <summary>끝을 살짝 깎은(챔퍼) 원기둥. 축 = Y, 중심 = 원점</summary>
        public static HeroMeshData Cylinder(float radius, float height, float chamfer = 0.002f, int sides = 18)
        {
            float hh = height * 0.5f, c = Mathf.Min(chamfer, radius * 0.5f, hh * 0.5f);
            var prof = new List<Vector2>
            {
                new Vector2(0f, -hh), new Vector2(radius - c, -hh), new Vector2(radius, -hh + c),
                new Vector2(radius, hh - c), new Vector2(radius - c, hh), new Vector2(0f, hh),
            };
            return Lathe(prof, sides);
        }

        // ═════════════════════════ 관 (전선) ═════════════════════════
        public static HeroMeshData Tube(IList<Vector3> path, float radius, int sides = 8)
        {
            var d = new HeroMeshData();
            int cnt = path.Count;
            Vector3 prevN = Vector3.zero;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 tg = (i == 0 ? path[1] - path[0] : i == cnt - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]).normalized;
                Vector3 nrm;
                if (i == 0) nrm = Vector3.Cross(tg, Mathf.Abs(tg.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                else { nrm = prevN - Vector3.Dot(prevN, tg) * tg; nrm = nrm.sqrMagnitude < 1e-8f ? Vector3.Cross(tg, Vector3.up).normalized : nrm.normalized; }
                prevN = nrm;
                Vector3 bin = Vector3.Cross(tg, nrm);
                for (int j = 0; j <= sides; j++)
                {
                    float ang = 2f * Mathf.PI * j / sides;
                    Vector3 dir = nrm * Mathf.Cos(ang) + bin * Mathf.Sin(ang);
                    d.v.Add(path[i] + dir * radius); d.n.Add(dir); d.uv.Add(new Vector2(j / (float)sides * 2f * Mathf.PI * radius, i * 0.02f));
                }
            }
            int ring = sides + 1;
            for (int i = 0; i < cnt - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * ring + j, a1 = a0 + 1, b0 = a0 + ring, b1 = b0 + 1;
                    d.t.Add(a0); d.t.Add(b0); d.t.Add(b1); d.t.Add(a0); d.t.Add(b1); d.t.Add(a1);
                }
            d.FixWinding();
            return d;
        }

        /// <summary>Catmull-Rom 으로 제어점 사이를 부드럽게 이은 점 목록 (전선 곡선용)</summary>
        public static List<Vector3> Smooth(IList<Vector3> pts, int perSegment = 6)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                for (int k = 0; k < perSegment; k++)
                {
                    float t = k / (float)perSegment, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }
    }

    /// <summary>
    /// 형상들을 재질별로 모아 프리팹 한 개로 만든다.
    ///   var b = new HeroBuilder(); b.Box(mat, center, size, r); ... b.Save("Hero_Microwave", out tris);
    /// 메시는 Assets/Prefabs/Furniture/Hero/Meshes/ 에 에셋으로 저장하고 (같은 이름이면 내용만 갱신 → GUID 유지),
    /// 프리팹은 Assets/Prefabs/Furniture/Hero/ 에 저장한다.
    /// </summary>
    public sealed class HeroBuilder
    {
        public const string HeroDir = "Assets/Prefabs/Furniture/Hero";
        public const string MeshDir = HeroDir + "/Meshes";

        private readonly Dictionary<Material, HeroMeshData> groups = new Dictionary<Material, HeroMeshData>();
        private readonly List<Material> order = new List<Material>();

        public void Add(Material m, HeroMeshData d, Vector3 pos, Vector3 euler = default, Vector3? scale = null)
        {
            if (!groups.TryGetValue(m, out HeroMeshData g)) { g = new HeroMeshData(); groups[m] = g; order.Add(m); }
            g.Append(d, Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale ?? Vector3.one));
        }

        public void Box(Material m, Vector3 center, Vector3 size, float radius = 0.004f, Vector3 euler = default, int seg = 2)
            => Add(m, HeroMeshKit.RoundedBox(size, radius, seg), center, euler);

        public void Cyl(Material m, Vector3 center, float radius, float height, Vector3 euler = default, float chamfer = 0.002f, int sides = 18)
            => Add(m, HeroMeshKit.Cylinder(radius, height, chamfer, sides), center, euler);

        public void Lathe(Material m, Vector3 center, IList<Vector2> prof, Vector3 euler = default, int sides = 20)
            => Add(m, HeroMeshKit.Lathe(prof, sides), center, euler);

        public void Tube(Material m, IList<Vector3> path, float radius, int sides = 8)
            => Add(m, HeroMeshKit.Tube(path, radius, sides), Vector3.zero);

        public int TriangleCount { get { int c = 0; foreach (var g in groups.Values) c += g.Triangles; return c; } }

        /// <summary>프리팹(자식 = 재질별 메시)을 저장하고 에셋 경로를 돌려준다</summary>
        public string Save(string prefabName, bool castShadowsOnGlass = false)
        {
            EnsureFolder(HeroDir); EnsureFolder(MeshDir);
            var root = new GameObject(prefabName);
            foreach (Material m in order)
            {
                string safe = m.name.Replace("Int_", "").Replace("Hero_", "");
                Mesh mesh = groups[m].ToMesh(prefabName + "_" + safe);
                mesh = SaveMesh(mesh, $"{MeshDir}/{prefabName}_{safe}.asset");
                var go = new GameObject("mesh_" + safe, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                if (m.renderQueue >= 3000 && !castShadowsOnGlass) mr.shadowCastingMode = ShadowCastingMode.Off;   // 투명 재질은 그림자 없음
            }
            string path = $"{HeroDir}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return path;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
