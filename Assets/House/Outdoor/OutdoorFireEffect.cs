using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 8층 외벽에서 터져 나오는 불꽃과 검은 연기. 폭발 때 Ignite() 로 시작해서 rampSeconds 동안 점점 거세진다.
    /// 연기는 월드 좌표로 위(10층 베란다 쪽)로 솟아올라 창밖 시야를 약간 가린다. 불빛은 외벽을 주황색으로 비추며 깜빡인다.
    /// </summary>
    public class OutdoorFireEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light fireLight;
        [SerializeField] private AudioSource loopAudio;

        [Header("강도 곡선")]
        [SerializeField, Min(1f)] private float rampSeconds = 90f;
        [SerializeField] private float maxFlameRate = 60f, maxSmokeRate = 45f, maxSparkRate = 25f;
        [SerializeField] private float maxLightIntensity = 6f;
        [SerializeField, Range(0f, 1f)] private float flicker = 0.35f;

        private bool lit;
        private float litAt;

        public bool IsLit => lit;
        /// <summary>0~1: 불이 붙은 뒤 경과에 따른 강도</summary>
        public float Intensity => lit ? Mathf.Clamp01(0.25f + (Time.time - litAt) / rampSeconds) : 0f;

        private void Awake() => SetRates(0f);

        public void Ignite()
        {
            if (lit) return;
            lit = true; litAt = Time.time;
            foreach (ParticleSystem ps in new[] { flame, smoke, sparks }) if (ps != null) ps.Play();
            if (loopAudio != null && loopAudio.clip != null) loopAudio.Play();
        }

        private void Update()
        {
            if (!lit) return;
            float k = Intensity;
            SetRates(k);
            if (fireLight != null)
            {
                fireLight.enabled = true;
                fireLight.intensity = maxLightIntensity * k * (1f - flicker + flicker * Mathf.PerlinNoise(Time.time * 7f, 0.3f) * 2f);
            }
            if (loopAudio != null) loopAudio.volume = k;
        }

        private void SetRates(float k)
        {
            Rate(flame, maxFlameRate * k); Rate(smoke, maxSmokeRate * k); Rate(sparks, maxSparkRate * k);
            if (fireLight != null && k <= 0f) fireLight.enabled = false;
        }

        private static void Rate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var em = ps.emission; em.rateOverTime = rate;
        }
    }
}
