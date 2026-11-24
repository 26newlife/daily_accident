using GameCore;
using UnityEngine;
using UnityEngine.Audio;

namespace GameGameplay
{
    public class QuakeFireFx : MonoBehaviour
    {
        private Light glow; private float seed;

        public void Init(QuakeFx lib, float scale, bool withLight)
        {
            seed = Random.value * 100f;
            if (withLight) { glow = gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.color = QuakeFx.FireColor; glow.range = 8f * scale; glow.shadows = LightShadows.None; }
            QuakeFx.Loop(transform, lib.Fire, QuakeFx.FireColor, 1f, 2.5f * scale, 0.9f * scale, -0.3f, 0.6f * scale, 40f * scale);
            QuakeFx.Loop(transform, lib.Smoke, QuakeFx.SmokeColor, 4f, 2f * scale, 2.5f * scale, -0.1f, 0.6f * scale, 8f * scale);
            if (lib.FireLoopSound != null)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.clip = lib.FireLoopSound; s.loop = true; s.spatialBlend = 1f; s.minDistance = 1.5f; s.maxDistance = 20f;
                s.volume = 0.5f * Mathf.Clamp01(scale); s.outputAudioMixerGroup = QuakeFx.SfxGroup;
                s.time = Random.Range(0f, lib.FireLoopSound.length * 0.9f); s.Play();
            }
        }

        private void Update() { if (glow != null) glow.intensity = 4f + Mathf.PerlinNoise(Time.time * 6f, seed) * 4f; }
    }
}
