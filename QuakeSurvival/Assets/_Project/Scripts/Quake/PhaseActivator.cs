using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>지정한 단계가 되면 대상 오브젝트들을 켠다(예: 전진 때 깨진 유리 등장).</summary>
    public sealed class PhaseActivator : MonoBehaviour
    {
        [SerializeField] private QuakeDirector director;
        [SerializeField] private QuakePhase activateAt = QuakePhase.Foreshock;
        [SerializeField] private GameObject[] targets = new GameObject[0];

        private void Awake()
        {
            SetTargets(false);
        }

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
            if (phase == activateAt) SetTargets(true);
        }

        private void SetTargets(bool active)
        {
            foreach (var t in targets)
            {
                if (t != null) t.SetActive(active);
            }
        }
    }
}
