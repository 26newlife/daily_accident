using QuakeSurvival.Art;
using QuakeSurvival.Interaction;
using QuakeSurvival.Player;
using QuakeSurvival.Safety;
using QuakeSurvival.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>HUD(TextMeshPro + Noto Sans KR): 조준점, 상호작용 안내, 자막, 재난문자, 결과 화면.</summary>
    internal static class HudBuilder
    {
        public sealed class Parts
        {
            public MessageUI Messages;
            public GameOverUI GameOver;
        }

        public static Parts Build(FirstPersonController controller, Interactor interactor, OutcomeEvaluator evaluator)
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform hud = canvasGo.transform;

            var cross = UiText(hud, "Crosshair", "·", 48, Vector2.zero, new Vector2(60f, 60f), false);
            cross.color = Palette.WithAlpha(Palette.UiText, 0.8f);

            var prompt = UiText(hud, "InteractionPrompt", "", 32, new Vector2(0f, -110f), new Vector2(900f, 60f), true);
            prompt.color = Palette.UiAlert;
            var promptUi = canvasGo.AddComponent<InteractionPromptUI>();
            SetRef(promptUi, "interactor", interactor);
            SetRef(promptUi, "label", prompt);

            BuildStaminaBar(canvasGo, controller);
            MessageUI messages = BuildMessages(canvasGo);
            GameOverUI gameOver = BuildGameOver(canvasGo, controller, interactor, evaluator, messages);
            return new Parts { Messages = messages, GameOver = gameOver };
        }

        /// <summary>화면 아래 가운데 얇은 달리기 체력 막대(가득 차면 숨김).</summary>
        private static void BuildStaminaBar(GameObject canvasGo, FirstPersonController controller)
        {
            const float width = 320f;
            const float height = 8f;
            var back = UiImage(canvasGo.transform, "StaminaBar", Palette.WithAlpha(Palette.Shadow, 0.6f), new Vector2(0f, -480f), new Vector2(width, height));
            var group = back.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var fill = UiImage(back.transform, "Fill", Palette.UiText, new Vector2(-width / 2f, 0f), new Vector2(width, height));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);

            var bar = canvasGo.AddComponent<StaminaBarUI>();
            SetRef(bar, "player", controller);
            SetRef(bar, "group", group);
            SetRef(bar, "fill", fill.rectTransform);
            SetRef(bar, "fillImage", fill);
            SetColor(bar, "normalColor", Palette.WithAlpha(Palette.UiText, 0.85f));
            SetColor(bar, "exhaustedColor", Palette.AccentEmergency);
        }

        private static void SetColor(Object target, string field, Color value)
        {
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MessageUI BuildMessages(GameObject canvasGo)
        {
            Transform hud = canvasGo.transform;
            var subtitle = UiText(hud, "Subtitle", "", 30, new Vector2(0f, -400f), new Vector2(1100f, 120f), false);

            // 재난문자: 휴대폰 알림처럼 오른쪽 위, 왼쪽에 신호 빨강 띠
            var panel = UiImage(hud, "AlertPanel", Palette.UiPanel, new Vector2(600f, 390f), new Vector2(640f, 200f));
            UiImage(panel.transform, "AlertStripe", Palette.AccentEmergency, new Vector2(-314f, 0f), new Vector2(12f, 200f));
            var header = UiText(panel.transform, "AlertHeader", "긴급재난문자", 22, new Vector2(10f, 70f), new Vector2(580f, 36f), true);
            header.alignment = TextAlignmentOptions.Left;
            header.color = Palette.AccentEmergency;
            var alert = UiText(panel.transform, "AlertText", "", 24, new Vector2(10f, -18f), new Vector2(580f, 140f), false);
            alert.alignment = TextAlignmentOptions.TopLeft;
            alert.color = Palette.UiAlert;

            var messages = canvasGo.AddComponent<MessageUI>();
            SetRef(messages, "subtitle", subtitle);
            SetRef(messages, "alertPanel", panel.gameObject);
            SetRef(messages, "alertText", alert);
            return messages;
        }

        private static GameOverUI BuildGameOver(GameObject canvasGo, FirstPersonController controller,
            Interactor interactor, OutcomeEvaluator evaluator, MessageUI messages)
        {
            var bg = UiImage(canvasGo.transform, "GameOver", Palette.WithAlpha(Palette.Shadow, 0.97f), Vector2.zero, Vector2.zero);
            var rt = bg.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var group = bg.gameObject.AddComponent<CanvasGroup>();

            var title = UiText(bg.transform, "Title", "", 64, new Vector2(0f, 400f), new Vector2(1600f, 100f), true);
            title.color = Palette.AccentEmergency;
            var body = UiText(bg.transform, "Body", "", 28, new Vector2(0f, -60f), new Vector2(1500f, 800f), false);
            body.lineSpacing = 12f;

            var ui = canvasGo.AddComponent<GameOverUI>();
            SetRef(ui, "evaluator", evaluator);
            SetRef(ui, "player", controller);
            SetRef(ui, "interactor", interactor);
            SetRef(ui, "messages", messages);
            SetRef(ui, "group", group);
            SetRef(ui, "title", title);
            SetRef(ui, "body", body);
            return ui;
        }

        internal static TextMeshProUGUI UiText(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = bold ? CurrentFonts.Bold : CurrentFonts.Regular;
            t.fontSharedMaterial = bold ? CurrentFonts.BoldShadow : CurrentFonts.RegularShadow;
            t.text = text;
            t.fontSize = size;
            t.characterSpacing = 2f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Palette.UiText;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        internal static Image UiImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }
}
