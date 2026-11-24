using System;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>플레이어가 지금 있는 구역 (원본 PlayerArea + 옥상)</summary>
    public enum QuakeArea
    {
        Unknown = 0,
        Apartment = 1,   // 10층 집 안 (1004호)
        Hallway = 2,     // 10층 복도
        Stairwell = 3,   // 계단실 (10층 입구 + 계단탑 1~20층·옥상층)
        Outside = 4,     // 건물 밖 지면
        Shelter = 5,     // 뒷산 지진해일 대피소 (고지대)
        OutdoorSite = 6, // 지진 옥외대피장소 (단지 놀이터 옆 공터)
        Rooftop = 7,     // 옥상
    }

    public static class QuakeAreaExtensions
    {
        /// <summary>지붕 없는 바깥 (비를 맞는 곳)</summary>
        public static bool IsOutdoors(this QuakeArea a) => a == QuakeArea.Outside || a == QuakeArea.Shelter || a == QuakeArea.OutdoorSite || a == QuakeArea.Rooftop;
        /// <summary>대피지역: 이 안에서는 어떤 사고로도 죽지 않고 결과에서 감점된다</summary>
        public static bool IsEvacuationSite(this QuakeArea a) => a == QuakeArea.Shelter || a == QuakeArea.OutdoorSite;
        /// <summary>폭발·붕괴가 덮치는 건물 안 (옥상 포함)</summary>
        public static bool IsInsideBuilding(this QuakeArea a) => a == QuakeArea.Apartment || a == QuakeArea.Hallway || a == QuakeArea.Stairwell || a == QuakeArea.Rooftop;
    }

    /// <summary>
    /// 구역 판정 (원본 ZoneSensor 이식). 트리거 대신 좌표 범위로 판정한다 — 집 좌표는 BaseHouseSpec / HouseStairwellBuilder 기준이고,
    /// 대피소·옥외대피장소 범위는 시나리오 빌더가 채운다. 경계를 벗어나면(어느 구역도 아님) 이전 구역을 유지한다.
    /// </summary>
    public class QuakeAreas : MonoBehaviour
    {
        [SerializeField] private Bounds shelter = new Bounds(new Vector3(-15f, -12f, -100f), new Vector3(46f, 8f, 30f));
        [SerializeField] private Bounds outdoorSite = new Bounds(new Vector3(-70f, -27.5f, 56f), new Vector3(44f, 4f, 24f));
        [SerializeField, Tooltip("우리 동 바닥 범위 (x, z) — 여진 때 외벽 거리 계산용")] private Rect buildingFootprint = new Rect(-24.2f, -4f, 34.2f, 13.4f);

        public static QuakeAreas Instance { get; private set; }
        public QuakeArea Current { get; private set; } = QuakeArea.Unknown;
        public event Action<QuakeArea> AreaChanged;
        public Bounds ShelterBounds => shelter;
        public Bounds OutdoorSiteBounds => outdoorSite;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            PlayerController p = QuakeTimeline.Instance != null ? QuakeTimeline.Instance.Player : FindFirstObjectByType<PlayerController>();
            if (p == null) return;
            QuakeArea a = Classify(p.transform.position);
            if (a == QuakeArea.Unknown || a == Current) return;
            Current = a;
            Debug.Log($"[Quake] Area -> {a}");
            AreaChanged?.Invoke(a);
        }

        public QuakeArea Classify(Vector3 feet)
        {
            Vector3 probe = feet + Vector3.up * 0.3f;
            if (shelter.Contains(probe)) return QuakeArea.Shelter;
            if (outdoorSite.Contains(probe)) return QuakeArea.OutdoorSite;
            float x = feet.x, y = feet.y, z = feet.z;
            if (x > -24.3f && x < -13.9f && z > -3.4f && z < 0.25f && y > -29f && y < 36.5f) return QuakeArea.Stairwell;
            if (y > 32.3f && x > -18.1f && x < 10.1f && z > -4.1f && z < 9.5f) return QuakeArea.Rooftop;
            if (y > -0.6f && y < 2.6f)
            {
                if (x > -0.1f && x < 7.3f && z > -0.05f && z < 9.4f) return QuakeArea.Apartment;
                if (x > -14.1f && x < 9.7f && z > -2.5f && z < 0.1f) return QuakeArea.Hallway;
            }
            if (y < -20f) return QuakeArea.Outside;
            return QuakeArea.Unknown;
        }

        /// <summary>우리 동 외벽(계단탑 포함)까지의 수평 거리 (안에 있으면 0)</summary>
        public float DistanceToBuilding(Vector3 p)
        {
            float dx = Mathf.Max(buildingFootprint.xMin - p.x, 0f, p.x - buildingFootprint.xMax);
            float dz = Mathf.Max(buildingFootprint.yMin - p.z, 0f, p.z - buildingFootprint.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public Vector3 BuildingCenter => new Vector3(buildingFootprint.center.x, -14f, buildingFootprint.center.y);

        public void SetBounds(Bounds shelterBounds, Bounds siteBounds) { shelter = shelterBounds; outdoorSite = siteBounds; }
    }
}
