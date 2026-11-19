using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameCore
{
    /// <summary>화면 모드 (설정 드롭다운 순서와 동일)</summary>
    /// <summary>힌트 난이도. Easy: 모든 힌트 + 목표 화살표 / Normal: 조작·시스템 힌트만 / Hard: 힌트 없음(조작 카드만)</summary>
    public enum HintLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public enum DisplayMode
    {
        Fullscreen = 0,   // 전체화면 (독점)
        Windowed = 1,     // 창 모드
        Borderless = 2,   // 테두리 없는 창
    }

    /// <summary>
    /// 게임 설정 저장/조회용 정적 클래스. 모든 값은 PlayerPrefs에 저장된다.
    /// 다른 스크립트에서는 GameSettings.MouseSensitivity 처럼 바로 읽으면 되고,
    /// 값이 바뀌면 OnChanged 이벤트가 발생한다.
    ///
    /// 값 변경 즉시 반영: 조작 / 소리 / 밝기.
    /// "적용" 시 반영: 해상도 / 화면 모드 / 그래픽 품질 / VSync / 프레임 제한 (ApplyGraphics).
    /// 디스크 저장은 Save() 호출 시.
    /// </summary>
    public static class GameSettings
    {
        // ───────── 상수 ─────────
        /// <summary>Resources 폴더의 AudioMixer 이름 (없으면 AudioListener.volume으로 마스터만 처리)</summary>
        public const string MixerResourceName = "GameAudioMixer";
        public const string MasterParam = "MasterVolume";
        public const string MusicParam = "MusicVolume";
        public const string SfxParam = "SfxVolume";
        public const string VoiceParam = "VoiceVolume";

        public const float SensitivityMin = 0.1f;
        public const float SensitivityMax = 10f;
        public const float SensitivityDefault = 3f;
        public const float BrightnessMin = 0.5f;
        public const float BrightnessMax = 1.5f;
        public const float BrightnessDefault = 1f;

        /// <summary>프레임 제한 선택지. 0 = 무제한</summary>
        public static readonly int[] FrameLimitOptions = { 30, 60, 120, 0 };

        private const float VolumeDefault = 0.8f;
        private const float VoiceDefault = 1f;
        private const int FrameLimitDefault = 60;

        // ───────── PlayerPrefs 키 ─────────
        private const string KeyMouse = "settings.mouseSensitivity";
        private const string KeyGamepad = "settings.gamepadSensitivity";
        private const string KeyInvertY = "settings.invertY";
        private const string KeyMaster = "settings.masterVolume";
        private const string KeyMusic = "settings.musicVolume";
        private const string KeySfx = "settings.sfxVolume";
        private const string KeyVoice = "settings.voiceVolume";
        private const string KeyResW = "settings.resolutionWidth";
        private const string KeyResH = "settings.resolutionHeight";
        private const string KeyDisplayMode = "settings.displayMode";
        private const string KeyQuality = "settings.qualityLevel";
        private const string KeyVSync = "settings.vsync";
        private const string KeyFrameLimit = "settings.frameLimit";
        private const string KeyBrightness = "settings.brightness";
        private const string KeyHintLevel = "settings.hintLevel";
        private const string KeyHighlight = "settings.highlight";

        // ───────── 현재 값 ─────────
        private static bool loaded;
        private static float mouseSensitivity, gamepadSensitivity;
        private static bool invertY;
        private static float masterVolume, musicVolume, sfxVolume, voiceVolume;
        private static int resolutionWidth, resolutionHeight;
        private static DisplayMode displayMode;
        private static int qualityLevel;
        private static bool vSync;
        private static int frameLimit;
        private static float brightness;
        private static int hintLevel;
        private static bool highlightEnabled;

        private static AudioMixer mixer;
        private static bool mixerSearched;

        /// <summary>어떤 설정이든 값이 바뀌면 발생</summary>
        public static event Action OnChanged;

        // ───────── 프로퍼티 ─────────
        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return mouseSensitivity; }
            set { EnsureLoaded(); Set(ref mouseSensitivity, Mathf.Clamp(value, SensitivityMin, SensitivityMax)); }
        }

        public static float GamepadSensitivity
        {
            get { EnsureLoaded(); return gamepadSensitivity; }
            set { EnsureLoaded(); Set(ref gamepadSensitivity, Mathf.Clamp(value, SensitivityMin, SensitivityMax)); }
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return invertY; }
            set { EnsureLoaded(); Set(ref invertY, value); }
        }

        public static float MasterVolume
        {
            get { EnsureLoaded(); return masterVolume; }
            set { EnsureLoaded(); SetAudio(ref masterVolume, Mathf.Clamp01(value)); }
        }

        public static float MusicVolume
        {
            get { EnsureLoaded(); return musicVolume; }
            set { EnsureLoaded(); SetAudio(ref musicVolume, Mathf.Clamp01(value)); }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return sfxVolume; }
            set { EnsureLoaded(); SetAudio(ref sfxVolume, Mathf.Clamp01(value)); }
        }

        public static float VoiceVolume
        {
            get { EnsureLoaded(); return voiceVolume; }
            set { EnsureLoaded(); SetAudio(ref voiceVolume, Mathf.Clamp01(value)); }
        }

        public static int ResolutionWidth { get { EnsureLoaded(); return resolutionWidth; } }
        public static int ResolutionHeight { get { EnsureLoaded(); return resolutionHeight; } }

        public static DisplayMode DisplayMode
        {
            get { EnsureLoaded(); return displayMode; }
            set
            {
                EnsureLoaded();
                if (displayMode == value) return;
                displayMode = value;
                OnChanged?.Invoke();
            }
        }

        public static int QualityLevel
        {
            get { EnsureLoaded(); return qualityLevel; }
            set { EnsureLoaded(); Set(ref qualityLevel, Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); }
        }

        public static bool VSync
        {
            get { EnsureLoaded(); return vSync; }
            set { EnsureLoaded(); Set(ref vSync, value); }
        }

        /// <summary>프레임 제한 (0 = 무제한)</summary>
        public static int FrameLimit
        {
            get { EnsureLoaded(); return frameLimit; }
            set { EnsureLoaded(); Set(ref frameLimit, Mathf.Max(0, value)); }
        }

        /// <summary>밝기 (BrightnessMin ~ BrightnessMax, 1 = 기본). BrightnessOverlay가 이 값을 화면에 반영한다.</summary>
        public static float Brightness
        {
            get { EnsureLoaded(); return brightness; }
            set { EnsureLoaded(); Set(ref brightness, Mathf.Clamp(value, BrightnessMin, BrightnessMax)); }
        }

        /// <summary>힌트 난이도 (기본 Normal)</summary>
        public static HintLevel HintLevel
        {
            get { EnsureLoaded(); return (HintLevel)hintLevel; }
            set { EnsureLoaded(); Set(ref hintLevel, Mathf.Clamp((int)value, 0, 2)); }
        }

        /// <summary>가까운 상호작용 대상의 은은한 하이라이트 사용 여부</summary>
        public static bool HighlightEnabled
        {
            get { EnsureLoaded(); return highlightEnabled; }
            set { EnsureLoaded(); Set(ref highlightEnabled, value); }
        }

        public static void SetResolution(int width, int height)
        {
            EnsureLoaded();
            if (resolutionWidth == width && resolutionHeight == height) return;
            resolutionWidth = width;
            resolutionHeight = height;
            OnChanged?.Invoke();
        }

        // ───────── 조회 도우미 ─────────
        /// <summary>Screen.resolutions 기반 해상도 목록 (가로x세로 중복 제거, 오름차순). 현재 설정값이 없으면 추가한다.</summary>
        public static List<Vector2Int> GetAvailableResolutions()
        {
            EnsureLoaded();
            var list = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (seen.Add(v)) list.Add(v);
            }
            var current = new Vector2Int(resolutionWidth, resolutionHeight);
            if (seen.Add(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        // ───────── 저장 / 불러오기 / 적용 ─────────
        /// <summary>PlayerPrefs에 저장</summary>
        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetFloat(KeyMouse, mouseSensitivity);
            PlayerPrefs.SetFloat(KeyGamepad, gamepadSensitivity);
            PlayerPrefs.SetInt(KeyInvertY, invertY ? 1 : 0);
            PlayerPrefs.SetFloat(KeyMaster, masterVolume);
            PlayerPrefs.SetFloat(KeyMusic, musicVolume);
            PlayerPrefs.SetFloat(KeySfx, sfxVolume);
            PlayerPrefs.SetFloat(KeyVoice, voiceVolume);
            PlayerPrefs.SetInt(KeyResW, resolutionWidth);
            PlayerPrefs.SetInt(KeyResH, resolutionHeight);
            PlayerPrefs.SetInt(KeyDisplayMode, (int)displayMode);
            PlayerPrefs.SetInt(KeyQuality, qualityLevel);
            PlayerPrefs.SetInt(KeyVSync, vSync ? 1 : 0);
            PlayerPrefs.SetInt(KeyFrameLimit, frameLimit);
            PlayerPrefs.SetFloat(KeyBrightness, brightness);
            PlayerPrefs.SetInt(KeyHintLevel, hintLevel);
            PlayerPrefs.SetInt(KeyHighlight, highlightEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>저장된 값으로 되돌린다 (저장하지 않은 변경 취소). 소리는 즉시 다시 반영한다.</summary>
        public static void Load()
        {
            ReadPrefs();
            ApplyAudio();
            OnChanged?.Invoke();
        }

        /// <summary>기본값으로 되돌린다. 저장은 Save()를 호출해야 반영된다.</summary>
        public static void ResetToDefaults()
        {
            ApplyDefaults();
            ApplyAudio();
            OnChanged?.Invoke();
        }

        /// <summary>소리 설정 반영 (AudioMixer가 있으면 Exposed Parameter, 없으면 AudioListener.volume)</summary>
        public static void ApplyAudio()
        {
            EnsureLoaded();
            AudioMixer m = Mixer;
            if (m != null)
            {
                AudioListener.volume = 1f;
                m.SetFloat(MasterParam, ToDecibel(masterVolume));
                m.SetFloat(MusicParam, ToDecibel(musicVolume));
                m.SetFloat(SfxParam, ToDecibel(sfxVolume));
                m.SetFloat(VoiceParam, ToDecibel(voiceVolume));
            }
            else
            {
                AudioListener.volume = masterVolume;
            }
        }

        /// <summary>해상도 / 화면 모드 / 품질 / VSync / 프레임 제한 반영</summary>
        public static void ApplyGraphics()
        {
            EnsureLoaded();

            // SetQualityLevel이 VSync를 품질 프리셋 값으로 덮어쓰므로 품질을 먼저 적용한다
            int q = Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1);
            if (q != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(q, true);

            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = frameLimit > 0 ? frameLimit : -1;

#if !UNITY_EDITOR
            // 에디터 Game 뷰에서는 해상도/전체화면이 적용되지 않으므로 빌드에서만
            Screen.SetResolution(resolutionWidth, resolutionHeight, ToFullScreenMode(displayMode));
#endif
        }

        public static void ApplyAll()
        {
            ApplyAudio();
            ApplyGraphics();
        }

        // ───────── 내부 ─────────
        private static AudioMixer Mixer
        {
            get
            {
                if (!mixerSearched)
                {
                    mixerSearched = true;
                    mixer = Resources.Load<AudioMixer>(MixerResourceName);
                }
                return mixer;
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            ReadPrefs();
        }

        private static void ReadPrefs()
        {
            ApplyDefaults();

            mouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyMouse, mouseSensitivity), SensitivityMin, SensitivityMax);
            gamepadSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyGamepad, gamepadSensitivity), SensitivityMin, SensitivityMax);
            invertY = PlayerPrefs.GetInt(KeyInvertY, invertY ? 1 : 0) == 1;
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, masterVolume));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, musicVolume));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, sfxVolume));
            voiceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVoice, voiceVolume));
            resolutionWidth = PlayerPrefs.GetInt(KeyResW, resolutionWidth);
            resolutionHeight = PlayerPrefs.GetInt(KeyResH, resolutionHeight);
            displayMode = (DisplayMode)Mathf.Clamp(PlayerPrefs.GetInt(KeyDisplayMode, (int)displayMode), 0, 2);
            qualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, qualityLevel), 0, QualitySettings.names.Length - 1);
            vSync = PlayerPrefs.GetInt(KeyVSync, vSync ? 1 : 0) == 1;
            frameLimit = Mathf.Max(0, PlayerPrefs.GetInt(KeyFrameLimit, frameLimit));
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(KeyBrightness, brightness), BrightnessMin, BrightnessMax);
            hintLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyHintLevel, hintLevel), 0, 2);
            highlightEnabled = PlayerPrefs.GetInt(KeyHighlight, highlightEnabled ? 1 : 0) == 1;

            loaded = true;
        }

        private static void ApplyDefaults()
        {
            mouseSensitivity = SensitivityDefault;
            gamepadSensitivity = SensitivityDefault;
            invertY = false;
            masterVolume = VolumeDefault;
            musicVolume = VolumeDefault;
            sfxVolume = VolumeDefault;
            voiceVolume = VoiceDefault;
            Resolution cur = Screen.currentResolution;
            resolutionWidth = cur.width;
            resolutionHeight = cur.height;
            displayMode = FromFullScreenMode(Screen.fullScreenMode);
            qualityLevel = QualitySettings.GetQualityLevel();
            vSync = true;
            frameLimit = FrameLimitDefault;
            brightness = BrightnessDefault;
            hintLevel = (int)HintLevel.Normal;
            highlightEnabled = true;
            loaded = true;
        }

        private static void Set<T>(ref T field, T value) where T : IEquatable<T>
        {
            if (field.Equals(value)) return;
            field = value;
            OnChanged?.Invoke();
        }

        private static void SetAudio(ref float field, float value)
        {
            if (Mathf.Approximately(field, value)) return;
            field = value;
            ApplyAudio();
            OnChanged?.Invoke();
        }

        private static float ToDecibel(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;

        private static FullScreenMode ToFullScreenMode(DisplayMode mode)
        {
            switch (mode)
            {
                case DisplayMode.Windowed: return FullScreenMode.Windowed;
                case DisplayMode.Borderless: return FullScreenMode.FullScreenWindow;
                default: return FullScreenMode.ExclusiveFullScreen;
            }
        }

        private static DisplayMode FromFullScreenMode(FullScreenMode mode)
        {
            switch (mode)
            {
                case FullScreenMode.Windowed: return DisplayMode.Windowed;
                case FullScreenMode.ExclusiveFullScreen: return DisplayMode.Fullscreen;
                default: return DisplayMode.Borderless;
            }
        }

        // 시작 시 저장된 설정을 자동 적용
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureLoaded();
            ApplyAll();
        }

        // 에디터에서 Domain Reload를 끄고 플레이해도 값이 꼬이지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            mixer = null;
            mixerSearched = false;
            OnChanged = null;
        }
    }
}
