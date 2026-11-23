using System.IO;
using GameGameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameUI.EditorTools.Art
{
    /// <summary>
    /// 인테리어 "룩" (후처리·SSAO·카메라) 구성. 전부 코드로 만들어 빌더/메뉴가 여러 번 실행해도 같은 결과다.
    ///   · Volume 프로필 2종 (Assets/Settings/House_Interior_Day / _Smoke) + 베이스 프리팹의 HouseVolumes
    ///   · SSAO 렌더러 피처 보정 (실내 접촉 그림자를 조금 더 또렷하게)
    ///   · Player 카메라: 후처리 켜기 + SMAA (예전에는 후처리가 꺼져 있었다)
    /// 정전·화재·소등 연출은 조명/환경광이 맡으므로 건드리지 않는다.
    /// </summary>
    public static class InteriorLookBuilder
    {
        public const string DayPath = "Assets/Settings/House_Interior_Day.asset";
        public const string SmokePath = "Assets/Settings/House_Interior_Smoke.asset";
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Gameplay/Player.prefab";

        [MenuItem("Tools/GameUI/Art/Apply Interior Look (Volume + SSAO + Camera)")]
        public static void ApplyMenu() { Debug.Log(ApplyAll()); }

        /// <summary>배치모드: -executeMethod GameUI.EditorTools.Art.InteriorLookBuilder.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            string t; try { t = ApplyAll(); } catch (System.Exception e) { t = "오류: " + e; Debug.LogException(e); }
            Debug.Log(t); EditorApplication.Exit(t.StartsWith("오류") ? 1 : 0);
        }

        public static string ApplyAll()
        {
            EnsureProfiles();
            string ssao = TuneSsao();
            string cam = EnsurePlayerCamera();
            AssetDatabase.SaveAssets();
            return $"[Look] 프로필 2종 / {ssao} / {cam}";
        }

        // ═════════════════════════ Volume 프로필 ═════════════════════════
        public static void EnsureProfiles()
        {
            Build(DayPath, p =>
            {
                var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
                var bloom = p.Add<Bloom>(true);
                bloom.threshold.Override(1.05f); bloom.intensity.Override(0.22f); bloom.scatter.Override(0.55f); bloom.tint.Override(new Color(1f, 0.97f, 0.92f));
                var vig = p.Add<Vignette>(true); vig.intensity.Override(0.16f); vig.smoothness.Override(0.45f); vig.rounded.Override(false);
                var ca = p.Add<ColorAdjustments>(true);
                ca.postExposure.Override(0.05f); ca.contrast.Override(8f); ca.saturation.Override(4f);
                var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(7f); wb.tint.Override(0f);
            });
            Build(SmokePath, p =>
            {
                var vig = p.Add<Vignette>(true); vig.intensity.Override(0.38f); vig.smoothness.Override(0.5f);
                var ca = p.Add<ColorAdjustments>(true);
                ca.postExposure.Override(-0.45f); ca.saturation.Override(-32f); ca.contrast.Override(10f);
                ca.colorFilter.Override(new Color(0.88f, 0.82f, 0.74f));
            });
        }

        private static void Build(string path, System.Action<VolumeProfile> fill)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);   // 값이 바뀌었을 수 있으니 같은 경로로 다시 만든다 (참조는 경로로 다시 연결)
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(p, path);
            fill(p);
            foreach (VolumeComponent c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
        }

        // ═════════════════════════ 집 프리팹에 Volume 붙이기 ═════════════════════════
        /// <summary>houseRoot 아래에 HouseVolumes(+Volume 2개)를 만든다. 이미 있으면 프로필 참조만 갱신한다</summary>
        public static void EnsureVolumes(Transform houseRoot)
        {
            EnsureProfiles();
            Transform root = houseRoot.Find("HouseVolumes");
            if (root == null) { root = new GameObject("HouseVolumes").transform; root.SetParent(houseRoot, false); }
            var hv = root.GetComponent<HouseVolumes>() ?? root.gameObject.AddComponent<HouseVolumes>();
            Volume day = Vol(root, "Volume_Day", AssetDatabase.LoadAssetAtPath<VolumeProfile>(DayPath), 0f, 1f);
            Volume smoke = Vol(root, "Volume_Smoke", AssetDatabase.LoadAssetAtPath<VolumeProfile>(SmokePath), 1f, 0f);
            var so = new SerializedObject(hv);
            so.FindProperty("day").objectReferenceValue = day;
            so.FindProperty("smoke").objectReferenceValue = smoke;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Volume Vol(Transform parent, string name, VolumeProfile profile, float priority, float weight)
        {
            Transform t = parent.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            var v = t.GetComponent<Volume>() ?? t.gameObject.AddComponent<Volume>();
            v.isGlobal = true; v.priority = priority; v.weight = weight; v.sharedProfile = profile;
            return v;
        }

        // ═════════════════════════ SSAO ═════════════════════════
        private static string TuneSsao()
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (data == null) return "SSAO: 렌더러 없음";
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
            {
                if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var so = new SerializedObject(f);
                SetF(so, "m_Settings.Intensity", 0.75f);
                SetF(so, "m_Settings.Radius", 0.32f);
                SetF(so, "m_Settings.DirectLightingStrength", 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f); EditorUtility.SetDirty(data);
                return "SSAO 강도 0.75 / 반경 0.32 / 직접광 0.4";
            }
            return "SSAO: 피처 없음";
        }

        private static void SetF(SerializedObject so, string path, float v) { var p = so.FindProperty(path); if (p != null) p.floatValue = v; }

        // ═════════════════════════ Player 카메라 ═════════════════════════
        private static string EnsurePlayerCamera()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Camera cam = root.GetComponentInChildren<Camera>(true);
                if (cam == null) return "카메라 없음";
                var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                ConfigureCamera(data);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                return "Player 카메라 후처리 ON + SMAA";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>플레이어 카메라 URP 설정 (빌더도 같은 함수를 쓴다)</summary>
        public static void ConfigureCamera(UniversalAdditionalCameraData data)
        {
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
        }
    }
}
