using System;
using System.Collections.Generic;
using System.Text;
using GameGameplay;
using UnityEditor;
using UnityEngine;

namespace GameUI.EditorTools.Interior
{
    /// <summary>
    /// 1004호 + 복도 통로/동선/상호작용 접근성 검증기. Tools > GameUI > Validate Apartment Clearance
    /// 씬의 실제 콜라이더를 격자(0.05m)로 샘플링해서 (Physics.OverlapCapsule) 장애물 지도를 만들고,
    /// 거리 변환으로 "플레이어 캡슐(반지름 R)이 지나갈 수 있는 폭"을 계산한다. 문은 열린 상태로 간주(문짝 콜라이더 제외).
    /// 결과는 Console 요약(PASS/FAIL) + 씬 뷰 표시(빨강=실패, 노랑=끼임 위험, 초록=통과한 경로 지점).
    /// </summary>
    public static class ApartmentClearanceValidator
    {
        private const float MinX = -15.5f, MaxX = 7.0f, MinZ = -3.6f, MaxZ = 9.6f;
        private static readonly Vector2 Start = new Vector2(1.7f, 4.0f);
        private static readonly Vector2 ApartmentCenter = new Vector2(3.3f, 3.7f);

        // 문짝/미닫이는 열린 것으로 간주해 장애물에서 제외 (id → 열림 반경은 문 크기에서 계산)
        private static readonly string[] SwingDoorIds = { "door_front", "door_bedroom", "door_bathroom", "door_utility", "fire_door_shelter", "fire_door_stairs" };
        private static readonly string[] OpenableIds = { "door_front", "door_bedroom", "door_bathroom", "door_utility", "door_balcony", "fire_door_shelter", "fire_door_stairs" };
        private static readonly string[] WindowIds = { "window_bedroom", "window_living", "window_kitchen" };

        private struct Route { public string name; public Vector2 a, b; public float width; public Route(string n, float ax, float az, float bx, float bz, float w) { name = n; a = new Vector2(ax, az); b = new Vector2(bx, bz); width = w; } }

        // 필수 동선. 폭: 문(0.8~0.85m)을 지나는 구간은 문 폭, 그 외 주 동선 0.9, 보조 0.7~0.76
        private static readonly Route[] Routes =
        {
            new Route("침실(시작) → 현관문 앞", 1.7f, 4.0f, 3.0f, 0.6f, 0.80f),
            new Route("현관 → 10층 복도", 3.0f, 0.6f, 3.0f, -1.0f, 0.90f),
            new Route("복도 → 비상계단 안", 3.0f, -1.0f, -14.6f, -1.1f, 0.90f),
            new Route("현관 → 거실", 3.0f, 0.6f, 3.8f, 5.2f, 0.90f),
            new Route("홀 → 주방", 3.2f, 3.5f, 5.0f, 3.4f, 0.90f),
            new Route("거실 → 주방(북쪽 개구부)", 3.8f, 5.2f, 5.5f, 4.0f, 0.76f),
            new Route("거실 → 베란다 문", 3.8f, 5.2f, 4.5f, 7.9f, 0.90f),
            new Route("베란다 → 대피공간 방화문", 4.5f, 7.9f, 6.0f, 8.1f, 0.90f),
            new Route("베란다 → 경량칸막이 앞", 4.5f, 7.9f, 1.0f, 8.2f, 0.70f),
            new Route("주방 → 다용도실", 5.35f, 3.0f, 5.35f, 1.0f, 0.76f),
            new Route("현관 → 화장실(젖은 수건)", 2.4f, 1.2f, 1.3f, 1.2f, 0.76f),
            new Route("침실 → 거실", 1.7f, 4.0f, 3.2f, 3.5f, 0.80f),
            new Route("침실 → 창문 앞(동쪽 통로)", 1.7f, 4.0f, 2.0f, 6.6f, 0.76f),
        };

        // ───────── 결과 ─────────
        public sealed class Marker { public Vector3 pos; public Color color; public float radius; public string label; }
        private static readonly List<Marker> markers = new List<Marker>();

        public sealed class Result
        {
            public string category, name, detail; public bool pass;
        }

        public sealed class Report
        {
            internal readonly List<Result> results = new List<Result>();
            public int FailCount { get { int n = 0; foreach (Result r in results) if (!r.pass) n++; return n; } }
            public bool AllPass => FailCount == 0;
            internal void Add(string cat, string name, bool pass, string detail = "") => results.Add(new Result { category = cat, name = name, pass = pass, detail = detail });
        }

        // ───────── 격자 ─────────
        private static int nx, nz;
        private static bool[] obstacle;          // 벽 또는 가구
        private static bool[] furniture;         // 가구만
        private static float[] topY;             // 가구 상단 높이
        private static string[] furnName;        // 가구 이름 (보고용)
        private static float[] clearance;        // 가장 가까운 장애물까지 거리 (m)
        private static float step;

        private static int Idx(int ix, int iz) => iz * nx + ix;
        private static int IX(float x) => Mathf.Clamp(Mathf.RoundToInt((x - MinX) / step), 0, nx - 1);
        private static int IZ(float z) => Mathf.Clamp(Mathf.RoundToInt((z - MinZ) / step), 0, nz - 1);
        private static Vector2 CellPos(int ix, int iz) => new Vector2(MinX + ix * step, MinZ + iz * step);

        // ═════════════════════════ 메뉴 ═════════════════════════
        [MenuItem("Tools/GameUI/Validate Apartment Clearance")]
        public static void ValidateMenu()
        {
            Report r = Run();
            Debug.Log(Summarize(r));
            SceneView.RepaintAll();
        }

        /// <summary>검증을 실행하고 요약 문자열을 반환 (빌더 메뉴에서 호출)</summary>
        public static string RunAndSummarize() => Summarize(Run());

        [MenuItem("Tools/GameUI/Clear Clearance Markers")]
        public static void ClearMarkers() { markers.Clear(); SceneView.RepaintAll(); }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Interior.ApartmentClearanceValidator.ValidateBatch</summary>
        public static void ValidateBatch()
        {
            Report r = Run();
            Debug.Log(Summarize(r));
            EditorApplication.Exit(r.AllPass ? 0 : 1);
        }

        [InitializeOnLoadMethod]
        private static void HookSceneView()
        {
            SceneView.duringSceneGui -= DrawMarkers;
            SceneView.duringSceneGui += DrawMarkers;
        }

        private static void DrawMarkers(SceneView sv)
        {
            foreach (Marker m in markers)
            {
                Handles.color = m.color;
                Handles.DrawSolidDisc(m.pos, Vector3.up, m.radius);
                if (!string.IsNullOrEmpty(m.label)) Handles.Label(m.pos + Vector3.up * 0.3f, m.label);
            }
        }

        // ═════════════════════════ 실행 ═════════════════════════
        public static Report Run()
        {
            var report = new Report();
            markers.Clear();
            ClearanceSpec.Load();

            GameObject lvGo = GameObject.Find("Gameplay/Level");
            if (lvGo == null) lvGo = GameObject.Find("Level");
            if (lvGo == null) { report.Add("준비", "Level", false, "열린 씬에서 Level 오브젝트를 찾지 못했습니다 (GameScene 을 여세요)."); return report; }
            Transform level = lvGo.transform;
            Transform structure = level.Find("Structure"), interactables = level.Find("Interactables"), interior = level.Find("Interior");
            if (structure == null || interactables == null) { report.Add("준비", "Level 구조", false, "Structure/Interactables 가 없습니다."); return report; }

            Physics.SyncTransforms();
            BuildGrid(level, structure, interactables, interior);

            float R = ClearanceSpec.Radius, tol = ClearanceSpec.Tolerance;
            int startIdx = NearestPassable(Start, R - tol);
            if (startIdx < 0) { report.Add("준비", "시작 위치", false, "시작 위치(1.7, 4.0) 주변에 플레이어가 설 수 있는 공간이 없습니다."); AddMarker(new Vector3(Start.x, 0.1f, Start.y), Color.red, "시작 위치 막힘"); return report; }
            bool[] reach = Flood(startIdx, R - tol);

            CheckRoutes(report);
            CheckPinch(report, reach);
            CheckDoorSwings(report, interactables);
            CheckWindowFronts(report, interactables);
            CheckInteractables(report, reach, interactables);
            CheckWindowBarriers(report, interior);
            return report;
        }

        // ───────── 격자 생성 ─────────
        private static void BuildGrid(Transform level, Transform structure, Transform interactables, Transform interior)
        {
            step = ClearanceSpec.GridStep;
            nx = Mathf.CeilToInt((MaxX - MinX) / step) + 1;
            nz = Mathf.CeilToInt((MaxZ - MinZ) / step) + 1;
            obstacle = new bool[nx * nz]; furniture = new bool[nx * nz]; topY = new float[nx * nz]; furnName = new string[nx * nz];

            var openable = new List<Transform>();
            foreach (string id in OpenableIds) { Transform t = interactables.Find(id); if (t != null) openable.Add(t); }
            Transform safety = interior != null ? interior.Find("Safety") : null;

            float y0 = ClearanceSpec.StepOffset + 0.03f;           // 계단턱(stepOffset) 이하의 낮은 물체는 걸리지 않고 넘는다
            float y1 = ClearanceSpec.Height - 0.03f;
            const float thin = 0.035f;
            var buf = new Collider[64];

            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    Vector2 p = CellPos(ix, iz);
                    int n = Physics.OverlapCapsuleNonAlloc(new Vector3(p.x, y0, p.y), new Vector3(p.x, y1, p.y), thin, buf, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++)
                    {
                        Collider c = buf[k];
                        if (c == null || c.isTrigger || !c.enabled) continue;
                        if (c.GetComponentInParent<FireSource>() != null) continue;     // 불이 붙기 전에는 콜라이더가 꺼져 있다
                        if (IsWallMounted(c.transform, interactables)) continue;        // 스위치/창 잠금장치 등 벽에 붙은 얇은 물체
                        if (IsUnder(c.transform, safety)) continue;
                        bool isDoor = false;
                        foreach (Transform d in openable) if (IsUnder(c.transform, d)) { isDoor = true; break; }
                        if (isDoor) continue;

                        int i = Idx(ix, iz);
                        obstacle[i] = true;
                        if (!IsUnder(c.transform, structure))
                        {
                            furniture[i] = true;
                            if (c.bounds.max.y > topY[i]) { topY[i] = c.bounds.max.y; furnName[i] = NameOf(c.transform, interior); }
                        }
                    }
                }

            clearance = DistanceTransform(obstacle, nx, nz, step);
        }

        private static bool IsWallMounted(Transform t, Transform interactables)
        {
            Transform cur = t;
            while (cur != null && cur.parent != interactables) cur = cur.parent;
            if (cur == null) return false;
            string n = cur.name;
            return n.StartsWith("switch_") || n.EndsWith("_lock") || n.StartsWith("window_") || n == "door_handle_check";
        }

        private static string NameOf(Transform t, Transform interior)
        {
            // Interior/<방>/<가구> 이면 가구 이름, 아니면 상호작용 루트 이름
            Transform cur = t;
            while (cur.parent != null && cur.parent != interior && cur.parent.name != "Interactables" && cur.parent.name != "Furniture" && cur.parent.name != "Structure") cur = cur.parent;
            return cur.name;
        }

        private static bool IsUnder(Transform t, Transform root)
        {
            if (root == null) return false;
            for (Transform c = t; c != null; c = c.parent) if (c == root) return true;
            return false;
        }

        // ───────── 거리 변환 (Felzenszwalb) ─────────
        private static float[] DistanceTransform(bool[] obs, int w, int h, float cell)
        {
            const float INF = 1e9f;
            var f = new float[w * h];
            for (int i = 0; i < f.Length; i++) f[i] = obs[i] ? 0f : INF;
            var tmp = new float[Mathf.Max(w, h)]; var d = new float[Mathf.Max(w, h)];
            var v = new int[Mathf.Max(w, h)]; var z = new float[Mathf.Max(w, h) + 1];

            for (int x = 0; x < w; x++)             // 열 방향
            {
                for (int y = 0; y < h; y++) tmp[y] = f[y * w + x];
                Edt1D(tmp, h, d, v, z);
                for (int y = 0; y < h; y++) f[y * w + x] = d[y];
            }
            for (int y = 0; y < h; y++)             // 행 방향
            {
                for (int x = 0; x < w; x++) tmp[x] = f[y * w + x];
                Edt1D(tmp, w, d, v, z);
                for (int x = 0; x < w; x++) f[y * w + x] = d[x];
            }
            for (int i = 0; i < f.Length; i++) f[i] = f[i] >= INF * 0.5f ? 99f : Mathf.Sqrt(f[i]) * cell;
            return f;
        }

        private static void Edt1D(float[] f, int n, float[] d, int[] v, float[] z)
        {
            const float INF = 1e9f;
            int k = 0; v[0] = 0; z[0] = -INF; z[1] = INF;
            for (int q = 1; q < n; q++)
            {
                float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k]);
                }
                k++; v[k] = q; z[k] = s; z[k + 1] = INF;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                d[q] = (q - v[k]) * (q - v[k]) + f[v[k]];
            }
        }

        // ───────── 격자 탐색 ─────────
        private static int NearestPassable(Vector2 p, float minClear)
        {
            int cx = IX(p.x), cz = IZ(p.y);
            for (int r = 0; r < 20; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                        if (clearance[Idx(x, z)] >= minClear) return Idx(x, z);
                    }
            return -1;
        }

        private static bool[] Flood(int startIdx, float minClear)
        {
            var seen = new bool[nx * nz];
            if (startIdx < 0 || clearance[startIdx] < minClear) return seen;
            var queue = new Queue<int>();
            queue.Enqueue(startIdx); seen[startIdx] = true;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(); int x = i % nx, z = i / nx;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int ax = x + dx, az = z + dz;
                        if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                        int j = Idx(ax, az);
                        if (seen[j] || clearance[j] < minClear) continue;
                        seen[j] = true; queue.Enqueue(j);
                    }
            }
            return seen;
        }

        private static void AddMarker(Vector3 pos, Color c, string label, float radius = 0.2f)
            => markers.Add(new Marker { pos = pos, color = new Color(c.r, c.g, c.b, 0.65f), radius = radius, label = label });

        // ═════════════════════════ 1+2. 통로 폭 / 필수 동선 연결 ═════════════════════════
        private static void CheckRoutes(Report report)
        {
            float tol = ClearanceSpec.Tolerance;
            foreach (Route r in Routes)
            {
                float need = r.width;   // 문 폭(0.8m)처럼 구조상 정해진 구간은 그 폭이 상한
                float clear = need * 0.5f - tol;
                int a = NearestPassable(r.a, clear), b = NearestPassable(r.b, clear);
                bool ok = false; string detail = "";
                if (a < 0 || b < 0) detail = "출발/도착 지점에 플레이어가 서 있을 공간이 없음";
                else
                {
                    bool[] seen = Flood(a, clear);
                    ok = seen[b];
                    if (!ok)
                    {
                        // 이 경로가 통과할 수 있는 최대 폭을 이분 탐색해서 병목 폭을 알려준다
                        float lo = 0.05f, hi = clear;
                        for (int it = 0; it < 10; it++)
                        {
                            float mid = (lo + hi) * 0.5f;
                            int a2 = NearestPassable(r.a, mid), b2 = NearestPassable(r.b, mid);
                            bool good = a2 >= 0 && b2 >= 0 && Flood(a2, mid)[b2];
                            if (good) lo = mid; else hi = mid;
                        }
                        detail = $"필요 폭 {need:F2}m, 실제 병목 약 {lo * 2f + 2 * tol:F2}m";
                        AddMarker(new Vector3(r.a.x, 0.1f, r.a.y), Color.red, r.name + " ✕");
                        AddMarker(new Vector3(r.b.x, 0.1f, r.b.y), Color.red, "");
                    }
                    else
                    {
                        AddMarker(new Vector3(r.a.x, 0.05f, r.a.y), Color.green, "", 0.08f);
                        AddMarker(new Vector3(r.b.x, 0.05f, r.b.y), Color.green, "", 0.08f);
                    }
                }
                report.Add("동선", r.name + $" (폭 {need:F2}m)", ok, detail);
            }
        }

        // ═════════════════════════ 4. 끼임 위험 ═════════════════════════
        private static void CheckPinch(Report report, bool[] reach)
        {
            // 플레이어가 들어갈 수는 있지만(R) 절대 최소 폭의 절반(R+0.1)으로는 지나갈 수 없는 좁은 구간
            float R = ClearanceSpec.Radius, tol = ClearanceSpec.Tolerance;
            float coreClear = ClearanceSpec.AbsoluteMinWidth * 0.5f - tol, cover = ClearanceSpec.AbsoluteMinWidth * 0.5f;
            var core = new bool[nx * nz];
            for (int i = 0; i < core.Length; i++) core[i] = clearance[i] >= coreClear;
            float[] distToCore = DistanceTransform(core, nx, nz, step);   // core 칸까지의 거리

            var pinch = new bool[nx * nz];
            for (int i = 0; i < pinch.Length; i++) pinch[i] = reach[i] && distToCore[i] > cover;

            var seen = new bool[nx * nz]; int clusters = 0;
            for (int i = 0; i < pinch.Length; i++)
            {
                if (!pinch[i] || seen[i]) continue;
                var stack = new Stack<int>(); stack.Push(i); seen[i] = true;
                var cells = new List<int>();
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); cells.Add(c); int x = c % nx, z = c / nx;
                    for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int ax = x + dx, az = z + dz;
                            if ((dx == 0 && dz == 0) || ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                            int j = Idx(ax, az);
                            if (pinch[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                        }
                }
                if (cells.Count < 6) continue;
                Vector2 sum = Vector2.zero; foreach (int c in cells) sum += CellPos(c % nx, c / nx);
                Vector2 mid = sum / cells.Count;
                clusters++;
                string who = NearbyNames(mid, 0.6f);
                AddMarker(new Vector3(mid.x, 0.15f, mid.y), Color.yellow, "끼임 위험: " + who, 0.25f);
                report.Add("끼임", $"({mid.x:F2}, {mid.y:F2}) 좁은 통로 [{who}]", false, $"{cells.Count} 칸");
            }
            if (clusters == 0) report.Add("끼임", "들어갈 수 있지만 지나갈 수 없는 좁은 틈 없음", true);
        }

        private static string NearbyNames(Vector2 p, float radius)
        {
            var names = new SortedSet<string>();
            int rr = Mathf.CeilToInt(radius / step);
            int cx = IX(p.x), cz = IZ(p.y);
            for (int dz = -rr; dz <= rr; dz++)
                for (int dx = -rr; dx <= rr; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                    int i = Idx(x, z);
                    if (furniture[i] && furnName[i] != null && dx * dx + dz * dz <= rr * rr) names.Add(furnName[i]);
                }
            return names.Count == 0 ? "벽" : string.Join(", ", names);
        }

        // ═════════════════════════ 문 열림 반경 + 0.3m ═════════════════════════
        private static void CheckDoorSwings(Report report, Transform interactables)
        {
            foreach (string id in SwingDoorIds)
            {
                Transform t = interactables.Find(id);
                Transform vis = t != null ? t.Find("Hinge/Visual") : null;
                var tog = t != null ? t.GetComponent<ToggleInteractable>() : null;
                var rend = vis != null ? vis.GetComponent<Renderer>() : null;
                if (rend == null || tog == null) { report.Add("문 열림", id, false, "문 오브젝트 구조를 읽지 못했습니다."); continue; }

                Vector2 hinge = new Vector2(t.position.x, t.position.z);
                Vector2 toCenter = new Vector2(rend.bounds.center.x, rend.bounds.center.z) - hinge;
                float length = toCenter.magnitude * 2f;
                float yaw = new SerializedObject(tog).FindProperty("onLocalEuler").vector3Value.y;
                float a0 = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
                float a1 = a0 - yaw;                                  // Y축 +θ 회전은 평면 각도를 -θ 만큼 돌린다

                var hits = new SortedSet<string>(); Vector2 first = Vector2.zero; bool any = false;
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int i = Idx(ix, iz);
                        if (!furniture[i]) continue;
                        Vector2 p = CellPos(ix, iz);
                        if (DistToSector(p - hinge, length, a0, a1) > ClearanceSpec.DoorSwingMargin) continue;
                        hits.Add(furnName[i] ?? "가구"); if (!any) { first = p; any = true; }
                    }
                if (any) AddMarker(new Vector3(first.x, 0.2f, first.y), Color.red, id + " 열림 반경 침범: " + string.Join(",", hits), 0.25f);
                report.Add("문 열림", $"{id} (반경 {length:F2}+{ClearanceSpec.DoorSwingMargin:F1}m)", !any, any ? "침범: " + string.Join(", ", hits) : "");
            }
        }

        /// <summary>경첩 기준 상대 위치 v 에서 부채꼴(반경 r, 각도 a0~a1)까지의 거리</summary>
        private static float DistToSector(Vector2 v, float r, float a0, float a1)
        {
            float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            for (int k = -1; k <= 1; k++)
            {
                float a = ang + 360f * k;
                if (a >= lo && a <= hi) return Mathf.Max(0f, v.magnitude - r);
            }
            // 각도 밖: 두 반지름 선분 중 가까운 쪽까지의 거리
            return Mathf.Min(DistToSegment(v, Vector2.zero, Dir(a0) * r), DistToSegment(v, Vector2.zero, Dir(a1) * r));
        }

        private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        // ═════════════════════════ 창문 앞 0.8m ═════════════════════════
        private static void CheckWindowFronts(Report report, Transform interactables)
        {
            foreach (string id in WindowIds)
            {
                Transform t = interactables.Find(id);
                Transform pane = t != null ? t.Find("Pane") : null;
                Transform handle = t != null ? t.Find("Handle") : null;
                var rend = pane != null ? pane.GetComponent<Renderer>() : null;
                if (rend == null) { report.Add("창문 앞", id, false, "창문 구조를 읽지 못했습니다."); continue; }

                Bounds b = rend.bounds; bool thinX = b.size.x < b.size.z;
                Vector2 hp = handle != null ? new Vector2(handle.position.x, handle.position.z) : new Vector2(b.center.x, b.center.z);
                float inward = thinX ? -Mathf.Sign(b.center.x - ApartmentCenter.x) : -Mathf.Sign(b.center.z - ApartmentCenter.y);
                // 창 아래 가구가 창턱(약 0.9~1.0m)보다 낮으면 팔을 뻗어 열 수 있으므로 허용 (주방 하부장 0.9m)
                float allowedTop = id == "window_kitchen" ? b.min.y - 0.05f : ClearanceSpec.StepOffset;

                var hits = new SortedSet<string>(); Vector2 first = Vector2.zero;
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int i = Idx(ix, iz);
                        if (!furniture[i] || topY[i] <= allowedTop) continue;
                        Vector2 p = CellPos(ix, iz);
                        float along = thinX ? p.y - hp.y : p.x - hp.x;                 // 벽 방향 거리
                        float depth = thinX ? (p.x - b.center.x) * inward : (p.y - b.center.z) * inward; // 벽에서 방 안쪽 거리
                        if (Mathf.Abs(along) <= ClearanceSpec.WindowFrontHalfWidth && depth > 0.1f && depth <= ClearanceSpec.WindowFrontDepth + 0.125f)
                        { if (hits.Count == 0) first = p; hits.Add(furnName[i] ?? "가구"); }
                    }
                if (hits.Count > 0) AddMarker(new Vector3(first.x, 0.2f, first.y), Color.red, id + " 앞 침범: " + string.Join(",", hits), 0.25f);
                report.Add("창문 앞", $"{id} (앞 {ClearanceSpec.WindowFrontDepth:F1}m)", hits.Count == 0, hits.Count > 0 ? "침범: " + string.Join(", ", hits) : "");
            }
        }

        // ═════════════════════════ 3. 상호작용 접근성 ═════════════════════════
        private static void CheckInteractables(Report report, bool[] reach, Transform interactables)
        {
            float R = ClearanceSpec.Radius, tol = ClearanceSpec.Tolerance, front = ClearanceSpec.InteractableFront;
            float maxDist = ClearanceSpec.InteractDistance;
            var all = UnityEngine.Object.FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            Array.Sort(all, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            var hitBuf = new RaycastHit[32];

            foreach (InteractableObject io in all)
            {
                if (io is FireSource) continue;                       // 불은 점화 전에는 상호작용 대상이 아니다
                if (io is NpcBase) continue;                          // 이동하는 NPC(할머니/청년)는 가구 접근성 검증 대상이 아니다
                Vector3 ip = io.transform.position;
                if (ip.x < MinX - 0.5f || ip.x > MaxX + 0.5f || ip.z < MinZ - 0.5f || ip.z > MaxZ + 0.5f) continue;   // 계단참·옥상·하강 구간·집결지는 압축 이동(페이드+순간이동)으로 가는 별도 구역
                if (io.Id == "door_1003_inside") continue;            // 1003호 안쪽 현관은 경량칸막이를 부순 뒤에만 들어갈 수 있다
                Collider[] cols = io.GetComponentsInChildren<Collider>(false);
                Bounds b = default; bool has = false;
                foreach (Collider c in cols) { if (c.isTrigger) continue; if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds); }
                if (!has) { report.Add("상호작용", io.name, false, "콜라이더가 없어 상호작용할 수 없음"); continue; }

                bool anyStand = false, anySee = false; string blocker = "";
                Vector2 bestCell = Vector2.zero; float bestDist = float.MaxValue;
                int stride = 1;
                for (int iz = 0; iz < nz; iz += stride)
                    for (int ix = 0; ix < nx; ix += stride)
                    {
                        int i = Idx(ix, iz);
                        if (!reach[i] || clearance[i] < R - tol) continue;
                        Vector2 p = CellPos(ix, iz);
                        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.y, 0f, p.y - b.max.z);
                        float hd = Mathf.Sqrt(dx * dx + dz * dz);
                        if (hd > front) continue;
                        anyStand = true;
                        if (hd < bestDist) { bestDist = hd; bestCell = p; }

                        // 시선(레이캐스트): 눈높이에서 대상 콜라이더로, 상호작용 거리 안에서 처음 맞는 것이 대상이어야 한다
                        Vector3 eye = new Vector3(p.x, ClearanceSpec.EyeHeight, p.y);
                        Vector3 target = b.ClosestPoint(eye);
                        Vector3 dir = target - eye; float dist = dir.magnitude;
                        if (dist > maxDist || dist < 0.01f) continue;
                        string who;
                        if (FirstHitIs(eye, dir / dist, dist + 0.2f, io, hitBuf, out who)) { anySee = true; }
                        else if (blocker == "" || hd < 0.5f) blocker = who;
                        if (anySee) goto done;
                    }
            done:
                bool pass = anyStand && anySee;
                string detail = !anyStand ? $"대상 앞 {front:F1}m 안에 플레이어가 설 공간이 없음"
                    : !anySee ? $"서 있을 수는 있지만 시선이 가려짐 (가린 물체: {blocker})" : "";
                if (!pass)
                {
                    Vector3 mp = anyStand ? new Vector3(bestCell.x, 0.1f, bestCell.y) : new Vector3(b.center.x, 0.1f, b.center.z);
                    AddMarker(mp, Color.red, io.name + " 접근 불가", 0.2f);
                }
                report.Add("상호작용", io.name, pass, detail);
            }
        }

        private static bool FirstHitIs(Vector3 origin, Vector3 dir, float maxDist, InteractableObject target, RaycastHit[] buf, out string firstName)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, buf, maxDist, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Collider bestCol = null;
            for (int i = 0; i < n; i++)
            {
                Collider c = buf[i].collider;
                if (c.GetComponentInParent<FireSource>() != null) continue;
                if (buf[i].distance < best) { best = buf[i].distance; bestCol = c; }
            }
            firstName = bestCol != null ? bestCol.name : "";
            return bestCol != null && bestCol.GetComponentInParent<InteractableObject>() == target;
        }

        // ═════════════════════════ 점프 대비: 창문 바깥 투명 콜라이더 ═════════════════════════
        private static void CheckWindowBarriers(Report report, Transform interior)
        {
            Transform safety = interior != null ? interior.Find("Safety") : null;
            foreach (string id in WindowIds)
            {
                bool ok = safety != null && safety.Find("WindowBarrier_" + id) != null;
                report.Add("점프/탈출", $"{id} 바깥 투명 콜라이더", ok, ok ? "" : "점프로 창밖으로 나갈 수 있음 (Build Apartment Interior 재실행)");
            }
        }

        // ═════════════════════════ 리포트 ═════════════════════════
        private static string Summarize(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Clearance] 플레이어 R={ClearanceSpec.Radius:F2} H={ClearanceSpec.Height:F2} 상호작용 거리={ClearanceSpec.InteractDistance:F1}m | " +
                          $"주 동선 {ClearanceSpec.MainRouteWidth:F1}m / 보조 {ClearanceSpec.AuxRouteWidth:F1}m / 절대 최소 {ClearanceSpec.AbsoluteMinWidth:F2}m");
            string cat = null; int pass = 0, fail = 0;
            foreach (Result x in r.results)
            {
                if (x.category != cat) { cat = x.category; sb.AppendLine($"── {cat} ──"); }
                sb.AppendLine($"  {(x.pass ? "PASS" : "FAIL")}  {x.name}{(string.IsNullOrEmpty(x.detail) ? "" : "  → " + x.detail)}");
                if (x.pass) pass++; else fail++;
            }
            sb.AppendLine($"== 결과: {(fail == 0 ? "ALL PASS" : "FAIL " + fail + "건")} (PASS {pass} / FAIL {fail}) — 실패 위치는 씬 뷰의 빨강/노랑 원으로 표시됨");
            return sb.ToString();
        }
    }
}
