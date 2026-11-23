using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// 방별 프리뷰 캡처 + 정적 씬 통계 (Tools > GameUI > Art).
    ///   · Capture Room Previews : 방마다 플레이어 눈높이 시점 / 대각선 시점 PNG 를 Docs/Screenshots/ 에 저장한다.
    ///     파일명 {태그}_{방}_{player|diag}.png — 태그는 환경변수 ROOM_SHOT_TAG(기본 "before") 또는 메뉴에서 입력.
    ///   · Scene Stats           : 활성 렌더러·삼각형·머티리얼 종류·조명 수를 센다 (프레임 시간은 장치 의존이라 별도 측정).
    /// 시점은 집 좌표(Docs/BaseHouseSpec.md)에 고정이라 업그레이드 전/후를 같은 구도로 비교할 수 있다.
    /// UI·플레이어 캡슐은 끄고 3D 장면만 찍는다. 씬은 저장하지 않는다.
    /// </summary>
    public static class RoomPreviewCapture
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private struct Shot { public string room, kind; public Vector3 pos, look; public float fov; }

        // 눈높이 1.6m(player) / 모서리 위에서 내려다보는 시점(diag). 좌표는 월드(m)
        private static readonly Shot[] Shots =
        {
            new Shot { room = "bedroom",  kind = "player", pos = new Vector3(2.0f, 1.6f, 3.75f), look = new Vector3(0.9f, 1.0f, 6.6f), fov = 75 },
            new Shot { room = "bedroom",  kind = "diag",   pos = new Vector3(2.4f, 2.25f, 2.55f), look = new Vector3(0.7f, 0.5f, 6.7f), fov = 75 },
            new Shot { room = "living",   kind = "player", pos = new Vector3(3.0f, 1.6f, 4.8f), look = new Vector3(5.2f, 1.0f, 6.8f), fov = 75 },
            new Shot { room = "living",   kind = "diag",   pos = new Vector3(2.9f, 2.3f, 4.7f), look = new Vector3(5.8f, 0.5f, 6.9f), fov = 75 },
            new Shot { room = "kitchen",  kind = "player", pos = new Vector3(3.95f, 1.6f, 3.3f), look = new Vector3(6.3f, 1.0f, 3.5f), fov = 75 },
            new Shot { room = "kitchen",  kind = "diag",   pos = new Vector3(5.4f, 2.25f, 4.45f), look = new Vector3(6.2f, 0.9f, 3.1f), fov = 75 },
            new Shot { room = "utility",  kind = "player", pos = new Vector3(5.0f, 1.6f, 2.25f), look = new Vector3(6.2f, 1.0f, 0.6f), fov = 75 },
            new Shot { room = "bathroom", kind = "player", pos = new Vector3(1.6f, 1.6f, 2.2f), look = new Vector3(0.6f, 1.0f, 0.6f), fov = 75 },
            new Shot { room = "entry",    kind = "player", pos = new Vector3(3.0f, 1.6f, 2.3f), look = new Vector3(3.0f, 1.1f, 0.2f), fov = 75 },
            new Shot { room = "entry",    kind = "diag",   pos = new Vector3(2.0f, 2.2f, 2.3f), look = new Vector3(3.7f, 0.5f, 0.4f), fov = 75 },
            new Shot { room = "balcony",  kind = "player", pos = new Vector3(4.2f, 1.6f, 7.8f), look = new Vector3(1.0f, 1.3f, 8.5f), fov = 75 },
            new Shot { room = "shelter",  kind = "player", pos = new Vector3(5.6f, 1.6f, 8.0f), look = new Vector3(6.5f, 0.8f, 8.7f), fov = 75 },
            new Shot { room = "corridor", kind = "player", pos = new Vector3(1.0f, 1.6f, -1.0f), look = new Vector3(-3.0f, 1.3f, -1.4f), fov = 75 },
        };

        [MenuItem("Tools/GameUI/Art/Capture Room Previews (before)")]
        public static void CaptureBeforeMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Debug.Log(Capture("before", 1280, 720)); }

        [MenuItem("Tools/GameUI/Art/Capture Room Previews (after)")]
        public static void CaptureAfterMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Debug.Log(Capture("after", 1280, 720)); }

        [MenuItem("Tools/GameUI/Art/Scene Stats")]
        public static void StatsMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Debug.Log(Stats()); }

        /// <summary>배치모드: ROOM_SHOT_TAG / ROOM_SHOT_DIR / ROOM_REPORT 환경변수. -nographics 없이 실행해야 한다</summary>
        public static void CaptureBatch()
        {
            string tag = Environment.GetEnvironmentVariable("ROOM_SHOT_TAG") ?? "before";
            string text; try { text = Capture(tag, 1280, 720) + "\n" + Stats(); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("ROOM_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            EditorApplication.Exit(text.StartsWith("오류") ? 1 : 0);
        }

        public static void StatsBatch()
        {
            string text; try { text = Stats(); } catch (Exception e) { text = "오류: " + e; }
            string path = Environment.GetEnvironmentVariable("ROOM_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text, Encoding.UTF8);
            EditorApplication.Exit(text.StartsWith("오류") ? 1 : 0);
        }

        // ═════════════════════════ 캡처 ═════════════════════════
        public static string Capture(string tag, int w, int h)
        {
            string dir = Environment.GetEnvironmentVariable("ROOM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Screenshots"));
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // 3D 장면만: UI 와 플레이어(캡슐·카메라) 끄기
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)) c.gameObject.SetActive(false);
            GameObject player = GameObject.Find("Gameplay/Player"); if (player != null) player.SetActive(false);
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) c.enabled = false;

            // 환경변수 ROOM_LIGHTS=on: 모든 실내등을 켠 상태로 찍는다 (게임 시작 상태는 방 불이 꺼져 있어 재질 비교가 어렵다)
            if (Environment.GetEnvironmentVariable("ROOM_LIGHTS") == "on")
                foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) { l.gameObject.SetActive(true); l.enabled = true; }

            var camGo = new GameObject("RoomPreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f; cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;   // 게임과 같은 후처리 적용 (태그 before 는 기존 상태 기록용이라 후처리 없이 찍힌다)
            if (tag == "before") urp.renderPostProcessing = false;

            var sb = new StringBuilder();
            foreach (Shot s in Shots)
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
                string file = Path.Combine(dir, $"{tag}_{s.room}_{s.kind}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                sb.AppendLine("저장: " + file);
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            UnityEngine.Object.DestroyImmediate(camGo);
            return sb.ToString();
        }

        // ═════════════════════════ 정적 통계 ═════════════════════════
        public static string Stats()
        {
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            long tris = 0; int renderers = 0, skinned = 0, particles = 0;
            var mats = new HashSet<Material>(); var meshes = new HashSet<Mesh>();
            int shadowCasters = 0; long houseTris = 0, outdoorTris = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Mesh m = null;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (mf != null) m = mf.sharedMesh; renderers++; }
                else if (r is SkinnedMeshRenderer sm) { m = sm.sharedMesh; skinned++; }
                else if (r is ParticleSystemRenderer) { particles++; continue; }
                else continue;
                if (m == null) continue;
                long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
                tris += t; meshes.Add(m);
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadowCasters++;
                foreach (Material mat in r.sharedMaterials) if (mat != null) mats.Add(mat);
                string path = GetPath(r.transform);
                if (path.Contains("OutdoorWorld")) outdoorTris += t; else houseTris += t;
            }
            int lights = 0, realtime = 0, shadowLights = 0, probes = 0;
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            { if (!l.enabled || !l.gameObject.activeInHierarchy) continue; lights++; if (l.lightmapBakeType == LightmapBakeType.Realtime) realtime++; if (l.shadows != LightShadows.None) shadowLights++; }
            probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude).Length;
            var sb = new StringBuilder();
            sb.AppendLine("== 정적 씬 통계 (GameScene, 활성 오브젝트 기준) ==");
            sb.AppendLine($"렌더러(MeshRenderer) {renderers} / 스킨드 {skinned} / 파티클 {particles}");
            sb.AppendLine($"삼각형 합계 {tris:N0}  (집 {houseTris:N0} / 창밖 {outdoorTris:N0})");
            sb.AppendLine($"고유 메시 {meshes.Count} / 고유 머티리얼 {mats.Count} / 그림자 드리우는 렌더러 {shadowCasters}");
            sb.AppendLine($"조명 {lights} (실시간 {realtime}, 그림자 {shadowLights}) / 반사 프로브 {probes}");
            sb.AppendLine("※ 드로우콜·프레임 시간은 장치 의존 — Unity Game 뷰의 Stats 로 직접 확인");
            return sb.ToString();
        }

        private static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
