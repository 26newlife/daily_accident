using System;
using UnityEngine;

namespace QuakeSurvival.Safety
{
    /// <summary>
    /// 플레이어 발밑을 매 프레임 검사해서 현재 구역(AreaVolume)과
    /// 위험 구역(HazardVolume) 진입을 알린다.
    /// 경계(문간)에서 두 구역에 걸치면 기존 구역을 유지해 판정이 흔들리지 않게 한다.
    /// 구역이 겹치면 우선순위(AreaVolume.Priority)가 높은 구역을 고른다.
    /// </summary>
    public sealed class ZoneSensor : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private LayerMask zoneMask = 1 << 2; // Ignore Raycast
        [SerializeField] private float probeRadius = 0.2f;
        [SerializeField] private float probeHeight = 0.3f;

        public event Action<PlayerArea> AreaChanged;
        public event Action<DeathCause> HazardEntered;

        public PlayerArea CurrentArea { get; private set; } = PlayerArea.Unknown;

        private readonly Collider[] hits = new Collider[16];

        private void Awake()
        {
            if (player == null)
            {
                Debug.LogError("[ZoneSensor] player가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector3 probe = player.position + Vector3.up * probeHeight;
            int count = Physics.OverlapSphereNonAlloc(probe, probeRadius, hits, zoneMask, QueryTriggerInteraction.Collide);

            AreaVolume best = null;
            int currentPriority = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                // 겹친 위험 구역은 매 프레임 모두 알린다(대피지역에서 막힌 뒤 밖으로 나가도 다시 판정되도록).
                // 사망은 한 번만 확정되고 감점도 원인별로 한 번만 기록되므로 반복 호출해도 안전하다
                if (hits[i].TryGetComponent(out HazardVolume h)) HazardEntered?.Invoke(h.Cause);
                if (!hits[i].TryGetComponent(out AreaVolume area)) continue;
                if (area.Area == CurrentArea) currentPriority = Mathf.Max(currentPriority, area.Priority);
                else if (best == null || area.Priority > best.Priority) best = area;
            }

            // 지금 구역에 아직 걸쳐 있으면 우선순위가 더 높은 구역이 있을 때만 바꾼다(문간 떨림 방지)
            if (best != null && best.Priority > currentPriority) SetArea(best.Area);
        }

        private void SetArea(PlayerArea area)
        {
            CurrentArea = area;
#if UNITY_EDITOR || DEBUG
            Debug.Log($"[ZoneSensor] Area -> {area}");
#endif
            AreaChanged?.Invoke(area);
        }
    }
}
