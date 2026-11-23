using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 배터리 라디오. 처음 상호작용하면 챙기고(은신처 자리로 옮긴다), 이후에는 채널을 고른다.
    /// 공식 채널은 "전선 안정화"만 방송하지만, 단파 채널에는 불길한 단서가 숨어 있다 (기만 단서 DeceptionClue).
    /// </summary>
    public class WarRadio : InteractableObject
    {
        [SerializeField, Tooltip("챙긴 뒤 놓일 은신처 위치 (비우면 제자리)")] private Transform shelterSpot;
        [SerializeField] private AudioSource staticSource;

        public static WarRadio Instance { get; private set; }
        protected override void OnEnable() { base.OnEnable(); Instance = this; }
        protected override void OnDisable() { base.OnDisable(); if (Instance == this) Instance = null; }

        public override string GetPrompt()
        {
            WarState s = WarState.Instance;
            return s != null && s.HasRadio ? "라디오 주파수 맞추기" : "배터리 라디오 챙기기";
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            WarState s = WarState.Instance;
            if (s == null || s.Config == null) return;
            if (!s.HasRadio)
            {
                s.SetHasRadio();
                if (shelterSpot != null) transform.SetPositionAndRotation(shelterSpot.position, shelterSpot.rotation);
                EventBus.ShowMessage("배터리 라디오를 챙겼다. 은신처에서 주파수를 맞출 수 있다.");
                Publish(ScenarioEventType.ItemAcquired);
                return;
            }
            OpenTuner();
        }

        private void OpenTuner()
        {
            ChoicePanel panel = ChoicePanel.Instance;
            WarState s = WarState.Instance;
            if (panel == null || s == null) return;
            var opts = new List<ChoicePanel.Option>();
            WarConfig.Channel[] ch = s.Config.channels;
            for (int i = 0; i < ch.Length && i < 5; i++)
            {
                int index = i;
                opts.Add(new ChoicePanel.Option($"{ch[i].name}  ({ch[i].label})", () => Tune(index)));
            }
            panel.Show("라디오", "손잡이를 돌려 주파수를 맞춘다.", opts, true);
        }

        public void Tune(int index)
        {
            WarState s = WarState.Instance;
            if (s == null) return;
            WarConfig.Channel c = s.Config.channels[index];
            s.SetRadioFrequency(index);
            WarAudio.Instance?.Play(WarSound.Static, 0.6f);
            string[] lines = c.lines;
            // 거짓 종전 방송 이후에는 공식 채널이 계속 종전을 방송한다
            if (index == 0 && ScenarioFlags.GetBool(WarKeys.FlagPeaceBroadcast)) lines = s.Config.falsePeaceLines;
            WarTime.Sequence(lines, 0.6f, 3.2f);
            if (!string.IsNullOrEmpty(c.clueId) && s.AddClue(c.clueId))
                WarTime.After(0.8f, () => WarHud.Instance?.Notice("미심쩍은 방송이다… 기억해 둘 만하다. (기만 단서 확보)", 5f));
            Publish(ScenarioEventType.Custom);
        }
    }
}
