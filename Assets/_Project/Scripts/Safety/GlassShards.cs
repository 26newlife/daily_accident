using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>깨진 유리 구역 표시(Trigger, "Ignore Raycast" 레이어). GlassInjury가 감지한다.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GlassShards : MonoBehaviour
    {
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }
}
