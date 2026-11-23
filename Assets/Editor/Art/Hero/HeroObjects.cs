using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using M = GameUI.EditorTools.Interior.InteriorMaterialFactory;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// Hero 상호작용 오브젝트 (③단계): 기존 회색 박스 대신 베벨·부품이 있는 절차적 모델을 얹는다.
    /// 규칙 — 박스 콜라이더·id·컴포넌트·위치·크기(경계)는 그대로. 겉모습만 자식 "HeroVisual" 로 추가하고
    /// 기존 박스(Visual)의 렌더러만 끈다. 모델은 박스 경계 안에 들어가며, 손잡이 등 앞으로 튀는 부분은 1.5cm 이내.
    /// 호출 위치: BaseHouseFixups.Apply (빌더가 다시 돌아도 유지) / 메뉴 Tools > GameUI > Art > Hero.
    /// </summary>
    public static class HeroObjects
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        // ═════════════════════════ 재질 ═════════════════════════
        private static Material Mat(string name, string hex, float smooth, float metal = 0f, string emissionHex = null, float emissionBoost = 1f, bool transparent = false, float alpha = 1f)
        {
            M.EnsureFolder(M.MaterialDir);
            string path = $"{M.MaterialDir}/Int_Hero_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(sh) { name = "Int_Hero_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            Color c = M.Hex(hex); c.a = alpha;
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (emissionHex != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", M.Hex(emissionHex) * emissionBoost);
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f); mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material PlasticAged => Mat("PlasticAged", "D8C79C", 0.38f);          // 오래 써서 누렇게 변한 멀티탭 몸체
        private static Material OutletFace => Mat("OutletFace", "CFC8B4", 0.3f);
        private static Material OutletDark => Mat("OutletDark", "1B1A19", 0.25f);
        private static Material Scorch => Mat("Scorch", "3A2A1E", 0.12f);                   // 과열로 그을린 자국
        private static Material LedRed => Mat("LedRed", "B3141A", 0.7f, 0f, "FF2A22", 1.5f);  // 전원 스위치 렌즈(켜짐)
        private static Material Brass => Mat("Brass", "C9A04A", 0.75f, 0.9f);
        private static Material DisplayGlow => Mat("DisplayGlow", "0C1C1A", 0.9f, 0f, "55FFD0", 0.9f);
        private static Material ButtonGrey => Mat("ButtonGrey", "4A4D53", 0.35f);
        private static Material PlugGrey => Mat("PlugGrey", "B9B7B0", 0.4f);

        // ═════════════════════════ 전자레인지 ═════════════════════════
        // 로컬: 중심 원점, +Z = 앞면, 가로 0.40(X) × 높이 0.28(Y) × 깊이 0.40(Z) — 기존 박스 경계 안. 문(왼쪽) + 조작부(오른쪽)
        private static string BuildMicrowave()
        {
            var b = new HeroBuilder();
            Material steel = M.Metal, dark = M.MetalDark, black = M.PlasticBlack, glass = M.Screen, chrome = M.Chrome, rubber = M.Rubber;

            b.Box(steel, new Vector3(0f, 0.004f, -0.028f), new Vector3(0.400f, 0.268f, 0.344f), 0.012f);   // 본체 (z -0.200 ~ 0.144)
            b.Box(dark, new Vector3(0f, 0.004f, 0.1585f), new Vector3(0.400f, 0.268f, 0.029f), 0.008f);      // 전면 패널 (~0.173)
            // 문: 프레임 + 유리 + 손잡이
            b.Box(dark, new Vector3(-0.054f, 0f, 0.1775f), new Vector3(0.272f, 0.218f, 0.012f), 0.006f);     // 문틀 (~0.1835)
            b.Box(glass, new Vector3(-0.054f, 0f, 0.1835f), new Vector3(0.238f, 0.180f, 0.004f), 0.004f);    // 어두운 유리 창 (~0.1855)
            b.Box(dark, new Vector3(-0.054f, 0f, 0.1845f), new Vector3(0.246f, 0.188f, 0.002f), 0.002f, default, 1);   // 유리 안쪽 테두리 (그림자 선)
            for (int i = -1; i <= 1; i += 2)   // 손잡이 받침 2개 + 손잡이 막대 (문 오른쪽 가장자리)
                b.Cyl(chrome, new Vector3(0.071f, i * 0.058f, 0.1895f), 0.0035f, 0.012f, new Vector3(90f, 0f, 0f), 0.0008f, 10);
            b.Cyl(chrome, new Vector3(0.071f, 0f, 0.1965f), 0.0048f, 0.17f, default, 0.002f, 12);
            // 조작부
            b.Box(black, new Vector3(0.1405f, 0.002f, 0.1775f), new Vector3(0.094f, 0.222f, 0.012f), 0.005f);   // 패널 판
            b.Box(DisplayGlow, new Vector3(0.1405f, 0.074f, 0.1845f), new Vector3(0.070f, 0.028f, 0.002f), 0.002f, default, 1);   // 디스플레이
            for (int k = 0; k < 4; k++)   // 숫자 자리 (밝은 막대)
                b.Box(DisplayGlow, new Vector3(0.1215f + k * 0.0125f + (k >= 2 ? 0.006f : 0f), 0.074f, 0.1858f), new Vector3(0.007f, 0.016f, 0.0008f), 0.0004f, default, 1);
            for (int k = 0; k < 3; k++)   // 버튼 3개
                b.Box(ButtonGrey, new Vector3(0.1405f, 0.032f - k * 0.027f, 0.1865f), new Vector3(0.066f, 0.017f, 0.006f), 0.003f, default, 1);
            b.Cyl(steel, new Vector3(0.1405f, -0.080f, 0.1895f), 0.0215f, 0.014f, new Vector3(90f, 0f, 0f), 0.0025f, 20);   // 다이얼
            b.Box(dark, new Vector3(0.1405f, -0.0705f, 0.1980f), new Vector3(0.003f, 0.012f, 0.0016f), 0.0006f, default, 1);   // 다이얼 지시선
            // 윗면 통풍 슬릿, 고무발
            for (int k = 0; k < 6; k++)
                b.Box(black, new Vector3(-0.115f, 0.1387f, -0.12f + k * 0.012f), new Vector3(0.090f, 0.0016f, 0.0055f), 0.0006f, default, 1);
            foreach (float x in new[] { -0.165f, 0.165f })
                foreach (float z in new[] { -0.15f, 0.12f })
                    b.Cyl(rubber, new Vector3(x, -0.134f, z), 0.012f, 0.008f, default, 0.001f, 10);
            return b.Save("Hero_Microwave");
        }

        // ═════════════════════════ 멀티탭 (문어발) ═════════════════════════
        // 로컬: 중심 = 몸체 중심, X = 길이 0.35, Y 높이 0.05, Z 폭 0.10. 오른쪽 끝(+X)에 전원 스위치(별도 Hero)
        private static string BuildMultiTap()
        {
            var b = new HeroBuilder();
            Material body = PlasticAged, face = OutletFace, hole = OutletDark, scorch = Scorch;
            b.Box(body, Vector3.zero, new Vector3(0.350f, 0.050f, 0.098f), 0.013f, default, 3);
            float top = 0.025f;
            float[] xs = { -0.138f, -0.083f, -0.028f, 0.027f };
            foreach (float x in xs)
            {
                b.Box(hole, new Vector3(x, top, 0f), new Vector3(0.046f, 0.0012f, 0.046f), 0.004f, default, 1);          // 홈
                b.Box(face, new Vector3(x, top + 0.0007f, 0f), new Vector3(0.040f, 0.0012f, 0.040f), 0.004f, default, 1);  // 콘센트 면
                foreach (float dz in new[] { -0.0095f, 0.0095f })
                    b.Cyl(hole, new Vector3(x, top + 0.0016f, dz), 0.0046f, 0.0008f, default, 0.0003f, 10);                  // 구멍 2개
            }
            // 그을린 자국 (콘센트 옆)
            b.Box(scorch, new Vector3(-0.055f, top + 0.0003f, 0.036f), new Vector3(0.030f, 0.0006f, 0.014f), 0.0005f, default, 1);
            b.Box(scorch, new Vector3(-0.108f, top + 0.0003f, -0.038f), new Vector3(0.022f, 0.0006f, 0.011f), 0.0005f, default, 1);
            b.Box(scorch, new Vector3(0.060f, top + 0.0003f, 0.034f), new Vector3(0.018f, 0.0006f, 0.010f), 0.0005f, default, 1);
            // 꽂힌 플러그 3개 + 늘어진 전선 (문어발)
            Material[] plugMat = { M.PlasticBlack, M.Plastic, PlugGrey };
            Material[] cordMat = { M.PlasticBlack, M.Plastic, M.PlasticBlack };
            float[] cordZ = { 0.15f, 0.12f, 0.17f };
            for (int i = 0; i < 3; i++)
            {
                float x = xs[i];
                b.Box(plugMat[i], new Vector3(x, top + 0.015f, 0f), new Vector3(0.034f, 0.030f, 0.036f), 0.006f);
                b.Cyl(M.Rubber, new Vector3(x, top + 0.033f, 0.012f), 0.0055f, 0.012f, new Vector3(60f, 0f, 0f), 0.001f, 8);   // 전선 보호대
                var path = HeroMeshKit.Smooth(new[]
                {
                    new Vector3(x, top + 0.036f, 0.016f), new Vector3(x + 0.004f, top + 0.034f, 0.034f),
                    new Vector3(x + 0.010f * (i - 1), -0.012f, 0.070f), new Vector3(x + 0.020f * (i - 1), -0.022f, cordZ[i]),
                }, 5);
                b.Tube(cordMat[i], path, 0.0032f, 7);
            }
            return b.Save("Hero_MultiTap");
        }

        // 전원 스위치 (로컬 원점 = multitap_switch 박스 중심; 몸체 윗면 = y -0.010)
        private static string BuildMultiTapSwitch()
        {
            var b = new HeroBuilder();
            b.Box(M.PlasticBlack, new Vector3(0f, -0.0075f, 0f), new Vector3(0.058f, 0.006f, 0.046f), 0.003f, default, 1);
            b.Box(LedRed, new Vector3(0f, 0.0005f, 0f), new Vector3(0.046f, 0.012f, 0.032f), 0.004f, new Vector3(0f, 0f, 6f), 2);
            return b.Save("Hero_MultiTapSwitch");
        }

        // 코드 끝 플러그 (로컬 원점 = multitap_plug 박스 중심, 핀이 -X 쪽)
        private static string BuildPowerPlug()
        {
            var b = new HeroBuilder();
            b.Box(M.PlasticBlack, Vector3.zero, new Vector3(0.046f, 0.034f, 0.040f), 0.008f);
            for (int i = 0; i < 3; i++)   // 손잡이 홈 (위·아래 가로 줄)
                b.Box(M.Rubber, new Vector3(-0.004f + i * 0.0105f, 0.0172f, 0f), new Vector3(0.004f, 0.0012f, 0.030f), 0.0005f, default, 1);
            foreach (float z in new[] { -0.0095f, 0.0095f })
                b.Cyl(Brass, new Vector3(-0.034f, 0f, z), 0.0026f, 0.022f, new Vector3(0f, 0f, 90f), 0.0006f, 10);   // 핀 2개
            b.Cyl(M.Rubber, new Vector3(0.0245f, 0f, 0f), 0.0075f, 0.008f, new Vector3(0f, 0f, 90f), 0.001f, 10);     // 코드 보호대
            return b.Save("Hero_PowerPlug");
        }

        // ═════════════════════════ 프리팹 만들기 ═════════════════════════
        [MenuItem("Tools/GameUI/Art/Hero/Rebuild Hero Prefabs (Microwave, MultiTap)")]
        public static void RebuildMenu() { Debug.Log(RebuildPrefabs()); }

        public static string RebuildPrefabs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Hero] " + BuildMicrowave());
            sb.AppendLine("[Hero] " + BuildMultiTap());
            sb.AppendLine("[Hero] " + BuildMultiTapSwitch());
            sb.AppendLine("[Hero] " + BuildPowerPlug());
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            return sb.ToString();
        }

        private static GameObject LoadHero(string name)
        {
            string path = $"{HeroBuilder.HeroDir}/{name}.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { RebuildPrefabs(); go = AssetDatabase.LoadAssetAtPath<GameObject>(path); }   // 없으면 만든다
            return go;
        }

        // ═════════════════════════ 집에 적용 ═════════════════════════
        /// <summary>houseRoot = Apartment_1004_Base. 여러 번 실행해도 같은 결과 (HeroVisual 을 지우고 다시 붙인다)</summary>
        public static int Apply(Transform houseRoot)
        {
            int n = 0;
            n += Attach(houseRoot, "Level/Interactables/microwave", "Hero_Microwave", Quaternion.Euler(0f, 270f, 0f));   // 앞면이 서쪽(-X)을 본다
            n += Attach(houseRoot, "Level/Interactables/multitap_switch", "Hero_MultiTapSwitch", Quaternion.identity);
            n += Attach(houseRoot, "Level/Interactables/multitap_plug", "Hero_PowerPlug", Quaternion.identity);
            // 멀티탭 몸체: 소품 박스(Level/Furniture/Multitap_Body, 크기 스케일이 있어 자식으로 못 붙임) 옆에 형제로
            Transform body = houseRoot.Find("Level/Furniture/Multitap_Body");
            if (body == null) Debug.LogWarning("[Hero] Multitap_Body 를 찾지 못했습니다.");
            else
            {
                HideBox(body);
                GameObject hero = LoadHero("Hero_MultiTap");
                Transform old = body.parent.Find("HeroVisual_MultiTap");
                if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) != hero)
                { UnityEngine.Object.DestroyImmediate(old.gameObject); old = null; }
                if (old == null)
                {
                    GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(hero, body.parent);
                    inst.name = "HeroVisual_MultiTap";
                    old = inst.transform;
                }
                old.position = body.position; old.rotation = Quaternion.identity;
                n++;
            }
            // 기존 장식(전자레인지 유리/패널/손잡이)은 Hero 모델에 포함됐으므로 끈다 (BaseHouseFixups 위치 보정이 찾을 수 있게 삭제하지 않고 비활성)
            foreach (string part in new[] { "MicrowaveGlass", "MicrowavePanel", "MicrowaveHandle" })
            {
                Transform t = houseRoot.Find("Level/Interior/Kitchen/" + part);
                if (t != null) t.gameObject.SetActive(false);
            }
            return n;
        }

        private static int Attach(Transform root, string path, string heroName, Quaternion localRot)
        {
            Transform t = root.Find(path);
            if (t == null) { Debug.LogWarning("[Hero] 대상을 찾지 못했습니다: " + path); return 0; }
            HideBox(t.Find("Visual"));
            GameObject hero = LoadHero(heroName);
            Transform old = t.Find("HeroVisual");
            if (old != null)
            {
                // 이미 같은 Hero 프리팹이면 그대로 둔다 (집 프리팹에서 상속된 인스턴스는 씬에서 지울 수 없다)
                if (PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == hero)
                {
                    old.localPosition = Vector3.zero; old.localRotation = localRot; old.localScale = Vector3.one;
                    return 1;
                }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(hero, t);
            inst.name = "HeroVisual";
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = localRot; inst.transform.localScale = Vector3.one;
            return 1;
        }

        private static void HideBox(Transform box)
        {
            if (box == null) return;
            var mr = box.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;   // 콜라이더·컴포넌트는 그대로
        }

        // ═════════════════════════ 캡처 (Docs/Screenshots/hero_*.png) ═════════════════════════
        private struct HShot { public string name; public Vector3 pos, look; public float fov; }
        private static readonly HShot[] Shots =
        {
            new HShot { name = "microwave_front", pos = new Vector3(5.40f, 1.18f, 3.45f), look = new Vector3(6.30f, 1.04f, 3.45f), fov = 38 },
            new HShot { name = "microwave_angle", pos = new Vector3(5.55f, 1.50f, 2.90f), look = new Vector3(6.28f, 1.04f, 3.45f), fov = 38 },
            new HShot { name = "microwave_room",  pos = new Vector3(4.05f, 1.55f, 3.00f), look = new Vector3(6.30f, 1.00f, 3.55f), fov = 60 },
            new HShot { name = "multitap_top",    pos = new Vector3(3.00f, 0.62f, 4.25f), look = new Vector3(3.00f, 0.06f, 4.65f), fov = 40 },
            new HShot { name = "multitap_angle",  pos = new Vector3(3.55f, 0.38f, 4.15f), look = new Vector3(2.95f, 0.06f, 4.65f), fov = 42 },
            new HShot { name = "multitap_room",   pos = new Vector3(4.40f, 1.40f, 5.60f), look = new Vector3(3.00f, 0.15f, 4.65f), fov = 60 },
        };

        [MenuItem("Tools/GameUI/Art/Hero/Capture Hero Shots (after)")]
        public static void CaptureAfterMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Debug.Log(Capture("after", 1280, 720)); }

        /// <summary>배치모드: HERO_TAG(before|after), HERO_REPORT, ROOM_SHOT_DIR 환경변수. -nographics 없이 실행</summary>
        public static void CaptureBatch()
        {
            string tag = Environment.GetEnvironmentVariable("HERO_TAG") ?? "after";
            string text; try { text = Capture(tag, 1280, 720); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("HERO_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            EditorApplication.Exit(text.StartsWith("오류") ? 1 : 0);
        }

        /// <summary>배치모드: Hero 프리팹을 만들고 집 프리팹 + GameScene 에 적용한다 (BaseHouseFixups.ApplyToAssets 가 Hero 도 적용)</summary>
        public static void RebuildBatch()
        {
            string text; try { text = RebuildPrefabs(); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("HERO_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            if (text.StartsWith("오류")) EditorApplication.Exit(1);
        }

        public static string Capture(string tag, int w, int h)
        {
            string dir = Environment.GetEnvironmentVariable("ROOM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Screenshots"));
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)) c.gameObject.SetActive(false);
            GameObject player = GameObject.Find("Gameplay/Player"); if (player != null) player.SetActive(false);
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) c.enabled = false;
            // 시작 상태는 방 불이 꺼져 있어 모델을 비교하기 어렵다 → 모든 실내등을 켠다
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) { l.gameObject.SetActive(true); l.enabled = true; }

            var camGo = new GameObject("HeroShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 100f; cam.allowHDR = true; cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var sb = new StringBuilder();
            foreach (HShot s in Shots)
            {
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt; cam.aspect = (float)w / h; cam.fieldOfView = s.fov;
                camGo.transform.position = s.pos;
                camGo.transform.rotation = Quaternion.LookRotation((s.look - s.pos).normalized, Vector3.up);
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = null;
                string file = Path.Combine(dir, $"hero_{tag}_{s.name}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                sb.AppendLine("저장: " + file);
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(camGo);
            return sb.ToString();
        }
    }
}
