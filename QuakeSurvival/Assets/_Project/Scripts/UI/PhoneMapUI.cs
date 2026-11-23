using TMPro;
using UnityEngine;

namespace QuakeSurvival.UI
{
    /// <summary>
    /// 휴대폰 화면: 주변 대피 지도(위가 북쪽=바다)와 내 위치 화살표, 아래쪽 메뉴 글자.
    /// 지도 그림은 에디터 빌더가 미리 만들어 두고, 여기서는 켜고 끄기와 내 위치만 갱신한다.
    /// </summary>
    public sealed class PhoneMapUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private TMP_Text menuText;
        [SerializeField] private Transform player;

        [Header("지도 좌표(월드 x,z -> 지도 픽셀)")]
        [SerializeField] private Vector2 worldCenter = new Vector2(0f, -35f);
        [SerializeField] private float pixelsPerMeter = 2.8f;
        [SerializeField] private Vector2 mapHalfSize = new Vector2(170f, 200f);

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel == null || playerMarker == null || menuText == null || player == null)
            {
                Debug.LogError("[PhoneMapUI] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            panel.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            if (!enabled) return;
            panel.SetActive(open);
            if (open) UpdateMarker();
        }

        public void SetMenu(string text)
        {
            if (!enabled) return;
            menuText.text = text;
        }

        private void LateUpdate()
        {
            if (IsOpen) UpdateMarker();
        }

        private void UpdateMarker()
        {
            Vector3 p = player.position;
            var pos = new Vector2(p.x - worldCenter.x, p.z - worldCenter.y) * pixelsPerMeter;
            // 지도 밖으로 나가면 가장자리에 붙여 방향만 알 수 있게 한다
            pos.x = Mathf.Clamp(pos.x, -mapHalfSize.x, mapHalfSize.x);
            pos.y = Mathf.Clamp(pos.y, -mapHalfSize.y, mapHalfSize.y);
            playerMarker.anchoredPosition = pos;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        }
    }
}
