namespace GameGameplay
{
    /// <summary>시나리오 1 의 상호작용 id / 플래그 / 기록 키 (한 곳에서 관리)</summary>
    public static class WarKeys
    {
        // 상호작용 id
        public const string Radio = "war_radio";
        public const string PickupSupplies = "war_pickup_supplies";
        public const string PickupPantry = "war_pickup_pantry";
        public const string PickupMedkit = "war_pickup_medkit";
        public const string PickupPipe = "war_pickup_pipe";
        public const string PickupClothes = "war_pickup_clothes";
        public const string PickupBlanket = "war_pickup_blanket";
        public const string CivilDefenseBox = "war_civil_defense_box";
        public const string SurvivorSoldier = "war_survivor_soldier";
        public const string SurvivorChild = "war_survivor_child";
        public const string SurvivorNeighbor = "war_survivor_neighbor";
        public static readonly string[] Windows = { "window_living", "window_kitchen", "window_bedroom", "window_shelter" };
        public const string FrontDoor = "door_front";

        // 아이템 id
        public const string ItemRadio = "item_radio";
        public const string ItemGasMask = "item_gasmask";
        public const string ItemIodine = "item_iodine";

        // ScenarioFlags
        public const string FlagHasRadio = "war_has_radio";
        public const string FlagPeaceBroadcast = "war_peace_broadcast";
        public const string FlagRefusedLiar = "war_peace_refused";
        public const string FlagPersuaded = "war_persuaded";
        public const string FlagWentOutside = "war_went_outside";
        public const string FlagSurvivedFlash = "war_survived_flash";
        public const string FlagAllSaved = "war_all_saved";
        public const string FlagIodineBefore = "war_iodine_before_fallout";
        public const string FlagMaskOn = "war_mask_worn";
        public const string FlagCluesEnough = "war_clues_enough";

        // ScoreSystem 기록
        public const string RecClues = "war_clues";
        public const string RecRefused = "war_refused";
        public const string RecOutside = "war_outside";
        public const string RecRadiation = "war_radiation";
        public const string RecJoined = "war_joined";
        public const string RecLost = "war_lost";
        public const string RecFood = "war_food";
        public const string RecComfort = "war_comfort";

        // 엔딩 id
        public const string EndingTrue = "ending_war_true";
        public const string EndingBitter = "ending_war_bitter";
        public const string EndingBad = "ending_war_bad";

        // 단서 id
        public const string ClueDefense = "clue_defense_collapse";
        public const string ClueWeapon = "clue_special_weapon";
        public const string ClueWithdrawal = "clue_military_withdrawal";
    }
}
