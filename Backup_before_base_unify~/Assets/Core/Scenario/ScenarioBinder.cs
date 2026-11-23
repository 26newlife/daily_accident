using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameGameplay
{
    /// <summary>씬 안 오브젝트의 안정적인 계층 경로 ("Root/Child/Grand", 같은 이름 형제는 "이름#순번")</summary>
    public static class ScenarioPaths
    {
        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent) parts.Add(Part(c));
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Part(Transform t)
        {
            int k = 0;
            if (t.parent != null)
            {
                for (int i = 0; i < t.parent.childCount; i++)
                {
                    Transform s = t.parent.GetChild(i);
                    if (s == t) break;
                    if (s.name == t.name) k++;
                }
            }
            else
            {
                foreach (GameObject r in t.gameObject.scene.GetRootGameObjects())
                {
                    if (r.transform == t) break;
                    if (r.name == t.name) k++;
                }
            }
            return k == 0 ? t.name : $"{t.name}#{k}";
        }

        private static void Split(string part, out string name, out int k)
        {
            int i = part.LastIndexOf('#');
            if (i > 0 && int.TryParse(part.Substring(i + 1), out int n)) { name = part.Substring(0, i); k = n; }
            else { name = part; k = 0; }
        }

        public static GameObject Find(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path) || !scene.IsValid() || !scene.isLoaded) return null;
            string[] parts = path.Split('/');
            Split(parts[0], out string rn, out int rk);
            Transform cur = null;
            int seen = 0;
            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name == rn) { if (seen++ == rk) { cur = r.transform; break; } }
            for (int i = 1; cur != null && i < parts.Length; i++)
            {
                Split(parts[i], out string n, out int k);
                Transform next = null; int s = 0;
                for (int c = 0; c < cur.childCount; c++)
                {
                    Transform ch = cur.GetChild(c);
                    if (ch.name == n) { if (s++ == k) { next = ch; break; } }
                }
                cur = next;
            }
            return cur != null ? cur.gameObject : null;
        }
    }

    /// <summary>
    /// 시나리오 씬 로드 후 집(베이스 씬) 참조를 되살린다. 빌더(ScenarioSceneSplitter)가 씬 분리 때 기록한 바인딩을
    /// SerializedProperty 경로로 리플렉션 설정한다 (배열·중첩 구조체 지원).
    /// </summary>
    public static class ScenarioBinder
    {
        public static int Apply(Scene scenarioScene, Scene baseScene, ScenarioBinding[] bindings, List<string> errors)
        {
            int ok = 0;
            if (bindings == null) return 0;
            foreach (ScenarioBinding b in bindings)
            {
                GameObject owner = ScenarioPaths.Find(scenarioScene, b.ownerPath);
                GameObject target = ScenarioPaths.Find(baseScene, b.targetPath);
                if (owner == null) { errors.Add($"owner 없음: {b.ownerPath}"); continue; }
                if (target == null) { errors.Add($"대상 없음: {b.targetPath}  (← {b.ownerPath}.{b.propertyPath})"); continue; }
                Type ot = Type.GetType(b.ownerType);
                if (ot == null) { errors.Add($"owner 타입 없음: {b.ownerType}"); continue; }
                Component[] comps = owner.GetComponents(ot);
                if (b.ownerIndex >= comps.Length) { errors.Add($"owner 컴포넌트 없음: {b.ownerPath} {ot.Name}[{b.ownerIndex}]"); continue; }
                Type tt = string.IsNullOrEmpty(b.targetType) ? null : Type.GetType(b.targetType);
                try
                {
                    if (SetByPath(comps[b.ownerIndex], b.propertyPath, target, tt)) ok++;
                    else errors.Add($"필드 설정 실패: {ot.Name}.{b.propertyPath}");
                }
                catch (Exception e) { errors.Add($"{ot.Name}.{b.propertyPath}: {e.Message}"); }
            }
            return ok;
        }

        private struct Step { public string field; public int index; }

        private static bool SetByPath(object owner, string path, GameObject target, Type compType)
        {
            var steps = new List<Step>();
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string f = parts[i];
                if (i + 2 < parts.Length + 0 && parts[i + 1] == "Array" && parts[i + 2].StartsWith("data["))
                {
                    string num = parts[i + 2].Substring(5).TrimEnd(']');
                    steps.Add(new Step { field = f, index = int.Parse(num) });
                    i += 2;
                }
                else steps.Add(new Step { field = f, index = -1 });
            }
            return Rec(ref owner, steps, 0, target, compType);
        }

        private static FieldInfo FindField(Type t, string name)
        {
            for (; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static object Resolve(Type fieldType, GameObject target, Type compType)
        {
            if (fieldType == typeof(GameObject)) return target;
            if (typeof(Component).IsAssignableFrom(fieldType)) return target.GetComponent(compType != null && fieldType.IsAssignableFrom(compType) ? compType : fieldType);
            if (fieldType == typeof(UnityEngine.Object)) return compType != null ? (object)target.GetComponent(compType) : target;
            return null;
        }

        /// <summary>cur 를 (구조체면 복사본까지) 갱신한다. 성공 여부 반환</summary>
        private static bool Rec(ref object cur, List<Step> steps, int i, GameObject target, Type compType)
        {
            Step s = steps[i];
            FieldInfo f = FindField(cur.GetType(), s.field);
            if (f == null) return false;
            bool last = i == steps.Count - 1;
            object fv = f.GetValue(cur);

            if (s.index >= 0)
            {
                Type elem; IList list = fv as IList;
                if (list == null) return false;
                elem = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType.GetGenericArguments()[0];
                if (s.index >= list.Count) return false;
                if (last)
                {
                    object v = Resolve(elem, target, compType);
                    if (v == null) return false;
                    list[s.index] = v;
                    return true;
                }
                object el = list[s.index];
                if (el == null || !Rec(ref el, steps, i + 1, target, compType)) return false;
                list[s.index] = el;
                return true;
            }

            if (last)
            {
                object v = Resolve(f.FieldType, target, compType);
                if (v == null) return false;
                f.SetValue(cur, v);
                return true;
            }
            if (fv == null || !Rec(ref fv, steps, i + 1, target, compType)) return false;
            if (f.FieldType.IsValueType) f.SetValue(cur, fv);
            return true;
        }
    }
}
