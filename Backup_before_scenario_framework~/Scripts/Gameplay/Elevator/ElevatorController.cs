using System;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    public enum ElevatorState
    {
        Idle = 0,       // 대기 (호출 전)
        Called = 1,     // 호출 버튼에 불이 들어옴
        Arriving = 2,   // 도착 대기
        DoorOpen = 3,   // 복도 쪽 문이 열려 있음
        Boarded = 4,    // 탑승해서 내부 화면
        Moving = 5,     // 이동 중 (층 표시 변화)
        Stalled = 6,    // 시나리오 A: 급정지
        Smoke = 7,      // 시나리오 A: 연기 유입
        Burned = 8,     // 시나리오 B: 폭발 후 불타는 문
        Arrived = 9,    // 평상시 도착 (문 열림 → 복도 복귀 직전)
    }

    /// <summary>
    /// 10층 복도 엘리베이터 상태 머신.
    ///   평상시(화재 전): 호출 → 도착 → 문 열림 → 탑승(내부 화면) → 층 선택 → 정상 이동 → 도착 → 10층 복도 복귀
    ///   화재 후: 호출 첫 순간에 시나리오 A/B 를 한 번 정한다 (같은 판에서는 재호출해도 동일, 재시작하면 다시 랜덤)
    ///     A: 정상처럼 도착 → 탑승 → 층 선택(또는 idleTimeout) → "펑" → 급정지 → 연기 유입 → 질식사 (ElevatorStallSmoke)
    ///     B: 도착 징조(문틈 연기) → 문이 열리는 순간 폭발. 문 앞 blastRadius 안이면 즉사 (ElevatorFireball), 밖이면 불·연기 번짐
    /// 모든 시간은 ElevatorHazardConfig, 예약은 GameClock.ScheduleAt(RunId 로 재시작 시 무효, 일시정지 중 정지) + 세대 번호(gen)로
    /// 취소한다. 코루틴/Invoke 는 쓰지 않는다. 위험한 행동은 막지 않는다.
    /// </summary>
    public class ElevatorController : MonoBehaviour
    {
        [SerializeField] private ElevatorHazardConfig config;
        [SerializeField] private ElevatorCabinInterior cabin;
        [SerializeField, Tooltip("복도 쪽 엘리베이터 문 (ElevatorInteractable Door 역할)")] private ElevatorInteractable exteriorDoor;
        [SerializeField, Tooltip("하차 시 돌아오는 문 앞 위치")] private Transform outsideStand;
        [SerializeField] private ElevatorFireballEffect fireball;
        [SerializeField, Tooltip("도착 징조: 문틈으로 새는 작은 연기")] private ParticleSystem leakSmoke;
        [SerializeField, Tooltip("시나리오 B 폭발 후 복도에 붙는 불")] private FireSource blastFire;
        [SerializeField, Tooltip("내부 구역 (격리, 연기 유입 연출/호흡기)")] private HazardZone cabinZone;

        [Header("소리 슬롯 (클립은 비워도 동작)")]
        [SerializeField] private AudioSource dingAudio;
        [SerializeField] private AudioSource motorAudio;
        [SerializeField] private AudioSource popAudio;
        [SerializeField] private AudioSource smokeAudio;
        [SerializeField] private AudioSource alarmAudio;
        [SerializeField] private AudioSource buttonAudio;
        [SerializeField] private AudioSource denyAudio;
        [SerializeField] private AudioSource coughAudio;

        public static ElevatorController Instance { get; private set; }
        public ElevatorState State { get; private set; } = ElevatorState.Idle;
        public ElevatorScenario Scenario { get; private set; } = ElevatorScenario.None;
        public int CurrentFloor { get; private set; } = HomeFloor;
        public int TargetFloor { get; private set; } = HomeFloor;
        public const int HomeFloor = 10;

        private int gen;
        private float moveAccum, smokeT, redTimer, shakeTimer;
        private bool towelExtended;
        private int direction;

        private ElevatorHazardConfig Cfg
        {
            get
            {
                if (config == null) { config = ScriptableObject.CreateInstance<ElevatorHazardConfig>(); config.hideFlags = HideFlags.HideAndDontSave; }
                return config;
            }
        }

        private static bool Blocked => GameFlow.IsEnded || GameFlow.IsPaused;
        private static bool Fire => ElevatorHazardSystem.FireMode;

        private void Awake()
        {
            Instance = this;
            if (cabin != null) cabin.SetDoorSeconds(Cfg.doorMoveSeconds);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static void ResetRunState() { Instance = null; }

        /// <summary>디버그: 다음 화재 후 호출의 시나리오를 강제 (아직 결정 전일 때만)</summary>
        public void DebugForceScenario(ElevatorScenario s) { Scenario = s; Debug.Log($"[Risk] Elevator scenario forced = {s}"); }

        // ═════════════════════ 예약 (세대 번호로 취소) ═════════════════════
        private void After(float seconds, Action action)
        {
            GameClock clock = GameClock.Instance;
            if (clock == null) return;
            int g = gen;
            clock.ScheduleAt(clock.GameSeconds + seconds, () => { if (g == gen && !GameFlow.IsEnded) action(); }, "elevator");
        }

        private void Go(ElevatorState s) { State = s; }

        // ═════════════════════ 호출 ═════════════════════
        public void Call(Transform at)
        {
            if (Blocked) return;
            Count("risk_elevator_attempts");
            if (State != ElevatorState.Idle) return;   // 이미 호출됨 / 문이 열려 있음 — 버튼 연타는 무시 (막는 메시지 없음)

            if (Fire && Scenario == ElevatorScenario.None)
            {
                Scenario = ElevatorScenarioSelector.Roll(Cfg);
                Debug.Log($"[Risk] Elevator scenario = {Scenario}");
                Record("risk_elevator_scenario", (float)Scenario);
            }

            gen++;
            Go(ElevatorState.Arriving);
            Play(buttonAudio);
            ElevatorInteractable.SetCallButtonsLit(true);
            if (Fire && Scenario == ElevatorScenario.B && leakSmoke != null) leakSmoke.Play();   // 눈치챌 수 있는 징조
            After(Cfg.arrivalDelay, OnArrive);
        }

        private void OnArrive()
        {
            ElevatorInteractable.SetCallButtonsLit(false);
            if (exteriorDoor != null) exteriorDoor.SetOpen(true);
            if (leakSmoke != null) leakSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Play(dingAudio);
            Go(ElevatorState.DoorOpen);

            if (Fire && Scenario == ElevatorScenario.B) { Blast(); return; }
            if (Fire && Scenario == ElevatorScenario.A && cabin != null) cabin.SetFlicker(true);   // 미세한 이상만
            After(Cfg.doorOpenHoldSeconds, () => { if (State == ElevatorState.DoorOpen) CloseExterior(); });
        }

        private void CloseExterior()
        {
            if (exteriorDoor != null) exteriorDoor.SetOpen(false);
            if (cabin != null) cabin.SetFlicker(false);
            Go(ElevatorState.Idle);
        }

        // ═════════════════════ 시나리오 B: 문 열림 폭발 ═════════════════════
        private void Blast()
        {
            Go(ElevatorState.Burned);
            ElevatorHazardConfig c = Cfg;
            if (fireball != null) fireball.Play();
            Play(popAudio);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.6f, 0.15f), 1f, 1.6f);
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) player.Shake(c.blastShakeAmplitude, 1.0f);

            Vector3 door = exteriorDoor != null ? exteriorDoor.transform.position : transform.position;
            float dist = player != null ? FlatDistance(player.transform.position, door) : 99f;
            Debug.Log($"[Risk] Elevator blast: player distance {dist:0.0}m (radius {c.blastRadius:0.0}m)");
            Count("risk_attempts_total");

            // 근처 NPC 도 함께 피해 (구조 실패·감점 기록)
            foreach (NpcBase npc in NpcBase.All)
                if (npc != null && !npc.IsGone && FlatDistance(npc.transform.position, door) <= c.blastRadius * c.blastNpcRadiusMultiplier) npc.ElevatorVictim();

            if (dist <= c.blastRadius)
            {
                After(c.blastDeathDelay, () => Die(DeathReason.ElevatorFireball));
                return;
            }
            // 반경 밖: 즉사는 아니지만 불이 복도로 번지고 연기가 급증한다
            if (blastFire != null && !blastFire.IsLit) blastFire.Ignite(c.blastFireIntensity);
            PlayerVitals v = player != null ? player.GetComponent<PlayerVitals>() : null;
            if (v != null)
            {
                if (dist <= c.blastRadius * 2f) v.AddBurn(c.blastBurnAmount);
                v.Modify(-c.blastBreathLoss);
            }
        }

        // ═════════════════════ 탑승 ═════════════════════
        public void Board()
        {
            if (Blocked) return;
            if (State == ElevatorState.Burned) { Die(DeathReason.ElevatorFireball); return; }   // 불타는 문 안으로 들어가면
            if (State != ElevatorState.DoorOpen) return;
            Count("risk_elevator_boarded");

            gen++;
            Go(ElevatorState.Boarded);
            TargetFloor = CurrentFloor = HomeFloor;
            moveAccum = 0f;
            Action arrived = () =>
            {
                if (exteriorDoor != null) exteriorDoor.SetOpen(false);
                if (cabin != null) { cabin.ResetCabin(); cabin.SetDoorOpen(true, true); cabin.SetFloor(CurrentFloor, 0); }
            };
            if (EvacDirector.Instance != null && cabin != null && cabin.PlayerSpawn != null)
                EvacDirector.Instance.Travel(cabin.PlayerSpawn.position, cabin.PlayerSpawn.eulerAngles.y, null, Cfg.boardFadeSeconds, arrived, false);
            else arrived();

            // 탑승만 하고 층을 고르지 않아도 막히지 않고 결과로 이어진다 (화재 후 시나리오 A)
            if (Fire && Scenario == ElevatorScenario.A) After(Cfg.idleTimeout, () => { if (State == ElevatorState.Boarded) StartStall(); });
        }

        // ═════════════════════ 내부 버튼 ═════════════════════
        public void PressButton(ElevatorButton b)
        {
            if (Blocked || b == null) return;
            switch (b.Kind)
            {
                case ElevatorButton.ButtonKind.Floor:
                    if (State == ElevatorState.Boarded) { TargetFloor = b.Floor; cabin?.Panel?.LightFloor(b.Floor); Play(buttonAudio); StartMove(); }
                    else if (State == ElevatorState.Moving) { TargetFloor = b.Floor; cabin?.Panel?.LightFloor(b.Floor); Play(buttonAudio); }   // 마지막 선택을 따른다
                    else Play(denyAudio);   // 정지/연기 중: 반응 없음 (소리만)
                    break;
                case ElevatorButton.ButtonKind.Open:
                    if (State == ElevatorState.Boarded && cabin != null) { cabin.SetDoorOpen(true); Play(buttonAudio); } else Play(denyAudio);
                    break;
                case ElevatorButton.ButtonKind.Close:
                    if (State == ElevatorState.Boarded && cabin != null) { cabin.SetDoorOpen(false); Play(buttonAudio); } else Play(denyAudio);
                    break;
                case ElevatorButton.ButtonKind.Alarm:
                    Play(alarmAudio);   // 연결 중… 소리만, 응답 없음 (안내 문구 없음)
                    cabin?.Panel?.BlinkAlarm();
                    break;
                case ElevatorButton.ButtonKind.Exit:
                    if (State == ElevatorState.Boarded && cabin != null && cabin.DoorOpen) ExitToCorridor(); else Play(denyAudio);
                    break;
            }
        }

        // ═════════════════════ 이동 ═════════════════════
        private void StartMove()
        {
            gen++;   // 탑승 후 대기(idleTimeout) 예약 취소
            Go(ElevatorState.Moving);
            ElevatorHazardConfig c = Cfg;
            if (cabin != null) cabin.SetDoorOpen(false);
            moveAccum = -c.doorMoveSeconds;   // 문이 닫힌 뒤 층 표시가 변하기 시작
            direction = Math.Sign(TargetFloor - CurrentFloor);
            if (cabin != null) cabin.SetFloor(CurrentFloor, direction);
            if (motorAudio != null && motorAudio.clip != null) motorAudio.Play();

            if (Fire && Scenario == ElevatorScenario.A)
            {
                float trip = Mathf.Max(1, Mathf.Abs(TargetFloor - CurrentFloor)) * c.floorTravelTime;
                float popAt = Mathf.Min(c.popDelay, Mathf.Max(0.6f, trip * 0.6f));   // 이동 구간의 중간쯤
                After(c.doorMoveSeconds + popAt, () => { if (State == ElevatorState.Moving) StartStall(); });
            }
        }

        private void Update()
        {
            if (Blocked) return;
            float dt = Time.deltaTime;
            ElevatorHazardConfig c = Cfg;

            if (State == ElevatorState.Moving)
            {
                moveAccum += dt;
                shakeTimer -= dt;
                if (shakeTimer <= 0f && moveAccum > 0f) { shakeTimer = 0.5f; PlayerShake(c.travelShakeAmplitude, 0.5f); }
                while (moveAccum >= c.floorTravelTime && CurrentFloor != TargetFloor)
                {
                    moveAccum -= c.floorTravelTime;
                    CurrentFloor += Math.Sign(TargetFloor - CurrentFloor);
                    direction = Math.Sign(TargetFloor - CurrentFloor);
                    if (cabin != null) cabin.SetFloor(CurrentFloor, direction);
                }
                if (CurrentFloor == TargetFloor && moveAccum >= c.floorTravelTime) OnTripEnd();
            }
            else if (State == ElevatorState.Smoke)
            {
                smokeT += dt;
                float smoke = Mathf.Clamp01(smokeT / Mathf.Max(1f, c.suffocationTime * 0.5f));
                if (cabinZone != null) cabinZone.SetSmoke(smoke);
                redTimer -= dt;
                if (redTimer <= 0f)
                {
                    redTimer = 2.2f;
                    if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(0.7f, 0.05f, 0.03f), c.smokeRedFlashAlpha, 1.6f);
                    if (coughAudio != null && coughAudio.clip != null) coughAudio.Play();
                }
            }
        }

        private void OnTripEnd()
        {
            if (motorAudio != null) motorAudio.Stop();
            if (Fire && Scenario == ElevatorScenario.A) { StartStall(); return; }   // 화재 후에는 목적지에 도착하지 못한다

            // 평상시: 도착 → "딩" → 문 열림 → 10층 복도로 복귀
            Go(ElevatorState.Arrived);
            Play(dingAudio);
            if (cabin != null) { cabin.SetDoorOpen(true); cabin.SetFloor(CurrentFloor, 0); cabin.Panel?.ClearLights(); }
            gen++;
            After(Cfg.normalExitDelay, ExitToCorridor);
        }

        private void ExitToCorridor()
        {
            gen++;
            Action back = () =>
            {
                if (cabin != null) { cabin.ResetCabin(); }
                CurrentFloor = TargetFloor = HomeFloor;
                if (exteriorDoor != null) exteriorDoor.SetOpen(false);
                Go(ElevatorState.Idle);
            };
            Go(ElevatorState.Arrived);
            if (EvacDirector.Instance != null && outsideStand != null)
                EvacDirector.Instance.Travel(outsideStand.position, outsideStand.eulerAngles.y, null, Cfg.boardFadeSeconds, back, false);
            else back();
        }

        // ═════════════════════ 시나리오 A: 정지 → 연기 → 질식 ═════════════════════
        private void StartStall()
        {
            gen++;
            Go(ElevatorState.Stalled);
            ElevatorHazardConfig c = Cfg;
            if (motorAudio != null) motorAudio.Stop();
            Play(popAudio);
            if (cabin != null) cabin.SetPower(false);   // 층 표시 깜빡임 → 꺼짐, 조명 꺼짐, 붉은 비상등
            PlayerShake(c.popShakeAmplitude, c.popShakeSeconds);
            if (ScreenFlash.Instance != null) ScreenFlash.Instance.Flash(new Color(1f, 0.8f, 0.5f), 0.7f, 0.5f);
            Debug.Log("[Risk] Elevator stalled (pop)");
            After(c.smokeDelay, StartSmoke);
        }

        private void StartSmoke()
        {
            gen++;
            Go(ElevatorState.Smoke);
            smokeT = 0f; redTimer = 0f; towelExtended = false;
            if (cabin != null) cabin.SetSmoke(true);
            if (smokeAudio != null && smokeAudio.clip != null) smokeAudio.Play();
            After(Cfg.suffocationTime, KillBySmoke);
        }

        private void KillBySmoke()
        {
            InventorySystem inv = InventorySystem.Instance;
            ItemData towel = inv != null ? inv.Find(EvacKeys.ItemWetTowel) : null;
            bool worn = towel != null && inv.IsEquipped(towel);
            if (worn && !towelExtended)
            {
                towelExtended = true;   // 수건은 조금 늦출 뿐 근본 해결이 아니다
                After(Cfg.suffocationTime * Mathf.Max(0f, Cfg.towelSuffocationMultiplier - 1f), KillBySmoke);
                return;
            }
            Die(DeathReason.ElevatorStallSmoke);
        }

        // ═════════════════════ 헬퍼 ═════════════════════
        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        private static void Play(AudioSource s) { if (s != null && s.clip != null) s.Play(); }
        private static void PlayerShake(float amp, float seconds) { PlayerController p = FindFirstObjectByType<PlayerController>(); if (p != null) p.Shake(amp, seconds); }
        private static void Die(DeathReason reason) => EvacDirector.Die(reason);
        private static void Count(string key) { if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment(key); if (key == "risk_elevator_attempts") ScoreSystem.Instance.Increment("risk_attempts_total"); } }
        private static void Record(string key, float v) { if (ScoreSystem.Instance != null) ScoreSystem.Instance.Record(key, v); }
    }
}
