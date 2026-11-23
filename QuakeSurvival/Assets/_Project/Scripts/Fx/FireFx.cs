using QuakeSurvival.Audio;
using QuakeSurvival.Art;
using UnityEngine;

namespace QuakeSurvival.Fx
{
    /// <summary>계속 타오르는 불: 불꽃 + 연기 파티클 + 일렁이는 조명.</summary>
    public sealed class FireFx : MonoBehaviour
    {
        private Light glow;
        private float seed;

        /// <param name="withLight">false면 일렁이는 조명 없이 불꽃/연기만 만든다(작은 불이 많을 때 성능용).</param>
        public static FireFx Spawn(FxLibrary lib, Vector3 position, float scale, bool withLight = true)
        {
            var go = new GameObject("Fire");
            go.transform.position = position;
            var fx = go.AddComponent<FireFx>();
            fx.Init(lib, scale, withLight);
            return fx;
        }

        private void Init(FxLibrary lib, float scale, bool withLight)
        {
            seed = Random.value * 100f;
            if (withLight)
            {
                glow = gameObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = Palette.Fire;
                glow.range = 8f * scale;
            }

            var flames = new ParticleFactory.Spec(Palette.Fire, 1f, 2.5f * scale, 0.9f * scale, -0.3f, 0.6f * scale);
            var smoke = new ParticleFactory.Spec(Palette.Smoke, 4f, 2f * scale, 2.5f * scale, -0.1f, 0.6f * scale);
            ParticleFactory.Loop(transform, lib.Fire, flames, 40f * scale);
            ParticleFactory.Loop(transform, lib.Smoke, smoke, 8f * scale);
            StartCrackle(lib.FireLoopSound, scale);
        }

        private void StartCrackle(AudioClip clip, float scale)
        {
            if (clip == null) return;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 20f;
            MixedVolume.Attach(source, AudioCategory.Sfx, 0.5f * scale);
            source.time = Random.Range(0f, clip.length * 0.9f);
            source.Play();
        }

        private void Update()
        {
            if (glow != null) glow.intensity = 4f + Mathf.PerlinNoise(Time.time * 6f, seed) * 4f;
        }
    }
}
