using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 아파트 단지 상공을 지나는 전투기 편대와 헬기 편대를 주기적으로 생성·이동시킨다.
    /// 경로는 씬의 웨이포인트(Transform 자식들)이고, 기체는 프리팹(S01_Fighter / S01_Helicopter)을 풀링해 쓴다. 주기·속도·편대 규모는 WarConfig.
    /// 편대는 경로를 좌우로 평행 이동시킨 선을 따라, 뒤쪽 기체일수록 늦게 출발해 V 자 대형을 이룬다.
    /// </summary>
    public class WarFlyovers : MonoBehaviour
    {
        [SerializeField] private WarAircraft jetPrefab, heliPrefab;
        [SerializeField, Tooltip("각 항목 = 웨이포인트를 자식으로 가진 Transform")] private Transform[] jetRoutes, heliRoutes;

        private readonly List<WarAircraft> pool = new List<WarAircraft>();
        private readonly List<WarAircraft> flying = new List<WarAircraft>();
        private int token, jetCount, heliCount;
        private bool running;

        public int FlightsSpawned { get; private set; }
        public bool Running => running;

        public void StartFlights(float startDelay)
        {
            WarConfig c = WarState.Instance != null ? WarState.Instance.Config : null;
            if (c == null || running) return;
            running = true;
            int t = ++token;
            WarTime.After(startDelay, () => LaunchJets(t));
            WarTime.After(startDelay + 14f, () => LaunchHelis(t));
        }

        public void StopFlights()
        {
            running = false; token++;
            foreach (WarAircraft a in new List<WarAircraft>(flying)) a.Abort();
        }

        private static float Rand(Vector2 r) => Random.Range(r.x, r.y);

        private void LaunchJets(int t)
        {
            if (!running || token != t || GameFlow.IsEnded) return;
            WarConfig c = WarState.Instance.Config;
            Launch(jetPrefab, jetRoutes, jetCount++, c.jetsPerFlight, c.jetSpeed, c.formationSpacing, true);
            WarTime.After(Rand(c.jetInterval), () => LaunchJets(t));
        }

        private void LaunchHelis(int t)
        {
            if (!running || token != t || GameFlow.IsEnded) return;
            WarConfig c = WarState.Instance.Config;
            Launch(heliPrefab, heliRoutes, heliCount++, c.helisPerFlight, c.heliSpeed, c.formationSpacing, false);
            WarTime.After(Rand(c.heliInterval), () => LaunchHelis(t));
        }

        private void Launch(WarAircraft prefab, Transform[] routes, int index, int count, float speed, float spacing, bool jet)
        {
            if (prefab == null || routes == null || routes.Length == 0) return;
            Transform route = routes[index % routes.Length];
            var pts = new List<Vector3>();
            foreach (Transform w in route) pts.Add(w.position);
            if (pts.Count < 2) return;
            Vector3 fwd = (pts[pts.Count - 1] - pts[0]).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            AudioClip clip = WarAudio.Instance != null ? WarAudio.Instance.GetClip(jet ? WarSound.Jet : WarSound.Rotor) : null;
            float vol = WarState.Instance.Config.flyoverVolume;
            for (int k = 0; k < count; k++)
            {
                float off = k - (count - 1) * 0.5f;
                float lateral = off * spacing;
                float back = Mathf.Abs(off) * spacing * (jet ? 0.35f : 0.8f);   // 가운데가 선두인 V 자 대형
                var path = new Vector3[pts.Count];
                for (int i = 0; i < pts.Count; i++) path[i] = pts[i] + side * lateral + Vector3.up * (jet ? 0f : (k % 2) * 3f);
                WarAircraft a = Get(prefab);
                a.Fly(path, speed, back / speed + k * 0.15f, vol, clip, OnDone);
                flying.Add(a);
            }
            FlightsSpawned++;
        }

        private WarAircraft Get(WarAircraft prefab)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].Kind == prefab.Kind) { WarAircraft a = pool[i]; pool.RemoveAt(i); return a; }
            WarAircraft inst = Instantiate(prefab, transform);
            inst.name = prefab.name + "_Instance";
            return inst;
        }

        private void OnDone(WarAircraft a) { flying.Remove(a); pool.Add(a); }
    }
}
