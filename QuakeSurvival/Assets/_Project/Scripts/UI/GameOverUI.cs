using QuakeSurvival.Audio;
using System.Collections;
using QuakeSurvival.Interaction;
using QuakeSurvival.Player;
using QuakeSurvival.Safety;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace QuakeSurvival.UI
{
    /// <summary>
    /// 판정 결과 화면.
    /// - 사망: 화면을 어둡게 하고 힌트만 보여준다.
    /// - 생존: 잠시(가족 연락 시간) 기다린 뒤 등급과 수칙 체크 결과를 보여준다.
    /// R키로 처음(평상시 -> 전진)부터 다시 시작한다.
    /// </summary>
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private EvacuationRecord record;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Interactor interactor;
        [SerializeField] private MessageUI messages;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private float fadeSeconds = 1.5f;
        [SerializeField] private float clearGraceSeconds = 8f;
        [SerializeField] private AudioClip deathSting;
        [SerializeField] private AudioClip clearSting;

        private const string RestartHint = "\n\n[R] 다시 시작";
        private static readonly Color DeathColor = Art.Palette.AccentEmergency;
        private static readonly Color ClearColor = Art.Palette.UiClear;
        private bool shown;

        private void Awake()
        {
            bool missing = evaluator == null || record == null || player == null || interactor == null
                || messages == null || group == null || title == null || body == null;
            if (missing || group.gameObject == gameObject)
            {
                Debug.LogError("[GameOverUI] 참조가 없거나 group이 자기 자신에 붙어 있습니다(별도 자식 패널이어야 함).", this);
                enabled = false;
                return;
            }
            group.alpha = 0f;
            group.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (evaluator != null)
            {
                evaluator.Decided += OnDecided;
                evaluator.DeathPrevented += OnDeathPrevented;
            }
        }

        private void OnDisable()
        {
            if (evaluator != null)
            {
                evaluator.Decided -= OnDecided;
                evaluator.DeathPrevented -= OnDeathPrevented;
            }
        }

        // 대피지역이라 살아남은 순간: 위험했다는 것만 알려 준다(감점은 결과 화면에서)
        private void OnDeathPrevented(DeathCause cause)
        {
            if (shown) return;
            messages.ShowSubtitle("(대피지역 안이라 간신히 무사했다... 하지만 정말 위험한 순간이었다)");
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!shown || keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) Restart();
        }

        private void OnDecided(DeathCause cause)
        {
            if (cause == DeathCause.None) StartCoroutine(ShowClearAfterGrace());
            else ShowDeath(cause);
        }

        private void ShowDeath(DeathCause cause)
        {
            DeathInfo info = DeathCatalog.Get(cause);
            SoundPlayer.Play2D(deathSting, AudioCategory.Ui, 0.8f);
            Present(info.Title, DeathColor, info.Hint + RestartHint);
        }

        private IEnumerator ShowClearAfterGrace()
        {
            if (evaluator.ClearKind == ClearKind.Shelter)
            {
                messages.ShowSubtitle("(대피소에 도착했다... 가족이 걱정하고 있을 것이다. [T] 휴대폰)");
                yield return new WaitForSeconds(clearGraceSeconds);
            }

            string intro = ClearIntro(evaluator.ClearKind);
            EvacuationReport report = record.CreateReport();
            SoundPlayer.Play2D(clearSting, AudioCategory.Ui, 0.7f);
            Present("생존했습니다", ClearColor, intro + ResultFormatter.Body(report) + RestartHint);
        }

        private static string ClearIntro(ClearKind kind)
        {
            switch (kind)
            {
                case ClearKind.Vertical:
                    return "대피소까지는 가지 못했지만, 물이 닿지 않는 높은 곳에서 버텼다.\n\n";
                case ClearKind.Protected:
                    return "물살이 대피지역까지 밀려왔지만 간신히 살아남았다. 더 높은 곳으로 갔어야 했다...\n\n";
                default:
                    return "지진해일 대피소에 도착했다. 아래로 거센 물살이 마을을 덮치고 있다...\n\n";
            }
        }

        private void Present(string titleText, Color titleColor, string bodyText)
        {
            player.InputEnabled = false;
            interactor.InputEnabled = false;
            messages.HideAll();

            title.color = titleColor;
            title.text = titleText;
            body.text = bodyText;

            shown = true;
            group.gameObject.SetActive(true);
            StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Clamp01(t / fadeSeconds);
                yield return null;
            }
            group.alpha = 1f;
        }

        private static void Restart()
        {
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
        }
    }
}
