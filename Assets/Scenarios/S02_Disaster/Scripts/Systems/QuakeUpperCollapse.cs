using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 크게 흔들린 건물의 위층은 언제 무너질지 모른다 — 지진(전진)이 시작된 뒤 계단으로 11층 이상 올라가면 무너지는 위층에 깔려 사망 (원본 규칙).
    /// 10~11층 사이 반층참에는 떨어진 콘크리트 조각과 금(단서)이 있다. 설정(QuakeConfig.upperFloorsCollapse)으로 끌 수 있다.
    /// 규모별: 6점대(강진)일 때만 동작한다 — 위층 붕괴 + 본진 뒤 옥상에 있으면 손상된 건물 붕괴(QuakeBuildingCollapse).
    /// 5점대(중진)는 위층이 버티므로 옥상 수직대피가 가능하다 (QuakeCoverTracker 가 옥상에서 웅크리면 보호로 친다).
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

        // 5점대(중진)면 위층이 무너지지 않는다 — 옥상 수직대피가 가능하다
        private bool Active => QuakeTimeline.Instance != null && QuakeTimeline.Instance.Config.upperFloorsCollapse && QuakeTimeline.Instance.IsStrong && QuakeTimeline.Instance.Phase >= QuakePhase.Foreshock;

        [SerializeField, Tooltip("6점대 강진: 본진이 시작되고 이만큼(초) 지난 뒤 옥상에 있으면 건물이 무너진다")] private float rooftopCollapseDelay = 6f;
        private float rooftopTime;
        private bool rooftopWarned;

        private void OnPhase(QuakePhase p)
        {
            if (p == QuakePhase.Foreshock && clueRubble != null && Active) clueRubble.SetActive(true);
        }

        private void Update()
        {
            if (!Active || QuakeAreas.Instance == null) return;
            if (QuakeAreas.Instance.Current == QuakeArea.Rooftop) { RooftopCollapse(); return; }
            rooftopTime = 0f;
            if (QuakeAreas.Instance.Current != QuakeArea.Stairwell) return;
            float y = QuakeTimeline.Instance.Player.transform.position.y;
            if (!clueShown && y > clueY && y < deadlyY)
            {
                clueShown = true;
                EventBus.ShowSubtitle("(위쪽 계단에 콘크리트 조각이 떨어져 있다... 벽에 큰 금이 가 있고, 위에서 무언가 갈라지는 소리가 난다.)", 5f);
            }
            if (y > deadlyY) QuakeOutcome.Instance.Kill(DeathReason.QuakeUpperFloorCollapse);
        }

        /// <summary>6점대 강진: 본진이 시작된 뒤 옥상에 머물면 (먼저 경고 자막) 손상된 건물이 무너진다. 웅크려도 소용없다</summary>
        private void RooftopCollapse()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl.Phase < QuakePhase.MainShock) return;
            rooftopTime += Time.deltaTime;
            if (!rooftopWarned && rooftopTime > rooftopCollapseDelay * 0.4f)
            {
                rooftopWarned = true;
                EventBus.ShowSubtitle("(발밑 옥상 바닥이 쩍 갈라진다... 건물 전체가 비명을 지르듯 삐걱거린다!)", 4f);
                tl.AddImpulse(0.4f);
            }
            if (rooftopTime > rooftopCollapseDelay) QuakeOutcome.Instance.Kill(DeathReason.QuakeBuildingCollapse);
        }
    }
}
