using System.Text;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>엔딩 화면: 엔딩 이름/설명, 구조 점수 내역, 소요 시간, 다시 시작 / 메인 메뉴 버튼.</summary>
    public class EndingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text endingNameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private float fadeDuration = 0.8f;

        [Header("문구 형식")]
        [SerializeField, Tooltip("{0} = 항목, {1} = 획득, {2} = 최대")] private string scoreLineFormat = "{0}   {1} / {2}";
        [SerializeField, Tooltip("{0} = 합계, {1} = 만점, {2} = 등급")] private string totalFormat = "구조 점수   {0} / {1}   {2}";
        [SerializeField, Tooltip("{0} = 시간")] private string timeFormat = "소요 시간   {0}";
        [SerializeField] private Color survivalColor = new Color(0.4f, 0.9f, 0.5f);
        [SerializeField] private Color failureColor = new Color(1f, 0.45f, 0.45f);

        private void Awake()
        {
            if (group != null) { group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false; }
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        public void Show(EndingData ending, ScoreSystem score, float realSeconds)
        {
            if (endingNameText != null)
            {
                endingNameText.text = ending.EndingName;
                endingNameText.color = ending.IsSurvival ? survivalColor : failureColor;
            }
            if (descriptionText != null) descriptionText.text = ending.Description;
            if (scoreText != null) scoreText.text = BuildScoreText(score);
            if (timeText != null) timeText.text = string.Format(timeFormat, GameClock.Format(realSeconds));
            StartCoroutine(FadeIn());
        }

        private string BuildScoreText(ScoreSystem score)
        {
            if (score == null) return string.Empty;
            var sb = new StringBuilder();
            if (score.Categories != null)
                foreach (ScoreCategoryData c in score.Categories)
                    if (c != null) sb.AppendLine(string.Format(scoreLineFormat, c.Label, score.GetEarned(c.Id), c.MaxPoints));
            sb.AppendLine();
            sb.Append(string.Format(totalFormat, score.Total, score.MaxTotal, score.GetGradeLabel()));
            return sb.ToString();
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
            GameFlow.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void GoToMainMenu()
        {
            GameFlow.Reset();
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
