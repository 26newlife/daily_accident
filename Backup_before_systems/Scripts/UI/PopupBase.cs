using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 모달 팝업 공통 베이스 (시나리오 선택 / 설정 / 확인창이 상속).
    /// - 열고 닫을 때 페이드 + 스케일(0.95 → 1.0) 애니메이션
    /// - 딤(바깥) 클릭, X 버튼, ESC / 게임패드 B 로 닫기 (RequestClose)
    /// - 팝업 위에 팝업이 열리면 아래 팝업은 입력이 잠기고, 닫히면 풀린다 (스택 관리)
    /// - 닫히면 열기 전 포커스를 복원한다
    /// 팝업 GameObject는 항상 활성 상태로 두고 CanvasGroup으로 숨긴다.
    /// </summary>
    public abstract class PopupBase : MonoBehaviour
    {
        [Header("팝업 공통")]
        [SerializeField] protected CanvasGroup rootGroup;      // 딤 + 창 전체
        [SerializeField] protected RectTransform window;       // 스케일 애니메이션 대상
        [SerializeField] protected Button dimButton;           // 바깥 클릭 → 닫기
        [SerializeField] protected Button closeButton;         // 우측 상단 X
        [SerializeField] private float openDuration = 0.2f;
        [SerializeField] private float closeDuration = 0.15f;
        [SerializeField, Range(0.5f, 1f)] private float hiddenScale = 0.95f;

        // ───────── 스택 (정적) ─────────
        private static readonly List<PopupBase> stack = new List<PopupBase>();
        private static int visibleCount;

        /// <summary>팝업이 하나라도 화면에 보이는지 (애니메이션 중 포함)가 바뀔 때 호출</summary>
        public static event Action VisibilityChanged;

        public static PopupBase Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public static bool AnyVisible => visibleCount > 0;

        /// <summary>ESC / 게임패드 B. 처리할 팝업이 있으면 true</summary>
        public static bool HandleCancel()
        {
            PopupBase top = Top;
            if (top == null) return false;
            top.OnCancelPressed();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            stack.Clear();
            visibleCount = 0;
            VisibilityChanged = null;
        }

        // ───────── 인스턴스 상태 ─────────
        private Coroutine animRoutine;
        private GameObject previousSelection;
        private GameObject lastFocus;
        private bool isVisible;
        private bool isBlocked;

        /// <summary>열려 있는 상태 (닫기 애니메이션이 시작되면 false)</summary>
        public bool IsOpen { get; private set; }

        private RectTransform ScaleTarget => window != null ? window : (RectTransform)transform;

        protected virtual void Awake()
        {
            if (rootGroup == null) rootGroup = GetComponent<CanvasGroup>();
            if (dimButton != null) dimButton.onClick.AddListener(RequestClose);
            if (closeButton != null) closeButton.onClick.AddListener(RequestClose);

            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            ScaleTarget.localScale = Vector3.one * hiddenScale;
        }

        protected virtual void OnDestroy()
        {
            if (IsOpen) stack.Remove(this);
            if (isVisible) { isVisible = false; visibleCount = Mathf.Max(0, visibleCount - 1); }
        }

        // ───────── 열기 / 닫기 ─────────
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            PopupBase below = Top;
            if (below != null) below.SetBlocked(true);
            stack.Add(this);

            EventSystem es = EventSystem.current;
            previousSelection = es != null ? es.currentSelectedGameObject : null;
            lastFocus = null;
            isBlocked = false;

            if (!isVisible) { isVisible = true; visibleCount++; }
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = false; // 애니메이션이 끝나면 켠다

            OnOpening();
            VisibilityChanged?.Invoke();
            StartAnim(Animate(1f, 1f, openDuration, FinishOpen));
        }

        /// <param name="restoreSelection">닫힌 뒤 열기 전 포커스를 복원할지</param>
        public void Close(bool restoreSelection = true)
        {
            if (!IsOpen) return;
            IsOpen = false;
            stack.Remove(this);

            PopupBase below = Top;
            if (below != null) below.SetBlocked(false);

            rootGroup.interactable = false;
            OnClosing();
            StartAnim(Animate(0f, hiddenScale, closeDuration, () => FinishClose(restoreSelection)));
        }

        /// <summary>바깥 클릭 / X / ESC / B 로 닫으려 할 때. 팝업별로 override 가능</summary>
        public virtual void RequestClose() => Close();

        protected virtual void OnCancelPressed() => RequestClose();

        // ───────── 하위 클래스 훅 ─────────
        protected virtual void OnOpening() { }
        protected virtual void OnOpened() { }
        protected virtual void OnClosing() { }
        protected virtual void OnClosed() { }

        /// <summary>팝업이 열렸을 때 처음 포커스를 받을 대상</summary>
        protected abstract Selectable GetInitialSelection();

        // ───────── 포커스 ─────────
        /// <summary>포커스를 잃었거나 사용할 수 없는 대상(잠긴 슬롯 등)에 있으면 팝업 안으로 되돌린다</summary>
        public void RecoverFocus()
        {
            if (!IsOpen || isBlocked || animRoutine != null) return;
            EventSystem es = EventSystem.current;
            if (es == null) return;

            GameObject cur = es.currentSelectedGameObject;
            if (cur != null && IsValidFocus(cur)) { lastFocus = cur; return; }

            GameObject target = lastFocus != null && IsValidFocus(lastFocus) ? lastFocus : null;
            if (target == null)
            {
                Selectable first = GetInitialSelection();
                target = first != null ? first.gameObject : null;
            }
            Select(target, true);
        }

        private bool IsValidFocus(GameObject go)
        {
            if (!go.activeInHierarchy || !go.transform.IsChildOf(transform)) return false;
            return !go.TryGetComponent(out Selectable s) || s.interactable;
        }

        protected static void Select(GameObject go, bool silent)
        {
            EventSystem es = EventSystem.current;
            if (es == null || go == null || es.currentSelectedGameObject == go) return;
            if (silent && go.TryGetComponent(out MenuButtonFX fx)) fx.SuppressNextSelectSound();
            es.SetSelectedGameObject(go);
        }

        // ───────── 내부 ─────────
        private void SetBlocked(bool blocked)
        {
            isBlocked = blocked;
            ApplyInteractable();
        }

        private void ApplyInteractable() => rootGroup.interactable = IsOpen && !isBlocked && animRoutine == null;

        private void StartAnim(IEnumerator routine)
        {
            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(routine);
        }

        private IEnumerator Animate(float toAlpha, float toScale, float duration, Action onDone)
        {
            RectTransform target = ScaleTarget;
            float fromAlpha = rootGroup.alpha;
            float fromScale = target.localScale.x;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                rootGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, e);
                float s = Mathf.Lerp(fromScale, toScale, e);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rootGroup.alpha = toAlpha;
            target.localScale = new Vector3(toScale, toScale, 1f);
            animRoutine = null;
            onDone?.Invoke();
        }

        private void FinishOpen()
        {
            ApplyInteractable();
            Selectable first = GetInitialSelection();
            Select(first != null ? first.gameObject : null, true);
            OnOpened();
        }

        private void FinishClose(bool restoreSelection)
        {
            rootGroup.blocksRaycasts = false;
            if (isVisible) { isVisible = false; visibleCount = Mathf.Max(0, visibleCount - 1); }
            OnClosed();
            VisibilityChanged?.Invoke();

            GameObject prev = previousSelection;
            previousSelection = null;
            if (restoreSelection && prev != null && prev.activeInHierarchy) Select(prev, true);
        }
    }
}
