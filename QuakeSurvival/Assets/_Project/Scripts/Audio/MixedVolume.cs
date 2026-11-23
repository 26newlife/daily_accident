using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>
    /// AudioSource 하나의 음량을 (기본 음량 x 갈래 음량)으로 유지한다.
    /// 설정 메뉴에서 볼륨을 바꾸면(AudioMix.Changed) 재생 중에도 바로 반영된다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MixedVolume : MonoBehaviour
    {
        private AudioSource source;
        private AudioCategory category;
        private float baseVolume = 1f;

        /// <summary>AudioSource에 붙여 갈래와 기본 음량을 정한다.</summary>
        public static MixedVolume Attach(AudioSource source, AudioCategory category, float baseVolume)
        {
            var mixed = source.gameObject.AddComponent<MixedVolume>();
            mixed.source = source;
            mixed.category = category;
            mixed.BaseVolume = baseVolume;
            return mixed;
        }

        public float BaseVolume
        {
            get => baseVolume;
            set
            {
                baseVolume = Mathf.Clamp01(value);
                Apply();
            }
        }

        private void OnEnable()
        {
            AudioMix.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            AudioMix.Changed -= Apply;
        }

        private void Apply()
        {
            if (source != null) source.volume = baseVolume * AudioMix.Gain(category);
        }
    }
}
