using QuakeSurvival.Art;
using QuakeSurvival.Safety;
using UnityEngine;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 1~12층 계단실(꺾인 계단). 플레이어는 10층에서 출발하며, 11층으로 올라가면 무너지는 위층에 깔려 사망한다.
    /// 10~11층 사이 층계참에는 떨어진 콘크리트 조각과 금(단서)이 있다. 층마다 바닥 층계참(x 0~1.2), 첫 계단(남쪽 줄, +x로 하강),
    /// 중간 층계참(x 4~5.4), 둘째 계단(북쪽 줄, -x로 하강). 1층 남쪽 벽에 건물 출구.
    /// </summary>
    internal static class StairwellBuilder
    {
        private const int StepsPerFlight = 10;
        private const float StepRise = 0.15f;
        private const float StepRun = 0.28f;
        private const float FlightStartX = 1.2f;
        private const float FlightEndX = 4.0f;
        private const float Width = 5.4f;
        private const float NorthZ = -4f;
        private const float SouthZ = -7f;
        private const float CenterZ = -5.5f;

        /// <summary>계단실을 만들고 위층 붕괴 위험 구역을 돌려준다(지진 시작 때 켜도록 SystemsBuilder가 연결).</summary>
        public static GameObject Build()
        {
            Transform root = Root("Stairwell", Vector3.zero);
            Material concrete = Mat("Concrete", Palette.Concrete, 0.25f);
            Material wall = Mat("StairWall", Palette.StairWall);
            float top = FloorY(TopBuildingFloor) + FloorHeight;

            for (int floor = 1; floor <= TopBuildingFloor; floor++)
            {
                BuildFloor(root, floor, concrete, wall);
            }
            GameObject upperHazard = BuildUpperCollapse(root);

            Box("GroundSlab", root, new Vector3(Width / 2f, -0.075f, CenterZ), new Vector3(Width, 0.15f, 3f), concrete);
            BuildOuterWalls(root, wall, top);
            ZoneBuilder.Area("Area_Stairwell", root, new Vector3(Width / 2f, top / 2f, CenterZ), new Vector3(Width, top, 3f), PlayerArea.Stairwell);
            ZoneBuilder.Cover("Cover_Stairwell", root, new Vector3(Width / 2f, top / 2f, CenterZ), new Vector3(Width, top, 3f), true);
            return upperHazard;
        }

        private static void BuildFloor(Transform root, int floor, Material concrete, Material wall)
        {
            float y0 = FloorY(floor);
            Box($"Landing_{floor}F", root, new Vector3(0.6f, y0 - 0.075f, CenterZ), new Vector3(1.2f, 0.15f, 3f), concrete);
            PointLight($"Emergency_{floor}F", root, new Vector3(0.6f, y0 + 2.5f, CenterZ), Palette.AccentEmergency, 1.2f, 6f);

            if (floor != PlayerFloor)
            {
                // 다른 층 복도로 가는 문은 닫혀 있다(벽 + 문 모양)
                Box($"WestWall_{floor}F", root, new Vector3(-0.05f, y0 + 1.5f, CenterZ), new Vector3(0.1f, 3f, 3f), wall);
                Box($"ClosedDoor_{floor}F", root, new Vector3(0.02f, y0 + 1.05f, CenterZ), new Vector3(0.04f, 2.1f, 1f), Mat("Door", Palette.Door, 0.4f));
            }
            Label($"{floor}F", root, new Vector3(0.06f, y0 + 1.9f, -6.5f), new Vector3(0f, -90f, 0f), Palette.ExitGreen);

            if (floor == 1) return;
            BuildFlights(root, y0, concrete, floor);
        }

        private static void BuildFlights(Transform root, float y0, Material concrete, int floor)
        {
            float midY = y0 - StepsPerFlight * StepRise;
            Box($"MidLanding_{floor}F", root, new Vector3(4.7f, midY - 0.075f, CenterZ), new Vector3(1.4f, 0.15f, 3f), concrete);

            const float southLaneZ = -6.25f;
            const float northLaneZ = -4.75f;
            for (int i = 0; i < StepsPerFlight; i++)
            {
                float down = StepRise * (i + 1);
                float x1 = FlightStartX + StepRun * i + StepRun / 2f;
                Box($"Step_{floor}F_A{i}", root, new Vector3(x1, y0 - down - StepRise / 2f, southLaneZ), new Vector3(StepRun, StepRise, 1.45f), concrete);
                float x2 = FlightEndX - StepRun * i - StepRun / 2f;
                Box($"Step_{floor}F_B{i}", root, new Vector3(x2, midY - down - StepRise / 2f, northLaneZ), new Vector3(StepRun, StepRise, 1.45f), concrete);
            }
        }

        /// <summary>
        /// 10층 위로 올라가는 길: 중간 층계참에 떨어진 잔해와 벽의 금(단서),
        /// 11층 바닥 높이부터 위는 위험 구역(위층 붕괴 = 사망).
        /// </summary>
        private static GameObject BuildUpperCollapse(Transform root)
        {
            int dangerFloor = PlayerFloor + 1;
            float midY = FloorY(dangerFloor) - StepsPerFlight * StepRise;
            Material rubble = Mat("SinkholeRubble", Palette.Hex(0x4B4640), 0.15f);
            Material crack = UnlitMat("GroundCrack", Palette.Hex(0x0A0A0B));
            var chunks = new[]
            {
                (new Vector3(4.3f, midY + 0.12f, -6.3f), 0.45f, 20f), (new Vector3(5.0f, midY + 0.1f, -5.2f), 0.35f, 65f),
                (new Vector3(4.6f, midY + 0.08f, -4.6f), 0.3f, 130f), (new Vector3(5.1f, midY + 0.15f, -6.6f), 0.55f, 200f),
            };
            foreach (var (pos, size, yaw) in chunks)
            {
                GameObject chunk = ApartmentShellBuilder.Decor("FallenConcrete", root, pos, new Vector3(size, size * 0.5f, size * 0.8f), rubble);
                chunk.transform.localRotation = Quaternion.Euler(12f, yaw, -8f);
            }
            // 동쪽 벽(층계참 쪽)에 대각선으로 간 금
            for (int i = 0; i < 4; i++)
            {
                GameObject seg = ApartmentShellBuilder.Decor("WallCrack", root,
                    new Vector3(Width - 0.01f, midY + 0.9f + i * 0.55f, -6.4f + i * 0.45f + (i % 2) * 0.15f), new Vector3(0.02f, 0.7f, 0.06f), crack);
                seg.transform.localRotation = Quaternion.Euler(i % 2 == 0 ? 35f : -25f, 0f, 0f);
            }

            float bottom = FloorY(dangerFloor) - 0.1f;
            float top = FloorY(TopBuildingFloor) + FloorHeight;
            return ZoneBuilder.Hazard("Hazard_UpperFloorCollapse", root, new Vector3(Width / 2f, (bottom + top) / 2f, CenterZ),
                new Vector3(Width, top - bottom, 3f), DeathCause.UpperFloorCollapse);
        }

        private static void BuildOuterWalls(Transform root, Material wall, float top)
        {
            const float exitWidth = 1.2f;
            const float exitHeight = 2.2f;
            Box("Wall_East", root, new Vector3(Width + 0.05f, top / 2f, CenterZ), new Vector3(0.1f, top, 3.2f), wall);
            Box("Wall_North", root, new Vector3(Width / 2f, top / 2f, NorthZ + 0.05f), new Vector3(Width, top, 0.1f), wall);
            Box("Wall_South", root, new Vector3((exitWidth + Width) / 2f, top / 2f, SouthZ - 0.05f), new Vector3(Width - exitWidth, top, 0.1f), wall);
            Box("Wall_South_AboveExit", root, new Vector3(exitWidth / 2f, (exitHeight + top) / 2f, SouthZ - 0.05f), new Vector3(exitWidth, top - exitHeight, 0.1f), wall);
            Box("Divider", root, new Vector3((FlightStartX + FlightEndX) / 2f, top / 2f, CenterZ), new Vector3(FlightEndX - FlightStartX, top, 0.1f), wall);
            Box("Roof", root, new Vector3(Width / 2f, top + 0.05f, CenterZ), new Vector3(Width, 0.1f, 3f), wall);
            Label("EXIT", root, new Vector3(0.6f, exitHeight + 0.2f, SouthZ + 0.05f), new Vector3(0f, 180f, 0f), Palette.ExitGreen);
        }
    }
}
