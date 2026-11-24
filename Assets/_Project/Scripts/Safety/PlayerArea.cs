namespace QuakeSurvival.Safety
{
    /// <summary>플레이어가 지금 있는 구역. (씬에 숫자로 저장되므로 새 값은 항상 맨 뒤에 추가한다)</summary>
    public enum PlayerArea
    {
        Unknown,
        Apartment,   // 10층 집 안
        Hallway,     // 10층 복도
        Stairwell,   // 계단실
        Outside,     // 건물 밖
        Shelter,     // 고지대 지진해일 대피소
        OutdoorSite  // 지진 옥외대피장소(초등학교 운동장)
    }

    public static class PlayerAreaExtensions
    {
        /// <summary>지붕 없는 바깥(비가 맞는 곳)인지.</summary>
        public static bool IsOutdoors(this PlayerArea area)
        {
            return area == PlayerArea.Outside || area == PlayerArea.Shelter || area == PlayerArea.OutdoorSite;
        }

        /// <summary>
        /// 대피지역(옥외대피장소, 지진해일 대피소)인지. 이 안에서는 어떤 사고로도 죽지 않고, 대신 결과에서 감점된다.
        /// </summary>
        public static bool IsEvacuationSite(this PlayerArea area)
        {
            return area == PlayerArea.Shelter || area == PlayerArea.OutdoorSite;
        }
    }
}
