using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 땅이 갈라지는 균열과 땅이 꺼지는 싱크홀 (원본 GroundRupture 이식). 위험 범위는 좌표로 판정한다.
    ///   · 균열: 정해진 단계/시각에 길이 0 에서 끝까지 벌어진다. 다 벌어지면 낮은 위험 범위가 켜져 밟으면 사망 (점프로 넘을 수 있다)
    ///   · 싱크홀: 먼저 주변이 울리고 금이 가며(경고), 몇 초 뒤 구멍이 열린다. 열린 순간부터 그 안에 있거나 들어가면 사망
    /// </summary>
    public class QuakeGroundRupture : MonoBehaviour
    {
        [Serializable]
        public struct Crack
        {
            [Tooltip("균열 조각의 시작점. 자식 상자는 +x 방향으로 뻗어 있어 x 크기로 길이를 조절한다")] public Transform pivot;
            public QuakePhase phase; public float delay; public float openSeconds;
            [Tooltip("위험 범위 (pivot 로컬, 다 벌어진 뒤)")] public Vector3 hazardSize;
        }

        [Serializable]
        public struct Sinkhole
        {
            public Transform pit; public QuakePhase phase;
            [Tooltip("구멍이 열리는 시각 (단계 시작 기준). warningSeconds 보다 커야 경고가 먼저 나온다")] public float delay;
            public float radius;
        }

        [SerializeField] private Crack[] cracks = new Crack[0];
        [SerializeField] private Sinkhole[] sinkholes = new Sinkhole[0];
        [SerializeField] private float warningSeconds = 3.5f, openSeconds = 1.2f, warnRadius = 30f;
        [SerializeField] private AudioClip rumble;

        private Vector3[] crackScales; private float[] crackStart, holeStart; private bool[] holeWarned, crackArmed, holeArmed;

        public void Init()
        {
            crackScales = new Vector3[cracks.Length]; crackStart = new float[cracks.Length]; crackArmed = new bool[cracks.Length];
            for (int i = 0; i < cracks.Length; i++)
            {
                crackStart[i] = -1f;
                if (cracks[i].pivot == null) continue;
                crackScales[i] = cracks[i].pivot.localScale;
                cracks[i].pivot.localScale = new Vector3(0f, crackScales[i].y, crackScales[i].z);
            }
            holeStart = new float[sinkholes.Length]; holeWarned = new bool[sinkholes.Length]; holeArmed = new bool[sinkholes.Length];
            for (int i = 0; i < sinkholes.Length; i++) { holeStart[i] = -1f; if (sinkholes[i].pit != null) sinkholes[i].pit.localScale = Vector3.zero; }
        }

        private void Update()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || !tl.Running || crackStart == null) return;
            Vector3 feet = tl.Player.transform.position;
            for (int i = 0; i < cracks.Length; i++) UpdateCrack(i, tl, feet);
            for (int i = 0; i < sinkholes.Length; i++) UpdateSinkhole(i, tl, feet);
        }

        private void UpdateCrack(int i, QuakeTimeline tl, Vector3 feet)
        {
            Crack c = cracks[i];
            if (c.pivot == null) return;
            if (crackStart[i] < 0f) { if (!tl.Reached(c.phase, c.delay)) return; crackStart[i] = Time.time; QuakeFx.PlayAt(rumble, c.pivot.position, 0.8f, 0.1f, 60f); }
            float t = Mathf.Clamp01((Time.time - crackStart[i]) / Mathf.Max(0.01f, c.openSeconds));
            Vector3 full = crackScales[i];
            c.pivot.localScale = new Vector3(full.x * (1f - (1f - t) * (1f - t)), full.y, full.z);
            if (t >= 1f) crackArmed[i] = true;
            if (!crackArmed[i]) return;
            // 위험 범위: pivot 로컬 (0 ~ 길이) x (높이) x (폭)
            Vector3 local = c.pivot.InverseTransformPoint(feet + Vector3.up * 0.05f);
            Vector3 lossy = c.pivot.lossyScale;
            float lx = local.x * lossy.x, ly = local.y, lz = local.z * c.pivot.lossyScale.z;
            if (lx >= 0f && lx <= full.x && Mathf.Abs(lz) <= c.hazardSize.z * 0.5f && ly <= c.hazardSize.y) QuakeOutcome.Instance.Kill(DeathReason.QuakeGroundCrack);
        }

        private void UpdateSinkhole(int i, QuakeTimeline tl, Vector3 feet)
        {
            Sinkhole s = sinkholes[i];
            if (s.pit == null) return;
            if (!holeWarned[i] && tl.Reached(s.phase, s.delay - warningSeconds))
            {
                holeWarned[i] = true;
                if (Near(s.pit.position, feet))
                {
                    tl.AddImpulse(0.25f);
                    EventBus.ShowSubtitle("(발밑에서 무언가 무너지는 소리가 난다... 땅이 갈라지고 있다!)", 3.5f);
                    QuakeFx.PlayAt(rumble, s.pit.position, 0.7f, 0.1f, 60f);
                }
            }
            if (holeStart[i] < 0f)
            {
                if (!tl.Reached(s.phase, s.delay)) return;
                holeStart[i] = Time.time; holeArmed[i] = true;
                Vector3 c = s.pit.position;
                Debug.Log($"[Quake] 싱크홀 열림 {c}");
                tl.AddImpulse(Mathf.Clamp01(1f - Vector3.Distance(c, feet) / 60f));
                if (QuakeFx.Instance != null)
                {
                    QuakeFx.PlayAt(QuakeFx.Instance.ExplosionSound, c, 0.6f, 0.1f, 70f);
                    var dust = new GameObject("SinkholeDust").transform; dust.position = c + Vector3.up * 0.5f;
                    QuakeFx.Burst(dust, QuakeFx.Instance.Smoke, new Color(0.45f, 0.42f, 0.38f, 0.75f), 5f, 3.5f, 4f, -0.03f, 2.5f, 35);
                    Destroy(dust.gameObject, 10f);
                }
            }
            float t = Mathf.Clamp01((Time.time - holeStart[i]) / Mathf.Max(0.01f, openSeconds));
            s.pit.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, 1f - (1f - t) * (1f - t));
            if (holeArmed[i])
            {
                Vector3 d = feet - s.pit.position; d.y = 0f;
                if (d.magnitude <= s.radius * Mathf.Lerp(0.1f, 1f, t) && Mathf.Abs(feet.y - s.pit.position.y) < 1.5f) QuakeOutcome.Instance.Kill(DeathReason.QuakeSinkhole);
            }
        }

        private bool Near(Vector3 point, Vector3 feet)
        {
            var flat = new Vector2(feet.x - point.x, feet.z - point.z);
            return flat.magnitude <= warnRadius && Mathf.Abs(feet.y - point.y) < 5f;
        }
    }
}
