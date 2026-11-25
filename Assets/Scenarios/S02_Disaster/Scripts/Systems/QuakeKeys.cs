namespace GameGameplay
{
    /// <summary>시나리오 2(자연재해 — 지진·지진해일) 상호작용 id · 플래그 · 엔딩 id. 모든 시나리오 전용 id 는 s02_ 접두어</summary>
    public static class QuakeKeys
    {
        // ── 집(베이스) 상호작용 id (변경 금지 — BaseHouseSpec) ──
        public const string GasValveMain = "gas_valve_main";
        public const string FuseCock = "fuse_cock";
        public const string FrontDoor = "door_front";
        public const string ShoeCabinet = "shoe_cabinet";
        public const string StairDoor = "fire_door_stairs";

        // ── 시나리오 2 전용 상호작용 id ──
        public const string Breaker = "s02_breaker";
        public const string MultitapPlug = "multitap_plug";   // 집의 멀티탭 코드 (뽑으면 전기 조치)
        public const string EmergencyBag = "s02_emergency_bag";
        public const string Neighbor = "s02_injured_neighbor";
        public const string PhoneCallFamily = "s02_call_family";
        public const string PhoneTextFamily = "s02_text_family";

        // ── 조치 (체크리스트) ──
        public const string ActGas = "gas_valve", ActBreaker = "breaker", ActDoor = "door", ActShoes = "shoes", ActBag = "bag";

        // ── 점수 플래그 (ScoreRules FlagTrue 키) ──
        public const string FlagCoverForeshock = "s02_rule_cover_foreshock";
        public const string FlagGas = "s02_rule_gas";
        public const string FlagBreaker = "s02_rule_breaker";
        public const string FlagDoor = "s02_rule_door";
        public const string FlagShoes = "s02_rule_shoes";
        public const string FlagNoElevator = "s02_rule_no_elevator";
        public const string FlagCoverMainShock = "s02_rule_cover_mainshock";
        public const string FlagCoverAftershock = "s02_rule_cover_aftershock";
        public const string FlagOutdoorSite = "s02_rule_outdoor_site";
        public const string FlagShelter = "s02_rule_shelter";
        public const string FlagNoCalls = "s02_rule_no_calls";
        public const string FlagNoPrevented = "s02_rule_no_prevented";
        public const string FlagBag = "s02_bonus_bag";
        public const string FlagNeighbor = "s02_bonus_neighbor";
        public const string FlagFamilyText = "s02_bonus_family_text";

        /// <summary>현재 지진 단계 (ScenarioFlags 숫자, QuakePhase 값)</summary>
        public const string FlagPhase = "s02_phase";

        // ── 엔딩 id ──
        public const string EndingShelter = "s02_shelter";
        public const string EndingVertical = "s02_vertical";
        public const string EndingProtected = "s02_protected";
    }
}
