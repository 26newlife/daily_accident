using UnityEngine;

namespace GameGameplay
{
    /// <summary>창문 앞 풍경 구역. 플레이어가 가까이 있으면 근경 보행자를 상세 모드로 갱신한다. 창에서 바라보는 방향도 기록한다.</summary>
    public class WindowViewZone : MonoBehaviour
    {
        [SerializeField] private string windowId = "window_living";
        [SerializeField, Tooltip("창밖을 향한 방향 (월드)")] private Vector3 outward = Vector3.forward;
        [SerializeField, Min(0.5f)] private float activateDistance = 4f;
        [SerializeField, Tooltip("창에서 보이는 풍경 설명 (인스펙터 메모)"), TextArea(1, 2)] private string viewNote;

        public string WindowId => windowId;
        public Vector3 Outward => outward.normalized;
        public float ActivateDistance => activateDistance;

        public bool IsPlayerNear(Vector3 playerPos) => (transform.position - playerPos).sqrMagnitude <= activateDistance * activateDistance;
    }
}
