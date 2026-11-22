using System.Collections.Generic;
using UnityEngine;

namespace GameGameplay
{
    public enum WarSound { Alert, Siren, Jet, Knock, Boom, Static, Rotor, Rumble }

    /// <summary>
    /// 시나리오 1 효과음. 외부 에셋 없이 절차 생성(placeholder)한 클립을 재생한다.
    /// 실제 음원이 생기면 overrides 슬롯(Inspector)에 끼우면 그것이 우선한다 — 비워 두면 절차 음이 난다.
    /// </summary>
    public class WarAudio : MonoBehaviour
    {
        [System.Serializable] public struct Override { public WarSound sound; public AudioClip clip; }
        [SerializeField] private Override[] overrides;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;

        public static WarAudio Instance { get; private set; }
        private const int Rate = 22050;
        private readonly Dictionary<WarSound, AudioClip> cache = new Dictionary<WarSound, AudioClip>();
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private AudioSource loop;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(WarSound sound, float volume = 1f)
        {
            AudioClip clip = Clip(sound);
            if (clip == null) return;
            AudioSource src = Free();
            src.loop = false; src.clip = clip; src.volume = volume * masterVolume; src.spatialBlend = 0f; src.Play();
        }

        /// <summary>반복 재생 (헬기 로터 등) — 이미 반복 중이면 교체</summary>
        public void PlayLoop(WarSound sound, float volume = 0.6f)
        {
            AudioClip clip = Clip(sound);
            if (clip == null) return;
            if (loop == null) loop = NewSource();
            loop.loop = true; loop.clip = clip; loop.volume = volume * masterVolume; loop.spatialBlend = 0f; loop.Play();
        }

        public void StopLoop() { if (loop != null) loop.Stop(); }

        private AudioSource Free()
        {
            foreach (AudioSource s in pool) if (!s.isPlaying) return s;
            return NewSource();
        }

        private AudioSource NewSource()
        {
            var go = new GameObject("WarAudioSource");
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            pool.Add(s);
            return s;
        }

        public AudioClip GetClip(WarSound sound) => Clip(sound);

        private AudioClip Clip(WarSound sound)
        {
            if (overrides != null) foreach (Override o in overrides) if (o.sound == sound && o.clip != null) return o.clip;
            if (cache.TryGetValue(sound, out AudioClip c) && c != null) return c;
            c = Make(sound);
            cache[sound] = c;
            return c;
        }

        // ───────── 절차 생성 (placeholder) ─────────
        private static AudioClip Make(WarSound sound)
        {
            float len;
            switch (sound)
            {
                case WarSound.Alert: len = 2.4f; break;
                case WarSound.Siren: len = 8f; break;
                case WarSound.Jet: len = 5.5f; break;
                case WarSound.Knock: len = 1.6f; break;
                case WarSound.Boom: len = 6f; break;
                case WarSound.Static: len = 1.5f; break;
                case WarSound.Rotor: len = 1.0f; break;
                default: len = 4f; break;
            }
            int n = Mathf.CeilToInt(len * Rate);
            var d = new float[n];
            var rng = new System.Random((int)sound * 97 + 13);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = 0f;
                switch (sound)
                {
                    case WarSound.Alert:
                    {
                        float beat = t % 0.8f;
                        float on = beat < 0.35f ? 1f : 0f;
                        v = on * (Mathf.Sin(2f * Mathf.PI * (beat < 0.17f ? 1046f : 784f) * t) * 0.5f);
                        break;
                    }
                    case WarSound.Siren:
                    {
                        float f = 420f + 340f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4f));
                        v = Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f * Mathf.Min(1f, t * 2f) * Mathf.Min(1f, (len - t) * 2f);
                        break;
                    }
                    case WarSound.Jet:
                    {
                        lp += (noise - lp) * Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(Mathf.PI * u));
                        v = lp * 2.2f * Mathf.Sin(Mathf.PI * u);
                        break;
                    }
                    case WarSound.Knock:
                    {
                        float k = (t % 0.5f);
                        float on = t < 1.3f ? Mathf.Exp(-k * 28f) : 0f;
                        v = on * (Mathf.Sin(2f * Mathf.PI * 95f * k) * 0.9f + noise * 0.15f);
                        break;
                    }
                    case WarSound.Boom:
                    {
                        lp += (noise - lp) * 0.04f;
                        v = (lp * 3f + Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.6f) * Mathf.Exp(-t * 0.75f) * Mathf.Min(1f, t * 20f);
                        break;
                    }
                    case WarSound.Static:
                    {
                        v = noise * 0.35f * (0.6f + 0.4f * Mathf.Sin(t * 40f));
                        break;
                    }
                    case WarSound.Rotor:
                    {
                        float ph = t % 0.25f;
                        v = Mathf.Exp(-ph * 22f) * (Mathf.Sin(2f * Mathf.PI * 60f * ph) * 0.9f) + noise * 0.03f;
                        break;
                    }
                    default:
                    {
                        lp += (noise - lp) * 0.02f;
                        v = lp * 2.5f * Mathf.Min(1f, t) * Mathf.Min(1f, (len - t));
                        break;
                    }
                }
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("War_" + sound, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
