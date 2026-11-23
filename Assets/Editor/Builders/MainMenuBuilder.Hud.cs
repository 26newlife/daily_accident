using System.Collections.Generic;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 공통 HUD (GameHUD 프리팹) 빌더.
    /// 모든 시나리오가 같은 HUD 레이아웃을 쓰도록, HUD 계층을 한 곳(BuildHudHierarchy)에서 만들어 프리팹으로 저장한다.
    ///   · 전체 빌드(Build Apartment Scene)와 기존 씬 제자리 패치(Tools > GameUI > HUD > Rebuild HUD In Place)가 같은 코드를 쓴다.
    ///   · 프리팹 안에는 씬 오브젝트 참조를 저장할 수 없으므로 interactor/vitals/inventory/objectives 는 씬 인스턴스에서 연결한다
    ///     (비어 있으면 HUD 가 Awake 에서 직접 찾는다).
    /// 화면 영역 (기준 1920x1080, SafeAreaFitter 안쪽 / 아래 y 는 화면 아래에서 위로):
    ///   우측 상단  SurvivalStats          앵커 우상단, (-40,-40), 폭 440, 높이는 행 수에 따라 자동 (세로 정렬, 간격 10)
    ///   상단 중앙  ObjectiveBanner        (0,-40) 820x64        (시나리오 큰 팝업 WarHud.AlertPopup 은 (0,-140) 그 아래)
    ///   하단 중앙  고정 슬롯 (서로 겹치지 않음): Inventory y=40~112 / Prompt 124~182 / HintToast 192~256 / Message 266~326 / Subtitle 336~432
    ///   중앙       Crosshair, HoldBar(0,-50), Scent(0,+120, 폭 560 — 좌측 J 패널과 안 겹침)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        public const string HudPrefabPath = "Assets/Prefabs/UI/GameHUD.prefab";

        // 하단 중앙 슬롯 (바닥에서 위로, 피벗 아래)
        private const float HudInvY = 40f, HudInvH = 72f;
        private const float HudPromptY = 124f, HudPromptH = 58f;
        private const float HudToastY = 192f, HudToastH = 64f;
        private const float HudMessageY = 266f, HudMessageH = 60f;
        private const float HudSubtitleY = 336f, HudSubtitleH = 96f;

        /// <summary>HUD 계층을 만든다 (parent 아래 "HUD"). 씬 참조는 연결하지 않는다</summary>
        private static GameObject BuildHudHierarchy(Transform parent, GenAssets g, TMP_FontAsset font)
        {
            var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(HUD), typeof(SafeAreaFitter));
            hudGo.transform.SetParent(parent, false);
            Stretch((RectTransform)hudGo.transform, 0);
            Transform hud = hudGo.transform;
            var hudSo = SO(hudGo.GetComponent<HUD>());

            // ── 중앙: 크로스헤어 / 길게 누르기 / 감각 묘사 ──
            Image crosshair = CreateImage("Crosshair", hud, g.radial, Image.Type.Simple, false);
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));

            var holdGo = new GameObject("HoldBar", typeof(RectTransform), typeof(CanvasGroup));
            holdGo.transform.SetParent(hud, false);
            Place((RectTransform)holdGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(260, 10));
            Image holdBg = CreateImage("Bg", holdGo.transform, g.white, Image.Type.Simple, false);
            Stretch(holdBg.rectTransform, 0);
            holdBg.color = new Color(0, 0, 0, 0.6f);
            Image holdFill = CreateImage("Fill", holdGo.transform, g.white, Image.Type.Filled, false);
            Stretch(holdFill.rectTransform, 0);
            holdFill.fillMethod = Image.FillMethod.Horizontal; holdFill.fillOrigin = 0; holdFill.fillAmount = 0f;
            holdFill.color = Accent;

            var scentGo = new GameObject("Scent", typeof(RectTransform), typeof(CanvasGroup));
            scentGo.transform.SetParent(hud, false);
            Place((RectTransform)scentGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(560, 80));
            TMP_Text scentText = GameText("Text", scentGo.transform, string.Empty, font, 28, TextAlignmentOptions.Center, new Color(0.96f, 0.88f, 0.42f), true);
            scentText.fontStyle = FontStyles.Italic;
            Stretch(scentText.rectTransform, 0);

            // ── 우측 상단: 생존 상태 (세로 정렬 행) ──
            var statsGo = new GameObject("SurvivalStats", typeof(RectTransform), typeof(CanvasGroup), typeof(SurvivalStatsPanel), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            statsGo.transform.SetParent(hud, false);
            var statsRt = (RectTransform)statsGo.transform;
            Place(statsRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(400, 100));
            Image statsBg = CreateImage("Bg", statsGo.transform, g.panel, Image.Type.Sliced, false);
            statsBg.color = new Color(0.03f, 0.05f, 0.08f, 0.62f);
            Stretch(statsBg.rectTransform, 0);
            statsBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            statsBg.transform.SetAsFirstSibling();
            var vlg = statsGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 16, 18);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var csf = statsGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            TMP_Text statsTitle = GameText("Title", statsGo.transform, "생존 상태", font, 24, TextAlignmentOptions.MidlineLeft, TextDim);
            statsTitle.fontStyle = FontStyles.Bold; statsTitle.characterSpacing = 2f;
            statsTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            // 행 견본: [라벨][막대][수치]
            var rowGo = new GameObject("RowTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(statsGo.transform, false);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            rowGo.GetComponent<LayoutElement>().preferredHeight = 34f;
            TMP_Text rowLabel = GameText("Label", rowGo.transform, "라벨", font, 24, TextAlignmentOptions.MidlineLeft, TextMain);
            rowLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 104f;
            Image bar = CreateImage("Bar", rowGo.transform, g.white, Image.Type.Simple, false);
            bar.color = new Color(0, 0, 0, 0.55f);
            var barLe = bar.gameObject.AddComponent<LayoutElement>(); barLe.flexibleWidth = 1f; barLe.preferredHeight = 14f; barLe.minWidth = 80f;
            Image barFill = CreateImage("Fill", bar.transform, g.white, Image.Type.Filled, false);
            Stretch(barFill.rectTransform, 0);
            barFill.fillMethod = Image.FillMethod.Horizontal; barFill.fillOrigin = 0; barFill.fillAmount = 1f;
            TMP_Text rowValue = GameText("Value", rowGo.transform, "0", font, 24, TextAlignmentOptions.MidlineRight, TextMain);
            rowValue.gameObject.AddComponent<LayoutElement>().preferredWidth = 56f;
            rowGo.SetActive(false);

            // 줄 견본
            TMP_Text lineT = GameText("LineTemplate", statsGo.transform, "줄", font, 22, TextAlignmentOptions.MidlineLeft, TextDim, true);
            lineT.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
            lineT.gameObject.SetActive(false);

            var statsSo = SO(statsGo.GetComponent<SurvivalStatsPanel>());
            statsSo.FindProperty("group").objectReferenceValue = statsGo.GetComponent<CanvasGroup>();
            statsSo.FindProperty("content").objectReferenceValue = statsRt;
            statsSo.FindProperty("titleText").objectReferenceValue = statsTitle;
            statsSo.FindProperty("rowTemplate").objectReferenceValue = rowGo.transform;
            statsSo.FindProperty("lineTemplate").objectReferenceValue = lineT;
            statsSo.FindProperty("barTemplateFill").objectReferenceValue = barFill;
            statsSo.ApplyModifiedPropertiesWithoutUndo();
            statsGo.GetComponent<CanvasGroup>().alpha = 0f;
            statsGo.GetComponent<CanvasGroup>().blocksRaycasts = false;

            // ── 상단 중앙: 새 목표 알림 ──
            CanvasGroup banner = GroupWithPanel("ObjectiveBanner", hud, g, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(640, 72), WithA(PanelDark, 0.85f));
            TMP_Text bannerText = GameText("Text", banner.transform, string.Empty, font, 32, TextAlignmentOptions.Center, TextMain);
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.enableAutoSizing = true; bannerText.fontSizeMin = 22f; bannerText.fontSizeMax = 32f;   // 긴 목표 이름도 폭 안에서 줄어든다
            bannerText.textWrappingMode = TextWrappingModes.Normal; bannerText.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(bannerText.rectTransform, 8);
            banner.alpha = 0f; banner.blocksRaycasts = false;

            // ── 하단 중앙 고정 슬롯 ──
            const int slotCount = 8;
            const float slotSize = HudInvH, slotGap = 8f;
            var invGo = new GameObject("InventoryBar", typeof(RectTransform));
            invGo.transform.SetParent(hud, false);
            Place((RectTransform)invGo.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HudInvY), new Vector2(slotCount * (slotSize + slotGap), slotSize));
            var slotViews = new InventorySlotView[slotCount];
            for (int i = 0; i < slotCount; i++)
                slotViews[i] = BuildInventorySlot(invGo.transform, i, (i - (slotCount - 1) * 0.5f) * (slotSize + slotGap), slotSize, g, font);

            CanvasGroup promptGroup = GroupWithPanel("Prompt", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HudPromptY), new Vector2(620, HudPromptH), new Color(0, 0, 0, 0.55f));
            TMP_Text promptText = GameText("Text", promptGroup.transform, "[E] 상호작용", font, 30, TextAlignmentOptions.Center, TextMain);
            Stretch(promptText.rectTransform, 6);
            promptGroup.alpha = 0f;

            CanvasGroup toast = GroupWithPanel("HintToast", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HudToastY), new Vector2(1000, HudToastH), new Color(0.02f, 0.05f, 0.09f, 0.7f));
            TMP_Text toastText = GameText("Text", toast.transform, string.Empty, font, 28, TextAlignmentOptions.Center, TextMain, true);
            Stretch(toastText.rectTransform, 8);
            toast.alpha = 0f; toast.blocksRaycasts = false; toast.interactable = false;

            CanvasGroup msgGroup = GroupWithPanel("Message", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HudMessageY), new Vector2(900, HudMessageH), new Color(0, 0, 0, 0.6f));
            TMP_Text msgText = GameText("Text", msgGroup.transform, string.Empty, font, 30, TextAlignmentOptions.Center, TextMain);
            Stretch(msgText.rectTransform, 6);
            msgGroup.alpha = 0f;

            CanvasGroup subGroup = GroupWithPanel("Subtitle", hud, g, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HudSubtitleY), new Vector2(1300, HudSubtitleH), new Color(0, 0, 0, 0.65f));
            TMP_Text subText = GameText("Text", subGroup.transform, string.Empty, font, 30, TextAlignmentOptions.Center, TextMain, true);
            Stretch(subText.rectTransform, 10);
            var subDisplay = subGroup.gameObject.AddComponent<SubtitleDisplay>();
            var subSo = SO(subDisplay);
            subSo.FindProperty("group").objectReferenceValue = subGroup;
            subSo.FindProperty("text").objectReferenceValue = subText;
            subSo.ApplyModifiedPropertiesWithoutUndo();

            // ── HUD 컴포넌트 연결 (프리팹 안의 것만) ──
            hudSo.FindProperty("crosshair").objectReferenceValue = crosshair;
            hudSo.FindProperty("promptGroup").objectReferenceValue = promptGroup;
            hudSo.FindProperty("promptText").objectReferenceValue = promptText;
            hudSo.FindProperty("bannerGroup").objectReferenceValue = banner;
            hudSo.FindProperty("bannerText").objectReferenceValue = bannerText;
            hudSo.FindProperty("stats").objectReferenceValue = statsGo.GetComponent<SurvivalStatsPanel>();
            SetObjectArray(hudSo.FindProperty("slotViews"), slotViews);
            hudSo.FindProperty("messageGroup").objectReferenceValue = msgGroup;
            hudSo.FindProperty("messageText").objectReferenceValue = msgText;
            hudSo.FindProperty("holdGroup").objectReferenceValue = holdGo.GetComponent<CanvasGroup>();
            hudSo.FindProperty("holdFill").objectReferenceValue = holdFill;
            hudSo.FindProperty("scentGroup").objectReferenceValue = scentGo.GetComponent<CanvasGroup>();
            hudSo.FindProperty("scentText").objectReferenceValue = scentText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            return hudGo;
        }

        /// <summary>HUD 프리팹(GameHUD)을 만들어 저장한다. 이미 있으면 같은 GUID 로 덮어쓴다</summary>
        private static GameObject BuildHudPrefab(GenAssets g, TMP_FontAsset font)
        {
            EnsureFolder("Assets/Prefabs/UI");
            var temp = new GameObject("HudTemp", typeof(RectTransform));
            try
            {
                GameObject hud = BuildHudHierarchy(temp.transform, g, font);
                hud.name = "GameHUD";
                return PrefabUtility.SaveAsPrefabAsset(hud, HudPrefabPath);
            }
            finally { Object.DestroyImmediate(temp); }
        }

        /// <summary>HUD 프리팹을 만들고 캔버스 아래에 인스턴스를 놓은 뒤 씬 참조를 연결한다 (인스턴스 이름 "HUD")</summary>
        private static GameObject CreateHudInstance(Transform canvas, GenAssets g, TMP_FontAsset font, PlayerInteractor interactor, PlayerVitals vitals, InventorySystem inventory, ObjectiveSystem objectives, int siblingIndex = -1)
        {
            GameObject prefab = BuildHudPrefab(g, font);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            inst.name = "HUD";
            if (siblingIndex >= 0) inst.transform.SetSiblingIndex(siblingIndex);
            var so = SO(inst.GetComponent<HUD>());
            so.FindProperty("interactor").objectReferenceValue = interactor;
            so.FindProperty("vitals").objectReferenceValue = vitals;
            so.FindProperty("inventory").objectReferenceValue = inventory;
            so.FindProperty("objectives").objectReferenceValue = objectives;
            so.ApplyModifiedPropertiesWithoutUndo();
            return inst;
        }

        // ═════════════════════════ 기존 씬 제자리 패치 ═════════════════════════
        [MenuItem("Tools/GameUI/HUD/Rebuild HUD In Place (GameScene + Settings)")]
        public static void RebuildHudInPlaceMenu()
        {
            if (!EditorUtility.DisplayDialog("HUD 제자리 재구성", "GameScene 의 HUD 를 공통 HUD 프리팹(GameHUD)으로 교체하고, 설정 팝업(GameScene·MainMenu)에 '새 목표 알림' 토글을 추가합니다.\n다른 UI 와 시스템은 건드리지 않습니다.", "실행", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RebuildHudInPlace();
        }

        public static void RebuildHudInPlace()
        {
            GenAssets g = GenerateAssets();
            TMP_FontAsset font = EnsureKoreanFont();

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameObject canvasGo = GameObject.Find("Gameplay/GameCanvas");
            if (canvasGo == null) { Debug.LogError("[HudRebuild] Gameplay/GameCanvas 를 찾지 못했습니다."); return; }
            Transform ct = canvasGo.transform;

            Transform oldHud = ct.Find("HUD"), oldToast = ct.Find("HintToast");
            int index = oldHud != null ? oldHud.GetSiblingIndex() : ct.childCount;

            // 옛 HUD 계층을 가리키던 바깥 참조(역참조) 점검 — 교체 전에 기록
            var outside = new List<string>();
            var inHud = new HashSet<Object>();
            foreach (Transform t in new[] { oldHud, oldToast })
                if (t != null) foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) { inHud.Add(c.gameObject); foreach (Component comp in c.GetComponents<Component>()) if (comp != null) inHud.Add(comp); }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (m == null || (oldHud != null && m.transform.IsChildOf(oldHud)) || (oldToast != null && m.transform.IsChildOf(oldToast))) continue;
                    var so = new SerializedObject(m); var it = so.GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null && inHud.Contains(it.objectReferenceValue))
                            outside.Add($"{m.GetType().Name}.{it.propertyPath} → {it.objectReferenceValue.name}");
                }
            foreach (string o in outside) Debug.Log("[HudRebuild] 옛 HUD 를 가리키던 참조: " + o);

            var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            var interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
            var vitals = player != null ? player.GetComponent<PlayerVitals>() : null;
            var inventory = player != null ? player.GetComponent<InventorySystem>() : null;
            var objectives = Object.FindFirstObjectByType<ObjectiveSystem>(FindObjectsInactive.Include);

            if (oldHud != null) Object.DestroyImmediate(oldHud.gameObject);
            if (oldToast != null) Object.DestroyImmediate(oldToast.gameObject);
            GameObject hud = CreateHudInstance(ct, g, font, interactor, vitals, inventory, objectives, index);

            // 힌트 시스템이 새 토스트를 쓰도록 다시 연결
            var hint = Object.FindFirstObjectByType<HintSystem>(FindObjectsInactive.Include);
            if (hint != null)
            {
                var hso = SO(hint);
                hso.FindProperty("toastGroup").objectReferenceValue = hud.transform.Find("HintToast").GetComponent<CanvasGroup>();
                hso.FindProperty("toastText").objectReferenceValue = hud.transform.Find("HintToast/Text").GetComponent<TMP_Text>();
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 캔버스 스케일: Expand (항상 1920x1080 이상)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // J 패널: 좌측 상단 고정 (하단 슬롯·상단 중앙 알림과 겹치지 않는 영역)
            Transform detail = ct.Find("ObjectiveDetail");
            if (detail != null) ApplyObjectiveDetailLayout((RectTransform)detail);
            AddObjectiveNoticeToggle(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Scene menu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            AddObjectiveNoticeToggle(menu);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HudRebuild] 완료: HUD 프리팹 {HudPrefabPath}, 설정 토글 추가. 역참조 {outside.Count}건.");
        }

        /// <summary>J 패널 배치: 좌측 상단 (40,-40), 460x560. 제목·본문 영역도 폭에 맞춘다 (전체 빌드와 제자리 패치가 공유)</summary>
        private static void ApplyObjectiveDetailLayout(RectTransform detail)
        {
            Place(detail, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(460, 560));
            Transform title = detail.Find("Title"), body = detail.Find("Body");
            if (title != null) Place((RectTransform)title, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -20), new Vector2(404, 50));
            if (body != null) Place((RectTransform)body, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -80), new Vector2(404, 460));
        }

        /// <summary>설정 팝업 '조작' 페이지에 '새 목표 알림' 토글 행을 추가한다 (기존 '상호작용 하이라이트' 행을 복제)</summary>
        private static void AddObjectiveNoticeToggle(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (SettingsPanel panel in root.GetComponentsInChildren<SettingsPanel>(true))
                {
                    var so = SO(panel);
                    SerializedProperty tp = so.FindProperty("objectiveNoticeToggle");
                    var hl = so.FindProperty("highlightToggle").objectReferenceValue as Toggle;
                    if (tp == null || hl == null || tp.objectReferenceValue != null) continue;
                    Transform row = hl.transform.parent;
                    var copy = Object.Instantiate(row.gameObject, row.parent, false);
                    copy.name = "Row_6";
                    var rt = (RectTransform)copy.transform;
                    rt.anchoredPosition = ((RectTransform)row).anchoredPosition + new Vector2(0, -74f);
                    var label = copy.transform.Find("Label") != null ? copy.transform.Find("Label").GetComponent<TMP_Text>() : null;
                    if (label != null) label.text = "새 목표 알림";
                    Transform tg = copy.transform.Find(hl.gameObject.name);
                    if (tg == null) tg = copy.GetComponentInChildren<Toggle>(true).transform;
                    tg.name = "ObjectiveNoticeToggle";
                    tp.objectReferenceValue = tg.GetComponent<Toggle>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[HudRebuild] '{scene.name}' 설정 팝업에 새 목표 알림 토글 추가 ({panel.name})");
                }
        }
    }
}
