using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 현관문 도어 스코프(외시경). 문 안쪽 눈높이에 붙은 작은 렌즈이며 문과 함께 움직인다(문이 열리면 사용 불가).
    /// 상호작용하면 PeepholeCameraController 가 카메라를 렌즈 위치로 옮겨 복도를 보여 준다.
    /// 문구멍으로 복도를 본 사실만 ScoreSystem 에 "기록"한다 (문 너머의 열기는 알 수 없으므로 손잡이 확인과는 별개).
    /// </summary>
    public class DoorPeephole : InteractableObject
    {
        [Header("문구멍")]
        [SerializeField, Tooltip("카메라가 놓일 위치(렌즈). 비우면 이 오브젝트의 위치")] private Transform viewPoint;
        [SerializeField, Tooltip("렌즈가 붙은 문. 비우면 부모에서 찾는다")] private ToggleInteractable door;
        [SerializeField, Tooltip("문밖 쪽으로 카메라를 내보내는 거리(m) — 문짝 안쪽에 파묻히지 않게")] private float outsideOffset = 0.07f;

        [Header("기록 (점수는 올리지 않고 기록만)")]
        [SerializeField] private string recordKey = "peephole_checked";
        [SerializeField] private string recordSmokeKey = "peephole_corridor_smoke";

        private PeepholeCameraController controller;

        public Transform ViewPoint => viewPoint != null ? viewPoint : transform;
        /// <summary>문밖(복도) 방향 = 이 오브젝트의 forward</summary>
        public Vector3 OutDirection => transform.forward;
        public Vector3 CameraPosition => ViewPoint.position + OutDirection * outsideOffset;

        private void Awake()
        {
            if (door == null) door = GetComponentInParent<ToggleInteractable>();
        }

        public override bool CanInteract(PlayerInteractor interactor)
        {
            if (!base.CanInteract(interactor)) return false;
            if (door != null && door.IsOn) return false;                  // 문이 열려 있으면 문구멍을 쓸 수 없다
            if (controller == null) controller = FindFirstObjectByType<PeepholeCameraController>();
            return controller != null && !PeepholeCameraController.IsActive;
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            if (controller == null) return;
            controller.Enter(this);
            Publish(ScenarioEventType.Interacted);
            if (ScoreSystem.Instance != null)
            {
                ScoreSystem.Instance.Increment(recordKey);
                HazardZone z = HazardManager.Instance != null ? HazardManager.Instance.FindZone("zone_corridor") : null;
                ScoreSystem.Instance.Record(recordSmokeKey, z != null ? z.SmokeLevel : 0f);
            }
        }
    }
}
