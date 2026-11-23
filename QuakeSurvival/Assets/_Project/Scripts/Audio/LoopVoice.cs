using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>
    /// 반복 재생 소리 하나(비, 땅울림, 사이렌 등). Target 음량으로 부드럽게 따라가며
    /// 음량이 0이 되면 멈추고, 0보다 커지면 다시 재생한다.
    /// </summary>
    public sealed class LoopVoice
    {
        private readonly AudioSource source;
        private readonly AudioCategory category;
        private readonly float fadeSpeed;
        private float current;

        public float Target { get; set; }

        public LoopVoice(GameObject host, AudioClip clip, AudioCategory category, float fadeSpeed, bool spatial)
        {
            this.category = category;
            this.fadeSpeed = fadeSpeed;
            source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = 3f;
            source.maxDistance = 80f;
        }

        public float Pitch
        {
            set => source.pitch = value;
        }

        public void Tick(float deltaTime)
        {
            if (source.clip == null) return;
            current = Mathf.MoveTowards(current, Mathf.Clamp01(Target), fadeSpeed * deltaTime);
            source.volume = current * AudioMix.Gain(category);

            if (current > 0.001f && !source.isPlaying)
            {
                source.time = Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
            else if (current <= 0.001f && source.isPlaying)
            {
                source.Stop();
            }
        }

        public void StopNow()
        {
            current = 0f;
            Target = 0f;
            source.Stop();
        }
    }
}
