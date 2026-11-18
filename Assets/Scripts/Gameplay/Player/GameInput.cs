using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameGameplay
{
    /// <summary>
    /// 게임플레이 입력 (New Input System). InputActions 에셋 없이 코드로 액션을 만들어
    /// 키보드+마우스 / 게임패드 바인딩을 한 곳에서 관리한다. 바인딩을 바꾸려면 아래 상수만 수정하면 된다.
    /// </summary>
    public class GameInput : MonoBehaviour
    {
        // ───────── 바인딩 (한 곳에서 관리) ─────────
        private const string KbUp = "<Keyboard>/w", KbDown = "<Keyboard>/s", KbLeft = "<Keyboard>/a", KbRight = "<Keyboard>/d";
        private const string PadMove = "<Gamepad>/leftStick", PadLook = "<Gamepad>/rightStick", MouseLook = "<Mouse>/delta";
        private const string KbSprint = "<Keyboard>/leftShift", PadSprint = "<Gamepad>/leftStickPress";
        private const string KbCrouch = "<Keyboard>/leftCtrl", PadCrouch = "<Gamepad>/buttonEast";
        private const string KbInteract = "<Keyboard>/e", PadInteract = "<Gamepad>/buttonSouth";
        private const string MouseSpray = "<Mouse>/leftButton", PadSpray = "<Gamepad>/rightTrigger";
        private const string KbPause = "<Keyboard>/escape", PadPause = "<Gamepad>/start";
        private const string KbPhone = "<Keyboard>/tab", PadPhone = "<Gamepad>/select";
        private const string KbFlashlight = "<Keyboard>/f", PadFlashlight = "<Gamepad>/dpad/up";
        private const string KbInvUse = "<Keyboard>/q", PadInvUse = "<Gamepad>/buttonWest";
        private const string KbInvNext = "<Keyboard>/rightBracket", PadInvNext = "<Gamepad>/dpad/right";
        private const string KbInvPrev = "<Keyboard>/leftBracket", PadInvPrev = "<Gamepad>/dpad/left";
        private static readonly string[] InvSlotKeys =
            { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4", "<Keyboard>/5", "<Keyboard>/6", "<Keyboard>/7", "<Keyboard>/8" };

        private InputAction spray, move, look, sprint, crouch, interact, pause, phone, flashlight, invUse, invNext, invPrev, invSlot;

        /// <summary>마지막으로 입력이 들어온 장치가 게임패드인가 (프롬프트 키 표시용)</summary>
        public static bool UsingGamepad { get; private set; }

        public Vector2 Move => move.ReadValue<Vector2>();
        public Vector2 Look => look.ReadValue<Vector2>();
        public bool LookIsGamepad => look.activeControl != null && look.activeControl.device is Gamepad;
        public bool SprintHeld => sprint.IsPressed();
        public bool CrouchHeld => crouch.IsPressed();
        public bool InteractHeld => interact.IsPressed();
        public bool SprayHeld => spray.IsPressed();

        public event Action InteractPressed;
        public event Action PausePressed;
        public event Action PhonePressed;
        public event Action FlashlightPressed;
        public event Action InventoryUsePressed;
        public event Action<int> InventoryCycle;        // -1 / +1
        public event Action<int> InventorySlotPressed;  // 슬롯 인덱스(0~)

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", KbUp).With("Down", KbDown).With("Left", KbLeft).With("Right", KbRight);
            move.AddBinding(PadMove);

            look = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            look.AddBinding(MouseLook);
            look.AddBinding(PadLook);

            sprint = Button("Sprint", KbSprint, PadSprint);
            crouch = Button("Crouch", KbCrouch, PadCrouch);
            interact = Button("Interact", KbInteract, PadInteract);
            spray = Button("Spray", MouseSpray, PadSpray);
            pause = Button("Pause", KbPause, PadPause);
            phone = Button("Phone", KbPhone, PadPhone);
            flashlight = Button("Flashlight", KbFlashlight, PadFlashlight);
            invUse = Button("InventoryUse", KbInvUse, PadInvUse);
            invNext = Button("InventoryNext", KbInvNext, PadInvNext);
            invPrev = Button("InventoryPrev", KbInvPrev, PadInvPrev);

            invSlot = new InputAction("InventorySlot", InputActionType.Button);
            foreach (string key in InvSlotKeys) invSlot.AddBinding(key);

            interact.performed += _ => { UsingGamepad = IsPad(_); InteractPressed?.Invoke(); };
            pause.performed += _ => { UsingGamepad = IsPad(_); PausePressed?.Invoke(); };
            phone.performed += _ => { UsingGamepad = IsPad(_); PhonePressed?.Invoke(); };
            flashlight.performed += _ => FlashlightPressed?.Invoke();
            invUse.performed += _ => InventoryUsePressed?.Invoke();
            invNext.performed += _ => InventoryCycle?.Invoke(1);
            invPrev.performed += _ => InventoryCycle?.Invoke(-1);
            invSlot.performed += ctx =>
            {
                int index = ctx.action.GetBindingIndexForControl(ctx.control);
                if (index >= 0) InventorySlotPressed?.Invoke(index);
            };
        }

        private static InputAction Button(string name, string keyboard, string pad)
        {
            var a = new InputAction(name, InputActionType.Button);
            a.AddBinding(keyboard);
            a.AddBinding(pad);
            return a;
        }

        private static bool IsPad(InputAction.CallbackContext ctx) => ctx.control != null && ctx.control.device is Gamepad;

        private void OnEnable() { foreach (InputAction a in All()) a.Enable(); }
        private void OnDisable() { foreach (InputAction a in All()) a.Disable(); }

        private void OnDestroy() { foreach (InputAction a in All()) a.Dispose(); }

        private void Update()
        {
            InputControl c = move.activeControl ?? look.activeControl;
            if (c != null) UsingGamepad = c.device is Gamepad;
        }

        private InputAction[] All() => new[] { spray, move, look, sprint, crouch, interact, pause, phone, flashlight, invUse, invNext, invPrev, invSlot };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => UsingGamepad = false;
    }
}
