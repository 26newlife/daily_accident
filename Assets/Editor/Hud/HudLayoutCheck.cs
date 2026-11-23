using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameGameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI.EditorTools
{
    /// <summary>
    /// HUD 레이아웃 검증 도구 (Tools > GameUI > HUD).
    ///   · Check HUD Layout   : 해상도별로 HUD 의 모든 요소를 "동시에 가장 크게 표시된" 상태로 놓고 서로 겹침 / 화면 밖 잘림을 계산한다 (렌더링 불필요).
    ///   · Capture HUD Shots  : 같은 상태를 RenderTexture 로 렌더해 Docs/Screenshots/ 에 PNG 로 저장한다 (그래픽 장치 필요 — 배치모드에서도 -nographics 없이 가능).
    /// 캔버스 환산은 CanvasScaler(ScaleWithScreenSize, 기준 1920x1080, Expand) 공식과 같다. 씬은 저장하지 않는다.
    /// </summary>
    public static class HudLayoutCheck
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private static readonly Vector2 Reference = new Vector2(1920, 1080);

        public static readonly (string name, int w, int h)[] Resolutions =
        {
            ("1920x1080 (16:9)", 1920, 1080), ("1280x720 (16:9)", 1280, 720), ("1366x768", 1366, 768),
            ("1920x1200 (16:10)", 1920, 1200), ("1280x800 (16:10)", 1280, 800), ("1024x768 (4:3)", 1024, 768),
            ("2560x1080 (21:9)", 2560, 1080), ("3440x1440 (21:9)", 3440, 1440),
        };

        [MenuItem("Tools/GameUI/HUD/Check HUD Layout")]
        public static void CheckMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string report = Check();
            Debug.Log(report);
        }

        [MenuItem("Tools/GameUI/HUD/Capture HUD Shots")]
        public static void CaptureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Capture());
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.HudLayoutCheck.CheckBatch (환경변수 HUD_REPORT 로 결과 파일 지정)</summary>
        public static void CheckBatch()
        {
            string text; try { text = Check(); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("HUD_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            EditorApplication.Exit(text.Contains("FAIL") || text.StartsWith("오류") ? 1 : 0);
        }

        public static void CaptureBatch()
        {
            string text; try { text = Capture(); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("HUD_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            EditorApplication.Exit(text.StartsWith("오류") ? 1 : 0);
        }

        // ═════════════════════════ 공통: 최대 표시 상태 만들기 ═════════════════════════
        private sealed class Elem { public string name; public RectTransform rt; public bool belowHud; }

        private static Transform FindHud()
        {
            GameObject go = GameObject.Find("Gameplay/GameCanvas/HUD");
            if (go == null) throw new Exception("Gameplay/GameCanvas/HUD 를 찾지 못했습니다 (GameScene 을 열고 HUD 를 재구성하세요).");
            return go.transform;
        }

        /// <summary>생존 상태 패널에 최대 표시 상태(시나리오 1: 막대 4 + 줄 2)를 채운다. 씬 저장 안 함</summary>
        private static void FillStats(Transform statsRoot, bool war)
        {
            var panel = statsRoot.GetComponent<SurvivalStatsPanel>();
            var so = new SerializedObject(panel);
            var row = so.FindProperty("rowTemplate").objectReferenceValue as RectTransform;
            var line = so.FindProperty("lineTemplate").objectReferenceValue as TMP_Text;
            string[] labels = war ? new[] { "방사능", "쾌적도", "정신력", "체온" } : new[] { "호흡기", "화상" };
            foreach (string l in labels)
            {
                var r = UnityEngine.Object.Instantiate(row.gameObject, statsRoot, false); r.SetActive(true);
                r.transform.Find("Label").GetComponent<TMP_Text>().text = l;
                var fill = r.transform.Find("Bar/Fill").GetComponent<Image>(); fill.fillAmount = 0.62f; fill.color = new Color(0.9f, 0.6f, 0.25f);
                r.transform.Find("Value").GetComponent<TMP_Text>().text = "62";
            }
            if (war)
            {
                string[] lines = { "식량 7   물 6   약품 3   행동력 12", "기만 단서 3   동행 1   [방독면 착용]" };
                foreach (string s in lines) { var t = UnityEngine.Object.Instantiate(line.gameObject, statsRoot, false); t.SetActive(true); t.GetComponent<TMP_Text>().text = s; }
            }
            statsRoot.GetComponent<CanvasGroup>().alpha = 1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)statsRoot);
        }

        private static void ShowAll(Transform hud, bool war)
        {
            foreach (string n in new[] { "ObjectiveBanner", "Prompt", "HintToast", "Message", "Subtitle", "Scent" })
            {
                Transform t = hud.Find(n);
                if (t != null) { var cg = t.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1f; }
            }
            SetText(hud, "ObjectiveBanner/Text", "새 목표: 가스 밸브를 잠그고 환기를 시킨다");
            SetText(hud, "Prompt/Text", "[E] 전자레인지 열기  (길게 누르기)");
            SetText(hud, "HintToast/Text", "하얀 점이 커지면 상호작용할 수 있습니다.");
            SetText(hud, "Message", null);
            SetText(hud, "Message/Text", "수도꼭지를 틀었지만 물이 나오지 않는다. 단수다.");
            SetText(hud, "Subtitle/Text", "[긴급 방송] 주민 여러분께 알립니다. 현재 8층에서 화재가 발생했습니다. 침착하게 계단을 이용해 대피하십시오.");
            SetText(hud, "Scent/Text", "어디선가 가스 냄새가 난다…");
            var hold = hud.Find("HoldBar"); if (hold != null) { hold.GetComponent<CanvasGroup>().alpha = 1f; var f = hold.Find("Fill"); if (f != null) f.GetComponent<Image>().fillAmount = 0.6f; }
            FillStats(hud.Find("SurvivalStats"), war);
        }

        private static void SetText(Transform hud, string path, string text)
        {
            Transform t = hud.Find(path);
            if (t == null || text == null) return;
            var tmp = t.GetComponent<TMP_Text>(); if (tmp != null) tmp.text = text;
        }

        /// <summary>겹침 검사 대상 요소. 시나리오 1 재난문자 팝업은 런타임 생성이라 같은 앵커·크기로 대신 만든다 (WarHud.Build 와 일치해야 함)</summary>
        private static List<Elem> CollectElements(Transform hud, Transform canvas)
        {
            var list = new List<Elem>();
            foreach (string n in new[] { "SurvivalStats", "ObjectiveBanner", "Crosshair", "HoldBar", "Scent", "InventoryBar", "Prompt", "HintToast", "Message", "Subtitle" })
            {
                Transform t = hud.Find(n);
                if (t == null) throw new Exception("HUD 요소 없음: " + n);
                list.Add(new Elem { name = n, rt = (RectTransform)t, belowHud = true });
            }
            var popup = new GameObject("AlertPopup(WarHud)", typeof(RectTransform)).GetComponent<RectTransform>();
            popup.SetParent(hud, false);
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0.5f, 1f);
            popup.anchoredPosition = new Vector2(0f, -140f); popup.sizeDelta = new Vector2(700f, 190f);
            list.Add(new Elem { name = "AlertPopup(S01)", rt = popup, belowHud = true });

            Transform detail = canvas.Find("ObjectiveDetail");
            if (detail != null) list.Add(new Elem { name = "ObjectiveDetail(J)", rt = (RectTransform)detail, belowHud = false });
            return list;
        }

        // 요소 rect (캔버스 기준 좌표, 좌하단 원점). 앵커가 한 점인 요소만 지원 (HUD 요소는 모두 해당)
        private static Rect RectOf(RectTransform rt, Vector2 canvasSize)
        {
            Vector2 size = new Vector2(rt.rect.width > 0 ? rt.rect.width : rt.sizeDelta.x, rt.rect.height > 0 ? rt.rect.height : rt.sizeDelta.y);
            Vector2 anchor = new Vector2(Mathf.Lerp(0, canvasSize.x, rt.anchorMin.x), Mathf.Lerp(0, canvasSize.y, rt.anchorMin.y));
            Vector2 pos = anchor + rt.anchoredPosition;
            Vector2 min = pos - Vector2.Scale(rt.pivot, size);
            return new Rect(min, size);
        }

        private static Vector2 CanvasSize(int w, int h)
        {
            float scale = Mathf.Min(w / Reference.x, h / Reference.y);   // CanvasScaler Expand: 캔버스는 항상 기준 이상
            return new Vector2(w / scale, h / scale);
        }

        // ═════════════════════════ 겹침 검사 ═════════════════════════
        public static string Check()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Transform hud = FindHud();
            Transform canvas = hud.parent;
            var sb = new StringBuilder();
            int totalFail = 0;
            foreach (bool war in new[] { true, false })
            {
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                hud = FindHud(); canvas = hud.parent;
                ShowAll(hud, war);
                List<Elem> elems = CollectElements(hud, canvas);
                sb.AppendLine($"=== 프로필: {(war ? "시나리오 1 최대 표시 (생존 상태 막대 4 + 줄 2)" : "시나리오 3 최대 표시 (호흡기+화상)")} ===");
                foreach (var res in Resolutions)
                {
                    Vector2 cs = CanvasSize(res.w, res.h);
                    // 세이프 에어리어 가정: 좌우 2.5% / 상하 2.5% 안쪽 (콘솔 TV 기준)
                    float mx = cs.x * 0.025f, my = cs.y * 0.025f;
                    var rects = new List<(string n, Rect r)>();
                    foreach (Elem e in elems)
                    {
                        Rect r = RectOf(e.rt, cs);
                        // HUD 루트는 세이프 에어리어에 맞춰지므로 (세이프 에어리어 가정 시) 요소를 안쪽 영역 기준으로 다시 배치한다
                        if (e.belowHud) r = ApplyInset(e.rt, cs, mx, my);
                        rects.Add((e.name, r));
                    }
                    var fails = new List<string>();
                    for (int i = 0; i < rects.Count; i++)
                    {
                        Rect r = rects[i].r;
                        if (r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > cs.x + 0.5f || r.yMax > cs.y + 0.5f) fails.Add($"잘림: {rects[i].n} {Fmt(r)} (캔버스 {cs.x:0}x{cs.y:0})");
                        for (int j = i + 1; j < rects.Count; j++)
                            if (Overlaps(r, rects[j].r)) fails.Add($"겹침: {rects[i].n} × {rects[j].n}  {Fmt(r)} / {Fmt(rects[j].r)}");
                    }
                    totalFail += fails.Count;
                    sb.AppendLine($"  {(fails.Count == 0 ? "PASS" : "FAIL")}  {res.name,-20} 캔버스 {cs.x:0}x{cs.y:0}");
                    foreach (string f in fails) sb.AppendLine("        - " + f);
                }
                // 참고: 스마트폰 패널은 전체 화면 모달이라 검사 제외 (열려 있는 동안 HUD 와 같이 보일 수 있음)
            }
            sb.AppendLine($"== 결과: {(totalFail == 0 ? "ALL PASS (겹침·잘림 0건)" : "FAIL " + totalFail + "건")}");
            return sb.ToString();
        }

        /// <summary>세이프 에어리어(좌우 mx, 상하 my 안쪽)를 부모 영역으로 본 요소 rect</summary>
        private static Rect ApplyInset(RectTransform rt, Vector2 cs, float mx, float my)
        {
            Vector2 areaSize = new Vector2(cs.x - 2 * mx, cs.y - 2 * my);
            Rect local = RectOf(rt, areaSize);
            return new Rect(local.x + mx, local.y + my, local.width, local.height);
        }

        private static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax - 0.5f && a.xMax > b.xMin + 0.5f && a.yMin < b.yMax - 0.5f && a.yMax > b.yMin + 0.5f;
        private static string Fmt(Rect r) => $"[{r.xMin:0},{r.yMin:0}~{r.xMax:0},{r.yMax:0}]";

        // ═════════════════════════ 스크린샷 ═════════════════════════
        /// <summary>평소 숨겨진 패널(조작법 카드·스마트폰·일시정지·사망/엔딩·선택지·설정·목표 화살표·시작 안내)을 끄고, J 패널은 예시 문구로 연다</summary>
        private static readonly string NL = ((char)10).ToString();

        private static void PrepareForShot(Transform canvasT)
        {
            foreach (string n in new[] { "ControlsCard", "PhonePanel", "PauseMenu", "SettingsPopup", "DeathScreen", "EndingScreen", "ChoicePanel", "ObjectiveMarker", "ConfirmDialog" })
            { Transform t = canvasT.Find(n); if (t != null) t.gameObject.SetActive(false); }
            GameObject notice = GameObject.Find("NoticeRoot"); if (notice != null) notice.SetActive(false);
            Transform detail = canvasT.Find("ObjectiveDetail");
            if (detail != null)
            {
                detail.GetComponent<CanvasGroup>().alpha = 1f;
                SetText(detail, "Title", "현재 목표");
                SetText(detail, "Body", "<b>● 가스 밸브를 잠근다</b>" + NL + "<color=#B8C4D0>다용도실의 메인 밸브를 시계 방향으로 돌려 잠근다.</color>" + NL + NL + "<b>○ 환기를 시킨다</b>" + NL + "<color=#B8C4D0>거실 창문을 열어 가스를 빼낸다.</color>" + NL + NL + "<s><alpha=#88>알람 끄기</s>");
            }
        }

        public static string Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Screenshots"));
            string env = Environment.GetEnvironmentVariable("HUD_SHOT_DIR"); if (!string.IsNullOrEmpty(env)) dir = env;
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            string tag = Environment.GetEnvironmentVariable("HUD_SHOT_TAG") ?? "hud";
            foreach (bool war in new[] { true, false })
            {
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                Transform hud = FindHud(); Transform canvasT = hud.parent;
                ShowAll(hud, war);
                var canvas = canvasT.GetComponent<Canvas>();
                PrepareForShot(canvasT);
                Camera view = null;
                foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) if (c.name == "PlayerCamera") view = c;
                if (view == null) throw new Exception("PlayerCamera 를 찾지 못했습니다.");
                view.enabled = true;
                foreach (var res in new[] { Resolutions[0], Resolutions[1], Resolutions[3], Resolutions[6] })
                {
                    var rt = new RenderTexture(res.w, res.h, 24, RenderTextureFormat.ARGB32);
                    // 3D 뷰와 UI 를 한 카메라로 한 번에 렌더한다 (URP 는 Camera.Render() 대신 렌더 요청을 쓴다)
                    view.targetTexture = rt;
                    view.aspect = (float)res.w / res.h;   // 배치모드에서는 카메라 비율이 임시 화면 비율로 고정돼 있어 RT 비율로 직접 지정한다
                    // 배치모드의 임시 화면 크기가 ScreenSpace 캔버스를 잘라내므로, 캔버스를 월드 공간에서 카메라 앞에 화면과 정확히 맞춰 놓는다
                    // (Expand 와 같은 배율: 캔버스 = 화면 / min(w/1920, h/1080))
                    float scale = Mathf.Min(res.w / Reference.x, res.h / Reference.y);
                    var scaler = canvasT.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
                    canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view;
                    var crt = (RectTransform)canvasT;
                    crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.sizeDelta = new Vector2(res.w / scale, res.h / scale);
                    const float d = 1f;
                    // 카메라의 "실제" 시야 사각형을 직접 잰다 (물리 카메라·게이트 핏 등으로 fov/aspect 계산이 어긋날 수 있다)
                    Vector3 bl = view.ViewportToWorldPoint(new Vector3(0, 0, d)), tr = view.ViewportToWorldPoint(new Vector3(1, 1, d));
                    Vector3 sz = Quaternion.Inverse(view.transform.rotation) * (tr - bl);
                    crt.localScale = new Vector3(Mathf.Abs(sz.x) / crt.sizeDelta.x, Mathf.Abs(sz.y) / crt.sizeDelta.y, 1f);
                    sb.AppendLine($"[진단] 시야 사각형 {Mathf.Abs(sz.x):0.0000} x {Mathf.Abs(sz.y):0.0000} (비 {Mathf.Abs(sz.x / sz.y):0.000}, RT 비 {(float)res.w / res.h:0.000}) physical={view.usePhysicalProperties}");
                    crt.position = view.transform.position + view.transform.forward * d;
                    crt.rotation = view.transform.rotation;
                    Canvas.ForceUpdateCanvases();
                    foreach (var tmp in canvasT.GetComponentsInChildren<TMP_Text>(true)) tmp.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();
                    sb.AppendLine($"[진단] RT {rt.width}x{rt.height} Screen {Screen.width}x{Screen.height} cam.rect {view.rect} pixelRect {view.pixelRect} pixelW {view.pixelWidth} aspect {view.aspect} fov {view.fieldOfView} canvasSize {crt.sizeDelta} scale {crt.localScale.x:0.00000}");
                    var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                    if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(view, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(view, req);
                    else view.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(res.w, res.h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, res.w, res.h), 0, 0); tex.Apply();
                    RenderTexture.active = null;
                    string file = Path.Combine(dir, $"{tag}_{(war ? "s01" : "s03")}_{res.w}x{res.h}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    sb.AppendLine("저장: " + file + (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(view, req) ? "" : "  (render request 미지원 → Camera.Render)"));
                    view.targetTexture = null;
                    view.ResetAspect();
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                }
            }
            return sb.ToString();
        }
    }
}
