using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// 메뉴 버튼의 Hover / Focus / Press / Disabled 연출.
    /// - 마우스 호버 시 해당 버튼을 EventSystem 선택 상태로 만들어 키보드/게임패드 포커스와 하나로 통일한다.
    ///   (즉 키보드·게임패드 포커스도 호버와 동일한 효과가 나온다)
    /// - Hover: 테두리 밝아짐 + 글로우 증가 + 1.03배 확대 + 패널 밝아짐 + 왼쪽 accent bar 슬라이드 (0.15초)
    /// - Press: 0.97배 축소 후 복귀
    /// - Disabled(Selectable.interactable = false): 회색 톤, 글로우 없음, 호버/클릭/포커스 무반응.
    ///   interactable이 바뀌면 0.3초 색상 페이드를 하고, 활성화될 때는 살짝 펄스한다.
    /// - DOTween 없이 코루틴으로 보간한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public class MenuButtonFX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [Header("참조 (교체 가능한 그래픽)")]
        [SerializeField] private RectTransform scaleTarget;   // 확대/축소 대상 (비우면 자기 자신)
        [SerializeField] private Image fillImage;             // 패널 바탕
        [SerializeField] private Image borderImage;           // 테두리
        [SerializeField] private Image glowImage;             // 바깥 글로우
        [SerializeField] private Image bracketImage;          // 모서리 브래킷 장식 (선택)
        [SerializeField] private Image iconImage;             // 왼쪽 아이콘 (선택)
        [SerializeField] private Image accentBar;             // 왼쪽 하이라이트 바 (호버 시 슬라이드)
        [SerializeField] private TMP_Text label;              // 버튼 텍스트

        [Header("색상 - 기본 상태")]
        [SerializeField] private Color fillNormal = new Color(0.06f, 0.09f, 0.13f, 0.82f);
        [SerializeField] private Color borderNormal = new Color(0.35f, 0.55f, 0.75f, 0.55f);
        [SerializeField] private Color glowNormal = new Color(0.35f, 0.55f, 0.75f, 0.0f);
        [SerializeField] private Color bracketNormal = new Color(0.55f, 0.70f, 0.85f, 0.45f);
        [SerializeField] private Color labelNormal = new Color(0.86f, 0.90f, 0.95f, 1f);

        [Header("색상 - Hover/Focus 상태")]
        [SerializeField] private Color fillHighlight = new Color(0.09f, 0.14f, 0.20f, 0.90f);
        [SerializeField] private Color borderHighlight = new Color(0.55f, 0.80f, 1.00f, 1f);
        [SerializeField] private Color glowHighlight = new Color(0.40f, 0.70f, 1.00f, 0.45f);
        [SerializeField] private Color bracketHighlight = new Color(0.80f, 0.92f, 1.00f, 1f);
        [SerializeField] private Color labelHighlight = Color.white;

        [Header("색상 - 비활성 (회색 #6B7280 계열, 글로우 없음)")]
        [SerializeField] private Color fillDisabled = new Color(0.07f, 0.08f, 0.09f, 0.80f);
        [SerializeField] private Color borderDisabled = new Color(0.42f, 0.447f, 0.502f, 0.85f);
        [SerializeField] private Color glowDisabled = new Color(0.42f, 0.447f, 0.502f, 0f);
        [SerializeField] private Color bracketDisabled = new Color(0.42f, 0.447f, 0.502f, 0.35f);
        [SerializeField] private Color labelDisabled = new Color(0.42f, 0.447f, 0.502f, 1f);

        [Header("Hover / Press 연출")]
        [SerializeField] private float highlightScale = 1.03f;
        [SerializeField] private float hoverDuration = 0.15f;           // 호버 진입/해제 시간
        [SerializeField] private float pressedScale = 0.97f;
        [SerializeField] private float pressDuration = 0.08f;
        [SerializeField, Range(0.5f, 2f)] private float pressedBrightness = 1.35f; // 채움/테두리 밝기 배율
        [SerializeField] private float submitPulseDuration = 0.12f;                 // 키보드/패드 Submit 시 눌림 유지 시간
        [SerializeField] private float glowPulseSpeed = 2.2f;           // 포커스 중 글로우 숨쉬기 속도
        [SerializeField, Range(0f, 1f)] private float glowPulseAmount = 0.25f;
        [SerializeField] private float accentSlide = 14f;               // accent bar가 슬라이드해 들어오는 거리(px)

        [Header("활성/비활성 전환 연출")]
        [SerializeField] private float stateFadeDuration = 0.3f;        // 회색 ↔ 컬러 페이드 시간
        [SerializeField] private float activatePulseScale = 0.06f;      // 활성화될 때 펄스 확대량
        [SerializeField] private float activatePulseGlow = 0.4f;        // 활성화될 때 글로우 번쩍임

        [Header("사운드 (클립은 비워둬도 됨)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;

        private Selectable selectable;
        private bool isHighlighted;
        private bool isPressed;
        private bool suppressNextSelectSound;
        private bool knownInteractable = true;

        private float highlightT;   // 0 = 기본, 1 = 하이라이트
        private float pressT;       // 0 = 뗌, 1 = 눌림
        private float disabledT;    // 0 = 활성, 1 = 비활성
        private float pulseT;       // 활성화 펄스 (1 → 0)
        private Coroutine animRoutine;
        private Coroutine submitRoutine;
        private RectTransform accentRect;
        private Vector2 accentBasePos;

        public TMP_Text Label => label;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (scaleTarget == null) scaleTarget = (RectTransform)transform;
            if (accentBar != null)
            {
                accentRect = accentBar.rectTransform;
                accentBasePos = accentRect.anchoredPosition;
            }
            SyncInteractableImmediate();
        }

        private void OnEnable()
        {
            // 다시 활성화될 때 현재 선택 상태를 반영
            isHighlighted = knownInteractable && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            isPressed = false;
            highlightT = isHighlighted ? 1f : 0f;
            pressT = 0f;
            pulseT = 0f;
            SyncInteractableImmediate();
            if (isHighlighted) StartAnim();
        }

        private void OnDisable()
        {
            animRoutine = null;
            submitRoutine = null;
        }

        private void Update()
        {
            // Selectable.interactable 변경 감지 (코드/Inspector 어디서 바꿔도 연출이 따라온다)
            if (selectable == null || selectable.interactable == knownInteractable) return;
            knownInteractable = selectable.interactable;
            if (knownInteractable) pulseT = 1f;           // 활성화될 때 펄스
            else { isHighlighted = false; isPressed = false; }
            StartAnim();
        }

        /// <summary>interactable 상태를 연출 없이 즉시 반영 (씬 시작 시 복원용)</summary>
        public void SyncInteractableImmediate()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            knownInteractable = selectable == null || selectable.interactable;
            disabledT = knownInteractable ? 0f : 1f;
            pulseT = 0f;
            ApplyVisuals(0f);
        }

        /// <summary>다음 선택 시 hover 사운드를 생략 (코드로 기본 포커스를 줄 때 사용)</summary>
        public void SuppressNextSelectSound() => suppressNextSelectSound = true;

        // ───────── 포인터 ─────────
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return; // 비활성 버튼은 호버해도 무반응
            // 마우스 호버 = 선택. 키보드 포커스와 상태를 공유한다.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 선택은 유지 (키보드로 이어서 조작 가능). 눌림만 해제.
            SetPressed(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            SetPressed(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            PlayClip(clickClip);
        }

        // ───────── 선택 (키보드/게임패드) ─────────
        public void OnSelect(BaseEventData eventData)
        {
            // 비활성 버튼은 (클릭 등으로 선택되더라도) 하이라이트하지 않는다
            if (selectable != null && !selectable.interactable) return;

            isHighlighted = true;
            if (suppressNextSelectSound) suppressNextSelectSound = false;
            else PlayClip(hoverClip);
            StartAnim();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isHighlighted = false;
            isPressed = false;
            StartAnim();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsInteractable()) return;
            PlayClip(clickClip);
            if (!isActiveAndEnabled) return;
            if (submitRoutine != null) StopCoroutine(submitRoutine);
            submitRoutine = StartCoroutine(SubmitPulse());
        }

        private IEnumerator SubmitPulse()
        {
            SetPressed(true);
            yield return new WaitForSecondsRealtime(submitPulseDuration);
            SetPressed(false);
            submitRoutine = null;
        }

        // ───────── 내부 ─────────
        private bool IsInteractable() => selectable != null && selectable.IsInteractable();

        private void SetPressed(bool pressed)
        {
            if (isPressed == pressed) return;
            isPressed = pressed;
            StartAnim();
        }

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void StartAnim()
        {
            if (!isActiveAndEnabled) return;
            if (animRoutine == null) animRoutine = StartCoroutine(AnimRoutine());
        }

        /// <summary>목표 상태까지 보간. 하이라이트 중에는 글로우 펄스 때문에 계속 돈다.</summary>
        private IEnumerator AnimRoutine()
        {
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                float disabledTarget = knownInteractable ? 0f : 1f;

                highlightT = Mathf.MoveTowards(highlightT, isHighlighted ? 1f : 0f, dt / Mathf.Max(0.01f, hoverDuration));
                pressT = Mathf.MoveTowards(pressT, isPressed ? 1f : 0f, dt / Mathf.Max(0.01f, pressDuration));
                disabledT = Mathf.MoveTowards(disabledT, disabledTarget, dt / Mathf.Max(0.01f, stateFadeDuration));
                if (pulseT > 0f) pulseT = Mathf.MoveTowards(pulseT, 0f, dt / Mathf.Max(0.01f, stateFadeDuration));

                float pulse = isHighlighted ? Mathf.Sin(Time.unscaledTime * glowPulseSpeed * Mathf.PI) * 0.5f + 0.5f : 0f;
                ApplyVisuals(pulse);

                bool settled = !isHighlighted && !isPressed
                               && highlightT <= 0f && pressT <= 0f
                               && disabledT == disabledTarget && pulseT <= 0f;
                if (settled)
                {
                    ApplyVisuals(0f);
                    animRoutine = null;
                    yield break;
                }
                yield return null;
            }
        }

        private void ApplyVisuals(float pulse)
        {
            float d = Smooth(disabledT);
            float h = Smooth(highlightT) * (1f - d);   // 비활성으로 갈수록 하이라이트 효과 제거
            float p = Smooth(pressT);
            float wave = pulseT > 0f ? Mathf.Sin((1f - pulseT) * Mathf.PI) : 0f; // 활성화 펄스 0 → 1 → 0

            float scale = Mathf.Lerp(1f, highlightScale, h) * Mathf.Lerp(1f, pressedScale, p) * (1f + wave * activatePulseScale);
            if (scaleTarget != null) scaleTarget.localScale = new Vector3(scale, scale, 1f);

            float bright = Mathf.Lerp(1f, pressedBrightness, p);

            if (fillImage != null) fillImage.color = Brighten(Mix(fillNormal, fillHighlight, fillDisabled, h, d), bright);
            if (borderImage != null) borderImage.color = Brighten(Mix(borderNormal, borderHighlight, borderDisabled, h, d), bright);
            if (bracketImage != null) bracketImage.color = Mix(bracketNormal, bracketHighlight, bracketDisabled, h, d);
            if (label != null) label.color = Mix(labelNormal, labelHighlight, labelDisabled, h, d);

            if (glowImage != null)
            {
                Color g = Mix(glowNormal, glowHighlight, glowDisabled, h, d);
                g.a *= 1f - glowPulseAmount + glowPulseAmount * pulse * 2f; // 펄스로 글로우 증감
                g.a = Mathf.Clamp01(g.a * Mathf.Lerp(1f, 1.4f, p) + wave * activatePulseGlow * (1f - d));
                glowImage.color = g;
            }

            if (accentBar != null)
            {
                Color a = borderHighlight;
                a.a = h;
                accentBar.color = a;
                if (accentRect != null) accentRect.anchoredPosition = accentBasePos + new Vector2(-accentSlide * (1f - h), 0f);
            }
        }

        private static Color Mix(Color normal, Color highlight, Color disabled, float h, float d)
            => Color.Lerp(Color.Lerp(normal, highlight, h), disabled, d);

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        private static Color Brighten(Color c, float m) => new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
    }
}
