using UnityEngine;

namespace QuakeSurvival.Art
{
    /// <summary>
    /// 아트 바이블 - 모든 색의 단일 출처. 재질, 조명, 안개, 파티클, UI는 여기 값만 쓴다.
    ///
    /// 스타일: 사실적인 비율의 단순한 형태(로우 디테일) 해안 아파트. 질감보다 색과 빛으로 분위기를 만든다.
    /// 분위기: 폭우 내리는 해 질 녘. 북쪽 바다 창으로 들어오는 차가운 회청색 빛, 실내는 따뜻한 전등.
    ///         본진 정전 뒤에는 붉은 비상등과 손전등, 번개만 남는다. 그림자는 검정이 아니라 짙은 남색.
    /// 강조색: 비상 상황을 알리는 신호 빨강(비상등, 경고, 사망 화면) 하나만 쓴다.
    /// </summary>
    public static class Palette
    {
        // 실내
        public static readonly Color FloorOak = Hex(0x7A5C40);
        public static readonly Color WallPlaster = Hex(0xD9D2C3);
        public static readonly Color Trim = Hex(0xEFEAE0);
        public static readonly Color WoodDark = Hex(0x4A3426);
        public static readonly Color Fabric = Hex(0x4F6A7A);
        public static readonly Color FabricWarm = Hex(0xA8836A);
        public static readonly Color Bedding = Hex(0xC9CED6);
        public static readonly Color Tile = Hex(0xB9C1C4);
        public static readonly Color Appliance = Hex(0xE4E6E3);
        public static readonly Color Metal = Hex(0x8A9097);
        public static readonly Color Glass = new Color(0.66f, 0.78f, 0.84f, 0.28f);
        public static readonly Color ScreenOff = Hex(0x15191E);

        // 조작 물건(눈에 띄어야 하는 것)
        public static readonly Color GasPipe = Hex(0xE3B23C);
        public static readonly Color PanelBox = Hex(0xC8C6BE);
        public static readonly Color Door = Hex(0x5B4A3E);
        public static readonly Color Shoes = Hex(0x2F3D5C);
        public static readonly Color EmergencyBag = Hex(0xD9662B);

        // 건물/외부
        public static readonly Color Concrete = Hex(0x8C8D88);
        public static readonly Color StairWall = Hex(0xA9A79F);
        public static readonly Color Facade = Hex(0x6F747A);
        public static readonly Color Asphalt = Hex(0x34383C);
        public static readonly Color Sand = Hex(0x8C7E62);
        public static readonly Color Grass = Hex(0x3E5236);
        public static readonly Color SeaDeep = Hex(0x1B2E3A);
        public static readonly Color SignBlue = Hex(0x1F4E8C);
        public static readonly Color CarBody = Hex(0x55606A);
        public static readonly Color SignGreen = Hex(0x1E7A46);     // 지진 옥외대피장소 표지판(초록 바탕 흰 글씨)
        public static readonly Color TsunamiArrow = Hex(0x5FA8FF);  // 지진해일 대피 방향 바닥 화살표
        public static readonly Color Dirt = Hex(0x6E5A44);          // 학교 운동장 흙
        public static readonly Color SchoolWall = Hex(0xB8A98F);

        // 빛/공기
        public static readonly Color DuskKey = Hex(0xC9A58C);      // 구름 낀 해 질 녘 빛
        public static readonly Color AmbientSky = Hex(0x3C4656);
        public static readonly Color AmbientEquator = Hex(0x343A44);
        public static readonly Color AmbientGround = Hex(0x2A2522);
        public static readonly Color Fog = Hex(0x2E3440);
        public static readonly Color LampWarm = Hex(0xFFE2B8);
        public static readonly Color Lightning = Hex(0xD8E4FF);
        public static readonly Color Shadow = Hex(0x1C2230);

        // 강조/효과
        public static readonly Color AccentEmergency = Hex(0xE8402A);
        public static readonly Color ExitGreen = Hex(0x3BD16F);
        public static readonly Color Fire = Hex(0xFF8A33);
        public static readonly Color Smoke = new Color(0.13f, 0.12f, 0.12f, 0.7f);
        public static readonly Color Rain = new Color(0.7f, 0.75f, 0.85f, 0.45f);

        // UI
        public static readonly Color UiText = Hex(0xF2EEE6);
        public static readonly Color UiPanel = new Color(0.07f, 0.08f, 0.1f, 0.88f);
        public static readonly Color UiAlert = Hex(0xFFD36B);
        public static readonly Color UiClear = Hex(0x6FD98A);

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
