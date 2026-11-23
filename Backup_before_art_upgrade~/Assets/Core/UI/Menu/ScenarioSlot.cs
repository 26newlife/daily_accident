using System;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public enum ScenarioSlotState
    {
        Locked,     // 잠김 (회색, 자물쇠, 클릭 불가)
        Available,  // 선택 가능
        Selected,   // 현재 선택됨 (파란 테두리 고정 + 체크)
    }

    /// <summary>
    /// 시나리오 슬롯 1개. 번호 / 이름 / 설명 표시와 상태(잠김·가능·선택됨) 전환을 담당한다.
    /// 호버·포커스 효과는 같은 오브젝트의 MenuButtonFX가 처리한다.
    /// </summary>
    public class ScenarioSlot : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private GameObject lockIcon;        // 잠김일 때만 표시
        [SerializeField] private GameObject selectedBadge;   // 선택됨 표시 (체크 + "선택됨")
        [SerializeField] private Image selectedFrame;        // 선택됨일 때 고정되는 파란 테두리

        [Header("색상")]
        [SerializeField] private Color numberColor = new Color(0.18f, 0.61f, 1f, 1f);       // #2F9BFF
        [SerializeField] private Color descriptionColor = new Color(0.60f, 0.68f, 0.76f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.42f, 0.447f, 0.502f, 1f);  // #6B7280

        [Header("문구")]
        [SerializeField] private string lockedDescriptionFallback = "준비 중 (COMING SOON)";

        public ScenarioData Data { get; private set; }
        public ScenarioSlotState State { get; private set; }
        public Button Button => button;

        /// <summary>활성 슬롯이 눌렸을 때</summary>
        public event Action<ScenarioSlot> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        /// <summary>데이터로 슬롯 내용을 채운다 (number는 1부터)</summary>
        public void Setup(int number, ScenarioData data)
        {
            Data = data;
            bool unlocked = data != null && data.IsUnlocked;

            if (numberText != null) numberText.text = number.ToString("00");
            if (nameText != null) nameText.text = data != null ? data.DisplayName : "???";
            if (descriptionText != null)
            {
                string desc = data != null ? data.Description : null;
                descriptionText.text = string.IsNullOrEmpty(desc) && !unlocked ? lockedDescriptionFallback : desc;
            }

            ApplyState(unlocked ? ScenarioSlotState.Available : ScenarioSlotState.Locked);
        }

        /// <summary>선택됨 표시 on/off (잠긴 슬롯은 무시)</summary>
        public void SetSelected(bool selected)
        {
            if (State == ScenarioSlotState.Locked) return;
            ApplyState(selected ? ScenarioSlotState.Selected : ScenarioSlotState.Available);
        }

        private void ApplyState(ScenarioSlotState state)
        {
            State = state;
            bool locked = state == ScenarioSlotState.Locked;

            if (button != null) button.interactable = !locked;
            if (lockIcon != null) lockIcon.SetActive(locked);
            if (selectedBadge != null) selectedBadge.SetActive(state == ScenarioSlotState.Selected);
            if (selectedFrame != null) selectedFrame.enabled = state == ScenarioSlotState.Selected;

            if (numberText != null) numberText.color = locked ? lockedColor : numberColor;
            if (descriptionText != null) descriptionText.color = locked ? lockedColor : descriptionColor;
            // 이름 색은 MenuButtonFX(label)가 기본/호버/비활성에 맞춰 관리한다
        }

        private void OnClick()
        {
            if (State == ScenarioSlotState.Locked) return;
            Clicked?.Invoke(this);
        }
    }
}
