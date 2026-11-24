using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 지진해일 (원본 TsunamiController + TsunamiWave 이식). 강(+z, 남쪽) 쪽 물이 먼저 멀리 빠졌다가 단지 쪽으로 밀려오며 차오른다.
    /// 물은 +z 쪽에서 -z(뒷산) 쪽으로 온다. 물이 플레이어를 덮치면 Engulfed, 끝까지 버티면 Passed.
    /// </summary>
    public class QuakeTsunami : MonoBehaviour
    {
        [SerializeField, Tooltip("윗면이 y=top, 앞면(-z쪽)이 z=front 가 되도록 옮겨지는 큰 물 상자")] private Transform water;
        [SerializeField, Tooltip("물 벽 (아래쪽 중심이 원점, 높이 1)")] private Transform waveBody;
        [SerializeField] private Transform waveCrest;
        [SerializeField] private float maxWaveHeight = 9f, drownMargin = 0.3f;
        [SerializeField] private float groundY = -28.5f;

        private bool finished, waveVisible = true;
        private Vector3 waterSize;
        private ParticleSystem spray;
        public float FrontZ { get; private set; }
        public float TopY { get; private set; }
        public static QuakeTsunami Instance { get; private set; }

        private QuakeConfig Cfg => QuakeTimeline.Instance.Config;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Init()
        {
            if (water == null) return;
            waterSize = water.localScale;
            Apply(Cfg.waterCalmFrontZ, groundY - 0.4f);
            if (QuakeFx.Instance != null && waveBody != null)
            {
                var root = new GameObject("WaveSpray").transform;
                root.SetParent(transform, false);
                spray = QuakeFx.Loop(root, QuakeFx.Instance.Smoke, new Color(0.85f, 0.9f, 0.92f, 0.6f), 2.5f, 7f, 3.5f, 0.5f, 12f, 90f);
            }
            SetWaveVisible(false);
        }

        /// <summary>해일이 밀려오는 진행도 (0~1). 물이 빠지는 동안·해일 단계 전에는 -1</summary>
        public float ArrivalProgress
        {
            get
            {
                QuakeTimeline tl = QuakeTimeline.Instance;
                if (tl == null || tl.Phase != QuakePhase.Tsunami) return -1f;
                float t = tl.PhaseElapsed;
                if (t < Cfg.tsunamiDrawbackEnd) return -1f;
                return Mathf.Clamp01((t - Cfg.tsunamiDrawbackEnd) / (Cfg.tsunamiArrivalEnd - Cfg.tsunamiDrawbackEnd));
            }
        }

        /// <summary>해일이 다가오는 정도 (0 = 평소, 1 = 최고 수위) — 굉음 음량</summary>
        public float Approach
        {
            get
            {
                QuakeTimeline tl = QuakeTimeline.Instance;
                if (tl == null || tl.Phase != QuakePhase.Tsunami) return 0f;
                float t = tl.PhaseElapsed;
                if (t < Cfg.tsunamiDrawbackEnd) return 0.15f * (t / Cfg.tsunamiDrawbackEnd);
                return Mathf.Clamp01(0.15f + 0.85f * (t - Cfg.tsunamiDrawbackEnd) / (Cfg.tsunamiArrivalEnd - Cfg.tsunamiDrawbackEnd));
            }
        }

        private void Update()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (water == null || finished || tl == null || tl.Phase != QuakePhase.Tsunami) return;
            QuakeConfig c = Cfg;
            float t = tl.PhaseElapsed, calmTop = groundY - 0.4f, drawTop = groundY - 2f, peakTop = groundY + c.waterPeakHeight;
            if (t < c.tsunamiDrawbackEnd)
            {
                float k = Smooth(t / c.tsunamiDrawbackEnd);
                Apply(Mathf.Lerp(c.waterCalmFrontZ, c.waterDrawbackFrontZ, k), Mathf.Lerp(calmTop, drawTop, k));
            }
            else if (t < c.tsunamiArrivalEnd)
            {
                float k = Smooth((t - c.tsunamiDrawbackEnd) / (c.tsunamiArrivalEnd - c.tsunamiDrawbackEnd));
                Apply(Mathf.Lerp(c.waterDrawbackFrontZ, c.waterPeakFrontZ, k), Mathf.Lerp(drawTop, peakTop, k));
            }
            Transform p = tl.Player.transform;
            if (p.position.z > FrontZ && p.position.y < TopY - drownMargin) { finished = true; Debug.Log($"[Quake] 해일이 덮침 front={FrontZ:0.0} top={TopY:0.0} player={p.position}"); QuakeOutcome.Instance.OnEngulfed(); }
            else if (t >= c.tsunamiHoldEnd) { finished = true; QuakeOutcome.Instance.OnTsunamiPassed(); }
        }

        private void LateUpdate()
        {
            if (waveBody == null || waveCrest == null) return;
            float pr = ArrivalProgress;
            if (pr < 0f) { SetWaveVisible(false); return; }
            float rise = Mathf.SmoothStep(0f, 1f, pr / 0.12f);
            float settle = 1f - Mathf.SmoothStep(0f, 1f, (pr - 0.8f) / 0.2f);
            float height = maxWaveHeight * rise * settle;
            if (pr >= 1f && height < 0.05f) { SetWaveVisible(false); return; }
            SetWaveVisible(true);
            waveBody.position = new Vector3(waveBody.position.x, TopY - 0.5f, FrontZ);
            waveBody.localScale = new Vector3(waveBody.localScale.x, height + 0.5f, waveBody.localScale.z);
            waveCrest.position = new Vector3(waveCrest.position.x, TopY + height, FrontZ - 0.6f);
            if (spray != null) spray.transform.position = new Vector3(QuakeTimeline.Instance.Player.transform.position.x, TopY + height, FrontZ);
        }

        private void SetWaveVisible(bool v)
        {
            if (waveVisible == v) return;
            waveVisible = v;
            if (waveBody != null) waveBody.gameObject.SetActive(v);
            if (waveCrest != null) waveCrest.gameObject.SetActive(v);
            if (spray != null) { if (v) spray.Play(); else spray.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        private void Apply(float frontZ, float topY)
        {
            FrontZ = frontZ; TopY = topY;
            water.position = new Vector3(water.position.x, topY - waterSize.y / 2f, frontZ + waterSize.z / 2f);
        }

        private static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }
}
