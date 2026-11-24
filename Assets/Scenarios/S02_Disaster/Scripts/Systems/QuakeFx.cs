using GameCore;
using UnityEngine;
using UnityEngine.Audio;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 2 효과 모음 (원본 FxLibrary + ParticleFactory + ExplosionFx + FireFx + SoundPlayer 이식).
    /// 재질·소리는 빌더가 연결한다. 파티클은 에셋 없이 코드로 만든다. 소리는 공통 오디오 믹서의 SFX 그룹으로 보낸다(설정 음량 반영).
    /// </summary>
    public class QuakeFx : MonoBehaviour
    {
        [SerializeField] private Material fire;
        [SerializeField] private Material smoke;
        [SerializeField] private Material fireball;
        [SerializeField] private Material rain;
        [SerializeField] private Material debris;
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private AudioClip fireLoopSound;

        public static readonly Color FireColor = new Color(1f, 0.55f, 0.2f);
        public static readonly Color SmokeColor = new Color(0.22f, 0.21f, 0.2f, 0.75f);
        public static readonly Color RainColor = new Color(0.75f, 0.8f, 0.88f, 0.45f);

        public static QuakeFx Instance { get; private set; }
        public Material Fire => fire;
        public Material Smoke => smoke;
        public Material Fireball => fireball;
        public Material Rain => rain;
        public Material Debris => debris;
        public bool IsValid => fire != null && smoke != null && fireball != null;

        private static AudioMixerGroup sfxGroup;
        private AudioSource[] pool;
        private float[] startedAt;

        private void Awake()
        {
            Instance = this;
            var mixer = Resources.Load<AudioMixer>(GameSettings.MixerResourceName);
            if (mixer != null) { AudioMixerGroup[] g = mixer.FindMatchingGroups("SFX"); if (g != null && g.Length > 0) sfxGroup = g[0]; }
            pool = new AudioSource[16]; startedAt = new float[16];
            for (int i = 0; i < pool.Length; i++)
            {
                var c = new GameObject("Voice");
                c.transform.SetParent(transform, false);
                pool[i] = c.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].outputAudioMixerGroup = sfxGroup;
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ═════════════════════ 소리 ═════════════════════
        public static AudioMixerGroup SfxGroup => sfxGroup;

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.06f, float maxDistance = 25f)
        {
            AudioSource s = Instance != null ? Instance.Prepare(clip, volume, pitchJitter) : null;
            if (s == null) return;
            s.transform.position = position;
            s.spatialBlend = 1f; s.minDistance = 1.5f; s.maxDistance = maxDistance; s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.Play();
        }

        public static void Play2D(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            AudioSource s = Instance != null ? Instance.Prepare(clip, volume, pitchJitter) : null;
            if (s == null) return;
            s.spatialBlend = 0f;
            s.Play();
        }

        private AudioSource Prepare(AudioClip clip, float volume, float jitter)
        {
            if (clip == null) return null;
            int idx = 0;
            for (int i = 0; i < pool.Length; i++) { if (!pool[i].isPlaying) { idx = i; break; } if (startedAt[i] < startedAt[idx]) idx = i; }
            startedAt[idx] = Time.unscaledTime;
            AudioSource s = pool[idx];
            s.Stop(); s.clip = clip; s.volume = volume; s.pitch = 1f + Random.Range(-jitter, jitter);
            return s;
        }

        // ═════════════════════ 파티클 ═════════════════════
        public static ParticleSystem Burst(Transform parent, Material material, Color color, float lifetime, float speed, float size, float gravity, float radius, int count)
        {
            ParticleSystem ps = Create(parent, "Burst", material, color, lifetime, speed, size, gravity, radius, false);
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ps.Play();
            return ps;
        }

        public static ParticleSystem Loop(Transform parent, Material material, Color color, float lifetime, float speed, float size, float gravity, float radius, float rate)
        {
            ParticleSystem ps = Create(parent, "Loop", material, color, lifetime, speed, size, gravity, radius, true);
            var em = ps.emission; em.rateOverTime = rate;
            ps.Play();
            return ps;
        }

        private static ParticleSystem Create(Transform parent, string name, Material material, Color color, float lifetime, float speed, float size, float gravity, float radius, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color; main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 2000;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        // ═════════════════════ 폭발 / 불 ═════════════════════
        /// <summary>한 번 터지는 폭발: 섬광 + 커지며 사라지는 불덩이 + 불꽃/연기</summary>
        public static void Explosion(Vector3 position, float scale)
        {
            QuakeFx lib = Instance;
            if (lib == null || !lib.IsValid) return;
            var go = new GameObject("Explosion");
            go.transform.position = position;
            go.AddComponent<QuakeExplosionFx>().Init(lib, scale);
        }

        /// <summary>계속 타오르는 불 (불꽃 + 연기 + 선택: 일렁이는 조명 + 타닥 소리)</summary>
        public static GameObject FireAt(Vector3 position, float scale, bool withLight = true)
        {
            QuakeFx lib = Instance;
            if (lib == null || !lib.IsValid) return null;
            var go = new GameObject("Fire");
            go.transform.position = position;
            go.AddComponent<QuakeFireFx>().Init(lib, scale, withLight);
            return go;
        }

        public AudioClip ExplosionSound => explosionSound;
        public AudioClip FireLoopSound => fireLoopSound;
    }
}
