using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 4. 기만된 평화 (핵심 반전 / 분기점)
    /// 수 주 뒤 라디오 긴급 속보로 "거짓 종전 선언" 이 흘러나온다. 합류한 생존자들은 환호하며 합판을 뜯고 나가려 한다.
    /// 기만 단서(DeceptionClue)를 기준 개수 이상 모은 플레이어만 방송이 함정임을 간파하고 설득할 수 있다.
    /// 플레이어가 직접 밖으로 나가는 것도 막지 않는다 — 결과(Act 5 의 섬광)로만 돌아온다.
    /// </summary>
    public class Act4_FalsePeace : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveJudge;
        [SerializeField] private WarSurvivor[] survivors;
        [SerializeField] private EnvironmentPreset dawnPreset;

        private bool survivorsLeaving, persuaded;
        private Vector3 doorPos;

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) return;
            War.SetPhase(WarPhase.Peace);
            AddObjective(objectiveJudge);
            WarOutbreak.Instance?.Calm();   // 포성과 편대 비행이 멎는다 — 거짓 평화의 정적
            ApplyPreset(dawnPreset);

            HouseController house = HouseController.Instance;
            ScenarioAnchor door = house != null ? house.FindAnchor("anchor_entrance_door") : null;
            doorPos = door != null ? door.transform.position : transform.position;

            EventBus.ShowSubtitle("수 주가 흘렀다. 포성이 어느 순간 뚝 그쳤다.", 4.5f);
            At(6f, PlayBroadcast);
            At(c.act4PersuadeSeconds, OnPersuadeDeadline, "war.act4.deadline");
            At(c.flashDelayAfterBroadcast, CompleteAct, "war.act4.end");
            WarSurvivor.Talked += OnTalk;
        }

        protected override void OnExit() { WarSurvivor.Talked -= OnTalk; }
        private void OnDisable() { WarSurvivor.Talked -= OnTalk; }

        private void PlayBroadcast()
        {
            ScenarioFlags.Set(WarKeys.FlagPeaceBroadcast, true);
            WarAudio.Instance?.Play(WarSound.Static, 0.5f);
            WarTime.Sequence(Cfg.falsePeaceLines, 0.5f, 4f, () =>
            {
                if (!IsActive) return;
                EventBus.ShowMessage(War.AliveSurvivors > 0 ? "생존자들이 환호한다. 문과 창문 쪽으로 몰려든다…" : "혼자 남은 은신처에서도 한숨이 터져 나온다. 정말 끝난 걸까?");
                WarHud.Instance?.Notice("방송을 믿을 것인가? 단파에서 들었던 이야기를 떠올려 보자.", 7f);
            });
        }

        // ───────── 설득 ─────────
        private void OnTalk(WarSurvivor who)
        {
            if (!IsActive || survivorsLeaving && persuaded) return;
            ChoicePanel panel = ChoicePanel.Instance;
            if (panel == null || War == null) return;
            var opts = new List<ChoicePanel.Option>();
            string body;
            if (War.CluesEnough)
            {
                body = $"{who.DisplayName}이(가) 들뜬 얼굴로 말한다. \"끝났대요! 나가서 가족을 찾아야죠.\"\n단서가 있다: 단파 방송, 방어선 붕괴, 군 철수 정황. 방송은 이상하다.";
                opts.Add(new ChoicePanel.Option("단서를 짚으며 설득하고 문을 다시 잠근다", Persuade));
                opts.Add(new ChoicePanel.Option("…나가게 둔다", () => { }));
            }
            else
            {
                body = $"{who.DisplayName}이(가) 들뜬 얼굴로 말한다. \"끝났대요! 나가서 가족을 찾아야죠.\"\n방송이 이상한 것 같지만, 확신할 근거가 부족하다. (기만 단서 {War.DeceptionCluesCount}/{Cfg.deceptionClueThreshold})";
                opts.Add(new ChoicePanel.Option("\"뭔가 이상해요\" 하고 말려 본다 (근거 부족)", () =>
                {
                    War.AddSanity(-4f);
                    EventBus.ShowMessage("\"증거도 없이 무슨 소리예요?\" 아무도 귀 기울이지 않는다.");
                }));
                opts.Add(new ChoicePanel.Option("…나가게 둔다", () => { }));
            }
            panel.Show("종전 방송", body, opts, true);
        }

        private void Persuade()
        {
            persuaded = true;
            survivorsLeaving = true;
            ScenarioFlags.Set(WarKeys.FlagPersuaded, true);
            HouseController.Instance?.SetDoorState(WarKeys.FrontDoor, DoorState.Locked);
            foreach (WarSurvivor s in survivors) if (s != null) s.Stop();
            EventBus.ShowSubtitle("\"…그 방송, 정말 믿어도 될까요?\" 생존자들이 서로를 바라보다 천천히 자리에 앉는다.", 5f);
            CompleteObjective(objectiveJudge);
        }

        private void OnPersuadeDeadline()
        {
            if (persuaded || survivorsLeaving) return;
            survivorsLeaving = true;
            if (War.AliveSurvivors <= 0) { CompleteObjective(objectiveJudge); return; }

            EventBus.ShowSubtitle("생존자들이 문을 열어젖히고 복도로 뛰쳐나간다. \"나가요! 이제 끝났어요!\"", 5f);
            HouseController.Instance?.SetDoorState(WarKeys.FrontDoor, DoorState.Open);
            foreach (WarSurvivor s in survivors)
            {
                if (s == null || s.Current != WarSurvivor.State.Inside) continue;
                WarSurvivor who = s;
                who.WalkTo(doorPos + new Vector3(0f, 0f, -1.5f), Cfg.survivorWalkSpeed, () => War.LoseSurvivor());
            }
            EventBus.ShowMessage("동료들이 떠났다. 문을 닫고 안쪽 방으로 물러나야 한다.");
        }

        private void Update()
        {
            // 플레이어가 직접 밖으로 나갔는가 (막지 않는다)
            if (War != null && Cfg != null && !ScenarioFlags.GetBool(WarKeys.FlagWentOutside) && PlayerIsOutside(Cfg.act4DoorOutsideZ))
            {
                ScenarioFlags.Set(WarKeys.FlagWentOutside, true);
                ScoreSystem.Instance?.Record(WarKeys.RecOutside, 1f);
                EventBus.ShowMessage("복도로 나왔다. 아무도 없는 복도가 이상하리만치 조용하다…");
            }
        }
    }
}
