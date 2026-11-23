using QuakeSurvival.Interaction;
using UnityEngine;
using TMPro;

namespace QuakeSurvival.UI
{
    /// <summary>바라보는 물건의 안내 문구를 "[F] 가스 밸브 잠그기" 형태로 표시한다.</summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string keyHint = "[F] ";

        private void OnEnable()
        {
            if (interactor == null || label == null)
            {
                Debug.LogError("[InteractionPromptUI] interactor 또는 label이 연결되지 않았습니다.", this);
                return;
            }
            interactor.PromptChanged += Show;
            Show(interactor.CurrentPrompt);
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.PromptChanged -= Show;
        }

        private void Show(string prompt)
        {
            bool hasPrompt = !string.IsNullOrEmpty(prompt);
            label.enabled = hasPrompt;
            label.text = hasPrompt ? keyHint + prompt : string.Empty;
        }
    }
}
