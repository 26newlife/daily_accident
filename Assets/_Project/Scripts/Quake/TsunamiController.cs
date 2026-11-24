using System;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 지진해일: 바다(물 박스)가 먼저 멀리 빠졌다가(drawback) 육지 쪽으로 밀려오며 차오른다.
    /// 물은 +z(바다) 쪽에서 -z(내륙) 쪽으로 온다. 물이 플레이어를 덮치면 Engulfed, 버티면 Passed.
    /// </summary>
    public sealed class TsunamiController : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private Transform player;
        [SerializeField] private Transform water; // 윗면이 y=top, 앞면(-z쪽)이 z=front가 되도록 배치되는 큰 박스

        [Header("물 위치")]
        [SerializeField] private float calmFrontZ = 25f;
        [SerializeField] private float calmTopY = -0.4f;
        [SerializeField] private float drawbackFrontZ = 70f;
        [SerializeField] private float drawbackTopY = -2f;
        [SerializeField] private float peakFrontZ = -62f;
        [SerializeField] private float peakTopY = 5.5f;

        [Header("시간(쓰나미 단계 시작 기준, 초)")]
        [SerializeField] private float drawbackEnd = 25f;
        [SerializeField] private float arrivalEnd = 55f;
        [SerializeField] private float holdEnd = 70f;
        [SerializeField] private float drownMargin = 0.3f;

        public event Action Engulfed;
        public event Action Passed;

        private bool finished;
        private Vector3 waterSize;

        public float FrontZ { get; private set; }
        public float TopY { get; private set; }

        /// <summary>
        /// 해일이 밀려오는 진행도(0~1). 물이 빠지는 동안과 지진해일 단계 전에는 -1.
        /// 1을 넘긴 뒤(최고 수위 유지)에도 1을 돌려준다.
        /// </summary>
        public float ArrivalProgress
        {
            get
            {
                if (director == null || director.Phase != QuakePhase.Tsunami) return -1f;
                float t = director.PhaseElapsed;
                if (t < drawbackEnd) return -1f;
                return Mathf.Clamp01((t - drawbackEnd) / (arrivalEnd - drawbackEnd));
            }
        }

        /// <summary>해일이 육지 쪽으로 밀려오는 중(또는 최고 수위에 도달함).</summary>
        public bool IsArriving => ArrivalProgress >= 0f;

        /// <summary>해일이 다가오는 정도(0 = 평소, 1 = 최고 수위). 굉음 음량에 쓴다.</summary>
        public float Approach
        {
            get
            {
                if (director == null || director.Phase != QuakePhase.Tsunami) return 0f;
                float t = director.PhaseElapsed;
                if (t < drawbackEnd) return 0.15f * (t / drawbackEnd);
                return Mathf.Clamp01(0.15f + 0.85f * (t - drawbackEnd) / (arrivalEnd - drawbackEnd));
            }
        }

        private void Awake()
        {
            if (director == null || player == null || water == null)
            {
                Debug.LogError("[TsunamiController] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            waterSize = water.localScale;
            Apply(calmFrontZ, calmTopY);
        }

        private void Update()
        {
            if (finished || director.Phase != QuakePhase.Tsunami) return;

            float t = director.PhaseElapsed;
            if (t < drawbackEnd)
            {
                float k = Smooth(t / drawbackEnd);
                Apply(Mathf.Lerp(calmFrontZ, drawbackFrontZ, k), Mathf.Lerp(calmTopY, drawbackTopY, k));
            }
            else if (t < arrivalEnd)
            {
                float k = Smooth((t - drawbackEnd) / (arrivalEnd - drawbackEnd));
                Apply(Mathf.Lerp(drawbackFrontZ, peakFrontZ, k), Mathf.Lerp(drawbackTopY, peakTopY, k));
            }

            if (IsPlayerUnderwater()) Finish(Engulfed);
            else if (t >= holdEnd) Finish(Passed);
        }

        private bool IsPlayerUnderwater()
        {
            Vector3 p = player.position;
            return p.z > FrontZ && p.y < TopY - drownMargin;
        }

        private void Finish(Action evt)
        {
            finished = true;
            Debug.Log($"[TsunamiController] finished front={FrontZ:0.0} top={TopY:0.0} player={player.position}");
            evt?.Invoke();
        }

        private void Apply(float frontZ, float topY)
        {
            FrontZ = frontZ;
            TopY = topY;
            water.position = new Vector3(water.position.x, topY - waterSize.y / 2f, frontZ + waterSize.z / 2f);
        }

        private static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
    }
}
