using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 메인 메뉴 씬 / 버튼 프리팹 / 임시 텍스처 / Build Settings를 한 번에 생성하는 에디터 도구.
    /// 메뉴: Tools > GameUI > Build Main Menu
    /// 배치모드: -executeMethod GameUI.EditorTools.MainMenuBuilder.BuildAllBatch
    ///
    /// 주의: 다시 실행하면 MainMenu.unity와 MenuButton.prefab을 덮어쓴다.
    /// (GameScene.unity는 이미 있으면 건드리지 않는다)
    /// </summary>
    public static partial class MainMenuBuilder
    {
        // ───────── 경로 ─────────
        private const string GenDir = "Assets/Art/UI/Generated";
        private const string UserBackgroundPng = "Assets/Art/UI/MainMenuBackground.png";
        private const string UserBackgroundJpg = "Assets/Art/UI/MainMenuBackground.jpg";
        private const string FontsDir = "Assets/Fonts";
        private const string PrefabPath = "Assets/Prefabs/UI/MenuButton.prefab";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        // ───────── 기본 팔레트 (생성 시 초기값일 뿐, 이후엔 전부 Inspector에서 수정) ─────────
        private static readonly Color Accent = Hex("2F9BFF");   // 파란 강조 (#2F9BFF)
        private static readonly Color DisabledGray = Hex("6B7280"); // 비활성 회색 (#6B7280)
        private static readonly Color PanelDark = new Color(0.05f, 0.07f, 0.10f, 0.92f);
        private static readonly Color PanelBorder = new Color(0.35f, 0.55f, 0.75f, 0.6f);
        private static readonly Color TextMain = new Color(0.90f, 0.93f, 0.97f, 1f);
        private static readonly Color TextDim = new Color(0.60f, 0.68f, 0.76f, 1f);

        private struct GenAssets
        {
            public Sprite panel, border, glow, brackets, gradient, radial, lockIcon, check, cross, background, white;
            public Texture2D smoke;
            public Material additive;
        }

        private struct ButtonStyle
        {
            public Color fillN, borderN, glowN, bracketN, labelN;
            public Color fillH, borderH, glowH, bracketH, labelH;
        }

        // ═════════════════════════ 진입점 ═════════════════════════
        [MenuItem("Tools/GameUI/Build Main Menu")]
        public static void BuildAllMenu()
        {
            if (File.Exists(MainMenuScenePath) &&
                !EditorUtility.DisplayDialog("Build Main Menu",
                    "MainMenu.unity와 MenuButton.prefab을 다시 생성합니다(덮어쓰기). 계속할까요?", "생성", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
        }

        [MenuItem("Tools/GameUI/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath)) { Debug.Log("[MainMenuBuilder] TMP Essentials 이미 존재"); return; }
#pragma warning disable CS0618 // 구버전 호환용 API (6000.x에서도 동작)
            AssetDatabase.ImportPackage(TmpEssentialsPackage, false);
#pragma warning restore CS0618
            AssetDatabase.Refresh();
            Debug.Log("[MainMenuBuilder] TMP Essentials 임포트 완료");
        }

        /// <summary>배치모드 1단계: TMP 리소스 임포트</summary>
        public static void ImportTmpEssentialsBatch() => ImportTmpEssentials();

        /// <summary>배치모드 2단계: 전체 생성</summary>
        public static void BuildAllBatch()
        {
            BuildAll();
            AssetDatabase.SaveAssets();
        }

        public static void BuildAll()
        {
            if (!File.Exists(TmpSettingsPath)) ImportTmpEssentials();
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) == null)
            {
                Debug.LogError("[MainMenuBuilder] TMP Essentials가 아직 로드되지 않았습니다. 잠시 후 다시 실행하세요.");
                return;
            }

            EnsureFolder("Assets/Art/UI");
            EnsureFolder(GenDir);
            EnsureFolder("Assets/Prefabs/UI");
            EnsureFolder("Assets/Scenes");
            EnsureFolder(FontsDir);

            GenAssets g = GenerateAssets();
            TMP_FontAsset korean = EnsureKoreanFont();   // 한글 동적(Dynamic) 폰트 + 폴백 등록
            TMP_FontAsset font = FindFont();

            GameObject buttonPrefab = BuildButtonPrefab(g, font);
            BuildMainMenuScene(g, font, buttonPrefab);
            BuildGameSceneIfMissing(font);
            SetupBuildSettings();
            PrewarmKoreanGlyphs(korean);

            AssetDatabase.SaveAssets();
            Debug.Log("[MainMenuBuilder] 완료: MainMenu 씬, MenuButton 프리팹, Build Settings 생성됨. 사용 폰트: " + (font != null ? font.name : "(없음)"));
        }

        // ═════════════════════════ 폰트 ═════════════════════════
        /// <summary>Assets/Fonts에 TMP Font Asset이 있으면 그걸 쓰고, 없으면 TMP 기본 폰트(LiberationSans SDF)</summary>
        private static TMP_FontAsset FindFont()
        {
            // 한글용 NotoSansKR는 기본 폰트가 아니라 폴백으로만 쓴다 (영문 타이틀은 기존 폰트 유지)
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == KoreanFontPath) continue;
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
            return TMP_Settings.defaultFontAsset;
        }

        // ═════════════════════════ 텍스처 생성 ═════════════════════════
        private static GenAssets GenerateAssets()
        {
            var g = new GenAssets();
            const int P = 64, C = 18; // 패널 텍스처 크기, 모서리 깎임 크기

            g.panel = MakeSprite("PanelChamfer", P, P, (x, y) =>
            {
                float d = ChamferSdf(x, y, P, P, C);
                return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            }, new Vector4(C + 4, C + 4, C + 4, C + 4));

            g.border = MakeSprite("PanelChamferBorder", P, P, (x, y) =>
            {
                float d = ChamferSdf(x, y, P, P, C);
                const float w = 2.5f; // 테두리 두께(px)
                float ring = Mathf.Abs(d + w * 0.5f) - w * 0.5f;
                return new Color(1, 1, 1, Mathf.Clamp01(0.5f - ring));
            }, new Vector4(C + 4, C + 4, C + 4, C + 4));

            const int G = 128, GI = 44;
            g.glow = MakeSprite("SoftGlow", G, G, (x, y) =>
            {
                float dx = Mathf.Max(GI - x, x - (G - GI), 0f);
                float dy = Mathf.Max(GI - y, y - (G - GI), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - dist / (GI - 2f));
                return new Color(1, 1, 1, a * a);
            }, new Vector4(GI, GI, GI, GI));

            const int B = 64, arm = 16, th = 3;
            g.brackets = MakeSprite("CornerBrackets", B, B, (x, y) =>
            {
                int ix = (int)x, iy = (int)y;
                bool nearL = ix < th, nearR = ix >= B - th, nearB = iy < th, nearT = iy >= B - th;
                bool inArmX = ix < arm || ix >= B - arm;
                bool inArmY = iy < arm || iy >= B - arm;
                bool on = ((nearL || nearR) && inArmY) || ((nearB || nearT) && inArmX);
                return new Color(1, 1, 1, on ? 1f : 0f);
            }, new Vector4(arm + 2, arm + 2, arm + 2, arm + 2));

            g.gradient = MakeSprite("LeftGradient", 256, 4, (x, y) =>
            {
                float u = x / 255f;
                float a = 1f - Mathf.SmoothStep(0.12f, 0.70f, u);
                return new Color(1, 1, 1, a);
            }, Vector4.zero);

            g.radial = MakeSprite("RadialGlow", 128, 128, (x, y) =>
            {
                float dx = (x - 64f) / 64f, dy = (y - 64f) / 64f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1, 1, 1, a * a);
            }, Vector4.zero);

            g.lockIcon = MakeSprite("LockIcon", 96, 96, LockPixel, Vector4.zero);
            g.check = MakeSprite("CheckIcon", 96, 96, CheckPixel, Vector4.zero);
            g.cross = MakeSprite("CrossIcon", 96, 96, CrossPixel, Vector4.zero);
            g.white = MakeSprite("White", 4, 4, (x, y) => Color.white, Vector4.zero);

            g.smoke = MakeSmokeTexture("SmokeNoise", 256);
            g.background = LoadUserBackground() ?? MakeSprite("MainMenuBackground_Placeholder", 960, 540, PlaceholderBackground, Vector4.zero, uncompressed: false);

            // Additive 머티리얼
            string matPath = GenDir + "/UIAdditive.mat";
            g.additive = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (g.additive == null)
            {
                Shader sh = Shader.Find("GameUI/UIAdditive");
                if (sh != null)
                {
                    g.additive = new Material(sh);
                    AssetDatabase.CreateAsset(g.additive, matPath);
                }
                else Debug.LogWarning("[MainMenuBuilder] GameUI/UIAdditive 셰이더를 찾지 못해 기본 머티리얼을 사용합니다.");
            }
            return g;
        }

        /// <summary>좌상단·우하단이 45도로 깎인 사각형의 근사 SDF (음수 = 내부)</summary>
        private static float ChamferSdf(float x, float y, int w, int h, float c)
        {
            const float inset = 1f;
            float l = inset, r = w - inset, b = inset, t = h - inset;
            float d = Mathf.Max(l - x, x - r, b - y, y - t);
            float tl = (c - ((x - l) + (t - y))) / 1.41421f; // 좌상단 깎임
            float br = (c - ((r - x) + (y - b))) / 1.41421f; // 우하단 깎임
            return Mathf.Max(d, tl, br);
        }

        /// <summary>자물쇠 아이콘 (흰색, 열쇠구멍은 투명). 시나리오 슬롯 잠금 표시용</summary>
        private static Color LockPixel(float x, float y)
        {
            float body = RoundRectSdf(x, y, 48f, 30f, 30f, 22f, 6f);

            float dx = x - 48f, dy = y - 52f;
            float ring = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 18f) - 4f;                              // 걸쇠 윗부분(반원)
            float legs = Mathf.Max(Mathf.Abs(Mathf.Abs(dx) - 18f) - 4f, Mathf.Max(40f - y, y - 52f));      // 걸쇠 다리
            float shackle = y >= 52f ? ring : legs;

            float a = Mathf.Clamp01(0.5f - Mathf.Min(body, shackle));

            // 열쇠구멍 (원 + 아래로 이어지는 홈)
            float holeCircle = Mathf.Sqrt((x - 48f) * (x - 48f) + (y - 33f) * (y - 33f)) - 6f;
            float holeSlit = RoundRectSdf(x, y, 48f, 24f, 3f, 9f, 1f);
            float hole = Mathf.Min(holeCircle, holeSlit);
            a *= Mathf.Clamp01(0.5f + hole);
            return new Color(1, 1, 1, a);
        }

        /// <summary>체크 아이콘 (흰색). 선택된 시나리오 표시용</summary>
        private static Color CheckPixel(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d = Mathf.Min(SegDist(p, new Vector2(18, 50), new Vector2(40, 28)), SegDist(p, new Vector2(40, 28), new Vector2(80, 72))) - 5f;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        }

        /// <summary>X 아이콘 (흰색). 팝업 닫기 버튼용</summary>
        private static Color CrossPixel(float x, float y)
        {
            Vector2 p = new Vector2(x, y);
            float d = Mathf.Min(SegDist(p, new Vector2(24, 24), new Vector2(72, 72)), SegDist(p, new Vector2(24, 72), new Vector2(72, 24))) - 4.5f;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>둥근 사각형 SDF (음수 = 내부)</summary>
        private static float RoundRectSdf(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r;
            float qy = Mathf.Abs(y - cy) - hy + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>임시 배경: 야간 하늘 + 화재 불빛 + 건물 실루엣 (실제 이미지로 교체 예정)</summary>
        private static Color PlaceholderBackground(float x, float y)
        {
            float u = x / 960f, v = y / 540f;
            Color sky = Color.Lerp(new Color(0.06f, 0.05f, 0.06f), new Color(0.03f, 0.05f, 0.10f), v);
            // 화재 불빛 (오른쪽 중앙)
            float fx = (u - 0.62f) * 1.6f, fy = (v - 0.42f) * 1.2f;
            float fire = Mathf.Exp(-(fx * fx + fy * fy) * 6f);
            Color c = sky + new Color(1f, 0.38f, 0.08f) * fire * 0.9f;
            // 연기 (위쪽 노이즈)
            float n = Mathf.PerlinNoise(u * 4f, v * 3f) * Mathf.SmoothStep(0.3f, 0.9f, v);
            c = Color.Lerp(c, new Color(0.12f, 0.11f, 0.12f), n * 0.5f);
            // 건물 실루엣
            float bx = Mathf.Floor(u * 14f);
            float height = 0.25f + Mathf.PerlinNoise(bx * 0.73f, 3.1f) * 0.35f;
            if (u > 0.45f && u < 0.85f) height += 0.12f; // 화재 아파트
            if (v < height)
            {
                float win = (Mathf.Repeat(u * 140f, 1f) < 0.45f && Mathf.Repeat(v * 70f, 1f) < 0.5f) ? 1f : 0f;
                Color building = new Color(0.04f, 0.04f, 0.05f) + new Color(1f, 0.45f, 0.1f) * fire * 0.35f;
                c = building + new Color(1f, 0.5f, 0.15f) * win * fire * 0.6f;
            }
            // 바닥
            if (v < 0.12f) c = Color.Lerp(new Color(0.03f, 0.03f, 0.04f), c, v / 0.12f);
            c.a = 1f;
            return c;
        }

        private static Sprite LoadUserBackground()
        {
            foreach (string path in new[] { UserBackgroundPng, UserBackgroundJpg })
            {
                if (!File.Exists(path)) continue;
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp != null && imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
            return null;
        }

        private static Sprite MakeSprite(string name, int w, int h, System.Func<float, float, Color> pixel, Vector4 border, bool uncompressed = true)
        {
            string path = $"{GenDir}/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x + 0.5f, y + 0.5f);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.spritePixelsPerUnit = 100;
            if (uncompressed) imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>타일링 가능한 연기 노이즈 (주기적 value-noise fBm)</summary>
        private static Texture2D MakeSmokeTexture(string name, int size)
        {
            string path = $"{GenDir}/{name}.png";
            var rng = new System.Random(1234);
            const int baseCells = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];

            // 옥타브별 격자 랜덤값
            int octaves = 5;
            var grids = new float[octaves][,];
            for (int o = 0; o < octaves; o++)
            {
                int n = baseCells << o;
                grids[o] = new float[n, n];
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) grids[o][i, j] = (float)rng.NextDouble();
            }

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f, amp = 0.5f, norm = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        int n = baseCells << o;
                        float fx = (float)x / size * n, fy = (float)y / size * n;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                        float v00 = grids[o][x0 % n, y0 % n], v10 = grids[o][(x0 + 1) % n, y0 % n];
                        float v01 = grids[o][x0 % n, (y0 + 1) % n], v11 = grids[o][(x0 + 1) % n, (y0 + 1) % n];
                        sum += amp * Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
                        norm += amp;
                        amp *= 0.5f;
                    }
                    float v = sum / norm;
                    float a = Mathf.SmoothStep(0.35f, 0.85f, v);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // ═════════════════════════ 버튼 프리팹 ═════════════════════════
        private static GameObject BuildButtonPrefab(GenAssets g, TMP_FontAsset font)
        {
            var root = new GameObject("MenuButton", typeof(RectTransform), typeof(CanvasGroup), typeof(Button), typeof(MenuButtonFX));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(560, 110);

            Image glow = CreateImage("Glow", root.transform, g.glow, Image.Type.Sliced, false);
            Stretch(glow.rectTransform, -30);
            glow.pixelsPerUnitMultiplier = 1f;

            Image fill = CreateImage("Fill", root.transform, g.panel, Image.Type.Sliced, true);
            Stretch(fill.rectTransform, 0);
            var shadow = fill.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.55f);
            shadow.effectDistance = new Vector2(5, -5);

            Image border = CreateImage("Border", root.transform, g.border, Image.Type.Sliced, false);
            Stretch(border.rectTransform, 0);

            Image brackets = CreateImage("Brackets", root.transform, g.brackets, Image.Type.Sliced, false);
            Stretch(brackets.rectTransform, -9);
            brackets.pixelsPerUnitMultiplier = 1f;

            // 호버 시 슬라이드로 나타나는 왼쪽 하이라이트 바
            Image accent = CreateImage("AccentBar", root.transform, g.white, Image.Type.Simple, false);
            var art = accent.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0, 0.5f);
            art.pivot = new Vector2(0, 0.5f);
            art.anchoredPosition = new Vector2(10, 0);
            art.sizeDelta = new Vector2(5, 60);
            accent.color = new Color(1, 1, 1, 0);

            TMP_Text label = CreateText("Label", root.transform, "BUTTON", font, 42, TextAlignmentOptions.MidlineLeft);
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.characterSpacing = 8f;
            Stretch(label.rectTransform, 0);
            label.rectTransform.offsetMin = new Vector2(52, 0);
            label.rectTransform.offsetMax = new Vector2(-24, 0);

            var button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None; // 연출은 MenuButtonFX가 담당
            button.targetGraphic = fill;

            var so = new SerializedObject(root.GetComponent<MenuButtonFX>());
            so.FindProperty("scaleTarget").objectReferenceValue = rt;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("borderImage").objectReferenceValue = border;
            so.FindProperty("glowImage").objectReferenceValue = glow;
            so.FindProperty("bracketImage").objectReferenceValue = brackets;
            so.FindProperty("accentBar").objectReferenceValue = accent;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            ApplyStyle(root, BlueStyle());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static ButtonStyle BlueStyle() => new ButtonStyle
        {
            fillN = new Color(0.06f, 0.09f, 0.13f, 0.82f), fillH = new Color(0.09f, 0.14f, 0.20f, 0.90f),
            borderN = new Color(0.35f, 0.55f, 0.75f, 0.55f), borderH = new Color(0.55f, 0.80f, 1.00f, 1f),
            glowN = new Color(0.35f, 0.55f, 0.75f, 0f), glowH = new Color(0.40f, 0.70f, 1.00f, 0.40f),
            bracketN = new Color(0.55f, 0.70f, 0.85f, 0.40f), bracketH = new Color(0.80f, 0.92f, 1.00f, 1f),
            labelN = new Color(0.86f, 0.90f, 0.95f, 1f), labelH = Color.white,
        };

        /// <summary>START GAME 활성 상태: 파란색(#2F9BFF) 테두리 + 글로우</summary>
        private static ButtonStyle StartStyle() => new ButtonStyle
        {
            fillN = new Color(0.04f, 0.09f, 0.16f, 0.88f), fillH = new Color(0.06f, 0.14f, 0.25f, 0.94f),
            borderN = WithA(Accent, 0.95f), borderH = Hex("7CC4FF"),
            glowN = WithA(Accent, 0.30f), glowH = WithA(Accent, 0.75f),
            bracketN = WithA(Accent, 0.75f), bracketH = Hex("9AD2FF"),
            labelN = Color.white, labelH = Color.white,
        };

        /// <summary>시나리오 슬롯: 호버/포커스 시 테두리가 파란색(#2F9BFF)</summary>
        private static ButtonStyle SlotStyle() => new ButtonStyle
        {
            fillN = new Color(0.06f, 0.09f, 0.13f, 0.85f), fillH = new Color(0.07f, 0.13f, 0.21f, 0.95f),
            borderN = new Color(0.35f, 0.55f, 0.75f, 0.50f), borderH = Accent,
            glowN = WithA(Accent, 0f), glowH = WithA(Accent, 0.45f),
            bracketN = new Color(0.55f, 0.70f, 0.85f, 0.35f), bracketH = Hex("9AD2FF"),
            labelN = new Color(0.90f, 0.93f, 0.97f, 1f), labelH = Color.white,
        };

        private static ButtonStyle RedStyle() => new ButtonStyle
        {
            fillN = new Color(0.14f, 0.03f, 0.03f, 0.85f), fillH = new Color(0.22f, 0.05f, 0.05f, 0.92f),
            borderN = new Color(0.75f, 0.15f, 0.12f, 0.70f), borderH = new Color(1f, 0.30f, 0.25f, 1f),
            glowN = new Color(0.90f, 0.15f, 0.10f, 0f), glowH = new Color(1f, 0.20f, 0.15f, 0.45f),
            bracketN = new Color(0.85f, 0.25f, 0.20f, 0.45f), bracketH = new Color(1f, 0.45f, 0.40f, 1f),
            labelN = new Color(0.95f, 0.85f, 0.85f, 1f), labelH = Color.white,
        };

        private static void ApplyStyle(GameObject button, ButtonStyle s)
        {
            var so = new SerializedObject(button.GetComponent<MenuButtonFX>());
            so.FindProperty("fillNormal").colorValue = s.fillN;
            so.FindProperty("borderNormal").colorValue = s.borderN;
            so.FindProperty("glowNormal").colorValue = s.glowN;
            so.FindProperty("bracketNormal").colorValue = s.bracketN;
            so.FindProperty("labelNormal").colorValue = s.labelN;
            so.FindProperty("fillHighlight").colorValue = s.fillH;
            so.FindProperty("borderHighlight").colorValue = s.borderH;
            so.FindProperty("glowHighlight").colorValue = s.glowH;
            so.FindProperty("bracketHighlight").colorValue = s.bracketH;
            so.FindProperty("labelHighlight").colorValue = s.labelH;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 에디터에서 보이는 정적 색상도 기본 상태로 맞춰둔다
            var fx = button.GetComponent<MenuButtonFX>();
            SetGraphicColor(button.transform.Find("Fill"), s.fillN);
            SetGraphicColor(button.transform.Find("Border"), s.borderN);
            SetGraphicColor(button.transform.Find("Glow"), s.glowN);
            SetGraphicColor(button.transform.Find("Brackets"), s.bracketN);
            SetGraphicColor(button.transform.Find("Label"), s.labelN);
            if (PrefabUtility.IsPartOfPrefabInstance(fx)) PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
        }

        private static void SetGraphicColor(Transform t, Color c)
        {
            if (t == null) return;
            var gr = t.GetComponent<Graphic>();
            gr.color = c;
            if (PrefabUtility.IsPartOfPrefabInstance(gr)) PrefabUtility.RecordPrefabInstancePropertyModifications(gr);
        }

        private static GameObject InstantiateButton(GameObject prefab, Transform parent, string name, string text,
            AudioSource audio, Vector2 anchoredPos, Vector2? size = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (size.HasValue) rt.sizeDelta = size.Value;
            rt.anchoredPosition = anchoredPos + new Vector2(rt.sizeDelta.x * 0.5f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rt);

            var label = go.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = text;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);

            var so = new SerializedObject(go.GetComponent<MenuButtonFX>());
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedProperties();
            return go;
        }

        // ═════════════════════════ 게임 씬 (임시) ═════════════════════════
        private static void BuildGameSceneIfMissing(TMP_FontAsset font)
        {
            if (File.Exists(GameScenePath)) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var t = CreateText("Placeholder", canvasGo.transform, "GAME SCENE (PLACEHOLDER)", font, 48, TextAlignmentOptions.Center);
            Stretch(t.rectTransform, 0);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            // 메인 메뉴 씬을 다시 열어둔다
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        }

        private static void SetupBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MainMenuScenePath && s.path != GameScenePath && File.Exists(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ═════════════════════════ UI 헬퍼 ═════════════════════════
        /// <summary>패널 왼쪽 기준 행 배치. xOffset 기본은 컨트롤 열(400)</summary>
        private static void PlaceRow(RectTransform rt, float y, Vector2 size, float xOffset = 400)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(xOffset, y);
        }

        private static void ScaleLabel(GameObject button, float size)
        {
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(16, 0);
            lrt.offsetMax = new Vector2(-16, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lrt);
        }

        private static ColorBlock FocusColors(Color normal)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = normal;
            cb.highlightedColor = Color.Lerp(normal, Accent, 0.55f);
            cb.selectedColor = Color.Lerp(normal, Accent, 0.75f);
            cb.pressedColor = Accent;
            cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            cb.fadeDuration = 0.08f;
            return cb;
        }

        private static void StyleSlider(Slider s)
        {
            Transform bg = s.transform.Find("Background");
            bg.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.19f, 1f);
            s.fillRect.GetComponent<Image>().color = Accent;
            var handle = s.handleRect.GetComponent<Image>();
            handle.color = Color.white;
            s.handleRect.sizeDelta = new Vector2(30, 0);
            s.targetGraphic = handle;
            s.colors = FocusColors(new Color(0.85f, 0.88f, 0.92f));
        }

        private static void StyleDropdown(TMP_Dropdown d, TMP_FontAsset font)
        {
            var img = d.GetComponent<Image>();
            img.color = Color.white;
            d.colors = FocusColors(new Color(0.12f, 0.15f, 0.19f, 1f));
            d.captionText.color = TextMain;
            d.captionText.fontSize = 26;
            d.captionText.font = font;
            d.itemText.color = TextMain;
            d.itemText.fontSize = 24;
            d.itemText.font = font;
            d.template.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 0.98f);
            d.template.sizeDelta = new Vector2(d.template.sizeDelta.x, 260);
            var arrow = d.transform.Find("Arrow");
            if (arrow != null) arrow.GetComponent<Image>().color = Accent;
            var item = d.template.GetComponentInChildren<Toggle>(true);
            if (item != null)
            {
                ((RectTransform)item.transform).sizeDelta = new Vector2(0, 44);
                item.colors = FocusColors(new Color(0.10f, 0.12f, 0.16f, 1f));
                var itemBg = item.transform.Find("Item Background");
                if (itemBg != null) itemBg.GetComponent<Image>().color = Color.white;
                var check = item.transform.Find("Item Checkmark");
                if (check != null) check.GetComponent<Image>().color = Accent;
            }
            var content = d.template.Find("Viewport/Content") as RectTransform;
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, 44);
        }

        private static void StyleToggle(Toggle t)
        {
            var bg = (RectTransform)t.transform.Find("Background");
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            bg.anchoredPosition = Vector2.zero;
            bg.GetComponent<Image>().color = Color.white;
            t.colors = FocusColors(new Color(0.12f, 0.15f, 0.19f, 1f));
            var check = (RectTransform)bg.Find("Checkmark");
            check.anchorMin = Vector2.zero; check.anchorMax = Vector2.one;
            check.offsetMin = new Vector2(6, 6); check.offsetMax = new Vector2(-6, -6);
            check.GetComponent<Image>().color = Accent;
            t.isOn = true;
        }

        private static void SetVerticalNav(Selectable[] items, bool wrap)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                int up = i - 1, down = i + 1;
                if (wrap) { up = (up + items.Length) % items.Length; down %= items.Length; }
                nav.selectOnUp = up >= 0 && up < items.Length && up != i ? items[up] : null;
                nav.selectOnDown = down >= 0 && down < items.Length && down != i ? items[down] : null;
                items[i].navigation = nav;
                if (PrefabUtility.IsPartOfPrefabInstance(items[i])) PrefabUtility.RecordPrefabInstancePropertyModifications(items[i]);
            }
        }

        private static DefaultControls.Resources BuiltinUiResources() => new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };

        private static TMP_DefaultControls.Resources ToTmpResources(DefaultControls.Resources r) => new TMP_DefaultControls.Resources
        {
            standard = r.standard, background = r.background, inputField = r.inputField,
            knob = r.knob, checkmark = r.checkmark, dropdown = r.dropdown, mask = r.mask,
        };

        private static void CreateSmoke(string name, Transform parent, Texture2D tex, Vector2 speed, Vector2 tiling, float alpha, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(UVScroller));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 0);
            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            raw.color = WithA(tint, alpha);
            raw.uvRect = new Rect(0, 0, tiling.x, tiling.y);
            var so = new SerializedObject(go.GetComponent<UVScroller>());
            so.FindProperty("scrollSpeed").vector2Value = speed;
            so.FindProperty("tiling").vector2Value = tiling;
            so.FindProperty("baseAlpha").floatValue = alpha;
            so.FindProperty("alphaVariation").floatValue = alpha * 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Image.Type type, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.raycastTarget = raycast;
            return img;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static void Stretch(RectTransform rt, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private static void PlaceCorner(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
        private static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
