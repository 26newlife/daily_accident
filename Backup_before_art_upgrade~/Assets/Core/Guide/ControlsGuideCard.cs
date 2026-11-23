using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 조작법 패널. 게임 시작 때 자동으로 뜨지 않고, 일시정지 메뉴의 "조작법" 버튼으로만 열린다.
    /// 오른쪽 위 X 버튼(또는 ESC / 패드 B 를 PauseMenu 가 전달)으로 닫을 때까지 계속 표시되며, 자동으로 사라지지 않는다.
    /// 닫으면 일시정지 메뉴로 돌아간다 (게임은 재개되지 않는다). 일시정지(timeScale 0) 중에도 동작하도록 unscaled 시간만 쓴다.
    /// </summary>
    public class ControlsGuideCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button closeButton;
        [SerializeField] private string title = "조작법";
        [SerializeField, TextArea(8, 14), Tooltip("키보드/마우스와 게임패드 표기를 한 줄에 함께 보여준다")] private string controlsText =
            "WASD / 왼쪽 스틱<pos=46%>이동\n" +
            "마우스 / 오른쪽 스틱<pos=46%>시점\n" +
            "Shift / L3<pos=46%>달리기\n" +
            "Ctrl / B<pos=46%>웅크리기\n" +
            "Space / Y<pos=46%>점프\n" +
            "E / A<pos=46%>상호작용 (길게 누르기 포함)\n" +
            "Tab / Select<pos=46%>스마트폰\n" +
            "J / 방향키 아래<pos=46%>목표 확인\n" +
            "ESC / Start<pos=46%>일시정지";
        [SerializeField] private float fadeSpeed = 10f;

        private bool visible;

        public bool IsVisible => visible;

        private void Awake()
        {
            if (panel != null) { panel.alpha = 0f; panel.blocksRaycasts = false; panel.interactable = false; }
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        private void Update()
        {
            if (panel == null) return;
            panel.alpha = Mathf.MoveTowards(panel.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        }

        /// <summary>패널 표시. X 로 닫기 전까지 유지된다</summary>
        public void Show()
        {
            visible = true;
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = controlsText;
            if (panel != null) { panel.blocksRaycasts = true; panel.interactable = true; }
            transform.SetAsLastSibling();   // 일시정지 메뉴 위에 그려지게
            if (closeButton != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        /// <summary>패널만 닫는다 (일시정지 상태는 유지)</summary>
        public void Hide()
        {
            if (!visible) return;
            visible = false;
            if (panel != null) { panel.blocksRaycasts = false; panel.interactable = false; }
        }
    }
}
