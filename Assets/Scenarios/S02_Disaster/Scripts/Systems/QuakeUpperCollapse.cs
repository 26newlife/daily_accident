using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 크게 흔들린 건물의 위층은 언제 무너질지 모른다 — 지진(전진)이 시작된 뒤 계단으로 11층 이상 올라가면 무너지는 위층에 깔려 사망 (원본 규칙).
    /// 10~11층 사이 반층참에는 떨어진 콘크리트 조각과 금(단서)이 있다. 설정(QuakeConfig.upperFloorsCollapse)으로 끌 수 있다.
    /// </summary>
    public class QuakeUpperCollapse : MonoBehaviour
    {
        [SerializeField] private GameObject clueRubble;
        [SerializeField, Tooltip("이 높이를 넘으면 위층 붕괴 (11층 바닥 근처)")] private float deadlyY = 2.6f;
        [SerializeField, Tooltip("이 높이를 넘으면 단서 자막")] private float clueY = 0.9f;
        private bool clueShown;

        public void Init()
        {
            if (clueRubble != null) clueRubble.SetActive(false);
            if (QuakeTimeline.Instance != null) { QuakeTimeline.Instance.PhaseChanged -= OnPhase; QuakeTimeline.Instance.PhaseChanged += OnPhase; }
        }

        private void OnDestroy() { if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.PhaseChanged -= OnPhase; }

        private bool Active => QuakeTimeline.Instance != null && QuakeTimeline.Instance.Config.upperFloorsCollapse && QuakeTimeline.Instance.Phase >= QuakePhase.Foreshock;

        private void OnPhase(QuakePhase p)
        {
            if (p == QuakePhase.Foreshock && clueRubble != null && QuakeTimeline.Instance.Config.upperFloorsCollapse) clueRubble.SetActive(true);
        }

        private void Update()
        {
            if (!Active || QuakeAreas.Instance == null || QuakeAreas.Instance.Current != QuakeArea.Stairwell) return;
            float y = QuakeTimeline.Instance.Player.transform.position.y;
            if (!clueShown && y > clueY && y < deadlyY)
            {
                clueShown = true;
                EventBus.ShowSubtitle("(위쪽 계단에 콘크리트 조각이 떨어져 있다... 벽에 큰 금이 가 있고, 위에서 무언가 갈라지는 소리가 난다.)", 5f);
            }
            if (y > deadlyY) QuakeOutcome.Instance.Kill(DeathReason.QuakeUpperFloorCollapse);
        }
    }
}
