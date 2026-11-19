using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 방 단위 위험 구역(트리거 볼륨). 플레이어가 안에 있으면 HazardManager 가 이 구역의 값을 체감 값에 반영한다.
    /// 값은 Act 로직이 SetValues 로 바꾼다. (예: 주방 가스 누출 → zone_kitchen.GasLevel = 0.6)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HazardZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = "zone_id";
        [SerializeField, Range(0f, 1f)] private float gasLevel;
        [SerializeField, Range(0f, 1f)] private float smokeLevel;
        [SerializeField] private float temperature = 24f;

        [Header("격리 / 자세 / 사망 사유 (Act 3~4 구역용)")]
        [SerializeField, Tooltip("true 면 플레이어가 이 구역 안에 있을 때 전역(집 전체) 연기를 무시하고 이 구역의 값만 체감한다 (대피공간, 층 계단참 등)")] private bool isolated;
        [SerializeField, Range(0f, 1f), Tooltip("웅크렸을 때 체감 연기 배율 — 낮은 자세로 연기층 아래를 지나는 구간(루트 D)에서 0.2 정도로 둔다")] private float crouchSmokeScale = 1f;
        [SerializeField, Tooltip("이 구역의 연기로 호흡기 게이지가 0 이 되면 사용할 사망 사유 (Suffocation = 기본)")] private DeathReason suffocationReason = DeathReason.Suffocation;

        public string ZoneId => zoneId;
        public float GasLevel => gasLevel;
        public float SmokeLevel => smokeLevel;
        public float Temperature => temperature;
        public bool Isolated { get => isolated; set => isolated = value; }
        /// <summary>침수 등 확장용 수위(0~1). 시나리오가 HouseController.SetZoneWater 로 설정하고 필요한 곳에서 읽는다</summary>
        public float WaterLevel { get; private set; }
        public void SetWater(float level) { WaterLevel = Mathf.Clamp01(level); if (HazardManager.Instance != null) HazardManager.Instance.NotifyZoneValuesChanged(); }
        /// <summary>시나리오가 런타임에 호흡기 사망 사유를 지정한다 (집 구역을 시나리오 규칙에 맞춤)</summary>
        public void SetSuffocationReason(DeathReason reason) { suffocationReason = reason; }
        public void SetCrouchSmokeScale(float scale) { crouchSmokeScale = Mathf.Clamp01(scale); }
        public float CrouchSmokeScale => crouchSmokeScale;
        public DeathReason SuffocationReason => suffocationReason;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            if (HazardManager.Instance != null) HazardManager.Instance.Register(this);
        }

        private void Start()
        {
            // HazardManager 가 늦게 깨어난 경우를 대비
            if (HazardManager.Instance != null) HazardManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (HazardManager.Instance != null) HazardManager.Instance.Unregister(this);
        }

        /// <summary>월드 좌표가 이 구역(트리거 콜라이더) 안에 있는가 — 순간이동 뒤 구역 재동기화, NPC 위치의 연기 판정에 쓴다</summary>
        public bool Contains(Vector3 worldPoint)
        {
            var col = GetComponent<Collider>();
            return col != null && col.enabled && col.bounds.Contains(worldPoint);
        }

        public void SetValues(float gas, float smoke, float temp)
        {
            gasLevel = Mathf.Clamp01(gas);
            smokeLevel = Mathf.Clamp01(smoke);
            temperature = temp;
            if (HazardManager.Instance != null) HazardManager.Instance.NotifyZoneValuesChanged();
        }

        public void SetSmoke(float smoke) => SetValues(gasLevel, smoke, temperature);
        public void SetGas(float gas) => SetValues(gas, smokeLevel, temperature);

        private void OnTriggerEnter(Collider other)
        {
            if (HazardManager.Instance != null && other.GetComponent<PlayerController>() != null)   // NPC(CharacterController)는 제외
                HazardManager.Instance.PlayerEntered(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (HazardManager.Instance != null && other.GetComponent<PlayerController>() != null)
                HazardManager.Instance.PlayerExited(this);
        }
    }
}
