using UnityEngine;
using UnityEngine.InputSystem;

public class InputIntentSource : MonoBehaviour
{
    public float lookSensitivity = 1.0f;

    [Header("Actions")] public InputIntent intent = new InputIntent();
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

    void OnEnable()
    {
        moveAction?.action?.Enable();
        lookAction?.action?.Enable();
        rightClickAction?.action?.Enable();
        leftClickAction?.action?.Enable();
        pointAction?.action?.Enable();
        zoomAction?.action?.Enable();
        shiftAction?.action?.Enable();
        cancelAction?.action?.Enable();
        spaceAction?.action?.Enable();
        dashAction?.action?.Enable();

        // 绑定回调（Jump/Dash 用 started 做“一次触发”）
        if (spaceAction != null) spaceAction.action.started += OnJumpStarted;
        if (dashAction != null) dashAction.action.started += OnDashStarted;
    }

    void OnDisable()
    {
        if (spaceAction != null) spaceAction.action.started -= OnJumpStarted;
        if (dashAction != null) dashAction.action.started -= OnDashStarted;

        moveAction?.action?.Disable();
        lookAction?.action?.Disable();
        rightClickAction?.action?.Disable();
        leftClickAction?.action?.Disable();
        pointAction?.action?.Disable();
        zoomAction?.action?.Disable();
        shiftAction?.action?.Disable();
        cancelAction?.action?.Disable();
        spaceAction?.action?.Disable();
        dashAction?.action?.Disable();
    }

    void Update()
    {
        intent.Move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        var rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
        intent.Look = rawLook * lookSensitivity;

        if (rightClickAction != null)
        {
            var a = rightClickAction.action;
            intent.RightClick = a.WasPressedThisFrame();
            intent.RightHeld = a.IsPressed();
        }

        if (leftClickAction != null)
        {
            var a = leftClickAction.action;
            intent.LeftClick = a.WasPressedThisFrame();
            intent.LeftHeld = a.IsPressed();
        }

        intent.PointerScreenPos = pointAction != null
            ? pointAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        if (zoomAction != null)
        {
            var scroll = zoomAction.action.ReadValue<Vector2>();
            intent.Zoom = scroll.y;
        }

        intent.Shift = shiftAction != null && shiftAction.action.IsPressed();
        intent.Cancel = cancelAction != null && cancelAction.action.WasPressedThisFrame();

        Current = intent;
    }

    void LateUpdate()
    {
        // 清空一帧事件：确保 jump/dash 只活一帧
        intent.ClearOneFrameActions();
    }
    void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        intent.Space = true;
    }

    void OnDashStarted(InputAction.CallbackContext ctx)
    {
        intent.Dash = true;
    }
}