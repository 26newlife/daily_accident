using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 플레이어 시작 위치 (시나리오가 ScenarioDefinition.startSpawnId 로 선택). 방향은 이 오브젝트의 Y 회전.
    /// 예: spawn_bedroom_bed, spawn_living_room, spawn_entrance, spawn_corridor, spawn_balcony, spawn_shelter. id 는 변경 금지.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string id = "spawn_id";
        public string Id => id;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.25f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 0.6f);
        }
    }
}
