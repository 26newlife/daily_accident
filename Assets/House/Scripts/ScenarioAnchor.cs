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

        // 시나리오 씬(Additive)의 앵커 (예: s01_radio) 는 HouseController.Awake 이후에 로드되므로, 켜질 때 스스로 등록한다
        private static readonly System.Collections.Generic.Dictionary<string, ScenarioAnchor> live = new System.Collections.Generic.Dictionary<string, ScenarioAnchor>();
        public static ScenarioAnchor FindLive(string anchorId) => live.TryGetValue(anchorId, out var a) && a != null ? a : null;
        private void OnEnable() { if (!string.IsNullOrEmpty(id)) live[id] = this; }
        private void OnDisable() { if (live.TryGetValue(id, out var a) && a == this) live.Remove(id); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => live.Clear();

        private void OnDrawGizmos() { Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f); Gizmos.DrawWireSphere(transform.position, 0.15f); }
    }
}
