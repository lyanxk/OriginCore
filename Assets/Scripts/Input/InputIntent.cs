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

    // Command card one-frame hotkeys.
    public bool CommandQ;
    public bool CommandW;
    public bool CommandE;
    public bool CommandR;
    public bool CommandA;
    public bool CommandS;
    public bool CommandD;
    public bool CommandF;
    public bool CommandZ;
    public bool CommandX;
    public bool CommandC;
    public bool CommandV;
    public bool CommandM;

    public bool GetCommandPressed(int slotIndex)
    {
        switch (slotIndex)
        {
            case 1: return CommandQ;
            case 2: return CommandW;
            case 3: return CommandE;
            case 4: return CommandR;
            case 5: return CommandA;
            case 6: return CommandS;
            case 7: return CommandD;
            case 8: return CommandF;
            case 9: return CommandZ;
            case 10: return CommandX;
            case 11: return CommandC;
            case 12: return CommandV;
            case 13: return CommandM;
            default: return false;
        }
    }

    public void ClearOneFrameActions()
    {
        Space = false;
        Dash = false;

        CommandQ = false;
        CommandW = false;
        CommandE = false;
        CommandR = false;
        CommandA = false;
        CommandS = false;
        CommandD = false;
        CommandF = false;
        CommandZ = false;
        CommandX = false;
        CommandC = false;
        CommandV = false;
        CommandM = false;
    }
}
