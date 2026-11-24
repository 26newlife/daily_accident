using System.Collections.Generic;
using QuakeSurvival.Interaction;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 매뉴얼 조치(가스, 차단기, 문 등)가 지금 완료 상태인지 기록한다.
    /// 씬의 SwitchInteractable들을 구독해서 actionId별 상태를 보관한다.
    /// </summary>
    public sealed class SafetyChecklist : MonoBehaviour
    {
        public const string GasValve = "gas_valve";
        public const string Breaker = "breaker";
        public const string Door = "door";
        public const string Shoes = "shoes";
        public const string EmergencyBag = "bag";

        /// <summary>조치 상태가 바뀔 때 (actionId, 완료 여부).</summary>
        public event System.Action<string, bool> Changed;

        [SerializeField] private SwitchInteractable[] switches = new SwitchInteractable[0];

        private readonly Dictionary<string, bool> states = new Dictionary<string, bool>();

        public bool IsDone(string actionId) => states.TryGetValue(actionId, out bool on) && on;

        /// <summary>해당 조치들을 더 이상 바꿀 수 없게 잠근다(판정이 끝난 뒤 점수가 바뀌지 않도록).</summary>
        public void Lock(params string[] actionIds)
        {
            foreach (var sw in switches)
            {
                if (sw != null && System.Array.IndexOf(actionIds, sw.ActionId) >= 0) sw.Locked = true;
            }
        }

        private void OnEnable()
        {
            foreach (var sw in switches)
            {
                if (sw != null) sw.OnChanged.AddListener(Record);
            }
        }

        // 스위치의 Awake(startOn 적용)가 끝난 뒤 초기 상태를 기록한다.
        private void Start()
        {
            foreach (var sw in switches)
            {
                if (sw != null) states[sw.ActionId] = sw.IsOn;
            }
        }

        private void OnDisable()
        {
            foreach (var sw in switches)
            {
                if (sw != null) sw.OnChanged.RemoveListener(Record);
            }
        }

        private void Record(string actionId, bool isOn)
        {
            states[actionId] = isOn;
#if UNITY_EDITOR || DEBUG
            Debug.Log($"[SafetyChecklist] {actionId} = {isOn}");
#endif
            Changed?.Invoke(actionId, isOn);
        }
    }
}
