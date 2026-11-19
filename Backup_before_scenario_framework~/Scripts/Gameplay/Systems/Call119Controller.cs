using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 스마트폰 119 신고. 폰의 119 버튼(PhoneAction "call_119")을 누르면 위치 선택지를 띄우고,
    /// 정확한 위치("1004호 대피공간(10층)")를 고르면 구조대가 그 위치로 출동한다. 잘못 말하면 구조가 지연된다.
    /// 결과는 ScoreSystem.Records (call119_result: 2 정확 / 1 오답) 와 Custom 이벤트 "call_119_done" 으로 남긴다.
    /// 어느 Act 에서든 동작한다(항상 활성). 신고는 한 번만 가능하다.
    /// </summary>
    public class Call119Controller : MonoBehaviour
    {
        [SerializeField] private PhoneUI phone;
        [SerializeField] private string callActionId = EvacKeys.PhoneCall119Action;
        [SerializeField] private string dispatcherTitle = "119 상황실";
        [SerializeField, TextArea(1, 3)] private string promptBody = "\"119입니다. 어디서 도움이 필요하신가요? 정확한 위치를 말씀해 주세요.\"";
        [SerializeField, Tooltip("정답 위치 (첫 번째)와 오답 위치들. 표시 순서는 그대로")] private string correctLocation = "1004호 대피공간 (10층)";
        [SerializeField] private string[] wrongLocations = { "10층 복도 쪽이에요", "1004호요… 몇 층인지는 모르겠어요", "일단 불이 났어요, 빨리 와 주세요!" };
        [SerializeField, TextArea(1, 2)] private string correctReply = "\"1004호 대피공간이시군요. 구조대가 출동합니다. 방화문을 닫고 문틈을 막은 채 기다리세요.\"";
        [SerializeField, TextArea(1, 2)] private string wrongReply = "\"위치를 정확히 확인하지 못했습니다. 일단 출동하지만 시간이 더 걸릴 수 있습니다. 가능하면 다시 알려 주세요.\"";
        [SerializeField] private string alreadyCalledMessage = "이미 119에 신고했다.";

        public static Call119Controller Instance { get; private set; }
        /// <summary>0 = 미신고, 1 = 오답 위치, 2 = 정확한 위치</summary>
        public int Result { get; private set; }
        public bool HasCalled => Result > 0;

        private void Awake() { Instance = this; }
        private void OnEnable() => EventBus.ScenarioEventRaised += OnEvent;
        private void OnDisable() => EventBus.ScenarioEventRaised -= OnEvent;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEvent(ScenarioEvent e)
        {
            if (e.Type != ScenarioEventType.PhoneAction || e.SourceId != callActionId) return;
            if (GameFlow.IsEnded) return;
            if (HasCalled) { EventBus.ShowMessage(alreadyCalledMessage); return; }
            Open();
        }

        public void Open()
        {
            ChoicePanel panel = ChoicePanel.Instance;
            if (panel == null || panel.IsOpen) return;
            if (phone == null) phone = FindFirstObjectByType<PhoneUI>();
            if (phone != null) phone.SetOpen(false);

            var options = new List<ChoicePanel.Option> { new ChoicePanel.Option(correctLocation, () => Report(true)) };
            if (wrongLocations != null)
                foreach (string w in wrongLocations) options.Add(new ChoicePanel.Option(w, () => Report(false)));
            panel.Show(dispatcherTitle, promptBody, options);
        }

        /// <summary>선택 결과 처리 (디버그 패널에서도 호출)</summary>
        public void Report(bool correct)
        {
            if (HasCalled) return;
            Result = correct ? 2 : 1;
            ScoreSystem score = ScoreSystem.Instance;
            if (score != null)
            {
                score.Record(EvacKeys.RecCall119, Result);
                score.Record(EvacKeys.RecCall119Time, GameClock.Instance != null ? GameClock.Instance.GameSeconds : 0f);
            }
            EventBus.ShowSubtitle(correct ? correctReply : wrongReply, 6f);
            EventBus.Raise(ScenarioEventType.Custom, "call_119_done", correct);
        }
    }
}
