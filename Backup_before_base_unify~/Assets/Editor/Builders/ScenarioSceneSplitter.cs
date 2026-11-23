using System;
using System.Collections.Generic;
using GameGameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameUI.EditorTools
{
    /// <summary>
    /// 하나의 씬으로 만들어진 장면에서 "시나리오 전용 오브젝트"를 Additive 시나리오 씬으로 분리한다.
    /// 씬 사이 참조는 저장할 수 없으므로, 시나리오 컴포넌트가 가진 "집(베이스 씬) 오브젝트 참조"를
    /// (소유 경로 + 프로퍼티 경로 + 대상 경로 + 타입) ScenarioBinding 으로 기록하고 null 로 비운다.
    /// 런타임에는 ScenarioRunner → ScenarioBinder 가 이를 복원한다.
    /// 반대로 베이스 씬 컴포넌트가 시나리오 오브젝트를 참조하고 있으면 reverseRefs 로 보고한다 (집은 시나리오를 몰라야 한다).
    /// </summary>
    public static class ScenarioSceneSplitter
    {
        public sealed class Result
        {
            public readonly List<ScenarioBinding> bindings = new List<ScenarioBinding>();
            public readonly List<string> reverseRefs = new List<string>();
            public int objectsMoved;
        }

        /// <param name="contentRoot">시나리오 내용물 루트 (이동 후 비활성으로 저장)</param>
        /// <param name="sceneRootGo">ScenarioSceneRoot 가 붙은 오브젝트 (항상 활성)</param>
        /// <param name="extraMoves">집 계층에 섞여 만들어진 시나리오 오브젝트 (contentRoot 아래로 옮겨진다)</param>
        public static Result Split(Scene baseScene, GameObject contentRoot, GameObject sceneRootGo, IEnumerable<GameObject> extraMoves, string scenePath)
        {
            var result = new Result();

            // 1) 집 계층에 섞여 있던 시나리오 오브젝트를 내용물 루트 아래로 (월드 위치 유지)
            Transform moved = new GameObject("MovedFromHouse").transform;
            moved.SetParent(contentRoot.transform, false);
            foreach (GameObject g in extraMoves) if (g != null && !g.transform.IsChildOf(contentRoot.transform)) g.transform.SetParent(moved, true);

            var inS = new HashSet<GameObject>();
            foreach (Transform t in contentRoot.GetComponentsInChildren<Transform>(true)) inS.Add(t.gameObject);
            foreach (Transform t in sceneRootGo.GetComponentsInChildren<Transform>(true)) inS.Add(t.gameObject);
            result.objectsMoved = inS.Count;

            // 2) 시나리오 → 집 참조를 바인딩으로 기록하고 비운다
            var sMonos = new List<MonoBehaviour>();
            sMonos.AddRange(contentRoot.GetComponentsInChildren<MonoBehaviour>(true));
            sMonos.AddRange(sceneRootGo.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (MonoBehaviour m in sMonos)
            {
                if (m == null) continue;
                var so = new SerializedObject(m);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object v = it.objectReferenceValue;
                    if (v == null || EditorUtility.IsPersistent(v)) continue;
                    GameObject go = v is Component c ? c.gameObject : v as GameObject;
                    if (go == null || go.scene != baseScene || inS.Contains(go)) continue;
                    var comps = m.GetComponents(m.GetType());
                    result.bindings.Add(new ScenarioBinding
                    {
                        ownerPath = ScenarioPaths.PathOf(m.transform),
                        ownerType = m.GetType().AssemblyQualifiedName,
                        ownerIndex = Array.IndexOf(comps, m),
                        propertyPath = it.propertyPath,
                        targetPath = ScenarioPaths.PathOf(go.transform),
                        targetType = v is Component ? v.GetType().AssemblyQualifiedName : string.Empty,
                    });
                    it.objectReferenceValue = null;
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3) 집/베이스 → 시나리오 역참조 검사
            foreach (GameObject root in baseScene.GetRootGameObjects())
            {
                foreach (MonoBehaviour m in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (m == null || inS.Contains(m.gameObject)) continue;
                    var so = new SerializedObject(m);
                    var it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object v = it.objectReferenceValue;
                        if (v == null || EditorUtility.IsPersistent(v)) continue;
                        GameObject go = v is Component c ? c.gameObject : v as GameObject;
                        if (go != null && inS.Contains(go))
                            result.reverseRefs.Add($"{ScenarioPaths.PathOf(m.transform)} [{m.GetType().Name}].{it.propertyPath} → {go.name}");
                    }
                }
            }

            // 4) 바인딩 저장 + 새 씬으로 이동 + 저장
            var rso = new SerializedObject(sceneRootGo.GetComponent<ScenarioSceneRoot>());
            SerializedProperty arr = rso.FindProperty("bindings");
            arr.arraySize = result.bindings.Count;
            for (int i = 0; i < result.bindings.Count; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                ScenarioBinding b = result.bindings[i];
                e.FindPropertyRelative("ownerPath").stringValue = b.ownerPath;
                e.FindPropertyRelative("ownerType").stringValue = b.ownerType;
                e.FindPropertyRelative("ownerIndex").intValue = b.ownerIndex;
                e.FindPropertyRelative("propertyPath").stringValue = b.propertyPath;
                e.FindPropertyRelative("targetPath").stringValue = b.targetPath;
                e.FindPropertyRelative("targetType").stringValue = b.targetType;
            }
            rso.ApplyModifiedPropertiesWithoutUndo();

            Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            contentRoot.transform.SetParent(null, true);
            sceneRootGo.transform.SetParent(null, true);
            contentRoot.SetActive(false);   // Awake/OnEnable 은 바인딩 복원 뒤 ScenarioRunner 가 활성화할 때 실행된다
            SceneManager.MoveGameObjectToScene(sceneRootGo, s);
            SceneManager.MoveGameObjectToScene(contentRoot, s);
            string dir = System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) MainMenuBuilder.EnsureFolderPublic(dir);
            EditorSceneManager.SaveScene(s, scenePath);
            EditorSceneManager.CloseScene(s, true);
            SceneManager.SetActiveScene(baseScene);
            return result;
        }
    }
}
