using System.Collections;
using UnityEngine;
using TMPro;

namespace QuakeSurvival.UI
{
    /// <summary>화면 아래 자막과 오른쪽 위 재난문자 팝업을 표시한다.</summary>
    public sealed class MessageUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private GameObject alertPanel;
        [SerializeField] private TMP_Text alertText;
        [SerializeField] private float subtitleSeconds = 5f;
        [SerializeField] private float alertSeconds = 12f;

        private Coroutine subtitleRoutine;
        private Coroutine alertRoutine;

        private void Awake()
        {
            if (subtitle == null || alertPanel == null || alertText == null)
            {
                Debug.LogError("[MessageUI] UI 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            subtitle.text = string.Empty;
            alertPanel.SetActive(false);
        }

        public void ShowSubtitle(string text)
        {
            if (!enabled) return;
            if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
            subtitleRoutine = StartCoroutine(ShowFor(subtitleSeconds, () => subtitle.text = text, () => subtitle.text = string.Empty));
        }

        /// <summary>재난문자가 뜰 때 알린다(수신음 재생용).</summary>
        public event System.Action AlertShown;

        public void ShowAlert(string text)
        {
            if (!enabled) return;
            AlertShown?.Invoke();
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertRoutine = StartCoroutine(ShowFor(alertSeconds,
                () => { alertText.text = text; alertPanel.SetActive(true); },
                () => alertPanel.SetActive(false)));
        }

        public void HideAll()
        {
            if (!enabled) return;
            StopAllCoroutines();
            subtitle.text = string.Empty;
            alertPanel.SetActive(false);
        }

        private static IEnumerator ShowFor(float seconds, System.Action show, System.Action hide)
        {
            show();
            yield return new WaitForSeconds(seconds);
            hide();
        }
    }
}
