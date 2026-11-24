using QuakeSurvival.Art;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 10층 투룸 아파트의 벽/바닥/천장/창문(로컬 좌표, 바닥 y=0, 10m x 8m).
    ///
    ///   z=4  +-----------+-----------+-----------+   북쪽 = 바다(창문)
    ///        |   방2     |   방1     |  베란다   |
    ///   z=1  +--[문]-----+----[문]---+  (열림)   |
    ///        | 욕실 [문] |   거실         부엌   |
    ///   z=-.6+-----+     |                [가스] |
    ///        | 현관[분전함]                      |
    ///   z=-4 +-[현관문]--------------------------+
    ///       x=-5  -3   -1.5        2            5
    /// </summary>
    internal static class ApartmentShellBuilder
    {
        public const float Height = 2.6f;
        private const float Ext = 0.2f;
        private const float Int = 0.12f;
        private const float SillTop = 0.9f;
        private const float WindowTop = 2.1f;

        public static void Build(Transform root)
        {
            Material wall = Mat("Wall", Palette.WallPlaster);
            BuildFloors(root);
            Box("Ceiling", root, new Vector3(0f, Height + 0.05f, 0f), new Vector3(10f, 0.1f, 8f), Mat("Ceiling", Palette.Trim));

            BuildExteriorWalls(root, wall);
            BuildNorthWindows(root, wall);
            BuildInteriorWalls(root, wall);
            BuildBaseboards(root);
        }

        private static void BuildFloors(Transform root)
        {
            Box("Floor", root, new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 8f), Mat("FloorOak", Palette.FloorOak, 0.35f));
            Decor("Floor_Entrance", root, new Vector3(-4f, 0.005f, -2.3f), new Vector3(2f, 0.01f, 3.4f), Mat("EntranceTile", Palette.Tile, 0.5f));
            Decor("Floor_Bath", root, new Vector3(-4f, 0.005f, 0.2f), new Vector3(2f, 0.01f, 1.6f), Mat("BathTile", Palette.Tile, 0.6f));
            Decor("Floor_Veranda", root, new Vector3(3.5f, 0.005f, 2.5f), new Vector3(3f, 0.01f, 3f), Mat("VerandaTile", Palette.Concrete, 0.3f));
            Decor("Floor_Kitchen", root, new Vector3(3.5f, 0.005f, -1.5f), new Vector3(3f, 0.01f, 5f), Mat("KitchenTile", Palette.Tile, 0.45f));
        }

        private static void BuildExteriorWalls(Transform root, Material wall)
        {
            float h = Height / 2f;
            Box("Wall_East", root, new Vector3(5f, h, 0f), new Vector3(Ext, Height, 8f), wall);
            Box("Wall_West", root, new Vector3(-5f, h, 0f), new Vector3(Ext, Height, 8f), wall);
            // 남쪽 벽: 현관문 자리(x=-4.5 ~ -3.0)를 비워 둔다
            Box("Wall_South_A", root, new Vector3(1f, h, -4f), new Vector3(8f, Height, Ext), wall);
            Box("Wall_South_B", root, new Vector3(-4.75f, h, -4f), new Vector3(0.5f, Height, Ext), wall);
            Box("Wall_South_Top", root, new Vector3(-3.75f, 2.35f, -4f), new Vector3(1.5f, 0.5f, Ext), wall);
        }

        /// <summary>북쪽 벽: 방2/방1은 허리 높이 창, 베란다는 바닥부터 천장까지 통유리.</summary>
        private static void BuildNorthWindows(Transform root, Material wall)
        {
            WallSpan(root, wall, -5f, -4.4f);
            Window(root, wall, -4.4f, -2.2f, SillTop, WindowTop);
            WallSpan(root, wall, -2.2f, -0.9f);
            Window(root, wall, -0.9f, 1.4f, SillTop, WindowTop);
            WallSpan(root, wall, 1.4f, 2.1f);
            Window(root, wall, 2.1f, 4.9f, 0f, Height);
            WallSpan(root, wall, 4.9f, 5f);
        }

        private static void WallSpan(Transform root, Material wall, float x0, float x1)
        {
            Box("Wall_North", root, new Vector3((x0 + x1) / 2f, Height / 2f, 4f), new Vector3(x1 - x0, Height, Ext), wall);
        }

        private static void Window(Transform root, Material wall, float x0, float x1, float bottom, float top)
        {
            float cx = (x0 + x1) / 2f;
            float w = x1 - x0;
            if (bottom > 0f) Box("Wall_Sill", root, new Vector3(cx, bottom / 2f, 4f), new Vector3(w, bottom, Ext), wall);
            if (top < Height) Box("Wall_Lintel", root, new Vector3(cx, (top + Height) / 2f, 4f), new Vector3(w, Height - top, Ext), wall);

            Material frame = Mat("WindowFrame", Palette.Trim, 0.4f);
            float hgt = top - bottom;
            Box("Frame_Bottom", root, new Vector3(cx, bottom + 0.03f, 4f), new Vector3(w, 0.06f, 0.24f), frame);
            Box("Frame_Top", root, new Vector3(cx, top - 0.03f, 4f), new Vector3(w, 0.06f, 0.24f), frame);
            Box("Frame_Mid", root, new Vector3(cx, bottom + hgt / 2f, 4f), new Vector3(0.05f, hgt, 0.22f), frame);
            GameObject glass = Box("Glass", root, new Vector3(cx, bottom + hgt / 2f, 4f), new Vector3(w, hgt, 0.02f), GlassMat("WindowGlass", Palette.Glass));
            glass.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void BuildInteriorWalls(Transform root, Material wall)
        {
            float h = Height / 2f;
            // 방 아래쪽 벽(z=1): 방2 문(x -3.2~-2.3), 방1 문(x 0.2~1.1), x=2부터는 베란다로 열려 있음
            InteriorSpanX(root, wall, 1f, -5f, -3.2f);
            DoorHeader(root, wall, new Vector3(-2.75f, 0f, 1f), 0.9f, true);
            InteriorSpanX(root, wall, 1f, -2.3f, 0.2f);
            DoorHeader(root, wall, new Vector3(0.65f, 0f, 1f), 0.9f, true);
            InteriorSpanX(root, wall, 1f, 1.1f, 2f);

            Box("Wall_Room1_Room2", root, new Vector3(-1.5f, h, 2.5f), new Vector3(Int, Height, 3f), wall);
            Box("Wall_Room1_Veranda", root, new Vector3(2f, h, 2.5f), new Vector3(Int, Height, 3f), wall);

            // 욕실(x -5~-3, z -0.6~1): 남쪽 벽, 동쪽 벽에 문(z 0~0.8)
            InteriorSpanX(root, wall, -0.6f, -5f, -3f);
            Box("Wall_Bath_East_A", root, new Vector3(-3f, h, -0.3f), new Vector3(Int, Height, 0.6f), wall);
            DoorHeader(root, wall, new Vector3(-3f, 0f, 0.4f), 0.8f, false);
            Box("Wall_Bath_East_B", root, new Vector3(-3f, h, 0.9f), new Vector3(Int, Height, 0.2f), wall);
        }

        private static void InteriorSpanX(Transform root, Material wall, float z, float x0, float x1)
        {
            Box("Wall_Inner", root, new Vector3((x0 + x1) / 2f, Height / 2f, z), new Vector3(x1 - x0, Height, Int), wall);
        }

        private static void DoorHeader(Transform root, Material wall, Vector3 center, float width, bool alongX)
        {
            Vector3 size = alongX ? new Vector3(width, 0.5f, Int) : new Vector3(Int, 0.5f, width);
            Box("DoorHeader", root, center + new Vector3(0f, 2.35f, 0f), size, wall);
            Material trim = Mat("Trim", Palette.Trim, 0.3f);
            Vector3 jambSize = alongX ? new Vector3(0.05f, 2.1f, Int + 0.04f) : new Vector3(Int + 0.04f, 2.1f, 0.05f);
            Vector3 offset = alongX ? new Vector3(width / 2f, 1.05f, 0f) : new Vector3(0f, 1.05f, width / 2f);
            Decor("Jamb", root, center + offset, jambSize, trim);
            Decor("Jamb", root, center - offset + new Vector3(0f, 2.1f, 0f), jambSize, trim);
        }

        private static void BuildBaseboards(Transform root)
        {
            Material trim = Mat("Trim", Palette.Trim, 0.3f);
            Decor("Base_East", root, new Vector3(4.88f, 0.05f, -1.5f), new Vector3(0.03f, 0.1f, 5f), trim);
            Decor("Base_West", root, new Vector3(-4.88f, 0.05f, -2.3f), new Vector3(0.03f, 0.1f, 3.4f), trim);
            Decor("Base_South", root, new Vector3(1f, 0.05f, -3.88f), new Vector3(8f, 0.1f, 0.03f), trim);
            Decor("Base_RoomWall", root, new Vector3(-0.9f, 0.05f, 0.93f), new Vector3(2.4f, 0.1f, 0.03f), trim);
        }

        /// <summary>충돌 없는 장식용 박스(바닥 무늬, 몰딩 등).</summary>
        public static GameObject Decor(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = Box(name, parent, localPos, scale, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
