using System;
using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 설정 팝업 (탭 3개: 조작 / 소리 / 화면). 실제 값은 모두 GameSettings(정적 클래스)가 보관한다.
    /// - 조작·소리·밝기: 값을 바꾸는 즉시 반영
    /// - 해상도·화면 모드·품질·VSync·프레임 제한: "적용" 버튼에서 반영
    /// - "적용": 반영 + PlayerPrefs 저장 / "닫기"(X, 바깥 클릭, ESC 포함): 저장하지 않은 변경 취소
    /// - "기본값으로 초기화": 기본값으로 되돌림 (저장은 "적용"을 눌러야 확정)
    /// </summary>
    public class SettingsPanel : PopupBase
    {
        [Header("탭 (같은 순서: 조작 / 소리 / 화면)")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] tabPages;
        [SerializeField] private GameObject[] tabIndicators;   // 선택된 탭 밑줄

        [Header("① 조작")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TMP_Text mouseSensitivityValue;
        [SerializeField] private Slider gamepadSensitivitySlider;
        [SerializeField] private TMP_Text gamepadSensitivityValue;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private TMP_Dropdown hintLevelDropdown;
        [SerializeField] private Toggle highlightToggle;
        [SerializeField] private Toggle objectiveNoticeToggle;

        [Header("② 소리")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private TMP_Text voiceValue;

        [Header("③ 화면")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown frameLimitDropdown;
        [SerializeField] private Toggle vSyncToggle;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private TMP_Text brightnessValue;

        [Header("하단 버튼")]
        [SerializeField] private Button resetButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button footerCloseButton;

        [Header("문구")]
        [SerializeField] private string[] displayModeLabels = { "전체화면", "창 모드", "테두리 없는 창" };
        [SerializeField] private string unlimitedFrameLabel = "무제한";
        [SerializeField] private string[] hintLevelLabels = { "쉬움 (힌트+화살표)", "보통 (기본)", "어려움 (힌트 없음)" };

        private readonly List<Action> refreshers = new List<Action>();
        private List<Vector2Int> resolutions;
        private bool isRefreshing;
        private int currentTab;

        protected override void Awake()
        {
            base.Awake();

            SetupControls();
            SetupDropdownOptions();
            BindControls();

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (applyButton != null) applyButton.onClick.AddListener(OnApplyClicked);
            if (footerCloseButton != null) footerCloseButton.onClick.AddListener(RequestClose);

            SelectTab(0);
            RefreshUI();
        }

        // ───────── PopupBase ─────────
        protected override void OnOpening()
        {
            RefreshUI();
            SelectTab(0);
        }

        protected override Selectable GetInitialSelection()
            => tabButtons != null && currentTab < tabButtons.Length ? tabButtons[currentTab] : closeButton;

        /// <summary>드롭다운 목록이 열려 있으면 닫기 요청(ESC 등)을 무시해 드롭다운만 닫히게 한다</summary>
        public override void RequestClose()
        {
            if (IsAnyDropdownExpanded()) return;
            GameSettings.Load(); // "적용"하지 않은 변경은 취소
            base.RequestClose();
        }

        private bool IsAnyDropdownExpanded()
            => (resolutionDropdown != null && resolutionDropdown.IsExpanded)
               || (displayModeDropdown != null && displayModeDropdown.IsExpanded)
               || (hintLevelDropdown != null && hintLevelDropdown.IsExpanded)
               || (qualityDropdown != null && qualityDropdown.IsExpanded)
               || (frameLimitDropdown != null && frameLimitDropdown.IsExpanded);

        // ───────── 탭 ─────────
        public void SelectTab(int index)
        {
            if (tabPages == null || tabPages.Length == 0) return;
            currentTab = Mathf.Clamp(index, 0, tabPages.Length - 1);
            for (int i = 0; i < tabPages.Length; i++)
            {
                if (tabPages[i] != null) tabPages[i].SetActive(i == currentTab);
                if (tabIndicators != null && i < tabIndicators.Length && tabIndicators[i] != null)
                    tabIndicators[i].SetActive(i == currentTab);
            }
        }

        // ───────── 초기화 ─────────
        private void SetupControls()
        {
            ConfigureSlider(mouseSensitivitySlider, GameSettings.SensitivityMin, GameSettings.SensitivityMax);
            ConfigureSlider(gamepadSensitivitySlider, GameSettings.SensitivityMin, GameSettings.SensitivityMax);
            ConfigureSlider(masterSlider, 0f, 1f);
            ConfigureSlider(musicSlider, 0f, 1f);
            ConfigureSlider(sfxSlider, 0f, 1f);
            ConfigureSlider(voiceSlider, 0f, 1f);
            ConfigureSlider(brightnessSlider, GameSettings.BrightnessMin, GameSettings.BrightnessMax);
        }

        private static void ConfigureSlider(Slider s, float min, float max)
        {
            if (s == null) return;
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = false;
        }

        private void SetupDropdownOptions()
        {
            if (resolutionDropdown != null)
            {
                resolutions = GameSettings.GetAvailableResolutions();
                var options = new List<string>();
                foreach (Vector2Int r in resolutions) options.Add($"{r.x} x {r.y}");
                FillDropdown(resolutionDropdown, options);
            }

            FillDropdown(displayModeDropdown, new List<string>(displayModeLabels));
            FillDropdown(hintLevelDropdown, new List<string>(hintLevelLabels));
            FillDropdown(qualityDropdown, new List<string>(QualitySettings.names));

            var frameOptions = new List<string>();
            foreach (int limit in GameSettings.FrameLimitOptions)
                frameOptions.Add(limit > 0 ? limit.ToString() : unlimitedFrameLabel);
            FillDropdown(frameLimitDropdown, frameOptions);
        }

        private static void FillDropdown(TMP_Dropdown dd, List<string> options)
        {
            if (dd == null) return;
            dd.ClearOptions();
            dd.AddOptions(options);
        }

        /// <summary>UI 컨트롤 ↔ GameSettings 연결. 각 컨트롤마다 변경 콜백과 UI 갱신 함수를 등록한다.</summary>
        private void BindControls()
        {
            BindSlider(mouseSensitivitySlider, mouseSensitivityValue,
                () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0"));
            BindSlider(gamepadSensitivitySlider, gamepadSensitivityValue,
                () => GameSettings.GamepadSensitivity, v => GameSettings.GamepadSensitivity = v, v => v.ToString("0.0"));
            BindToggle(invertYToggle, () => GameSettings.InvertY, v => GameSettings.InvertY = v);
            BindDropdown(hintLevelDropdown, () => (int)GameSettings.HintLevel, i => GameSettings.HintLevel = (HintLevel)i);
            BindToggle(highlightToggle, () => GameSettings.HighlightEnabled, v => GameSettings.HighlightEnabled = v);
            BindToggle(objectiveNoticeToggle, () => GameSettings.ObjectiveNotice, v => GameSettings.ObjectiveNotice = v);

            BindSlider(masterSlider, masterValue, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
            BindSlider(musicSlider, musicValue, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
            BindSlider(sfxSlider, sfxValue, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Percent);
            BindSlider(voiceSlider, voiceValue, () => GameSettings.VoiceVolume, v => GameSettings.VoiceVolume = v, Percent);

            BindDropdown(resolutionDropdown,
                () => FindResolutionIndex(),
                i => { if (resolutions != null && i >= 0 && i < resolutions.Count) GameSettings.SetResolution(resolutions[i].x, resolutions[i].y); });
            BindDropdown(displayModeDropdown, () => (int)GameSettings.DisplayMode, i => GameSettings.DisplayMode = (DisplayMode)i);
            BindDropdown(qualityDropdown, () => GameSettings.QualityLevel, i => GameSettings.QualityLevel = i);
            BindDropdown(frameLimitDropdown,
                () => Mathf.Max(0, Array.IndexOf(GameSettings.FrameLimitOptions, GameSettings.FrameLimit)),
                i => GameSettings.FrameLimit = GameSettings.FrameLimitOptions[Mathf.Clamp(i, 0, GameSettings.FrameLimitOptions.Length - 1)]);
            BindToggle(vSyncToggle, () => GameSettings.VSync, v => GameSettings.VSync = v);
            BindSlider(brightnessSlider, brightnessValue, () => GameSettings.Brightness, v => GameSettings.Brightness = v, Percent);
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        private void BindSlider(Slider slider, TMP_Text valueText, Func<float> get, Action<float> set, Func<float, string> format)
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(v =>
            {
                if (isRefreshing) return;
                set(v);
                if (valueText != null) valueText.text = format(v);
            });
            refreshers.Add(() =>
            {
                float v = get();
                slider.SetValueWithoutNotify(v);
                if (valueText != null) valueText.text = format(v);
            });
        }

        private void BindToggle(Toggle toggle, Func<bool> get, Action<bool> set)
        {
            if (toggle == null) return;
            toggle.onValueChanged.AddListener(v => { if (!isRefreshing) set(v); });
            refreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
        }

        private void BindDropdown(TMP_Dropdown dropdown, Func<int> get, Action<int> set)
        {
            if (dropdown == null) return;
            dropdown.onValueChanged.AddListener(i => { if (!isRefreshing) set(i); });
            refreshers.Add(() =>
            {
                dropdown.SetValueWithoutNotify(get());
                dropdown.RefreshShownValue();
            });
        }

        /// <summary>현재 GameSettings 값을 모든 컨트롤에 표시</summary>
        private void RefreshUI()
        {
            isRefreshing = true;
            foreach (Action refresh in refreshers) refresh();
            isRefreshing = false;
        }

        private int FindResolutionIndex()
        {
            if (resolutions == null || resolutions.Count == 0) return 0;
            int best = 0;
            long bestDist = long.MaxValue;
            for (int i = 0; i < resolutions.Count; i++)
            {
                long dx = resolutions[i].x - GameSettings.ResolutionWidth;
                long dy = resolutions[i].y - GameSettings.ResolutionHeight;
                long dist = dx * dx + dy * dy;
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        // ───────── 하단 버튼 ─────────
        private void OnApplyClicked()
        {
            GameSettings.ApplyGraphics();
            GameSettings.Save();
        }

        private void OnResetClicked()
        {
            GameSettings.ResetToDefaults();
            RefreshUI();
        }
    }
}
