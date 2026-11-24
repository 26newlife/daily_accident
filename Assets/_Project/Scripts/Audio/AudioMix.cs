using System;
using UnityEngine;

namespace QuakeSurvival.Audio
{
    /// <summary>소리 갈래. 설정 메뉴의 볼륨 슬라이더가 갈래별로 조절한다.</summary>
    public enum AudioCategory
    {
        Ambient,  // 비, 땅울림, 사이렌, 바다
        Sfx,      // 조작, 폭발, 발소리
        Ui        // 재난문자 수신음, 결과 화면
    }

    /// <summary>
    /// 오디오 믹서 대신 쓰는 단순 음량 층(헤드리스 환경에서 믹서 에셋을 만들 수 없어서).
    /// 모든 소리는 (자기 음량 x 갈래 음량 x 전체 음량)으로 재생된다.
    /// </summary>
    public static class AudioMix
    {
        private static float master = 1f;
        private static float ambient = 1f;
        private static float sfx = 1f;
        private static float ui = 1f;

        public static event Action Changed;

        public static float Master
        {
            get => master;
            set { master = Mathf.Clamp01(value); Changed?.Invoke(); }
        }

        public static void SetCategory(AudioCategory category, float volume)
        {
            float v = Mathf.Clamp01(volume);
            switch (category)
            {
                case AudioCategory.Ambient: ambient = v; break;
                case AudioCategory.Sfx: sfx = v; break;
                default: ui = v; break;
            }
            Changed?.Invoke();
        }

        public static float Gain(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Ambient: return master * ambient;
                case AudioCategory.Sfx: return master * sfx;
                default: return master * ui;
            }
        }
    }
}
