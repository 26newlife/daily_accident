using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUI.EditorTools
{
    /// <summary>
    /// [디버그 도구] 씬 계층 덤프 (베이스 집 통일 점검에서 만들어 유지).
    /// GameScene(베이스 집) 과 시나리오 씬(S01/S03, Additive)을 각각 텍스트로 덤프한다.
    /// 한 줄 = 한 오브젝트: 경로 | 활성 | 프리팹 | 월드 위치/회전/스케일 | 콜라이더 | 머티리얼 | 상호작용 id | 조명 | 앵커/스폰/구역 id.
    /// 읽기 전용(씬 저장 안 함). 두 시나리오 오버레이를 비교할 때 쓴다. 배치모드: -executeMethod GameUI.EditorTools.SceneReportDump.RunBatch
    /// 결과: 프로젝트 루트/Logs/SceneReport/{base,s01,s03,meta}.txt
    /// </summary>
    public static class SceneReportDump
    {
        private const string BaseScene = "Assets/Scenes/GameScene.unity";
        private const string S01Scene = "Assets/Scenarios/S01_WarTime/Scenes/S01_WarTime.unity";
        private const string S03Scene = "Assets/Scenarios/S03_ApartmentFire/Scenes/S03_ApartmentFire.unity";

        [MenuItem("Tools/GameUI/Debug/Dump Scene Report")]
        public static void DumpMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "SceneReport"));
            Run(dir);
            EditorUtility.RevealInFinder(dir);
        }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.SceneReportDump.RunBatch</summary>
        public static void RunBatch()
        {
            string dir = Environment.GetEnvironmentVariable("SCENE_REPORT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "SceneReport"));
            try { Run(dir); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Scene b = EditorSceneManager.OpenScene(BaseScene, OpenSceneMode.Single);
            Write(Path.Combine(dir, "base.txt"), DumpScene(b));
            File.WriteAllText(Path.Combine(dir, "meta.txt"), DumpMeta(b), Encoding.UTF8);
            foreach (var pair in new[] { ("s01", S01Scene), ("s03", S03Scene) })
            {
                Scene s = EditorSceneManager.OpenScene(pair.Item2, OpenSceneMode.Additive);
                Write(Path.Combine(dir, pair.Item1 + ".txt"), DumpScene(s));
                EditorSceneManager.CloseScene(s, true);
            }
            Debug.Log("[SceneReport] 완료: " + dir);
        }

        private static void Write(string path, string text) => File.WriteAllText(path, text, Encoding.UTF8);

        // ───────── 메타 (플레이어·카메라·설정·통로 검증) ─────────
        private static string DumpMeta(Scene baseScene)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Player / Camera / Settings (serialized scalars)");
            foreach (GameObject root in baseScene.GetRootGameObjects())
                foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (m == null) continue;
                    string tn = m.GetType().Name;
                    if (tn != "PlayerController" && tn != "PlayerInteractor" && tn != "PlayerVitals" && tn != "HouseController") continue;
                    sb.AppendLine($"[{tn}] {Rel(m.transform)}");
                    var so = new SerializedObject(m); var it = so.GetIterator();
                    while (it.NextVisible(true))
                        if (it.propertyType == SerializedPropertyType.Float || it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Boolean || it.propertyType == SerializedPropertyType.Vector3)
                            sb.AppendLine($"  {it.propertyPath} = {Val(it)}");
                }
            foreach (CharacterController cc in UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include))
                sb.AppendLine($"[CharacterController] {Rel(cc.transform)} r={cc.radius} h={cc.height} step={cc.stepOffset} slope={cc.slopeLimit} center={cc.center}");
            foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                sb.AppendLine($"[Camera] {Rel(c.transform)} localPos={c.transform.localPosition} fov={c.fieldOfView}");

            sb.AppendLine().AppendLine("## Validate Apartment Clearance");
            try { sb.AppendLine(Interior.ApartmentClearanceValidator.RunAndSummarize()); }
            catch (Exception e) { sb.AppendLine("검증 실패: " + e.Message); }

            sb.AppendLine().AppendLine("## 집(Apartment_1004_Base)에 FireHazardProp 태그가 붙은 화재용 오브젝트 (useFireProps=false 시 숨김)");
            foreach (FireHazardProp p in FireHazardProp.FindAllIncludingInactive())
                sb.AppendLine($"  {p.name}  {Rel(p.transform)}");
            return sb.ToString();
        }

        private static string Val(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.###");
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("0.###");
                default: return "?";
            }
        }

        // ───────── 계층 덤프 ─────────
        private static string DumpScene(Scene scene)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# scene: " + scene.path);
            var ids = new Dictionary<string, List<string>>();
            foreach (GameObject root in scene.GetRootGameObjects()) Visit(root.transform, sb, ids);
            sb.AppendLine().AppendLine("## interactable ids");
            foreach (var kv in ids) sb.AppendLine($"{kv.Key}\t{kv.Value.Count}\t{string.Join(" ; ", kv.Value)}");
            return sb.ToString();
        }

        private static void Visit(Transform t, StringBuilder sb, Dictionary<string, List<string>> ids)
        {
            var f = new List<string>();
            f.Add(Rel(t));
            f.Add(t.gameObject.activeSelf ? "on" : "OFF");
            GameObject pr = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
            f.Add(pr == t.gameObject ? "prefab=" + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(pr)) : "");
            Vector3 p = t.position, e = t.eulerAngles, s = t.lossyScale;
            f.Add($"pos={p.x:0.##},{p.y:0.##},{p.z:0.##}");
            f.Add($"rot={e.x:0.#},{e.y:0.#},{e.z:0.#}");
            f.Add($"scl={s.x:0.##},{s.y:0.##},{s.z:0.##}");

            var col = new List<string>();
            foreach (Collider c in t.GetComponents<Collider>())
            {
                Bounds bb = c.bounds;
                col.Add($"{c.GetType().Name}{(c.isTrigger ? "(T)" : "")}[{bb.size.x:0.##}x{bb.size.y:0.##}x{bb.size.z:0.##}]");
            }
            f.Add(col.Count > 0 ? "col=" + string.Join("+", col) : "");

            var mats = new List<string>();
            var r = t.GetComponent<Renderer>();
            if (r != null) foreach (Material m in r.sharedMaterials) mats.Add(m != null ? m.name : "null");
            f.Add(mats.Count > 0 ? "mat=" + string.Join("+", mats) : "");

            var extra = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { extra.Add("MISSING"); continue; }
                if (c is InteractableObject io)
                {
                    extra.Add($"{io.GetType().Name}:{io.Id}");
                    if (!ids.TryGetValue(io.Id, out var l)) ids[io.Id] = l = new List<string>();
                    l.Add(Rel(t));
                }
                else if (c is Light lt) extra.Add($"Light:{lt.type},{lt.intensity:0.##},r{lt.range:0.#}");
                else if (c is ScenarioAnchor a) extra.Add("Anchor:" + a.Id);
                else if (c is SpawnPoint sp) extra.Add("Spawn:" + sp.Id);
                else if (c is MonoBehaviour mb)
                {
                    string n = mb.GetType().Name;
                    if (n == "LightGroup" || n == "HazardZone" || n == "WindowViewZone" || n == "ActBase" || n.StartsWith("Act") || n.StartsWith("War") || n.StartsWith("Npc") || n.EndsWith("Npc"))
                    {
                        var so = new SerializedObject(mb); var idp = so.FindProperty("id") ?? so.FindProperty("zoneId") ?? so.FindProperty("groupId");
                        extra.Add(n + (idp != null && idp.propertyType == SerializedPropertyType.String ? ":" + idp.stringValue : ""));
                    }
                }
            }
            f.Add(extra.Count > 0 ? "cmp=" + string.Join(",", extra) : "");
            sb.AppendLine(string.Join(" | ", f));
            foreach (Transform c in t) Visit(c, sb, ids);
        }

        private static string Rel(Transform t)
        {
            var stack = new Stack<string>();
            for (Transform c = t; c != null; c = c.parent) stack.Push(c.name);
            return string.Join("/", stack);
        }
    }
}
