using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 들어가는 즉시 사망하는 위험 구역(예: 엘리베이터 안).
    /// GameObject가 활성화된 동안만 작동한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HazardVolume : MonoBehaviour
    {
        [SerializeField] private DeathCause cause = DeathCause.ElevatorTrapped;

        public DeathCause Cause => cause;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
