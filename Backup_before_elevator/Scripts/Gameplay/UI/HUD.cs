using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameGameplay
{
    /// <summary>
    /// 게임 HUD: 크로스헤어, 상호작용 프롬프트, 현재 목표 + 목표 알림 배너, 게임 내 시간,
    /// 호흡기 게이지(연기 속에서만 표시), 인벤토리 바, 안내 문구(토스트).
    /// 모든 문구 형식은 Inspector 에서 바꿀 수 있고, 데이터는 각 시스템의 이벤트를 구독해 갱신한다.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("시스템 참조")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerVitals vitals;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private ObjectiveSystem objectives;
        [SerializeField] private GameClock clock;

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

        [Header("목표")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private string objectiveHeader = "목표";
        [SerializeField] private string mainFormat = "● {0}";
        [SerializeField] private string subFormat = "○ {0}";
        [SerializeField] private string completedFormat = "<s><alpha=#88>{0}</s>";
        [SerializeField] private string failedFormat = "<color=#FF6B6B><s>{0}</s></color>";
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private string bannerAddedFormat = "새 목표  {0}";
        [SerializeField] private string bannerCompletedFormat = "목표 완료  {0}";
        [SerializeField] private string bannerFailedFormat = "목표 실패  {0}";
        [SerializeField] private Color bannerAddedColor = new Color(0.18f, 0.61f, 1f);
        [SerializeField] private Color bannerCompletedColor = new Color(0.4f, 0.9f, 0.5f);
        [SerializeField] private Color bannerFailedColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private float bannerSlideDistance = 24f;
        [SerializeField] private float bannerFadeTime = 0.3f;
        [SerializeField] private float bannerHoldTime = 2.2f;
        [SerializeField, Tooltip("새 목표가 추가될 때 목표 목록이 한 번 커졌다 돌아오는 정도")] private float objectivePulseScale = 1.1f;
        [SerializeField] private float objectivePulseTime = 0.6f;
        [SerializeField] private Color objectivePulseColor = new Color(0.6f, 0.85f, 1f);

        [Header("시계")]
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private string clockFormat = "{0}";

        [Header("호흡기 게이지")]
        [SerializeField] private CanvasGroup vitalsGroup;
        [SerializeField] private Image vitalsFill;
        [SerializeField] private Color vitalsColorFull = new Color(0.45f, 0.85f, 0.55f);
        [SerializeField] private Color vitalsColorEmpty = new Color(0.95f, 0.25f, 0.2f);
        [SerializeField] private float vitalsFadeSpeed = 4f;

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
        private Coroutine bannerRoutine, messageRoutine, pulseRoutine;
        private RectTransform bannerRect;
        private Vector2 bannerHome;

        private void Awake()
        {
            if (bannerGroup != null) { bannerRect = (RectTransform)bannerGroup.transform; bannerHome = bannerRect.anchoredPosition; bannerGroup.alpha = 0f; }
            if (messageGroup != null) messageGroup.alpha = 0f;
            if (scentGroup != null) scentGroup.alpha = 0f;
            if (holdGroup != null) holdGroup.alpha = 0f;
            if (promptGroup != null) promptGroup.alpha = 0f;
            if (vitalsGroup != null) vitalsGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            if (objectives != null) objectives.Changed += OnObjectiveChanged;
            if (inventory != null) inventory.Changed += RefreshInventory;
            if (vitals != null) vitals.Changed += RefreshVitals;
            EventBus.MessageRequested += ShowMessage;
            EventBus.ScentRequested += ShowScent;
        }

        private void OnDisable()
        {
            if (objectives != null) objectives.Changed -= OnObjectiveChanged;
            if (inventory != null) inventory.Changed -= RefreshInventory;
            if (vitals != null) vitals.Changed -= RefreshVitals;
            EventBus.MessageRequested -= ShowMessage;
            EventBus.ScentRequested -= ShowScent;
        }

        private void Start()
        {
            RefreshObjectives();
            RefreshInventory();
            RefreshVitals();
        }

        private void Update()
        {
            UpdatePrompt();
            UpdateClock();
            UpdateVitalsVisibility();
        }

        // ───────── 프롬프트 ─────────
        private void UpdatePrompt()
        {
            InteractableObject target = interactor != null ? interactor.Current : null;
            bool show = target != null;
            if (show && promptText != null)
            {
                string key = GameInput.UsingGamepad ? interactPadLabel : interactKeyLabel;
                promptText.text = string.Format(promptFormat, key, target.GetPrompt()) + (target.HoldSeconds > 0f ? holdSuffix : string.Empty);
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

        private void UpdateClock()
        {
            if (clockText != null && clock != null) clockText.text = string.Format(clockFormat, clock.FormattedTime);
        }

        // ───────── 호흡기 게이지 ─────────
        private void UpdateVitalsVisibility()
        {
            if (vitalsGroup == null || vitals == null) return;
            bool show = vitals.IsInSmoke || vitals.Normalized < 0.999f;
            vitalsGroup.alpha = Mathf.MoveTowards(vitalsGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * vitalsFadeSpeed);
        }

        private void RefreshVitals()
        {
            if (vitals == null || vitalsFill == null) return;
            vitalsFill.fillAmount = vitals.Normalized;
            vitalsFill.color = Color.Lerp(vitalsColorEmpty, vitalsColorFull, vitals.Normalized);
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

        // ───────── 목표 ─────────
        private void OnObjectiveChanged(ObjectiveSystem.Entry entry, ObjectiveSystem.ChangeKind kind)
        {
            RefreshObjectives();
            if (entry == null || entry.Data == null) return;

            switch (kind)
            {
                case ObjectiveSystem.ChangeKind.Added:
                    EnqueueBanner(string.Format(bannerAddedFormat, entry.Data.Title), bannerAddedColor);
                    if (objectiveText != null) { if (pulseRoutine != null) StopCoroutine(pulseRoutine); pulseRoutine = StartCoroutine(PulseObjectives()); }
                    break;
                case ObjectiveSystem.ChangeKind.Completed: EnqueueBanner(string.Format(bannerCompletedFormat, entry.Data.Title), bannerCompletedColor); break;
                case ObjectiveSystem.ChangeKind.Failed: EnqueueBanner(string.Format(bannerFailedFormat, entry.Data.Title), bannerFailedColor); break;
            }
        }

        /// <summary>새 목표 추가 시 목록을 잠깐 키우고 색을 밝혔다가 돌려 놓는다 (약한 강조)</summary>
        private IEnumerator PulseObjectives()
        {
            Transform t = objectiveText.transform;
            Color baseColor = objectiveText.color;
            for (float e = 0f; e < objectivePulseTime; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(e / objectivePulseTime * Mathf.PI);   // 0 → 1 → 0
                t.localScale = Vector3.one * Mathf.Lerp(1f, objectivePulseScale, k);
                objectiveText.color = Color.Lerp(baseColor, objectivePulseColor, k);
                yield return null;
            }
            t.localScale = Vector3.one;
            objectiveText.color = baseColor;
            pulseRoutine = null;
        }

        private void RefreshObjectives()
        {
            if (objectiveText == null || objectives == null) return;
            var sb = new StringBuilder();
            if (objectives.Entries.Count > 0) sb.AppendLine(objectiveHeader);
            foreach (ObjectiveSystem.Entry e in objectives.Entries)
            {
                string line = string.Format(e.Data.IsMain ? mainFormat : subFormat, e.Data.Title);
                if (e.State == ObjectiveState.Completed) line = string.Format(completedFormat, line);
                else if (e.State == ObjectiveState.Failed) line = string.Format(failedFormat, line);
                sb.AppendLine(line);
            }
            objectiveText.text = sb.ToString();
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
        public void ShowMessage(string text)
        {
            if (messageGroup == null || messageText == null) return;
            messageText.text = text;
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine());
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

        private IEnumerator MessageRoutine()
        {
            messageGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(messageHoldTime);
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
