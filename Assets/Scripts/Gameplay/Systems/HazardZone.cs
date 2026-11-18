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

        public string ZoneId => zoneId;
        public float GasLevel => gasLevel;
        public float SmokeLevel => smokeLevel;
        public float Temperature => temperature;

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
            if (HazardManager.Instance != null && other.GetComponent<CharacterController>() != null)
                HazardManager.Instance.PlayerEntered(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (HazardManager.Instance != null && other.GetComponent<CharacterController>() != null)
                HazardManager.Instance.PlayerExited(this);
        }
    }
}
