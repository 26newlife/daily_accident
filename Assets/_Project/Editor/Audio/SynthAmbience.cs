using System;
using static QuakeSurvival.EditorTools.AudioSynth.SynthDsp;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>배경(반복) 소리 합성: 비, 땅울림, 사이렌, 바다 굉음, 불, 끊기는 통화음.</summary>
    internal static class SynthAmbience
    {
        public static float[] RainOutside()
        {
            int n = Samples(6.5f);
            float[] hiss = HighPass(LowPass(White(n, 11), 0.55f), 0.02f);
            float[] drop = Envelope(HighPass(White(Samples(0.02f), 12), 0.3f), t => Decay(t, 0.004f));
            float[] drops = Scatter(n, 13, 900, drop, 0.2f, 0.7f);
            return SeamlessLoop(Normalize(Mix((hiss, 0.6f), (drops, 0.5f)), 0.6f), 0.5f);
        }

        /// <summary>창과 벽 너머로 들리는 먹먹한 빗소리.</summary>
        public static float[] RainInside()
        {
            int n = Samples(6.5f);
            float[] muffled = LowPass(White(n, 21), 0.06f);
            float[] patter = Scatter(n, 22, 260, Envelope(LowPass(White(Samples(0.03f), 23), 0.25f), t => Decay(t, 0.008f)), 0.2f, 0.6f);
            return SeamlessLoop(Normalize(Mix((muffled, 1f), (patter, 0.35f)), 0.5f), 0.5f);
        }

        /// <summary>땅울림: 갈색 잡음 + 낮은 사인 흔들림.</summary>
        public static float[] Rumble()
        {
            int n = Samples(6.5f);
            float[] body = LowPass(Brown(n, 31), 0.05f);
            float[] wobble = Tone(n, t => 38f + 6f * (float)Math.Sin(t * 1.7f));
            float[] grind = LowPass(White(n, 32), 0.12f);
            return SeamlessLoop(Normalize(Mix((body, 1f), (wobble, 0.35f), (grind, 0.15f)), 0.85f), 0.6f);
        }

        /// <summary>민방위 사이렌: 8초 주기로 음이 올라갔다 내려온다(주기와 길이가 같아 자연스럽게 반복).</summary>
        public static float[] Siren()
        {
            const float period = 8f;
            int n = Samples(period);
            Func<float, float> freq = t => 480f + 420f * (0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * t / period));
            float[] main = Tone(n, freq, v => SoftClip(v * 1.3f));
            float[] second = Tone(n, t => freq(t) * 2f);
            // 위상이 이어지므로 페이드 없이 그대로 반복한다(페이드를 넣으면 이음매에서 소리가 끊긴다).
            return Normalize(Mix((main, 1f), (second, 0.15f)), 0.55f);
        }

        public static float[] OceanRoar()
        {
            int n = Samples(7f);
            float[] low = LowPass(Brown(n, 41), 0.04f);
            float[] surf = LowPass(White(n, 42), 0.2f);
            float[] swell = Envelope(surf, t => 0.5f + 0.5f * (float)Math.Sin(t * 1.3f));
            return SeamlessLoop(Normalize(Mix((low, 1f), (swell, 0.4f)), 0.8f), 0.7f);
        }

        public static float[] FireLoop()
        {
            int n = Samples(4.5f);
            float[] bed = LowPass(White(n, 51), 0.08f);
            float[] pop = Envelope(HighPass(White(Samples(0.015f), 52), 0.2f), t => Decay(t, 0.003f));
            float[] crackle = Scatter(n, 53, 160, pop, 0.3f, 1f);
            return SeamlessLoop(Normalize(Mix((bed, 0.6f), (crackle, 0.7f)), 0.6f), 0.4f);
        }

        /// <summary>
        /// 통화 연결음(한국식 1초 울림, 2초 쉼)에 잡음과 끊김을 섞은 '통화 상태 불량' 루프.
        /// 울림과 끊김은 짧게 부드럽게 켜고 끄며, 잡음은 이음매 없이 이어 붙여 반복할 때 '뚝' 소리가 나지 않게 한다.
        /// </summary>
        public static float[] BadCall()
        {
            int n = Samples(6f);
            float[] ring = Tone(n, t => 440f);
            float[] ring2 = Tone(n, t => 480f);
            float[] tones = Envelope(Mix((ring, 0.5f), (ring2, 0.5f)), RingGate);
            float[] hiss = SeamlessLoop(HighPass(White(n + Samples(0.3f), 61), 0.1f), 0.3f);
            float[] dropout = Dropout(n, 62);
            float[] mixed = Mix((tones, 0.5f), (hiss, 0.12f));
            for (int i = 0; i < n; i++) mixed[i] *= dropout[i];
            return Normalize(mixed, 0.45f);
        }

        /// <summary>3초마다 1초 울림. 앞뒤 20ms에 걸쳐 켜지고 꺼진다.</summary>
        private static float RingGate(float t)
        {
            const float ramp = 0.02f;
            float local = t % 3f;
            if (local >= 1f) return 0f;
            return Math.Min(1f, Math.Min(local / ramp, (1f - local) / ramp));
        }

        /// <summary>
        /// 가끔 소리가 거의 끊기는 구간. 시작과 끝 구간은 항상 켜져 있어 반복 이음매가 매끄럽고,
        /// 켜짐/꺼짐은 짧게 스무딩해 딸깍 소리가 나지 않는다.
        /// </summary>
        private static float[] Dropout(int n, int seed)
        {
            const int block = 900;
            var rng = new Random(seed);
            var gate = new float[n];
            float target = 1f;
            float current = 1f;
            for (int i = 0; i < n; i++)
            {
                if (i % block == 0)
                {
                    bool edge = i < block || i >= n - 2 * block;
                    target = !edge && rng.NextDouble() < 0.25 ? 0.1f : 1f;
                }
                current += (target - current) * 0.01f;
                gate[i] = current;
            }
            return gate;
        }
    }
}
