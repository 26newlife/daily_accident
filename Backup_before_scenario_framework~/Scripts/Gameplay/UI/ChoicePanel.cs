using System;
using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 범용 선택지 패널: NPC 대화(설득 선택지), 119 신고 위치 선택 등에 쓴다.
    /// 열려 있는 동안 GameFlow.IsModalOpen = true (커서 해제 + 이동/시점 정지, 게임 시간은 계속 흐른다).
    /// 마우스 클릭, 숫자키 1~N, Esc(닫기)로 고른다. 선택하지 않고 닫아도 아무 일도 일어나지 않는다.
    /// </summary>
    public class ChoicePanel : MonoBehaviour
    {
        public struct Option
        {
            public string label;
            public Action onChosen;
            public Option(string label, Action onChosen) { this.label = label; this.onChosen = onChosen; }
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TMP_Text[] optionLabels;
        [SerializeField] private string cancelLabel = "닫기";

        private readonly List<Option> current = new List<Option>();
        private int cancelIndex = -1;

        public static ChoicePanel Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            Instance = this;
            SetVisible(false);
            if (optionButtons != null)
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    int index = i;
                    if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => Choose(index));
                }
        }

        private void OnDisable() { if (IsOpen) Close(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) GameFlow.SetModalOpen(false);
        }

        /// <summary>선택지를 연다. 마지막에 "닫기" 항목이 자동으로 붙는다 (allowCancel = false 면 생략)</summary>
        public void Show(string title, string body, IList<Option> options, bool allowCancel = true)
        {
            if (GameFlow.IsEnded || optionButtons == null) return;
            current.Clear();
            cancelIndex = -1;
            if (options != null) foreach (Option o in options) current.Add(o);
            int maxOptions = optionButtons.Length - (allowCancel ? 1 : 0);
            if (current.Count > maxOptions) current.RemoveRange(maxOptions, current.Count - maxOptions);
            if (allowCancel) { cancelIndex = current.Count; current.Add(new Option(cancelLabel, null)); }

            if (titleText != null) titleText.text = title ?? string.Empty;
            if (bodyText != null) bodyText.text = body ?? string.Empty;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool used = i < current.Count;
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(used);
                if (used && optionLabels != null && i < optionLabels.Length && optionLabels[i] != null)
                    optionLabels[i].text = $"{i + 1}.  {current[i].label}";
            }

            IsOpen = true;
            GameFlow.SetModalOpen(true);
            SetVisible(true);
            if (EventSystem.current != null && optionButtons.Length > 0) EventSystem.current.SetSelectedGameObject(optionButtons[0].gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameFlow.SetModalOpen(false);
            SetVisible(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= current.Count) return;
            Action act = current[index].onChosen;
            Close();
            act?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (GameFlow.IsEnded) { Close(); return; }
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            for (int i = 0; i < keys.Length && i < current.Count; i++)
                if (kb[keys[i]].wasPressedThisFrame) { Choose(i); return; }
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
