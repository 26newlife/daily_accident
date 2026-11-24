using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>본진이 시작되면 실내 조명을 끈다(정전).</summary>
    public sealed class PowerOutage : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private Light[] lights = new Light[0];

        private void OnEnable()
        {
            if (director != null) director.PhaseChanged += OnPhase;
        }

        private void OnDisable()
        {
            if (director != null) director.PhaseChanged -= OnPhase;
        }

        private void OnPhase(QuakePhase phase)
        {
            if (phase != QuakePhase.MainShock) return;
            foreach (var l in lights)
            {
                if (l != null) l.enabled = false;
            }
        }
    }
}
