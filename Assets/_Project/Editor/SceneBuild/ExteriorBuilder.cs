using System.Collections.Generic;
using QuakeSurvival.Art;
using QuakeSurvival.Safety;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 건물 외형(12층, 창문 무늬), 도로/차/가로등/나무, 해변과 바다, 남쪽 고지대 언덕과 지진해일 대피소,
    /// 남동쪽 해안초등학교(지진 옥외대피장소).
    /// 바다는 북쪽(+z). 아파트 폭발 잔해가 닿지 않도록 학교와 언덕은 아파트에서 70m 이상 떨어져 있다.
    /// 지진해일에 무너질 물체(건물, 차, 나무, 가로등)는 각각 하나의 부모 아래에 묶어 Parts로 돌려준다.
    /// </summary>
    internal static class ExteriorBuilder
    {
        public const float HillBaseZ = -80f;
        public const float HillTopZ = -110f;
        public const float HillHeight = 12f;
        public const float ShelterZ = HillTopZ - 13f;

        public const float GroundMinX = -60f;
        public const float GroundMaxX = 115f;
        private const float GroundMaxZ = 6f;

        public sealed class Parts
        {
            public Transform Sea;
            public Light[] StreetLights;
            public Transform ApartmentMass;
            public Transform SchoolBuilding;
            public Transform[] Cars;
            public Transform[] Trees;
            public Transform[] StreetLightPosts;
        }

        public static Parts Build()
        {
            Transform root = Root("Exterior", Vector3.zero);
            var parts = new Parts { ApartmentMass = BuildBuildingMass(root) };
            BuildGround(root);
            BuildStreet(root, parts);
            BuildHill(root);
            parts.SchoolBuilding = SchoolBuilder.Build(root);
            BuildBoundary(root);
            BuildZones(root);
            parts.Sea = BuildSea(root);
            return parts;
        }

        private static Transform BuildBuildingMass(Transform root)
        {
            Transform mass = Pivot(root, "ApartmentMass", Vector3.zero);
            float under = TopFloorY - 0.1f;
            float above = FloorY(PlayerFloor) + FloorHeight + 0.1f;
            float roofY = FloorY(TopBuildingFloor) + FloorHeight;
            float aboveHeight = roofY - above;
            FacadeBox("Mass_Below_West", mass, new Vector3(-2.5f, under / 2f, -2.5f), new Vector3(5f, under, 13f));
            FacadeBox("Mass_Below_East", mass, new Vector3(2.7f, under / 2f, 0f), new Vector3(5.4f, under, 8f));
            // 위층(11~12층)도 아래층처럼 계단실 자리(x 0~5.4, z -7~-4)를 비워 둔다
            FacadeBox("Mass_Above_West", mass, new Vector3(-2.5f, (above + roofY) / 2f, -2.5f), new Vector3(5f, aboveHeight, 13f));
            FacadeBox("Mass_Above_East", mass, new Vector3(2.7f, (above + roofY) / 2f, 0f), new Vector3(5.4f, aboveHeight, 8f));
            return mass;
        }

        /// <summary>층(3m)마다 창문 한 줄이 반복되는 외벽. 크기별로 타일 수를 맞춘 재질을 쓴다.</summary>
        private static void FacadeBox(string name, Transform root, Vector3 pos, Vector3 size)
        {
            float width = (size.x + size.z) / 2f;
            var tiling = new Vector2(Mathf.Max(1f, Mathf.Round(width / 3.5f)), Mathf.Max(1f, Mathf.Round(size.y / FloorHeight)));
            Material mat = TexturedMat("Facade_" + name, ProceduralTextures.FacadeWindows(), tiling, 0.35f);
            Box(name, root, pos, size, mat);
        }

        private static void BuildGround(Transform root)
        {
            float cx = (GroundMinX + GroundMaxX) / 2f;
            float width = GroundMaxX - GroundMinX;
            float cz = (GroundMaxZ + HillBaseZ) / 2f;
            Box("Ground", root, new Vector3(cx, -0.05f, cz), new Vector3(width, 0.1f, GroundMaxZ - HillBaseZ), Mat("Asphalt", Palette.Asphalt, 0.55f));
            Box("Beach", root, new Vector3(cx, -0.05f, 15.5f), new Vector3(width, 0.1f, 19f), Mat("Sand", Palette.Sand, 0.2f));
            ApartmentShellBuilder.Decor("Sidewalk", root, new Vector3(cx, 0.01f, -11f), new Vector3(width, 0.02f, 6f), Mat("Sidewalk", Palette.Concrete, 0.3f));
        }

        private static void BuildStreet(Transform root, Parts parts)
        {
            Material line = Mat("RoadLine", Palette.Trim, 0.3f);
            for (int i = -6; i <= 12; i++)
            {
                ApartmentShellBuilder.Decor("RoadDash", root, new Vector3(i * 9f, 0.012f, -19f), new Vector3(4f, 0.01f, 0.15f), line);
            }

            Material car = Mat("CarBody", Palette.CarBody, 0.7f);
            Material carGlass = Mat("CarGlass", Palette.ScreenOff, 0.9f);
            var cars = new List<Transform>();
            foreach (var p in new[] { new Vector3(-12f, 0f, -16.8f), new Vector3(9f, 0f, -16.8f), new Vector3(20f, 0f, -21.2f), new Vector3(48f, 0f, -16.8f) })
            {
                Transform c = Pivot(root, "Car", p);
                Box("Car_Body", c, new Vector3(0f, 0.55f, 0f), new Vector3(4.2f, 0.7f, 1.8f), car);
                Box("Car_Cabin", c, new Vector3(-0.3f, 1.15f, 0f), new Vector3(2.2f, 0.55f, 1.6f), carGlass);
                cars.Add(c);
            }
            parts.Cars = cars.ToArray();

            var lights = new List<Light>();
            var posts = new List<Transform>();
            Material pole = Mat("Metal", Palette.Metal, 0.8f);
            Material head = EmissiveMat("StreetLampHead", Palette.LampWarm, 1.5f);
            foreach (float x in new[] { -18f, -6f, 6f, 18f, 32f, 46f, 60f })
            {
                Transform post = Pivot(root, "StreetLight", new Vector3(x, 0f, -14.5f));
                Box("StreetLight_Pole", post, new Vector3(0f, 3f, 0f), new Vector3(0.15f, 6f, 0.15f), pole);
                ApartmentShellBuilder.Decor("StreetLight_Head", post, new Vector3(0f, 6f, -0.5f), new Vector3(0.4f, 0.12f, 1f), head);
                lights.Add(PointLight("StreetLight", post, new Vector3(0f, 5.7f, -0.7f), Palette.LampWarm, 3f, 14f));
                posts.Add(post);
            }
            parts.StreetLights = lights.ToArray();
            parts.StreetLightPosts = posts.ToArray();

            Material trunk = Mat("WoodDark", Palette.WoodDark, 0.35f);
            Material leaf = Mat("Leaf", Palette.Grass, 0.2f);
            var trees = new List<Transform>();
            foreach (var p in new[] { new Vector2(-16f, -42f), new Vector2(-9f, -42f), new Vector2(9f, -42f), new Vector2(16f, -42f), new Vector2(40f, -60f), new Vector2(55f, -66f) })
            {
                Transform tree = Pivot(root, "Tree", new Vector3(p.x, 0f, p.y));
                Box("Tree_Trunk", tree, new Vector3(0f, 1.5f, 0f), new Vector3(0.35f, 3f, 0.35f), trunk);
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Tree_Crown";
                crown.transform.SetParent(tree, false);
                crown.transform.localPosition = new Vector3(0f, 4f, 0f);
                crown.transform.localScale = new Vector3(3f, 3.4f, 3f);
                crown.GetComponent<MeshRenderer>().sharedMaterial = leaf;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                trees.Add(tree);
            }
            parts.Trees = trees.ToArray();
        }

        private static void BuildHill(Transform root)
        {
            Material grass = Mat("Hill", Palette.Grass, 0.15f);
            float run = HillBaseZ - HillTopZ;
            float length = Mathf.Sqrt(run * run + HillHeight * HillHeight);
            float angle = Mathf.Atan2(HillHeight, run) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(angle, 0f, 0f);
            Vector3 normal = rotation * Vector3.up;
            Vector3 mid = new Vector3(0f, HillHeight / 2f, (HillBaseZ + HillTopZ) / 2f) - normal * 0.5f;

            GameObject ramp = Box("HillRamp", root, mid, new Vector3(40f, 1f, length), grass);
            ramp.transform.localRotation = rotation;
            Box("HillPlateau", root, new Vector3(0f, HillHeight - 0.5f, HillTopZ - 12.5f), new Vector3(40f, 1f, 25f), grass);
            Box("HillBody", root, new Vector3(0f, (HillHeight - 1f) / 2f, HillTopZ - 12.5f), new Vector3(40f, HillHeight - 1f, 25f), grass);
            BuildShelter(root);
        }

        private static void BuildShelter(Transform root)
        {
            Material shelter = Mat("Shelter", Palette.Trim, 0.3f);
            float y = HillHeight;
            const float sz = ShelterZ;
            Box("Shelter_Roof", root, new Vector3(0f, y + 3f, sz), new Vector3(8f, 0.2f, 6f), shelter);
            foreach (var p in new[] { new Vector2(-3.8f, -2.8f), new Vector2(3.8f, -2.8f), new Vector2(-3.8f, 2.8f), new Vector2(3.8f, 2.8f) })
            {
                Box("Shelter_Pillar", root, new Vector3(p.x, y + 1.5f, sz + p.y), new Vector3(0.2f, 3f, 0.2f), shelter);
            }
            Box("Shelter_SignBoard", root, new Vector3(0f, y + 3.5f, sz + 3.02f), new Vector3(5f, 0.7f, 0.05f), Mat("SignBoard", Palette.SignBlue, 0.3f));
            Label("지진해일 긴급대피장소", root, new Vector3(0f, y + 3.5f, sz + 3.06f), new Vector3(0f, 180f, 0f), Palette.UiAlert, 3f);
            PointLight("Shelter Light", root, new Vector3(0f, y + 2.7f, sz), Palette.LampWarm, 2f, 12f);
        }

        /// <summary>맵 끝에서 떨어지지 않도록 보이지 않는 벽(땅 가장자리, 언덕 옆과 뒤).</summary>
        private static void BuildBoundary(Transform root)
        {
            const float h = 30f;
            float hillEndZ = HillTopZ - 25f;
            Wall(root, new Vector3(GroundMinX, h / 2f, (GroundMaxZ + HillBaseZ) / 2f), new Vector3(1f, h, GroundMaxZ - HillBaseZ + 40f));
            Wall(root, new Vector3(GroundMaxX, h / 2f, (GroundMaxZ + HillBaseZ) / 2f), new Vector3(1f, h, GroundMaxZ - HillBaseZ + 40f));
            Wall(root, new Vector3((GroundMinX - 20f) / 2f, h / 2f, HillBaseZ), new Vector3(-20f - GroundMinX, h, 1f));
            Wall(root, new Vector3((GroundMaxX + 20f) / 2f, h / 2f, HillBaseZ), new Vector3(GroundMaxX - 20f, h, 1f));
            Wall(root, new Vector3(-20f, h / 2f, (HillBaseZ + hillEndZ) / 2f), new Vector3(1f, h, HillBaseZ - hillEndZ));
            Wall(root, new Vector3(20f, h / 2f, (HillBaseZ + hillEndZ) / 2f), new Vector3(1f, h, HillBaseZ - hillEndZ));
            Wall(root, new Vector3(0f, h / 2f, hillEndZ), new Vector3(40f, h, 1f));
        }

        private static void Wall(Transform root, Vector3 pos, Vector3 size)
        {
            GameObject wall = Box("Boundary", root, pos, size, null);
            Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
        }

        private static void BuildZones(Transform root)
        {
            float cx = (GroundMinX + GroundMaxX) / 2f;
            float width = GroundMaxX - GroundMinX;
            // 건물 밖: 바닷가부터 언덕 중턱(높이 9m 아래)까지
            const float outsideMinZ = HillBaseZ - 20f;
            const float outsideMaxZ = 29f;
            ZoneBuilder.Area("Area_Outside", root, new Vector3(cx, 4f, (outsideMinZ + outsideMaxZ) / 2f), new Vector3(width, 10f, outsideMaxZ - outsideMinZ), PlayerArea.Outside);
            ZoneBuilder.Area("Area_Shelter", root, new Vector3(0f, HillHeight + 3f, HillTopZ - 12.5f), new Vector3(40f, 6f, 25f), PlayerArea.Shelter);
            // 건물 외벽에서 충분히 떨어진 넓은 곳(숙일 필요 없음)
            const float openMinZ = HillBaseZ;
            const float openMaxZ = -18f;
            ZoneBuilder.Cover("Cover_OpenSpace", root, new Vector3(cx, 5f, (openMinZ + openMaxZ) / 2f), new Vector3(width, 14f, openMaxZ - openMinZ), false);
            ZoneBuilder.Cover("Cover_Beach", root, new Vector3(cx, 2f, 17.5f), new Vector3(width, 4f, 15f), false);
            ZoneBuilder.Cover("Cover_Shelter", root, new Vector3(0f, HillHeight + 3f, HillTopZ - 12.5f), new Vector3(40f, 6f, 25f), false);
        }

        private static Transform BuildSea(Transform root)
        {
            Material water = Mat("SeaWater", Palette.SeaDeep, 0.92f);
            GameObject sea = Box("SeaWater", root, new Vector3(25f, -10.4f, 175f), new Vector3(350f, 20f, 300f), water);
            Object.DestroyImmediate(sea.GetComponent<Collider>());
            return sea.transform;
        }
    }
}
