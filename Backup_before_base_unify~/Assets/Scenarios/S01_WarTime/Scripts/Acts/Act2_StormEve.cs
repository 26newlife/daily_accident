using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 2. 폭풍 전야의 대비 (파밍 및 거점 요새화)
    /// 제한된 시간과 행동력 안에서 4가지를 준비한다:
    ///   ① 식량·물 확보(집 안 수색) ② 라디오 청취(단파에서 기만 단서) ③ 창밖 관찰(군 철수 정황)
    ///   ④ 비상 약품과 특수 물자(방독면·요오드화 칼륨) + 보온 용품(담요·옷)
    /// 행동력이 바닥나거나 시간이 다 되면 Act 3 으로 넘어간다.
    /// </summary>
    public class Act2_StormEve : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveSupplies, objectiveRadio, objectiveObserve, objectiveGear;

        private bool finishing;
        private bool announcedLow;

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) return;
            War.SetPhase(WarPhase.Prep);
            AddObjective(objectiveSupplies); AddObjective(objectiveRadio); AddObjective(objectiveObserve); AddObjective(objectiveGear);
            War.Changed += Evaluate;
            EventBus.ShowMessage("식량·물·약품·보온 용품을 집 안에서 모아야 한다. 움직일 수 있는 횟수(행동력)는 한정되어 있다.");
            EventBus.ShowSubtitle("아파트 단지에 정적이 내려앉았다. 이웃들이 하나둘 짐을 끌고 복도로 나온다.", 5f);
            At(c.act2LimitSeconds - 40f, () => EventBus.ShowMessage("곧 외곽 교전이 격화된다. 마무리해야 한다."));
            At(c.act2LimitSeconds, Finish, "war.act2.timeout");
            At(70f, () => HouseController.Instance?.TriggerStructureShake(StructureShakePreset.Light));
            At(150f, () => { WarAudio.Instance?.Play(WarSound.Rumble, 0.6f); HouseController.Instance?.ShakeCamera(0.25f, 2f); });
            Evaluate();
        }

        protected override void OnExit()
        {
            if (War != null) War.Changed -= Evaluate;
        }

        private void Evaluate()
        {
            WarState s = War; WarConfig c = Cfg;
            if (s == null || c == null || !IsActive) return;
            if (s.Food >= c.goalFood && s.Water >= c.goalWater) CompleteObjective(objectiveSupplies);
            if (s.HasClue(WarKeys.ClueDefense) || s.HasClue(WarKeys.ClueWeapon)) CompleteObjective(objectiveRadio);
            if (s.HasClue(WarKeys.ClueWithdrawal)) CompleteObjective(objectiveObserve);
            if (s.HasGasMask || s.IodineTaken || s.HasIodine) CompleteObjective(objectiveGear);

            if (s.ActionPoints <= 2 && !announcedLow && s.ActionPoints > 0) { announcedLow = true; EventBus.ShowMessage("기운이 거의 바닥났다. 남은 행동은 몇 번뿐이다."); }
            if (s.ActionPoints <= 0 && !finishing)
            {
                finishing = true;
                EventBus.ShowMessage("더는 움직일 힘이 없다. 이제 버티는 일만 남았다.");
                At(c.act2ToAct3DelaySeconds, CompleteAct);
            }
        }

        private void Finish()
        {
            if (finishing) return;
            finishing = true;
            EventBus.ShowMessage("교전이 가까워졌다. 더는 밖으로 나갈 수 없다. 은신처에 틀어박힌다.");
            At(Cfg.act2ToAct3DelaySeconds, CompleteAct);
        }
    }
}
