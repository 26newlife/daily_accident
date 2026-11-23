using QuakeSurvival.Art;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 기본 도형으로 만든 가구(임시). Asset Store 가구를 받으면 이 파일의 해당 함수만 프리팹 배치로 바꾸면 된다.
    /// 큰 가구는 충돌을 남기고(길을 막음), 장식(러그, 조명, 소품)은 충돌을 뺀다.
    /// </summary>
    internal static class FurnitureBuilder
    {
        public static void Build(Transform root)
        {
            Transform f = Pivot(root, "Furniture", Vector3.zero);
            Living(f);
            Kitchen(f);
            Bathroom(f);
            Room1(f);
            Room2(f);
            Veranda(f);
            Entrance(f);
        }

        private static void Living(Transform f)
        {
            Material fabric = Mat("Fabric", Palette.Fabric, 0.15f);
            Material wood = Mat("WoodDark", Palette.WoodDark, 0.35f);

            // 소파(남쪽 벽, 북쪽 바다를 향함)
            Box("Sofa_Base", f, new Vector3(-0.75f, 0.22f, -3.4f), new Vector3(2.2f, 0.44f, 0.85f), fabric);
            Box("Sofa_Back", f, new Vector3(-0.75f, 0.6f, -3.72f), new Vector3(2.2f, 0.55f, 0.22f), fabric);
            Box("Sofa_ArmL", f, new Vector3(-1.8f, 0.4f, -3.4f), new Vector3(0.2f, 0.45f, 0.85f), fabric);
            Box("Sofa_ArmR", f, new Vector3(0.3f, 0.4f, -3.4f), new Vector3(0.2f, 0.45f, 0.85f), fabric);
            Material cushion = Mat("Cushion", Palette.FabricWarm, 0.1f);
            ApartmentShellBuilder.Decor("Cushion", f, new Vector3(-1.4f, 0.55f, -3.5f), new Vector3(0.35f, 0.3f, 0.12f), cushion);

            ApartmentShellBuilder.Decor("Rug", f, new Vector3(-0.75f, 0.012f, -2.2f), new Vector3(2.6f, 0.01f, 1.8f), Mat("Rug", Palette.FabricWarm, 0.05f));
            Box("CoffeeTable", f, new Vector3(-0.75f, 0.2f, -2.2f), new Vector3(1.1f, 0.4f, 0.55f), wood);

            Box("TvStand", f, new Vector3(-0.9f, 0.25f, 0.75f), new Vector3(1.6f, 0.5f, 0.4f), wood);
            Box("Tv", f, new Vector3(-0.9f, 0.85f, 0.8f), new Vector3(1.3f, 0.75f, 0.06f), Mat("ScreenOff", Palette.ScreenOff, 0.85f));
            CeilingLight(f, new Vector3(-0.5f, 0f, -1.5f));
        }

        private static void Kitchen(Transform f)
        {
            Material cabinet = Mat("Cabinet", Palette.Trim, 0.3f);
            Material top = Mat("Countertop", Palette.Metal, 0.6f);
            // 동쪽 벽 조리대(가스 밸브는 이 위 벽에 있다)
            Box("Counter", f, new Vector3(4.6f, 0.43f, -1.45f), new Vector3(0.6f, 0.86f, 3.4f), cabinet);
            ApartmentShellBuilder.Decor("Countertop", f, new Vector3(4.58f, 0.89f, -1.45f), new Vector3(0.66f, 0.04f, 3.44f), top);
            ApartmentShellBuilder.Decor("Cooktop", f, new Vector3(4.6f, 0.92f, -0.2f), new Vector3(0.5f, 0.02f, 0.6f), Mat("ScreenOff", Palette.ScreenOff, 0.85f));
            ApartmentShellBuilder.Decor("Sink", f, new Vector3(4.6f, 0.9f, -2.2f), new Vector3(0.45f, 0.03f, 0.6f), Mat("Metal", Palette.Metal, 0.8f));
            ApartmentShellBuilder.Decor("UpperCabinet", f, new Vector3(4.72f, 1.95f, -2f), new Vector3(0.35f, 0.7f, 2.4f), cabinet);
            Box("Fridge", f, new Vector3(4.55f, 0.9f, -3.55f), new Vector3(0.75f, 1.8f, 0.7f), Mat("Appliance", Palette.Appliance, 0.7f));
            CeilingLight(f, new Vector3(3.5f, 0f, -1.5f));

            // 식탁(몸 보호용 튼튼한 탁자) 의자는 북쪽에만 두어 남쪽으로 기어들어갈 수 있게 한다
            Material wood = Mat("WoodDark", Palette.WoodDark, 0.35f);
            Chair(f, new Vector3(2.4f, 0f, -0.4f), wood);
            Chair(f, new Vector3(3.2f, 0f, -0.4f), wood);
        }

        private static void Bathroom(Transform f)
        {
            Material white = Mat("Porcelain", Palette.Appliance, 0.8f);
            Box("Bathtub", f, new Vector3(-4.6f, 0.28f, 0.2f), new Vector3(0.7f, 0.56f, 1.5f), white);
            Box("Toilet", f, new Vector3(-3.5f, 0.22f, -0.3f), new Vector3(0.4f, 0.44f, 0.5f), white);
            ApartmentShellBuilder.Decor("Mirror", f, new Vector3(-3.95f, 1.5f, 0.92f), new Vector3(0.6f, 0.7f, 0.02f), GlassMat("Mirror", Palette.Glass))
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Room1(Transform f)
        {
            Material wood = Mat("WoodDark", Palette.WoodDark, 0.35f);
            Box("Desk", f, new Vector3(0.25f, 0.37f, 3.55f), new Vector3(1.4f, 0.74f, 0.6f), wood);
            Chair(f, new Vector3(0.25f, 0f, 2.9f), wood);
            Box("Bookshelf", f, new Vector3(-1.25f, 0.9f, 2.6f), new Vector3(0.35f, 1.8f, 1.1f), wood);
            for (int i = 0; i < 3; i++)
            {
                ApartmentShellBuilder.Decor("Books", f, new Vector3(-1.2f, 0.45f + i * 0.5f, 2.6f), new Vector3(0.25f, 0.28f, 0.9f), Mat("Books", Palette.FabricWarm, 0.2f));
            }
            Box("Wardrobe", f, new Vector3(1.6f, 1f, 1.6f), new Vector3(0.6f, 2f, 1f), Mat("Cabinet", Palette.Trim, 0.3f));
            CeilingLight(f, new Vector3(0.25f, 0f, 2.5f));
        }

        private static void Room2(Transform f)
        {
            Material wood = Mat("WoodDark", Palette.WoodDark, 0.35f);
            Box("Bed_Frame", f, new Vector3(-3.6f, 0.2f, 2.9f), new Vector3(1.6f, 0.4f, 2f), wood);
            Box("Bed_Mattress", f, new Vector3(-3.6f, 0.5f, 2.85f), new Vector3(1.5f, 0.2f, 1.9f), Mat("Bedding", Palette.Bedding, 0.1f));
            Box("Bed_Head", f, new Vector3(-3.6f, 0.7f, 3.88f), new Vector3(1.6f, 0.9f, 0.08f), wood);
            ApartmentShellBuilder.Decor("Pillow", f, new Vector3(-3.6f, 0.66f, 3.55f), new Vector3(1.1f, 0.12f, 0.35f), Mat("Trim", Palette.Trim, 0.3f));
            Box("Nightstand", f, new Vector3(-2.5f, 0.25f, 3.6f), new Vector3(0.45f, 0.5f, 0.4f), wood);
            CeilingLight(f, new Vector3(-3.2f, 0f, 2.5f));
        }

        private static void Veranda(Transform f)
        {
            Material pot = Mat("Pot", Palette.FabricWarm, 0.2f);
            Material leaf = Mat("Leaf", Palette.Grass, 0.2f);
            foreach (var p in new[] { new Vector3(4.5f, 0f, 3.5f), new Vector3(2.5f, 0f, 3.5f) })
            {
                Box("PlantPot", f, p + new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 0.4f), pot);
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Plant";
                crown.transform.SetParent(f, false);
                crown.transform.localPosition = p + new Vector3(0f, 0.75f, 0f);
                crown.transform.localScale = new Vector3(0.6f, 0.75f, 0.6f);
                crown.GetComponent<MeshRenderer>().sharedMaterial = leaf;
                Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
        }

        private static void Entrance(Transform f)
        {
            Box("ShoeCabinet", f, new Vector3(-4.7f, 0.45f, -1.8f), new Vector3(0.4f, 0.9f, 1.2f), Mat("WoodDark", Palette.WoodDark, 0.35f));
        }

        private static void Chair(Transform f, Vector3 pos, Material wood)
        {
            Transform chair = Pivot(f, "Chair", pos);
            Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), wood);
            ApartmentShellBuilder.Decor("Back", chair, new Vector3(0f, 0.75f, 0.19f), new Vector3(0.42f, 0.55f, 0.04f), wood);
            foreach (var l in new[] { new Vector2(-0.18f, -0.18f), new Vector2(0.18f, -0.18f), new Vector2(-0.18f, 0.18f), new Vector2(0.18f, 0.18f) })
            {
                ApartmentShellBuilder.Decor("Leg", chair, new Vector3(l.x, 0.22f, l.y), new Vector3(0.04f, 0.44f, 0.04f), wood);
            }
        }

        private static void CeilingLight(Transform f, Vector3 pos)
        {
            ApartmentShellBuilder.Decor("CeilingLight", f, new Vector3(pos.x, ApartmentShellBuilder.Height - 0.03f, pos.z),
                new Vector3(0.5f, 0.04f, 0.5f), EmissiveMat("LampFixture", Palette.LampWarm, 0.6f));
        }
    }
}
