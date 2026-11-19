using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    public enum YoungManState
    {
        Hidden = 0,
        Waiting = 1,     // 복도에서 허둥대며 대기
        Running = 2,     // 수건 없이 비상계단으로 달려가는 중
        Following = 3,   // 설득/수건을 받고 플레이어와 함께 대피
        Stayed = 4,      // 설득됐지만 그 자리에서 기다림
        Lost = 5,        // 계단으로 내려가다 연기에 쓰러짐 (비그래픽, 화면 밖)
        Rescued = 6,
        Descending = 7,  // 계단으로 들어가 화면 밖으로 내려가는 중 (잠시 뒤 쓰러지는 소리)
    }

    /// <summary>
    /// 1002호 청년 (NPC_YoungMan, id: npc_youngman). 복도에 플레이어가 나오면 일정 시간 뒤 수건 없이 계단으로 달려간다.
    /// 그 전에 말을 걸어 설득하거나 젖은 수건을 건네면 함께 대피한다. 방치하면 계단 방화문을 열어 둔 채 내려가
    /// 연기에 쓰러진다 (구조 실패, 점수 감점, 비그래픽 — 자막만). 열린 방화문은 복도 연기를 가속시킨다.
    /// </summary>
    public class YoungManNpc : NpcBase
    {
        [Header("청년")]
        [SerializeField, Tooltip("달려가는 목적지 (비상계단 방화문 앞)")] private Transform stairRunTarget;
        [SerializeField, Tooltip("청년이 열어 두는 계단 방화문")] private ToggleInteractable stairDoor;
        [SerializeField] private string[] hurryLines = { "계단으로 내려가야 해!", "여기 있다간 다 죽어요!", "비켜요, 먼저 갈게요!" };
        [SerializeField] private string persuadedLine = "…수건이 있어야 한다고요? 알겠어요, 같이 가요.";
        [SerializeField] private string towelLine = "고마워요! 이걸 쓰면 되는 거죠?";
        [SerializeField] private string waitLine = "빨리요! 시간이 없어요!";
        [SerializeField] private string lostMessage = "계단 아래쪽에서 누군가 쓰러지는 소리가 들렸다. 연기를 막을 수건이 없었다.";

        private float runStartAt;
        private float fallAt = -1f;
        private bool begun;

        public static YoungManNpc Instance { get; private set; }
        public YoungManState State { get; private set; } = YoungManState.Hidden;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            State = YoungManState.Hidden;
            SetVisible(false);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>플레이어가 복도에 나왔을 때 호출 — 허둥대다가 runDelay 뒤에 계단으로 달려간다</summary>
        public void BeginWaiting()
        {
            if (begun) return;
            begun = true;
            Show();
            State = YoungManState.Waiting;
            runStartAt = Time.time + T.youngManRunDelay;
            Speak("불… 불이 났어요! 어떻게 해요?!", 3.5f);
        }

        public void ForceState(YoungManState s)
        {
            if (!begun) { begun = true; Show(); }
            State = s;
            IsFollowing = s == YoungManState.Following;
            if (s == YoungManState.Following) ScenarioFlags.Set(EvacKeys.FlagYoungManSaved, true);
        }

        public void MarkRescued()
        {
            if (State == YoungManState.Lost) return;
            State = YoungManState.Rescued;
            ScenarioFlags.Set(EvacKeys.FlagYoungManSaved, true);
            MarkNeighborRescued();
        }

        private void Show()
        {
            SetVisible(true);
            if (motor != null) motor.enabled = true;
            Interactable = true;
            IsGone = false;
        }

        private void Update()
        {
            if (GameFlow.GameplayBlocked) return;
            float dt = Time.deltaTime;

            switch (State)
            {
                case YoungManState.Waiting:
                    FaceToward(PlayerPos);
                    ApplyMove(Vector3.zero);
                    if (Time.time >= runStartAt)
                    {
                        State = YoungManState.Running;
                        Speak(hurryLines[Random.Range(0, hurryLines.Length)], 3f);
                    }
                    break;

                case YoungManState.Running:
                    Vector3 target = stairRunTarget != null ? stairRunTarget.position : transform.position;
                    if (MoveToward(target, T.youngManRunSpeed, 0.6f)) RunIntoStairs();
                    break;

                case YoungManState.Following:
                    UpdateFollow(T.youngManRunSpeed * 0.5f, waitLine);
                    if (UpdateBreathing(dt)) Fall();
                    break;

                case YoungManState.Stayed:
                    FaceToward(PlayerPos);
                    ApplyMove(Vector3.zero);
                    break;
            }

            if (fallAt > 0f && Time.time >= fallAt) { fallAt = -1f; Fall(); }
        }

        private void RunIntoStairs()
        {
            // 방화문을 열어 둔 채 계단으로 내려간다 → 복도 연기가 빨라진다. 잠시 뒤 쓰러지는 소리가 들린다.
            State = YoungManState.Descending;   // 한 번만 실행되도록 상태를 바꾼다
            if (stairDoor != null && !stairDoor.IsOn) stairDoor.SetState(true);
            Vanish();
            fallAt = Time.time + T.youngManFallSeconds;
        }

        private void Fall()
        {
            if (State == YoungManState.Lost) return;
            State = YoungManState.Lost;
            IsFollowing = false;
            ScenarioFlags.Set(EvacKeys.FlagYoungManDead, true);
            ScenarioFlags.Set(EvacKeys.FlagYoungManSaved, false);
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_youngman_lost");
            EventBus.ShowMessage(lostMessage);
            Vanish();
        }

        public override string GetPrompt() => State == YoungManState.Following ? "청년과 이야기하기" : "청년에게 말 걸기";

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor)) return false;
            return State == YoungManState.Waiting || State == YoungManState.Running || State == YoungManState.Following || State == YoungManState.Stayed;
        }

        protected override void OpenDialogue(ChoicePanel panel)
        {
            var options = new List<ChoicePanel.Option>();
            string body;
            if (State == YoungManState.Waiting || State == YoungManState.Running)
            {
                body = "청년이 숨을 헐떡이며 계단 쪽을 보고 있다. 수건도 없이 뛰어갈 기세다.";
                options.Add(new ChoicePanel.Option("수건 없이 계단은 위험해요. 같이 가요", Persuade));
                if (PlayerHasWetTowel) options.Add(new ChoicePanel.Option("젖은 수건 나눠주기", GiveTowel));
            }
            else
            {
                body = "청년이 당신을 따라오고 있다.";
                options.Add(new ChoicePanel.Option("여기서 기다려요", () => { State = YoungManState.Stayed; IsFollowing = false; }));
                if (!HasTowel && PlayerHasWetTowel) options.Add(new ChoicePanel.Option("젖은 수건 나눠주기", GiveTowel));
                if (State == YoungManState.Stayed) options.Add(new ChoicePanel.Option("같이 가요", () => { State = YoungManState.Following; IsFollowing = true; }));
            }
            panel.Show("1002호 청년", body, options);
        }

        private void Persuade()
        {
            State = YoungManState.Following;
            IsFollowing = true;
            fallAt = -1f;
            ScenarioFlags.Set(EvacKeys.FlagYoungManSaved, true);
            MarkNeighborRescued();
            Speak(persuadedLine, T.npcPersuadeSeconds + 1.5f);
            if (ScoreSystem.Instance != null) ScoreSystem.Instance.Increment("neighbor_youngman_persuaded");
        }

        private void GiveTowel()
        {
            if (TryGiveWetTowel()) Speak(towelLine, 3.5f);
            if (State != YoungManState.Following) Persuade();
        }
    }
}

