using System;
using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 시나리오 선택 팝업. ScenarioDatabase를 읽어 슬롯을 자동 생성(GridLayoutGroup)하고,
    /// 활성 슬롯을 누르면 확인창을 띄운다. "예"를 누르면 GameSession에 저장하고 팝업을 닫는다.
    /// 시나리오를 추가하려면 ScenarioDatabase 목록에 ScenarioData만 추가하면 된다.
    /// </summary>
    public class ScenarioSelectPopup : PopupBase
    {
        [Header("시나리오")]
        [SerializeField] private ScenarioDatabase database;
        [SerializeField] private ScenarioSlot slotPrefab;
        [SerializeField] private RectTransform gridRoot;        // GridLayoutGroup이 붙은 부모

        [Header("확인창")]
        [SerializeField] private ConfirmDialog confirmDialog;
        [SerializeField] private string confirmFormat = "{0}을(를) 선택하시겠습니까?";

        private readonly List<ScenarioSlot> slots = new List<ScenarioSlot>();

        /// <summary>"예"로 시나리오가 확정되었을 때</summary>
        public event Action<ScenarioData> ScenarioConfirmed;

        protected override void Awake()
        {
            base.Awake();
            BuildSlots();
        }

        /// <summary>데이터베이스 내용으로 슬롯을 (다시) 만든다</summary>
        public void BuildSlots()
        {
            slots.Clear();
            if (gridRoot == null || slotPrefab == null || database == null)
            {
                Debug.LogError($"[ScenarioSelectPopup] 슬롯을 만들 수 없습니다. 참조 확인: database={(database != null)}, slotPrefab={(slotPrefab != null)}, gridRoot={(gridRoot != null)} " +
                               "(비어 있으면 Inspector에서 연결하거나 Tools > GameUI > Build Main Menu 를 다시 실행하세요)", this);
                return;
            }

            for (int i = gridRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = gridRoot.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            for (int i = 0; i < database.Count; i++)
            {
                ScenarioData data = database.Scenarios[i];
                if (data == null) continue;
                ScenarioSlot slot = Instantiate(slotPrefab, gridRoot);
                slot.name = $"Slot_{i + 1:00}";
                slot.Setup(i + 1, data);
                slot.Clicked += OnSlotClicked;
                slots.Add(slot);
            }
            Debug.Log($"슬롯 생성: {slots.Count}개 (데이터베이스 {database.Count}개)", this);
            RefreshSelection();
        }

        protected override void OnOpening()
        {
            if (slots.Count == 0) BuildSlots(); // 혹시 Awake에서 만들어지지 못했다면 열릴 때 다시 시도
            RefreshSelection();
        }

        /// <summary>현재 GameSession 선택에 맞춰 "선택됨" 표시 갱신</summary>
        private void RefreshSelection()
        {
            ScenarioData selected = GameSession.SelectedScenario;
            foreach (ScenarioSlot slot in slots) slot.SetSelected(slot.Data == selected);
        }

        protected override Selectable GetInitialSelection()
        {
            ScenarioData selected = GameSession.SelectedScenario;
            foreach (ScenarioSlot slot in slots)
                if (selected != null && slot.Data == selected) return slot.Button;
            foreach (ScenarioSlot slot in slots)
                if (slot.Button != null && slot.Button.interactable) return slot.Button;
            return closeButton;
        }

        private void OnSlotClicked(ScenarioSlot slot)
        {
            ScenarioData data = slot.Data;
            if (data == null || !data.IsUnlocked) return;

            if (confirmDialog == null) { Confirm(data); return; }
            confirmDialog.Show(string.Format(confirmFormat, data.DisplayName), () => Confirm(data), null, false);
        }

        private void Confirm(ScenarioData data)
        {
            GameSession.SelectScenario(data);
            RefreshSelection();
            ScenarioConfirmed?.Invoke(data);
            Close(); // 닫히면서 SCENARIO SELECT 버튼으로 포커스가 돌아간다
        }
    }
}
