using QuakeSurvival.Art;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// Noto Sans KR(OFL)로 TextMeshPro 동적 폰트 에셋과 그림자(언더레이) 재질 프리셋을 만든다.
    /// 한국어 전용 게임이라 동적 아틀라스(샘플링 48, 여백 5 = 약 10%, 1024, 멀티 아틀라스)를 쓰고,
    /// 에디터에서 생긴 글리프가 빌드에 섞이지 않도록 Clear Dynamic Data On Build를 켠다.
    /// </summary>
    internal static class FontSetup
    {
        private const string Dir = "Assets/_Project/Fonts";
        private const int SamplingSize = 48;
        private const int Padding = 5;
        private const int AtlasSize = 1024;

        public sealed class Fonts
        {
            public TMP_FontAsset Regular;
            public TMP_FontAsset Bold;
            public Material RegularShadow;
            public Material BoldShadow;
        }

        public static Fonts Ensure()
        {
            EnsureEssentials();
            var regular = EnsureFontAsset("NotoSansKR-Regular");
            var bold = EnsureFontAsset("NotoSansKR-Bold");
            SetDefaultFont(regular);
            return new Fonts
            {
                Regular = regular,
                Bold = bold,
                RegularShadow = EnsureShadowPreset(regular, "NotoSansKR-Regular Shadow"),
                BoldShadow = EnsureShadowPreset(bold, "NotoSansKR-Bold Shadow"),
            };
        }

        private static void EnsureEssentials()
        {
            if (TMP_Settings.instance != null) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            if (TMP_Settings.instance == null)
            {
                // 패키지 가져오기는 비동기로 끝나므로, 프로젝트당 처음 한 번은 다시 실행해야 한다
                throw new System.InvalidOperationException(
                    "TextMeshPro 기본 리소스를 가져오는 중입니다. 가져오기가 끝난 뒤 씬 만들기를 한 번 더 실행해 주세요.");
            }
        }

        private static TMP_FontAsset EnsureFontAsset(string name)
        {
            string path = $"{Dir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Dir}/{name}.otf");
            if (font == null) throw new System.IO.FileNotFoundException($"폰트 파일이 없습니다: {Dir}/{name}.otf");

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, path);

            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            var so = new SerializedObject(asset);
            var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) clear.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>어두운 남색 그림자(언더레이)가 들어간 재질 프리셋. 밝은 배경에서도 글자가 읽히게 한다.</summary>
        private static Material EnsureShadowPreset(TMP_FontAsset font, string name)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(font.material) { name = name };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", Palette.WithAlpha(Palette.Shadow, 0.85f));
            mat.SetFloat("_UnderlayOffsetX", 0.6f);
            mat.SetFloat("_UnderlayOffsetY", -0.6f);
            mat.SetFloat("_UnderlaySoftness", 0.3f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetDefaultFont(TMP_FontAsset font)
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null || prop.objectReferenceValue == font) return;
            prop.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
