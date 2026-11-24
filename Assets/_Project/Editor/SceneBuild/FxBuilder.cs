using QuakeSurvival.Art;
using QuakeSurvival.Fx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>효과용 재질(불, 연기, 불덩이, 비)과 FxLibrary 생성.</summary>
    internal static class FxBuilder
    {
        private const string MatDir = "Assets/_Project/Materials";

        public static FxLibrary BuildLibrary(GameObject host)
        {
            var lib = host.AddComponent<FxLibrary>();
            SetRef(lib, "fire", ParticleMat("FxFire", Palette.Fire, additive: true));
            SetRef(lib, "smoke", ParticleMat("FxSmoke", Palette.Smoke, additive: false));
            SetRef(lib, "fireball", UnlitMat("FxFireball", Palette.Fire));
            return lib;
        }

        public static Material RainMaterial() => ParticleMat("FxRain", Palette.Rain, additive: false);

        private static Material ParticleMat(string name, Color color, bool additive)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            MakeTransparent(mat, additive);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void MakeTransparent(Material mat, bool additive)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Material UnlitMat(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
