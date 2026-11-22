using System.Collections.Generic;
using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 3. 고립된 방주 (실내 생존 + 방문자 이벤트)
    /// 외부 교전으로 외출 불가. 식량·물·정신력을 소모하며 버틴다.
    ///   · 생존자 방문(군인/아이/이웃): 문틈 확인 후 합류 여부를 정한다 (합류 = 소모 증가 vs 능력·자원, 거절 = 정신력 하락)
    ///   · 야간 외부인 위협: 식량 양도 / 무기 방어 / 소등 후 빈집 위장
    /// 현관문 상호작용(door_front)에 오버라이드를 걸어 "문틈으로 확인하기" 를 제공한다. 사망 없음 — 선택의 결과만 남는다.
    /// </summary>
    public class Act3_IsolatedArk : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveHold, objectiveVisitors;
        [SerializeField] private WarSurvivor soldier, child, neighbor;
        [SerializeField] private EnvironmentPreset nightPreset;

        private int pendingVisitor = -1;      // 0..2 방문자, 3 = 약탈자
        private int resolved;
        private int visitorToken;

        private static readonly string[] VisitorTitles = { "다친 군인", "길 잃은 아이", "구호물품을 든 이웃" };
        private static readonly string[] VisitorKnock =
        {
            "누군가 문을 힘없이 두드린다. 쿵… 쿵…",
            "문 아래쪽에서 작고 다급한 두드림이 들린다.",
            "익숙한 목소리가 조심스럽게 문 너머에서 이름을 부른다.",
        };
        private static readonly string[] VisitorSee =
        {
            "문틈으로 군복 차림의 남자가 보인다. 다리를 크게 다쳤다.\n\"제발… 잠시만 쉬게 해 주십시오. 부상병은 응급처치와 수리를 도울 수 있습니다.\"",
            "복도에 아이 하나가 울먹이며 서 있다.\n\"엄마를 잃어버렸어요… 무서워요…\"",
            "옆 동 이웃이 구호물품 상자를 안고 서 있다.\n\"혼자 있기 무서워서… 이거 나눠 먹읍시다.\"",
        };

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) return;
            War.SetPhase(WarPhase.Ark);
            AddObjective(objectiveHold); AddObjective(objectiveVisitors);
            EventBus.ShowSubtitle("며칠이 흘렀다. 창밖에서는 포성이 밤낮없이 이어진다.", 5f);
            EventBus.ShowMessage("외출은 불가능하다. 식량과 물, 정신력을 아끼며 버텨야 한다.");

            for (int i = 0; i < c.visitorTimes.Length && i < 3; i++) { int idx = i; At(c.visitorTimes[i], () => Knock(idx)); }
            At(c.nightTime, () => { ApplyPreset(nightPreset); EventBus.ShowSubtitle("해가 졌다. 어둠 속에서 포성이 더 가까워진다.", 5f); });
            At(c.raiderTime, () => Knock(3));
            At(40f, () => WarAudio.Instance?.Play(WarSound.Rumble, 0.5f));
            At(100f, () => { WarAudio.Instance?.Play(WarSound.Boom, 0.45f); HouseController.Instance?.ShakeCamera(0.3f, 2f); });
            At(c.act3LimitSeconds, () => CompleteAct(), "war.act3.end");
        }

        protected override void OnExit() { ClearDoor(); }

        // ───────── 방문 ─────────
        private void Knock(int index)
        {
            if (pendingVisitor >= 0) return;   // 앞의 방문이 아직 처리 중이면 건너뛴다 (겹치지 않게)
            pendingVisitor = index;
            int token = ++visitorToken;
            WarAudio.Instance?.Play(WarSound.Knock, 1f);
            EventBus.ShowSubtitle(index < 3 ? VisitorKnock[index] : "쾅! 쾅! 거친 발길질과 함께 낯선 목소리들이 문을 흔든다. \"안에 누구 있지? 열어!\"", 5f);
            HouseController house = HouseController.Instance;
            if (house != null)
                house.ReplaceInteraction(WarKeys.FrontDoor, new InteractableObject.InteractionOverride
                {
                    prompt = () => "문틈으로 확인하기",
                    onInteract = _ => OpenDoorMenu(index),
                });
            float wait = index < 3 ? Cfg.visitorAnswerSeconds : Cfg.raiderAnswerSeconds;
            At(wait, () => { if (visitorToken == token && pendingVisitor == index) Unanswered(index); });
        }

        private void OpenDoorMenu(int index)
        {
            ChoicePanel panel = ChoicePanel.Instance;
            if (panel == null || pendingVisitor != index) return;
            var opts = new List<ChoicePanel.Option>();
            if (index < 3)
            {
                opts.Add(new ChoicePanel.Option($"{VisitorTitles[index]}을(를) 들인다 (합류)", () => Accept(index)));
                opts.Add(new ChoicePanel.Option("거절한다", () => Refuse(index)));
                panel.Show(VisitorTitles[index], VisitorSee[index] + "\n\n합류하면 식량·물 소모가 빨라지지만 도움도 얻는다. 거절하면 정신력이 떨어진다.", opts, false);
            }
            else
            {
                opts.Add(new ChoicePanel.Option($"식량 {Cfg.raiderFoodDemand}개를 문틈으로 내준다", GiveFood));
                opts.Add(new ChoicePanel.Option(War.HasWeapon ? "쇠파이프를 들고 쫓아낸다" : "무기가 없다 — 큰소리로 쫓아낸다", Defend));
                opts.Add(new ChoicePanel.Option("불을 끄고 빈집인 척한다", LightsOut));
                panel.Show("정체불명의 무리", "문틈으로 보니 서너 명이 서성인다. 무기를 든 자도 있다.\n식량을 바라는 눈빛이다. 어떻게 할 것인가?", opts, false);
            }
        }

        private void Accept(int index)
        {
            WarSurvivor who = index == 0 ? soldier : index == 1 ? child : neighbor;
            string id = index == 0 ? WarKeys.SurvivorSoldier : index == 1 ? WarKeys.SurvivorChild : WarKeys.SurvivorNeighbor;
            War.Join(id);
            if (who != null) who.Appear();
            EventBus.ShowMessage(index == 0 ? "군인이 신음하며 들어왔다. 야전 응급키트를 내놓았다." : index == 1 ? "아이가 품에 안겨 울음을 그쳤다. 정신력이 올랐다." : "이웃이 구호물품을 내놓았다. 식량과 물이 늘었다.");
            Resolve();
        }

        private void Refuse(int index)
        {
            War.RefuseVisitor();
            WarAudio.Instance?.Play(WarSound.Knock, 0.4f);
            EventBus.ShowMessage("문을 열어 주지 않았다. 발소리가 멀어진다. 마음이 무겁다.");
            Resolve();
        }

        private void Unanswered(int index)
        {
            if (index < 3)
            {
                War.RefuseVisitor();
                EventBus.ShowMessage("끝내 문을 열지 못했다. 두드리는 소리가 잦아든다…");
            }
            else { LoseFood(Cfg.raiderFoodDemand, "문이 부서져라 두드리던 무리가 환풍구로 식량을 훔쳐 갔다."); }
            Resolve();
        }

        private void GiveFood()
        {
            int give = Mathf.Min(War.Food, Cfg.raiderFoodDemand);
            War.AddFood(-give);
            War.AddSanity(-3f);
            EventBus.ShowMessage(give > 0 ? "식량을 건네자 무리가 물러났다. 하지만 다시 올지도 모른다." : "줄 식량이 없다. 무리가 욕설을 남기고 물러났다.");
            Resolve();
        }

        private void Defend()
        {
            if (War.HasWeapon)
            {
                War.AddSanity(2f);
                WarAudio.Instance?.Play(WarSound.Knock, 0.8f);
                EventBus.ShowMessage("쇠파이프로 문을 후려치며 소리치자 무리가 물러났다. 손이 떨린다.");
            }
            else
            {
                War.AddSanity(-Cfg.defendFailSanityLoss);
                LoseFood(Cfg.raiderFoodDemand, "무기도 없이 큰소리만 쳤다. 비웃음이 돌아왔고, 식량을 빼앗겼다.");
            }
            Resolve();
        }

        private void LightsOut()
        {
            HouseController.Instance?.SetBlackout(true);
            EventBus.ShowMessage("불을 껐다. 숨을 죽이고 기다린다…");
            bool quiet = War.AliveSurvivors <= Cfg.lightsOutMaxSurvivors;
            At(12f, () =>
            {
                HouseController.Instance?.SetBlackout(false);
                if (quiet) EventBus.ShowMessage("숨을 죽이고 기다리자 무리가 빈집인 줄 알고 지나갔다.");
                else LoseFood(Cfg.raiderFoodDemand, "여러 명의 기척이 새어 나갔다. 무리가 들이닥쳐 식량을 가져갔다.");
            });
            Resolve();
        }

        private void LoseFood(int n, string msg)
        {
            War.AddFood(-Mathf.Min(War.Food, n));
            War.AddSanity(-6f);
            EventBus.ShowMessage(msg);
        }

        private void Resolve()
        {
            pendingVisitor = -1;
            visitorToken++;
            resolved++;
            ClearDoor();
            if (resolved >= 3) CompleteObjective(objectiveVisitors);
        }

        private static void ClearDoor()
        {
            if (HouseController.Instance != null) HouseController.Instance.ReplaceInteraction(WarKeys.FrontDoor, null);
        }
    }
}
