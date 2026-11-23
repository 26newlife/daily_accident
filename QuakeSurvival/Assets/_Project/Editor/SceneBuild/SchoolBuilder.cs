using QuakeSurvival.Art;
using QuakeSurvival.Safety;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 지진 옥외대피장소: 아파트 남동쪽(약 80m) '해안초등학교'. 운동장(흙, 울타리, 축구 골대)과 동쪽 3층 학교 건물.
    /// 실제 행동요령처럼 안전한 곳은 건물에서 떨어진 운동장이고, 학교 건물 안은 대피 장소가 아니다.
    /// 운동장은 바다와 같은 낮은 땅이라 지진해일 경보가 나면 다시 남쪽 언덕으로 올라가야 한다.
    /// </summary>
    internal static class SchoolBuilder
    {
        // 운동장 범위(월드 x, z)
        public const float FieldMinX = 75f;
        public const float FieldMaxX = 96f;
        public const float FieldMinZ = -50.5f;
        public const float FieldMaxZ = -29.5f;

        // 북쪽 정문(아파트에서 오는 길), 서쪽 쪽문(언덕으로 가는 길)
        public const float NorthGateMinX = 78f;
        public const float NorthGateMaxX = 82f;
        public const float WestGateMinZ = -49f;
        public const float WestGateMaxZ = -45f;

        public static Vector3 FieldCenter => new Vector3((FieldMinX + FieldMaxX) / 2f, 0f, (FieldMinZ + FieldMaxZ) / 2f);
        public static Vector2 FieldSize => new Vector2(FieldMaxX - FieldMinX, FieldMaxZ - FieldMinZ);

        private const float FenceHeight = 1.2f;
        public const float BuildingMinX = 98f;
        public const float BuildingMaxX = 106f;
        private const float BuildingHeight = 10.5f;

        public static Transform Build(Transform parent)
        {
            Transform root = new GameObject("School").transform;
            root.SetParent(parent, false);
            BuildField(root);
            BuildFence(root);
            BuildGoal(root, new Vector3(FieldCenter.x, 0f, FieldMaxZ - 1f));
            BuildGoal(root, new Vector3(FieldCenter.x, 0f, FieldMinZ + 1f));
            Transform building = BuildBuilding(root);

            Vector3 c = FieldCenter;
            ZoneBuilder.Area("Area_OutdoorSite", root, new Vector3(c.x, 3f, c.z), new Vector3(FieldSize.x, 6f, FieldSize.y), PlayerArea.OutdoorSite, 1);
            return building;
        }

        private static void BuildField(Transform root)
        {
            Vector3 c = FieldCenter;
            ApartmentShellBuilder.Decor("Playground", root, new Vector3(c.x, 0.012f, c.z), new Vector3(FieldSize.x, 0.02f, FieldSize.y), Mat("Dirt", Palette.Dirt, 0.25f));
            Material line = Mat("FieldLine", Palette.Trim, 0.2f);
            ApartmentShellBuilder.Decor("FieldLine_Center", root, new Vector3(c.x, 0.025f, c.z), new Vector3(FieldSize.x - 2f, 0.01f, 0.12f), line);
            ApartmentShellBuilder.Decor("FieldLine_West", root, new Vector3(FieldMinX + 1f, 0.025f, c.z), new Vector3(0.12f, 0.01f, FieldSize.y - 2f), line);
            ApartmentShellBuilder.Decor("FieldLine_East", root, new Vector3(FieldMaxX - 1f, 0.025f, c.z), new Vector3(0.12f, 0.01f, FieldSize.y - 2f), line);
        }

        private static void BuildFence(Transform root)
        {
            Material fence = Mat("Fence", Palette.Metal, 0.6f);
            // 북쪽: 정문만 비운다
            FenceX(root, FieldMinX, NorthGateMinX, FieldMaxZ, fence);
            FenceX(root, NorthGateMaxX, FieldMaxX, FieldMaxZ, fence);
            // 남쪽, 동쪽: 막힘
            FenceX(root, FieldMinX, FieldMaxX, FieldMinZ, fence);
            FenceZ(root, FieldMinZ, FieldMaxZ, FieldMaxX, fence);
            // 서쪽: 언덕 쪽 쪽문만 비운다
            FenceZ(root, FieldMinZ, WestGateMinZ, FieldMinX, fence);
            FenceZ(root, WestGateMaxZ, FieldMaxZ, FieldMinX, fence);
        }

        private static void FenceX(Transform root, float x0, float x1, float z, Material mat)
        {
            Box("Fence", root, new Vector3((x0 + x1) / 2f, FenceHeight / 2f, z), new Vector3(x1 - x0, FenceHeight, 0.06f), mat);
        }

        private static void FenceZ(Transform root, float z0, float z1, float x, Material mat)
        {
            Box("Fence", root, new Vector3(x, FenceHeight / 2f, (z0 + z1) / 2f), new Vector3(0.06f, FenceHeight, z1 - z0), mat);
        }

        private static void BuildGoal(Transform root, Vector3 center)
        {
            Material white = Mat("GoalPost", Palette.Trim, 0.4f);
            const float halfWidth = 1.6f;
            const float height = 2f;
            Box("Goal_Post", root, center + new Vector3(-halfWidth, height / 2f, 0f), new Vector3(0.1f, height, 0.1f), white);
            Box("Goal_Post", root, center + new Vector3(halfWidth, height / 2f, 0f), new Vector3(0.1f, height, 0.1f), white);
            Box("Goal_Bar", root, center + new Vector3(0f, height, 0f), new Vector3(halfWidth * 2f + 0.1f, 0.1f, 0.1f), white);
        }

        /// <summary>학교 건물(지진해일에 무너지는 단위). 이름표와 조명도 같이 무너지도록 한 부모 아래에 둔다.</summary>
        private static Transform BuildBuilding(Transform parent)
        {
            float width = BuildingMaxX - BuildingMinX;
            float depth = FieldSize.y - 1f;
            Transform root = Pivot(parent, "SchoolBuilding", Vector3.zero);
            var center = new Vector3((BuildingMinX + BuildingMaxX) / 2f, BuildingHeight / 2f, FieldCenter.z);
            var tiling = new Vector2(Mathf.Max(1f, Mathf.Round(depth / 3.5f)), 3f);
            Material wall = TexturedMat("Facade_School", ProceduralTextures.FacadeWindows(), tiling, 0.3f);
            wall.SetColor("_BaseColor", Palette.SchoolWall);
            Box("SchoolBuilding", root, center, new Vector3(width, BuildingHeight, depth), wall);

            // 운동장 쪽(서쪽) 벽에 학교 이름과 조명
            Label("해안초등학교", root, new Vector3(BuildingMinX - 0.05f, BuildingHeight - 1.2f, FieldCenter.z),
                new Vector3(0f, 90f, 0f), Palette.UiText, 6f);
            PointLight("School Light", root, new Vector3(BuildingMinX - 1f, 6f, FieldCenter.z - 5f), Palette.LampWarm, 2.5f, 18f);
            PointLight("School Light", root, new Vector3(BuildingMinX - 1f, 6f, FieldCenter.z + 5f), Palette.LampWarm, 2.5f, 18f);
            return root;
        }
    }
}
