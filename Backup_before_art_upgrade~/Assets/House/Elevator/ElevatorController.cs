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
        Stalled = 6,    // (시나리오 훅) 급정지
        Smoke = 7,      // (시나리오 훅) 연기 유입
        Burned = 8,     // (시나리오 훅) 문 열림 순간 위험이 터진 뒤
        Arrived = 9,    // 도착 (문 열림 → 복도 복귀 직전)
    }

    /// <summary>
    /// 시나리오가 엘리베이터에 위험 동작을 끼워 넣는 확장 지점 (집 에셋은 시나리오 내용을 모른다).
    /// 등록: ElevatorController.Hazard = this. 등록하지 않으면 항상 정상 작동한다.
    /// </summary>
    public interface IElevatorHazard
    {
        /// <summary>지금 위험 모드인가 (예: 화재 후)</summary>
        bool Dangerous { get; }
        /// <summary>호출 버튼을 누른 순간 (시나리오 결정, 도착 징조 등)</summary>
        void OnCalled(ElevatorController c);
        /// <summary>문이 열린 순간. true = 위험 처리됨(컨트롤러는 Burned 상태로 둔다)</summary>
        bool OnArrived(ElevatorController c);
        /// <summary>탑승 시도. true = 시나리오가 처리함 (탑승하지 않음)</summary>
        bool OnBoardAttempt(ElevatorController c);
        /// <summary>탑승 직후 (idleTimeout 예약 등)</summary>
        void OnBoarded(ElevatorController c);
        /// <summary>층 이동 시작 직후 (정지 시점 예약 등)</summary>
        void OnMoveStarted(ElevatorController c, float tripSeconds);
        /// <summary>목적지 도착 직전. true = 도착을 가로챔(급정지 등)</summary>
        bool OnTripEnd(ElevatorController c);
    }

    /// <summary>
    /// 10층 복도 엘리베이터 상태 머신 (집 공용, 정상 작동):
    ///   호출 → 도착("딩", 문 열림) → 탑승(내부 화면으로 페이드 전환) → 층 선택 → 문 닫힘 → 층 표시 한 층씩 변경(미세 진동) → 도착 → 10층 복도 복귀.
    /// 위험한 동작은 시나리오가 Hazard(IElevatorHazard)로 끼워 넣는다. 모든 시간은 ElevatorConfig, 예약은 GameClock.ScheduleAt(RunId 로 재시작 시 무효,
    /// 일시정지 중 정지) + 세대 번호(gen)로 취소한다. 코루틴/Invoke 는 쓰지 않는다. 위험한 행동은 막지 않는다.
    /// </summary>
    public class ElevatorController : MonoBehaviour
    {
        [SerializeField] private ElevatorConfig config;
        [SerializeField] private ElevatorCabinInterior cabin;
        [SerializeField, Tooltip("복도 쪽 엘리베이터 문 (ElevatorInteractable Door 역할)")] private ElevatorInteractable exteriorDoor;
        [SerializeField, Tooltip("하차 시 돌아오는 문 앞 위치")] private Transform outsideStand;

        [Header("소리 슬롯 (클립은 비워도 동작)")]
        [SerializeField] private AudioSource dingAudio;
        [SerializeField] private AudioSource motorAudio;
        [SerializeField] private AudioSource alarmAudio;
        [SerializeField] private AudioSource buttonAudio;
        [SerializeField] private AudioSource denyAudio;

        public const int HomeFloor = 10;
        public static ElevatorController Instance { get; private set; }

        public ElevatorState State { get; private set; } = ElevatorState.Idle;
        public int CurrentFloor { get; private set; } = HomeFloor;
        public int TargetFloor { get; private set; } = HomeFloor;
        public IElevatorHazard Hazard { get; set; }
        public ElevatorCabinInterior Cabin => cabin;
        public ElevatorInteractable ExteriorDoor => exteriorDoor;
        public AudioSource MotorAudio => motorAudio;
        public ElevatorConfig Config
        {
            get
            {
                if (config == null) { config = ScriptableObject.CreateInstance<ElevatorConfig>(); config.hideFlags = HideFlags.HideAndDontSave; }
                return config;
            }
        }

        private int gen;
        private float moveAccum, shakeTimer;

        private static bool Blocked => GameFlow.IsEnded || GameFlow.IsPaused;
        private bool Dangerous => Hazard != null && Hazard.Dangerous;

        private void Awake()
        {
            Instance = this;
            if (cabin != null) cabin.SetDoorSeconds(Config.doorMoveSeconds);
        }

        private void OnEnable() { RunCleanup.Register(ResetRunState); }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static void ResetRunState() { Instance = null; }

        // ═════════════════════ 예약 (세대 번호로 취소) ═════════════════════
        /// <summary>게임 시간 기준 예약. 세대 번호가 바뀌면(NewGeneration) 이전 예약은 실행되지 않는다</summary>
        public void After(float seconds, Action action)
        {
            GameClock clock = GameClock.Instance;
            if (clock == null) return;
            int g = gen;
            clock.ScheduleAt(clock.GameSeconds + seconds, () => { if (g == gen && !GameFlow.IsEnded) action(); }, "elevator");
        }

        /// <summary>이전에 걸어 둔 모든 예약을 취소한다</summary>
        public void NewGeneration() { gen++; }
        public void Go(ElevatorState s) { State = s; }

        // ═════════════════════ 호출 ═════════════════════
        public void Call(Transform at)
        {
            if (Blocked) return;
            Count("risk_elevator_attempts");
            if (State != ElevatorState.Idle) return;   // 이미 호출됨 / 문이 열려 있음 — 버튼 연타는 무시 (막는 메시지 없음)

            gen++;
            Go(ElevatorState.Arriving);
            Play(buttonAudio);
            ElevatorInteractable.SetCallButtonsLit(true);
            Hazard?.OnCalled(this);
            After(Config.arrivalDelay, OnArrive);
        }

        private void OnArrive()
        {
            ElevatorInteractable.SetCallButtonsLit(false);
            if (exteriorDoor != null) exteriorDoor.SetOpen(true);
            Play(dingAudio);
            Go(ElevatorState.DoorOpen);

            if (Hazard != null && Hazard.OnArrived(this)) { Go(ElevatorState.Burned); return; }
            After(Config.doorOpenHoldSeconds, () => { if (State == ElevatorState.DoorOpen) CloseExterior(); });
        }

        private void CloseExterior()
        {
            if (exteriorDoor != null) exteriorDoor.SetOpen(false);
            if (cabin != null) cabin.SetFlicker(false);
            Go(ElevatorState.Idle);
        }

        // ═════════════════════ 탑승 ═════════════════════
        public void Board()
        {
            if (Blocked) return;
            if (Hazard != null && Hazard.OnBoardAttempt(this)) return;
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
                EvacDirector.Instance.Travel(cabin.PlayerSpawn.position, cabin.PlayerSpawn.eulerAngles.y, null, Config.boardFadeSeconds, arrived, false);
            else arrived();
            Hazard?.OnBoarded(this);
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
            gen++;   // 탑승 후 대기 예약 취소
            Go(ElevatorState.Moving);
            ElevatorConfig c = Config;
            if (cabin != null) cabin.SetDoorOpen(false);
            moveAccum = -c.doorMoveSeconds;   // 문이 닫힌 뒤 층 표시가 변하기 시작
            if (cabin != null) cabin.SetFloor(CurrentFloor, Math.Sign(TargetFloor - CurrentFloor));
            if (motorAudio != null && motorAudio.clip != null) motorAudio.Play();
            float trip = Mathf.Max(1, Mathf.Abs(TargetFloor - CurrentFloor)) * c.floorTravelTime;
            Hazard?.OnMoveStarted(this, trip);
        }

        private void Update()
        {
            if (Blocked || State != ElevatorState.Moving) return;
            float dt = Time.deltaTime;
            ElevatorConfig c = Config;
            moveAccum += dt;
            shakeTimer -= dt;
            if (shakeTimer <= 0f && moveAccum > 0f) { shakeTimer = 0.5f; PlayerShake(c.travelShakeAmplitude, 0.5f); }
            while (moveAccum >= c.floorTravelTime && CurrentFloor != TargetFloor)
            {
                moveAccum -= c.floorTravelTime;
                CurrentFloor += Math.Sign(TargetFloor - CurrentFloor);
                if (cabin != null) cabin.SetFloor(CurrentFloor, Math.Sign(TargetFloor - CurrentFloor));
            }
            if (CurrentFloor == TargetFloor && moveAccum >= c.floorTravelTime) OnTripEnd();
        }

        private void OnTripEnd()
        {
            if (motorAudio != null) motorAudio.Stop();
            if (Hazard != null && Hazard.OnTripEnd(this)) return;   // 시나리오가 도착을 가로챔 (급정지 등)

            // 정상: 도착 → "딩" → 문 열림 → 10층 복도로 복귀
            Go(ElevatorState.Arrived);
            Play(dingAudio);
            if (cabin != null) { cabin.SetDoorOpen(true); cabin.SetFloor(CurrentFloor, 0); cabin.Panel?.ClearLights(); }
            gen++;
            After(Config.normalExitDelay, ExitToCorridor);
        }

        public void ExitToCorridor()
        {
            gen++;
            Action back = () =>
            {
                if (cabin != null) cabin.ResetCabin();
                CurrentFloor = TargetFloor = HomeFloor;
                if (exteriorDoor != null) exteriorDoor.SetOpen(false);
                Go(ElevatorState.Idle);
            };
            Go(ElevatorState.Arrived);
            if (EvacDirector.Instance != null && outsideStand != null)
                EvacDirector.Instance.Travel(outsideStand.position, outsideStand.eulerAngles.y, null, Config.boardFadeSeconds, back, false);
            else back();
        }

        // ═════════════════════ 헬퍼 ═════════════════════
        public static void Play(AudioSource s) { if (s != null && s.clip != null) s.Play(); }
        public static void PlayerShake(float amp, float seconds) { PlayerController p = FindFirstObjectByType<PlayerController>(); if (p != null) p.Shake(amp, seconds); }
        private static void Count(string key) { if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment(key); ScoreSystem.Instance.Increment("risk_attempts_total"); } }
    }
}
