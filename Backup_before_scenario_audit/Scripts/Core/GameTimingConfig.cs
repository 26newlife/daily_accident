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
        [Header("Act 3 — 복도·이웃 (아직 미구현: 구현 시 이 값을 사용)")]
        [Min(1f)] public float act3DurationTarget = 90f;
        [Min(1f), Tooltip("복도 NPC 구조 상호작용 소요 (약 20~30초)")] public Vector2 act3NpcRescueSeconds = new Vector2(20f, 30f);
        [Min(1f)] public float act3HardTimeoutSeconds = 90f;

        [Header("Act 4 — 최종 탈출 (아직 미구현: 구현 시 이 값을 사용)")]
        [Min(1f), Tooltip("루트별 완료 목표 (약 2분 20초 이내)")] public float act4RouteTargetSeconds = 140f;
        [Min(1f)] public float act4HardTimeoutSeconds = 140f;
        [Min(1f), Tooltip("계단 이동 전체 소요 — 층수는 압축 연출")] public float stairsTotalSeconds = 35f;
        [Min(0.1f), Tooltip("중간층 통과 시 짧은 페이드/이동 연출 길이")] public float stairsMidFloorFadeSeconds = 0.7f;
        [Min(1f), Tooltip("대피공간 농성 루트 구조 대기 시간 (약 40~50초)")] public float shelterRescueWaitSeconds = 45f;

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
