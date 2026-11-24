using QuakeSurvival.Art;
using UnityEditor;
using UnityEngine;

namespace QuakeSurvival.EditorTools
{
    /// <summary>코드로 그리는 간단한 텍스처(팔레트 색만 사용). 한 번 만들어 에셋으로 저장해 재사용한다.</summary>
    internal static class ProceduralTextures
    {
        private const string Dir = "Assets/_Project/Textures";

        /// <summary>건물 외벽 한 칸(한 층, 창문 하나): 벽, 창틀, 어두운 유리, 아래 난간 줄.</summary>
        public static Texture2D FacadeWindows()
        {
            string path = Dir + "/FacadeWindow.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "FacadeWindow", wrapMode = TextureWrapMode.Repeat };
            Color wall = Palette.Facade;
            Color frame = Palette.Trim;
            Color glass = Palette.Hex(0x232B36);
            Color rail = Palette.Concrete;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = Pick(x, y, wall, frame, glass, rail);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);

            SceneBuildKit.EnsureFolder("Assets/_Project", "Textures");
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        private static Color Pick(int x, int y, Color wall, Color frame, Color glass, Color rail)
        {
            bool inFrame = x >= 16 && x < 112 && y >= 34 && y < 110;
            bool inGlass = x >= 20 && x < 108 && y >= 38 && y < 106;
            bool mullion = inGlass && (x >= 62 && x < 66);
            bool railBand = y >= 22 && y < 30 && x >= 10 && x < 118;
            if (mullion) return frame;
            if (inGlass) return glass;
            if (inFrame) return frame;
            if (railBand) return rail;
            return wall;
        }
    }
}
