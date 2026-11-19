using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>할머니 NPC 상태</summary>
    public enum GrandmaState
    {
        Hidden = 0,           // 아직 등장 전
        PressingElevator = 1, // 엘리베이터 호출 버튼을 반복해서 누르는 중 (기침)
        Persuaded = 2,        // 설득 성공 — 그 자리에서 동행을 기다린다
        FollowingPlayer = 3,  // 플레이어 뒤를 따라 이동 (느리게)
        Panic = 4,            // 방치되어 엘리베이터에 타려고 한다
        Fallen = 5,           // (루트 B) 1003호 안에서 연기에 쓰러져 호흡이 곤란하다
        Rescued = 6,          // 구출 완료 (엔딩 직전)
        Dead = 7,             // 엘리베이터에 탑승했거나 연기로 쓰러짐 (비그래픽: 화면 밖 / 자막)
    }

    /// <summary>
    /// 1003호 할머니 (NPC_Grandmother, 상호작용 id: npc_grandmother).
    /// 상태 머신: PressingElevator → Persuaded → FollowingPlayer / Panic → Dead / Rescued  (+ 루트 B 의 Fallen).
    /// 말을 걸면 선택지가 뜬다: "엘리베이터는 위험해요. 계단으로 가요"(설득), "젖은 수건 건네기", "함께 가요"(동행) 등.
    /// 위험한 선택(엘리베이터 버튼을 같이 눌러 보기)도 막지 않고 ElevatorHazardSystem 의 결과로 이어진다.
    /// 동행 중이거나 구출된 상태는 ScenarioFlags[FlagGrandmotherWith] 로 기록되어 히든 엔딩 판정에 쓰인다.
    /// </summary>
    public class GrandmotherNpc : NpcBase
    {
        [Header("할머니")]
        [SerializeField, Tooltip("호출 버튼을 누르는 팔 (위아래로 움직인다). 선택")] private Transform arm;
        [SerializeField, Tooltip("엘리베이터 문 앞 위치 (Panic 때 걸어가는 목적지). 비우면 현재 위치")] private Transform elevatorStandPoint;
        [SerializeField] private AudioSource buttonAudio;

        [Header("대사")]
        [SerializeField] private string[] pressLines = { "왜 이렇게 안 오지… 엘리베이터가 안 와요.", "(딸깍, 딸깍) 아이고, 불이 났다는데 왜 안 오는 거야.", "콜록, 콜록… 빨리 내려가야 하는데." };
        [SerializeField] private string persuadedLine = "계단으로요? …알았어요. 같이 가요.";
        [SerializeField] private string towelThanksLine = "이걸 입에 대라고요? 고마워요, 젊은이.";
        [SerializeField] private string waitLine = "기다려줘요… 난 빨리 못 걸어요.";
        [SerializeField] private string panicLine = "안 되겠어, 저거라도 타야지!";
        [SerializeField] private string boardedMessage = "엘리베이터 문이 닫혔다. 잠시 뒤 기계가 멈추는 소리가 들렸다…";
        [SerializeField] private string fallenLine = "(숨이 가쁜 소리) 도와… 줘요…";
        [SerializeField] private string liftedLine = "고마워요… 여기서 나가요.";
        [SerializeField] private string collapsedMessage = "할머니가 연기에 의식을 잃고 쓰러졌다.";

        private float stateTime;
        private float nextPressAt;
        private float panicDeadline;
        private bool begun;

        public static GrandmotherNpc Instance { get; private set; }
        public GrandmaState State { get; private set; } = GrandmaState.Hidden;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            SetState(GrandmaState.Hidden);
            SetVisible(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ═════════════════════ 외부 제어 (Act 3 / Act 4 / 디버그) ═════════════════════
        /// <summary>복도 엘리베이터 앞에서 호출 버튼을 반복해서 누르기 시작한다 (Act 3: 플레이어가 복도에 나왔을 때)</summary>
        public void BeginAtElevator()
        {
            if (begun) return;
            begun = true;
            Show();
            SetState(GrandmaState.PressingElevator);
            panicDeadline = Time.time + T.grandmotherPanicSeconds;
            nextPressAt = Time.time + 1f;
        }

        /// <summary>루트 B: 1003호 안에 쓰러져 있는 상태로 시작한다 (연기 속 호흡 곤란)</summary>
        public void BeginFallen(Vector3 position, float yaw)
        {
            if (begun && State != GrandmaState.Hidden) return;
            begun = true;
            Show();
            Warp(position, yaw);
            SetState(GrandmaState.Fallen);
            Health01 = 1f;
            if (visualRoot != null) visualRoot.localRotation = Quaternion.Euler(0f, 0f, 88f);   // 누운 자세
            Speak(fallenLine, 4f);
        }

        /// <summary>디버그/Act 가 상태를 강제로 바꾼다 (동행 / 방치 등)</summary>
        public void ForceState(GrandmaState state)
        {
            if (!begun) { begun = true; Show(); }
            if (state == GrandmaState.Fallen && visualRoot != null) visualRoot.localRotation = Quaternion.Euler(0f, 0f, 88f);
            else if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
            SetState(state);
        }

        public void MarkRescued()
        {
            if (State == GrandmaState.Dead) return;
            SetState(GrandmaState.Rescued);
            ScenarioFlags.Set(EvacKeys.FlagGrandmotherSaved, true);
            MarkNeighborRescued();
        }

        private void Show()
        {
            SetVisible(true);
            if (motor != null) motor.enabled = true;
            Interactable = true;
            IsGone = false;
        }

        private void SetState(GrandmaState s)
        {
            State = s;
            stateTime = 0f;
            IsFollowing = s == GrandmaState.FollowingPlayer;
            bool with = s == GrandmaState.FollowingPlayer || s == GrandmaState.Rescued;
            ScenarioFlags.Set(EvacKeys.FlagGrandmotherWith, with);   // 히든 엔딩 조건: 동행 중이거나 구출 완료
            if (s == GrandmaState.Dead) ScenarioFlags.Set(EvacKeys.FlagGrandmotherDead, true);
        }

        // ═════════════════════ 업데이트 ═════════════════════
        private void Update()
        {
            if (GameFlow.GameplayBlocked) return;
            float dt = Time.deltaTime;
            stateTime += dt;

            switch (State)
            {
                case GrandmaState.PressingElevator:
                    UpdatePressing();
                    if (UpdateBreathing(dt)) Collapse();
                    break;

                case GrandmaState.Persuaded:
                    FaceToward(PlayerPos);
                    ApplyMove(Vector3.zero);
                    if (UpdateBreathing(dt)) Collapse();
                    break;

                case GrandmaState.FollowingPlayer:
                    UpdateFollow(T.grandmotherFollowSpeed, waitLine);
                    if (UpdateBreathing(dt)) Collapse();
                    break;

                case GrandmaState.Panic:
                    UpdatePanic();
                    break;

                case GrandmaState.Fallen:
                    ApplyMove(Vector3.zero);
                    // 구출되지 않으면 일정 시간 뒤 의식을 잃는다 (연기 + 시간)
                    Health01 = Mathf.Max(0f, 1f - stateTime / Mathf.Max(1f, T.grandmotherFallenSeconds));
                    if (Health01 <= 0f) Collapse();
                    break;
            }
        }

        private void UpdatePressing()
        {
            if (elevatorStandPoint != null) FaceToward(elevatorStandPoint.position);
            ApplyMove(Vector3.zero);
            if (arm != null)
            {
                float k = Mathf.Max(0f, Mathf.Sin(Time.time * 6f));
                arm.localPosition = new Vector3(arm.localPosition.x, arm.localPosition.y, 0.18f + 0.12f * k);
            }
            if (Time.time >= nextPressAt)
            {
                nextPressAt = Time.time + Mathf.Max(0.5f, T.grandmotherPressInterval);
                if (buttonAudio != null && buttonAudio.clip != null) buttonAudio.Play();
                if (pressLines != null && pressLines.Length > 0) Speak(pressLines[Random.Range(0, pressLines.Length)], 3.5f);
            }
            // 방치: 일정 시간 안에 말을 걸지 않으면 엘리베이터에 타려고 한다
            if (Time.time >= panicDeadline)
            {
                SetState(GrandmaState.Panic);
                panicDeadline = Time.time + T.grandmotherBoardSeconds;
                Speak(panicLine, 3.5f);
                EventBus.ShowScent("할머니가 엘리베이터 쪽으로 걸어간다!");
            }
        }

        private void UpdatePanic()
        {
            Vector3 target = elevatorStandPoint != null ? elevatorStandPoint.position : transform.position;
            MoveToward(target, 1.2f, 0.4f);
            if (Time.time < panicDeadline) return;

            // 엘리베이터 탑승 → 화재 중 엘리베이터는 승강로 연기로 멈춘다 (비그래픽: 문이 닫히고 화면 밖)
            EventBus.ShowMessage(boardedMessage);
            if (ScoreSystem.Instance != null) { ScoreSystem.Instance.Increment("neighbor_grandmother_elevator"); ScoreSystem.Instance.Increment("risk_attempts_total"); }
            Debug.Log("[Risk] grandmother boarded the elevator during fire");
            SetState(GrandmaState.Dead);
            Vanish();
        }

        public override void ElevatorVictim()
        {
            if (State == GrandmaState.Dead || State == GrandmaState.Rescued) return;
            SetState(GrandmaState.Dead);
            base.ElevatorVictim();
        }

        private void Collapse()
        {
            if (State == GrandmaState.Dead) return;
            EventBus.ShowMessage(collapsedMessage);
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_grandmother_collapsed");
            SetState(GrandmaState.Dead);
            if (visualRoot != null) visualRoot.localRotation = Quaternion.Euler(0f, 0f, 88f);
            Interactable = false;
            IsFollowing = false;
        }

        // ═════════════════════ 대화 ═════════════════════
        public override float HoldSeconds => State == GrandmaState.Fallen ? T.npcLiftHoldSeconds : 0f;

        public override string GetPrompt()
        {
            switch (State)
            {
                case GrandmaState.Fallen: return "할머니 부축해서 일으키기";
                case GrandmaState.FollowingPlayer: return "할머니와 이야기하기";
                default: return "할머니에게 말 걸기";
            }
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor)) return false;
            return State != GrandmaState.Hidden && State != GrandmaState.Dead && State != GrandmaState.Rescued;
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (State == GrandmaState.Fallen)
            {
                if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
                SetState(GrandmaState.FollowingPlayer);
                ScenarioFlags.Set(EvacKeys.FlagGrandmotherSaved, true);
                MarkNeighborRescued();
                Speak(liftedLine, 4f);
                if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_grandmother_lifted");
                Publish(ScenarioEventType.Interacted);
                return;
            }
            base.OnInteract(interactor);
        }

        protected override void OpenDialogue(ChoicePanel panel)
        {
            var options = new List<ChoicePanel.Option>();
            string body;

            switch (State)
            {
                case GrandmaState.PressingElevator:
                case GrandmaState.Panic:
                    body = "할머니가 엘리베이터 버튼을 계속 누르고 있다. 기침이 심하다.";
                    options.Add(new ChoicePanel.Option("엘리베이터는 위험해요. 계단으로 가요", Persuade));
                    if (PlayerHasWetTowel) options.Add(new ChoicePanel.Option("젖은 수건으로 입과 코를 막으세요 (수건 건네기)", GiveTowelAndPersuade));
                    options.Add(new ChoicePanel.Option("같이 엘리베이터 버튼을 눌러 본다", PressElevatorTogether));
                    break;

                case GrandmaState.Persuaded:
                    body = "할머니가 불안한 얼굴로 당신을 본다.";
                    options.Add(new ChoicePanel.Option("저와 함께 가요 (동행)", StartFollowing));
                    if (!HasTowel && PlayerHasWetTowel) options.Add(new ChoicePanel.Option("젖은 수건 건네기", () => { if (TryGiveWetTowel()) Speak(towelThanksLine, 3.5f); }));
                    break;

                default: // FollowingPlayer
                    body = "할머니가 당신을 따라오고 있다.";
                    options.Add(new ChoicePanel.Option("여기서 잠시 기다려 주세요", () => { SetState(GrandmaState.Persuaded); }));
                    if (!HasTowel && PlayerHasWetTowel) options.Add(new ChoicePanel.Option("젖은 수건 건네기", () => { if (TryGiveWetTowel()) Speak(towelThanksLine, 3.5f); }));
                    break;
            }
            panel.Show("1003호 할머니", body, options);
        }

        private void Persuade()
        {
            SetState(GrandmaState.Persuaded);
            Speak(persuadedLine, T.npcPersuadeSeconds + 1.5f);
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_grandmother_persuaded");
        }

        private void GiveTowelAndPersuade()
        {
            if (TryGiveWetTowel()) Speak(towelThanksLine, T.npcPersuadeSeconds + 1.5f);
            Persuade();
        }

        private void StartFollowing()
        {
            SetState(GrandmaState.FollowingPlayer);
            ScenarioFlags.Set(EvacKeys.FlagGrandmotherSaved, true);
            MarkNeighborRescued();
            EventBus.ShowMessage("할머니가 당신을 따라온다. 걸음이 느리니 보폭을 맞추자.");
        }

        private void PressElevatorTogether()
        {
            // 위험한 선택도 막지 않는다 — ElevatorHazardSystem 이 화재 중 결과(화상·무반응·화염·질식)를 굴린다
            ElevatorHazardSystem.GetOrCreate().CallElevator(transform);
        }
    }
}

