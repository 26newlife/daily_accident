using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 해일 앞면이 닿으면 무너지는 바깥 물체 (원본 WaveCollapse 이식, 1단계: 나무·가로등·자동차).
    ///   · 원본처럼 대상을 기울이고(tilt) 가라앉히고(sink) 내륙(-z) 쪽으로 밀며(push) 먼지/물보라를 뿜는다
    ///   · 원본과 달리 물 높이도 본다: 대상 밑동이 해일 수면(TopY)보다 높으면(뒷산 위 등) 무너지지 않는다
    ///   · 플레이어가 사는 동은 대상이 아니다 (옥상에서 버티는 클리어가 있으므로)
    /// 대상은 집 프리팹 안에 있으므로 이름으로 찾아 움직이고, 시나리오가 끝나면 원래 자세·물리로 되돌린다.
    /// 오브젝트마다 컴포넌트를 붙이지 않고 이 한 곳에서 모두 처리한다. S02Bootstrap 이 시작 시 붙인다.
    /// </summary>
    public class QuakeWaveCollapse : MonoBehaviour
    {
        private struct Kind
        {
            public string name; public Vector3 tilt, push; public float sink, duration, dust, triggerAhead;
        }

        // 원본 CatastropheBuilder.BuildCollapses 수치
        private static readonly Kind[] Kinds =
        {
            new Kind { name = "Car_Sedan", tilt = new Vector3(0f, 40f, 75f), sink = 0f, push = new Vector3(0f, 0.5f, -16f), duration = 4f, dust = 0.4f, triggerAhead = 1f },
            new Kind { name = "Tree_Round", tilt = new Vector3(-82f, 0f, 0f), sink = 0f, push = new Vector3(0f, 0f, -3f), duration = 2.5f, dust = 0.4f, triggerAhead = 1f },
            new Kind { name = "Tree_Pine", tilt = new Vector3(-82f, 0f, 0f), sink = 0f, push = new Vector3(0f, 0f, -3f), duration = 2.5f, dust = 0.4f, triggerAhead = 1f },
            new Kind { name = "LampPost", tilt = new Vector3(-78f, 0f, 0f), sink = 0f, push = new Vector3(0f, 0f, -2f), duration = 2f, dust = 0.3f, triggerAhead = 0.5f },
        };

        private static readonly Color DustColor = new Color(0.55f, 0.55f, 0.52f, 0.7f);

        private class Target
        {
            public Transform t; public Kind kind;
            public Vector3 pos; public Quaternion rot;
            public float triggerZ, startTime = -1f;
            public bool done;
            public List<(Rigidbody body, bool kinematic)> bodies;
            public List<Collider> colliders;
        }

        private readonly List<Target> targets = new List<Target>();
        private int pending;

        public void Init()
        {
            targets.Clear();
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                foreach (Kind k in Kinds)
                {
                    if (t.name != k.name) continue;
                    targets.Add(new Target { t = t, kind = k, pos = t.position, rot = t.rotation, triggerZ = t.position.z + k.triggerAhead });
                    break;
                }
            }
            // 해일은 +z 에서 -z 로 오므로 z 가 큰 것부터 무너진다 → 앞쪽부터 검사하도록 정렬
            targets.Sort((a, b) => b.triggerZ.CompareTo(a.triggerZ));
            pending = targets.Count;
            Debug.Log($"[Quake] 해일 붕괴 대상 {targets.Count}개 (나무·가로등·자동차)");
        }

        private void Update()
        {
            if (targets.Count == 0) return;
            QuakeTsunami ts = QuakeTsunami.Instance;
            bool arriving = ts != null && ts.ArrivalProgress >= 0f;

            foreach (Target x in targets)
            {
                if (x.done || x.t == null) continue;
                if (x.startTime < 0f)
                {
                    // 앞면이 닿았고, 밑동이 물에 잠기는 높이일 때만
                    if (arriving && pending > 0 && ts.FrontZ <= x.triggerZ && x.pos.y < ts.TopY) Begin(x);
                    continue;
                }
                float k = Mathf.Clamp01((Time.time - x.startTime) / Mathf.Max(0.01f, x.kind.duration));
                Apply(x, k * k * (3f - 2f * k));
                if (k >= 1f) x.done = true;
            }
        }

        private void Begin(Target x)
        {
            x.startTime = Time.time;
            pending--;
            // 묶음째 움직이므로 안의 Rigidbody 는 고정하고 충돌체는 끈다 (물에 잠긴 곳이라 충돌이 필요 없다)
            x.bodies = new List<(Rigidbody, bool)>();
            foreach (Rigidbody b in x.t.GetComponentsInChildren<Rigidbody>()) { x.bodies.Add((b, b.isKinematic)); b.isKinematic = true; }
            x.colliders = new List<Collider>();
            foreach (Collider c in x.t.GetComponentsInChildren<Collider>()) if (!c.isTrigger && c.enabled) { x.colliders.Add(c); c.enabled = false; }

            QuakeFx fx = QuakeFx.Instance;
            if (fx != null && fx.Smoke != null && x.kind.dust > 0f)
            {
                var dust = new GameObject("CollapseDust").transform;
                dust.SetParent(transform, false);
                dust.position = x.pos + Vector3.up * 2f * x.kind.dust;
                float s = x.kind.dust;
                QuakeFx.Burst(dust, fx.Smoke, DustColor, 7f, 5f * s, 7f * s, -0.02f, 4f * s, Mathf.Max(4, Mathf.RoundToInt(30 * s)));
                Destroy(dust.gameObject, 12f);
            }
        }

        private static void Apply(Target x, float k)
        {
            Quaternion r = Quaternion.Euler(x.kind.tilt * k);
            x.t.SetPositionAndRotation(x.pos + x.kind.push * k + Vector3.down * (x.kind.sink * k), r * x.rot);
        }

        /// <summary>시나리오가 끝날 때: 원래 자세·물리로 되돌린다 (집 프리팹 오브젝트라 다른 시나리오에 남지 않게)</summary>
        public void Restore()
        {
            foreach (Target x in targets)
            {
                if (x.t == null || x.startTime < 0f) continue;
                x.t.SetPositionAndRotation(x.pos, x.rot);
                if (x.colliders != null) foreach (Collider c in x.colliders) if (c != null) c.enabled = true;
                if (x.bodies != null) foreach (var (b, kin) in x.bodies) if (b != null) b.isKinematic = kin;
                x.startTime = -1f; x.done = false;
            }
            pending = targets.Count;
        }

        private void OnDestroy() { Restore(); }
    }
}
