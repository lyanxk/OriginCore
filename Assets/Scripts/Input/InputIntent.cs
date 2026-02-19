using UnityEngine;

public struct InputIntent
{
    public Vector2 Move;     // x: left/right, y: forward/back
    public Vector2 Look;     // mouse delta
    public bool RightClick;  // pressed this frame
    public bool RightHeld;
    public bool LeftClick;   // pressed this frame
    public bool LeftHeld;
    public Vector2 PointerScreenPos;
    public float Zoom;
    public bool Shift;
    public bool Cancel;
    public bool Space;
    public bool Dash;
    public bool AttackPressed; // Attack down this frame

    public void ClearOneFrameActions()
    {
        Space = false;
        Dash = false;
    }
}
