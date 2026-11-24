using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 매뉴얼 조치(가스·차단기·문·신발·비상 가방)가 지금 완료 상태인지 기록한다 (원본 SafetyChecklist 이식).
    /// 집 오브젝트(가스 메인 밸브·퓨즈콕·현관문·신발장)와 시나리오 소품(차단기·비상 가방)의 이벤트를 듣는다.
    ///   가스 = 메인 밸브나 퓨즈콕 중 하나라도 잠김 / 차단기 = 내림 / 문 = 현관문 열림 / 신발 = 신발장에서 신음 / 가방 = 챙김
    /// 본진 판정 뒤에는 가스·차단기를 잠가(Lock) 점수가 바뀌지 않게 한다.
    /// </summary>
    public class QuakeChecklist : MonoBehaviour
    {
        private readonly Dictionary<string, bool> states = new Dictionary<string, bool>();
        private readonly HashSet<string> locked = new HashSet<string>();
        private bool mainOpen = true, fuseOpen = true, breakerOn = true;

        public static QuakeChecklist Instance { get; private set; }
        public event Action<string, bool> Changed;

        public bool IsDone(string action) => states.TryGetValue(action, out bool v) && v;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; EventBus.ScenarioEventRaised -= OnEvent; }
        private void OnEnable() { EventBus.ScenarioEventRaised -= OnEvent; EventBus.ScenarioEventRaised += OnEvent; }
        private void OnDisable() { EventBus.ScenarioEventRaised -= OnEvent; }

        /// <summary>시나리오 시작 시 현재 상태를 읽는다 (밸브·차단기·문)</summary>
        public void ReadInitialState()
        {
            states.Clear(); locked.Clear();
            foreach (ValveInteractable v in FindObjectsByType<ValveInteractable>(FindObjectsInactive.Exclude))
            {
                if (v.Id == QuakeKeys.GasValveMain) mainOpen = v.IsOpen;
                else if (v.Id == QuakeKeys.FuseCock) fuseOpen = v.IsOpen;
            }
            foreach (ToggleInteractable t in FindObjectsByType<ToggleInteractable>(FindObjectsInactive.Exclude))
            {
                if (t.Id == QuakeKeys.Breaker) breakerOn = t.IsOn;
                else if (t.Id == QuakeKeys.FrontDoor) states[QuakeKeys.ActDoor] = t.IsOn;
            }
            states[QuakeKeys.ActGas] = !mainOpen || !fuseOpen;
            states[QuakeKeys.ActBreaker] = !breakerOn;
        }

        public void Lock(params string[] actions) { foreach (string a in actions) locked.Add(a); }
        public bool IsLocked(string action) => locked.Contains(action);

        private void OnEvent(ScenarioEvent e)
        {
            switch (e.SourceId)
            {
                case QuakeKeys.GasValveMain when e.Type == ScenarioEventType.ValveChanged: mainOpen = e.State; Set(QuakeKeys.ActGas, !mainOpen || !fuseOpen); break;
                case QuakeKeys.FuseCock when e.Type == ScenarioEventType.ValveChanged: fuseOpen = e.State; Set(QuakeKeys.ActGas, !mainOpen || !fuseOpen); break;
                case QuakeKeys.Breaker when e.Type == ScenarioEventType.Toggled: breakerOn = e.State; Set(QuakeKeys.ActBreaker, !breakerOn); break;
                case QuakeKeys.FrontDoor when e.Type == ScenarioEventType.Toggled: Set(QuakeKeys.ActDoor, e.State); break;
                case QuakeKeys.ShoeCabinet when e.Type == ScenarioEventType.Interacted: Set(QuakeKeys.ActShoes, true); break;
                case QuakeKeys.EmergencyBag when e.Type == ScenarioEventType.Interacted || e.Type == ScenarioEventType.ItemAcquired: Set(QuakeKeys.ActBag, true); break;
            }
        }

        private void Set(string action, bool done)
        {
            if (locked.Contains(action)) return;
            if (states.TryGetValue(action, out bool cur) && cur == done) return;
            states[action] = done;
            Debug.Log($"[Quake] 조치 {action} = {done}");
            Changed?.Invoke(action, done);
        }
    }
}
