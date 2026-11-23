# 승인된 변경 목록 (공통 베이스 통일 작업)

사용자가 승인한 항목만 적용한다. 승인되지 않은 항목은 건드리지 않는다.

## 기준
- **A. 현 `Apartment_1004_Base`를 공통 베이스로 확정** (S03 개발 중 검증된 값 기준). 집은 이미 `GameScene`의 단일 프리팹 인스턴스이며 S01·S03가 같은 집을 공유한다.

## 승인 (적용 대상)
| ID | 내용 |
|---|---|
| B-01 | 현 베이스 확정 + `Docs/BaseHouseSpec.md` (id 변경 금지 규칙 포함) |
| B-02 | Clearance FAIL 5건 수정 (가구 소폭 이동, S01·S03 공통 반영) |
| X-01 | `WarFireExclusion` 문자열 토큰 매칭 → 명시적 태그 + 플래그 |
| X-02(a) | 화재 소품은 베이스에 유지, `useFireProps` 플래그로 토글 (오브젝트·id 불변) |
| X-05 | `Assets/_Recovery/0.unity` 를 Assets 밖으로 이동 |
| F-01~F-06 | `ScenarioDefinition` 기능 플래그. S01 = 호흡기 게이지·도어 스코프·창밖 보행자/차량 ON, 가스·화재 소품·엘리베이터 위험 OFF. S03 = 전부 ON |
| S01-01 | S01 소품·동행자를 `s01_*` 앵커에 고정 |
| S01-02 | S01 정전 연출 |
| S01-03 | S01 단수 연출 |
| S02-01 | 자연재해 시나리오 스켈레톤 (`isPlayable = false`) |

## 보류 / 제외 (적용하지 않음)
| ID | 상태 | 사유 |
|---|---|---|
| X-03 | 보류 | `DeathReason` enum 직렬화 값 변경 위험 (높음) |
| X-04 | 보류 | S01 목표 시간 17분 → 실측 후 조정 |
| S01-04 | 보류 | 비상배낭 소품 (통로 검증 필요) |
| S01-05 | 제외 | 지하 대피 (집 밖 공간 필요) |
| S03-01 | 유지 | S03 전용물 변경 없음 |

## 백업
`Backup_before_base_unify~/` (프로젝트 루트, Assets 밖). 작업 전 `git commit` 을 권장한다.

## 적용 결과 (4단계 완료)

### 검증 (Unity 6000.6.9f1, 배치모드 복사본 → 통과한 파일만 실제 프로젝트에 반영)
| 검증 | 결과 |
|---|---|
| 컴파일 | 에러 0 |
| Run Full Audit | PASS 183 / FAIL 0 |
| S03 런타임 스모크 (Act 1~4, 루트 A~D, 엘리베이터 2종, 재시작 5회, 구독·타이머 누수) | **PASS 85 / FAIL 0** |
| S01 런타임 스모크 | PASS 39 / FAIL 1 — "식량·물·약품·무기·보온 확보" `comfort=27 < 30`. 담요 12 + 겨울옷 ~15 인 수치와 테스트 기준(30) 불일치로 **HEAD 에도 있던 문제** (이번 변경과 무관) |
| Validate Apartment Clearance (베이스) | FAIL 5 → **2** (동쪽 통로 0.56m, `pickup_key` 시선 — 알려진 이슈로 문서화) |
| Missing Reference / 에러 로그 | 0 (스모크 "no error/exception logs" PASS) |

### 변경 전/후
| 대상 | 변경 전 | 변경 후 |
|---|---|---|
| `Apartment_1004_Base.prefab` GameObject / MonoBehaviour | 890 / 205 | 891(+1 stripped 참조) / 225 (`FireHazardProp` +20) |
| 베이스 상호작용 id | 95개 직렬화 `id` | 95개 — **id 변경 0건** |
| `S01_WarTime.unity` GameObject / MonoBehaviour | 62 / 24 | 75 / 36 (`Anchors` 그룹 + 앵커 12개) |
| `S02_Disaster/` | 없음 | 정의(`isPlayable=false`) + 빈 씬 + `Scenario_02` 연결 |
| `WarFireExclusion` (문자열 매칭) | S01 코드 | 삭제 → `FireHazardProp` 태그 + `useFireProps` |
| `Assets/_Recovery/0.unity` | Assets 안 | `_Recovery~/` (프로젝트 루트, Assets 밖) |

### 이동·변경된 베이스 오브젝트 (B-02)
`microwave`(+부속 3) z -0.10 · `coffee_pot` → (4.52, 1.02, 4.34) · `pickup_water_bottle` → (5.02, 1.01, 4.34) · `KitchenCounter` 깊이 0.52m, x +0.03 · `Wall_Shelter_0/_2` 개구부 0.90→0.95m. S01 `WaterAndCans` 는 건물 밖(x=7.05)에서 (5.50, 0.01, 4.30)으로 이동.

### 신규·변경 코드
`ScenarioDefinition`(기능 플래그 6개), `ScenarioFeatures`(신규), `ScenarioRunner`, `HouseServices`, `HUD.SetVitalsAllowed`, `HouseController`(`ApplyScenarioFeatures`, `RefreshBlackout`, 앵커 폴백), `ScenarioAnchor`(시나리오 앵커 자동 등록), `FireHazardProp`(신규), S01(`WarUtilities` 신규, `Act3/Act4/WarActBase/WarConfig/S01Bootstrap/S01SmokeTest`), `S03Bootstrap`(엘리베이터 위험 플래그), 에디터(`MainMenuBuilder.S01` 고정 좌표·앵커·플래그, `ScenarioSkeletonBuilder` 신규).

### 되돌리기
`Backup_before_base_unify~/` (씬·프리팹·스크립트 백업) 또는 `git checkout -- Assets` + 신규 파일 삭제. 작업 전 `git commit` 을 권장한다.
