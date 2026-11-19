using System.Collections;
using GameCore;
using GameData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 메인 메뉴 총괄: 버튼 이벤트, 모달 팝업(시나리오 / 설정) 열기, 선택 결과 반영, 씬 로드, 종료.
    /// 팝업의 열기/닫기 애니메이션과 ESC 처리는 PopupBase가 담당하고,
    /// 이 컨트롤러는 팝업이 떠 있는 동안 메인 버튼 입력만 잠근다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("씬")]
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private float sceneFadeOutDuration = 0.6f;

        [Header("메인 버튼")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button scenarioButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private CanvasGroup mainMenuGroup;     // 팝업이 떠 있는 동안 입력 잠금

        [Header("시나리오 / 설정")]
        [SerializeField] private StartButtonState startState;
        [SerializeField] private ScenarioSelectPopup scenarioPopup;
        [SerializeField] private SettingsPanel settingsPopup;
        [SerializeField] private BackgroundSwitcher backgroundSwitcher;

        [Header("연출 / 입력")]
        [SerializeField] private MenuIntroAnimation intro;
        [SerializeField] private InputSystemUIInputModule inputModule; // Cancel(ESC / B버튼) 액션 사용

        private InputAction cancelAction;
        private GameObject lastSelected;
        private bool isTransitioning;
        private bool hasPendingScenario;
        private ScenarioData pendingScenario;

        private void Awake()
        {
            GameRunContext.Cleanup("main-menu-enter");   // 게임에서 돌아온 경우 timeScale/오디오/static 상태 복구
            if (startButton != null) startButton.onClick.AddListener(OnStartGame);
            if (scenarioButton != null) scenarioButton.onClick.AddListener(OnScenarioSelect);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            SetInteractable(mainMenuGroup, true);
            GameSession.ScenarioChanged += OnScenarioChanged;
            PopupBase.VisibilityChanged += OnPopupVisibilityChanged;
        }

        private void Start()
        {
            // 입력 모듈이 OnEnable에서 기본 액션을 할당할 수 있으므로 Start에서 구독
            if (inputModule != null && inputModule.cancel != null) cancelAction = inputModule.cancel.action;
            if (cancelAction != null) cancelAction.performed += OnCancelPerformed;

            // 이미 선택된 시나리오가 있으면(예: 게임에서 메뉴로 복귀) 연출 없이 복원
            ScenarioData current = GameSession.SelectedScenario;
            if (startState != null) startState.ApplyScenario(current, animate: false);
            if (current != null && backgroundSwitcher != null) backgroundSwitcher.SwitchTo(current.BackgroundSprite, instant: true);
            RebuildNavigation();

            // 시작 시 기본 포커스는 SCENARIO SELECT (사운드는 생략)
            Select(scenarioButton != null ? scenarioButton.gameObject : null);
        }

        private void OnDestroy()
        {
            if (cancelAction != null) cancelAction.performed -= OnCancelPerformed;
            GameSession.ScenarioChanged -= OnScenarioChanged;
            PopupBase.VisibilityChanged -= OnPopupVisibilityChanged;
        }

        private void Update()
        {
            EventSystem es = EventSystem.current;
            if (es == null || isTransitioning) return;

            // 팝업이 떠 있으면 팝업이 자기 포커스를 관리한다
            PopupBase top = PopupBase.Top;
            if (top != null) { top.RecoverFocus(); return; }
            if (PopupBase.AnyVisible) return; // 닫히는 중

            // 빈 곳 클릭 / 비활성 버튼 클릭 등으로 선택이 사라지면 키보드·패드 조작이 끊기므로 마지막 선택을 복구
            GameObject sel = es.currentSelectedGameObject;
            bool valid = sel != null && sel.activeInHierarchy
                         && (!sel.TryGetComponent(out Selectable s) || s.interactable);
            if (valid) lastSelected = sel;
            else if (lastSelected != null && lastSelected.activeInHierarchy) Select(lastSelected);
        }

        // ───────── 버튼 동작 ─────────
        private void OnScenarioSelect()
        {
            if (isTransitioning || scenarioPopup == null) return;
            scenarioPopup.Open();
        }

        private void OnSettings()
        {
            if (isTransitioning || settingsPopup == null) return;
            settingsPopup.Open();
        }

        private void OnStartGame()
        {
            if (isTransitioning || !GameSession.HasSelection) return;
            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"[MainMenu] 씬 '{gameSceneName}'을(를) 로드할 수 없습니다. Build Settings에 등록되어 있는지 확인하세요.");
                return;
            }
            StartCoroutine(LoadSceneRoutine());
        }

        private IEnumerator LoadSceneRoutine()
        {
            isTransitioning = true;
            SetInteractable(mainMenuGroup, false);
            if (intro != null) yield return intro.FadeScreen(1f, sceneFadeOutDuration);
            SceneManager.LoadSceneAsync(gameSceneName);
        }

        private void OnQuit()
        {
            if (isTransitioning) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────── 시나리오 선택 결과 ─────────
        private void OnScenarioChanged(ScenarioData scenario)
        {
            // 팝업이 닫힌 뒤에 START GAME 전환 / 배경 교체가 보이도록 대기
            if (PopupBase.AnyVisible)
            {
                pendingScenario = scenario;
                hasPendingScenario = true;
                return;
            }
            ApplyScenario(scenario);
        }

        private void ApplyScenario(ScenarioData scenario)
        {
            if (startState != null) startState.ApplyScenario(scenario, animate: true);
            if (scenario != null && backgroundSwitcher != null) backgroundSwitcher.SwitchTo(scenario.BackgroundSprite); // null이면 기존 배경 유지
            RebuildNavigation();
        }

        private void OnPopupVisibilityChanged()
        {
            bool anyVisible = PopupBase.AnyVisible;
            SetInteractable(mainMenuGroup, !anyVisible && !isTransitioning);

            if (!anyVisible && hasPendingScenario)
            {
                hasPendingScenario = false;
                ApplyScenario(pendingScenario);
                pendingScenario = null;
            }
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx) => PopupBase.HandleCancel();

        // ───────── 네비게이션 ─────────
        /// <summary>메인 버튼 4개를 세로로 연결 (비활성 START GAME은 건너뛴다)</summary>
        private void RebuildNavigation()
        {
            var list = new System.Collections.Generic.List<Selectable>();
            foreach (Button b in new[] { startButton, scenarioButton, settingsButton, quitButton })
                if (b != null && b.interactable) list.Add(b);

            for (int i = 0; i < list.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (list.Count > 1)
                {
                    nav.selectOnUp = list[(i - 1 + list.Count) % list.Count];
                    nav.selectOnDown = list[(i + 1) % list.Count];
                }
                list[i].navigation = nav;
            }

            // 비활성 START GAME은 어디에서도 포커스를 받지 않도록 네비게이션 제거
            if (startButton != null && !startButton.interactable)
                startButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // ───────── 유틸 ─────────
        private static void SetInteractable(CanvasGroup g, bool on)
        {
            if (g == null) return;
            g.interactable = on;
            g.blocksRaycasts = on;
        }

        private void Select(GameObject go)
        {
            var es = EventSystem.current;
            if (es == null || go == null) return;
            if (go.TryGetComponent(out MenuButtonFX fx)) fx.SuppressNextSelectSound();
            es.SetSelectedGameObject(go);
            lastSelected = go;
        }
    }
}
