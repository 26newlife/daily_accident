using UnityEngine;

namespace GameGameplay
{
    /// <summary>시나리오 B: 문이 열리는 순간 복도로 터져 나오는 불꽃·연기 (파티클 + 순간 조명). 비그래픽 — 형체 없는 화염/연기와 빛만 쓴다</summary>
    public class ElevatorFireballEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private Light flashLight;
        [SerializeField] private AudioSource blastAudio;
        [SerializeField, Min(0.1f)] private float lightSeconds = 1.4f;
        [SerializeField] private int flameBurst = 80;
        [SerializeField] private int smokeBurst = 60;

        private float lightT = -1f;

        public void Play()
        {
            if (flame != null) flame.Emit(flameBurst);
            if (smoke != null) smoke.Emit(smokeBurst);
            if (blastAudio != null && blastAudio.clip != null) blastAudio.Play();
            if (flashLight != null) { flashLight.enabled = true; flashLight.intensity = 6f; lightT = 0f; }
        }

        private void Update()
        {
            if (lightT < 0f || flashLight == null) return;
            lightT += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(lightT / lightSeconds);
            flashLight.intensity = 6f * k;
            if (k <= 0f) { flashLight.enabled = false; lightT = -1f; }
        }
    }
}
