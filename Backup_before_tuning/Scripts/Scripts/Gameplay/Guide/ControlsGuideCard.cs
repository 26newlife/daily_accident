using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace GameGameplay
{
    /// <summary>
    /// 조작 가이드 카드. 게임 시작 직후 한 번 자동으로 뜨고(3~5초 또는 아무 입력 후 사라짐),
    /// 일시정지 메뉴의 "조작법" 버튼으로 다시 볼 수 있다 (이 경우 아무 입력이 올 때까지 유지).
    /// 게임패드를 쓰는 중이면 패드 버튼 문구로 자동 전환된다.
    /// </summary>
    public class ControlsGuideCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private string title = "조작법";
        [SerializeField, TextArea(6, 12)] private string keyboardText =
            "WASD<pos=38%>이동\n마우스<pos=38%>시점\nShift<pos=38%>달리기\nCtrl<pos=38%>웅크리기\nSpace<pos=38%>점프\nE<pos=38%>상호작용\nTab<pos=38%>스마트폰\nJ<pos=38%>목표 확인\nESC<pos=38%>일시정지";
        [SerializeField, TextArea(6, 12)] private string gamepadText =
            "왼쪽 스틱<pos=38%>이동\n오른쪽 스틱<pos=38%>시점\nL3<pos=38%>달리기\nB<pos=38%>웅크리기\nY<pos=38%>점프\nA<pos=38%>상호작용\nSelect<pos=38%>스마트폰\n방향키 아래<pos=38%>목표 확인\nStart<pos=38%>일시정지";
        [SerializeField] private string dismissHint = "아무 키나 누르면 닫힙니다";
        [SerializeField] private TMP_Text hintText;

        [Header("자동 표시")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField, Min(0f)] private float startDelay = 1.2f;
        [SerializeField, Min(1f)] private float autoHideSeconds = 5f;
        [SerializeField] private float fadeSpeed = 6f;
        [SerializeField, Tooltip("표시 직후 이 시간(초) 동안은 입력으로 닫히지 않는다 (시작 클릭이 바로 닫는 것 방지)")] private float inputGuardSeconds = 0.8f;

        private bool visible, modal;
        private float shownAt, hideAt;
        private System.IDisposable anyPress;

        /// <summary>마지막으로 닫힌 시각(unscaled). 같은 키(ESC)가 일시정지까지 열지 않도록 PauseMenu 가 참고한다</summary>
        public static float LastHiddenTime { get; private set; } = -10f;
        public bool IsVisible => visible;

        private void Awake()
        {
            if (panel != null) { panel.alpha = 0f; panel.blocksRaycasts = false; panel.interactable = false; }
        }

        private void Start()
        {
            if (showOnStart) Invoke(nameof(ShowAuto), startDelay);
        }

        private void OnDestroy() => StopListening();

        private void Update()
        {
            float target = visible ? 1f : 0f;
            if (panel != null) panel.alpha = Mathf.MoveTowards(panel.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            if (visible && !modal && Time.unscaledTime >= hideAt) Hide();
        }

        private void ShowAuto() => Show(false);

        /// <summary>카드 표시. modal = true 이면 입력이 올 때까지 유지 (일시정지 메뉴에서 호출)</summary>
        public void Show(bool keepUntilInput)
        {
            modal = keepUntilInput;
            visible = true;
            shownAt = Time.unscaledTime;
            hideAt = shownAt + autoHideSeconds;
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = GameInput.UsingGamepad ? gamepadText : keyboardText;
            if (hintText != null) hintText.text = modal ? dismissHint : string.Empty;
            StopListening();
            anyPress = InputSystem.onAnyButtonPress.Call(OnAnyPress);
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            LastHiddenTime = Time.unscaledTime;
            StopListening();
        }

        private void OnAnyPress(InputControl control)
        {
            if (!visible || Time.unscaledTime - shownAt < inputGuardSeconds) return;
            Hide();
        }

        private void StopListening()
        {
            if (anyPress != null) { anyPress.Dispose(); anyPress = null; }
        }
    }
}
