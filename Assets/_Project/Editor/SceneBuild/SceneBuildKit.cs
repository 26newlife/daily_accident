using UnityEditor;
using UnityEngine;

namespace QuakeSurvival.EditorTools
{
    /// <summary>씬 자동 생성용 공통 도우미(박스, 재질, 트리거, 직렬화 필드 설정).</summary>
    internal static class SceneBuildKit
    {
        public const float TopFloorY = 27f;   // 10층 바닥 높이(층고 3m)
        public const float FloorHeight = 3f;
        public const int PlayerFloor = 10;
        public const int TopBuildingFloor = 12; // 아파트 최고층(옥상 = 12층 천장)
        private const string MatDir = "Assets/_Project/Materials";
        private const int IgnoreRaycastLayer = 2;

        public static float FloorY(int floor) => (floor - 1) * FloorHeight;

        public static Transform Root(string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.position = position;
            return t;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Trigger(string name, Transform parent, Vector3 localPos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = IgnoreRaycastLayer;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return go;
        }

        public static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static Light PointLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        /// <summary>현재 빌드에서 쓰는 한글 폰트(SceneBuilder.BuildAll 시작 때 FontSetup.Ensure로 채움).</summary>
        public static FontSetup.Fonts CurrentFonts { get; set; }

        /// <summary>월드 공간 글자(TextMeshPro 3D). 깊이 테스트를 하므로 벽 너머로 비치지 않는다.</summary>
        public static void Label(string text, Transform parent, Vector3 localPos, Vector3 euler, Color color, float size = 2.4f)
        {
            var go = new GameObject("Label_" + text.Replace('\n', ' '));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            // 전체 씬 빌드 밖에서 단독으로 불려도 폰트를 준비한다
            CurrentFonts ??= FontSetup.Ensure();
            tmp.font = CurrentFonts.Bold;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);
        }

        /// <summary>반들거림(smoothness)을 지정한 Lit 재질.</summary>
        public static Material Mat(string name, Color color, float smoothness)
        {
            Material mat = Mat(name, color);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        /// <summary>텍스처를 반복(tiling)해서 쓰는 Lit 재질.</summary>
        public static Material TexturedMat(string name, Texture2D texture, Vector2 tiling, float smoothness)
        {
            Material mat = Mat(name, Color.white, smoothness);
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 반투명 유리 재질(URP Lit, Transparent). 그림자를 만들지 않아 창으로 햇빛과 번개가 들어온다.
        /// 기존 에셋이 있어도 설정을 다시 적용해 팔레트 변경을 따라간다.
        /// </summary>
        public static Material GlassMat(string name, Color color)
        {
            Material mat = LoadOrCreate(name);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_CastShadows", 0f);
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>스스로 빛나는 재질(비상구 표시, 조명 갓 등). 기존 에셋도 팔레트 색으로 갱신한다.</summary>
        public static Material EmissiveMat(string name, Color color, float intensity)
        {
            Material mat = LoadOrCreate(name);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 조명과 상관없이 항상 같은 색으로 보이는 재질(URP Unlit). 밤에도 색이 보여야 하는 표지판, 바닥 화살표에 쓴다.
        /// 기존 에셋이 있으면 셰이더와 색을 다시 맞춘다.
        /// </summary>
        public static Material UnlitMat(string name, Color color)
        {
            Material mat = LoadOrCreate(name);
            mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Material Mat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 팔레트가 바뀌면 기존 재질 색도 따라가게 한다
                existing.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
