# 공통 HUD 레이아웃 (GameHUD 프리팹)

모든 시나리오가 `Assets/Prefabs/UI/GameHUD.prefab` 한 벌을 쓴다 (`GameScene` 의 `Gameplay/GameCanvas/HUD`). 빌더: `MainMenuBuilder.Hud.cs` (`BuildHudHierarchy`).
기존 씬에 적용: `Tools > GameUI > HUD > Rebuild HUD In Place` (HUD 교체 + 설정 팝업에 '새 목표 알림' 토글 추가).
도식: `Docs/Screenshots/hud_layout_diagram_1920x1080.png`.

## 영역 (기준 1920x1080, `CanvasScaler = Expand` — 캔버스는 항상 이 크기 이상, HUD 루트는 `SafeAreaFitter` 로 세이프 에어리어 안쪽)
| 영역 | 요소 | 비고 |
|---|---|---|
| 우측 상단 | `SurvivalStats` (`SurvivalStatsPanel`) | 폭 400, 세로 정렬(행 간격 10). 평소엔 숨김, 보이는 행이 없으면 페이드 아웃 |
| 상단 중앙 | `ObjectiveBanner` (새 목표 알림) | 640x72, 약 3초(페이드 0.3+유지 2.4+0.3). 설정 `새 목표 알림` 으로 끔. 새 목표·완료·실패 때만 |
| 상단 중앙(아래) | S01 `AlertPopup` (재난문자) | 700x190, 배너 바로 아래 |
| 좌측 상단 | `ObjectiveDetail` (J 패널) | 460x560. 눌러서 열고 다시 눌러/일시정지·스마트폰이면 닫힘. 게임 시간은 흐름 |
| 중앙 | `Crosshair`, `HoldBar`(-50), `Scent`(+120, 폭 560) | |
| 하단 중앙 (위로 쌓임) | `InventoryBar` 40~112 → `Prompt` 124~182 → `HintToast` 192~256 → `Message` 266~326 → `Subtitle` 336~432 | 고정 슬롯이라 동시에 떠도 겹치지 않음 |

목표 목록은 **상시 표시하지 않는다** — `J` 패널로만 본다.

## 생존 상태 행 등록 (시나리오가 하는 일)
`SurvivalStatsPanel.Instance.SetStat(id, 라벨, 0~1 값, 수치 문구, 보일 조건, 좋은색, 나쁜색, 높을수록위험)` / `SetLine(id, 문구, 보일 조건)`.
- 공통(`HUD`): 호흡기(연기 속·감소 시에만), 화상(화상이 있을 때만) — `useRespiratoryGauge=false` 면 둘 다 숨김
- S01(`WarHud`): 방사능·쾌적도·정신력·체온 막대 + 자원 줄 2개 (항상 표시)

## 겹침 점검 (`Tools > GameUI > HUD > Check HUD Layout`)
8개 해상도(1920x1080, 1280x720, 1366x768, 1920x1200, 1280x800, 1024x768, 2560x1080, 3440x1440) x 2개 프로필(S01 최대/S03 최대), 세이프 에어리어 2.5% 가정, J 패널·S01 팝업 포함 — **겹침·잘림 0건**.
`Capture HUD Shots` 는 같은 상태를 PNG 로 저장한다. 배치모드에서는 오른쪽이 잘려 찍히므로 **에디터에서 직접 실행**할 것.

## 원인 (예전 겹침)
공통 `HUD.ObjectiveText`(앵커 좌상단 (48,-48), 760x340)와 S01 `WarHud.StatsPanel`(앵커 좌상단 (20,-20), 430x250)이 같은 앵커·같은 좌표를 썼다.
같은 종류의 겹침: S01 `AlertPopup` 대 배너, S01 `Notice`(y=150) 대 호흡기 게이지(y=168)·힌트 토스트(y=150), 자막·안내문·토스트 근접.
