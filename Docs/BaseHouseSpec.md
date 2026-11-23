# 공통 베이스 집 사양 (Base House Spec)

> 이 문서는 `Apartment_1004_Base` 프리팹(= `GameScene` 의 집 인스턴스)의 **현재 확정 사양**이다. 모든 시나리오가 이 집에서 시작한다.
> 좌표는 월드 기준(m). 자동 생성된 목록이며, 값이 바뀌면 이 문서도 갱신한다. 기준: 공통 베이스 = 시나리오 3 개발 중 검증된 현 상태(기준 A).

## 1. 변경 금지 규칙

- **상호작용 id, 앵커 id, 스폰 id, 조명 그룹 id, 구역(HazardZone) id 는 변경·삭제 금지.** 모든 시나리오와 Act 코드가 id 로 찾는다.
- **시나리오 전용 로직을 베이스 프리팹에 넣지 않는다.** 베이스는 이벤트만 발행하고, 동작은 시나리오가 `InteractionRouter` / `HouseController` API 로 정한다.
- **베이스 오브젝트를 시나리오에서 새로 만들거나 지우지 않는다.** 상태만 바꾼다([V]): 창문 파손, 정전, 연기·가스 농도, 문 잠금·온도, 상호작용 비활성·라벨/동작 교체 (`HouseController`).
- 시나리오 전용 가구·소품·NPC·이펙트는 **시나리오 씬(Additive)** 에 두고, 상호작용 id 는 `snake_case`, 접두어 `s01_` / `s03_` (시나리오 전용 앵커 id 도 같은 접두어).
- 화재용 소품(§5)은 베이스에 두되 `FireHazardProp` 태그를 붙인다. `ScenarioDefinition.useFireProps = false` 인 시나리오에서는 숨겨진다.
- 가구·소품 배치는 통로 기준을 지킨다: 주 동선 **0.9m 이상**, 보조 0.7m, 절대 최소 0.76m. 변경 후 `Tools > GameUI > Validate Apartment Clearance` 로 검증한다.
- 베이스를 수정하면 `Tools > GameUI > Debug > Run Full Audit` 와 S01·S03 스모크 테스트를 돌려 두 시나리오를 모두 확인한다.

## 2. 플레이어·통로 기준

| 항목 | 값 |
|---|---|
| 캡슐 반지름 / 높이 / 스텝 | 0.28 / 1.75 / 0.3 |
| 서기·웅크리기 높이 | 1.75 / 1.05 |
| 걷기 / 달리기 / 웅크리기 속도 | 1.8 / 3.8 / 1.0 m/s |
| 점프 높이 | 0.6 m (중력 -20) |
| 카메라 | FOV 78 (눈높이 = 머리 아래 0.15, 보정 -0.06) |
| 상호작용 거리 | 2.0 m |
| 통로 기준 | 주 동선 0.9 / 보조 0.7 / 절대 최소 0.76 m |

## 3. 스폰 포인트 · 앵커

| id | 위치 (x, y, z) | 방향(Y) |
|---|---|---|
| `spawn_bedroom_bed` | 1.7,0.02,4 | 0 |
| `spawn_living_room` | 3.2,0.02,3.4 | 0 |
| `spawn_entrance` | 3,0.02,1.2 | 0 |
| `spawn_corridor` | 0.5,0.02,-1.1 | 270 |
| `spawn_balcony` | 2.7,0.02,8.2 | 0 |
| `spawn_shelter` | 6,0.02,8.2 | 270 |

| 앵커 id | 위치 (x, y, z) |
|---|---|
| `anchor_living_center` | 4,1,5 |
| `anchor_kitchen_stove` | 6.3,1,3 |
| `anchor_balcony_window` | 2.7,1.5,8.9 |
| `anchor_living_window` | 3.2,1.4,7.3 |
| `anchor_bedroom_window` | 1.3,1.4,7.3 |
| `anchor_entrance_door` | 3,1,0.15 |
| `anchor_corridor_elevator` | -1.5,1,-2 |
| `anchor_stairwell_door` | -14,1,-1.1 |
| `anchor_shelter_door` | 5.4,1,8.2 |
| `anchor_utility_valve` | 6.4,1.2,1.2 |

시나리오 전용 앵커(예: S01 `s01_*`)는 시나리오 씬에 있고, 켜질 때 스스로 등록되어 `HouseController.FindAnchor` 로 찾을 수 있다.

## 4. 방별 기본 가구 (인테리어 프리팹 인스턴스)

### 침실 (`Interior/Bedroom`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| Bed | 0.73,0,4.95 |
| Nightstand | 1.53,0,5.75 |
| TableLamp | 1.53,0.52,5.86 |
| PhoneCharger | 1.45,0.52,5.62 |
| Wardrobe | 0.36,0,3.3 |
| Desk | 0.4,0,6.41 |
| Monitor | 0.25,0.74,6.52 |
| Keyboard | 0.42,0.74,6.55 |
| BookStack | 0.3,0.74,6.12 |
| Cup | 0.58,0.74,6.7 |
| PostIt_Monitor | 0.27,0.98,6.7 |
| DeskChair | 0.62,0,6.41 |
| Clothes_Chair | 0.86,0.82,6.41 |
| Rug_Bedroom | 1.75,0,3.55 |
| Panel_L | 0.42,0.12,7.2 |
| Panel_R | 2.2,0.12,7.2 |
| Frame_Bed | 0.14,1.55,4.95 |
| Frame_Portrait | 0.14,1.5,6.4 |
| Calendar_Bedroom | 2.51,1.5,4.55 |
| PostIt_Wall | 2.51,1.62,4.78 |
| Clock_Bedroom | 2.51,1.95,6 |

### 거실 (`Interior/Living`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| TVStand | 2.84,0,5.65 |
| TV | 2.84,0.5,5.6 |
| Clock_Living | 2.69,1.95,5.6 |
| Sofa | 6.03,0,5.98 |
| Cushion_1 | 6.14,0.42,5.28 |
| Cushion_2 | 6.14,0.42,6.68 |
| CoffeeTable | 4.85,0,5.98 |
| Remote | 4.75,0.42,5.6 |
| Cup | 4.95,0.42,5.62 |
| Newspaper | 4.85,0.42,5.9 |
| Rug_Living | 4.4,0,5.7 |
| Bookshelf | 6.03,0,7.13 |
| FloorLamp | 5.38,0,6.95 |
| Plant | 2.93,0,6.42 |
| Frame_Living | 6.46,1.55,5.98 |
| Panel_L | 2.97,0.12,7.2 |
| Panel_R | 4.9,0.12,7.2 |

### 주방 (`Interior/Kitchen`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| KitchenCounter | 6.21,0,3.55 (깊이 0.52 — B-02) |
| SinkUnit | 4.76,0,4.28 |
| Cup | 4.9,0.9,4.2 |
| Fridge | 4.3,0,2.78 |
| UpperCabinet | 4.45,1.85,2.65 |
| Calendar_Kitchen | 4.15,1.45,3.11 |
| PostIt_Fridge | 4.45,1.55,3.1 |

### 다용도실 (`Interior/Utility`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| WashingMachine | 4.6,0,0.5 |
| UtilityShelf | 6.3,0,0.45 |
| DryingRack_Folded | 5.5,0,0.2 |
| TrashBin | 4.44,0,0.94 |
| Boiler | 4.33,1.25,1.65 |

### 화장실 (`Interior/Bathroom`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| Toilet | 0.35,0,0.45 |
| Vanity | 1.3,0,2.13 |
| Bathtub | 0.45,0,1.65 |
| Mirror_Bath | 1.3,1,2.31 |
| ToothbrushCup | 1.52,0.8,2 |
| Soap | 1.5,0.8,2.2 |

### 현관 (`Interior/Entry`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| ShoeCabinet | 3.88,0,1.1 |
| UmbrellaStand | 2.1,0,0.3 |
| Shoes_1 | 2.15,0,0.3 |
| Shoes_2 | 2.4,0,0.36 |
| Shoes_3 | 2.2,0,1.2 |
| Shoes_4 | 3.5,0,0.55 |
| Mirror_Entry | 2,0,2.05 |
| SensorLight_Entry | 1.99,2.25,1.2 |

### 베란다(통유리) (`Interior/Balcony`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| Planter | 1,0,8.75 |
| OutdoorUnit | 1.8,0,8.73 |
| CrateStack | 2.5,0,8.68 |
| Laundry_0 | 0.8,2,8.3 |
| Laundry_1 | 1.3,2,8.3 |
| Laundry_2 | 1.85,2,8.3 |
| Laundry_3 | 2.5,2,8.3 |

### 대피공간 (`Interior/Shelter`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| ShelterShelf | 6.09,0,8.75 |

### 10층 복도(엘리베이터 홀) (`Interior/Corridor`)

| 가구/소품 | 위치 (x, y, z) |
|---|---|
| HydrantBox | -7,0.55,-0.22 |
| ElevatorCallPanel | -0.4,1,-2.06 |

> 그 외 `Interior/` 아래: `Floors`, `Baseboards`, `Frames`(문틀·창틀), `BalconyGlassFrame`, `Lights`, `ReflectionProbes`, `Safety`(창밖 투명 콜라이더). 엘리베이터 캐빈 내부(`ElevatorCabinInterior`)와 비상계단 입구는 `Level` 아래에 있다.

## 5. 상호작용 오브젝트 (id 목록)

| id | 종류 | 위치 | 비고 |
|---|---|---|---|
| `light_partition` | Trigger | 0,1,8.2 |  |
| `window_kitchen` | Toggle | 6.6,0,3.5 |  |
| `window_kitchen_lock` | Lock | 6.45,1.55,4.08 |  |
| `window_shelter` | Toggle | 6.6,0,8.2 |  |
| `window_bedroom` | Toggle | 1.3,0,7.4 |  |
| `window_bedroom_lock` | Lock | 1.88,1.5,7.25 |  |
| `window_living` | Toggle | 3.2,0,7.4 |  |
| `window_living_lock` | Lock | 3.58,1.5,7.25 |  |
| `door_balcony` | Toggle | 4.5,0,7.4 |  |
| `fire_door_shelter` | Toggle | 5.4,0,7.75 |  |
| `door_bedroom` | Toggle | 2.6,0,2.88 |  |
| `door_bathroom` | Toggle | 1.9,0,0.8 |  |
| `door_utility` | Toggle | 4.95,0,2.4 |  |
| `door_1002` | Trigger | -10.2,0,-0.16 |  |
| `door_1003` | Trigger | -3.6,0,-0.16 |  |
| `door_front` | Toggle | 2.53,0,0 |  |
| `door_handle_check` | Trigger | 3.36,1,0.09 | **화재 소품 (`FireHazardProp`)** |
| `fire_door_stairs` | Toggle | -14,0,-1.6 |  |
| `bed_bedroom` | Trigger | 0.73,0.65,4.7 |  |
| `closet_bedroom` | Trigger | 0.35,1,3.3 |  |
| `pickup_key` | Pickup | 0.5,0.8,6.2 |  |
| `alarm_clock` | Trigger | 1.53,0.58,5.75 |  |
| `switch_light_bedroom` | Toggle | 2.5,1.2,3.95 |  |
| `tv_living` | Trigger | 2.78,0.95,5.6 |  |
| `pickup_duct_tape` | Pickup | 4.85,0.45,6.15 | **화재 소품 (`FireHazardProp`)** |
| `multitap_switch` | Trigger | 3.12,0.07,4.65 | **화재 소품 (`FireHazardProp`)** |
| `multitap_plug` | Removable | 2.8,0.04,4.65 | **화재 소품 (`FireHazardProp`)** |
| `clutter_paper_1` | Removable | 2.9,0.03,4.4 | **화재 소품 (`FireHazardProp`)** |
| `clutter_cloth_1` | Removable | 3.15,0.05,4.42 | **화재 소품 (`FireHazardProp`)** |
| `clutter_paper_2` | Removable | 3.35,0.03,4.7 | **화재 소품 (`FireHazardProp`)** |
| `switch_light_living` | Toggle | 2.7,1.2,4.85 |  |
| `gas_range` | Trigger | 6.3,0.95,3 | **화재 소품 (`FireHazardProp`)** |
| `frying_pan` | Trigger | 6.3,1.03,3 | **화재 소품 (`FireHazardProp`)** |
| `range_hood` | Toggle | 6.35,1.85,3 | **화재 소품 (`FireHazardProp`)** |
| `pickup_water_bottle` | Pickup | 5.02,1.01,4.34 |  |
| `pickup_water_pot` | Pickup | 4.7,0.1,3.8 | **화재 소품 (`FireHazardProp`)** |
| `coffee_pot` | Trigger | 4.52,1.02,4.34 |  |
| `microwave` | Trigger | 6.3,1.04,3.45 |  |
| `drink_water` | Trigger | 4.15,1.1,4.4 |  |
| `fuse_cock` | Valve | 4.1,0.6,4.5 | **화재 소품 (`FireHazardProp`)** |
| `gas_valve_main` | Valve | 6.46,1.2,1.2 | **화재 소품 (`FireHazardProp`)** |
| `wash_face` | Trigger | 1.3,0.98,2.28 |  |
| `pickup_towel` | Pickup | 0.2,0.98,1.25 | **화재 소품 (`FireHazardProp`)** |
| `pickup_towel_2` | Pickup | 0.2,0.98,1.6 | **화재 소품 (`FireHazardProp`)** |
| `sink_wet_towel` | Convert | 1.1,0.85,2.12 | **화재 소품 (`FireHazardProp`)** |
| `shoe_cabinet` | Trigger | 3.88,0.55,1.1 |  |
| `pickup_extinguisher` | Pickup | 3.92,0.32,1.9 | **화재 소품 (`FireHazardProp`)** |
| `flower_pot` | Removable | 0.45,0.2,8.72 |  |
| `window_signal` | Trigger | 2.7,1.5,8.7 |  |
| `elevator_*` | Elevator* | 복도·캐빈 | 엘리베이터 (문·호출 버튼·캐빈·층 버튼) |

`FireHazardProp` 태그 20개: 위 표의 화재 소품 17개 + `ExtinguisherSign`, `HydrantBox`, `FireAlarmButton`(소화전·경보 소품, 이름 기준으로 태그).

## 6. 창문 · 문 · 개폐 방식

| id | 종류 | 개폐 |
|---|---|---|
| `window_bedroom` | 창(침실) | 잠금(`window_bedroom_lock`) 해제 후 열기 |
| `window_living` | 창(거실) | 잠금(`window_living_lock`) 해제 후 열기 |
| `window_kitchen` | 창(주방) | 잠금(`window_kitchen_lock`) 해제 후 열기 |
| `window_shelter` | 창(대피공간) | 열기/닫기 |
| `door_front` | 현관문 | 열기/닫기 (손잡이 온도 확인 `door_handle_check`) |
| `door_bedroom` | 방문(침실) | 열기/닫기 |
| `door_bathroom` | 방문(화장실) | 열기/닫기 |
| `door_utility` | 방문(다용도실) | 열기/닫기 |
| `door_balcony` | 베란다 문 | 열기/닫기 |
| `fire_door_shelter` | 방화문(대피공간) | 열기/닫기 — 벽 개구부 0.95m (B-02) |
| `fire_door_stairs` | 방화문(비상계단) | 열기/닫기 |
| `door_1002, door_1003` | 이웃집 문 | 트리거 (열리지 않음) |

통유리: `Structure/Wall_BalconyNorth/balcony_glass` (5.2 x 2.4 m, 위치 2.7, 1.2, 8.9). 창 바깥에는 점프 대비 투명 콜라이더(`Safety/WindowBarrier_*`)가 있다. 창 파손은 `HouseController.SetWindowState(id, WindowState.Broken)` 로만 한다. 위치는 §5 표 참고.

## 7. 조명 그룹 · 구역

- 조명 그룹 id: `bedroom`, `living`, `kitchen`, `utility`, `bathroom`, `entry`, `balcony`, `shelter`, `corridor`, `stairwell`, `exit`(비상구 유도등), `daylight`. `SetBlackout` 은 모두 끄고, `SetEmergencyLights` 는 `exit` 만 켠다.
- HazardZone id: `zone_bedroom`, `zone_living`, `zone_kitchen`, `zone_utility`, `zone_bathroom`, `zone_entry`, `zone_balcony`, `zone_shelter`, `zone_corridor`, `zone_stairwell`, `zone_elevator_cabin`. 연기·가스·온도·수위는 `HouseController.SetZone*` 로 올린다.

## 8. 창밖 풍경 (`OutdoorWorld`)

근경 건물군·타워·저층 건물·산·강·구름·보행자 스포너·차량·새·`FireEffect_8F`·`OutdoorWorldController`·`WindowViewZone` 4개(침실·거실·주방·베란다). 시나리오는 `EnvironmentPreset` 으로 하늘·태양·안개·보행자/차량 밀도를 바꾼다. 보행자·차량은 `useOutdoorTraffic` 플래그로 끌 수 있다.

## 9. 시나리오 기능 플래그 (`ScenarioDefinition`)

| 플래그 | 하는 일 | S01 | S03 | S02(뼈대) |
|---|---|---|---|---|
| `useRespiratoryGauge` | 호흡기 게이지 HUD | ON | ON | OFF |
| `useGasHazard` | 가스 누출 시스템 | OFF | ON | OFF |
| `useFireProps` | 화재 소품(`FireHazardProp`) + 화재 확산 | OFF | ON | OFF |
| `useElevatorHazard` | 엘리베이터 위험(2종 랜덤 사망) | OFF | ON | OFF |
| `useDoorPeephole` | 도어 스코프 | ON | ON | ON |
| `useOutdoorTraffic` | 창밖 보행자·차량 | ON | ON | ON |

## 10. 알려진 이슈 (Validate Apartment Clearance: FAIL 2건)

B-02 적용 후에도 S03 개발 때부터 있던 2건이 남아 있다. 플레이에는 지장이 없다 (사용자 결정: 문서화).

1. **침실 → 동쪽 창 통로 0.56m** (기준 0.76m): 가구가 아니라 긴 동쪽 벽 `Wall_BedroomEast_2` 때문이다. 벽을 0.1m 옮겨도 0.66m 까지만 개선되고 거실 스위치 시선(`switch_light_living`)이 가려진다. 해결하려면 벽 구조 변경이 필요하다 (별도 승인 항목).
2. **`pickup_key` 시선 가림** (`DeskChair`): 열쇠·의자를 옮겨도 해결되지 않았다.

B-02 로 해결한 것: 주방 창 앞 침범(전자레인지 z -0.10, `coffee_pot`·`pickup_water_bottle` 을 싱크대 위로, 조리대 깊이 0.52m), 주방 창 시선, 대피공간 방화문 병목(개구부 0.90→0.95m).
