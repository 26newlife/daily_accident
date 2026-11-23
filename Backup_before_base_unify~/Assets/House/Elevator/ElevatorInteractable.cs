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

        private static readonly System.Collections.Generic.List<ElevatorInteractable> all = new System.Collections.Generic.List<ElevatorInteractable>();
        private MaterialPropertyBlock block;
        protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
        protected override void OnDisable() { base.OnDisable(); all.Remove(this); }

        /// <summary>호출 버튼(CallButton 역할)에 불을 켜고 끈다 — 호출 후 도착까지 점등</summary>
        public static void SetCallButtonsLit(bool on)
        {
            foreach (ElevatorInteractable e in all)
            {
                if (e == null || e.role != PartRole.CallButton) continue;
                if (e.block == null) e.block = new MaterialPropertyBlock();
                Color c = on ? new Color(1f, 0.8f, 0.3f) : new Color(0.7f, 0.7f, 0.72f);
                foreach (Renderer r in e.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(e.block);
                    e.block.SetColor("_BaseColor", c);
                    e.block.SetColor("_Color", c);
                    e.block.SetColor("_EmissionColor", on ? c * 1.5f : Color.black);
                    r.SetPropertyBlock(e.block);
                }
            }
        }

        /// <summary>Door 전용: 문 열림/닫힘 외형 전환</summary>
        public void SetOpen(bool open)
        {
            if (closedVisual != null) closedVisual.SetActive(!open);
            if (cabinObject != null) cabinObject.SetActive(open);
        }

        protected override void OnInteract(PlayerInteractor interactor)
        {
            ElevatorController ctl = ElevatorController.Instance;
            if (ctl != null) { if (role == PartRole.Cabin) ctl.Board(); else ctl.Call(transform); }
            else Debug.LogWarning("[Elevator] ElevatorController 가 씬에 없습니다.");
            Publish(ScenarioEventType.Interacted);
        }
    }
}
