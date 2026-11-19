using GameCore;
using UnityEngine;

namespace GameGameplay
{
    /// <summary>
    /// 엘리베이터 내부 조작 패널 버튼. id: elevator_btn_floor_01 ~ _20 / elevator_btn_open / elevator_btn_close / elevator_btn_alarm / elevator_exit.
    /// 항상 상호작용 가능 — 결과(반응 없음 포함)는 ElevatorController 가 정한다. 눌린 층 버튼은 불이 들어온다.
    /// </summary>
    public class ElevatorButton : InteractableObject
    {
        public enum ButtonKind { Floor = 0, Open = 1, Close = 2, Alarm = 3, Exit = 4 }

        [Header("엘리베이터 버튼")]
        [SerializeField] private ButtonKind kind = ButtonKind.Floor;
        [SerializeField, Range(1, 20)] private int floor = 1;
        [SerializeField] private Renderer lamp;
        [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private Color unlitColor = new Color(0.75f, 0.77f, 0.8f);

        private MaterialPropertyBlock block;
        private bool lit;

        public ButtonKind Kind => kind;
        public int Floor => floor;
        public bool IsLit => lit;

        protected override void OnInteract(PlayerInteractor interactor)
        {
            ElevatorController c = ElevatorController.Instance;
            if (c != null) c.PressButton(this);
            Publish(ScenarioEventType.Interacted);
        }

        public void SetLit(bool on)
        {
            lit = on;
            if (lamp == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color col = on ? litColor : unlitColor;
            lamp.GetPropertyBlock(block);
            block.SetColor("_BaseColor", col);
            block.SetColor("_Color", col);
            block.SetColor("_EmissionColor", on ? litColor * 1.5f : Color.black);
            lamp.SetPropertyBlock(block);
        }
    }
}
