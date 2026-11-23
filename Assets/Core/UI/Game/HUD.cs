using System.Collections;
using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 게임 HUD (모든 시나리오 공통, 공통 HUD 프리팹 GameHUD 의 루트 컴포넌트).
    /// 영역(기준 1920x1080, 세이프 에어리어 안쪽):
    ///   · 우측 상단  : 생존 상태 (SurvivalStatsPanel — 호흡기·화상은 여기서 등록, 시나리오 1 은 WarHud 가 등록)
    ///   · 상단 중앙  : 새 목표 알림 배너 (약 3초, 설정으로 끌 수 있음) / 시나리오 전용 큰 팝업(WarHud 재난문자)은 그 아래
    ///   · 하단 중앙  : 인벤토리 → 상호작용 프롬프트 → 힌트 토스트 → 안내 문구 → 자막 (고정 슬롯 — 서로 겹치지 않는다)
    ///   · 중앙       : 크로스헤어, 길게 누르기 진행 바, 감각 묘사
    /// 목표 목록은 더 이상 상시 표시하지 않는다 — J 키 패널(ObjectiveDetailPanel)로 본다.
    /// 시스템 참조(interactor/vitals/inventory/objectives)는 프리팹 안에 저장할 수 없으므로 비어 있으면 씬에서 찾아 쓴다.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("시스템 참조 (비우면 씬에서 자동으로 찾는다)")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerVitals vitals;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private ObjectiveSystem objectives;

        [Header("크로스헤어 / 프롬프트")]
        [SerializeField] private Image crosshair;
        [SerializeField] private Color crosshairIdle = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color crosshairActive = new Color(0.18f, 0.61f, 1f, 1f);
        [SerializeField, Min(1f), Tooltip("상호작용 대상을 조준했을 때 크로스헤어 배율")] private float crosshairActiveScale = 1.8f;
        [SerializeField] private CanvasGroup promptGroup;
        [SerializeField] private TMP_Text promptText;
        [SerializeField, Tooltip("{0} = 키, {1} = 동작")] private string promptFormat = "[{0}] {1}";
        [SerializeField] private string interactKeyLabel = "E";
        [SerializeField] private string interactPadLabel = "A";
        [SerializeField, Tooltip("길게 눌러야 하는 대상의 프롬프트 뒤에 붙는 문구")] private string holdSuffix = "  (길게 누르기)";
        [SerializeField] private CanvasGroup holdGroup;
        [SerializeField] private Image holdFill;

        [Header("새 목표 알림 (상단 중앙)")]
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private string bannerAddedFormat = "새 목표: {0}";
        [SerializeField] private string bannerCompletedFormat = "목표 완료: {0}";
        [SerializeField] private string bannerFailedFormat = "목표 실패: {0}";
        [SerializeField] private Color bannerAddedColor = new Color(0.18f, 0.61f, 1f);
        [SerializeField] private Color bannerCompletedColor = new Color(0.4f, 0.9f, 0.5f);
        [SerializeField] private Color bannerFailedColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private float bannerSlideDistance = 24f;
        [SerializeField, Tooltip("페이드 인/아웃 시간 (초). 총 표시 시간 = 2 x 페이드 + 유지")] private float bannerFadeTime = 0.3f;
        [SerializeField, Tooltip("완전히 보이는 시간 (초). 기본 2.4 + 페이드 0.6 = 약 3초")] private float bannerHoldTime = 2.4f;

        [Header("생존 상태 (우측 상단)")]
        [SerializeField] private SurvivalStatsPanel stats;
        [SerializeField] private Color statGood = new Color(0.45f, 0.85f, 0.55f);
        [SerializeField] private Color statBad = new Color(0.95f, 0.25f, 0.2f);

        [Header("인벤토리")]
        [SerializeField] private InventorySlotView[] slotViews;

        [Header("안내 문구")]
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float messageHoldTime = 2.5f;

        [Header("감각 묘사 (냄새 등)")]
        [SerializeField] private CanvasGroup scentGroup;
        [SerializeField] private TMP_Text scentText;
        [SerializeField] private float scentHoldTime = 3.5f;

        private readonly Queue<(string text, Color color)> bannerQueue = new Queue<(string, Color)>();
        private Coroutine bannerRoutine, messageRoutine;
        private RectTransform bannerRect;
        private Vector2 bannerHome;
        private bool vitalsAllowed = true;

        public static HUD Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>(FindObjectsInactive.Include);
            if (inventory == null) inventory = FindFirstObjectByType<InventorySystem>(FindObjectsInactive.Include);
            if (objectives == null) objectives = FindFirstObjectByType<ObjectiveSystem>(FindObjectsInactive.Include);
            if (stats == null) stats = GetComponentInChildren<SurvivalStatsPanel>(true);

            if (bannerGroup != null) { bannerRect = (RectTransform)bannerGroup.transform; bannerHome = bannerRect.anchoredPosition; bannerGroup.alpha = 0f; }
            if (messageGroup != null) messageGroup.alpha = 0f;
            if (scentGroup != null) scentGroup.alpha = 0f;
            if (holdGroup != null) holdGroup.alpha = 0f;
            if (promptGroup != null) promptGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (objectives != null) objectives.Changed += OnObjectiveChanged;
            if (inventory != null) inventory.Changed += RefreshInventory;
            EventBus.MessageRequested += ShowMessage;
            EventBus.ScentRequested += ShowScent;
        }

        private void OnDisable()
        {
            if (objectives != null) objectives.Changed -= OnObjectiveChanged;
            if (inventory != null) inventory.Changed -= RefreshInventory;
            EventBus.MessageRequested -= ShowMessage;
            EventBus.ScentRequested -= ShowScent;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            RefreshInventory();
            RegisterVitals();
        }

        private void Update() { UpdatePrompt(); }

        // ───────── 생존 상태: 호흡기·화상 (연기·화재 속에서만 표시) ─────────
        private void RegisterVitals()
        {
            if (stats == null || vitals == null) return;
            PlayerVitals v = vitals;
            stats.SetStat("breath", "호흡기", () => v.Normalized, () => (v.Normalized * 100f).ToString("0"),
                () => vitalsAllowed && (v.IsInSmoke || v.Normalized < 0.999f), statGood, statBad);
            stats.SetStat("burn", "화상", () => v.Burn01, () => (v.Burn01 * 100f).ToString("0"),
                () => vitalsAllowed && v.Burn01 > 0.01f, statGood, statBad, true);
        }

        /// <summary>호흡기·화상 표시 허용 여부 (ScenarioDefinition.useRespiratoryGauge). false 면 연기 속에서도 숨긴다</summary>
        public void SetVitalsAllowed(bool allowed) { vitalsAllowed = allowed; }

        // ───────── 프롬프트 ─────────
        private void UpdatePrompt()
        {
            InteractableObject target = interactor != null ? interactor.Current : null;
            bool show = target != null;
            if (show && promptText != null)
            {
                string key = GameInput.UsingGamepad ? interactPadLabel : interactKeyLabel;
                promptText.text = string.Format(promptFormat, key, target.DisplayPrompt()) + (target.EffectiveHold > 0f ? holdSuffix : string.Empty);
            }
            if (promptGroup != null) promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 10f);
            if (crosshair != null)
            {
                crosshair.color = show ? crosshairActive : crosshairIdle;
                // 상호작용 가능한 대상을 조준하면 점이 커진다 ("하얀 점이 커지면 상호작용 가능" 힌트와 연동)
                float s = Mathf.Lerp(crosshair.rectTransform.localScale.x, show ? crosshairActiveScale : 1f, Time.unscaledDeltaTime * 14f);
                crosshair.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            float hold = interactor != null ? interactor.HoldProgress : 0f;
            if (holdFill != null) holdFill.fillAmount = hold;
            if (holdGroup != null) holdGroup.alpha = hold > 0f ? 1f : 0f;
        }

        // ───────── 인벤토리 ─────────
        private void RefreshInventory()
        {
            if (inventory == null || slotViews == null) return;
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null) continue;
                ItemData item = inventory.GetItem(i);
                slotViews[i].Show(i, item, i == inventory.SelectedIndex, inventory.IsEquipped(item));
            }
        }

        // ───────── 새 목표 알림 (목록 상시 표시는 없음 — J 키 패널) ─────────
        private void OnObjectiveChanged(ObjectiveSystem.Entry entry, ObjectiveSystem.ChangeKind kind)
        {
            if (entry == null || entry.Data == null || !GameSettings.ObjectiveNotice) return;
            switch (kind)
            {
                case ObjectiveSystem.ChangeKind.Added: EnqueueBanner(string.Format(bannerAddedFormat, entry.Data.Title), bannerAddedColor); break;
                case ObjectiveSystem.ChangeKind.Completed: EnqueueBanner(string.Format(bannerCompletedFormat, entry.Data.Title), bannerCompletedColor); break;
                case ObjectiveSystem.ChangeKind.Failed: EnqueueBanner(string.Format(bannerFailedFormat, entry.Data.Title), bannerFailedColor); break;
            }
        }

        private void EnqueueBanner(string text, Color color)
        {
            if (bannerGroup == null || bannerText == null) return;
            bannerQueue.Enqueue((text, color));
            if (bannerRoutine == null) bannerRoutine = StartCoroutine(BannerLoop());
        }

        private IEnumerator BannerLoop()
        {
            while (bannerQueue.Count > 0)
            {
                var (text, color) = bannerQueue.Dequeue();
                bannerText.text = text;
                bannerText.color = color;

                yield return FadeBanner(0f, 1f, bannerFadeTime, true);
                yield return new WaitForSecondsRealtime(bannerHoldTime);
                yield return FadeBanner(1f, 0f, bannerFadeTime, false);
            }
            bannerRoutine = null;
        }

        private IEnumerator FadeBanner(float from, float to, float duration, bool slideIn)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration))
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                bannerGroup.alpha = Mathf.Lerp(from, to, k);
                float slide = slideIn ? (1f - k) : 0f;
                bannerRect.anchoredPosition = bannerHome + Vector2.up * (bannerSlideDistance * slide);
                yield return null;
            }
            bannerGroup.alpha = to;
            bannerRect.anchoredPosition = bannerHome;
        }

        // ───────── 안내 문구 ─────────
        public void ShowMessage(string text) => ShowMessage(text, -1f);

        /// <summary>하단 안내 문구. holdSeconds 가 0 이하이면 기본 유지 시간(messageHoldTime) 사용</summary>
        public void ShowMessage(string text, float holdSeconds)
        {
            if (messageGroup == null || messageText == null) return;
            messageText.text = text;
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine(holdSeconds > 0f ? holdSeconds : messageHoldTime));
        }

        private Coroutine scentRoutine;

        /// <summary>냄새 같은 감각 묘사 (옅은 노란 글씨, 천천히 사라짐)</summary>
        public void ShowScent(string text)
        {
            if (scentGroup == null || scentText == null) return;
            scentText.text = text;
            if (scentRoutine != null) StopCoroutine(scentRoutine);
            scentRoutine = StartCoroutine(ScentRoutine());
        }

        private IEnumerator ScentRoutine()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { scentGroup.alpha = t; yield return null; }
            scentGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(scentHoldTime);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.5f) { scentGroup.alpha = 1f - t; yield return null; }
            scentGroup.alpha = 0f;
            scentRoutine = null;
        }

        private IEnumerator MessageRoutine(float hold)
        {
            messageGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                messageGroup.alpha = 1f - t;
                yield return null;
            }
            messageGroup.alpha = 0f;
            messageRoutine = null;
        }
    }
}
