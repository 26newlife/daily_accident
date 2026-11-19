using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 사망 화면: "왜 죽었는지 + 올바른 행동" 교육 피드백, 다시 시작 / 메인 메뉴 버튼.
    /// 문구는 DeathReasonData 에셋에서 가져온다.
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text whyText;
        [SerializeField] private TMP_Text correctActionText;
        [SerializeField, Tooltip("사망 엔딩 모음 (발견한 사망 엔딩 n / N). 비워두면 표시하지 않는다")] private TMP_Text collectionText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float fadeDuration = 0.8f;
        [SerializeField] private string missingDataTitle = "사망";

        private void Awake()
        {
            if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        public void Show(DeathReasonData data, string collection = null)
        {
            if (collectionText != null) collectionText.text = collection ?? string.Empty;
            if (titleText != null) titleText.text = data != null ? data.Title : missingDataTitle;
            if (whyText != null) whyText.text = data != null ? data.WhyYouDied : string.Empty;
            if (correctActionText != null) correctActionText.text = data != null ? data.CorrectAction : string.Empty;
            StartCoroutine(FadeIn());
        }

        private System.Collections.IEnumerator FadeIn()
        {
            group.blocksRaycasts = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration))
            {
                group.alpha = t;
                yield return null;
            }
            group.alpha = 1f;
            group.interactable = true;
            if (EventSystem.current != null && restartButton != null) EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        private void Restart()
        {
            GameRunContext.Cleanup("restart");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void GoToMainMenu()
        {
            GameRunContext.Cleanup("main-menu");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
