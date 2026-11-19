using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GameCore;
using GameData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 시나리오 선택 / 확인창 / 설정 팝업 / 시나리오 데이터 / 한글 폰트 / AudioMixer 생성부.
    /// (MainMenuBuilder.cs와 하나의 partial 클래스)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        // ───────── 경로 ─────────
        private const string DataDir = "Assets/Core/Data";
        private const string ScenarioDir = "Assets/Core/Data/Scenarios";
        private const string DatabasePath = "Assets/Core/Data/ScenarioDatabase.asset";
        private const string SlotPrefabPath = "Assets/Prefabs/UI/ScenarioSlot.prefab";
        private const string ConfirmPrefabPath = "Assets/Prefabs/UI/ConfirmDialog.prefab";
        private const string KoreanTtfPath = "Assets/Fonts/NotoSansKR.ttf";
        private const string KoreanFontPath = "Assets/Fonts/NotoSansKR SDF.asset";
        private const string MixerPath = "Assets/Resources/GameAudioMixer.mixer";

        // 런타임에 동적으로 채워지는 한글 문자열 (폰트 아틀라스 미리 채우기용)
        private const string RuntimeKoreanStrings =
            "시나리오를 선택하세요 SELECTED 을를 선택하시겠습니까 예 아니요 선택됨 준비 중 설정 조작 소리 화면 마우스 감도 게임패드 Y축 반전 " +
            "마스터 볼륨 배경음악 효과음 음성 내레이션 해상도 전체화면 창 모드 테두리 없는 그래픽 품질 수직 동기화 프레임 제한 무제한 밝기 " +
            "기본값으로 초기화 적용 닫기 전시상황 자연재해 아파트 화재";

        // ═════════════════════════ 한글 폰트 ═════════════════════════
        /// <summary>
        /// NotoSansKR.ttf로 Dynamic Atlas TMP Font Asset을 만들고, 기존 폰트(LiberationSans)와 TMP 전역 폴백에 등록한다.
        /// 영문은 기존 폰트 그대로, 한글만 이 폰트로 표시된다.
        /// </summary>
        private static TMP_FontAsset EnsureKoreanFont()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (fa == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(KoreanTtfPath);
                if (source == null)
                {
                    Debug.LogWarning($"[MainMenuBuilder] {KoreanTtfPath} 를 찾지 못해 한글 폰트를 만들 수 없습니다.");
                    return null;
                }

                fa = TMP_FontAsset.CreateFontAsset(source, 72, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(fa, KoreanFontPath);

                if (fa.material != null)
                {
                    fa.material.name = "NotoSansKR SDF Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                foreach (Texture2D tex in fa.atlasTextures)
                {
                    if (tex == null) continue;
                    tex.name = "NotoSansKR SDF Atlas";
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
                AssetDatabase.SaveAssets();
            }

            // 기본 폰트의 폴백 + TMP 전역 폴백에 등록
            TMP_FontAsset baseFont = FindFont();
            if (baseFont != null && baseFont != fa)
            {
                baseFont.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                if (!baseFont.fallbackFontAssetTable.Contains(fa))
                {
                    baseFont.fallbackFontAssetTable.Add(fa);
                    EditorUtility.SetDirty(baseFont);
                }
            }
            if (TMP_Settings.instance != null && TMP_Settings.fallbackFontAssets != null && !TMP_Settings.fallbackFontAssets.Contains(fa))
            {
                TMP_Settings.fallbackFontAssets.Add(fa);
                EditorUtility.SetDirty(TMP_Settings.instance);
            }
            AssetDatabase.SaveAssets();
            return fa;
        }

        /// <summary>UI에 쓰이는 한글을 미리 아틀라스에 채워 첫 표시 때 끊김이 없게 한다 (이후 새 글자는 자동 추가)</summary>
        private static void PrewarmKoreanGlyphs(TMP_FontAsset korean)
        {
            if (korean == null) return;
            var chars = new HashSet<char>(RuntimeKoreanStrings);
            foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (char c in t.text) chars.Add(c);

            var sb = new StringBuilder();
            foreach (char c in chars) if (c > 127) sb.Append(c);
            if (sb.Length == 0) return;

            korean.TryAddCharacters(sb.ToString());
            EditorUtility.SetDirty(korean);
            foreach (Texture2D tex in korean.atlasTextures) if (tex != null) EditorUtility.SetDirty(tex);
            AssetDatabase.SaveAssets();
        }

        // ═════════════════════════ 시나리오 데이터 ═════════════════════════
        private static ScenarioDatabase EnsureScenarioData(GenAssets g)
        {
            EnsureFolder(DataDir);
            EnsureFolder(ScenarioDir);

            // (id, 이름, 설명, 해금 여부)
            var defs = new (string id, string name, string desc, bool unlocked)[]
            {
                ("war", "전시상황", "포화 속 도시에서 살아남아라", true),
                ("disaster", "자연재해", "지진과 홍수 속에서 대피하라", true),
                ("apartment_fire", "아파트 화재", "고층 아파트 화재에서 탈출하라", true),
                ("locked_04", "???", "준비 중 (COMING SOON)", false),
                ("locked_05", "???", "준비 중 (COMING SOON)", false),
                ("locked_06", "???", "준비 중 (COMING SOON)", false),
                ("locked_07", "???", "준비 중 (COMING SOON)", false),
                ("locked_08", "???", "준비 중 (COMING SOON)", false),
                ("locked_09", "???", "준비 중 (COMING SOON)", false),
                ("locked_10", "???", "준비 중 (COMING SOON)", false),
            };

            var assets = new List<ScenarioData>();
            for (int i = 0; i < defs.Length; i++)
            {
                string path = $"{ScenarioDir}/Scenario_{i + 1:00}.asset";
                var data = AssetDatabase.LoadAssetAtPath<ScenarioData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<ScenarioData>();
                    AssetDatabase.CreateAsset(data, path);
                    var so = new SerializedObject(data);
                    so.FindProperty("id").stringValue = defs[i].id;
                    so.FindProperty("displayName").stringValue = defs[i].name;
                    so.FindProperty("description").stringValue = defs[i].desc;
                    so.FindProperty("isUnlocked").boolValue = defs[i].unlocked;
                    // 현재 배경 이미지는 3번(아파트 화재)용. 1·2번은 비워두면 기존 배경이 유지된다
                    if (defs[i].id == "apartment_fire") so.FindProperty("backgroundSprite").objectReferenceValue = g.background;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(data);
                }
                assets.Add(data);
            }

            var db = AssetDatabase.LoadAssetAtPath<ScenarioDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ScenarioDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            var dbSo = new SerializedObject(db);
            SerializedProperty list = dbSo.FindProperty("scenarios");
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (ScenarioData d in assets)
            {
                if (existing.Contains(d)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = d;
            }
            dbSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        // ═════════════════════════ AudioMixer ═════════════════════════
        /// <summary>
        /// Resources/GameAudioMixer.mixer 를 만든다 (Master > Music / SFX / Voice + Exposed Parameter 4개).
        /// AudioMixer 생성 API가 공개되어 있지 않아 리플렉션을 사용하며, 실패하면 경고만 남기고 null을 반환한다
        /// (이 경우 GameSettings는 AudioListener.volume으로 마스터 볼륨만 처리한다).
        /// </summary>
        private static AudioMixer EnsureAudioMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Resources");
            try
            {
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                System.Type ctrlType = FindType("UnityEditor.Audio.AudioMixerController");
                System.Type groupType = FindType("UnityEditor.Audio.AudioMixerGroupController");
                if (ctrlType == null || groupType == null) throw new System.Exception("AudioMixerController 타입을 찾지 못했습니다.");

                object controller = ctrlType.GetMethod("CreateMixerControllerAtPath", F).Invoke(null, new object[] { MixerPath });
                object master = ctrlType.GetProperty("masterGroup", F).GetValue(controller);

                MethodInfo createGroup = ctrlType.GetMethod("CreateNewGroup", F);
                MethodInfo addChild = ctrlType.GetMethod("AddChildToParent", F);
                bool createHasUndoArg = createGroup.GetParameters().Length == 2;

                var groups = new List<(string param, object group)> { (GameSettings.MasterParam, master) };
                foreach ((string groupName, string param) in new[] { ("Music", GameSettings.MusicParam), ("SFX", GameSettings.SfxParam), ("Voice", GameSettings.VoiceParam) })
                {
                    object grp = createGroup.Invoke(controller, createHasUndoArg ? new object[] { groupName, false } : new object[] { groupName });
                    addChild.Invoke(controller, new[] { grp, master });
                    groups.Add((param, grp));
                }

                // Exposed Parameter: 각 그룹의 Volume을 노출
                System.Type paramType = FindType("UnityEditor.Audio.ExposedAudioParameter");
                PropertyInfo exposedProp = ctrlType.GetProperty("exposedParameters", F);
                if (paramType == null || exposedProp == null) throw new System.Exception("exposedParameters API를 찾지 못했습니다.");

                var arr = System.Array.CreateInstance(paramType, groups.Count);
                for (int i = 0; i < groups.Count; i++)
                {
                    object p = System.Activator.CreateInstance(paramType);
                    object guid = groupType.GetMethod("GetGUIDForVolume", F).Invoke(groups[i].group, null);
                    SetMember(paramType, p, "guid", guid);
                    SetMember(paramType, p, "name", groups[i].param);
                    arr.SetValue(p, i);
                }
                exposedProp.SetValue(controller, arr);

                EditorUtility.SetDirty((Object)controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MainMenuBuilder] AudioMixer 자동 생성 실패 → 수동으로 만들어야 합니다 (README 체크리스트 참고).\n" + e);
                return null;
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null)
            {
                string report = string.Join(", ", new[] { GameSettings.MasterParam, GameSettings.MusicParam, GameSettings.SfxParam, GameSettings.VoiceParam }
                    .Select(n => n + (mixer.GetFloat(n, out _) ? "=OK" : "=노출 안 됨")));
                Debug.Log("[MainMenuBuilder] AudioMixer 생성: " + MixerPath + " (" + report + ")");
            }
            return mixer;
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void SetMember(System.Type type, object target, string name, object value)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo f = type.GetField(name, F);
            if (f != null) { f.SetValue(target, value); return; }
            PropertyInfo p = type.GetProperty(name, F);
            if (p != null) { p.SetValue(target, value); return; }
            throw new System.Exception($"{type.Name}.{name} 멤버를 찾지 못했습니다.");
        }

        // ═════════════════════════ 팝업 공통 프레임 ═════════════════════════
        private struct PopupParts
        {
            public CanvasGroup root;
            public RectTransform window;
            public Button dim;
            public Button close;
        }

        /// <summary>딤 + 창(패널/테두리/브래킷/제목/X 버튼)으로 이루어진 모달 팝업 골격 생성</summary>
        private static PopupParts BuildPopupFrame(string name, Transform parent, GenAssets g, TMP_FontAsset font,
            string title, string subtitle, Vector2 size, bool withCloseButton, float dimAlpha = 0.72f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 0);
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // 딤: 반투명 검정. 바깥 클릭 → 닫기
            Image dim = CreateImage("Dim", go.transform, g.white, Image.Type.Simple, true);
            Stretch(dim.rectTransform, 0);
            dim.color = new Color(0, 0, 0, dimAlpha);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dim;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };

            // 창
            var win = new GameObject("Window", typeof(RectTransform));
            win.transform.SetParent(go.transform, false);
            var wrt = (RectTransform)win.transform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = size;
            wrt.anchoredPosition = Vector2.zero;

            Image bg = CreateImage("PanelBackground", wrt, g.panel, Image.Type.Sliced, true); // 창 내부 클릭이 딤으로 새지 않게 raycast on
            Stretch(bg.rectTransform, 0);
            bg.color = PanelDark;
            Image border = CreateImage("PanelBorder", wrt, g.border, Image.Type.Sliced, false);
            Stretch(border.rectTransform, 0);
            border.color = PanelBorder;
            Image br = CreateImage("PanelBrackets", wrt, g.brackets, Image.Type.Sliced, false);
            Stretch(br.rectTransform, -10);
            br.color = WithA(Accent, 0.8f);

            if (!string.IsNullOrEmpty(title))
            {
                var t = CreateText("Title", wrt, title, font, 46, TextAlignmentOptions.MidlineLeft);
                t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                t.characterSpacing = 10f;
                t.color = TextMain;
                var trt = t.rectTransform;
                trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
                trt.pivot = new Vector2(0.5f, 1);
                trt.offsetMin = new Vector2(56, -92); trt.offsetMax = new Vector2(-130, -28);

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var s = CreateText("Subtitle", wrt, subtitle, font, 26, TextAlignmentOptions.MidlineLeft);
                    s.characterSpacing = 4f;
                    s.color = TextDim;
                    var srt = s.rectTransform;
                    srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
                    srt.pivot = new Vector2(0.5f, 1);
                    srt.offsetMin = new Vector2(56, -126); srt.offsetMax = new Vector2(-130, -90);
                }

                Image line = CreateImage("TitleLine", wrt, g.white, Image.Type.Simple, false);
                var lrt = line.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1);
                lrt.pivot = new Vector2(0, 0.5f);
                lrt.anchoredPosition = new Vector2(56, -136);
                lrt.sizeDelta = new Vector2(160, 4);
                line.color = Accent;
            }

            Button closeBtn = null;
            if (withCloseButton)
            {
                var cgo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                cgo.transform.SetParent(wrt, false);
                var crt = (RectTransform)cgo.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(1, 1);
                crt.pivot = new Vector2(1, 1);
                crt.anchoredPosition = new Vector2(-28, -28);
                crt.sizeDelta = new Vector2(64, 64);
                var cimg = cgo.GetComponent<Image>();
                cimg.sprite = g.panel;
                cimg.type = Image.Type.Sliced;
                cimg.color = Color.white;
                closeBtn = cgo.GetComponent<Button>();
                closeBtn.targetGraphic = cimg;
                closeBtn.transition = Selectable.Transition.ColorTint;
                closeBtn.colors = FocusColors(new Color(0.12f, 0.15f, 0.19f, 1f));

                Image x = CreateImage("Icon", crt, g.cross, Image.Type.Simple, false);
                Stretch(x.rectTransform, 14);
                x.preserveAspect = true;
                x.color = TextMain;
            }

            return new PopupParts { root = cg, window = wrt, dim = dimBtn, close = closeBtn };
        }

        /// <summary>PopupBase 공통 필드 연결</summary>
        private static void WirePopupBase(SerializedObject so, PopupParts p)
        {
            so.FindProperty("rootGroup").objectReferenceValue = p.root;
            so.FindProperty("window").objectReferenceValue = p.window;
            so.FindProperty("dimButton").objectReferenceValue = p.dim;
            so.FindProperty("closeButton").objectReferenceValue = p.close;
        }

        // ───────── 버튼 인스턴스 / 라벨 ─────────
        /// <summary>MenuButton 프리팹 인스턴스를 만들고 크기·라벨을 설정 (위치는 호출자가 지정)</summary>
        private static GameObject InstantiateButtonRaw(GameObject prefab, Transform parent, string name, string text, Vector2 size, float fontSize)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rt);

            var label = go.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 2f;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(16, 0);
            lrt.offsetMax = new Vector2(-16, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lrt);
            return go;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rt);
        }

        /// <summary>씬 안의 모든 MenuButtonFX에 효과음 AudioSource 슬롯을 연결 (클립은 비워둠)</summary>
        private static void AssignAudioToButtons(Transform root, AudioSource audio)
        {
            foreach (MenuButtonFX fx in root.GetComponentsInChildren<MenuButtonFX>(true))
            {
                var so = new SerializedObject(fx);
                so.FindProperty("audioSource").objectReferenceValue = audio;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ═════════════════════════ 시나리오 슬롯 프리팹 ═════════════════════════
        private static GameObject BuildSlotPrefab(GenAssets g, TMP_FontAsset font)
        {
            var root = new GameObject("ScenarioSlot", typeof(RectTransform), typeof(Button), typeof(MenuButtonFX), typeof(ScenarioSlot));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(572, 120);

            Image glow = CreateImage("Glow", root.transform, g.glow, Image.Type.Sliced, false);
            Stretch(glow.rectTransform, -26);
            glow.pixelsPerUnitMultiplier = 1f;

            Image fill = CreateImage("Fill", root.transform, g.panel, Image.Type.Sliced, true);
            Stretch(fill.rectTransform, 0);
            Image border = CreateImage("Border", root.transform, g.border, Image.Type.Sliced, false);
            Stretch(border.rectTransform, 0);

            // 선택됨: 파란 테두리 고정 (MenuButtonFX가 Border 색을 계속 바꾸므로 별도 Image)
            Image selFrame = CreateImage("SelectedFrame", root.transform, g.border, Image.Type.Sliced, false);
            Stretch(selFrame.rectTransform, 0);
            selFrame.color = Accent;
            selFrame.enabled = false;

            Image accent = CreateImage("AccentBar", root.transform, g.white, Image.Type.Simple, false);
            var art = accent.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0, 0.5f);
            art.pivot = new Vector2(0, 0.5f);
            art.anchoredPosition = new Vector2(10, 0);
            art.sizeDelta = new Vector2(5, 70);
            accent.color = new Color(1, 1, 1, 0);

            TMP_Text number = CreateText("Number", root.transform, "01", font, 46, TextAlignmentOptions.MidlineLeft);
            number.fontStyle = FontStyles.Bold;
            number.color = Accent;
            var nrt = number.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0, 0.5f);
            nrt.pivot = new Vector2(0, 0.5f);
            nrt.anchoredPosition = new Vector2(32, 0);
            nrt.sizeDelta = new Vector2(84, 80);

            TMP_Text nameText = CreateText("Name", root.transform, "시나리오", font, 36, TextAlignmentOptions.MidlineLeft);
            nameText.fontStyle = FontStyles.Bold;
            nameText.characterSpacing = 2f;
            var namert = nameText.rectTransform;
            namert.anchorMin = Vector2.zero; namert.anchorMax = Vector2.one;
            namert.offsetMin = new Vector2(128, 60); namert.offsetMax = new Vector2(-100, -10);

            TMP_Text desc = CreateText("Description", root.transform, "설명", font, 22, TextAlignmentOptions.MidlineLeft);
            desc.color = new Color(0.60f, 0.68f, 0.76f, 1f);
            var drt = desc.rectTransform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(128, 12); drt.offsetMax = new Vector2(-100, -62);

            Image lockIcon = CreateImage("LockIcon", root.transform, g.lockIcon, Image.Type.Simple, false);
            lockIcon.preserveAspect = true;
            lockIcon.color = DisabledGray;
            var lrt = lockIcon.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(1, 0.5f);
            lrt.pivot = new Vector2(1, 0.5f);
            lrt.anchoredPosition = new Vector2(-30, 0);
            lrt.sizeDelta = new Vector2(52, 52);

            // 선택됨 배지: 체크 아이콘 + "선택됨"
            var badge = new GameObject("SelectedBadge", typeof(RectTransform));
            badge.transform.SetParent(root.transform, false);
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = new Vector2(-24, 0);
            brt.sizeDelta = new Vector2(150, 48);
            Image check = CreateImage("Check", brt, g.check, Image.Type.Simple, false);
            check.preserveAspect = true;
            check.color = Accent;
            var crt = check.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0, 0.5f);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(40, 40);
            TMP_Text badgeText = CreateText("Text", brt, "선택됨", font, 24, TextAlignmentOptions.MidlineRight);
            badgeText.color = Accent;
            badgeText.fontStyle = FontStyles.Bold;
            var btrt = badgeText.rectTransform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
            btrt.offsetMin = new Vector2(44, 0); btrt.offsetMax = Vector2.zero;
            badge.SetActive(false);

            var button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None; // 연출은 MenuButtonFX가 담당
            button.targetGraphic = fill;

            var fxSo = new SerializedObject(root.GetComponent<MenuButtonFX>());
            fxSo.FindProperty("scaleTarget").objectReferenceValue = rt;
            fxSo.FindProperty("fillImage").objectReferenceValue = fill;
            fxSo.FindProperty("borderImage").objectReferenceValue = border;
            fxSo.FindProperty("glowImage").objectReferenceValue = glow;
            fxSo.FindProperty("accentBar").objectReferenceValue = accent;
            fxSo.FindProperty("label").objectReferenceValue = nameText;
            fxSo.ApplyModifiedPropertiesWithoutUndo();
            ApplyStyle(root, SlotStyle());

            var slotSo = new SerializedObject(root.GetComponent<ScenarioSlot>());
            slotSo.FindProperty("button").objectReferenceValue = button;
            slotSo.FindProperty("numberText").objectReferenceValue = number;
            slotSo.FindProperty("nameText").objectReferenceValue = nameText;
            slotSo.FindProperty("descriptionText").objectReferenceValue = desc;
            slotSo.FindProperty("lockIcon").objectReferenceValue = lockIcon.gameObject;
            slotSo.FindProperty("selectedBadge").objectReferenceValue = badge;
            slotSo.FindProperty("selectedFrame").objectReferenceValue = selFrame;
            slotSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ═════════════════════════ 확인창 프리팹 ═════════════════════════
        private static GameObject BuildConfirmPrefab(GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            PopupParts p = BuildPopupFrame("ConfirmDialog", null, g, font, null, null, new Vector2(820, 380), false, 0.6f);
            GameObject root = p.root.gameObject;
            var dialog = root.AddComponent<ConfirmDialog>();

            TMP_Text msg = CreateText("Message", p.window, "확인하시겠습니까?", font, 40, TextAlignmentOptions.Center);
            msg.textWrappingMode = TextWrappingModes.Normal;
            msg.color = TextMain;
            var mrt = msg.rectTransform;
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(48, 170); mrt.offsetMax = new Vector2(-48, -40);

            GameObject yes = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_Yes", "예", new Vector2(280, 84), 36);
            GameObject no = InstantiateButtonRaw(buttonPrefab, p.window, "Btn_No", "아니요", new Vector2(280, 84), 36);
            Anchor((RectTransform)yes.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-160, 52));
            Anchor((RectTransform)no.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(160, 52));

            // 좌우 이동 연결
            var yesBtn = yes.GetComponent<Button>();
            var noBtn = no.GetComponent<Button>();
            yesBtn.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = noBtn, selectOnLeft = noBtn };
            noBtn.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yesBtn, selectOnRight = yesBtn };

            var so = new SerializedObject(dialog);
            WirePopupBase(so, p);
            so.FindProperty("messageText").objectReferenceValue = msg;
            so.FindProperty("yesButton").objectReferenceValue = yesBtn;
            so.FindProperty("noButton").objectReferenceValue = noBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ConfirmPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ═════════════════════════ 설정 팝업 컨트롤 생성 ═════════════════════════
        private static RectTransform CreateSettingsPage(string name, Transform window)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(window, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(56, 130);
            rt.offsetMax = new Vector2(-56, -250);
            return rt;
        }

        private static RectTransform CreateSettingsRow(RectTransform page, int index, string labelText, TMP_FontAsset font)
        {
            var go = new GameObject($"Row_{index + 1}", typeof(RectTransform));
            go.transform.SetParent(page, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, 64);
            rt.anchoredPosition = new Vector2(0, -index * 74f);

            var label = CreateText("Label", rt, labelText, font, 30, TextAlignmentOptions.MidlineLeft);
            label.color = TextDim;
            label.characterSpacing = 2f;
            Anchor(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero);
            label.rectTransform.sizeDelta = new Vector2(400, 60);
            return rt;
        }

        private static void PlaceInRow(RectTransform rt, float x, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, 0);
        }

        private static (Slider slider, TMP_Text value) AddSlider(RectTransform row, string name, DefaultControls.Resources res, TMP_FontAsset font)
        {
            var go = DefaultControls.CreateSlider(res);
            go.name = name;
            go.transform.SetParent(row, false);
            PlaceInRow((RectTransform)go.transform, 420, new Vector2(560, 30));
            var slider = go.GetComponent<Slider>();
            StyleSlider(slider);

            var value = CreateText("Value", row, "0", font, 28, TextAlignmentOptions.MidlineLeft);
            value.color = TextMain;
            PlaceInRow(value.rectTransform, 1000, new Vector2(140, 50));
            return (slider, value);
        }

        private static TMP_Dropdown AddDropdown(RectTransform row, string name, DefaultControls.Resources res, TMP_FontAsset font)
        {
            var go = TMP_DefaultControls.CreateDropdown(ToTmpResources(res));
            go.name = name;
            go.transform.SetParent(row, false);
            PlaceInRow((RectTransform)go.transform, 420, new Vector2(560, 56));
            var dd = go.GetComponent<TMP_Dropdown>();
            StyleDropdown(dd, font);
            return dd;
        }

        private static Toggle AddToggle(RectTransform row, string name, DefaultControls.Resources res)
        {
            var go = DefaultControls.CreateToggle(res);
            go.name = name;
            go.transform.SetParent(row, false);
            Object.DestroyImmediate(go.transform.Find("Label").gameObject); // 레거시 Text 제거 (라벨은 행의 TMP 사용)
            PlaceInRow((RectTransform)go.transform, 420, new Vector2(48, 48));
            var t = go.GetComponent<Toggle>();
            StyleToggle(t);
            return t;
        }

        // ═════════════════════════ 메인 메뉴 씬 ═════════════════════════
        private static void BuildMainMenuScene(GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            ScenarioDatabase db = EnsureScenarioData(g);
            AudioMixer mixer = EnsureAudioMixer();
            GameObject slotPrefab = BuildSlotPrefab(g, font);
            GameObject confirmPrefab = BuildConfirmPrefab(g, font, buttonPrefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene이 로드돼 있던 에셋 오브젝트(ScriptableObject 등)를 언로드하므로, 씬 생성 후 경로로 다시 불러온다
            // (안 그러면 직렬화 시 참조가 null이 되어 시나리오 슬롯이 하나도 생성되지 않는다)
            db = AssetDatabase.LoadAssetAtPath<ScenarioDatabase>(DatabasePath);
            if (mixer != null) mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            confirmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPrefabPath);
            buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (db == null) Debug.LogError("[MainMenuBuilder] ScenarioDatabase를 불러오지 못했습니다: " + DatabasePath);

            // 카메라 (UI는 Overlay라 배경만 검게)
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // EventSystem + Input System UI 모듈 (마우스/키보드/게임패드)
            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var inputModule = esGo.GetComponent<InputSystemUIInputModule>();

            // UI 사운드 (클립은 비워둠). 믹서가 있으면 SFX 그룹으로 출력
            var audioGo = new GameObject("UIAudio", typeof(AudioSource));
            var audio = audioGo.GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            if (mixer != null)
            {
                AudioMixerGroup[] sfx = mixer.FindMatchingGroups("SFX");
                if (sfx != null && sfx.Length > 0) audio.outputAudioMixerGroup = sfx[0];
            }

            // Canvas
            var canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform ct = canvasGo.transform;

            // 1) 배경 2장 (현재 / 다음). 다음 이미지가 위에서 페이드인한다
            MenuBackground bgCurrent = CreateBackgroundImage("Background", ct, g.background);
            MenuBackground bgNext = CreateBackgroundImage("BackgroundNext", ct, g.background);
            bgNext.gameObject.GetComponent<CanvasGroup>().alpha = 0f;

            // 2) 연기 레이어 2장 (속도 차이로 깊이감)
            CreateSmoke("SmokeFar", ct, g.smoke, new Vector2(0.006f, 0.0025f), new Vector2(1.2f, 0.9f), 0.18f, new Color(0.75f, 0.72f, 0.70f));
            CreateSmoke("SmokeNear", ct, g.smoke, new Vector2(0.014f, 0.004f), new Vector2(0.8f, 0.6f), 0.14f, new Color(0.55f, 0.50f, 0.48f));

            // 3) 경광등 글로우 (하단 좌우)
            var sirenGo = new GameObject("SirenGlow", typeof(RectTransform), typeof(SirenGlow));
            sirenGo.transform.SetParent(ct, false);
            Stretch((RectTransform)sirenGo.transform, 0);
            Image red = CreateImage("RedGlow", sirenGo.transform, g.radial, Image.Type.Simple, false);
            Image blue = CreateImage("BlueGlow", sirenGo.transform, g.radial, Image.Type.Simple, false);
            PlaceCorner(red.rectTransform, new Vector2(0, 0), new Vector2(150, 40), new Vector2(1100, 520));
            PlaceCorner(blue.rectTransform, new Vector2(1, 0), new Vector2(-150, 40), new Vector2(1100, 520));
            if (g.additive != null) { red.material = g.additive; blue.material = g.additive; }
            var sirenSo = new SerializedObject(sirenGo.GetComponent<SirenGlow>());
            sirenSo.FindProperty("redGlow").objectReferenceValue = red;
            sirenSo.FindProperty("blueGlow").objectReferenceValue = blue;
            sirenSo.ApplyModifiedPropertiesWithoutUndo();

            // 4) 왼쪽 → 오른쪽 어두운 그라디언트
            Image grad = CreateImage("GradientOverlay", ct, g.gradient, Image.Type.Simple, false);
            Stretch(grad.rectTransform, 0);
            grad.color = new Color(0, 0, 0, 0.85f);

            // 5) 메인 메뉴 버튼 그룹 (화면 왼쪽 8%, 세로 중앙보다 약간 위)
            var menuGo = new GameObject("MainMenu", typeof(RectTransform), typeof(CanvasGroup));
            menuGo.transform.SetParent(ct, false);
            var mrt = (RectTransform)menuGo.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.08f, 0.5f);
            mrt.pivot = new Vector2(0, 0.5f);
            mrt.sizeDelta = new Vector2(560, 720);
            mrt.anchoredPosition = new Vector2(0, 40);

            const float h = 110f, gap = 35f, quitGap = 90f;
            float y0 = 220f;
            GameObject startBtn = InstantiateButton(buttonPrefab, mrt, "Btn_StartGame", "START GAME", audio, new Vector2(0, y0));
            GameObject scnBtn = InstantiateButton(buttonPrefab, mrt, "Btn_ScenarioSelect", "SCENARIO SELECT", audio, new Vector2(0, y0 - (h + gap)));
            GameObject setBtn = InstantiateButton(buttonPrefab, mrt, "Btn_Settings", "SETTINGS", audio, new Vector2(0, y0 - (h + gap) * 2));
            GameObject quitBtn = InstantiateButton(buttonPrefab, mrt, "Btn_Quit", "QUIT", audio, new Vector2(0, y0 - (h + gap) * 2 - h - quitGap));

            // START GAME: 활성 시 파란색 스타일, 초기 상태는 비활성(회색). 아이콘 없음, 라벨 정렬은 다른 버튼과 동일
            ApplyStyle(startBtn, StartStyle());
            var startSo = new SerializedObject(startBtn.GetComponent<Button>());
            startSo.FindProperty("m_Interactable").boolValue = false;
            startSo.ApplyModifiedPropertiesWithoutUndo();
            PaintDisabledPreview(startBtn);
            ApplyStyle(quitBtn, RedStyle());

            // "SELECTED: 시나리오" 안내 문구 (START GAME 위)
            TMP_Text selectedLabel = CreateText("SelectedLabel", mrt, "시나리오를 선택하세요", font, 28, TextAlignmentOptions.MidlineLeft);
            selectedLabel.characterSpacing = 4f;
            selectedLabel.color = DisabledGray;
            var slrt = selectedLabel.rectTransform;
            slrt.anchorMin = slrt.anchorMax = new Vector2(0, 0.5f);
            slrt.pivot = new Vector2(0, 0.5f);
            slrt.sizeDelta = new Vector2(560, 44);
            slrt.anchoredPosition = new Vector2(8, y0 + h * 0.5f + 40f);

            // 6) 시나리오 선택 팝업
            PopupParts scn = BuildPopupFrame("ScenarioPopup", ct, g, font, "SCENARIO SELECT", "시나리오 선택", new Vector2(1280, 880), true);
            var gridGo = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(scn.window, false);
            var grt = (RectTransform)gridGo.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
            grt.offsetMin = new Vector2(56, 50); grt.offsetMax = new Vector2(-56, -166);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(572, 120);
            grid.spacing = new Vector2(24, 14);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var scnPopup = scn.root.gameObject.AddComponent<ScenarioSelectPopup>();

            // 7) 설정 팝업 (탭 3개) — 게임 씬의 일시정지 메뉴에서도 재사용
            SettingsPanel settings = BuildSettingsPopup(ct, g, font, buttonPrefab);

            // 8) 확인창 (시나리오 팝업 위에 뜨도록 가장 위쪽 팝업 순서에 배치)
            var confirmGo = (GameObject)PrefabUtility.InstantiatePrefab(confirmPrefab, ct);
            confirmGo.name = "ConfirmDialog";
            var confirm = confirmGo.GetComponent<ConfirmDialog>();

            var scnSo = new SerializedObject(scnPopup);
            WirePopupBase(scnSo, scn);
            scnSo.FindProperty("database").objectReferenceValue = db;
            scnSo.FindProperty("slotPrefab").objectReferenceValue = slotPrefab.GetComponent<ScenarioSlot>();
            scnSo.FindProperty("gridRoot").objectReferenceValue = grt;
            scnSo.FindProperty("confirmDialog").objectReferenceValue = confirm;
            scnSo.ApplyModifiedPropertiesWithoutUndo();

            // 9) 화면 페이드 (최상단)
            Image fader = CreateImage("ScreenFader", ct, g.white, Image.Type.Simple, true);
            Stretch(fader.rectTransform, 0);
            fader.color = Color.black;
            var faderGroup = fader.gameObject.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;           // 에디터에서 작업하기 편하도록 0 (실행 시 1에서 시작)
            faderGroup.blocksRaycasts = false;

            // 모든 버튼에 효과음 AudioSource 슬롯 연결
            AssignAudioToButtons(ct, audio);

            // 10) 시스템 오브젝트: 컨트롤러 + 인트로 + START 상태 + 배경 전환
            var sysGo = new GameObject("MenuSystem", typeof(MainMenuController), typeof(MenuIntroAnimation), typeof(StartButtonState), typeof(BackgroundSwitcher));

            var introSo = new SerializedObject(sysGo.GetComponent<MenuIntroAnimation>());
            introSo.FindProperty("screenFader").objectReferenceValue = faderGroup;
            var items = introSo.FindProperty("items");
            var introItems = new Transform[] { selectedLabel.transform, startBtn.transform, scnBtn.transform, setBtn.transform, quitBtn.transform };
            items.arraySize = introItems.Length;
            for (int i = 0; i < introItems.Length; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = introItems[i];
            introSo.ApplyModifiedPropertiesWithoutUndo();

            var startStateSo = new SerializedObject(sysGo.GetComponent<StartButtonState>());
            startStateSo.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
            startStateSo.FindProperty("startFx").objectReferenceValue = startBtn.GetComponent<MenuButtonFX>();
            startStateSo.FindProperty("selectedLabel").objectReferenceValue = selectedLabel;
            startStateSo.ApplyModifiedPropertiesWithoutUndo();

            var bgSwitchSo = new SerializedObject(sysGo.GetComponent<BackgroundSwitcher>());
            bgSwitchSo.FindProperty("currentBackground").objectReferenceValue = bgCurrent;
            bgSwitchSo.FindProperty("nextBackground").objectReferenceValue = bgNext;
            bgSwitchSo.ApplyModifiedPropertiesWithoutUndo();

            var ctrlSo = new SerializedObject(sysGo.GetComponent<MainMenuController>());
            ctrlSo.FindProperty("startButton").objectReferenceValue = startBtn.GetComponent<Button>();
            ctrlSo.FindProperty("scenarioButton").objectReferenceValue = scnBtn.GetComponent<Button>();
            ctrlSo.FindProperty("settingsButton").objectReferenceValue = setBtn.GetComponent<Button>();
            ctrlSo.FindProperty("quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();
            ctrlSo.FindProperty("mainMenuGroup").objectReferenceValue = menuGo.GetComponent<CanvasGroup>();
            ctrlSo.FindProperty("startState").objectReferenceValue = sysGo.GetComponent<StartButtonState>();
            ctrlSo.FindProperty("scenarioPopup").objectReferenceValue = scnPopup;
            ctrlSo.FindProperty("settingsPopup").objectReferenceValue = settings;
            ctrlSo.FindProperty("backgroundSwitcher").objectReferenceValue = sysGo.GetComponent<BackgroundSwitcher>();
            ctrlSo.FindProperty("intro").objectReferenceValue = sysGo.GetComponent<MenuIntroAnimation>();
            ctrlSo.FindProperty("inputModule").objectReferenceValue = inputModule;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            esGo.GetComponent<EventSystem>().firstSelectedGameObject = scnBtn; // 기본 포커스: SCENARIO SELECT

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        /// <summary>설정 팝업(탭 3개)을 만들고 SettingsPanel을 반환. 메인 메뉴 / 게임 씬 공용</summary>
        private static SettingsPanel BuildSettingsPopup(Transform ct, GenAssets g, TMP_FontAsset font, GameObject buttonPrefab)
        {
            PopupParts set = BuildPopupFrame("SettingsPopup", ct, g, font, "SETTINGS", "설정", new Vector2(1280, 860), true);
            var settings = set.root.gameObject.AddComponent<SettingsPanel>();
            DefaultControls.Resources res = BuiltinUiResources();

            string[] tabNames = { "조작  CONTROLS", "소리  AUDIO", "화면  GRAPHICS" };
            var tabButtons = new Button[3];
            var tabIndicators = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject tab = InstantiateButtonRaw(buttonPrefab, set.window, $"Tab_{i + 1}", tabNames[i], new Vector2(300, 64), 26);
                Anchor((RectTransform)tab.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56 + i * 316, -156));
                Image ind = CreateImage("Indicator", tab.transform, g.white, Image.Type.Simple, false);
                var irt = ind.rectTransform;
                irt.anchorMin = new Vector2(0, 0); irt.anchorMax = new Vector2(1, 0);
                irt.pivot = new Vector2(0.5f, 0);
                irt.sizeDelta = new Vector2(-40, 5);
                irt.anchoredPosition = new Vector2(0, 2);
                ind.color = Accent;
                tabButtons[i] = tab.GetComponent<Button>();
                tabIndicators[i] = ind.gameObject;
            }

            RectTransform pageControls = CreateSettingsPage("Page_Controls", set.window);
            RectTransform pageAudio = CreateSettingsPage("Page_Audio", set.window);
            RectTransform pageGraphics = CreateSettingsPage("Page_Graphics", set.window);

            // ① 조작
            var mouse = AddSlider(CreateSettingsRow(pageControls, 0, "마우스 감도", font), "MouseSensitivitySlider", res, font);
            var gamepad = AddSlider(CreateSettingsRow(pageControls, 1, "게임패드 감도", font), "GamepadSensitivitySlider", res, font);
            Toggle invertY = AddToggle(CreateSettingsRow(pageControls, 2, "Y축 반전", font), "InvertYToggle", res);
            TMP_Dropdown hintDd = AddDropdown(CreateSettingsRow(pageControls, 3, "힌트 난이도", font), "HintLevelDropdown", res, font);
            Toggle highlightTg = AddToggle(CreateSettingsRow(pageControls, 4, "상호작용 하이라이트", font), "HighlightToggle", res);

            // ② 소리
            var master = AddSlider(CreateSettingsRow(pageAudio, 0, "마스터 볼륨", font), "MasterSlider", res, font);
            var music = AddSlider(CreateSettingsRow(pageAudio, 1, "배경음악", font), "MusicSlider", res, font);
            var sfxS = AddSlider(CreateSettingsRow(pageAudio, 2, "효과음", font), "SfxSlider", res, font);
            var voice = AddSlider(CreateSettingsRow(pageAudio, 3, "음성 (내레이션)", font), "VoiceSlider", res, font);

            // ③ 화면
            TMP_Dropdown resDd = AddDropdown(CreateSettingsRow(pageGraphics, 0, "해상도", font), "ResolutionDropdown", res, font);
            TMP_Dropdown modeDd = AddDropdown(CreateSettingsRow(pageGraphics, 1, "화면 모드", font), "DisplayModeDropdown", res, font);
            TMP_Dropdown qualDd = AddDropdown(CreateSettingsRow(pageGraphics, 2, "그래픽 품질", font), "QualityDropdown", res, font);
            TMP_Dropdown fpsDd = AddDropdown(CreateSettingsRow(pageGraphics, 3, "프레임 제한", font), "FrameLimitDropdown", res, font);
            Toggle vsync = AddToggle(CreateSettingsRow(pageGraphics, 4, "수직 동기화 (VSync)", font), "VSyncToggle", res);
            var bright = AddSlider(CreateSettingsRow(pageGraphics, 5, "밝기", font), "BrightnessSlider", res, font);

            // 하단 버튼: 기본값으로 초기화 / 적용 / 닫기
            GameObject resetBtn = InstantiateButtonRaw(buttonPrefab, set.window, "Btn_Reset", "기본값으로 초기화", new Vector2(380, 76), 30);
            GameObject applyBtn = InstantiateButtonRaw(buttonPrefab, set.window, "Btn_Apply", "적용", new Vector2(220, 76), 30);
            GameObject closeBtn = InstantiateButtonRaw(buttonPrefab, set.window, "Btn_Close", "닫기", new Vector2(220, 76), 30);
            Anchor((RectTransform)resetBtn.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 44));
            Anchor((RectTransform)applyBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56 - 236, 44));
            Anchor((RectTransform)closeBtn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 44));

            var setSo = new SerializedObject(settings);
            WirePopupBase(setSo, set);
            SetObjectArray(setSo.FindProperty("tabButtons"), tabButtons);
            SetObjectArray(setSo.FindProperty("tabPages"), new[] { pageControls.gameObject, pageAudio.gameObject, pageGraphics.gameObject });
            SetObjectArray(setSo.FindProperty("tabIndicators"), tabIndicators);
            setSo.FindProperty("mouseSensitivitySlider").objectReferenceValue = mouse.slider;
            setSo.FindProperty("mouseSensitivityValue").objectReferenceValue = mouse.value;
            setSo.FindProperty("gamepadSensitivitySlider").objectReferenceValue = gamepad.slider;
            setSo.FindProperty("gamepadSensitivityValue").objectReferenceValue = gamepad.value;
            setSo.FindProperty("invertYToggle").objectReferenceValue = invertY;
            setSo.FindProperty("hintLevelDropdown").objectReferenceValue = hintDd;
            setSo.FindProperty("highlightToggle").objectReferenceValue = highlightTg;
            setSo.FindProperty("masterSlider").objectReferenceValue = master.slider;
            setSo.FindProperty("masterValue").objectReferenceValue = master.value;
            setSo.FindProperty("musicSlider").objectReferenceValue = music.slider;
            setSo.FindProperty("musicValue").objectReferenceValue = music.value;
            setSo.FindProperty("sfxSlider").objectReferenceValue = sfxS.slider;
            setSo.FindProperty("sfxValue").objectReferenceValue = sfxS.value;
            setSo.FindProperty("voiceSlider").objectReferenceValue = voice.slider;
            setSo.FindProperty("voiceValue").objectReferenceValue = voice.value;
            setSo.FindProperty("resolutionDropdown").objectReferenceValue = resDd;
            setSo.FindProperty("displayModeDropdown").objectReferenceValue = modeDd;
            setSo.FindProperty("qualityDropdown").objectReferenceValue = qualDd;
            setSo.FindProperty("frameLimitDropdown").objectReferenceValue = fpsDd;
            setSo.FindProperty("vSyncToggle").objectReferenceValue = vsync;
            setSo.FindProperty("brightnessSlider").objectReferenceValue = bright.slider;
            setSo.FindProperty("brightnessValue").objectReferenceValue = bright.value;
            setSo.FindProperty("resetButton").objectReferenceValue = resetBtn.GetComponent<Button>();
            setSo.FindProperty("applyButton").objectReferenceValue = applyBtn.GetComponent<Button>();
            setSo.FindProperty("footerCloseButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            setSo.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        // ───────── 씬 구성 헬퍼 ─────────
        private static MenuBackground CreateBackgroundImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter), typeof(CanvasGroup), typeof(MenuBackground));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().raycastTarget = false;
            var bg = go.GetComponent<MenuBackground>();
            var so = new SerializedObject(bg);
            so.FindProperty("backgroundSprite").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            bg.SetSprite(sprite);
            return bg;
        }

        /// <summary>에디터에서 보이는 정적 색상을 비활성(회색) 상태로 맞춘다</summary>
        private static void PaintDisabledPreview(GameObject button)
        {
            Color gray = DisabledGray;
            SetGraphicColor(button.transform.Find("Fill"), new Color(0.07f, 0.08f, 0.09f, 0.80f));
            SetGraphicColor(button.transform.Find("Border"), WithA(gray, 0.85f));
            SetGraphicColor(button.transform.Find("Glow"), WithA(gray, 0f));
            SetGraphicColor(button.transform.Find("Brackets"), WithA(gray, 0.35f));
            SetGraphicColor(button.transform.Find("Label"), gray);
        }

        private static void SetObjectArray<T>(SerializedProperty prop, T[] values) where T : Object
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
