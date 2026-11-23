using QuakeSurvival.Art;
using QuakeSurvival.Interaction;
using QuakeSurvival.Safety;
using UnityEditor;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 10층 투룸 아파트: 구조(ApartmentShellBuilder) + 가구(FurnitureBuilder) +
    /// 조작 물건(가스 밸브, 차단기, 현관문, 신발, 비상 가방) + 식탁(몸 보호) + 깨진 유리 + 조명.
    /// </summary>
    internal static class ApartmentBuilder
    {
        public static readonly Vector3 GasValveLocal = new Vector3(4.86f, 1.2f, 0.5f);
        public static readonly Vector3 BreakerLocal = new Vector3(-4.85f, 1.5f, -3.2f);
        public static readonly Vector3 SpawnLocal = new Vector3(-0.75f, 0.05f, -1.4f);

        public sealed class Parts
        {
            public Light[] Lamps;
            public SwitchInteractable[] Switches;
            public GameObject[] GlassShards;
        }

        public static Parts Build()
        {
            Transform root = Root("Apartment_10F", new Vector3(0f, TopFloorY, 0f));
            ApartmentShellBuilder.Build(root);
            FurnitureBuilder.Build(root);
            BuildDiningTable(root);
            ZoneBuilder.Area("Area_Apartment", root, new Vector3(0f, 1.3f, 0f), new Vector3(10f, 2.6f, 8f), PlayerArea.Apartment);
            return new Parts
            {
                Lamps = BuildLamps(root),
                Switches = BuildInteractables(root),
                GlassShards = BuildGlass(root),
            };
        }

        private static Light[] BuildLamps(Transform root)
        {
            Color warm = Palette.LampWarm;
            return new[]
            {
                PointLight("Lamp_Living", root, new Vector3(-0.5f, 2.35f, -1.5f), warm, 2.2f, 7f),
                PointLight("Lamp_Kitchen", root, new Vector3(3.5f, 2.35f, -1.5f), warm, 1.8f, 6f),
                PointLight("Lamp_Room1", root, new Vector3(0.25f, 2.35f, 2.5f), warm, 1.2f, 5f),
                PointLight("Lamp_Room2", root, new Vector3(-3.2f, 2.35f, 2.5f), warm, 1.2f, 5f),
            };
        }

        /// <summary>부엌 식탁: 지진 때 몸을 보호할 튼튼한 탁자. 아래에 CoverZone.</summary>
        private static void BuildDiningTable(Transform root)
        {
            Material wood = Mat("WoodDark", Palette.WoodDark, 0.35f);
            Transform table = Pivot(root, "DiningTable", new Vector3(2.8f, 0f, -1.2f));
            Box("Top", table, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.06f, 0.9f), wood);
            foreach (var p in new[] { new Vector2(-0.75f, -0.4f), new Vector2(0.75f, -0.4f), new Vector2(-0.75f, 0.4f), new Vector2(0.75f, 0.4f) })
            {
                Box("Leg", table, new Vector3(p.x, 0.36f, p.y), new Vector3(0.06f, 0.72f, 0.06f), wood);
            }
            ZoneBuilder.Cover("Cover_UnderTable", table, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.7f, 0.8f), true);
        }

        private static SwitchInteractable[] BuildInteractables(Transform root)
        {
            // 가스 밸브: 부엌 동쪽 벽. 손잡이가 배관과 나란하면 열림, 90도 돌리면 잠김
            Material yellow = Mat("GasYellow", Palette.GasPipe, 0.5f);
            var gas = Switch(root, "GasValve", GasValveLocal, SafetyChecklist.GasValve, "가스 밸브 잠그기", "가스 밸브 열기");
            SetBool(gas, "oneWay", false);
            Box("Pipe", gas.transform, Vector3.zero, new Vector3(0.05f, 0.6f, 0.05f), yellow);
            var gasHandle = Pivot(gas.transform, "HandlePivot", new Vector3(-0.06f, 0f, 0f));
            Box("Handle", gasHandle, Vector3.zero, new Vector3(0.03f, 0.18f, 0.04f), yellow);
            SetVisual(gas, gasHandle, Vector3.zero, new Vector3(90f, 0f, 0f), false);

            // 전기 차단기: 현관 옆 분전함, 레버를 아래로 내림
            var breaker = Switch(root, "BreakerPanel", BreakerLocal, SafetyChecklist.Breaker, "전기 차단기 내리기", "전기 차단기 올리기");
            SetBool(breaker, "oneWay", false);
            Box("Box", breaker.transform, Vector3.zero, new Vector3(0.1f, 0.4f, 0.3f), Mat("Panel", Palette.PanelBox, 0.4f));
            var lever = Pivot(breaker.transform, "LeverPivot", new Vector3(0.07f, 0f, 0f));
            Box("Lever", lever, new Vector3(0.04f, 0f, 0f), new Vector3(0.08f, 0.03f, 0.05f), Mat("Black", Palette.ScreenOff, 0.5f));
            SetVisual(breaker, lever, new Vector3(0f, 0f, 45f), new Vector3(0f, 0f, -45f), false);

            // 현관문: 경첩(x=-4.5)을 축으로 복도 쪽으로 열림
            var door = Switch(root, "FrontDoor", new Vector3(-4.5f, 0f, -4f), SafetyChecklist.Door, "현관문 열기", "문 열림");
            var hinge = Pivot(door.transform, "Hinge", Vector3.zero);
            Box("Panel", hinge, new Vector3(0.75f, 1.025f, 0f), new Vector3(1.5f, 2.05f, 0.08f), Mat("Door", Palette.Door, 0.4f));
            ApartmentShellBuilder.Decor("Handle", hinge, new Vector3(1.35f, 1f, -0.07f), new Vector3(0.12f, 0.03f, 0.04f), Mat("Metal", Palette.Metal, 0.8f));
            SetVisual(door, hinge, Vector3.zero, new Vector3(0f, 100f, 0f), false);

            return new[] { gas, breaker, door, BuildShoes(root), BuildBag(root) };
        }

        private static SwitchInteractable BuildShoes(Transform root)
        {
            Material shoe = Mat("Shoes", Palette.Shoes, 0.3f);
            var shoes = Switch(root, "Shoes", new Vector3(-4.1f, 0f, -3.3f), SafetyChecklist.Shoes, "신발 신기", "신발 신음");
            var visual = Pivot(shoes.transform, "Visual", Vector3.zero);
            Box("Left", visual, new Vector3(-0.08f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 0.28f), shoe);
            Box("Right", visual, new Vector3(0.08f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 0.28f), shoe);
            SetVisual(shoes, visual, Vector3.zero, Vector3.zero, true);
            return shoes;
        }

        private static SwitchInteractable BuildBag(Transform root)
        {
            var item = Switch(root, "EmergencyBag", new Vector3(-4.65f, 0.9f, -1.6f), SafetyChecklist.EmergencyBag, "비상 가방 챙기기", "비상 가방 챙김");
            var visual = Pivot(item.transform, "Visual", Vector3.zero);
            Box("Bag", visual, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.4f, 0.35f), Mat("EmergencyBag", Palette.EmergencyBag, 0.2f));
            SetVisual(item, visual, Vector3.zero, Vector3.zero, true);
            return item;
        }

        /// <summary>전진 때 나타나는 깨진 유리(거실에서 현관으로 가는 길목, 부엌). 처음엔 꺼진 상태.</summary>
        private static GameObject[] BuildGlass(Transform root)
        {
            return new[]
            {
                GlassPatch(root, "Glass_Entrance", new Vector3(-2.4f, 0f, -2.3f), new Vector2(1.6f, 1.4f)),
                GlassPatch(root, "Glass_Kitchen", new Vector3(3.6f, 0f, -2.7f), new Vector2(1.2f, 1.2f)),
            };
        }

        private static GameObject GlassPatch(Transform root, string name, Vector3 center, Vector2 size)
        {
            Material glass = Mat("GlassShard", Palette.Hex(0xBFDCE6), 0.95f);
            GameObject zone = Trigger(name, root, center + new Vector3(0f, 0.1f, 0f), new Vector3(size.x, 0.2f, size.y));
            zone.AddComponent<GlassShards>();
            var rng = new System.Random(name.Length * 7919);
            for (int i = 0; i < 16; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * size.x;
                float z = ((float)rng.NextDouble() - 0.5f) * size.y;
                GameObject shard = ApartmentShellBuilder.Decor("Shard", zone.transform, new Vector3(x, -0.09f, z), new Vector3(0.12f, 0.01f, 0.08f), glass);
                shard.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
            }
            return zone;
        }

        private static SwitchInteractable Switch(Transform parent, string name, Vector3 localPos, string id, string promptOff, string promptOn)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sw = go.AddComponent<SwitchInteractable>();
            var so = new SerializedObject(sw);
            so.FindProperty("actionId").stringValue = id;
            so.FindProperty("promptWhenOff").stringValue = promptOff;
            so.FindProperty("promptWhenOn").stringValue = promptOn;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sw;
        }

        private static void SetVisual(SwitchInteractable sw, Transform visual, Vector3 off, Vector3 on, bool hideWhenOn)
        {
            var so = new SerializedObject(sw);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("offEuler").vector3Value = off;
            so.FindProperty("onEuler").vector3Value = on;
            so.FindProperty("hideVisualWhenOn").boolValue = hideWhenOn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
