using System;
using System.Collections;
using System.Collections.Generic;
using QuakeSurvival.Fx;
using QuakeSurvival.Safety;
using UnityEngine;

namespace QuakeSurvival.Quake
{
    /// <summary>
    /// 가스/차단기 조치를 하지 않았을 때의 대형 화재 + 연쇄 폭발 연출.
    /// 10층 집에서 시작해 복도, 계단, 출구를 따라 플레이어가 대피한 곳 근처까지 번진 뒤 끝난다.
    /// </summary>
    public sealed class ChainDisaster : MonoBehaviour
    {
        [SerializeField] private FxLibrary fx;
        [SerializeField] private CameraShake shake;
        [SerializeField] private Transform player;
        [SerializeField] private ZoneSensor zones;

        [Header("경로(빌더가 채움)")]
        [SerializeField] private Vector3 gasOrigin;
        [SerializeField] private Vector3 breakerOrigin;
        [SerializeField] private Vector3 apartmentDoor;
        [SerializeField] private Vector3 hallway;
        [SerializeField] private Vector3[] stairLandings = new Vector3[0]; // 위층 -> 아래층
        [SerializeField] private Vector3 buildingExit;
        [SerializeField] private Vector3[] facadeFires = new Vector3[0];

        [Header("연출")]
        [SerializeField] private float totalSeconds = 6f;
        [SerializeField] private float stepDistance = 3f;
        [SerializeField] private float nearPlayerDistance = 2f;
        [SerializeField] private float endDelay = 1.2f;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (fx == null || !fx.IsValid || shake == null || player == null || zones == null)
            {
                Debug.LogError("[ChainDisaster] 참조가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        /// <summary>연출을 재생하고 끝나면 onFinished를 호출한다. 재생할 수 없으면 바로 호출한다.</summary>
        public void Play(DeathCause cause, Action onFinished)
        {
            if (!enabled || IsPlaying)
            {
                onFinished?.Invoke();
                return;
            }
            Vector3 origin = cause == DeathCause.ElectricFire ? breakerOrigin : gasOrigin;
            StartCoroutine(Run(BuildRoute(origin), onFinished));
        }

        private List<Vector3> BuildRoute(Vector3 origin)
        {
            var keys = new List<Vector3> { origin };
            PlayerArea area = zones.CurrentArea;
            if (area != PlayerArea.Apartment)
            {
                keys.Add(apartmentDoor);
                keys.Add(hallway);
            }
            if (area == PlayerArea.Stairwell || area.IsOutdoors())
            {
                bool outside = area != PlayerArea.Stairwell;
                foreach (var landing in stairLandings)
                {
                    if (!outside && landing.y < player.position.y - 1f) break;
                    keys.Add(landing);
                }
                if (outside) keys.Add(buildingExit);
            }
            return Subdivide(keys);
        }

        private List<Vector3> Subdivide(List<Vector3> keys)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < keys.Count - 1; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(keys[i], keys[i + 1]) / stepDistance));
                for (int s = 0; s < steps; s++) points.Add(Vector3.Lerp(keys[i], keys[i + 1], (float)s / steps));
            }
            points.Add(keys[keys.Count - 1]);
            return points;
        }

        private IEnumerator Run(List<Vector3> route, Action onFinished)
        {
            IsPlaying = true;
            Debug.Log($"[ChainDisaster] start, {route.Count} blasts toward area={zones.CurrentArea}");
            foreach (var p in facadeFires) FireFx.Spawn(fx, p, 1.6f);

            float interval = Mathf.Clamp(totalSeconds / (route.Count + 1), 0.12f, 0.5f);
            for (int i = 0; i < route.Count; i++)
            {
                Blast(route[i], i == 0 ? 1.6f : 1f, i % 3 == 0);
                yield return new WaitForSeconds(interval);
            }

            Blast(NearPlayer(route[route.Count - 1]), 1.3f, true);
            yield return new WaitForSeconds(endDelay);
            IsPlaying = false;
            onFinished?.Invoke();
        }

        private Vector3 NearPlayer(Vector3 from)
        {
            Vector3 target = player.position + Vector3.up;
            Vector3 dir = from - target;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return target;
            return target + dir.normalized * nearPlayerDistance;
        }

        private void Blast(Vector3 position, float scale, bool leaveFire)
        {
            ExplosionFx.Spawn(fx, position, scale);
            if (leaveFire) FireFx.Spawn(fx, position, scale);
            float distance = Vector3.Distance(position, player.position);
            shake.AddImpulse(Mathf.Clamp01(1f - distance / 30f) * 0.8f);
        }
    }
}
