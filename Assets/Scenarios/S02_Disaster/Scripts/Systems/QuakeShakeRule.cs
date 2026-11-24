using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 땅이 흔들리는 동안(전진·본진·여진) 선 채로 돌아다니면 넘어져 사망한다 (원본 ShakeMovementRule 이식).
    /// 흔들림이 시작된 뒤 graceSeconds 동안은 가까운 탁자 밑으로 가도록 움직여도 되고, 웅크린 채 기어가는 것은 허용한다.
    /// 대피지역 안에서는 QuakeOutcome 이 사망 대신 감점으로 바꾼다.
    /// </summary>
    public class QuakeShakeRule : MonoBehaviour
    {
        [SerializeField] private float minIntensity = 0.1f;
        private Vector3 last; private float moved; private QuakePhase seenPhase = QuakePhase.Calm; private bool hasLast;

        private void Update()
        {
            QuakeTimeline tl = QuakeTimeline.Instance;
            if (tl == null || !tl.Running || tl.Player == null) return;
            Vector3 p = tl.Player.transform.position;
            Vector3 d = hasLast ? p - last : Vector3.zero;
            last = p; hasLast = true;
            if (tl.Phase != seenPhase) { seenPhase = tl.Phase; moved = 0f; }
            if (!QuakeTimeline.IsShakingPhase(tl.Phase) || tl.Intensity < minIntensity) return;
            if (tl.PhaseElapsed < tl.Config.shakeGraceSeconds) return;
            if (tl.Player.IsCrouching) return;
            if (d.magnitude > 3f) return;   // 순간이동(엘리베이터 탑승 등)은 세지 않는다
            moved += new Vector2(d.x, d.z).magnitude;
            if (moved > tl.Config.shakeAllowedDistance) { moved = 0f; QuakeOutcome.Instance.Kill(DeathReason.QuakeFellWhileShaking); }
        }
    }
}
