using QuakeSurvival.Art;
using QuakeSurvival.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static QuakeSurvival.EditorTools.SceneBuildKit;

namespace QuakeSurvival.EditorTools
{
    /// <summary>
    /// 휴대폰 지도 화면(HUD 오른쪽 아래). 실제 씬 배치(월드 x,z)를 같은 비율로 줄여 그린다.
    /// 위쪽이 북쪽(바다)이다. 좌표를 바꾸면 PhoneMapUI의 worldCenter/pixelsPerMeter도 같이 맞춘다.
    /// </summary>
    internal static class PhoneMapBuilder
    {
        // 지도에 담는 범위: x -40~115, z -140~40 (아파트, 학교, 남쪽 언덕, 바다)
        private static readonly Vector2 WorldCenter = new Vector2(37.5f, -50f);
        private const float PixelsPerMeter = 2.15f;
        private static readonly Vector2 MapSize = new Vector2(340f, 400f);

        public static PhoneMapUI Build(Transform canvas, Transform player)
        {
            Image frame = HudBuilder.UiImage(canvas, "Phone", Palette.WithAlpha(Palette.Shadow, 0.95f), new Vector2(760f, -150f), new Vector2(380f, 620f));
            Transform f = frame.transform;

            TextMeshProUGUI title = HudBuilder.UiText(f, "Title", "대피 지도", 22, new Vector2(0f, 280f), new Vector2(340f, 36f), true);
            title.color = Palette.UiAlert;

            Image mapBack = HudBuilder.UiImage(f, "Map", Palette.Asphalt, new Vector2(0f, 45f), MapSize);
            mapBack.gameObject.AddComponent<RectMask2D>();
            Transform map = mapBack.transform;
            DrawTerrain(map);
            DrawLabels(map);

            TextMeshProUGUI marker = HudBuilder.UiText(map, "PlayerMarker", "▲", 26, Vector2.zero, new Vector2(30f, 30f), true);
            marker.color = Palette.AccentEmergency;

            TextMeshProUGUI menu = HudBuilder.UiText(f, "Menu", "", 22, new Vector2(0f, -225f), new Vector2(340f, 80f), false);
            menu.alignment = TextAlignmentOptions.Left;

            var ui = canvas.gameObject.AddComponent<PhoneMapUI>();
            SetRef(ui, "panel", frame.gameObject);
            SetRef(ui, "playerMarker", marker.rectTransform);
            SetRef(ui, "menuText", menu);
            SetRef(ui, "player", player);
            var so = new UnityEditor.SerializedObject(ui);
            so.FindProperty("worldCenter").vector2Value = WorldCenter;
            so.FindProperty("pixelsPerMeter").floatValue = PixelsPerMeter;
            so.FindProperty("mapHalfSize").vector2Value = MapSize / 2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            frame.gameObject.SetActive(false);
            return ui;
        }

        private static void DrawTerrain(Transform map)
        {
            Area(map, "Sea", Palette.SeaDeep, -60f, 130f, 25f, 50f);
            Area(map, "Beach", Palette.Sand, -60f, 130f, 6f, 25f);
            Area(map, "Road", Palette.WithAlpha(Palette.Concrete, 0.55f), -60f, 130f, -21f, -17f);
            Area(map, "Hill", Palette.Grass, -20f, 20f, ExteriorBuilder.HillTopZ - 25f, ExteriorBuilder.HillBaseZ);
            Area(map, "Shelter", Palette.SignBlue, -4f, 4f, ExteriorBuilder.ShelterZ - 3f, ExteriorBuilder.ShelterZ + 3f);
            Area(map, "Apartment", Palette.Facade, -5f, 5.4f, -9f, 4f);
            Area(map, "Playground", Palette.Dirt, SchoolBuilder.FieldMinX, SchoolBuilder.FieldMaxX, SchoolBuilder.FieldMinZ, SchoolBuilder.FieldMaxZ);
            Area(map, "SchoolBuilding", Palette.SchoolWall, SchoolBuilder.BuildingMinX, SchoolBuilder.BuildingMaxX, SchoolBuilder.FieldMinZ + 0.5f, SchoolBuilder.FieldMaxZ - 0.5f);

            Vector3 field = SchoolBuilder.FieldCenter;
            Dot(map, "SiteDot", Palette.ExitGreen, field.x, field.z, 14f);
            Dot(map, "ShelterDot", Palette.TsunamiArrow, 0f, ExteriorBuilder.ShelterZ, 14f);
        }

        private static void DrawLabels(Transform map)
        {
            Text(map, "N ↑  바다", 37.5f, 35f, Palette.UiText, 16);
            Text(map, "우리 아파트", 0f, -2.5f, Palette.UiText, 15);
            Vector3 field = SchoolBuilder.FieldCenter;
            Text(map, "해안초등학교\n지진 옥외대피장소", field.x - 8.5f, field.z - 16f, Palette.ExitGreen, 15);
            Text(map, "남쪽 언덕\n지진해일 대피소", 0f, -100f, Palette.TsunamiArrow, 15);
        }

        private static Vector2 ToMap(float x, float z) => new Vector2(x - WorldCenter.x, z - WorldCenter.y) * PixelsPerMeter;

        private static void Area(Transform map, string name, Color color, float x0, float x1, float z0, float z1)
        {
            Vector2 center = ToMap((x0 + x1) / 2f, (z0 + z1) / 2f);
            var size = new Vector2(x1 - x0, z1 - z0) * PixelsPerMeter;
            HudBuilder.UiImage(map, name, color, center, size);
        }

        private static void Dot(Transform map, string name, Color color, float x, float z, float size)
        {
            HudBuilder.UiImage(map, name, color, ToMap(x, z), new Vector2(size, size));
        }

        private static void Text(Transform map, string text, float x, float z, Color color, int size)
        {
            TextMeshProUGUI t = HudBuilder.UiText(map, "Label", text, size, ToMap(x, z), new Vector2(170f, 48f), true);
            t.color = color;
            t.lineSpacing = -10f;
        }
    }
}
