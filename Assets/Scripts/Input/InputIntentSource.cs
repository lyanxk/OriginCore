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

    public InputIntent Current { get; private set; }
    
    InputAction _move, _look, _rightClick, _leftClick, _point, _zoom, _shift, _cancel, _space, _dash;

    void Awake()
    {
        _move       = moveAction?.action;
        _look       = lookAction?.action;
        _rightClick = rightClickAction?.action;
        _leftClick  = leftClickAction?.action;
        _point      = pointAction?.action;
        _zoom       = zoomAction?.action;

        _shift      = shiftAction?.action;
        _cancel     = cancelAction?.action;
        _space      = spaceAction?.action;
        _dash       = dashAction?.action;
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

        // started：一次触发
        if (_space != null) _space.started += OnJumpStarted;
        if (_dash  != null) _dash.started  += OnDashStarted;
    }

    void OnDisable()
    {
        if (_space != null) _space.started -= OnJumpStarted;
        if (_dash  != null) _dash.started  -= OnDashStarted;

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
    }

    void Update()
    {
        // Move / Look：如果 action 没配，就给 zero（ReadValue 不会被调用）
        intent.Move = (_move != null) ? _move.ReadValue<Vector2>() : Vector2.zero;

        var rawLook = (_look != null) ? _look.ReadValue<Vector2>() : Vector2.zero;
        intent.Look = rawLook * lookSensitivity;

        // Buttons：把 WasPressedThisFrame / IsPressed 都基于缓存 action
        if (_rightClick != null)
        {
            intent.RightClick = _rightClick.WasPressedThisFrame();
            intent.RightHeld  = _rightClick.IsPressed();
        }
        else
        {
            intent.RightClick = false;
            intent.RightHeld  = false;
        }

        if (_leftClick != null)
        {
            intent.LeftClick = _leftClick.WasPressedThisFrame();
            intent.LeftHeld  = _leftClick.IsPressed();
        }
        else
        {
            intent.LeftClick = false;
            intent.LeftHeld  = false;
        }

        intent.PointerScreenPos = (_point != null) ? _point.ReadValue<Vector2>() : Vector2.zero;

        // Zoom：只需要 y，避免多余逻辑
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
        // 清空一帧事件：确保 jump/dash 只活一帧
        intent.ClearOneFrameActions();
    }

    void OnJumpStarted(InputAction.CallbackContext ctx) => intent.Space = true;
    void OnDashStarted(InputAction.CallbackContext ctx) => intent.Dash = true;
}
