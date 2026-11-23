using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1 전용 HUD (런타임 생성): 재난문자 팝업, 화이트아웃, 그리고 생존 상태 행 등록.
    /// 생존 상태(방사능·쾌적도·정신력·체온·자원 요약)는 공통 HUD 의 SurvivalStatsPanel(우측 상단)에 등록한다 —
    /// 예전에는 이 클래스가 좌측 상단에 자체 패널을 그려 공통 목표 텍스트와 겹쳤다.
    /// 하단 알림(Notice)은 공통 HUD 의 안내 문구 슬롯을 쓴다 (호흡기·힌트 토스트와 겹치지 않는다).
    /// 재난문자 팝업은 상단 중앙의 새 목표 알림 배너 바로 아래에 놓인다.
    /// 한국어 글꼴은 HomeBase 의 기존 UI(ChoicePanel)에서 빌려 쓴다. 시나리오 씬이 언로드되면 함께 사라진다.
    /// </summary>
    public class WarHud : MonoBehaviour
    {
        private Canvas canvas;
        private TMP_Text popupText;
        private CanvasGroup popup;
        private Image whiteout;
        private float popupUntil, whiteHold, whiteFade, whiteTimer, whiteAlpha;
        private bool whiteActive;

        public static WarHud Instance { get; private set; }

        private static readonly Color StatGood = new Color(0.45f, 0.85f, 0.55f), StatBad = new Color(0.95f, 0.25f, 0.2f);

        private void Awake()
        {
            Instance = this;
            Build();
        }

        private void Start() { RegisterStats(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (canvas != null) Destroy(canvas.gameObject);
            SurvivalStatsPanel p = SurvivalStatsPanel.Instance;
            if (p != null) foreach (string id in StatIds) p.Remove(id);
        }

        private static readonly string[] StatIds = { "war_radiation", "war_comfort", "war_sanity", "war_temp", "war_res", "war_misc" };

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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 공통 GameCanvas 와 같은 규칙

            // 세이프 에어리어 안쪽 기준 (공통 HUD 와 같은 규칙)
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safe.transform.SetParent(cgo.transform, false);
            var srt = (RectTransform)safe.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;

            // 재난문자 팝업 (상단 중앙, 새 목표 배너 아래)
            var pgo = new GameObject("AlertPopup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pgo.transform.SetParent(safe.transform, false);
            var pr = pgo.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 1f);
            pr.anchoredPosition = new Vector2(0f, -140f); pr.sizeDelta = new Vector2(700f, 190f);   // 우측 상단 패널(폭 400)·좌측 J 패널(폭 460)과 가로로 겹치지 않는 폭
            pgo.GetComponent<Image>().color = new Color(0.85f, 0.1f, 0.1f, 0.92f);
            popup = pgo.GetComponent<CanvasGroup>(); popup.alpha = 0f; popup.blocksRaycasts = false;
            popupText = MakeText(pgo.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, -24f), 36f, TextAlignmentOptions.Center, font);

            // 화이트아웃 (전체 화면)
            var wgo = new GameObject("Whiteout", typeof(RectTransform), typeof(Image));
            wgo.transform.SetParent(cgo.transform, false);
            var wr = wgo.GetComponent<RectTransform>();
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one; wr.offsetMin = wr.offsetMax = Vector2.zero;
            whiteout = wgo.GetComponent<Image>(); whiteout.color = new Color(1f, 1f, 1f, 0f); whiteout.raycastTarget = false;
        }

        // ───────── 생존 상태 (공통 HUD 우측 상단 패널에 등록) ─────────
        private static float Pct(float v) => Mathf.Clamp01(v / 100f);

        private void RegisterStats()
        {
            SurvivalStatsPanel p = SurvivalStatsPanel.Instance;
            if (p == null) { Debug.LogWarning("[WarHud] SurvivalStatsPanel 이 없어 생존 상태를 표시하지 못합니다 (공통 HUD 프리팹 확인)."); return; }
            System.Func<bool> on = () => WarState.Instance != null;
            p.SetStat("war_radiation", "방사능", () => WarState.Instance != null ? Pct(WarState.Instance.Radiation) : 0f, () => WarState.Instance != null ? WarState.Instance.Radiation.ToString("0") : "-", on, StatGood, StatBad, true);
            p.SetStat("war_comfort", "쾌적도", () => WarState.Instance != null ? Pct(WarState.Instance.Comfort) : 0f, () => WarState.Instance != null ? WarState.Instance.Comfort.ToString("0") : "-", on, StatGood, StatBad);
            p.SetStat("war_sanity", "정신력", () => WarState.Instance != null ? Pct(WarState.Instance.Sanity) : 0f, () => WarState.Instance != null ? WarState.Instance.Sanity.ToString("0") : "-", on, StatGood, StatBad);
            p.SetStat("war_temp", "체온", () => WarState.Instance != null ? Pct(WarState.Instance.BodyTemp) : 0f, () => WarState.Instance != null ? WarState.Instance.BodyTemp.ToString("0") : "-", on, StatGood, StatBad);
            p.SetLine("war_res", () =>
            {
                WarState s = WarState.Instance;
                return s == null ? string.Empty : $"식량 {s.Food}   물 {s.Water}   약품 {s.Medicine}   행동력 {s.ActionPoints}";
            }, on);
            p.SetLine("war_misc", () =>
            {
                WarState s = WarState.Instance;
                if (s == null) return string.Empty;
                return $"기만 단서 {s.DeceptionCluesCount}   동행 {s.AliveSurvivors}" + (s.GasMaskWorn ? "   [방독면 착용]" : string.Empty);
            }, on);
        }

        public void ShowAlert(string text, float seconds)
        {
            if (popupText != null) popupText.text = text;
            if (popup != null) popup.alpha = 1f;
            popupUntil = Time.unscaledTime + seconds;
        }

        /// <summary>하단 안내 문구 (공통 HUD 의 안내 슬롯). seconds 동안 유지</summary>
        public void Notice(string text, float seconds)
        {
            HUD hud = HUD.Instance;
            if (hud != null) hud.ShowMessage(text, seconds);
            else GameCore.EventBus.ShowMessage(text);
        }

        /// <summary>화이트아웃: 즉시 흰 화면 → hold 초 유지 → fade 초 동안 사라짐</summary>
        public void Whiteout(float hold, float fade)
        {
            whiteActive = true; whiteHold = hold; whiteFade = Mathf.Max(0.1f, fade); whiteTimer = 0f; whiteAlpha = 1f;
        }

        private void Update()
        {
            if (popup != null && popup.alpha > 0f && Time.unscaledTime > popupUntil) popup.alpha = Mathf.MoveTowards(popup.alpha, 0f, Time.unscaledDeltaTime * 2f);

            if (whiteActive && whiteout != null)
            {
                whiteTimer += Time.unscaledDeltaTime;
                whiteAlpha = whiteTimer <= whiteHold ? 1f : Mathf.Clamp01(1f - (whiteTimer - whiteHold) / whiteFade);
                whiteout.color = new Color(1f, 1f, 1f, whiteAlpha);
                if (whiteAlpha <= 0f) whiteActive = false;
            }
        }
    }
}
