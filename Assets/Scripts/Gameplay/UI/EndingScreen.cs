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
        [SerializeField, Tooltip("잘한 점 / 아쉬운 점 요약 (교육 피드백). 비워두면 표시하지 않는다")] private TMP_Text feedbackText;
        [SerializeField, Tooltip("엔딩 모음 (사망 엔딩 / 생존 엔딩 발견 수). 비워두면 표시하지 않는다")] private TMP_Text collectionText;
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

        public void Show(EndingData ending, ScoreSystem score, float realSeconds, string collection = null)
        {
            if (feedbackText != null) feedbackText.text = BuildFeedbackText(score);
            if (collectionText != null) collectionText.text = collection ?? string.Empty;
            if (endingNameText != null)
            {
                endingNameText.text = string.IsNullOrEmpty(ending.RouteLabel) ? ending.EndingName : ending.EndingName + "  <size=60%>" + ending.RouteLabel + "</size>";
                endingNameText.color = ending.IsSurvival ? survivalColor : failureColor;
            }
            if (descriptionText != null) descriptionText.text = ending.Description;
            if (scoreText != null) scoreText.text = BuildScoreText(score);
            if (timeText != null) timeText.text = string.Format(timeFormat, GameClock.Format(realSeconds));
            StartCoroutine(FadeIn());
        }

        private string BuildFeedbackText(ScoreSystem score)
        {
            if (score == null || !score.HasRules) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("<color=#66E680><b>잘한 점</b></color>");
            if (score.GoodPoints.Count == 0) sb.AppendLine("· (없음)");
            for (int i = 0; i < score.GoodPoints.Count && i < 4; i++) sb.AppendLine("· " + score.GoodPoints[i]);
            sb.AppendLine();
            sb.AppendLine("<color=#FF9A6B><b>아쉬운 점</b></color>");
            if (score.BadPoints.Count == 0) sb.AppendLine("· (없음)");
            for (int i = 0; i < score.BadPoints.Count && i < 4; i++) sb.AppendLine("· " + score.BadPoints[i]);
            return sb.ToString();
        }

        private string BuildScoreText(ScoreSystem score)
        {
            if (score == null) return string.Empty;
            var sb = new StringBuilder();
            if (score.HasRules)
            {
                foreach (ScoreSystem.Line l in score.Breakdown) sb.AppendLine(string.Format(scoreLineFormat, l.label, l.earned, l.max));
            }
            else if (score.Categories != null)
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
