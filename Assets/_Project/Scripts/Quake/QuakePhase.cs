namespace QuakeSurvival.Quake
{
    /// <summary>지진 시나리오 진행 단계.</summary>
    public enum QuakePhase
    {
        Calm,        // 평상시
        Foreshock,   // 전진(약한 흔들림)
        Prepare,     // 전진 후 준비 시간(약 60초)
        MainShock,   // 본진(30초)
        Aftermath,   // 본진 직후(본진 판정, 대피)
        Aftershock,  // 여진 + 지진해일 경보
        Tsunami      // 지진해일 도달까지(끝은 TsunamiController가 판정)
    }
}
