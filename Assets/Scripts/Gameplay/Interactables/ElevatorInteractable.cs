using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 상호작용 3종 (항상 상호작용 가능):
    ///   CallButton(elevator_call_button) / Door(elevator_door) = 호출,  Cabin(elevator_cabin) = 열린 문으로 탑승.
    /// 결과는 ElevatorHazardSystem 이 평상시(정상 작동)와 화재 중(확률적 위험)으로 나누어 처리한다.
    /// </summary>
    public class ElevatorInteractable : InteractableObject
    {
        public enum PartRole { CallButton = 0, Door = 1, Cabin = 2 }

        [Header("엘리베이터")]
        [SerializeField] private PartRole role = PartRole.CallButton;
        [SerializeField, Tooltip("Door 전용: 문이 닫혀 있을 때 보이는 문짝 (열리면 숨김)")] private GameObject closedVisual;
        [SerializeField, Tooltip("Door 전용: 문이 열리면 나타나는 탑승 칸 오브젝트 (elevator_cabin)")] private GameObject cabinObject;

        public PartRole Role => role;

        /// <summary>Door 전용: 문 열림/닫힘 외형 전환</summary>
        public void SetOpen(bool open)
        {
            if (closedVisual != null) closedVisual.SetActive(!open);
            if (cabinObject != null) cabinObject.SetActive(open);
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            ElevatorHazardSystem sys = ElevatorHazardSystem.GetOrCreate();
            if (role == PartRole.Cabin) sys.Board();
            else sys.CallElevator(transform);
            Publish(ScenarioEventType.Interacted);
        }
    }
}
