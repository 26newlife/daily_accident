# 시나리오 1 (전시상황) — 추가물 목록

공통 베이스(`Docs/BaseHouseSpec.md`) 위에 **Additive 씬 `S01_WarTime`** 으로만 얹는다. 집 오브젝트는 만들거나 지우지 않고 상태만 바꾼다.
씬: `Assets/Scenarios/S01_WarTime/Scenes/S01_WarTime.unity` · 정의: `Data/S01_Definition.asset` · 시작 스폰 `spawn_living_room`.

## 기능 플래그
호흡기 게이지 ON · 도어 스코프 ON · 창밖 보행자/차량 ON · **가스 OFF · 화재 소품 OFF · 엘리베이터 위험 OFF** (`ScenarioFeatures` 가 시작 시 적용; 화재 소품 20개는 `FireHazardProp` 태그로 숨겨진다).

## 소품 · 동행자 (고정 위치 + 앵커, S01-01)
위치는 빌드 때 자동 탐색하지 않고 고정한다 (`MainMenuBuilder.S01.cs` 의 `S01FixedSpots`). 각 위치에 앵커 `s01_*` 가 있다 (`S01_Content/Anchors`).

| 앵커 id | 오브젝트 | 상호작용 id | 위치 (x, y, z) |
|---|---|---|---|
| `s01_battery_radio` | BatteryRadio | `war_radio` | 4.00, 0.01, 5.00 (거실) |
| `s01_radio_shelter_spot` | RadioShelterSpot | — (라디오 이동 지점) | 6.00, 0.01, 7.60 (대피공간) |
| `s01_water_and_cans` | WaterAndCans | `war_pickup_supplies` | **5.50, 0.01, 4.30 (주방 북동쪽 구석)** |
| `s01_pantry_box` | PantryBox | `war_pickup_pantry` | 5.50, 0.01, 2.20 |
| `s01_med_kit` | MedKit | `war_pickup_medkit` | 1.30, 0.58, 5.80 (침실 협탁) |
| `s01_civil_defense_box` | CivilDefenseBox | `war_civil_defense_box` | 6.22, 0.01, 1.38 (다용도실) |
| `s01_blanket` | Blanket | `war_pickup_blanket` | 1.60, 0.01, 6.80 |
| `s01_winter_clothes` | WinterClothes | `war_pickup_clothes` | 5.38, 0.01, 5.68 |
| `s01_steel_pipe` | SteelPipe | `war_pickup_pipe` | 3.40, 0.01, 2.00 (현관) |
| `s01_soldier` / `s01_child` / `s01_neighbor` | 동행자 | `war_survivor_*` | 3.40,5.40 / 4.30,5.80 / 4.90,4.80 (합류 전 숨김) |

> `WaterAndCans` 는 예전 자동 탐색 결과가 x=7.05(건물 밖)였다. 소품은 트리거 콜라이더라 통로를 막지 않지만, 위치를 바꿀 때는 건물 안쪽이고 접근 가능한지 확인한다 (`Validate Apartment Clearance` 는 트리거를 검사하지 않는다).

## 전쟁 연출 (기존, 변경 없음)
`WarOutbreak`(하늘·연기 기둥·포격 섬광·사이렌) · `WarFlyovers`/`WarAircraft`(전투기·헬기, `FlightRoutes`) · `WarAudio` · `WarHud` · `WarState`.

## 기반시설 연출 (S01-02 정전, S01-03 단수) — `WarUtilities`
| 연출 | 시점 | 동작 | 타이밍 값 (`WarConfig`) |
|---|---|---|---|
| 단수 | Act 3 시작 후 30초 | `wash_face`, `drink_water` → "수도꼭지 (단수)", 물이 나오지 않는다는 메시지 | `waterCutTime` |
| 정전 | Act 3 시작 후 60초 | `HouseController.SetBlackout(true)` + 비상구 유도등(`exit`)만 켬. 환경 프리셋이 바뀌어도 어둡게 유지(`RefreshBlackout`) | `blackoutTime` |
| 전기 복구 | Act 4 시작 후 2초 | `SetBlackout(false)` — 거짓 평화. 단수는 유지 | `powerRestoreDelayAct4` |

시간 값은 S01 의 기존 방식대로 `WarConfig`(S01 전용 타이밍 에셋)에 둔다. 상태는 `WarUtilities.Reset()` 과 `RunCleanup` 으로 재시작 때 초기화된다.

## 보류·제외
- S01-04 비상배낭(`s01_emergency_bag`, 현관 신발장 옆): **보류** (통로 0.9m 검증 필요).
- S01-05 지하 대피: **제외** (집 밖 공간 필요).

## 알려진 한계
- S01 스모크 테스트의 "식량·물·약품·무기·보온 확보" 항목이 `comfort=27`(기준 30)로 FAIL 한다. 담요 12 + 겨울옷 약 15 = 27 이라 `WarConfig` 수치와 테스트 기준이 어긋난 것이며, 이번 통일 작업 전부터 있던 문제다 (수치를 맞추거나 테스트 기준을 낮추면 된다).
- 목표 플레이 시간 `targetPlayMinutes = 17` (프로젝트 방침은 약 7분) — 실측 후 조정할 보류 항목(X-04).
