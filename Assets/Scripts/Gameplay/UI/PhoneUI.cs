using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 스마트폰 UI (Tab 으로 열기/닫기). 시계, 손전등(SpotLight) 토글, 문자 메시지 목록, 119 통화 버튼 자리.
    /// 손전등은 폰을 닫아도 유지되며 F 키/패드 십자키 위로도 켜고 끌 수 있다.
    /// 메시지는 AddMessage 로 추가, 119 통화는 PhoneAction 이벤트만 발행한다 (실제 결과는 Act 로직이 처리).
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private GameInput input;
        [SerializeField] private GameClock clock;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private RectTransform panelRect;

        [Header("시계")]
        [SerializeField] private TMP_Text clockText;

        [Header("손전등")]
        [SerializeField] private Light flashlight;
        [SerializeField] private Button flashlightButton;
        [SerializeField] private TMP_Text flashlightLabel;
        [SerializeField] private Image flashlightIndicator;
        [SerializeField] private string flashlightOnText = "손전등  ON";
        [SerializeField] private string flashlightOffText = "손전등  OFF";
        [SerializeField] private Color indicatorOn = new Color(1f, 0.9f, 0.4f);
        [SerializeField] private Color indicatorOff = new Color(0.3f, 0.3f, 0.3f);

        [Header("문자 메시지")]
        [SerializeField] private RectTransform messageListRoot;
        [SerializeField, Tooltip("복제용 템플릿 (비활성 상태로 둔다)")] private TMP_Text messageRowTemplate;
        [SerializeField] private GameObject emptyLabel;
        [SerializeField, Tooltip("{0} = 보낸 사람, {1} = 내용")] private string messageFormat = "<b>{0}</b>\n{1}";

        [Header("119")]
        [SerializeField] private Button call119Button;
        [SerializeField] private TMP_Text callStatusText;
        [SerializeField] private string call119ActionId = "call_119";
        [SerializeField, Tooltip("폰을 열 때 발행하는 PhoneAction id")] private string phoneOpenActionId = "phone_open";
        [SerializeField, Tooltip("손전등 토글 시 발행하는 PhoneAction id (State = 켜짐)")] private string flashlightActionId = "flashlight";
        [SerializeField] private string callPlaceholderText = "119 연결 중...";

        [Header("열기/닫기")]
        [SerializeField] private float fadeSpeed = 8f;

        private readonly List<GameObject> messageRows = new List<GameObject>();
        private bool isOpen;
        private float shown;

        public bool IsOpen => isOpen;
        public bool FlashlightOn => flashlight != null && flashlight.enabled;

        private void Awake()
        {
            if (panel != null) { panel.alpha = 0f; panel.interactable = false; panel.blocksRaycasts = false; }
            if (flashlight != null) flashlight.enabled = false;
            if (messageRowTemplate != null) messageRowTemplate.gameObject.SetActive(false);
            if (callStatusText != null) callStatusText.text = string.Empty;
            if (flashlightButton != null) flashlightButton.onClick.AddListener(ToggleFlashlight);
            if (call119Button != null) call119Button.onClick.AddListener(OnCall119);
            RefreshFlashlight();
            RefreshEmpty();
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.PhonePressed += OnPhonePressed;
            input.FlashlightPressed += ToggleFlashlight;
        }

        private void OnDisable()
        {
            if (input == null) return;
            input.PhonePressed -= OnPhonePressed;
            input.FlashlightPressed -= ToggleFlashlight;
        }

        private void Update()
        {
            if (isOpen && GameFlow.GameplayBlocked) SetOpen(false);

            shown = Mathf.MoveTowards(shown, isOpen ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            if (panel != null) panel.alpha = shown;
            if (panelRect != null) panelRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, shown);
            if (clockText != null && clock != null) clockText.text = clock.FormattedTime;
        }

        private void OnPhonePressed()
        {
            if (GameFlow.GameplayBlocked) return;
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            bool wasOpen = isOpen;
            isOpen = open;
            GameFlow.SetPhoneOpen(open);
            if (open && !wasOpen) EventBus.Raise(ScenarioEventType.PhoneAction, phoneOpenActionId, true);
            if (panel != null) { panel.interactable = open; panel.blocksRaycasts = open; }

            EventSystem es = EventSystem.current;
            if (es == null) return;
            if (open && flashlightButton != null) es.SetSelectedGameObject(flashlightButton.gameObject);
            else if (!open) es.SetSelectedGameObject(null);
        }

        // ───────── 손전등 ─────────
        public void ToggleFlashlight()
        {
            if (flashlight == null || GameFlow.GameplayBlocked) return;
            flashlight.enabled = !flashlight.enabled;
            RefreshFlashlight();
            EventBus.Raise(ScenarioEventType.PhoneAction, flashlightActionId, flashlight.enabled);
        }

        private void RefreshFlashlight()
        {
            bool on = FlashlightOn;
            if (flashlightLabel != null) flashlightLabel.text = on ? flashlightOnText : flashlightOffText;
            if (flashlightIndicator != null) flashlightIndicator.color = on ? indicatorOn : indicatorOff;
        }

        // ───────── 문자 메시지 ─────────
        public void AddMessage(string sender, string body)
        {
            if (messageListRoot == null || messageRowTemplate == null) return;
            TMP_Text row = Instantiate(messageRowTemplate, messageListRoot);
            row.gameObject.SetActive(true);
            row.text = string.Format(messageFormat, sender, body);
            row.transform.SetAsFirstSibling(); // 최신 메시지가 위
            messageRows.Add(row.gameObject);
            RefreshEmpty();
        }

        public void ClearMessages()
        {
            foreach (GameObject g in messageRows) if (g != null) Destroy(g);
            messageRows.Clear();
            RefreshEmpty();
        }

        private void RefreshEmpty()
        {
            if (emptyLabel != null) emptyLabel.SetActive(messageRows.Count == 0);
        }

        // ───────── 119 ─────────
        private void OnCall119()
        {
            if (callStatusText != null) callStatusText.text = callPlaceholderText;
            EventBus.Raise(ScenarioEventType.PhoneAction, call119ActionId);
        }
    }
}
