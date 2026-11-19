using GameCore;
using GameUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// ESC / 패드 Start 일시정지 메뉴: 계속하기 / 설정 / 메인 메뉴로.
    /// 설정은 메인 메뉴와 같은 SettingsPanel(PopupBase)을 재사용한다. 설정 팝업이 떠 있을 때 ESC 는 팝업만 닫는다.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private ControlsGuideCard controlsCard;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private SettingsPanel settingsPopup;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float fadeSpeed = 10f;

        private bool isOpen;
        private float shown;

        private void Awake()
        {
            if (panel != null) { panel.alpha = 0f; panel.interactable = false; panel.blocksRaycasts = false; }
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (controlsButton != null) controlsButton.onClick.AddListener(OpenControls);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        private void OnEnable()
        {
            if (input != null) { input.PausePressed += OnPausePressed; input.BackPressed += OnBackPressed; }
            PopupBase.VisibilityChanged += OnPopupVisibilityChanged;
        }

        private void OnDisable()
        {
            if (input != null) { input.PausePressed -= OnPausePressed; input.BackPressed -= OnBackPressed; }
            PopupBase.VisibilityChanged -= OnPopupVisibilityChanged;
        }

        private void Update()
        {
            shown = Mathf.MoveTowards(shown, isOpen ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            if (panel != null) panel.alpha = shown;

            // 팝업이 떠 있으면 팝업이 포커스를 관리 (게임패드/키보드 조작 유지)
            if (isOpen)
            {
                PopupBase top = PopupBase.Top;
                if (top != null) { top.RecoverFocus(); return; }
                // 조작법 패널이 떠 있는 동안은 패널의 X 버튼만 조작되게 메뉴 버튼을 잠근다
                bool cardOpen = controlsCard != null && controlsCard.IsVisible;
                if (panel != null) panel.interactable = !cardOpen;
                if (cardOpen) return;
                EventSystem es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject == null && resumeButton != null && !PopupBase.AnyVisible)
                    es.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        private void OnPausePressed()
        {
            if (GameFlow.IsEnded) return;
            // 조작법 패널이 열려 있으면 ESC/Start 는 패널만 닫고 일시정지 메뉴로 돌아간다 (재개하지 않는다)
            if (controlsCard != null && controlsCard.IsVisible) { CloseControls(); return; }
            if (GameFlow.CameraOverride || Time.unscaledTime - GameFlow.CameraOverrideLastExit < 0.2f) return;

            if (PopupBase.HandleCancel()) return; // 설정 팝업 등이 열려 있으면 그것만 닫는다
            if (isOpen) Resume(); else Open();
        }

        private void Open()
        {
            isOpen = true;
            GameFlow.SetPaused(true);
            if (panel != null) { panel.interactable = true; panel.blocksRaycasts = true; }
            if (EventSystem.current != null && resumeButton != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (PopupBase.AnyVisible) return;
            isOpen = false;
            GameFlow.SetPaused(false);
            if (panel != null) { panel.interactable = false; panel.blocksRaycasts = false; }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OpenControls()
        {
            if (controlsCard != null) controlsCard.Show();
        }

        private void OnBackPressed()
        {
            if (isOpen && controlsCard != null && controlsCard.IsVisible) CloseControls();   // 패드 B / 마우스 우클릭
        }

        private void CloseControls()
        {
            controlsCard.Hide();
            if (panel != null) panel.interactable = !PopupBase.AnyVisible;
            if (EventSystem.current != null && controlsButton != null) EventSystem.current.SetSelectedGameObject(controlsButton.gameObject);
        }

        private void OpenSettings()
        {
            if (settingsPopup != null) settingsPopup.Open();
        }

        private void OnPopupVisibilityChanged()
        {
            // 팝업이 떠 있는 동안은 일시정지 메뉴 버튼 입력을 잠근다
            if (panel != null && isOpen) panel.interactable = !PopupBase.AnyVisible;
        }

        private void GoToMainMenu()
        {
            GameRunContext.Cleanup("main-menu");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
