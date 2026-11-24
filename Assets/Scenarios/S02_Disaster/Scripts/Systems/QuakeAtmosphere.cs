using System.Collections;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 폭우·번개·배경 소리 (원본 RainFx + LightningFlash + QuakeSoundscape 이식).
    ///   · 비: 건물 밖(옥상 포함)에서는 머리 위에서 폭우가 내리고, 실내에서는 막힌 빗소리
    ///   · 지진: 흔들림 세기를 따라가는 땅울림 + 주변 삐걱임·덜컹거림
    ///   · 번개 뒤 천둥, 지진해일 사이렌과 밀려오는 굉음. 결과가 나면 배경 소리를 낮춘다
    /// </summary>
    public class QuakeAtmosphere : MonoBehaviour
    {
        [Header("반복 소리")]
        [SerializeField] private AudioClip rainOutside;
        [SerializeField] private AudioClip rainInside;
        [SerializeField] private AudioClip rumble;
        [SerializeField] private AudioClip siren;
        [SerializeField] private AudioClip oceanRoar;
        [Header("한 번 소리")]
        [SerializeField] private AudioClip creak;
        [SerializeField] private AudioClip clatter;
        [SerializeField] private AudioClip thunder;
        [Header("비 / 번개")]
        [SerializeField] private float rainHeight = 12f, rainRate = 1500f;
        [SerializeField] private Vector2 lightningInterval = new Vector2(8f, 20f);
        [SerializeField] private float flashIntensity = 3f;

        public static QuakeAtmosphere Instance { get; private set; }
        public AudioClip Clatter => clatter;
        public AudioClip Creak => creak;

        private AudioSource rainOut, rainIn, ground, sirenVoice, roar;
        private ParticleSystem rain;
        private Transform rainRoot;
        private float nextDebris, duck = 1f;
        private bool running;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Begin()
        {
            rainOut = Loop(rainOutside); rainIn = Loop(rainInside); ground = Loop(rumble); sirenVoice = Loop(siren); roar = Loop(oceanRoar);
            if (QuakeFx.Instance != null && QuakeFx.Instance.Rain != null)
            {
                rainRoot = new GameObject("RainFx").transform;
                rainRoot.SetParent(transform, false);
                rain = QuakeFx.Loop(rainRoot, QuakeFx.Instance.Rain, QuakeFx.RainColor, 0.9f, 22f, 0.04f, 0f, 1f, rainRate);
                var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24f, 24f, 0.1f); shape.rotation = new Vector3(90f, 0f, 0f);
                var r = rain.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1f;
                var em = rain.emission; em.enabled = false;
            }
            if (QuakeOutcome.Instance != null) { QuakeOutcome.Instance.Decided -= OnDecided; QuakeOutcome.Instance.Decided += OnDecided; }
            running = true;
            StartCoroutine(Lightning());
        }

        private AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.playOnAwake = false; s.volume = 0f; s.spatialBlend = 0f;
            s.outputAudioMixerGroup = QuakeFx.SfxGroup;
            return s;
        }

        private void OnDecided(DeathReason _) { duck = 0.3f; }

        private void Update()
        {
            if (!running || QuakeTimeline.Instance == null) return;
            QuakeTimeline tl = QuakeTimeline.Instance;
            Transform p = tl.Player != null ? tl.Player.transform : null;
            if (p == null) return;
            float dt = Time.deltaTime;
            bool outdoors = QuakeAreas.Instance != null && QuakeAreas.Instance.Current.IsOutdoors();
            if (rainRoot != null) rainRoot.position = p.position + Vector3.up * rainHeight;
            if (rain != null) { var em = rain.emission; if (em.enabled != outdoors) em.enabled = outdoors; }
            float shake = tl.Intensity;
            Fade(rainOut, (outdoors ? 0.9f : 0.12f) * duck, 0.8f, dt);
            Fade(rainIn, (outdoors ? 0f : 0.55f) * duck, 0.8f, dt);
            Fade(ground, Mathf.Clamp01(shake * 1.2f + tl.Impulse * 0.5f) * duck, 1.5f, dt);
            if (ground != null) ground.pitch = 0.85f + shake * 0.3f;
            Fade(sirenVoice, tl.Phase == QuakePhase.Tsunami ? 0.45f * duck : 0f, 0.3f, dt);
            Fade(roar, (QuakeTsunami.Instance != null ? QuakeTsunami.Instance.Approach : 0f) * duck, 0.5f, dt);

            if (duck >= 1f && shake >= 0.2f && Time.time >= nextDebris)
            {
                nextDebris = Time.time + Random.Range(0.25f, 1.1f) / Mathf.Max(0.3f, shake);
                Vector3 o = Random.insideUnitSphere * 4f; o.y = Mathf.Abs(o.y) * 0.5f + 0.5f;
                QuakeFx.PlayAt(Random.value < 0.35f ? creak : clatter, p.position + o, 0.4f + shake * 0.5f, 0.15f);
            }
        }

        private static void Fade(AudioSource s, float target, float speed, float dt)
        {
            if (s == null || s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, Mathf.Clamp01(target), speed * dt);
            if (s.volume > 0.001f && !s.isPlaying) { s.time = Random.Range(0f, s.clip.length * 0.9f); s.Play(); }
            else if (s.volume <= 0.001f && s.isPlaying) s.Stop();
        }

        private IEnumerator Lightning()
        {
            while (running)
            {
                yield return new WaitForSeconds(Random.Range(lightningInterval.x, lightningInterval.y));
                if (GameFlow.IsEnded) continue;
                Light sun = RenderSettings.sun;
                int flashes = Random.value < 0.5f ? 1 : 2;
                StartCoroutine(Thunder(Random.Range(0.4f, 2.5f)));
                if (sun == null) continue;
                float baseI = sun.intensity; Color baseC = sun.color;
                for (int i = 0; i < flashes; i++)
                {
                    sun.color = Color.white; sun.intensity = Mathf.Max(baseI, flashIntensity);
                    yield return new WaitForSeconds(Random.Range(0.06f, 0.12f));
                    sun.color = baseC; sun.intensity = baseI;
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                }
            }
        }

        private IEnumerator Thunder(float delay)
        {
            yield return new WaitForSeconds(delay);
            QuakeFx.Play2D(thunder, Random.Range(0.55f, 0.9f) * duck, 0.1f);
        }

        public void StopAll()
        {
            running = false;
            StopAllCoroutines();
            foreach (AudioSource s in new[] { rainOut, rainIn, ground, sirenVoice, roar }) if (s != null) s.Stop();
        }
    }
}
