using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 1. 일상 속의 균열 (프롤로그 / 튜토리얼)
    /// 위급재난문자 → 민방위 사이렌·전투기 굉음·흔들리는 실내 → 제한 시간 안에 필수품(라디오, 생수·비상식량) 파밍.
    /// 못 챙긴 것은 포기해야 한다 (사망 없음, 결과로만 돌아온다).
    /// </summary>
    public class Act1_CrackInDaily : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveRadio, objectiveSupplies;

        private readonly HashSet<string> got = new HashSet<string>();
        private bool finishing;

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) { Debug.LogError("[S01] WarState/WarConfig 가 없습니다."); return; }
            War.SetPhase(WarPhase.Prep);
            AddObjective(objectiveRadio); AddObjective(objectiveSupplies);
            ScoreSystem.Instance?.Record("war_act1_start", 1f);

            // 재난문자: 경보음 + 팝업 + 폰 메시지
            At(1.5f, () =>
            {
                WarAudio.Instance?.Play(WarSound.Alert, 1f);
                WarHud.Instance?.ShowAlert(c.alertText, c.alertPopupSeconds);
                PhoneUI phone = FindFirstObjectByType<PhoneUI>(FindObjectsInactive.Include);
                if (phone != null) phone.AddMessage(c.alertSender, c.alertText);
                EventBus.ShowMessage("스마트폰에서 위급재난문자 경보음이 울렸다.");
            });
            // 경보와 함께 창밖이 평화로운 일상에서 전시 상황으로 바뀐다 (하늘·조명 전환, 연기 기둥, 포격 섬광, 전투기·헬기 편대)
            At(1.5f + c.outbreakDelayAfterAlert, () => WarOutbreak.Instance?.Begin());
            At(5f, () => WarAudio.Instance?.Play(WarSound.Siren, 0.7f));
            At(60f, () => HouseController.Instance?.ShakeCamera(0.4f, 2.5f));
            foreach (GameTimingConfig.TimedBeat b in c.act1Beats) { GameTimingConfig.TimedBeat beat = b; At(b.time, () => Beat(beat)); }
            ScheduleSway();
            At(c.act1LimitSeconds, OnTimeout, "war.act1.timeout");
            At(c.act1LimitSeconds - 20f, () => EventBus.ShowMessage("서둘러야 한다. 20초 뒤에는 움직이기 어려워진다."));
        }

        private void ScheduleSway()
        {
            At(Cfg.lightSwayIntervalSeconds, () =>
            {
                HouseController.Instance?.TriggerStructureShake(StructureShakePreset.Light);
                ScheduleSway();
            });
        }

        protected override void OnScenarioEvent(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.ItemAcquired) return;
            if (e.SourceId == WarKeys.Radio) { got.Add("radio"); CompleteObjective(objectiveRadio); }
            else if (e.SourceId == WarKeys.PickupSupplies) { got.Add("supplies"); CompleteObjective(objectiveSupplies); }
            else return;
            if (got.Count >= 2 && !finishing)
            {
                finishing = true;
                EventBus.ShowMessage("필요한 것은 챙겼다. 이제 은신처를 준비해야 한다.");
                At(Cfg.act1ToAct2DelaySeconds, CompleteAct);
            }
        }

        private void OnTimeout()
        {
            if (finishing) return;
            finishing = true;
            EventBus.ShowMessage("더는 지체할 수 없다. 챙기지 못한 것은 포기하고 은신처 준비에 들어간다.");
            At(Cfg.act1ToAct2DelaySeconds, CompleteAct);
        }
    }
}
