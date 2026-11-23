using GameCore;
using GameData;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// Act 6. 잿더미 위의 진짜 종전 (멀티 엔딩)
    /// 라디오가 아닌 다국적 구호대의 확성기와 헬기 로터 소리로 진짜 구조가 온다.
    ///   · 진 엔딩: 기만 단서로 함정을 간파하고 합류한 생존자 전원과 함께 구조
    ///   · 씁쓸한 생존 엔딩: 혼자(또는 일부만) 살아남아 구조 — 거절/이탈한 동료에 대한 죄책감
    ///   · 배드 엔딩: 거짓 종전에 속아 외출했거나 낙진/피폭 관리에 실패해 사망 (Act 5 까지의 사망 화면)
    /// </summary>
    public class Act6_TrueArmistice : WarActBase
    {
        [SerializeField] private ObjectiveData objectiveRescue;
        [SerializeField] private EnvironmentPreset clearPreset;

        protected override void OnEnter()
        {
            WarConfig c = Cfg;
            if (War == null || c == null) return;
            War.SetPhase(WarPhase.Rescue);
            AddObjective(objectiveRescue);
            ApplyPreset(clearPreset);

            EventBus.ShowSubtitle("…멀리서 낯선 말소리가 들린다. 확성기다.", 4f);
            At(3f, () => WarTime.Sequence(c.rescueLines, 0f, 4.5f));
            At(c.act6RotorDelaySeconds, () =>
            {
                WarAudio.Instance?.PlayLoop(WarSound.Rotor, 0.7f);
                EventBus.ShowMessage("건물 위에서 헬기 로터 소리가 점점 커진다. 구조대다.");
                HouseController.Instance?.ShakeCamera(0.2f, 4f);
            });
            At(c.act6EndingDelaySeconds, Resolve, "war.act6.ending");
        }

        protected override void OnExit() { WarAudio.Instance?.StopLoop(); }

        private void Resolve()
        {
            CompleteObjective(objectiveRescue);
            War.PushRecords();
            bool allSaved = War.CluesEnough && War.Lost == 0 && War.Refused == 0 && War.Joined >= 3;
            ScenarioFlags.Set(WarKeys.FlagAllSaved, allSaved);
            string ending = allSaved ? WarKeys.EndingTrue : WarKeys.EndingBitter;
            if (DeathAndEnding.Instance != null) DeathAndEnding.Instance.TriggerEnding(ending);
        }
    }
}
