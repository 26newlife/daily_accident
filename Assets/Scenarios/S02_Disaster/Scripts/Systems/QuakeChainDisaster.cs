using System;
using System.Collections;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 가스/차단기 조치를 하지 않았을 때의 대형 화재 + 연쇄 폭발 연출 (원본 ChainDisaster 이식).
    /// 10층 집에서 시작해 복도, 계단, 출구를 따라 플레이어가 대피한 곳 근처까지 번진 뒤 끝난다.
    /// </summary>
    public class QuakeChainDisaster : MonoBehaviour
    {
        [SerializeField] private Vector3 gasOrigin = new Vector3(6.3f, 1f, 3f);
        [SerializeField] private Vector3 breakerOrigin = new Vector3(5.5f, 1.6f, 0.3f);
        [SerializeField] private Vector3 apartmentDoor = new Vector3(3f, 1f, 0f);
        [SerializeField] private Vector3 hallway = new Vector3(-7f, 1f, -1.1f);
        [SerializeField, Tooltip("위층 → 아래층")] private Vector3[] stairLandings = new Vector3[0];
        [SerializeField] private Vector3 buildingExit = new Vector3(-26f, -27.5f, -1.6f);
        [SerializeField] private Vector3[] facadeFires = new Vector3[0];
        [SerializeField] private float totalSeconds = 6f, stepDistance = 3f, nearPlayerDistance = 2f, endDelay = 1.2f;

        public bool IsPlaying { get; private set; }

        public void Play(DeathReason cause, Action onFinished)
        {
            if (IsPlaying || QuakeFx.Instance == null) { onFinished?.Invoke(); return; }
            Vector3 origin = cause == DeathReason.QuakeElectricFire ? breakerOrigin : gasOrigin;
            StartCoroutine(Run(BuildRoute(origin), onFinished));
        }

        private List<Vector3> BuildRoute(Vector3 origin)
        {
            var keys = new List<Vector3> { origin };
            QuakeArea area = QuakeAreas.Instance != null ? QuakeAreas.Instance.Current : QuakeArea.Apartment;
            Transform player = QuakeTimeline.Instance.Player.transform;
            if (area != QuakeArea.Apartment) { keys.Add(apartmentDoor); keys.Add(hallway); }
            if (area == QuakeArea.Stairwell || area.IsOutdoors())
            {
                bool outside = area != QuakeArea.Stairwell;
                foreach (Vector3 l in stairLandings)
                {
                    if (!outside && l.y < player.position.y - 1f) break;
                    keys.Add(l);
                }
                if (outside) keys.Add(buildingExit);
            }
            var pts = new List<Vector3>();
            for (int i = 0; i < keys.Count - 1; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(keys[i], keys[i + 1]) / stepDistance));
                for (int s = 0; s < steps; s++) pts.Add(Vector3.Lerp(keys[i], keys[i + 1], (float)s / steps));
            }
            pts.Add(keys[keys.Count - 1]);
            return pts;
        }

        private IEnumerator Run(List<Vector3> route, Action onFinished)
        {
            IsPlaying = true;
            Transform player = QuakeTimeline.Instance.Player.transform;
            foreach (Vector3 p in facadeFires) QuakeFx.FireAt(p, 1.6f);
            EventBus.ShowSubtitle("(펑! 집 안에서 불길이 치솟는다!)", 3f);
            float interval = Mathf.Clamp(totalSeconds / (route.Count + 1), 0.12f, 0.5f);
            for (int i = 0; i < route.Count; i++)
            {
                Blast(route[i], i == 0 ? 1.6f : 1f, i % 3 == 0, player);
                yield return new WaitForSeconds(interval);
            }
            Vector3 target = player.position + Vector3.up;
            Vector3 dir = route[route.Count - 1] - target; dir.y = 0f;
            Blast(dir.sqrMagnitude < 0.01f ? target : target + dir.normalized * nearPlayerDistance, 1.3f, true, player);
            yield return new WaitForSeconds(endDelay);
            IsPlaying = false;
            onFinished?.Invoke();
        }

        private static void Blast(Vector3 p, float scale, bool leaveFire, Transform player)
        {
            QuakeFx.Explosion(p, scale);
            if (leaveFire) QuakeFx.FireAt(p, scale, false);
            if (QuakeTimeline.Instance != null) QuakeTimeline.Instance.AddImpulse(Mathf.Clamp01(1f - Vector3.Distance(p, player.position) / 30f) * 0.8f);
        }

        public void SetRoute(Vector3[] landings, Vector3[] fires) { stairLandings = landings; facadeFires = fires; }
    }
}
