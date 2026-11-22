using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 1(전시 상황) 의 모든 수치·시간·방송 문구·연출 값. 코드에는 숫자를 박지 않고 이 에셋(Assets/Scenarios/S01_WarTime/Data/WarConfig.asset)만 고치면 된다.
    /// 시간은 모두 게임 시계(GameClock) 초 단위 (기본 배율 1:1 = 실시간 초).
    /// </summary>
    [CreateAssetMenu(fileName = "WarConfig", menuName = "GameGameplay/S01 War Config")]
    public class WarConfig : ScriptableObject
    {
        [Serializable]
        public struct Channel
        {
            public string name;
            [Tooltip("표시용 주파수 문구 (예: SW 7.45 MHz)")] public string label;
            [Tooltip("이 채널을 처음 들었을 때 얻는 단서 id (비우면 단서 없음)")] public string clueId;
            [TextArea(2, 5)] public string[] lines;
        }

        /// <summary>전쟁 발발 후 창밖 하늘·조명·안개 색감</summary>
        [Serializable]
        public struct SkyProfile
        {
            public Vector3 sunEuler;
            [Min(0f)] public float sunIntensity;
            public Color sunColor;
            public Color ambientColor;
            public Color fogColor;
            [Min(0f)] public float fogDensity;
            [Tooltip("Skybox/Procedural 의 _SkyTint")] public Color skyTint;
            [Range(0f, 5f)] public float atmosphereThickness;
            [Range(0f, 8f)] public float exposure;
            public Color groundColor;
        }

        // ───────── Act 1 ─────────
        [Header("Act 1 — 일상 속의 균열")]
        [Min(10f)] public float act1LimitSeconds = 80f;
        [Min(1f)] public float act1ToAct2DelaySeconds = 3f;
        [Min(0.5f)] public float alertPopupSeconds = 7f;
        [Min(1f)] public float lightSwayIntervalSeconds = 9f;
        [Min(0f), Tooltip("재난문자 경보음이 울린 뒤 창밖이 전시 상황으로 바뀌기 시작할 때까지 (초)")] public float outbreakDelayAfterAlert = 2f;
        [TextArea(2, 4)] public string alertText = "[행정안전부] 실제상황. 적의 전면 남침 확인. 즉시 지하 대피소로 이동하고 생필품을 확보하십시오.";
        public string alertSender = "행정안전부";
        [Tooltip("Act 1 환경 연출 (시작 후 초 → 자막)")] public GameTimingConfig.TimedBeat[] act1Beats =
        {
            new GameTimingConfig.TimedBeat { time = 7f, kind = GameTimingConfig.BeatKind.Subtitle, text = "창밖에서 민방위 사이렌이 길게 울린다.", seconds = 4f },
            new GameTimingConfig.TimedBeat { time = 20f, kind = GameTimingConfig.BeatKind.Subtitle, text = "굉음과 함께 전투기가 건물 위를 낮게 스쳐 지나간다.", seconds = 4f },
            new GameTimingConfig.TimedBeat { time = 40f, kind = GameTimingConfig.BeatKind.Scent, text = "복도에서 사람들이 뛰어다니는 발소리가 들린다.", seconds = 4f },
            new GameTimingConfig.TimedBeat { time = 60f, kind = GameTimingConfig.BeatKind.Subtitle, text = "또 한 번, 더 가까운 곳에서 전투기 소리가 쏟아진다. 서둘러야 한다.", seconds = 4f },
        };

        // ───────── 전시 상황 연출 (창밖) ─────────
        [Header("전쟁 발발 — 창밖 전환")]
        [Min(0.5f), Tooltip("평화로운 하늘 → 전시 하늘로 바뀌는 데 걸리는 시간")] public float skyTransitionSeconds = 9f;
        public SkyProfile warSky = new SkyProfile
        {
            sunEuler = new Vector3(11f, -30f, 0f), sunIntensity = 0.55f, sunColor = new Color(0.95f, 0.48f, 0.32f),
            ambientColor = new Color(0.24f, 0.17f, 0.16f), fogColor = new Color(0.32f, 0.22f, 0.2f), fogDensity = 0.0035f,
            skyTint = new Color(0.5f, 0.33f, 0.3f), atmosphereThickness = 2.4f, exposure = 0.7f, groundColor = new Color(0.18f, 0.14f, 0.13f),
        };
        [Header("연기 기둥 · 포격 섬광")]
        [Min(0)] public int plumeCount = 5;
        [Tooltip("창 정면(+z) 기준 거리 범위 (m)")] public Vector2 plumeDistance = new Vector2(240f, 460f);
        [Tooltip("좌우 퍼짐 (m)")] public float plumeSpread = 300f;
        [Min(10f)] public float plumeHeight = 95f;
        [Min(0.5f)] public float plumeEmissionRate = 5f;
        [Tooltip("포격 섬광 간격 (초, 최소~최대)")] public Vector2 flashInterval = new Vector2(5f, 13f);
        [Tooltip("섬광 뒤 폭음이 들리기까지 (초)")] public Vector2 flashSoundDelay = new Vector2(1.0f, 2.8f);
        [Range(0f, 1f), Tooltip("폭음과 함께 집이 살짝 흔들릴 확률")] public float flashShakeChance = 0.3f;
        [Header("엠비언트 사운드 (절차 음 — 실제 음원은 WarAudio.overrides)")]
        [Tooltip("공습 사이렌 간격 (초)")] public Vector2 sirenInterval = new Vector2(28f, 45f);
        [Range(0f, 1f)] public float sirenVolume = 0.35f;
        [Range(0f, 1f)] public float distantBoomVolume = 0.5f;

        [Header("전투기·헬기 편대")]
        [Min(0f), Tooltip("전쟁 발발 후 첫 비행까지 (초)")] public float flyoverStartDelay = 9f;
        public Vector2 jetInterval = new Vector2(22f, 38f);
        [Min(1)] public int jetsPerFlight = 2;
        [Min(20f)] public float jetSpeed = 130f;
        public Vector2 heliInterval = new Vector2(45f, 70f);
        [Min(1)] public int helisPerFlight = 3;
        [Min(5f)] public float heliSpeed = 38f;
        [Min(1f), Tooltip("편대원 사이 간격 (m)")] public float formationSpacing = 22f;
        [Range(0f, 1f)] public float flyoverVolume = 0.9f;

        // ───────── Act 2 ─────────
        [Header("Act 2 — 폭풍 전야의 대비")]
        [Min(30f)] public float act2LimitSeconds = 240f;
        [Min(1)] public int actionPoints = 12;
        [Tooltip("Act 2 목표 판정 기준")] public int goalFood = 6;
        public int goalWater = 6;
        public int actionCostSearch = 1;
        [Min(1f)] public float act2ToAct3DelaySeconds = 3f;

        [Header("라디오")]
        public Channel[] channels =
        {
            new Channel { name = "공식 방송", label = "AM 1,134 kHz", clueId = null, lines = new[] {
                "국방부 공식 발표입니다. 전선은 현재 안정화 단계에 들어섰으며, 시민 여러분은 동요하지 마시고 지침에 따라 주십시오.",
                "수도권 방어선은 굳건히 유지되고 있습니다. 유언비어에 현혹되지 마십시오." } },
            new Channel { name = "단파 A", label = "SW 7.45 MHz", clueId = "clue_defense_collapse", lines = new[] {
                "……치직……수도권 방어선이 무너졌다. 반복한다, 북부 방어선 붕괴……치직……",
                "……후방 지휘부가 철수 중이다. 공식 발표를 믿지 마라……치직……" } },
            new Channel { name = "단파 B", label = "SW 11.20 MHz", clueId = "clue_special_weapon", lines = new[] {
                "……치직……특수 무기 운반 차량이 도시 외곽에서 목격되었다. 이동 정황이 확실하다……",
                "……지하나 방호 시설에 있는 사람들은 절대 밖으로 나가지 마라……치직……" } },
            new Channel { name = "단파 C", label = "SW 4.80 MHz", clueId = null, lines = new[] { "……치이이이이이이……(잡음뿐이다. 누군가의 숨소리 같은 것이 섞여 있다.)……" } },
        };
        [Tooltip("군 철수 정황 — 창밖 관찰로 얻는 단서")] public string withdrawalClueText = "도로를 달리는 군용 트럭 행렬이 모두 후방 쪽으로 향하고 있다. 철수하는 것처럼 보인다.";
        public float comfortBlanket = 20f;
        public float comfortClothes = 15f;

        // ───────── Act 3 ─────────
        [Header("Act 3 — 고립된 방주")]
        [Min(30f)] public float act3LimitSeconds = 220f;
        [Min(5f)] public float foodConsumeIntervalSeconds = 60f;
        [Min(5f)] public float waterConsumeIntervalSeconds = 55f;
        [Tooltip("합류 인원 1명당 소비 간격이 이만큼 짧아진다 (초)")] public float consumeIntervalPerSurvivor = 6f;
        [Tooltip("식량/물이 바닥난 채로 소비 시점이 오면 정신력이 줄어드는 양")] public float starveSanityLoss = 8f;
        public float sanityDrainPerSecond = 0.03f;
        public float sanityRecoverPerSecond = 0.05f;
        [Tooltip("쾌적도가 이 이상이면 정신력이 서서히 회복")] public float comfortForRecovery = 30f;
        public float refuseSanityLoss = 10f;
        [Tooltip("방문자 노크 시각 (Act 3 시작 후 초): 군인, 아이, 이웃")] public float[] visitorTimes = { 25f, 70f, 115f };
        [Min(5f)] public float visitorAnswerSeconds = 25f;
        [Tooltip("약탈자 야간 방문 시각")] public float raiderTime = 150f;
        public float raiderAnswerSeconds = 25f;
        [Tooltip("야간 환경으로 바뀌는 시각")] public float nightTime = 135f;
        public int raiderFoodDemand = 2;
        public int soldierMedicine = 2;
        public float childSanityBonus = 12f;
        public int neighborFood = 3, neighborWater = 3;
        [Tooltip("소등 위장이 통하는 최대 동거 인원 (사람이 많으면 기척이 새어 나간다)")] public int lightsOutMaxSurvivors = 1;
        public float defendFailSanityLoss = 12f;

        // ───────── Act 4 ─────────
        [Header("Act 4 — 기만된 평화")]
        [Tooltip("이 개수 이상의 기만 단서가 있어야 방송이 함정임을 간파")] public int deceptionClueThreshold = 2;
        [Min(5f)] public float act4PersuadeSeconds = 45f;
        [Min(1f)] public float act4DoorOutsideZ = 0.05f;
        public float survivorWalkSpeed = 0.9f;
        [TextArea(2, 4)] public string[] falsePeaceLines =
        {
            "긴급 속보입니다. 양측의 극적인 합의로 정전 및 종전이 선언되었습니다.",
            "모든 시민은 안심하고 대피소에서 나와 일상으로 복귀하시기 바랍니다.",
        };

        // ───────── Act 5 ─────────
        [Header("Act 5 — 섬광과 낙진")]
        [Min(5f)] public float flashDelayAfterBroadcast = 60f;
        [Min(2f), Tooltip("섬광 후 충격파가 도달하기까지 — 창에서 떨어져 몸을 낮출 시간")] public float shockwaveDelaySeconds = 8f;
        [Min(0.1f)] public float whiteoutSeconds = 4f;
        [Min(0.3f), Tooltip("이 거리(m) 안의 창가에 서 있으면 충격파·유리 파편에 즉사")] public float windowDangerRadius = 1.7f;
        [Tooltip("창가여도 웅크리고 있으면 간신히 피한다")] public bool crouchSavesNearWindow = true;
        [Tooltip("이 거리(m) 밖의 안쪽 방이면 피폭·한파가 줄어든다")] public float deepInteriorDistance = 2.8f;
        [Min(30f)] public float falloutSeconds = 170f;
        [Min(0f)] public float baseRadiationPerSecond = 0.9f;
        [Range(0f, 1f)] public float deepInteriorExposure = 0.6f;
        [Range(0f, 1f)] public float gasMaskExposure = 0.4f;
        [Range(0f, 1f)] public float iodineExposure = 0.7f;
        [Tooltip("낙진 시작 전 복용해야 요오드 효과 인정")] public bool iodineMustBeBeforeFallout = true;
        public float radiationDeathLevel = 100f;
        [Min(1f)] public float medicineIntervalSeconds = 30f;
        public float medicineRadiationRelief = 8f;
        public float contaminatedFoodRadiation = 6f;
        [Tooltip("핵겨울: 체온이 초당 떨어지는 양")] public float coldPerSecond = 0.7f;
        [Range(0f, 1f)] public float deepInteriorColdFactor = 0.8f;
        [Tooltip("쾌적도 1점당 체온 하락 감소 비율 (최대 70%)")] public float comfortWarmth = 0.008f;
        public float bodyTempDeathLevel = 0f;
        [Tooltip("낙진 중 식량/물 소비 간격 및 고갈 허용 횟수")] public float falloutConsumeIntervalSeconds = 60f;
        [Min(1)] public int starvationLimit = 4;

        // ───────── Act 6 ─────────
        [Header("Act 6 — 잿더미 위의 진짜 종전")]
        [Min(1f)] public float act6RotorDelaySeconds = 8f;
        [Min(1f)] public float act6EndingDelaySeconds = 14f;
        [TextArea(2, 4)] public string[] rescueLines =
        {
            "여기는 다국적 인도지원단입니다. 생존자 여러분, 들리십니까? 구조대가 도착했습니다.",
            "창문을 열지 마시고, 마스크나 젖은 수건으로 입과 코를 가린 채 지시를 기다리십시오.",
            "이것은 방송이 아닙니다. 지금, 건물 위에 구조 헬기가 있습니다.",
        };
    }
}
