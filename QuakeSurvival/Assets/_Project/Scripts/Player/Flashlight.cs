using QuakeSurvival.Safety;
using UnityEngine;

namespace QuakeSurvival.Player
{
    /// <summary>비상 가방을 챙기면 가방 속 손전등이 켜진다(카메라에 붙은 스포트 조명).</summary>
    public sealed class Flashlight : MonoBehaviour
    {
        [SerializeField] private SafetyChecklist checklist;
        [SerializeField] private Light spot;

        private void Awake()
        {
            if (checklist == null || spot == null)
            {
                Debug.LogError("[Flashlight] 참조가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }
            spot.enabled = false;
        }

        private void OnEnable()
        {
            if (checklist != null) checklist.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (checklist != null) checklist.Changed -= OnChanged;
        }

        private void OnChanged(string actionId, bool done)
        {
            if (actionId == SafetyChecklist.EmergencyBag) spot.enabled = done;
        }
    }
}
