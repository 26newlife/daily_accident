# 시나리오 3 (아파트 화재) — 추가물 목록

공통 베이스(`Docs/BaseHouseSpec.md`) 위에 **Additive 씬 `S03_ApartmentFire`** 로 얹는다. **이번 통일 작업에서 S03 전용물은 변경하지 않았다 (S03-01: 현행 유지).**
씬: `Assets/Scenarios/S03_ApartmentFire/Scenes/S03_ApartmentFire.unity` · 정의: `Data/S03_Definition.asset` · 시작 스폰 `spawn_bedroom_bed`.

## 기능 플래그
전부 ON (호흡기 게이지, 가스, 화재 소품, 엘리베이터 위험, 도어 스코프, 창밖 교통). 플래그 기본값이 켜짐이라 기존 동작 그대로다.

## 씬 구성 (`S03_Content`)
| 그룹 | 내용 |
|---|---|
| `ScenarioSystems` | `Acts`(Act1~4), `GasLeakController`, `FireManager`, `ElevatorHazardSystem`, `ElevatorScenarioSelector`, `Call119Controller`, `ShelterSealSystem`, 연출 오디오 |
| `MovedFromHouse` | 화재 `FireSource`×6 (`fire_multitap`, `fire_pan`, `fire_elevator`, `fire_sofa`, `fire_corridor`, `fire_curtain`), 스파크·유리 파편 이펙트, 알람·누출 오디오, 엘리베이터 홀 이펙트 |
| `Structure` / `Interactables` | 14·17·20층 계단참, 옥상 문, 열쇠함, 계단 상호작용, 1003호 내부(`door_1003_inside`, `Floor_Unit1003`), 대피공간 실링 4면(`seal_door_*`), `pickup_hammer` |
| `HazardZones` | `zone_landing14/17/20`, `zone_roof`, `zone_descent_*`, `zone_assembly`, `zone_1003` |
| `EvacWorld` | `EvacSpawns`, `Npcs`(할머니·청년), 층별 계단참, 옥상, 하강 구간, 집결지, `PartitionController`, `RescueLadder` |

## 베이스 위에 입히는 상태 (`S03Bootstrap`)
계단실·대피공간·엘리베이터 캐빈 구역을 격리 구역으로 설정하고 사망 사유를 지정한다 (`StairwellSmoke`, `ShelterSmoke`, `ElevatorStallSmoke`). 엘리베이터 위험은 `useElevatorHazard` 가 꺼지면 `ElevatorHazardSystem` 을 멈춘다.

## 베이스 쪽 화재 소품
가스 밸브·멀티탭·소화기·수건·프라이팬·창 가연물 등 17개와 소화전 표지·경보 버튼 3개는 **베이스 프리팹에 있으며 `FireHazardProp` 태그가 붙어 있다** (`Docs/BaseHouseSpec.md` §5). S03 은 `useFireProps = true` 라서 그대로 쓴다.

## 창밖 연출
평화로운 단지 → 폭발 후 연기·불 (`OutdoorFireEffect_8F`, `Env_SmokySky` 프리셋) — 현행 유지.
