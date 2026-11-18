using System.Collections;
using GameCore;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>
    /// GameScene 진입 관문. 메인 메뉴에서 선택한 시나리오(GameSession)가 이 씬이 담당하는 시나리오가 아니면
    /// 게임 오브젝트를 모두 끄고 "준비 중" 안내 후 메인 메뉴로 돌려보낸다.
    /// 에디터에서 GameScene 을 바로 재생한 경우(선택 없음)에는 allowDirectPlayInEditor 로 통과시킬 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSceneEntry : MonoBehaviour
    {
        [Header("이 씬이 담당하는 시나리오")]
        [SerializeField] private ScenarioData requiredScenario;
        [SerializeField] private bool allowDirectPlayInEditor = true;

        [Header("진입 실패 처리")]
        [SerializeField, Tooltip("진입이 거부될 때 꺼둘 루트 오브젝트 (레벨, 플레이어, 시스템, HUD)")] private GameObject[] gameplayRoots;
        [SerializeField] private GameObject noticeRoot;
        [SerializeField] private TMP_Text noticeText;
        [SerializeField, TextArea(1, 3), Tooltip("{0} = 선택한 시나리오 이름")] private string notReadyMessage = "'{0}' 시나리오는 준비 중입니다.\n메인 메뉴로 돌아갑니다.";
        [SerializeField] private string noSelectionName = "선택되지 않은";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField, Min(0.5f)] private float noticeSeconds = 3f;

        private void Awake()
        {
            GameRunContext.Begin("scene-enter");   // 새 게임 / 다시 시작 / 메뉴 왕복 후 재진입 모두 여기를 거친다

            ScenarioData selected = GameSession.SelectedScenario;
            bool ok = selected != null && requiredScenario != null
                      && (selected == requiredScenario || selected.Id == requiredScenario.Id);
            if (selected == null && Application.isEditor && allowDirectPlayInEditor) ok = true;

            if (noticeRoot != null) noticeRoot.SetActive(!ok);
            if (ok) return;

            if (gameplayRoots != null)
                foreach (GameObject g in gameplayRoots) if (g != null) g.SetActive(false);

            string scenarioName = selected != null ? selected.DisplayName : noSelectionName;
            if (noticeText != null) noticeText.text = string.Format(notReadyMessage, scenarioName);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            yield return new WaitForSecondsRealtime(noticeSeconds);
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
