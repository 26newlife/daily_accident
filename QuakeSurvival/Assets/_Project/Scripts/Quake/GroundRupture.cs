using System;
using QuakeSurvival.Audio;
using QuakeSurvival.Fx;
using QuakeSurvival.UI;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 땅이 갈라지는 균열과 땅이 꺼지는 싱크홀.
    /// - 균열: 정해진 단계/시각에 길이 0에서 끝까지 쩍 벌어진다. 다 벌어지면 위험 구역이 켜져서 밟으면 사망
    ///   (위험 구역이 낮아서 점프로 뛰어넘을 수 있다).
    /// - 싱크홀: 먼저 주변 땅이 울리고 금이 가며(경고), 몇 초 뒤 구멍이 열린다.
    ///   구멍이 열리는 순간 위험 구역(HazardVolume)이 켜져서, 그 안에 있거나 들어가면 사망한다.
    /// 시각은 QuakeDirector의 단계 기준이며, 그 단계를 이미 지났으면 바로 실행한다.
    /// </summary>
    public sealed class GroundRupture : MonoBehaviour
    {
        [Serializable]
        public struct Crack
        {
            [Tooltip("균열 조각의 시작점. 자식 상자는 +x 방향으로 뻗어 있어 x 크기로 길이를 조절한다.")]
            public Transform pivot;
            [Tooltip("다 벌어지면 켜지는 위험 구역(처음엔 꺼 둔다). 없으면 장식.")]
            public GameObject hazard;
            public QuakePhase phase;
            public float delay;
            public float openSeconds;
        }

        [Serializable]
        public struct Sinkhole
        {
            [Tooltip("구멍 모양(검은 원판 + 가장자리 잔해). 처음엔 크기 0으로 숨긴다.")]
            public Transform pit;
            [Tooltip("구멍이 열리면 켜지는 위험 구역(처음엔 꺼 둔다).")]
            public GameObject hazard;
            public QuakePhase phase;
            [Tooltip("구멍이 열리는 시각(단계 시작 기준, 초). warningSeconds보다 커야 경고가 먼저 나온다.")]
            public float delay;
        }

        [SerializeField] private QuakeDirector director;
        [SerializeField] private FxLibrary fx;
        [SerializeField] private CameraShake shake;
        [SerializeField] private Transform player;
        [SerializeField] private MessageUI messages;
        [SerializeField] private Crack[] cracks = new Crack[0];
        [SerializeField] private Sinkhole[] sinkholes = new Sinkhole[0];

        [Header("싱크홀")]
        [SerializeField] private float warningSeconds = 3.5f;
        [SerializeField] private float openSeconds = 1.2f;
        [SerializeField] private float warnRadius = 30f;

        private Vector3[] crackScales;
        private float[] crackStart;
        private float[] holeStart;
        private bool[] holeWarned;

        // 참조가 빠져도 균열과 구멍은 처음부터 보이지 않도록 숨기기를 먼저 한다
        private void Awake()
        {
            HideAll();
            if (director == null || fx == null || !fx.IsValid || shake == null || player == null || messages == null)
            {
                Debug.LogError("[GroundRupture] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void HideAll()
        {
            crackScales = new Vector3[cracks.Length];
            crackStart = new float[cracks.Length];
            for (int i = 0; i < cracks.Length; i++)
            {
                crackStart[i] = -1f;
                if (cracks[i].hazard != null) cracks[i].hazard.SetActive(false);
                if (cracks[i].pivot == null) continue;
                crackScales[i] = cracks[i].pivot.localScale;
                cracks[i].pivot.localScale = new Vector3(0f, crackScales[i].y, crackScales[i].z);
            }
            holeStart = new float[sinkholes.Length];
            holeWarned = new bool[sinkholes.Length];
            for (int i = 0; i < sinkholes.Length; i++)
            {
                holeStart[i] = -1f;
                if (sinkholes[i].pit != null) sinkholes[i].pit.localScale = Vector3.zero;
                if (sinkholes[i].hazard != null) sinkholes[i].hazard.SetActive(false);
            }
        }

        private void Update()
        {
            for (int i = 0; i < cracks.Length; i++) UpdateCrack(i);
            for (int i = 0; i < sinkholes.Length; i++) UpdateSinkhole(i);
        }

        /// <summary>단계 기준 시각에 도달했는지(그 단계를 이미 지났으면 true).</summary>
        private bool Reached(QuakePhase phase, float delay)
        {
            if (director.Phase != phase) return director.Phase > phase;
            return director.PhaseElapsed >= delay;
        }

        private void UpdateCrack(int i)
        {
            Crack c = cracks[i];
            if (c.pivot == null) return;
            if (crackStart[i] < 0f)
            {
                if (!Reached(c.phase, c.delay)) return;
                crackStart[i] = Time.time;
            }
            float t = Mathf.Clamp01((Time.time - crackStart[i]) / Mathf.Max(0.01f, c.openSeconds));
            Vector3 full = crackScales[i];
            c.pivot.localScale = new Vector3(full.x * EaseOut(t), full.y, full.z);
            if (t >= 1f && c.hazard != null && !c.hazard.activeSelf) c.hazard.SetActive(true);
        }

        private void UpdateSinkhole(int i)
        {
            Sinkhole s = sinkholes[i];
            if (s.pit == null) return;
            if (!holeWarned[i] && Reached(s.phase, s.delay - warningSeconds)) Warn(i);
            if (holeStart[i] < 0f)
            {
                if (!Reached(s.phase, s.delay)) return;
                Open(i);
            }
            float t = Mathf.Clamp01((Time.time - holeStart[i]) / Mathf.Max(0.01f, openSeconds));
            s.pit.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, EaseOut(t));
        }

        private void Warn(int i)
        {
            holeWarned[i] = true;
            if (!IsNear(sinkholes[i].pit.position)) return;
            shake.AddImpulse(0.25f);
            messages.ShowSubtitle("(발밑에서 무언가 무너지는 소리가 난다... 땅이 갈라지고 있다!)");
        }

        private void Open(int i)
        {
            holeStart[i] = Time.time;
            holeWarned[i] = true;
            Vector3 center = sinkholes[i].pit.position;
            Debug.Log($"[GroundRupture] sinkhole opens at {center}");
            if (sinkholes[i].hazard != null) sinkholes[i].hazard.SetActive(true);

            float distance = Vector3.Distance(center, player.position);
            shake.AddImpulse(Mathf.Clamp01(1f - distance / 60f));
            SoundPlayer.PlayAt(fx.ExplosionSound, center, AudioCategory.Sfx, 0.6f, 0.1f, 70f);

            var dust = new GameObject("SinkholeDust").transform;
            dust.position = center + Vector3.up * 0.5f;
            var spec = new ParticleFactory.Spec(new Color(0.45f, 0.42f, 0.38f, 0.75f), 5f, 3.5f, 4f, -0.03f, 2.5f);
            ParticleFactory.Burst(dust, fx.Smoke, spec, 35);
            Destroy(dust.gameObject, 10f);
        }

        private bool IsNear(Vector3 point)
        {
            Vector3 p = player.position;
            var flat = new Vector2(p.x - point.x, p.z - point.z);
            return flat.magnitude <= warnRadius && Mathf.Abs(p.y - point.y) < 5f;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
