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

        // One-frame actions are captured through started callbacks.
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
    }

    void Update()
    {
        intent.Move = (_move != null) ? _move.ReadValue<Vector2>() : Vector2.zero;

        Vector2 rawLook = (_look != null) ? _look.ReadValue<Vector2>() : Vector2.zero;
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
        {
            intent.AttackPressed = _attack.WasPressedThisFrame();
        }
        else
        {
            intent.AttackPressed = false;
        }

        intent.PointerScreenPos = (_point != null) ? _point.ReadValue<Vector2>() : Vector2.zero;
        intent.Zoom = (_zoom != null) ? _zoom.ReadValue<Vector2>().y : 0f;

        bool shiftFromAction = (_shift != null) && _shift.IsPressed();
        bool shiftFromKeyboard = Keyboard.current != null &&
                                 (Keyboard.current.leftShiftKey.isPressed ||
                                  Keyboard.current.rightShiftKey.isPressed);
        intent.Shift = shiftFromAction || shiftFromKeyboard;
        intent.Cancel = (_cancel != null) && _cancel.WasPressedThisFrame();

        Current = intent;
    }

    void LateUpdate()
    {
        intent.ClearOneFrameActions();
    }

    void OnJumpStarted(InputAction.CallbackContext ctx) => intent.Space = true;
    void OnDashStarted(InputAction.CallbackContext ctx) => intent.Dash = true;
}
