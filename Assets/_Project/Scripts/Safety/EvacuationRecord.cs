using System.Collections.Generic;
using QuakeSurvival.Interaction;
using QuakeSurvival.Npc;
using QuakeSurvival.Quake;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>결과 화면의 한 줄: 행동요령 문구와 지켰는지 여부.</summary>
    public readonly struct RuleResult
    {
        public readonly string Text;
        public readonly bool Kept;
        public readonly int Points;
        public readonly bool Bonus;

        public RuleResult(string text, bool kept, int points, bool bonus)
        {
            Text = text;
            Kept = kept;
            Points = points;
            Bonus = bonus;
        }
    }

    /// <summary>결과 화면의 감점 한 줄: 대피지역 덕분에 넘긴 사고.</summary>
    public readonly struct PenaltyResult
    {
        public readonly string Text;
        public readonly int Points;

        public PenaltyResult(string text, int points)
        {
            Text = text;
            Points = points;
        }
    }

    /// <summary>한 판의 결과 요약(불변).</summary>
    public sealed class EvacuationReport
    {
        public IReadOnlyList<RuleResult> Rules { get; }
        public IReadOnlyList<PenaltyResult> Penalties { get; }
        public float ElapsedSeconds { get; }
        public ClearKind ClearKind { get; }

        public EvacuationReport(IReadOnlyList<RuleResult> rules, IReadOnlyList<PenaltyResult> penalties,
            float elapsedSeconds, ClearKind clearKind)
        {
            Rules = rules;
            Penalties = penalties;
            ElapsedSeconds = elapsedSeconds;
            ClearKind = clearKind;
        }
    }

    /// <summary>
    /// 플레이 중 기록(조치, 몸 보호 비율, 엘리베이터, 부상, 이웃, 연락)을 모아 결과를 만든다.
    /// 문구는 행정안전부 「지진 국민행동요령」의 내용을 기준으로 한다.
    /// </summary>
    public sealed class EvacuationRecord : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private SafetyChecklist checklist;
        [SerializeField] private CoverTracker cover;
        [SerializeField] private ElevatorCall elevator;
        [SerializeField] private GlassInjury injury;
        [SerializeField] private InjuredNeighbor neighbor;
        [SerializeField] private PhoneAction phone;
        [SerializeField] private OutcomeEvaluator evaluator;
        [SerializeField] private ZoneSensor zones;
        [SerializeField, Range(0f, 1f)] private float coverThreshold = 0.7f;
        [Tooltip("대피지역 덕분에 넘긴 사고 하나당 깎는 점수")]
        [SerializeField, Min(0)] private int protectedPenalty = 15;

        private float startTime;
        private float endTime = -1f;

        /// <summary>지진해일 경보 전에 옥외대피장소(학교 운동장)에 도착했는지.</summary>
        public bool ReachedOutdoorSite { get; private set; }

        private void Awake()
        {
            if (director == null || checklist == null || cover == null || elevator == null
                || injury == null || neighbor == null || phone == null || evaluator == null || zones == null)
            {
                Debug.LogError("[EvacuationRecord] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhase;
            if (evaluator != null) evaluator.Decided += OnDecided;
            if (zones != null) zones.AreaChanged += OnArea;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
            if (evaluator != null) evaluator.Decided -= OnDecided;
            if (zones != null) zones.AreaChanged -= OnArea;
        }

        private void OnArea(PlayerArea area)
        {
            if (area == PlayerArea.OutdoorSite && director.Phase < QuakePhase.Tsunami) ReachedOutdoorSite = true;
        }

        // 판정 순간을 대피 종료 시각으로 기록한다(결과 화면 대기 시간은 제외)
        private void OnDecided(DeathCause cause)
        {
            if (endTime < 0f) endTime = Time.time;
        }

        private void OnPhase(QuakePhase phase)
        {
            if (phase == QuakePhase.Foreshock) startTime = Time.time;
        }

        public EvacuationReport CreateReport()
        {
            if (!enabled) return new EvacuationReport(new List<RuleResult>(), new List<PenaltyResult>(), 0f, ClearKind.None);

            var rules = new List<RuleResult>
            {
                Rule("흔들릴 때는 탁자 아래로 들어가 몸을 보호한다 (첫 흔들림)", Covered(QuakePhase.Foreshock), 10),
                Rule("흔들림이 멈추면 가스 밸브를 잠근다", checklist.IsDone(SafetyChecklist.GasValve), 10),
                Rule("흔들림이 멈추면 전기 차단기를 내린다", checklist.IsDone(SafetyChecklist.Breaker), 10),
                Rule("문을 열어 출구를 확보한다", checklist.IsDone(SafetyChecklist.Door), 10),
                Rule("깨진 유리에 다치지 않도록 신발을 신는다", checklist.IsDone(SafetyChecklist.Shoes) && !injury.IsInjured, 5),
                Rule("엘리베이터 대신 계단을 이용한다", !elevator.WasCalled, 10),
                Rule("큰 흔들림이 오면 그 자리에서 자세를 낮추고 몸을 보호한다 (본진)", Covered(QuakePhase.MainShock), 10),
                Rule("건물 밖에서는 건물과 떨어진 넓은 곳으로 대피한다 (여진)", Covered(QuakePhase.Aftershock), 10),
                Rule("가까운 지진 옥외대피장소(학교 운동장 등)로 이동한다", ReachedOutdoorSite, 10),
                Rule("지진해일 경보 시 해안에서 먼 높은 곳으로 대피한다", evaluator.ClearKind == ClearKind.Shelter, 15),
                Rule("대피 중에는 통화를 자제한다", phone.CallsDuringEvacuation == 0, 5),
                Bonus("비상 가방을 챙긴다", checklist.IsDone(SafetyChecklist.EmergencyBag), 5),
                Bonus("다친 이웃과 함께 대피한다", neighbor.IsFollowing, 10),
                Bonus("대피 후 가족에게 문자로 안부를 알린다", phone.FamilyContacted, 5),
            };
            float end = endTime >= 0f ? endTime : Time.time;
            float elapsed = startTime > 0f ? end - startTime : 0f;
            return new EvacuationReport(rules, Penalties(), elapsed, evaluator.ClearKind);
        }

        private List<PenaltyResult> Penalties()
        {
            var list = new List<PenaltyResult>();
            foreach (DeathCause cause in evaluator.PreventedDeaths)
            {
                string title = DeathCatalog.Get(cause).Title;
                list.Add(new PenaltyResult($"대피지역이 아니었다면: {title}", protectedPenalty));
            }
            return list;
        }

        private bool Covered(QuakePhase phase) => cover.RatioOf(phase) >= coverThreshold;

        private static RuleResult Rule(string text, bool kept, int points) => new RuleResult(text, kept, points, false);

        private static RuleResult Bonus(string text, bool kept, int points) => new RuleResult(text, kept, points, true);
    }
}
