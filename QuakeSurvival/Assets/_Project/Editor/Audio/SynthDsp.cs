using System;

namespace QuakeSurvival.EditorTools.AudioSynth
{
    /// <summary>
    /// 소리 합성용 기본 도구(새 배열을 돌려주고 입력은 바꾸지 않는다).
    /// 샘플레이트 22050Hz 모노, 값 범위 -1~1.
    /// </summary>
    internal static class SynthDsp
    {
        public const int Rate = 22050;

        public static int Samples(float seconds) => (int)(seconds * Rate);

        public static float[] White(int n, int seed)
        {
            var rng = new Random(seed);
            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return y;
        }

        /// <summary>갈색 잡음(백색 잡음 적분): 낮게 깔리는 우르릉 소리의 재료.</summary>
        public static float[] Brown(int n, int seed)
        {
            float[] w = White(n, seed);
            var y = new float[n];
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                acc = acc * 0.997f + w[i] * 0.03f;
                y[i] = acc;
            }
            return Normalize(y, 1f);
        }

        /// <summary>1차 저역 통과(a가 작을수록 더 먹먹해진다, 0~1).</summary>
        public static float[] LowPass(float[] x, float a)
        {
            var y = new float[x.Length];
            float s = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                s += a * (x[i] - s);
                y[i] = s;
            }
            return y;
        }

        public static float[] HighPass(float[] x, float a)
        {
            float[] low = LowPass(x, a);
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] - low[i];
            return y;
        }

        /// <summary>시간(초) -> 값 함수로 사인파를 만든다. 주파수가 변해도 위상이 이어진다.</summary>
        public static float[] Tone(int n, Func<float, float> freqAt, Func<float, float> shape = null)
        {
            var y = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += 2.0 * Math.PI * freqAt(t) / Rate;
                float v = (float)Math.Sin(phase);
                y[i] = shape == null ? v : shape(v);
            }
            return y;
        }

        public static float[] Envelope(float[] x, Func<float, float> gainAt)
        {
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] * gainAt(i / (float)Rate);
            return y;
        }

        public static float Decay(float t, float tau) => (float)Math.Exp(-t / tau);

        public static float[] Mix(params (float[] signal, float gain)[] parts)
        {
            int n = 0;
            foreach (var p in parts) n = Math.Max(n, p.signal.Length);
            var y = new float[n];
            foreach (var p in parts)
            {
                for (int i = 0; i < p.signal.Length; i++) y[i] += p.signal[i] * p.gain;
            }
            return y;
        }

        public static float[] Normalize(float[] x, float peak)
        {
            float max = 1e-6f;
            foreach (float v in x) max = Math.Max(max, Math.Abs(v));
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] / max * peak;
            return y;
        }

        /// <summary>앞뒤 5ms 페이드(잘린 끝에서 '딱' 소리가 나지 않게).</summary>
        public static float[] EdgeFade(float[] x, float seconds = 0.005f)
        {
            int f = Math.Min(Samples(seconds), x.Length / 2);
            var y = (float[])x.Clone();
            for (int i = 0; i < f; i++)
            {
                float g = i / (float)f;
                y[i] *= g;
                y[x.Length - 1 - i] *= g;
            }
            return y;
        }

        /// <summary>
        /// 끝부분을 앞부분에 겹쳐 섞어 이음매 없이 반복되는 루프를 만든다.
        /// 잡음끼리 섞으므로 같은 힘(equal-power) 곡선을 써서 겹치는 구간이 작아지지 않게 한다.
        /// </summary>
        public static float[] SeamlessLoop(float[] x, float overlapSeconds)
        {
            int o = Math.Min(Samples(overlapSeconds), x.Length / 3);
            int n = x.Length - o;
            var y = new float[n];
            Array.Copy(x, y, n);
            for (int i = 0; i < o; i++)
            {
                float g = i / (float)o;
                y[i] = x[i] * (float)Math.Sqrt(g) + x[n + i] * (float)Math.Sqrt(1f - g);
            }
            return y;
        }

        /// <summary>무작위 시각에 짧은 소리 조각을 흩뿌린다(빗방울, 불꽃 튀는 소리, 유리 조각).</summary>
        public static float[] Scatter(int n, int seed, int count, float[] grain, float minGain, float maxGain)
        {
            var rng = new Random(seed);
            var y = new float[n];
            for (int k = 0; k < count; k++)
            {
                int start = rng.Next(0, Math.Max(1, n - grain.Length));
                float g = minGain + (float)rng.NextDouble() * (maxGain - minGain);
                for (int i = 0; i < grain.Length && start + i < n; i++) y[start + i] += grain[i] * g;
            }
            return y;
        }

        public static float SoftClip(float v) => (float)Math.Tanh(v * 1.5);
    }
}
