using System.Text;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1 전용 HUD (런타임 생성): 방사능·방어도·쾌적도·정신력·체온·자원 요약, 재난문자 팝업, 화이트아웃.
    /// 한국어 글꼴은 HomeBase 의 기존 UI(ChoicePanel)에서 빌려 쓴다. 시나리오 씬이 언로드되면 함께 사라진다.
    /// </summary>
    public class WarHud : MonoBehaviour
    {
        private Canvas canvas;
        private TMP_Text stats, popupText, noticeText;
        private CanvasGroup popup;
        private Image whiteout;
        private float popupUntil, whiteHold, whiteFade, whiteTimer, whiteAlpha;
        private bool whiteActive;
        private float refreshTimer;

        public static WarHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private static TMP_FontAsset FindFont()
        {
            ChoicePanel cp = FindFirstObjectByType<ChoicePanel>(FindObjectsInactive.Include);
            if (cp != null) { TMP_Text t = cp.GetComponentInChildren<TMP_Text>(true); if (t != null && t.font != null) return t.font; }
            TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
            return any != null ? any.font : null;
        }

        private TMP_Text MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        private void Build()
        {
            TMP_FontAsset font = FindFont();
            var cgo = new GameObject("WarHudCanvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 좌측 상단 상태 패널
            var panel = new GameObject("StatsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(cgo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(20f, -20f); prt.sizeDelta = new Vector2(430f, 250f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);
            panel.GetComponent<Image>().raycastTarget = false;
            stats = MakeText(panel.transform, "Stats", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -20f), 24f, TextAlignmentOptions.TopLeft, font);

            // 재난문자 팝업 (상단 중앙)
            var pgo = new GameObject("AlertPopup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pgo.transform.SetParent(cgo.transform, false);
            var pr = pgo.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -70f); pr.sizeDelta = new Vector2(1100f, 190f);
            pgo.GetComponent<Image>().color = new Color(0.85f, 0.1f, 0.1f, 0.92f);
            popup = pgo.GetComponent<CanvasGroup>(); popup.alpha = 0f; popup.blocksRaycasts = false;
            popupText = MakeText(pgo.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, -24f), 36f, TextAlignmentOptions.Center, font);

            // 하단 알림
            noticeText = MakeText(cgo.transform, "Notice", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1200f, 70f), 30f, TextAlignmentOptions.Center, font);
            noticeText.color = new Color(1f, 0.92f, 0.6f, 0f);

            // 화이트아웃
            var wgo = new GameObject("Whiteout", typeof(RectTransform), typeof(Image));
            wgo.transform.SetParent(cgo.transform, false);
            var wr = wgo.GetComponent<RectTransform>();
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one; wr.offsetMin = wr.offsetMax = Vector2.zero;
            whiteout = wgo.GetComponent<Image>(); whiteout.color = new Color(1f, 1f, 1f, 0f); whiteout.raycastTarget = false;
        }

        public void ShowAlert(string text, float seconds)
        {
            if (popupText != null) popupText.text = text;
            if (popup != null) popup.alpha = 1f;
            popupUntil = Time.unscaledTime + seconds;
        }

        private float noticeUntil;
        public void Notice(string text, float seconds)
        {
            if (noticeText == null) return;
            noticeText.text = text;
            noticeText.color = new Color(1f, 0.92f, 0.6f, 1f);
            noticeUntil = Time.unscaledTime + seconds;
        }

        /// <summary>화이트아웃: 즉시 흰 화면 → hold 초 유지 → fade 초 동안 사라짐</summary>
        public void Whiteout(float hold, float fade)
        {
            whiteActive = true; whiteHold = hold; whiteFade = Mathf.Max(0.1f, fade); whiteTimer = 0f; whiteAlpha = 1f;
        }

        private void Update()
        {
            if (popup != null && popup.alpha > 0f && Time.unscaledTime > popupUntil) popup.alpha = Mathf.MoveTowards(popup.alpha, 0f, Time.unscaledDeltaTime * 2f);
            if (noticeText != null && noticeText.color.a > 0f && Time.unscaledTime > noticeUntil)
            { Color c = noticeText.color; c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 1.5f); noticeText.color = c; }

            if (whiteActive && whiteout != null)
            {
                whiteTimer += Time.unscaledDeltaTime;
                whiteAlpha = whiteTimer <= whiteHold ? 1f : Mathf.Clamp01(1f - (whiteTimer - whiteHold) / whiteFade);
                whiteout.color = new Color(1f, 1f, 1f, whiteAlpha);
                if (whiteAlpha <= 0f) whiteActive = false;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 0.2f; Refresh(); }
        }

        private static string Bar(float v, float max = 100f)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(v / max * 10f), 0, 10);
            return new string('■', n) + new string('□', 10 - n);
        }

        private void Refresh()
        {
            WarState s = WarState.Instance;
            if (s == null || stats == null) return;
            var sb = new StringBuilder();
            sb.Append("<b>생존 상태</b>\n");
            sb.AppendFormat("방사능  {0}  {1:0}\n", Bar(s.Radiation), s.Radiation);
            sb.AppendFormat("쾌적도  {0}  {1:0}\n", Bar(s.Comfort), s.Comfort);
            sb.AppendFormat("정신력  {0}  {1:0}\n", Bar(s.Sanity), s.Sanity);
            sb.AppendFormat("체온      {0}  {1:0}\n", Bar(s.BodyTemp), s.BodyTemp);
            sb.AppendFormat("식량 {0}  물 {1}  약품 {2}  행동력 {3}\n", s.Food, s.Water, s.Medicine, s.ActionPoints);
            sb.AppendFormat("기만 단서 {0}  동행 {1}", s.DeceptionCluesCount, s.AliveSurvivors);
            if (s.GasMaskWorn) sb.Append("  [방독면 착용]");
            stats.text = sb.ToString();
        }
    }
}
