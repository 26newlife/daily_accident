using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 시나리오 3(아파트 화재)의 엘리베이터 위험 규칙 — 집의 ElevatorController 에 IElevatorHazard 로 끼워 넣는다.
    /// 화재 후(Act 2 폭발·정전 이후) 호출 첫 순간에 시나리오 A/B 를 한 번 정한다 (같은 판에서는 재호출해도 동일, 재시작하면 다시 랜덤).
    ///   A: 정상처럼 도착 → 탑승 → 층 선택(또는 idleTimeout) → "펑" → 급정지 → 연기 유입 → 질식사 (ElevatorStallSmoke)
    ///   B: 도착 징조(문틈 연기) → 문이 열리는 순간 폭발. 문 앞 blastRadius 안이면 즉사 (ElevatorFireball), 밖이면 불·연기 번짐
    /// 화재 판정: "blackout" 플래그 또는 불이 붙은 FireSource. 모든 수치는 ElevatorHazardConfig.
    /// </summary>
    public class ElevatorHazardSystem : MonoBehaviour, IElevatorHazard
    {
        /// <summary>Act 2 폭발 때 true 로 설정되는 "정전/화재 중" 플래그</summary>
        public const string FireModeFlag = "blackout";
        public const string FireStartTimeKey = "blackout_time";

        [SerializeField] private ElevatorHazardConfig config;
        [SerializeField] private ElevatorFireballEffect fireball;
        [SerializeField, Tooltip("도착 징조: 문틈으로 새는 작은 연기")] private ParticleSystem leakSmoke;
        [SerializeField, Tooltip("시나리오 B 폭발 후 복도에 붙는 불")] private FireSource blastFire;
        [SerializeField, Tooltip("내부 구역 (격리, 연기 유입 연출/호흡기)")] private HazardZone cabinZone;
        [SerializeField] private AudioSource popAudio;
        [SerializeField] private AudioSource smokeAudio;
        [SerializeField] private AudioSource coughAudio;

        public static ElevatorHazardSystem Instance { get; private set; }
        public ElevatorHazardConfig Config => config;
        public ElevatorScenario Scenario { get; private set; } = ElevatorScenario.None;

        private ElevatorController ctl;
        private float smokeT, redTimer;
        private bool towelExtended;

        private ElevatorHazardConfig Cfg
        {
            get
            {
                if (config == null) { config = ScriptableObject.CreateInstance<ElevatorHazardConfig>(); config.hideFlags = HideFlags.HideAndDontSave; }
                return config;
            }
        }

        public static bool FireMode
        {
            get
            {
                if (ScenarioFlags.GetBool(FireModeFlag)) return true;
                foreach (FireSource f in FireSource.All) if (f != null && f.IsLit) return true;
                return false;
            }
        }

        public bool Dangerous => FireMode;
        public static void ResetRunState() { Instance = null; }

        private void Awake() { Instance = this; }
        private void OnEnable()
        {
            RunCleanup.Register(ResetRunState);
            ctl = ElevatorController.Instance != null ? ElevatorController.Instance : FindFirstObjectByType<ElevatorController>();
            if (ctl != null) ctl.Hazard = this;
        }
        private void OnDisable() { if (ctl != null && ctl.Hazard == (IElevatorHazard)this) ctl.Hazard = null; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>디버그: 다음 화재 후 호출의 시나리오를 강제</summary>
        public void DebugForceScenario(ElevatorScenario s) { Scenario = s; Debug.Log($"[Risk] Elevator scenario forced = {s}"); }

        // ═════════════════════ IElevatorHazard ═════════════════════
        public void OnCalled(ElevatorController c)
        {
            if (!FireMode) return;
            if (Scenario == ElevatorScenario.None)
            {
                Scenario = ElevatorScenarioSelector.Roll(Cfg);
                Debug.Log($"[Risk] Elevator scenario = {Scenario}");
                Record("risk_elevator_scenario", (float)Scenario);
            }
            if (Scenario == ElevatorScenario.B && leakSmoke != null) leakSmoke.Play();   // 눈치챌 수 있는 징조
        }

        public bool OnArrived(ElevatorController c)
        {
            if (leakSmoke != null) leakSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (!FireMode) return false;
            if (Scenario == ElevatorScenario.B) { Blast(c); return true; }
            if (Scenario == ElevatorScenario.A && c.Cabin != null) c.Cabin.SetFlicker(true);   // 미세한 이상만
            return false;
        }

        public bool OnBoardAttempt(ElevatorController c)
        {
            if (c.State != ElevatorState.Burned) return false;
            Die(DeathReason.ElevatorFireball);   // 불타는 문 안으로 들어가면
            return true;
        }

        public void OnBoarded(ElevatorController c)
        {
            // 탑승만 하고 층을 고르지 않아도 막히지 않고 결과로 이어진다
            if (FireMode && Scenario == ElevatorScenario.A) c.After(Cfg.idleTimeout, () => { if (c.State == ElevatorState.Boarded) StartStall(c); });
        }

        public void OnMoveStarted(ElevatorController c, float trip)
        {
            if (!FireMode || Scenario != ElevatorScenario.A) return;
            float popAt = Mathf.Min(Cfg.popDelay, Mathf.Max(0.6f, trip * 0.6f));   // 이동 구간의 중간쯤
            c.After(c.Config.doorMoveSeconds + popAt, () => { if (c.State == ElevatorState.Moving) StartStall(c); });
        }

        public bool OnTripEnd(ElevatorController c)
        {
            if (!FireMode || Scenario != ElevatorScenario.A) return false;
            StartStall(c);   // 화재 후에는 목적지에 도착하지 못한다
            return true;
        }

        // ═════════════════════ 시나리오 B ═════════════════════
        private void Blast(ElevatorController c)
        {
            ElevatorHazardConfig cfg = Cfg;
            if (fireball != null) fireball.Play();
            ElevatorController.Play(popAudio);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.6f, 0.15f), 1f, 1.6f);
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) player.Shake(cfg.blastShakeAmplitude, 1.0f);

            Vector3 door = c.ExteriorDoor != null ? c.ExteriorDoor.transform.position : transform.position;
            float dist = player != null ? FlatDistance(player.transform.position, door) : 99f;
            Debug.Log($"[Risk] Elevator blast: player distance {dist:0.0}m (radius {cfg.blastRadius:0.0}m)");
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("risk_attempts_total");

            // 근처 NPC 도 함께 피해 (구조 실패·감점 기록)
            foreach (NpcBase npc in NpcBase.All)
                if (npc != null && !npc.IsGone && FlatDistance(npc.transform.position, door) <= cfg.blastRadius * cfg.blastNpcRadiusMultiplier) npc.ElevatorVictim();

            if (dist <= cfg.blastRadius) { c.After(cfg.blastDeathDelay, () => Die(DeathReason.ElevatorFireball)); return; }
            // 반경 밖: 즉사는 아니지만 불이 복도로 번지고 연기가 급증한다
            if (blastFire != null && !blastFire.IsLit) blastFire.Ignite(cfg.blastFireIntensity);
            PlayerVitals v = player != null ? player.GetComponent<PlayerVitals>() : null;
            if (v != null)
            {
                if (dist <= cfg.blastRadius * 2f) v.AddBurn(cfg.blastBurnAmount);
                v.Modify(-cfg.blastBreathLoss);
            }
        }

        // ═════════════════════ 시나리오 A: 정지 → 연기 → 질식 ═════════════════════
        private void StartStall(ElevatorController c)
        {
            c.NewGeneration();
            c.Go(ElevatorState.Stalled);
            ElevatorHazardConfig cfg = Cfg;
            if (c.MotorAudio != null) c.MotorAudio.Stop();
            ElevatorController.Play(popAudio);
            if (c.Cabin != null) c.Cabin.SetPower(false);   // 층 표시 깜빡임 → 꺼짐, 조명 꺼짐, 붉은 비상등
            ElevatorController.PlayerShake(cfg.popShakeAmplitude, cfg.popShakeSeconds);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.8f, 0.5f), 0.7f, 0.5f);
            Debug.Log("[Risk] Elevator stalled (pop)");
            c.After(cfg.smokeDelay, () => StartSmoke(c));
        }

        private void StartSmoke(ElevatorController c)
        {
            c.NewGeneration();
            c.Go(ElevatorState.Smoke);
            smokeT = 0f; redTimer = 0f; towelExtended = false;
            if (c.Cabin != null) c.Cabin.SetSmoke(true);
            if (smokeAudio != null && smokeAudio.clip != null) smokeAudio.Play();
            c.After(Cfg.suffocationTime, () => KillBySmoke(c));
        }

        private void KillBySmoke(ElevatorController c)
        {
            InventorySystem inv = InventorySystem.Instance;
            ItemData towel = inv != null ? inv.Find(CoreKeys.ItemWetTowel) : null;
            bool worn = towel != null && inv.IsEquipped(towel);
            if (worn && !towelExtended)
            {
                towelExtended = true;   // 수건은 조금 늦출 뿐 근본 해결이 아니다
                c.After(Cfg.suffocationTime * Mathf.Max(0f, Cfg.towelSuffocationMultiplier - 1f), () => KillBySmoke(c));
                return;
            }
            Die(DeathReason.ElevatorStallSmoke);
        }

        private void Update()
        {
            if (GameFlow.IsEnded || GameFlow.IsPaused || ctl == null || ctl.State != ElevatorState.Smoke) return;
            ElevatorHazardConfig cfg = Cfg;
            smokeT += Time.deltaTime;
            if (cabinZone != null) cabinZone.SetSmoke(Mathf.Clamp01(smokeT / Mathf.Max(1f, cfg.suffocationTime * 0.5f)));
            redTimer -= Time.deltaTime;
            if (redTimer <= 0f)
            {
                redTimer = 2.2f;
                if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(0.7f, 0.05f, 0.03f), cfg.smokeRedFlashAlpha, 1.6f);
                if (coughAudio != null && coughAudio.clip != null) coughAudio.Play();
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        private static void Die(DeathReason reason) => EvacDirector.Die(reason);
        private static void Record(string key, float v) { if (ScoreSystem.Instance != null) ScoreSystem.Instance.Record(key, v); }
    }
}
