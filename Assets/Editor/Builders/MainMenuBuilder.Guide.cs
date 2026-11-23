using System.IO;
using GameCore;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 가이드 시스템(힌트 토스트 / 조작 카드 / 목표 상세 / 목표 화살표 / 하이라이트)과 도어 스코프 연결 생성부.
    /// BuildGameplayUI 에서 호출된다. 힌트 문구는 Assets/Data/Hints/*.asset 에 저장되며, 이미 있으면 사용자가 수정한 내용을 보존한다.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string HintDir = "Assets/Scenarios/S03_ApartmentFire/Data/Hints";
        private const string ArrowSpritePath = "Assets/Art/UI/Generated/ArrowIcon.png";

        // ───────── 힌트 에셋 ─────────
        private static HintData EnsureHint(string file, string id, string text, HintTrigger trigger, float value = 1f, string param = "",
            bool easyOnly = false, int priority = 0, float delay = 0f, float duration = 3.5f, ScenarioEventType evType = ScenarioEventType.Toggled, bool evState = true)
            => Ensure<HintData>($"{HintDir}/{file}.asset", so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("text").stringValue = text;
                so.FindProperty("trigger").enumValueIndex = (int)trigger;
                so.FindProperty("triggerValue").floatValue = value;
                so.FindProperty("triggerParam").stringValue = param;
                so.FindProperty("scenarioType").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(ScenarioEventType)), evType);
                so.FindProperty("eventState").boolValue = evState;
                so.FindProperty("repeat").boolValue = false;
                so.FindProperty("delay").floatValue = delay;
                so.FindProperty("duration").floatValue = duration;
                so.FindProperty("priority").intValue = priority;
                so.FindProperty("easyOnly").boolValue = easyOnly;
            });

        /// <summary>
        /// 힌트 목록. 조작/시스템 설명은 Normal 이상, 상황 판단에 가까운 것은 easyOnly(Easy 전용).
        /// 위험 행동의 정답(예: "스위치를 켜지 마")은 어떤 난이도에서도 직접 쓰지 않는다.
        /// </summary>
        private static HintData[] EnsureHints()
        {
            EnsureFolder(HintDir);
            // 시작 직후 뜨던 조작 안내 힌트는 제거했다 (조작법은 일시정지 메뉴에서만 보여준다). 이전 빌드가 만든 에셋도 정리
            AssetDatabase.DeleteAsset($"{HintDir}/Hint_Move.asset");
            AssetDatabase.DeleteAsset($"{HintDir}/Hint_Aim.asset");
            var all = new[]
            {
                EnsureHint("Hint_Hold", "hint_hold", "[{interact}]를 길게 누르세요.", HintTrigger.FirstAimHold, 0f, "", false, 7),
                EnsureHint("Hint_Phone", "hint_phone", "[{phone}]으로 스마트폰을 열어 손전등과 문자를 확인하세요.", HintTrigger.AfterSeconds, 30f, "", false, 3),
                EnsureHint("Hint_Peephole", "hint_peephole", "[{interact}]로 문구멍을 통해 복도를 볼 수 있습니다.", HintTrigger.NearInteractable, 1.8f, "door_peephole", false, 4),
                EnsureHint("Hint_WindowOutside", "hint_window_outside", "창밖에서는 이웃과 구조대가 볼 수 있습니다.", HintTrigger.ScenarioEvent, 0f, "window_living;window_bedroom;window_kitchen;door_balcony", false, 2, 1f, 4f),
                EnsureHint("Hint_Inventory", "hint_inventory", "획득한 물건은 하단 슬롯에서 확인할 수 있습니다.", HintTrigger.ItemAcquiredAny, 0f, "", false, 5, 0.6f),
                EnsureHint("Hint_Extinguisher", "hint_extinguisher", "안전핀을 뽑고 불 아래쪽을 향해 분사하세요.", HintTrigger.ItemAcquiredId, 0f, "extinguisher", false, 8, 0.8f, 4f),
                EnsureHint("Hint_Vitals", "hint_vitals", "호흡기 게이지가 0이 되면 질식합니다.", HintTrigger.VitalsShown, 0f, "", false, 9, 0.5f),
                // ── Easy 전용 (상황 판단에 가까운 안내) ──
                EnsureHint("Hint_Gas", "hint_gas", "이상한 냄새가 납니다. 원인을 찾아보세요.", HintTrigger.GasSmell, 0.02f, "", true, 5, 1f),
                EnsureHint("Hint_Broadcast", "hint_broadcast", "전기 스위치와 불꽃을 조심하세요. 가스를 차단하고 환기하세요.", HintTrigger.BroadcastHeard, 8f, "", true, 6, 0f, 4.5f),
                EnsureHint("Hint_Smoke", "hint_smoke", "연기가 짙으면 [{crouch}]로 자세를 낮추고, 젖은 수건으로 코와 입을 막으세요.", HintTrigger.SmokeEntered, 0.05f, "", true, 7, 0.4f, 4.5f),
                EnsureHint("Hint_DoorHandle", "hint_door_handle", "문을 열기 전에 손등으로 손잡이 온도를 확인하세요.", HintTrigger.NearInteractable, 1.6f, "door_front", true, 4, 0.5f),
            };
            // 시나리오 3 전용 힌트(소화기·가스·방송·연기·손잡이)는 ScenarioDefinition 이 런타임에 덧붙인다. 나머지는 공용 힌트
            var scenario = new System.Collections.Generic.HashSet<string> { "hint_extinguisher", "hint_gas", "hint_broadcast", "hint_smoke", "hint_door_handle" };
            var common = new System.Collections.Generic.List<HintData>(); var mine = new System.Collections.Generic.List<HintData>();
            foreach (HintData h in all) { if (scenario.Contains(h.Id)) mine.Add(h); else common.Add(h); }
            s3Hints = mine.ToArray();
            return common.ToArray();
        }

        // ───────── 화살표 스프라이트 (절차적) ─────────
        private static Sprite EnsureArrowSprite()
        {
            if (!File.Exists(ArrowSpritePath))
            {
                EnsureFolder(GenDir);
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // 위쪽을 향한 삼각형 (밑변 폭이 아래로 갈수록 넓어짐) + 부드러운 가장자리
                        float t = y / (float)(n - 1);
                        float halfWidth = Mathf.Lerp(n * 0.5f - 2f, 0f, t);
                        float edge = Mathf.Clamp01(halfWidth - Mathf.Abs(x - n * 0.5f) + 1f);
                        px[y * n + x] = new Color(1f, 1f, 1f, edge * (y > 3 ? 1f : 0f));
                    }
                tex.SetPixels(px); tex.Apply();
                File.WriteAllBytes(ArrowSpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(ArrowSpritePath);
            }
            var importer = AssetImporter.GetAtPath(ArrowSpritePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
        }

        // ═════════════════════════ 가이드 + 도어 스코프 ═════════════════════════
        /// <summary>가이드 UI/컴포넌트를 만들고 조작 카드를 반환한다 (일시정지 메뉴의 "조작법" 버튼이 사용)</summary>
        private static ControlsGuideCard BuildGuideAndPeephole(Transform gameplay, Transform ct, GenAssets g, TMP_FontAsset font, GameObject player, Systems sys)
        {
            var input = player.GetComponent<GameInput>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var vitals = player.GetComponent<PlayerVitals>();
            var controller = player.GetComponent<PlayerController>();
            Camera cam = player.GetComponentInChildren<Camera>(true);

            // ── 힌트 토스트 (공통 HUD 프리팹 안의 고정 슬롯 — 다른 하단 UI 와 겹치지 않는다) ──
            Transform hudT = ct.Find("HUD");
            CanvasGroup toast = hudT.Find("HintToast").GetComponent<CanvasGroup>();
            TMP_Text toastText = hudT.Find("HintToast/Text").GetComponent<TMP_Text>();

            // ── 조작법 패널 (일시정지 메뉴의 "조작법" 버튼으로만 열림, X 로 닫기 전까지 유지) ──
            CanvasGroup cardGroup = GroupWithPanel("ControlsCard", ct, g, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(980, 640), new Color(0.03f, 0.06f, 0.1f, 0.96f));
            TMP_Text cardTitle = GameText("Title", cardGroup.transform, "조작법", font, 40, TextAlignmentOptions.Center, Accent);
            Place(cardTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(700, 60));
            TMP_Text cardBody = GameText("Body", cardGroup.transform, string.Empty, font, 30, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(cardBody.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(880, 480));
            cardBody.lineSpacing = 16f;

            // 오른쪽 위 X 닫기 버튼
            var xGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            xGo.transform.SetParent(cardGroup.transform, false);
            Place((RectTransform)xGo.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -34), new Vector2(56, 56));
            var xImage = xGo.GetComponent<Image>();
            xImage.color = new Color(0.55f, 0.16f, 0.16f, 0.95f);
            var xButton = xGo.GetComponent<Button>();
            xButton.targetGraphic = xImage;
            TMP_Text xLabel = GameText("X", xGo.transform, "X", font, 32, TextAlignmentOptions.Center, TextMain);
            Stretch(xLabel.rectTransform, 0);
            xLabel.raycastTarget = false;

            var card = cardGroup.gameObject.AddComponent<ControlsGuideCard>();
            var cso = SO(card);
            cso.FindProperty("panel").objectReferenceValue = cardGroup;
            cso.FindProperty("titleText").objectReferenceValue = cardTitle;
            cso.FindProperty("bodyText").objectReferenceValue = cardBody;
            cso.FindProperty("closeButton").objectReferenceValue = xButton;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // ── 목표 상세 패널 (J) ──
            CanvasGroup detail = GroupWithPanel("ObjectiveDetail", ct, g, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(460, 560), new Color(0.03f, 0.06f, 0.1f, 0.88f));
            TMP_Text detailTitle = GameText("Title", detail.transform, "현재 목표", font, 36, TextAlignmentOptions.TopLeft, Accent);
            Place(detailTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -20), new Vector2(404, 50));
            TMP_Text detailBody = GameText("Body", detail.transform, string.Empty, font, 26, TextAlignmentOptions.TopLeft, TextMain, true);
            Place(detailBody.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -80), new Vector2(404, 460));
            detail.alpha = 0f; detail.blocksRaycasts = false; detail.interactable = false;
            var detailPanel = detail.gameObject.AddComponent<ObjectiveDetailPanel>();
            var dso = SO(detailPanel);
            dso.FindProperty("input").objectReferenceValue = input;
            dso.FindProperty("objectives").objectReferenceValue = sys.objectives;
            dso.FindProperty("panel").objectReferenceValue = detail;
            dso.FindProperty("titleText").objectReferenceValue = detailTitle;
            dso.FindProperty("bodyText").objectReferenceValue = detailBody;
            dso.ApplyModifiedPropertiesWithoutUndo();

            // ── 목표 화살표 (Easy) ──
            var markerGo = new GameObject("ObjectiveMarker", typeof(RectTransform));
            markerGo.transform.SetParent(ct, false);
            Stretch((RectTransform)markerGo.transform, 0);
            Image arrow = CreateImage("Arrow", markerGo.transform, EnsureArrowSprite(), Image.Type.Simple, false);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            arrow.color = new Color(0.4f, 0.8f, 1f, 0.95f);
            TMP_Text dist = GameText("Distance", markerGo.transform, "0m", font, 26, TextAlignmentOptions.Center, TextMain);
            Place(dist.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 36));
            var marker = markerGo.AddComponent<ObjectiveMarker>();
            var mso = SO(marker);
            mso.FindProperty("objectives").objectReferenceValue = sys.objectives;
            mso.FindProperty("player").objectReferenceValue = player.transform;
            mso.FindProperty("arrow").objectReferenceValue = arrow.rectTransform;
            mso.FindProperty("distanceText").objectReferenceValue = dist;
            mso.FindProperty("canvas").objectReferenceValue = ct.GetComponent<Canvas>();
            string[,] map =
            {
                { "act1_alarm", "alarm_clock" }, { "act1_wash", "wash_face" }, { "act1_drink", "drink_water" }, { "act1_fuse", "fuse_cock" },
                { "act1_main_valve", "gas_valve_main" }, { "act1_ventilate", "window_living" }, { "act1_multitap", "multitap_plug" },
                { "act2_extinguisher", "pickup_extinguisher" }, { "act2_towel", "sink_wet_towel" }, { "act2_bottle", "pickup_water_bottle" },
                { "act2_handle", "door_handle_check" },
            };
            SerializedProperty entries = mso.FindProperty("entries");
            int count = 0;
            entries.arraySize = map.GetLength(0);
            for (int i = 0; i < map.GetLength(0); i++)
            {
                Transform t = FindInteractableAnywhere(map[i, 1]);
                if (t == null) continue;
                SerializedProperty e = entries.GetArrayElementAtIndex(count++);
                e.FindPropertyRelative("objectiveId").stringValue = map[i, 0];
                e.FindPropertyRelative("target").objectReferenceValue = t;
            }
            entries.arraySize = count;
            mso.ApplyModifiedPropertiesWithoutUndo();

            // ── 힌트 시스템 / 하이라이트 / 문구멍 컨트롤러 ──
            var guideRoot = new GameObject("Guide");
            guideRoot.transform.SetParent(gameplay, false);
            var hintSystem = guideRoot.AddComponent<HintSystem>();
            var hso = SO(hintSystem);
            hso.FindProperty("interactor").objectReferenceValue = interactor;
            hso.FindProperty("input").objectReferenceValue = input;
            hso.FindProperty("vitals").objectReferenceValue = vitals;
            hso.FindProperty("player").objectReferenceValue = player.transform;
            hso.FindProperty("toastGroup").objectReferenceValue = toast;
            hso.FindProperty("toastText").objectReferenceValue = toastText;
            hso.FindProperty("controlsCard").objectReferenceValue = card;
            SetObjectArray(hso.FindProperty("hints"), EnsureHints());
            hso.ApplyModifiedPropertiesWithoutUndo();

            var highlight = player.AddComponent<InteractableHighlight>();
            var lso = SO(highlight);
            lso.FindProperty("interactor").objectReferenceValue = interactor;
            lso.ApplyModifiedPropertiesWithoutUndo();

            var peepGo = new GameObject("Peephole");
            peepGo.transform.SetParent(gameplay, false);
            var peep = peepGo.AddComponent<PeepholeCameraController>();
            var pso = SO(peep);
            pso.FindProperty("player").objectReferenceValue = controller;
            pso.FindProperty("input").objectReferenceValue = input;
            pso.FindProperty("viewCamera").objectReferenceValue = cam;
            pso.FindProperty("canvas").objectReferenceValue = ct.GetComponent<Canvas>();
            // 복도 NPC 실루엣 (Interior 빌더가 만든 비활성 오브젝트) — Act 3 에서 ScenarioFlags 키를 true 로 하면 문구멍에 보인다
            Transform npcRoot = lvInteract != null && lvInteract.parent != null ? lvInteract.parent.Find("Interior/Corridor/CorridorNpcs") : null;
            if (npcRoot != null && npcRoot.childCount > 0)
            {
                SerializedProperty npcs = pso.FindProperty("corridorNpcs");
                npcs.arraySize = npcRoot.childCount;
                for (int i = 0; i < npcRoot.childCount; i++)
                {
                    Transform c = npcRoot.GetChild(i);
                    SerializedProperty e = npcs.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("silhouette").objectReferenceValue = c.gameObject;
                    e.FindPropertyRelative("flagKey").stringValue = "act3_npc_corridor_" + c.name.Replace("Npc_", "").ToLowerInvariant();
                }
            }
            pso.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private static Transform FindInteractableAnywhere(string id)
        {
            if (lvInteract != null)
            {
                Transform t = lvInteract.Find(id);
                if (t != null) return t;
                foreach (Transform c in lvInteract) { Transform d = c.Find(id); if (d != null) return d; }   // 문에 붙은 하위 상호작용 (door_handle_check)
                Transform hinge = lvInteract.Find("door_front/Hinge/" + id);
                if (hinge != null) return hinge;
            }
            return null;
        }
    }
}
