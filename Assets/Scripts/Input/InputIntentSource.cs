using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class InputIntentSource : MonoBehaviour
{
    public float lookSensitivity = 1.0f;

    [Header("Actions")]
    public InputActionReference moveAction;
    public InputActionReference lookAction;
    public InputActionReference rightClickAction;
    public InputActionReference leftClickAction;
    public InputActionReference pointAction;
    public InputActionReference zoomAction;

    public InputActionReference shiftAction; 
    public InputActionReference cancelAction;   

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
    }

    void OnDisable()
    {
        moveAction?.action?.Disable();
        lookAction?.action?.Disable();
        rightClickAction?.action?.Disable();
        leftClickAction?.action?.Disable();
        pointAction?.action?.Disable();
        zoomAction?.action?.Disable();
        shiftAction?.action?.Disable();
        cancelAction?.action?.Disable();
    }

    void Update()
    {
        var intent = new InputIntent();

        intent.Move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        var rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
        intent.Look = rawLook * lookSensitivity;

        if (rightClickAction != null)
        {
            var a = rightClickAction.action;
            intent.RightClick = a.WasPressedThisFrame();
            intent.RightHeld  = a.IsPressed();
        }

        if (leftClickAction != null)
        {
            var a = leftClickAction.action;
            intent.LeftClick = a.WasPressedThisFrame();
            intent.LeftHeld  = a.IsPressed();
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
        intent.Cancel   = cancelAction != null && cancelAction.action.WasPressedThisFrame();

        Current = intent;
    }
}
