using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 재사용 가능한 예 / 아니요 확인창 (콜백 방식).
    /// 사용: confirmDialog.Show("메시지", () => 예 처리, () => 아니요 처리);
    /// 바깥 클릭 / X / ESC / 게임패드 B 는 모두 "아니요"로 처리한다.
    /// </summary>
    public class ConfirmDialog : PopupBase
    {
        [Header("확인창")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onYes;
        private Action onNo;
        private bool restoreFocusAfterYes = true;

        protected override void Awake()
        {
            base.Awake();
            if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
            if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
        }

        /// <param name="restoreFocusAfterYes">"예" 후 포커스를 확인창 열기 전 위치로 되돌릴지 (호출자가 직접 포커스를 옮길 때는 false)</param>
        public void Show(string message, Action yes, Action no = null, bool restoreFocusAfterYes = true)
        {
            if (IsOpen) return;
            if (messageText != null) messageText.text = message;
            onYes = yes;
            onNo = no;
            this.restoreFocusAfterYes = restoreFocusAfterYes;
            Open();
        }

        public override void RequestClose() => OnNoClicked();

        protected override Selectable GetInitialSelection() => yesButton;

        private void OnYesClicked()
        {
            if (!IsOpen) return;
            Action cb = onYes;
            onYes = onNo = null;
            Close(restoreFocusAfterYes);
            cb?.Invoke();
        }

        private void OnNoClicked()
        {
            if (!IsOpen) return;
            Action cb = onNo;
            onYes = onNo = null;
            Close();
            cb?.Invoke();
        }
    }
}
