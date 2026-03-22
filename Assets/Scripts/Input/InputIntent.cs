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

    public bool GetCommandPressed(string hotkeyToken)
    {
        switch (CommandHotkeyUtility.NormalizeToken(hotkeyToken))
        {
            case "Q": return CommandQ;
            case "W": return CommandW;
            case "E": return CommandE;
            case "R": return CommandR;
            case "A": return CommandA;
            case "S": return CommandS;
            case "D": return CommandD;
            case "F": return CommandF;
            case "Z": return CommandZ;
            case "X": return CommandX;
            case "C": return CommandC;
            case "V": return CommandV;
            case "M": return CommandM;
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
