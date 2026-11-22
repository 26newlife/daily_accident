# 시나리오 1 — 전시 상황: 기만된 평화와 낙진 (S01_WarTime)

집(1004호)은 그대로 두고, 시나리오 3(아파트 화재)과 같은 프레임워크 위에 **Additive 씬 `S01_WarTime`** 으로 만든 시나리오입니다.

## 만드는 법 / 테스트
1. Unity: `Tools > GameUI > Build Apartment Scene` — 집 + 시나리오 3 + 시나리오 1 을 함께 만듭니다. (시나리오 1만: `Tools > GameUI > Scenarios > Build S01 War Time`)
2. 메인 메뉴 `SCENARIO SELECT` → **전시상황** → START.
3. 자동 점검: `Tools > GameUI > Debug > Run S01 Smoke Test` (격리 검사, 전쟁 발발 연출, 6개 Act 진행, 사망·엔딩 분기, 재시작 누수. 결과 `smoke_results_s01.txt`)

## 시나리오 3(화재)과의 격리
- 가스 누출·화재 확산·연기·FireSource·소화기·Act 로직은 **시나리오 3 씬(`S03_ApartmentFire`)에만** 있고, 시나리오 1 실행 시 로드되지 않습니다.
- 집(공유 에셋)에는 가스 밸브·멀티탭·소화기·수건·프라이팬 같은 화재용 상호작용 오브젝트가 들어 있습니다. 집을 고치지 않고, 시나리오 1 이 시작될 때 `WarFireExclusion` 이 그 오브젝트(약 21개)와 소화기·소화전·화재경보 소품을 **비활성화**합니다. 시나리오 3 실행 시에는 이 코드가 돌지 않습니다.
- 시나리오 1 은 시나리오 3 의 데이터 에셋·프리팹을 읽거나 수정하지 않습니다 (`Scenario_01.asset` 과 자기 폴더만 씁니다).
- 공용 `DeathReason` enum 에 시나리오 1 전용 값 5개가 있고, 감사 도구는 이 값을 시나리오 3 검사에서 제외합니다.

## 기능 (요새화·물물교환 제거 후)
삭제됨: 창문 합판/방어벽, 거점 방어도 HUD, 빗물 수집 장치, 간이 화장실, 현관 바리케이드, 물물교환 UI·현금 가치·물가 로직.
- Act 2 는 **집 안 수색**(행동력 12)으로 바뀌었습니다: 주방 수납장(식량·물), 안방 약상자(요오드화 칼륨), 민방위 장비함(방독면·약품), 쇠파이프(약탈자 방어), 담요·겨울옷(보온), 라디오(단파 단서), 거실 창을 열어 군 철수 관찰(단서).
- Act 5 충격파: 섬광 때 **밖(복도)에 있으면 즉사(NuclearFlash)**. 충격파까지 8초 동안 **창에서 떨어지거나 웅크리지 않으면 즉사(ShockwaveDebris)**. 이후 모든 창이 깨지고, 낙진 피폭은 방독면(×0.4)·요오드(×0.7, 낙진 전 복용)·창에서 먼 안쪽 방(×0.6)에 따라 줄고, 체온은 담요·옷(쾌적도)과 안쪽 방이 지켜 줍니다.

## 전쟁 발발 연출 (Act 1)
재난문자 경보 직후 `WarOutbreak.Begin()`:
- 하늘(Skybox/Procedural 재질 복사본)·태양·환경광·안개가 평화로운 낮 → 잿빛/붉은빛 전시 색감으로 `skyTransitionSeconds` 동안 전환 (`WarConfig.warSky`).
- 먼 도시 외곽에 검은 연기 기둥 5개(파티클), 간헐적 포격 섬광(번쩍임 → 시차 두고 폭음, 가끔 집이 흔들림).
- 공습 사이렌·원거리 포격 엠비언트 (절차 음; 실제 음원은 `WarAudio.overrides`).
- **전투기 편대(2기, V 자)와 헬기 편대(3기)** 가 `FlightRoutes/*` 웨이포인트를 따라 단지 상공을 주기적으로 통과합니다. 기체는 `Prefabs/S01_Fighter`(동체·주익·꼬리날개·캐노피·애프터버너 파티클)와 `Prefabs/S01_Helicopter`(동체·꼬리 붐·스키드·회전하는 메인/테일 로터) 프로시저럴 프리팹이고, 3D 사운드에 도플러가 적용됩니다.
- Act 4(거짓 평화)에서 비행·포격이 멎고(`Calm`), Act 5 섬광에서 완전히 멈춥니다. 이후 Act 의 환경 프리셋이 하늘 재질도 함께 바꿉니다 (밤/새벽/낙진/구조).
- 경로·편대 규모·주기·속도·연기/섬광 값은 `WarConfig` 와 씬의 `FlightRoutes` 웨이포인트에서 조정합니다.

## 변수 (`WarState`)
`Radiation`, `Comfort`, `Sanity`, `BodyTemp`, `Food/Water/Medicine`, `ActionPoints`, `HasGasMask/IodineTaken/IodineBeforeFallout`, `DeceptionCluesCount`, `RadioFrequency`, 동행자 `Joined/Lost/Refused`, `BrokenWindows`.

## 폴더
```
Assets/Scenarios/S01_WarTime/
  Game.Scenarios.S01.asmdef · Scenes/ · Prefabs/(S01_Fighter, S01_Helicopter) · Data/(WarConfig, Definition, Items, Objectives, DeathReasons, Endings, ScoreRules, Presets)
  Scripts/Systems  WarConfig · WarState · WarKeys · WarAudio · WarHud · WarWindows · WarFireExclusion · WarOutbreak · WarFlyovers · WarAircraft · WarRotorSpin · WarFx · WarTime
  Scripts/Interactables  WarPickup · WarRadio · WarSurvivor
  Scripts/Acts  WarActBase · Act1_CrackInDaily … Act6_TrueArmistice
```

## 알려진 한계
- 소품 위치는 빌드 때 집의 빈 자리를 탐색해 정합니다. 어색하면 `Props` 아래 오브젝트를 옮기세요.
- 비행 기체는 `Time.deltaTime` 기반 연출입니다 (게임플레이 타이밍과 무관).
- 밸런스는 스모크 테스트가 "충분히 준비하면 진 엔딩 가능, 준비 없이는 사망"까지만 검증합니다. 직접 플레이해 조정하세요.
