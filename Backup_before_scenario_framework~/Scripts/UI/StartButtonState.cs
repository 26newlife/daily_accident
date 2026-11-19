using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// START GAME 버튼의 활성/비활성 상태와 "선택된 시나리오" 안내 문구를 관리한다.
    /// - 시나리오 선택 전: interactable = false → MenuButtonFX가 회색 톤으로 표시
    /// - 선택 후: interactable = true → MenuButtonFX가 0.3초 색상 페이드 + 펄스로 파란색 전환
    /// 색상 보간 자체는 MenuButtonFX가 담당하므로 여기서는 interactable만 바꾼다.
    /// </summary>
    public class StartButtonState : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Button startButton;
        [SerializeField] private MenuButtonFX startFx;
        [SerializeField] private TMP_Text selectedLabel;

        [Header("문구 / 색상")]
        [SerializeField] private string noSelectionText = "시나리오를 선택하세요";
        [SerializeField] private string selectedFormat = "SELECTED: {0}";
        [SerializeField] private Color noSelectionColor = new Color(0.42f, 0.447f, 0.502f, 1f);  // #6B7280
        [SerializeField] private Color selectedColor = new Color(0.18f, 0.61f, 1f, 1f);          // #2F9BFF

        public bool IsReady => startButton != null && startButton.interactable;

        /// <summary>선택된 시나리오를 반영 (null이면 비활성)</summary>
        /// <param name="animate">false면 색상 전환 없이 즉시 반영 (씬 시작 시 복원용)</param>
        public void ApplyScenario(ScenarioData scenario, bool animate)
        {
            bool ready = scenario != null;

            if (startButton != null) startButton.interactable = ready;
            if (!animate && startFx != null) startFx.SyncInteractableImmediate();

            if (selectedLabel != null)
            {
                selectedLabel.text = ready ? string.Format(selectedFormat, scenario.DisplayName) : noSelectionText;
                selectedLabel.color = ready ? selectedColor : noSelectionColor;
            }
        }
    }
}
