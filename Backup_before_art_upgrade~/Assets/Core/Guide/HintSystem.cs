using System.Collections;
using System.Collections.Generic;
using GameCore;
using TMPro;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 화면 하단 중앙 튜토리얼 토스트. HintData 목록의 트리거를 감시하다가 조건이 처음 맞으면 한 번만 표시한다.
    /// 난이도(GameSettings.HintLevel): Easy = 전부, Normal = 조작/시스템 힌트만(easyOnly 제외), Hard = 없음.
    /// 게임 시간이 멈춰도(일시정지) 페이드는 unscaled 시간으로 진행된다.
    /// </summary>
    public class HintSystem : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private GameInput input;
        [SerializeField] private PlayerVitals vitals;
        [SerializeField] private Transform player;

        [Header("힌트 목록")]
        [SerializeField] private HintData[] hints;

        [Header("토스트 UI")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private float fadeTime = 0.35f;
        [SerializeField, Tooltip("힌트와 힌트 사이 최소 간격(초)")] private float gapBetween = 0.8f;
        [SerializeField, Tooltip("조작 가이드 카드가 떠 있는 동안은 힌트를 미룬다")] private ControlsGuideCard controlsCard;

        [Header("버튼 이름 (문구의 {토큰} 치환)")]
        [SerializeField] private string interactKey = "E", interactPad = "A";
        [SerializeField] private string phoneKey = "Tab", phonePad = "Select";
        [SerializeField] private string crouchKey = "Ctrl", crouchPad = "B";
        [SerializeField] private string sprintKey = "Shift", sprintPad = "L3";
        [SerializeField] private string jumpKey = "Space", jumpPad = "Y";

        private sealed class Pending { public HintData data; public float readyAt; }

        private readonly HashSet<string> shown = new HashSet<string>();
        private readonly List<Pending> pending = new List<Pending>();
        private readonly Dictionary<string, Transform> idMap = new Dictionary<string, Transform>();
        private bool displaying, mapBuilt;
        private float playTime, nextAllowedAt, broadcastAt = -1f;

        public static HintSystem Instance { get; private set; }

        /// <summary>시나리오 정의의 힌트를 기본(공용) 힌트에 덧붙인다</summary>
        public void AddScenarioHints(HintData[] extra)
        {
            if (extra == null || extra.Length == 0) return;
            var list = new System.Collections.Generic.List<HintData>(hints ?? new HintData[0]);
            foreach (HintData h in extra) if (h != null && !list.Contains(h)) list.Add(h);
            hints = list.ToArray();
        }

        private void Awake()
        {
            Instance = this;
            if (toastGroup != null) toastGroup.alpha = 0f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable()
        {
            if (interactor != null) interactor.TargetChanged += OnTargetChanged;
            EventBus.ScenarioEventRaised += OnScenarioEvent;
            EventBus.ItemAcquired += OnItemAcquired;
            EventBus.SubtitleRequested += OnSubtitle;
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.TargetChanged -= OnTargetChanged;
            EventBus.ScenarioEventRaised -= OnScenarioEvent;
            EventBus.ItemAcquired -= OnItemAcquired;
            EventBus.SubtitleRequested -= OnSubtitle;
        }

        private void Update()
        {
            if (GameFlow.IsEnded) return;
            if (!GameFlow.GameplayBlocked) playTime += Time.deltaTime;
            if (!mapBuilt) BuildIdMap();

            Poll();
            TryDisplayNext();
        }

        // ───────── 트리거 감시 ─────────
        private void Poll()
        {
            if (hints == null) return;
            HazardManager hz = HazardManager.Instance;
            foreach (HintData h in hints)
            {
                if (h == null || AlreadyQueuedOrShown(h)) continue;
                switch (h.Trigger)
                {
                    case HintTrigger.FirstMove:
                        if (input != null && input.Move.sqrMagnitude > 0.04f) Queue(h);
                        break;
                    case HintTrigger.AfterSeconds:
                        if (playTime >= h.TriggerValue) Queue(h);
                        break;
                    case HintTrigger.GasSmell:
                        if (hz != null && hz.PlayerGas > h.TriggerValue) Queue(h);
                        break;
                    case HintTrigger.BroadcastHeard:
                        if (broadcastAt >= 0f && playTime >= broadcastAt + h.TriggerValue) Queue(h);
                        break;
                    case HintTrigger.SmokeEntered:
                        if (hz != null && hz.PlayerSmoke > h.TriggerValue) Queue(h);
                        break;
                    case HintTrigger.VitalsShown:
                        if (vitals != null && vitals.IsInSmoke) Queue(h);
                        break;
                    case HintTrigger.NearInteractable:
                        if (player != null && idMap.TryGetValue(h.TriggerParam ?? string.Empty, out Transform t) && t != null
                            && (t.position - player.position).sqrMagnitude <= h.TriggerValue * h.TriggerValue) Queue(h);
                        break;
                }
            }
        }

        private void OnTargetChanged(InteractableObject target)
        {
            if (target == null || hints == null) return;
            foreach (HintData h in hints)
            {
                if (h == null || AlreadyQueuedOrShown(h)) continue;
                if (h.Trigger == HintTrigger.FirstAim) Queue(h);
                else if (h.Trigger == HintTrigger.FirstAimHold && target.HoldSeconds > 0f) Queue(h);
            }
        }

        private void OnScenarioEvent(ScenarioEvent e)
        {
            if (hints == null) return;
            foreach (HintData h in hints)
            {
                if (h == null || h.Trigger != HintTrigger.ScenarioEvent || AlreadyQueuedOrShown(h)) continue;
                if (h.ScenarioType != e.Type || e.State != h.EventState) continue;
                if (MatchesAny(h.TriggerParam, e.SourceId)) Queue(h);
            }
        }

        private void OnItemAcquired(string itemId)
        {
            if (hints == null) return;
            foreach (HintData h in hints)
            {
                if (h == null || AlreadyQueuedOrShown(h)) continue;
                if (h.Trigger == HintTrigger.ItemAcquiredAny) Queue(h);
                else if (h.Trigger == HintTrigger.ItemAcquiredId && MatchesAny(h.TriggerParam, itemId)) Queue(h);
            }
        }

        private void OnSubtitle(string text, float seconds)
        {
            if (broadcastAt < 0f) broadcastAt = playTime;
        }

        private static bool MatchesAny(string list, string id)
        {
            if (string.IsNullOrEmpty(list) || string.IsNullOrEmpty(id)) return false;
            foreach (string s in list.Split(';')) if (s.Trim() == id) return true;
            return false;
        }

        private void BuildIdMap()
        {
            mapBuilt = true;
            foreach (InteractableObject io in FindObjectsByType<InteractableObject>(FindObjectsSortMode.None))
                if (!idMap.ContainsKey(io.Id)) idMap[io.Id] = io.transform;
        }

        // ───────── 큐 / 표시 ─────────
        private bool AlreadyQueuedOrShown(HintData h)
        {
            if (!h.Repeat && shown.Contains(h.Id)) return true;
            foreach (Pending p in pending) if (p.data == h) return true;
            return false;
        }

        /// <summary>힌트를 대기열에 넣는다 (난이도 필터 적용)</summary>
        public void Queue(HintData h)
        {
            if (h == null || !h.IsAllowedAt(GameSettings.HintLevel)) { if (h != null && !h.Repeat) shown.Add(h.Id); return; }
            pending.Add(new Pending { data = h, readyAt = Time.unscaledTime + h.Delay });
        }

        private void TryDisplayNext()
        {
            if (displaying || pending.Count == 0 || Time.unscaledTime < nextAllowedAt) return;
            if (GameFlow.GameplayBlocked || (controlsCard != null && controlsCard.IsVisible)) return;

            Pending best = null;
            foreach (Pending p in pending)
                if (p.readyAt <= Time.unscaledTime && (best == null || p.data.Priority > best.data.Priority)) best = p;
            if (best == null) return;

            pending.Remove(best);
            if (!best.data.Repeat) shown.Add(best.data.Id);
            if (!best.data.IsAllowedAt(GameSettings.HintLevel)) return;   // 대기 중 난이도가 바뀐 경우
            StartCoroutine(ShowRoutine(best.data));
        }

        private IEnumerator ShowRoutine(HintData h)
        {
            displaying = true;
            if (toastText != null) toastText.text = Format(h.Text);
            yield return Fade(1f);
            float t = 0f;
            while (t < h.Duration) { if (!GameFlow.GameplayBlocked) t += Time.unscaledDeltaTime; yield return null; }
            yield return Fade(0f);
            displaying = false;
            nextAllowedAt = Time.unscaledTime + gapBetween;
        }

        private IEnumerator Fade(float target)
        {
            if (toastGroup == null) yield break;
            float start = toastGroup.alpha, t = 0f;
            while (t < fadeTime) { t += Time.unscaledDeltaTime; toastGroup.alpha = Mathf.Lerp(start, target, t / fadeTime); yield return null; }
            toastGroup.alpha = target;
        }

        /// <summary>{interact} 등 토큰을 현재 입력 장치의 버튼 이름으로 치환</summary>
        private string Format(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            bool pad = GameInput.UsingGamepad;
            return s.Replace("{interact}", pad ? interactPad : interactKey)
                    .Replace("{phone}", pad ? phonePad : phoneKey)
                    .Replace("{crouch}", pad ? crouchPad : crouchKey)
                    .Replace("{sprint}", pad ? sprintPad : sprintKey)
                    .Replace("{jump}", pad ? jumpPad : jumpKey);
        }
    }
}
