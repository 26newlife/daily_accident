using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>
    /// 한 번 재생하는 소리(효과음). 갈래 음량과 약간의 음높이 변화를 적용한다.
    /// 소리마다 오브젝트를 새로 만들지 않고, 미리 만든 AudioSource 묶음(풀)을 돌려쓴다.
    /// </summary>
    public static class SoundPlayer
    {
        private const int PoolSize = 16;
        private const string RootName = "OneShotAudioPool";

        private static GameObject root;
        private static AudioSource[] pool;
        private static float[] startedAt;

        /// <summary>월드 위치에서 3D로 재생한다(가까울수록 크게).</summary>
        public static AudioSource PlayAt(AudioClip clip, Vector3 position, AudioCategory category,
            float volume = 1f, float pitchJitter = 0.06f, float maxDistance = 25f)
        {
            if (clip == null) return null;
            AudioSource source = Prepare(clip, category, volume, pitchJitter);
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.Play();
            return source;
        }

        /// <summary>귀 바로 옆에서 2D로 재생한다(UI, 결과 화면, 몸에서 나는 소리).</summary>
        public static AudioSource Play2D(AudioClip clip, AudioCategory category, float volume = 1f, float pitchJitter = 0f)
        {
            if (clip == null) return null;
            AudioSource source = Prepare(clip, category, volume, pitchJitter);
            source.spatialBlend = 0f;
            source.Play();
            return source;
        }

        private static AudioSource Prepare(AudioClip clip, AudioCategory category, float volume, float pitchJitter)
        {
            EnsurePool();
            int index = PickFreeIndex();
            startedAt[index] = Time.unscaledTime;

            AudioSource source = pool[index];
            source.Stop();
            source.clip = clip;
            source.volume = volume * AudioMix.Gain(category);
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            return source;
        }

        /// <summary>쉬고 있는 소스를 고른다. 모두 바쁘면 가장 오래 재생 중인 것을 끊고 쓴다.</summary>
        private static int PickFreeIndex()
        {
            int oldest = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].isPlaying) return i;
                if (startedAt[i] < startedAt[oldest]) oldest = i;
            }
            return oldest;
        }

        /// <summary>풀이 없거나 장면이 바뀌어 지워졌으면 새로 만든다.</summary>
        private static void EnsurePool()
        {
            if (root != null) return;
            root = new GameObject(RootName);
            pool = new AudioSource[PoolSize];
            startedAt = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Voice");
                child.transform.SetParent(root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool[i] = source;
            }
        }
    }
}
