using UnityEngine;
using UnityEngine.Rendering;

namespace GameGameplay
{
    /// <summary>시나리오 1 런타임 연출용 도우미: 외부 에셋 없이 부드러운 원형 텍스처와 URP 파티클 재질을 만든다</summary>
    public static class WarFx
    {
        private static Texture2D soft;
        private static Material smokeMat, glowMat;

        public static Texture2D SoftTexture
        {
            get
            {
                if (soft != null) return soft;
                const int n = 64;
                soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "WarSoftCircle", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        soft.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                soft.Apply();
                return soft;
            }
        }

        public static Material Particle(bool additive)
        {
            ref Material m = ref (additive ? ref glowMat : ref smokeMat);
            if (m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh) { name = additive ? "WarGlow" : "WarSmoke" };
            m.mainTexture = SoftTexture;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftTexture);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }
    }
}
