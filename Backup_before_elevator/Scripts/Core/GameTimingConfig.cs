using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 플레이 템포(시간)와 난이도 배율을 한 곳에 모은 설정. 전체 한 판이 약 7분(6~8분)이 되도록 맞춰져 있다.
    /// 모든 값은 실시간(1:1) 초 기준이며 Inspector 에서 이 에셋 하나만 고치면 된다 (Assets/Data/GameTimingConfig.asset).
    /// 구현된 Act 1~2 는 이 값을 읽고, 아직 없는 Act 3~4 는 나중에 같은 값을 읽어 이 템포를 따른다.
    ///
    ///   구간            목표 시각         핵심
    ///   Act 1 (전조)    0:00 ~ 1:40       0:20 징후, 0:40 방송, 1:45 하드 타이머 폭발
    ///   Act 2 (폭발)    1:40 ~ 3:10       폭발 +9초 화재, 진압/농성 선택까지 약 90초
    ///   Act 3 (복도)    3:10 ~ 4:40       이웃 구조/경로 판단 약 90초
    ///   Act 4 (탈출)    4:40 ~ 7:00       루트별 약 2분 20초 이내
    /// </summary>
    [CreateAssetMenu(fileName = "GameTimingConfig", menuName = "GameUI/Game Timing Config")]
    public class GameTimingConfig : ScriptableObject
    {
        public enum BeatKind { Scent, Message, Subtitle }

        [Serializable]
        public struct TimedBeat
        {
            [Tooltip("Act 1: 게임 시작 후 초 / Act 2: 폭발 후 초")] public float time;
            public BeatKind kind;
            [TextArea(1, 2)] public string text;
            [Min(0.5f)] public float seconds;
        }

        [Serializable]
        public struct HoldOverride
        {
            [Tooltip("상호작용 id (정확히 일치)")] public string id;
            [Min(0f)] public float seconds;
        }

        // ───────── 구간 목표 (표시/로그/하드 타이머 기준) ─────────
        [Header("Act 목표 시작 시각 (게임 시작 후 초, 로그·오버레이 비교용)")]
        [Min(0f)] public float act1TargetStart = 0f;
        [Min(0f)] public float act2TargetStart = 100f;      // 1:40
        [Min(0f)] public float act3TargetStart = 190f;      // 3:10
        [Min(0f)] public float act4TargetStart = 280f;      // 4:40
        [Min(0f)] public float targetEnd = 420f;            // 7:00
        [Min(1f), Tooltip("사건·목표·위험 변화 사이 최대 간격 (환경 연출로 채운다)")] public float maxQuietSeconds = 25f;

        // ───────── Act 1 ─────────
        [Header("Act 1 — 평온한 아침과 전조")]
        [Min(1f), Tooltip("알람이 이 시간 안에 잦아든다(자동으로 꺼짐). 끄는 행동은 선택")] public float alarmAutoSilenceSeconds = 5f;
        [Min(0f), Tooltip("가스 냄새·누출음·멀티탭 스파크 시작")] public float act1SignsTime = 20f;
        [Min(0f), Tooltip("긴급 방송")] public float act1BroadcastTime = 40f;
        [Tooltip("방송 자막 길이(초) — 합계 약 12~15초")] public float[] broadcastCueSeconds = { 5.5f, 4f, 3.5f };
        [Min(1f), Tooltip("조치를 못 끝내도 이 시각에 폭발(Act 2 전환)")] public float act1HardTimeoutTime = 105f;
        [Min(0f), Tooltip("생존 조치를 모두 끝낸 뒤 Act 2 로 넘어가기까지 (실시간 초)")] public float act1ClearToAct2Delay = 1f;
        [Min(0f), Tooltip("Act 2 시작 후 폭발까지 — 조치를 모두 끝낸 경우 (Act1 클리어 지연과 합쳐 약 3초)")] public float explosionDelayAfterClear = 2f;
        [Min(0f), Tooltip("Act 2 시작 후 폭발까지 — 하드 타이머로 넘어온 경우")] public float explosionDelayAfterTimeout = 0.5f;
        [Min(1f), Tooltip("냄새 묘사 반복 간격")] public float smellRepeatSeconds = 15f;
        [Tooltip("Act 1 환경 연출 (시작 후 초)")] public TimedBeat[] act1Beats =
        {
            new TimedBeat { time = 55f, kind = BeatKind.Scent, text = "복도에서 사람들이 웅성거리는 소리가 들린다.", seconds = 4f },
            new TimedBeat { time = 80f, kind = BeatKind.Message, text = "멀리서 구급차 사이렌 소리가 들려온다.", seconds = 4f },
        };

        // ───────── Act 2 ─────────
        [Header("Act 2 — 폭발과 연쇄 화재 (폭발 시각 기준 상대 초)")]
        [Min(0f), Tooltip("폭발 후 화재 시작 — 약 8~10초")] public float act2FireDelay = 9f;
        [Min(0f), Tooltip("복도에 불길이 번지는 시각")] public float act2CorridorFireDelay = 45f;
        [Min(1f), Tooltip("시간 초과로 Act 2 종료 (약 90초)")] public float act2EndDelay = 85f;
        [Min(0f), Tooltip("클리어 후 Act 3 로 넘어가기까지 (실시간 초)")] public float act2ClearToAct3Delay = 2f;
        [Tooltip("Act 2 환경 연출 (폭발 후 초)")] public TimedBeat[] act2Beats =
        {
            new TimedBeat { time = 24f, kind = BeatKind.Scent, text = "천장에서 갈라지는 소리가 난다.", seconds = 4f },
            new TimedBeat { time = 55f, kind = BeatKind.Message, text = "이웃집에서 비명이 들린다.", seconds = 4f },
            new TimedBeat { time = 70f, kind = BeatKind.Message, text = "멀리서 사이렌 소리가 점점 가까워진다.", seconds = 4f },
        };

        // ───────── 상호작용 소요 시간 ─────────
        [Header("길게 누르기 시간 (상호작용 id 별, 목록에 없으면 오브젝트의 Inspector 값)")]
        public HoldOverride[] holdOverrides =
        {
            new HoldOverride { id = "window_living_lock", seconds = 0.8f },
            new HoldOverride { id = "window_bedroom_lock", seconds = 0.8f },
            new HoldOverride { id = "window_kitchen_lock", seconds = 0.8f },
            new HoldOverride { id = "window_living", seconds = 0.8f },
            new HoldOverride { id = "window_bedroom", seconds = 0.8f },
            new HoldOverride { id = "window_kitchen", seconds = 0.8f },
            new HoldOverride { id = "fuse_cock", seconds = 1.0f },
            new HoldOverride { id = "gas_valve_main", seconds = 1.0f },
            new HoldOverride { id = "multitap_plug", seconds = 0.8f },
        };

        // ───────── 화재 / 소화기 ─────────
        [Header("화재 확산 / 소화기")]
        [Min(0.1f), Tooltip("모든 불의 성장 속도 배율 (1.5~2배)")] public float fireGrowthMultiplier = 1.5f;
        [Min(0.1f), Tooltip("불이 옮겨붙는 속도 배율 (번짐 대기시간을 이 값으로 나눈다)")] public float fireSpreadMultiplier = 1.75f;
        [Range(0.3f, 1f), Tooltip("이 크기에 도달하면 소화기로 진압할 수 없다 → 2-B(수건 농성)로 유도")] public float uncontrollableIntensity = 0.9f;
        [Range(0f, 1f), Tooltip("진압 불가 상태에서 소화기/물이 줄여주는 비율")] public float uncontrollableSuppressMultiplier = 0.05f;
        [TextArea(1, 2)] public string uncontrollableMessage = "불길이 너무 커져 소화기로는 감당할 수 없다. 연기를 피하고 몸을 지켜야 한다.";
        [Min(0.01f), Tooltip("소화기 분사 중 초당 줄어드는 불 크기 (불 2개 진압에 약 15~20초)")] public float extinguisherSuppressPerSecond = 0.09f;
        [Min(1f), Tooltip("소화기 총 분사 가능 시간(초)")] public float extinguisherChargeSeconds = 22f;

        // ───────── 가스 / 연기 / 호흡기 ─────────
        [Header("가스·연기·호흡기 배율 (사망/생존 판정 기준은 그대로, 속도만 조정)")]
        [Min(0.1f)] public float gasRiseMultiplier = 1.4f;
        [Min(0.1f)] public float smokeRiseMultiplier = 1.8f;
        [Min(0.1f)] public float smokeClearMultiplier = 1.0f;
        [Min(0.05f), Tooltip("호흡기 게이지 감소 배율. 0.5 면 서서 짙은 연기(0.6) 속을 걸을 때 약 28초")] public float vitalsDrainMultiplier = 0.5f;

        // ───────── Act 3 / Act 4 (구현 시 이 값을 따른다) ─────────
        [Header("Act 3 — 복도·이웃")]
        [Min(1f)] public float act3DurationTarget = 90f;
        [Min(1f), Tooltip("복도 NPC 구조 상호작용 소요 (약 20~30초) — 목표 시간 안내용")] public Vector2 act3NpcRescueSeconds = new Vector2(20f, 30f);
        [Min(1f), Tooltip("Act 3 시작 후 이 시간이 지나면 어떤 상태든 Act 4 로 넘어간다")] public float act3HardTimeoutSeconds = 90f;
        [Min(0f), Tooltip("현관 손잡이가 뜨거워 복도로 못 나가는 경우 Act 3(상황 판단) 길이")] public float act3BlockedSeconds = 25f;
        [Min(0f), Tooltip("계단 방화문을 확인한 뒤 Act 4 로 넘어가기까지 (실시간 초)")] public float act3ClearDelay = 2f;

        [Header("Act 3 — 복도 / 계단실 연기")]
        [Min(0f), Tooltip("Act 3 시작 후 복도 연기가 오르기 시작하는 시각(초)")] public float corridorSmokeStartDelay = 5f;
        [Min(1f), Tooltip("복도 연기가 최대치까지 차는 시간(초)")] public float corridorSmokeRiseSeconds = 70f;
        [Range(0f, 1f)] public float corridorSmokeMax = 0.55f;
        [Min(1f), Tooltip("현관문을 열어 두었을 때 복도 연기 상승 배율")] public float corridorOpenDoorSmokeMultiplier = 1.5f;
        [Min(0f), Tooltip("폭발 후 이 시간(초)이 지나면 8~9층 연기가 비상계단으로 올라오기 시작한다 (이전에 지나가면 '초기 방화문 관리 성공')")] public float stairSmokeArrivalSeconds = 95f;
        [Min(1f), Tooltip("계단 연기가 최대치까지 차는 시간(초)")] public float stairSmokeRiseSeconds = 40f;
        [Range(0f, 1f), Tooltip("방화문을 열었을 때 계단 연기가 이 값 이상이면 하강 불가(stairwellDownBlocked)")] public float stairBlockedThreshold = 0.45f;

        [Header("Act 3 — NPC (할머니 / 청년)")]
        [Min(0.5f)] public float grandmotherPressInterval = 6f;
        [Min(1f), Tooltip("Act 3 복도 진입 후 이 시간(초) 동안 말을 걸지 않으면 할머니가 엘리베이터를 타려 한다(Panic)")] public float grandmotherPanicSeconds = 35f;
        [Min(1f), Tooltip("Panic 후 엘리베이터에 탑승하기까지(초) — 이 안에 설득하면 막을 수 있다")] public float grandmotherBoardSeconds = 9f;
        [Min(0.1f), Tooltip("할머니 동행 이동 속도(m/s). 플레이어 걷기(1.8)보다 느리다")] public float grandmotherFollowSpeed = 1.0f;
        [Min(1f), Tooltip("동행 NPC 가 이 거리(m) 이상 멀어지면 멈춰서 기다려 달라고 한다")] public float npcWaitDistance = 4.5f;
        [Min(0.5f)] public float npcFollowDistance = 1.4f;
        [Min(3f), Tooltip("동행 NPC 가 이 거리(m) 이상 뒤처지면 플레이어 뒤로 순간이동")] public float npcWarpDistance = 9f;
        [Min(0f), Tooltip("연기 1.0 에서 NPC 호흡 게이지(0~1)가 초당 줄어드는 양 (수건 없을 때)")] public float npcSmokeDrainPerSecond = 0.035f;
        [Range(0f, 1f), Tooltip("젖은 수건을 받은 NPC 의 감소 배율")] public float npcTowelDrainMultiplier = 0.3f;
        [Min(0f), Tooltip("설득(대화 선택) 처리 시간 — 길게 누르기가 아니라 대사 표시 시간")] public float npcPersuadeSeconds = 2.5f;
        [Min(0f), Tooltip("쓰러진 NPC 를 부축해 일으키는 길게 누르기 시간")] public float npcLiftHoldSeconds = 3f;
        [Min(1f), Tooltip("1003호에 쓰러진 할머니가 구출되지 않으면 의식을 잃기까지(초)")] public float grandmotherFallenSeconds = 55f;
        [Min(0f), Tooltip("플레이어가 복도에 나온 뒤 청년이 수건 없이 계단으로 달려가기까지(초)")] public float youngManRunDelay = 10f;
        [Min(0.5f)] public float youngManRunSpeed = 3.4f;
        [Min(0f), Tooltip("청년이 계단에 들어간 뒤 쓰러지는 소리가 들리기까지(초)")] public float youngManFallSeconds = 6f;

        [Header("Act 4 — 최종 탈출")]
        [Min(1f), Tooltip("루트별 완료 목표 (약 2분 20초 이내)")] public float act4RouteTargetSeconds = 140f;
        [Min(1f), Tooltip("Act 4 시작 후 이 시간 안에 어떤 루트로도 출발하지 못하면 연기에 갇힌다(ExitTimeout)")] public float act4HardTimeoutSeconds = 140f;
        [Min(1f), Tooltip("계단 이동 전체 소요 — 층수는 압축 연출 (루트 D 의 벽 짚고 걷는 3구간 합계)")] public float stairsTotalSeconds = 35f;
        [Min(0.1f), Tooltip("중간층 통과 시 짧은 페이드/이동 연출 길이")] public float stairsMidFloorFadeSeconds = 0.7f;
        [Min(1f), Tooltip("대피공간 농성 루트 구조 대기 시간 (약 40~50초)")] public float shelterRescueWaitSeconds = 45f;

        [Header("Act 4 — 루트 A (옥상)")]
        [Min(0f), Tooltip("14·17층 방화문이 열려 있을 때 연기가 따라오기 시작하기까지(초)")] public float fireDoorChaseSeconds = 10f;
        [Min(0f), Tooltip("연기가 따라오기 시작한 뒤 계단참 연기 상승 속도(1/초)")] public float landingSmokeRisePerSecond = 0.045f;
        [Min(0.1f), Tooltip("비상 열쇠함 유리 깨기 길게 누르기(초)")] public float roofKeyBoxHoldSeconds = 2.5f;
        [Min(0.1f), Tooltip("옥상 구조 신호(손 흔들기) 길게 누르기(초)")] public float roofSignalHoldSeconds = 6f;
        [Min(0f), Tooltip("구조 신호 후 헬기/고가사다리차 도착 연출 길이(초)")] public float rooftopArrivalSeconds = 7f;

        [Header("Act 4 — 루트 B (경량칸막이)")]
        [Min(1)] public int partitionHitsHammer = 3;
        [Min(1)] public int partitionHitsKick = 6;
        [Min(0.05f), Tooltip("타격 사이 최소 간격(초)")] public float partitionHitCooldown = 0.35f;
        [Min(0f), Tooltip("1003호 현관을 통해 복도를 지나 계단까지 가는 압축 이동 연출(초)")] public float routeBCorridorRunSeconds = 4f;

        [Header("Act 4 — 루트 C (대피공간 농성)")]
        [Range(0f, 1f), Tooltip("젖은 수건으로 한 면을 막았을 때의 밀봉률")] public float sealTowelRatio = 0.55f;
        [Range(0f, 1f), Tooltip("청테이프로 한 면을 막았을 때의 밀봉률")] public float sealTapeRatio = 1.0f;
        [Min(0.1f), Tooltip("문틈 한 면을 막는 길게 누르기(초)")] public float sealHoldSeconds = 1.6f;
        [Range(0f, 1f), Tooltip("밀봉 0% 일 때 닫힌 대피공간으로 새어드는 연기 비율")] public float shelterUnsealedLeak = 0.7f;
        [Range(0f, 1f), Tooltip("밀봉 100% 일 때 새어드는 연기 비율")] public float shelterSealedLeak = 0.05f;
        [Range(0f, 1f), Tooltip("대피공간 창문을 열면 체감 연기가 이 비율로 줄어든다(신선한 공기)")] public float shelterWindowFactor = 0.55f;
        [Min(0f), Tooltip("119 에 위치를 잘못 말했을 때 구조 도착 지연(초)")] public float rescueDelayWrong119 = 25f;
        [Min(0f), Tooltip("119 에 신고하지 않았을 때 구조 도착 지연(초)")] public float rescueDelayNo119 = 35f;
        [Min(0f), Tooltip("고가사다리차가 창문 앞에 도착하는 연출 길이(초)")] public float ladderArrivalSeconds = 6f;

        [Header("Act 4 — 루트 D (계단 하강)")]
        [Range(0.2f, 1f), Tooltip("벽을 짚고 내려가는 구간의 이동 속도 배율")] public float descentSpeedMultiplier = 0.55f;
        [Range(0f, 0.1f), Tooltip("하강 중 카메라 미세 흔들림 세기")] public float descentShakeAmplitude = 0.012f;
        [Min(0f), Tooltip("1층 도착 후 외부 집결지 연출 → 클리어까지(초)")] public float assemblyEndingDelay = 7f;

        // ───────── 시계 표시 / 개발용 로그 ─────────
        [Header("시계 표시 (진행은 1:1, 표시만 가상의 오전 시각)")]
        public bool useVirtualClock = true;
        [Min(0f), Tooltip("가상 시계의 시작 시각 (초). 7:12:00 = 25920")] public float virtualClockStartSeconds = 25920f;
        public bool virtualClockShowSeconds = true;

        [Header("개발용 (빌드에서는 비활성)")]
        public bool logTimings = true;
        public bool overlayVisibleAtStart = false;

        // ───────── 접근 ─────────
        private static GameTimingConfig active, fallback;

        /// <summary>씬의 GameClock 이 등록한 설정. 없으면 기본값 인스턴스를 만들어 쓴다 (필드 초기값이 곧 기본값)</summary>
        public static GameTimingConfig Current
        {
            get
            {
                if (active != null) return active;
                if (fallback == null) { fallback = CreateInstance<GameTimingConfig>(); fallback.hideFlags = HideFlags.HideAndDontSave; }
                return fallback;
            }
        }

        public static void SetActive(GameTimingConfig config) => active = config;
        public static void ClearActive(GameTimingConfig config) { if (active == config) active = null; }

        /// <summary>id 에 대한 길게 누르기 시간. 목록에 없으면 fallback(오브젝트 Inspector 값)</summary>
        public static float ResolveHold(string id, float fallbackSeconds)
        {
            GameTimingConfig c = Current;
            if (c.holdOverrides != null && !string.IsNullOrEmpty(id))
                foreach (HoldOverride h in c.holdOverrides) if (h.id == id) return h.seconds;
            return fallbackSeconds;
        }

        /// <summary>게임 시간(초) → 화면에 보일 시계 문자열</summary>
        public string FormatClock(float gameSeconds)
        {
            if (!useVirtualClock) return GameClock.Format(gameSeconds);
            int s = Mathf.FloorToInt(virtualClockStartSeconds + Mathf.Max(0f, gameSeconds)) % 86400;
            int h = s / 3600, m = (s % 3600) / 60, sec = s % 60;
            return virtualClockShowSeconds ? $"{h:00}:{m:00}:{sec:00}" : $"{h:00}:{m:00}";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active = null; fallback = null; }
    }
}
