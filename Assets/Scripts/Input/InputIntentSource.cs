using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    public class InputIntentSource : MonoBehaviour
    {
        public float lookSensitivity = 1.0f;

        [Header("Actions")]
        public InputIntent intent = new InputIntent();
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference rightClickAction;
        public InputActionReference leftClickAction;
        public InputActionReference pointAction;
        public InputActionReference zoomAction;

        public InputActionReference shiftAction;
        public InputActionReference ctrlAction;
        public InputActionReference cancelAction;
        public InputActionReference spaceAction;
        public InputActionReference dashAction;
        public InputActionReference attackAction;

        [Header("Command Actions (Optional)")]
        public InputActionReference commandQAction;
        public InputActionReference commandWAction;
        public InputActionReference commandEAction;
        public InputActionReference commandRAction;
        public InputActionReference commandAAction;
        public InputActionReference commandSAction;
        public InputActionReference commandDAction;
        public InputActionReference commandFAction;
        public InputActionReference commandZAction;
        public InputActionReference commandXAction;
        public InputActionReference commandCAction;
        public InputActionReference commandVAction;
        public InputActionReference commandMAction;

        public InputIntent Current { get; private set; }

        InputAction _move;
        InputAction _look;
        InputAction _rightClick;
        InputAction _leftClick;
        InputAction _point;
        InputAction _zoom;
        InputAction _shift;
        InputAction _ctrl;
        InputAction _cancel;
        InputAction _space;
        InputAction _dash;
        InputAction _attack;

        InputAction _commandQ;
        InputAction _commandW;
        InputAction _commandE;
        InputAction _commandR;
        InputAction _commandA;
        InputAction _commandS;
        InputAction _commandD;
        InputAction _commandF;
        InputAction _commandZ;
        InputAction _commandX;
        InputAction _commandC;
        InputAction _commandV;
        InputAction _commandM;

        void Awake()
        {
            _move = moveAction?.action;
            _look = lookAction?.action;
            _rightClick = rightClickAction?.action;
            _leftClick = leftClickAction?.action;
            _point = pointAction?.action;
            _zoom = zoomAction?.action;

            _shift = shiftAction?.action;
            _ctrl = ctrlAction?.action;
            _cancel = cancelAction?.action;
            _space = spaceAction?.action;
            _dash = dashAction?.action;
            _attack = attackAction?.action;

            _commandQ = commandQAction?.action;
            _commandW = commandWAction?.action;
            _commandE = commandEAction?.action;
            _commandR = commandRAction?.action;
            _commandA = commandAAction?.action;
            _commandS = commandSAction?.action;
            _commandD = commandDAction?.action;
            _commandF = commandFAction?.action;
            _commandZ = commandZAction?.action;
            _commandX = commandXAction?.action;
            _commandC = commandCAction?.action;
            _commandV = commandVAction?.action;
            _commandM = commandMAction?.action;
        }

        void OnEnable()
        {
            _move?.Enable();
            _look?.Enable();
            _rightClick?.Enable();
            _leftClick?.Enable();
            _point?.Enable();
            _zoom?.Enable();
            _shift?.Enable();
            _ctrl?.Enable();
            _cancel?.Enable();
            _space?.Enable();
            _dash?.Enable();
            _attack?.Enable();

            _commandQ?.Enable();
            _commandW?.Enable();
            _commandE?.Enable();
            _commandR?.Enable();
            _commandA?.Enable();
            _commandS?.Enable();
            _commandD?.Enable();
            _commandF?.Enable();
            _commandZ?.Enable();
            _commandX?.Enable();
            _commandC?.Enable();
            _commandV?.Enable();
            _commandM?.Enable();

            if (_space != null) _space.started += OnJumpStarted;
            if (_dash != null) _dash.started += OnDashStarted;
        }

        void OnDisable()
        {
            if (_space != null) _space.started -= OnJumpStarted;
            if (_dash != null) _dash.started -= OnDashStarted;

            _move?.Disable();
            _look?.Disable();
            _rightClick?.Disable();
            _leftClick?.Disable();
            _point?.Disable();
            _zoom?.Disable();
            _shift?.Disable();
            _ctrl?.Disable();
            _cancel?.Disable();
            _space?.Disable();
            _dash?.Disable();
            _attack?.Disable();

            _commandQ?.Disable();
            _commandW?.Disable();
            _commandE?.Disable();
            _commandR?.Disable();
            _commandA?.Disable();
            _commandS?.Disable();
            _commandD?.Disable();
            _commandF?.Disable();
            _commandZ?.Disable();
            _commandX?.Disable();
            _commandC?.Disable();
            _commandV?.Disable();
            _commandM?.Disable();
        }

        void Update()
        {
            intent.Move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

            Vector2 rawLook = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
            intent.Look = rawLook * lookSensitivity;

            if (_rightClick != null)
            {
                intent.RightClick = _rightClick.WasPressedThisFrame();
                intent.RightHeld = _rightClick.IsPressed();
            }
            else
            {
                intent.RightClick = false;
                intent.RightHeld = false;
            }

            if (_leftClick != null)
            {
                intent.LeftClick = _leftClick.WasPressedThisFrame();
                intent.LeftHeld = _leftClick.IsPressed();
            }
            else
            {
                intent.LeftClick = false;
                intent.LeftHeld = false;
            }

            if (_attack != null)
                intent.AttackPressed = _attack.WasPressedThisFrame();
            else
                intent.AttackPressed = false;

            intent.PointerScreenPos = _point != null ? _point.ReadValue<Vector2>() : Vector2.zero;
            intent.Zoom = _zoom != null ? _zoom.ReadValue<Vector2>().y : 0f;

            bool shiftFromAction = _shift != null && _shift.IsPressed();
            bool shiftFromKeyboard = Keyboard.current != null &&
                                     (Keyboard.current.leftShiftKey.isPressed ||
                                      Keyboard.current.rightShiftKey.isPressed);
            intent.Shift = shiftFromAction || shiftFromKeyboard;
            bool ctrlFromAction = _ctrl != null && _ctrl.IsPressed();
            bool ctrlFromKeyboard = Keyboard.current != null &&
                                    (Keyboard.current.leftCtrlKey.isPressed ||
                                     Keyboard.current.rightCtrlKey.isPressed);
            intent.Ctrl = ctrlFromAction || ctrlFromKeyboard;
            intent.Cancel = _cancel != null && _cancel.WasPressedThisFrame();
            intent.SpaceHeld = _space != null && _space.IsPressed();
            if (_space != null && _space.WasPressedThisFrame()) intent.Space = true;
            if (_dash != null && _dash.WasPressedThisFrame()) intent.Dash = true;

            intent.CommandQ = _commandQ != null && _commandQ.WasPressedThisFrame();
            intent.CommandW = _commandW != null && _commandW.WasPressedThisFrame();
            intent.CommandE = _commandE != null && _commandE.WasPressedThisFrame();
            intent.CommandR = _commandR != null && _commandR.WasPressedThisFrame();
            intent.CommandA = _commandA != null && _commandA.WasPressedThisFrame();
            intent.CommandS = _commandS != null && _commandS.WasPressedThisFrame();
            intent.CommandD = _commandD != null && _commandD.WasPressedThisFrame();
            intent.CommandF = _commandF != null && _commandF.WasPressedThisFrame();
            intent.CommandZ = _commandZ != null && _commandZ.WasPressedThisFrame();
            intent.CommandX = _commandX != null && _commandX.WasPressedThisFrame();
            intent.CommandC = _commandC != null && _commandC.WasPressedThisFrame();
            intent.CommandV = _commandV != null && _commandV.WasPressedThisFrame();
            intent.CommandM = _commandM != null && _commandM.WasPressedThisFrame();

            Current = intent;
        }

        void LateUpdate()
        {
            intent.ClearOneFrameActions();
        }

        public string GetCommandBindingDisplayString(string hotkeyToken)
        {
            InputAction action = GetCommandAction(hotkeyToken);
            if (action == null)
                return CommandHotkeyUtility.NormalizeToken(hotkeyToken);

            string display = action.GetBindingDisplayString();
            return string.IsNullOrWhiteSpace(display)
                ? CommandHotkeyUtility.NormalizeToken(hotkeyToken)
                : display;
        }

        InputAction GetCommandAction(string hotkeyToken)
        {
            switch (CommandHotkeyUtility.NormalizeToken(hotkeyToken))
            {
                case "Q": return _commandQ;
                case "W": return _commandW;
                case "E": return _commandE;
                case "R": return _commandR;
                case "A": return _commandA;
                case "S": return _commandS;
                case "D": return _commandD;
                case "F": return _commandF;
                case "Z": return _commandZ;
                case "X": return _commandX;
                case "C": return _commandC;
                case "V": return _commandV;
                case "M": return _commandM;
                default: return null;
            }
        }

        void OnJumpStarted(InputAction.CallbackContext ctx) => intent.Space = true;
        void OnDashStarted(InputAction.CallbackContext ctx) => intent.Dash = true;
    }
}
