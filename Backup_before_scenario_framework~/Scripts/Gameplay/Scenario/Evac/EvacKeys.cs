namespace GameGameplay
{
    /// <summary>
    /// Act 3~4(복도·탈출)에서 쓰는 상호작용 id / ScenarioFlags 키 / ScoreSystem.Records 키 모음.
    /// 문자열이 여러 파일에 흩어지지 않게 한 곳에 둔다 (빌더·Act·디버그·감사 도구가 모두 이 값을 참조).
    /// 기존 id(door_front, fire_door_stairs, fire_door_shelter, light_partition, flower_pot ...)는 바꾸지 않는다.
    /// </summary>
    public static class EvacKeys
    {
        // ───────── 상호작용 id (snake_case) ─────────
        public const string NpcGrandmother = "npc_grandmother";
        public const string NpcYoungMan = "npc_youngman";
        public const string StairFireDoor = "fire_door_stairs";
        public const string ShelterFireDoor = "fire_door_shelter";
        public const string FrontDoor = "door_front";
        public const string StairsUp = "stairs_up";
        public const string StairsDown = "stairs_down";
        public const string FireDoor14 = "fire_door_floor14";
        public const string FireDoor17 = "fire_door_floor17";
        public const string StairsUp14 = "stairs_up_floor14";
        public const string StairsUp17 = "stairs_up_floor17";
        public const string KeyBox = "key_box_glass";
        public const string RoofDoor = "roof_door";
        public const string RoofSignal = "roof_signal_point";
        public const string PickupHammer = "pickup_hammer";
        public const string LightPartition = "light_partition";
        public const string FlowerPot = "flower_pot";
        public const string FlowerPot2 = "balcony_pot_2";
        public const string StorageBox = "balcony_storage_box";
        public const string SealBottom = "seal_door_bottom";
        public const string SealLeft = "seal_door_left";
        public const string SealRight = "seal_door_right";
        public const string SealTop = "seal_door_top";
        public const string ShelterWindow = "window_shelter";
        public const string Door1003Inside = "door_1003_inside";
        public const string DescentLobbyDoor = "lobby_exit_door";
        public const string PhoneCall119Action = "call_119";

        // ───────── 아이템 id ─────────
        public const string ItemWetTowel = "wet_towel";
        public const string ItemTowel = "towel";
        public const string ItemTape = "duct_tape";
        public const string ItemHammer = "hammer";
        public const string ItemRoofKey = "roof_key";

        // ───────── 구역 id ─────────
        public const string ZoneCorridor = "zone_corridor";
        public const string ZoneStairwell = "zone_stairwell";
        public const string ZoneShelter = "zone_shelter";
        public const string ZoneLiving = "zone_living";
        public const string Zone1003 = "zone_1003";
        public const string ZoneLanding14 = "zone_landing14";
        public const string ZoneLanding17 = "zone_landing17";
        public const string ZoneLanding20 = "zone_landing20";
        public const string ZoneDescent1 = "zone_descent_1";
        public const string ZoneDescent2 = "zone_descent_2";
        public const string ZoneDescent3 = "zone_descent_3";
        public const string ZoneAssembly = "zone_assembly";

        // ───────── 엔딩 id ─────────
        public const string EndingRooftop = "ending_rooftop";
        public const string EndingPartition = "ending_partition";
        public const string EndingShelter = "ending_shelter";
        public const string EndingDescent = "ending_descent";
        public const string EndingHidden = "ending_hidden_grandmother";

        // ───────── ScenarioFlags 키 ─────────
        public const string FlagHandleHot = "act3_front_door_blocked";          // Act 2 가 기록 (뜨거움)
        public const string FlagFrontDoorClosed = "act3_front_door_closed";     // 복도로 나가 현관문을 닫았다
        public const string FlagInCorridor = "act3_reached_corridor";
        public const string FlagStairDownBlocked = "stairwell_down_blocked";    // 방화문을 열었을 때 아래층 연기로 하강 불가
        public const string FlagEarlyFireDoor = "early_fire_door_success";      // 연기가 올라오기 전에 계단에 도달
        public const string FlagFireDoorManaged = "fire_door_managed";          // 방화문/현관문을 닫아 연기 확산을 막았다
        public const string FlagGrandmotherWith = "evac_grandmother_with_player";   // 할머니가 동행 중/구출 완료 (히든 엔딩 조건)
        public const string FlagGrandmotherSaved = "act3_grandmother_rescued";
        public const string FlagGrandmotherDead = "act3_grandmother_dead";
        public const string FlagYoungManSaved = "act3_youngman_saved";
        public const string FlagYoungManDead = "act3_youngman_dead";
        public const string FlagNeighborRescued = "act3_neighbor_rescued";      // 한 명이라도 구했다
        public const string FlagRoute = "act4_route";                           // 텍스트 A/B/C/D
        public const string FlagRouteSafe = "act4_route_safe";                  // RouteResolver 가 가능하다고 본 루트를 골랐다
        public const string FlagRoofKey = "act4_roof_key_taken";
        public const string FlagPartitionOpen = "act4_partition_open";
        public const string FlagShelterRescue = "act4_shelter_rescued";

        // ───────── ScoreSystem.Records 키 ─────────
        public const string RecExplosionTime = "act2_explosion_time";
        public const string RecCall119 = "call119_result";                      // 2 정확 / 1 오답 / 없음
        public const string RecCall119Time = "call119_time";
        public const string RecStairSmokeAtOpen = "act3_stair_smoke_at_open";
        public const string RecSealRatio = "act4_seal_ratio";
        public const string RecRouteTime = "act4_route_commit_time";
        public const string RecEndingTime = "run_ending_time";
    }
}
