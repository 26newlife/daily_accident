using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 대피공간(zone_shelter) 연기 유입 계산 (항상 활성).
    ///   원천 연기 = 집 전체 연기 / 거실 연기 중 큰 값
    ///   유입 비율 = 방화문 열림 → 1.0 / 닫힘 → Lerp(shelterUnsealedLeak, shelterSealedLeak, 4면 평균 밀봉률)
    ///   창문을 열면 신선한 공기로 체감 연기가 shelterWindowFactor 배로 줄어든다.
    /// 대피공간 구역은 격리(isolated)되어 안에서는 집 전체 연기를 무시하고 이 값만 체감한다.
    /// 호흡기가 0 이 되면 ShelterSmoke 사망(교육 피드백: 문틈을 막고 환기하라).
    /// </summary>
    public class ShelterSealSystem : MonoBehaviour
    {
        [SerializeField] private HazardZone shelterZone;
        [SerializeField] private ToggleInteractable fireDoor;
        [SerializeField] private ToggleInteractable window;
        [SerializeField] private SealPoint[] sealPoints;
        [SerializeField] private string sourceZoneId = EvacKeys.ZoneLiving;
        [SerializeField, Min(0.001f)] private float risePerSecond = 0.04f;
        [SerializeField, Min(0.001f)] private float fallPerSecond = 0.07f;

        public static ShelterSealSystem Instance { get; private set; }
        public float SealRatio { get; private set; }
        public float TargetSmoke { get; private set; }
        public bool DoorClosed => fireDoor != null && !fireDoor.IsOn;
        public bool WindowOpen => window != null && window.IsOn;
        public int SealedSides { get; private set; }

        private float current;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (shelterZone != null) shelterZone.Isolated = true;
        }

        private void Update()
        {
            if (GameFlow.GameplayBlocked || shelterZone == null) return;
            GameTimingConfig t = GameTimingConfig.Current;

            float sum = 0f;
            int sealedSides = 0;
            if (sealPoints != null && sealPoints.Length > 0)
            {
                foreach (SealPoint s in sealPoints) if (s != null) { sum += s.SealRatio; if (s.SealRatio > 0f) sealedSides++; }
                SealRatio = sum / sealPoints.Length;
            }
            SealedSides = sealedSides;

            HazardManager hz = HazardManager.Instance;
            float source = 0f;
            if (hz != null)
            {
                HazardZone living = hz.FindZone(sourceZoneId);
                source = Mathf.Max(hz.SmokeLevel, living != null ? living.SmokeLevel : 0f);
            }

            float leak = DoorClosed ? Mathf.Lerp(t.shelterUnsealedLeak, t.shelterSealedLeak, SealRatio) : 1f;
            float window01 = WindowOpen ? t.shelterWindowFactor : 1f;
            TargetSmoke = Mathf.Clamp01(source * leak * window01);

            current = Mathf.MoveTowards(current, TargetSmoke, (TargetSmoke > current ? risePerSecond : fallPerSecond) * Time.deltaTime);
            shelterZone.SetSmoke(current);
        }
    }
}
