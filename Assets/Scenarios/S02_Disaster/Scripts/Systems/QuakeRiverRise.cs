using System.Collections.Generic;
using GameCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameGameplay
{
    /// <summary>
    /// 단지 앞 강(집 프리팹 EvacLandmarks/River, z 68~76)이 폭우로 천천히 불어난다.
    ///   · 시작 직후("불어난 강물이 거칠게 흐른다" 자막) ~ 여진 끝까지: 수면이 둑 위로 서서히 차오르고, 둑을 넘으면 물길이 양옆으로 조금 넓어진다
    ///   · 지진해일 단계: 물이 빠져나가는 동안(tsunamiDrawbackEnd) 강바닥 아래로 내려간다 → 그 뒤는 QuakeTsunami 의 해일이 덮는다
    ///   · 물이 빠진 자리가 평평한 잔디로 보이지 않도록, 시작할 때 강 자리에 깊은 물길(강바닥 + 양쪽 옆면)을 파 둔다:
    ///     집 프리팹의 잔디 바닥이 강 위까지 덮고 있으므로 그 렌더러만 끄고 강 자리를 뺀 잔디 조각으로 바꿔 끼운다 (시나리오가 끝나면 모두 원래대로)
    /// 순수 연출(충돌 없음). 강 오브젝트는 집 프리팹에 있으므로 이름으로 찾아 위치/크기만 바꾸고, 시나리오가 끝나면 원래대로 되돌린다.
    /// S02Bootstrap 이 시나리오 시작 시 붙인다 (씬 수정 불필요).
    /// </summary>
    public class QuakeRiverRise : MonoBehaviour
    {
        [SerializeField, Tooltip("수면이 시작 후 이 시각(초)부터 오르기 시작한다")] private float riseStartSeconds = 4f;
        [SerializeField, Tooltip("여진 끝까지 오르는 높이(m). 둑 높이는 약 0.3m — 넘으면 넘친다")] private float riseHeight = 0.55f;
        [SerializeField, Tooltip("둑을 넘은 뒤 물길이 양쪽으로 넓어지는 최대 폭(m, 한쪽)")] private float overflowWidth = 3f;
        [SerializeField, Tooltip("해일 전 물이 빠질 때 원래 수면보다 내려가는 깊이(m). 물길 깊이보다 커야 강바닥이 드러난다")] private float drawbackDepth = 2.1f;
        [SerializeField, Tooltip("물길(강바닥)의 깊이(m, 지면 기준)")] private float channelDepth = 1.8f;
        [SerializeField] private float bankTopAbove = 0.28f;

        // pos/scale = 프리팹 원래 값(복원용), basePos/baseScale = 이 연출이 기준으로 삼는 값 (물을 물길에 맞게 두껍게 했으면 다르다)
        private struct Piece { public Transform t; public Vector3 pos, scale, basePos, baseScale; public bool widen; }
        private Piece[] pieces;
        private bool found;
        private readonly List<GameObject> made = new List<GameObject>();
        private readonly List<Renderer> hidden = new List<Renderer>();

        public void Init()
        {
            Transform river = FindRiver();
            if (river == null) { Debug.LogWarning("[Quake] 단지 앞 강(EvacLandmarks/River)을 찾지 못해 강 수위 연출을 건너뜀"); return; }
            var list = new System.Collections.Generic.List<Piece>();
            foreach (Transform c in river)
            {
                if (c.name != "Water" && c.name != "Current") continue;
                var piece = new Piece { t = c, pos = c.localPosition, scale = c.localScale, widen = c.name == "Water" };
                piece.basePos = piece.pos; piece.baseScale = piece.scale;
                list.Add(piece);
            }
            pieces = list.ToArray();
            DigChannel(river);
            found = pieces.Length > 0;
            Debug.Log($"[Quake] 강 수위 연출 연결: 조각 {pieces.Length}개");
        }

        /// <summary>집 프리팹의 EvacLandmarks 아래 River (먼 하천 River 와 이름이 같으므로 부모로 구분)</summary>
        private static Transform FindRiver()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name != "EvacLandmarks") continue;
                Transform r = t.Find("River");
                if (r != null) return r;
            }
            return null;
        }

        public void Restore()
        {
            if (!found) return;
            foreach (Piece p in pieces) if (p.t != null) { p.t.localPosition = p.pos; p.t.localScale = p.scale; }
            foreach (GameObject g in made) if (g != null) Destroy(g);
            made.Clear();
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
        }

        /// <summary>
        /// 강 자리에 물길을 판다: 강바닥(진흙) + 양쪽 옆면을 놓고, 그 위를 덮고 있던 잔디 바닥(LawnNear, GroundFar)은 강 자리만 뺀 조각으로 바꾼다.
        /// 물은 물길을 채우도록 두껍게 해서, 빠지면 옆면과 강바닥이 드러난다. 모두 충돌 없는 연출용이다 (강가 난간이 사람을 막는다).
        /// </summary>
        private void DigChannel(Transform river)
        {
            int w = System.Array.FindIndex(pieces, p => p.t != null && p.t.name == "Water");
            if (w < 0) return;
            Piece water = pieces[w];
            float top = water.pos.y + water.scale.y * 0.5f;            // 수면 윗면
            float ground = top - 0.02f;                                  // 지면 높이
            float x0 = water.pos.x - water.scale.x * 0.5f, x1 = water.pos.x + water.scale.x * 0.5f;
            float z0 = water.pos.z - water.scale.z * 0.5f, z1 = water.pos.z + water.scale.z * 0.5f;
            float bedTop = ground - channelDepth;

            Transform bankT = river.Find("Bank_N");
            Renderer bankR = bankT != null ? bankT.GetComponent<Renderer>() : null;
            Material stone = bankR != null ? bankR.sharedMaterial : null;
            if (stone == null) return;   // 재질을 빌릴 곳이 없으면 (프리팹이 달라졌으면) 물길은 포기하고 수위 연출만

            // 강바닥 + 양쪽 옆면 + 양 끝 마개
            Color wallColor = new Color(0.42f, 0.36f, 0.28f);
            Box(river, "Riverbed", new Vector3((x0 + x1) * 0.5f, bedTop - 0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), stone, new Color(0.34f, 0.28f, 0.21f));
            Box(river, "RiverWall_N", new Vector3((x0 + x1) * 0.5f, ground - channelDepth * 0.5f, z0 + 0.05f), new Vector3(x1 - x0, channelDepth, 0.1f), stone, wallColor);
            Box(river, "RiverWall_S", new Vector3((x0 + x1) * 0.5f, ground - channelDepth * 0.5f, z1 - 0.05f), new Vector3(x1 - x0, channelDepth, 0.1f), stone, wallColor);
            foreach (float x in new[] { x0 + 0.05f, x1 - 0.05f })
                Box(river, "RiverEnd", new Vector3(x, ground - channelDepth * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.1f, channelDepth, z1 - z0), stone, wallColor);

            // 강 자리를 뺀 잔디: 이 높이에 원래 깔려 있던 바닥(LawnNear, 먼 바닥 GroundFar)을 둘로 나눠 다시 깐다
            Replace(river, "LawnNear", z0, z1);
            Replace(river, "GroundFar", z0, z1);

            // 물은 물길을 채우도록 아래로 두껍게 (윗면은 그대로)
            float thick = top - bedTop + 0.05f;
            water.basePos = new Vector3(water.pos.x, top - thick * 0.5f, water.pos.z);
            water.baseScale = new Vector3(water.scale.x, thick, water.scale.z);
            pieces[w] = water;
            water.t.localPosition = water.basePos; water.t.localScale = water.baseScale;
        }

        private void Replace(Transform river, string objectName, float riverZ0, float riverZ1)
        {
            Renderer old = null;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (r.name == objectName && r is MeshRenderer) { old = r; break; }
            if (old == null) return;
            Bounds b = old.bounds;   // 월드
            Vector3 lo = river.InverseTransformPoint(b.min), hi = river.InverseTransformPoint(b.max);
            float y = river.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)).y;   // 윗면
            float zMin = Mathf.Min(lo.z, hi.z), zMax = Mathf.Max(lo.z, hi.z), xMin = Mathf.Min(lo.x, hi.x), xMax = Mathf.Max(lo.x, hi.x);
            if (zMin >= riverZ0 || zMax <= riverZ1) return;   // 강 자리를 가로지르지 않으면 건드리지 않는다
            old.enabled = false; hidden.Add(old);
            Box(river, objectName + "_A", new Vector3((xMin + xMax) * 0.5f, y - 0.05f, (zMin + riverZ0) * 0.5f), new Vector3(xMax - xMin, 0.1f, riverZ0 - zMin), old.sharedMaterial, null);
            Box(river, objectName + "_B", new Vector3((xMin + xMax) * 0.5f, y - 0.05f, (riverZ1 + zMax) * 0.5f), new Vector3(xMax - xMin, 0.1f, zMax - riverZ1), old.sharedMaterial, null);
        }

        private void Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Color? tint)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (tint.HasValue)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", tint.Value);
                mpb.SetColor("_Color", tint.Value);
                r.SetPropertyBlock(mpb);
            }
            made.Add(go);
        }

        private void OnDestroy() { Restore(); }

        private void Update()
        {
            if (!found) return;
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || !tl.Running) return;

            float rise;    // 원래 수면 기준 높이(m)
            if (tl.Phase != QuakePhase.Tsunami)
            {
                // 단계 길이 합 기준 (디버그 Skip 으로 단계를 넘겨도 수위가 그 단계에 맞게 따라온다)
                float total = 0f, elapsed = tl.PhaseElapsed;
                for (QuakePhase p = QuakePhase.Calm; p < QuakePhase.Tsunami; p++) { total += tl.DurationOf(p); if (p < tl.Phase) elapsed += tl.DurationOf(p); }
                float k = Mathf.Clamp01((elapsed - riseStartSeconds) / Mathf.Max(1f, total - riseStartSeconds));
                rise = riseHeight * Mathf.SmoothStep(0f, 1f, k);
            }
            else
            {
                QuakeConfig c = tl.Config;
                float k = Mathf.Clamp01(tl.PhaseElapsed / Mathf.Max(1f, c.tsunamiDrawbackEnd));
                rise = Mathf.Lerp(riseHeight, -drawbackDepth, Mathf.SmoothStep(0f, 1f, k));
            }

            float over = Mathf.Max(0f, rise - bankTopAbove);
            float widen = overflowWidth * Mathf.Clamp01(over / Mathf.Max(0.01f, riseHeight - bankTopAbove));
            foreach (Piece p in pieces)
            {
                if (p.t == null) continue;
                p.t.localPosition = p.basePos + Vector3.up * rise;
                if (p.widen) p.t.localScale = new Vector3(p.baseScale.x, p.baseScale.y, p.baseScale.z + widen * 2f);
            }
        }
    }
}
