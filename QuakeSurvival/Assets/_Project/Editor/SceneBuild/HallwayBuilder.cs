using QuakeSurvival.Art;
using QuakeSurvival.Interaction;
using QuakeSurvival.Safety;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>10층 복도와 엘리베이터(호출 버튼, 문, 갇힘 위험 구역).</summary>
    internal static class HallwayBuilder
    {
        public sealed class Parts
        {
            public Light Lamp;
            public ElevatorCall Elevator;
        }

        public static Parts Build()
        {
            Transform root = Root("Hallway_10F", new Vector3(0f, TopFloorY, 0f));
            Material floor = Mat("HallFloor", Palette.Tile, 0.45f);
            Material wall = Mat("HallWall", Palette.WallPlaster);

            Box("Floor", root, new Vector3(-2.5f, -0.05f, -5.5f), new Vector3(5f, 0.1f, 3f), floor);
            Box("Ceiling", root, new Vector3(-2.5f, 2.65f, -5.5f), new Vector3(5f, 0.1f, 3f), wall);
            Box("Wall_West", root, new Vector3(-5f, 1.3f, -5.5f), new Vector3(0.2f, 2.6f, 3f), wall);
            // 남쪽 벽: 엘리베이터 자리(x=-2.5 ~ -1.5)를 비워 둔다. 동쪽은 계단실로 열려 있다.
            Box("Wall_South_A", root, new Vector3(-3.75f, 1.3f, -7f), new Vector3(2.5f, 2.6f, 0.2f), wall);
            Box("Wall_South_B", root, new Vector3(-0.75f, 1.3f, -7f), new Vector3(1.5f, 2.6f, 0.2f), wall);
            Box("Wall_South_Top", root, new Vector3(-2f, 2.35f, -7f), new Vector3(1f, 0.5f, 0.2f), wall);

            ZoneBuilder.Area("Area_Hallway", root, new Vector3(-2.5f, 1.3f, -5.5f), new Vector3(5f, 2.6f, 3f), PlayerArea.Hallway);
            ZoneBuilder.Cover("Cover_HallWall", root, new Vector3(-3.8f, 0.35f, -6.6f), new Vector3(2.4f, 0.7f, 0.6f), true);

            Light lamp = PointLight("Hall Lamp", root, new Vector3(-2.5f, 2.4f, -5.5f), Palette.LampWarm, 2f, 7f);
            PointLight("Emergency Light", root, new Vector3(-0.3f, 2.4f, -5.5f), Palette.AccentEmergency, 0.8f, 6f);

            return new Parts { Lamp = lamp, Elevator = BuildElevator(root) };
        }

        private static ElevatorCall BuildElevator(Transform root)
        {
            Material steel = Mat("Steel", Palette.Metal, 0.75f);
            Material button = EmissiveMat("ElevatorButton", Palette.UiAlert, 0.8f);

            Transform car = Pivot(root, "ElevatorCar", Vector3.zero);
            Box("Floor", car, new Vector3(-2f, -0.05f, -7.9f), new Vector3(1.2f, 0.1f, 1.6f), steel);
            Box("Ceiling", car, new Vector3(-2f, 2.65f, -7.9f), new Vector3(1.2f, 0.1f, 1.6f), steel);
            Box("Back", car, new Vector3(-2f, 1.3f, -8.75f), new Vector3(1.2f, 2.6f, 0.1f), steel);
            Box("Side_W", car, new Vector3(-2.6f, 1.3f, -7.9f), new Vector3(0.1f, 2.6f, 1.6f), steel);
            Box("Side_E", car, new Vector3(-1.4f, 1.3f, -7.9f), new Vector3(0.1f, 2.6f, 1.6f), steel);
            GameObject hazard = ZoneBuilder.Hazard("Hazard_ElevatorCar", car, new Vector3(-2f, 1f, -7.95f), new Vector3(1f, 2f, 1.4f), DeathCause.ElevatorTrapped);

            Transform door = Pivot(root, "ElevatorDoor", new Vector3(-2f, 0f, -7.05f));
            Box("Panel", door, new Vector3(0f, 1.05f, 0f), new Vector3(1f, 2.1f, 0.06f), steel);

            GameObject btn = Box("ElevatorButton", root, new Vector3(-1.2f, 1.2f, -6.88f), new Vector3(0.12f, 0.2f, 0.05f), button);
            var call = btn.AddComponent<ElevatorCall>();
            SetRef(call, "doorPanel", door);
            SetRef(call, "carHazard", hazard);
            return call;
        }
    }
}
