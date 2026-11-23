# 인테리어 스타일 가이드 (참고: "Modern Apartment Interior Pack" 스크린샷, `Docs/Screenshots/스크린샷 2026-11-23 0634*.png`)

레이아웃·치수·가구 배치·창문·문 위치·상호작용 id 는 **그대로**. 바꾸는 것은 겉모습(재질·조명·후처리·모델)뿐이다. 외부 유료 에셋은 쓰지 않는다 (경로 B — 에셋 팩이 프로젝트에 없음. 팩을 구매해 임포트하면 가구·가전 모델 품질이 더 높아질 수 있다).

## 참고 이미지에서 읽은 스타일
| 요소 | 특징 | 적용 |
|---|---|---|
| 벽·천장 | 따뜻한 오프화이트 무광(`#E8E3DA` 부근), 모서리의 부드러운 AO | `Int_Wall` `EBE6DC`, `Int_Ceiling` `F6F3EC` |
| 바닥 | 밝은 애쉬/메이플 판재(폭 0.12m, 결, 약한 광택), 현관은 연회색 | `Int_FloorWood` = 절차적 오크 마루 PBR, 현관 타일은 기존 유지 |
| 가구 | 라이트 오크 + 얇은 검정 금속 다리, 침구는 슬레이트 블루(`#4F6B8A`)·크림 | `Wood` `C79E69`, `Blanket` `4F6B8A`, `MetalDark` `2B2D31` |
| 주방 | 회백색 하부장, 블랙 상판·싱크, 스틸 손잡이, 블랙 인덕션 | 대리석 상판 PBR, 나뭇결 하부장 |
| 문·창 | 그레이지 현관문 + 다크 그린 문틀, 흰 알루미늄 창, 그레이지 커튼 | 커튼 `B8A890` (현관문·문틀은 ③ Hero 단계) |
| 조명 | 창으로 들어오는 밝은 자연광, 또렷한 창 그림자, 약간 헤이즈한 하늘 | 톤매핑 Neutral + 따뜻한 화이트 밸런스 + 약한 블룸·비네트 |
| 소품 | 낮은~중간 밀도, 채도 낮은 색 | 책·액자·천 소품을 채도 낮은 톤으로 (소화기 빨강·포스트잇 노랑 등 기능 색은 유지) |

## PBR 머티리얼 (절차적, 코드 생성 — `Assets/Editor/Art/PbrTextureFactory.cs`)
`Assets/Materials/Interior/Textures/Pbr/` 에 Albedo / Normal / Mask(R 메탈릭 · G 오클루전 · A 스무스니스) 세트가 있다. 타일링 가능(주기 노이즈), 512~1024.
| 세트 | 쓰는 곳 | 메모 |
|---|---|---|
| `oak_planks` (1024) | 거실·침실 마루 (UV 1m 당 8줄) | 판 길이 1/0.5/0.33m 로 이어짐, 이음매 홈 |
| `wood_grain` | 가구·걸레받이 (틴트로 오크/라이트/월넛) | 노멀 약하게 |
| `plaster` | 벽·천장 | **노멀 0** — 큰 면은 UV 가 늘어나 노멀이 거칠어 보임 (알베도 얼룩만) |
| `fabric_weave` | 소파·침구·베개·커튼·러그 | 평직 64올, 타일링 3~4 |
| `steel_brushed` | `Metal`, `MetalDark` | 노멀 0.1 (냉장고 등 큰 면에서 구겨져 보이지 않게) |
| `marble_white` | `StoneTop` (주방·화장실 상판) | |
| `leather_grain` | `Int_Leather` (③ Hero 모델용) | 보로노이 주름 |
재질 갱신: `Tools > GameUI > Art > Upgrade Interior Materials (Palette + PBR)` — 이름이 같은 `Int_*.mat` 를 갱신하므로 씬·프리팹 재빌드 없이 모든 곳에 반영된다.
파일을 지우면 같은 결과로 재생성된다 (결정적 해시).

## 조명 · 후처리 (실시간 + SSAO, 라이트맵 베이크 없음)
- 기존 유지: 3색 환경광, `Sun_Morning`(소프트 섀도), 창가 포인트 라이트, `LightGroup`(정전·소등), 방별 반사 프로브.
- **Player 카메라 후처리를 켰다** (예전에는 꺼져 있었음) + SMAA. 빌더(`BuildPlayerPrefab`)도 같은 설정을 한다.
- Volume 프로필 2종 (`Assets/Settings/House_Interior_*.asset`), 집 프리팹의 `HouseVolumes`(Volume_Day weight 1, Volume_Smoke weight 0~1):
  - Day: Tonemapping Neutral, Bloom(문턱 1.05, 0.22), Vignette 0.16, 노출 +0.05 / 대비 +8 / 채도 +4, 화이트 밸런스 +7
  - Smoke: 노출 -0.45, 채도 -32, 비네트 0.38 — `EnvironmentPreset.windowView == DarkSmoky` 일 때 `HouseController` 가 서서히 전환
- SSAO(렌더러 피처): 강도 0.4→0.75, 반경 0.32, 직접광 0.4
- 라이트 프로브는 **만들지 않았다** — 베이크 없이는 효과가 없다. 나중에 베이크를 켜면 `Static` 플래그와 함께 추가한다.
- 정전·화재·소등은 조명/환경광으로 연출되므로 이 Volume 과 충돌하지 않는다 (확인: S03 스모크 85/0).

## 정적 성능 기준선 (`Tools > GameUI > Art > Scene Stats`)
렌더러 1,647 · 삼각형 232,270 (집 24,286 / 창밖 207,984) · 고유 머티리얼 198 · 실시간 조명 23(그림자 1) · 반사 프로브 9. ②단계는 텍스처만 늘렸다 (VRAM 약 10MB).
집 모델은 아직 프리미티브 기반이라 삼각형 예산(Hero 3,000~10,000 / 가구 1,000~5,000 / 소품 100~1,500)에 여유가 크다.

## 프리뷰 캡처
`Tools > GameUI > Art > Capture Room Previews (before|after)` → `Docs/Screenshots/{before|after}_{방}_{player|diag}.png` (13장, 같은 구도).
주의: 편집 모드 캡처는 런타임 `LightGroup` 시작 상태(침실만 ON)를 재현하지 못한다.

## 빌더 순서 주의
`Build Apartment Scene` → (필요 시) `Build Apartment Interior`. 두 빌더 모두 끝에서 `BaseHouseFixups.Apply` 를 다시 적용하므로 보정(B-02, 화재 소품 태그, 베란다 치우기 소품, 후처리 Volume)이 유지된다. 기존 에셋에 직접 적용하려면 `Tools > GameUI > Base House > Apply Fixups`.
