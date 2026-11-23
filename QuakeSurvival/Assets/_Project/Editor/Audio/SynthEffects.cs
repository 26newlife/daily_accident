using System;
using static QuakeSurvival.EditorTools.AudioSynth.SynthDsp;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>한 번 재생하는 효과음 합성: 천둥, 폭발, 삐걱임, 조작음, 알림음, 결과 화면.</summary>
    internal static class SynthEffects
    {
        public static float[] Thunder()
        {
            int n = Samples(5f);
            float[] crack = Envelope(HighPass(White(n, 71), 0.15f), t => Decay(t, 0.08f));
            float[] roll = Envelope(LowPass(Brown(n, 72), 0.06f),
                t => Decay(t, 1.6f) * (0.6f + 0.4f * (float)Math.Sin(t * 5f + Math.Sin(t * 2.3f))));
            return EdgeFade(Normalize(Mix((crack, 0.5f), (roll, 1f)), 0.9f));
        }

        public static float[] Explosion()
        {
            int n = Samples(2.6f);
            float[] blast = Envelope(LowPass(White(n, 81), 0.25f), t => Decay(t, 0.35f));
            float[] boom = Envelope(Tone(n, t => 55f - 15f * Math.Min(1f, t)), t => Decay(t, 0.6f));
            float[] debris = Envelope(LowPass(Brown(n, 82), 0.1f), t => Decay(t, 0.9f));
            return EdgeFade(Normalize(Mix((blast, 1f), (boom, 0.8f), (debris, 0.5f)), 0.95f));
        }

        /// <summary>건물이 비틀리며 내는 낮은 삐걱임.</summary>
        public static float[] Creak()
        {
            int n = Samples(1.3f);
            var rng = new Random(91);
            float[] saw = Tone(n, t => 85f + 25f * (float)Math.Sin(t * 9f) + (float)rng.NextDouble() * 8f, v => SoftClip(v * 3f));
            float[] body = LowPass(saw, 0.15f);
            return EdgeFade(Normalize(Envelope(body, t => (float)Math.Sin(Math.PI * Math.Min(1f, t / 1.3f))), 0.7f), 0.02f);
        }

        /// <summary>물건이 흔들려 부딪히는 덜컹 소리(짧은 충돌음 여러 개).</summary>
        public static float[] Clatter()
        {
            int n = Samples(0.6f);
            float[] hit = Envelope(HighPass(White(Samples(0.04f), 101), 0.08f), t => Decay(t, 0.01f));
            return EdgeFade(Normalize(Scatter(n, 102, 7, hit, 0.4f, 1f), 0.7f));
        }

        public static float[] Valve()
        {
            int n = Samples(0.7f);
            float[] squeak = Tone(n, t => 1500f + 250f * (1f - t / 0.7f) + 40f * (float)Math.Sin(t * 60f));
            float[] grind = HighPass(White(n, 111), 0.2f);
            return EdgeFade(Normalize(Envelope(Mix((squeak, 0.5f), (grind, 0.15f)), t => (float)Math.Sin(Math.PI * t / 0.7f)), 0.45f));
        }

        public static float[] Breaker()
        {
            int n = Samples(0.35f);
            float[] click = Envelope(HighPass(White(n, 121), 0.3f), t => Decay(t, 0.006f));
            float[] thunk = Envelope(Tone(n, t => 120f), t => Decay(t, 0.05f));
            return EdgeFade(Normalize(Mix((click, 0.7f), (thunk, 1f)), 0.8f));
        }

        public static float[] Door()
        {
            int n = Samples(1.1f);
            var rng = new Random(131);
            float[] creak = Tone(n, t => 230f - 70f * t + (float)rng.NextDouble() * 25f, v => SoftClip(v * 2f));
            return EdgeFade(Normalize(Envelope(LowPass(creak, 0.3f), t => (float)Math.Sin(Math.PI * t / 1.1f)), 0.5f), 0.02f);
        }

        /// <summary>물건 집기(천 스치는 소리).</summary>
        public static float[] Pickup()
        {
            int n = Samples(0.3f);
            float[] rustle = LowPass(HighPass(White(n, 141), 0.15f), 0.5f);
            return EdgeFade(Normalize(Envelope(rustle, t => (float)Math.Sin(Math.PI * t / 0.3f)), 0.5f));
        }

        public static float[] ButtonClick()
        {
            int n = Samples(0.08f);
            return EdgeFade(Normalize(Envelope(HighPass(White(n, 151), 0.3f), t => Decay(t, 0.008f)), 0.5f), 0.002f);
        }

        public static float[] ElevatorDing()
        {
            int n = Samples(1.4f);
            float[] a = Tone(n, t => 1318.5f);
            float[] b = Tone(n, t => 2637f);
            return EdgeFade(Normalize(Envelope(Mix((a, 1f), (b, 0.25f)), t => Decay(t, 0.45f)), 0.55f));
        }

        /// <summary>긴급재난문자 수신음: 높은 두 음을 빠르게 번갈아 울리는 경보.</summary>
        public static float[] Alert()
        {
            int n = Samples(3f);
            float[] tone = Tone(n, t => ((int)(t / 0.2f) % 2 == 0) ? 1050f : 1400f, v => SoftClip(v * 1.6f));
            float[] pulse = Envelope(tone, t => ((t % 1f) < 0.85f) ? 1f : 0f);
            return EdgeFade(Normalize(LowPass(pulse, 0.6f), 0.6f));
        }

        public static float[] TextSent()
        {
            int n = Samples(0.35f);
            float[] blip = Tone(n, t => t < 0.12f ? 880f : 1320f);
            return EdgeFade(Normalize(Envelope(blip, t => Decay(t, 0.15f)), 0.5f));
        }

        public static float[] Footstep()
        {
            int n = Samples(0.18f);
            float[] thud = Envelope(LowPass(White(n, 161), 0.08f), t => Decay(t, 0.03f));
            float[] scuff = Envelope(HighPass(White(n, 162), 0.3f), t => Decay(t, 0.015f));
            return EdgeFade(Normalize(Mix((thud, 1f), (scuff, 0.2f)), 0.6f));
        }

        public static float[] GlassCrunch()
        {
            int n = Samples(0.5f);
            float[] shard = Envelope(HighPass(White(Samples(0.012f), 171), 0.5f), t => Decay(t, 0.002f));
            return EdgeFade(Normalize(Scatter(n, 172, 40, shard, 0.3f, 1f), 0.75f));
        }

        public static float[] DeathSting()
        {
            int n = Samples(2.8f);
            float[] boom = Envelope(LowPass(Brown(n, 181), 0.05f), t => Decay(t, 0.8f));
            float[] fall = Envelope(Tone(n, t => 220f - 90f * Math.Min(1f, t / 2.5f)), t => Decay(t, 1.2f));
            return EdgeFade(Normalize(Mix((boom, 0.8f), (fall, 0.6f)), 0.8f));
        }

        public static float[] ClearSting()
        {
            int n = Samples(1.6f);
            Func<float, float> chord = t => t < 0.18f ? 523.25f : (t < 0.36f ? 659.25f : 783.99f);
            float[] melody = Tone(n, chord);
            float[] pad = Tone(n, t => 261.6f);
            return EdgeFade(Normalize(Envelope(Mix((melody, 1f), (pad, 0.3f)), t => Decay(t, 0.7f)), 0.55f));
        }
    }
}
