using System.Text;
using GameCore;
using TMPro;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// J 키(패드 방향키 아래)로 여는 현재 목표 상세 패널. 진행 중/완료/실패 목표의 제목과 설명(ObjectiveData.Description)을 보여준다.
    /// 게임을 멈추지 않는 가벼운 오버레이이며, 다시 누르거나 일시정지/스마트폰이 열리면 닫힌다.
    /// </summary>
    public class ObjectiveDetailPanel : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField] private ObjectiveSystem objectives;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private string title = "현재 목표";
        [SerializeField] private string emptyText = "아직 정해진 목표가 없습니다.";
        [SerializeField] private string activeFormat = "<b>● {0}</b>\n<color=#B8C4D0>{1}</color>";
        [SerializeField] private string doneFormat = "<s><alpha=#88>{0}</s>";
        [SerializeField] private string failedFormat = "<color=#FF6B6B><s>{0}</s></color>";
        [SerializeField] private float fadeSpeed = 8f;

        private bool open;

        private void Awake() { if (panel != null) panel.alpha = 0f; }
        private void OnEnable() { if (input != null) input.ObjectivesPressed += Toggle; }
        private void OnDisable() { if (input != null) input.ObjectivesPressed -= Toggle; }

        private void Update()
        {
            if (open && (GameFlow.GameplayBlocked || GameFlow.IsPhoneOpen)) open = false;
            if (panel != null) panel.alpha = Mathf.MoveTowards(panel.alpha, open ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        }

        private void Toggle()
        {
            if (GameFlow.GameplayBlocked || GameFlow.IsPhoneOpen) return;
            open = !open;
            if (open) Refresh();
        }

        private void Refresh()
        {
            if (titleText != null) titleText.text = title;
            if (bodyText == null) return;
            var sb = new StringBuilder();
            if (objectives != null)
                foreach (ObjectiveSystem.Entry e in objectives.Entries)
                {
                    if (e.Data == null) continue;
                    if (sb.Length > 0) sb.Append("\n\n");
                    switch (e.State)
                    {
                        case ObjectiveState.Completed: sb.Append(string.Format(doneFormat, e.Data.Title)); break;
                        case ObjectiveState.Failed: sb.Append(string.Format(failedFormat, e.Data.Title)); break;
                        default: sb.Append(string.Format(activeFormat, e.Data.Title, e.Data.Description)); break;
                    }
                }
            bodyText.text = sb.Length > 0 ? sb.ToString() : emptyText;
        }
    }
}
