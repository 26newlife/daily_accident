using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 집 곳곳의 이름 붙은 위치 (이펙트·NPC·소품을 배치할 때 시나리오가 id 로 찾는다).
    /// 예: anchor_living_center, anchor_kitchen_stove, anchor_balcony_window, anchor_entrance_door, anchor_corridor_elevator, anchor_stairwell_door, anchor_shelter_door.
    /// id 는 변경 금지 (모든 시나리오가 공유).
    /// </summary>
    public class ScenarioAnchor : MonoBehaviour
    {
        [SerializeField] private string id = "anchor_id";
        [SerializeField, TextArea(1, 2)] private string description;
        public string Id => id;
        public string Description => description;

        private void OnDrawGizmos() { Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f); Gizmos.DrawWireSphere(transform.position, 0.15f); }
    }
}
