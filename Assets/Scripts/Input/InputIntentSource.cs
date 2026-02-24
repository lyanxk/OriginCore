using UnityEngine;
using UnityEngine.InputSystem;

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

    public InputIntent Current { get; private set; }

    InputAction _move;
    InputAction _look;
    InputAction _rightClick;
    InputAction _leftClick;
    InputAction _point;
    InputAction _zoom;
    InputAction _shift;
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

    void Awake()
    {
        _move = moveAction?.action;
        _look = lookAction?.action;
        _rightClick = rightClickAction?.action;
        _leftClick = leftClickAction?.action;
        _point = pointAction?.action;
        _zoom = zoomAction?.action;

        _shift = shiftAction?.action;
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
        intent.Cancel = _cancel != null && _cancel.WasPressedThisFrame();

        bool qFromAction = _commandQ != null && _commandQ.WasPressedThisFrame();
        bool wFromAction = _commandW != null && _commandW.WasPressedThisFrame();
        bool eFromAction = _commandE != null && _commandE.WasPressedThisFrame();
        bool rFromAction = _commandR != null && _commandR.WasPressedThisFrame();
        bool aFromAction = _commandA != null && _commandA.WasPressedThisFrame();
        bool sFromAction = _commandS != null && _commandS.WasPressedThisFrame();
        bool dFromAction = _commandD != null && _commandD.WasPressedThisFrame();
        bool fFromAction = _commandF != null && _commandF.WasPressedThisFrame();
        bool zFromAction = _commandZ != null && _commandZ.WasPressedThisFrame();
        bool xFromAction = _commandX != null && _commandX.WasPressedThisFrame();
        bool cFromAction = _commandC != null && _commandC.WasPressedThisFrame();
        bool vFromAction = _commandV != null && _commandV.WasPressedThisFrame();

        bool qFromKeyboard = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        bool wFromKeyboard = Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame;
        bool eFromKeyboard = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool rFromKeyboard = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool aFromKeyboard = Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
        bool sFromKeyboard = Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame;
        bool dFromKeyboard = Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame;
        bool fFromKeyboard = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
        bool zFromKeyboard = Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;
        bool xFromKeyboard = Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
        bool cFromKeyboard = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        bool vFromKeyboard = Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;

        intent.CommandQ = qFromAction || qFromKeyboard;
        intent.CommandW = wFromAction || wFromKeyboard;
        intent.CommandE = eFromAction || eFromKeyboard;
        intent.CommandR = rFromAction || rFromKeyboard;
        intent.CommandA = aFromAction || aFromKeyboard;
        intent.CommandS = sFromAction || sFromKeyboard;
        intent.CommandD = dFromAction || dFromKeyboard;
        intent.CommandF = fFromAction || fFromKeyboard;
        intent.CommandZ = zFromAction || zFromKeyboard;
        intent.CommandX = xFromAction || xFromKeyboard;
        intent.CommandC = cFromAction || cFromKeyboard;
        intent.CommandV = vFromAction || vFromKeyboard;

        Current = intent;
    }

    void LateUpdate()
    {
        intent.ClearOneFrameActions();
    }

    void OnJumpStarted(InputAction.CallbackContext ctx) => intent.Space = true;
    void OnDashStarted(InputAction.CallbackContext ctx) => intent.Dash = true;
}
