using UnityEngine;

public struct InputIntent
{
    public Vector2 Move;     // x: 左右, y: 前后
    public Vector2 Look;     // 鼠标增量
    public bool RightClick;  // 右键按下
    public bool RightHeld;  // 右键按住
    public bool LeftClick;  // 左键按下
    public bool LeftHeld;  // 左键按住
    public Vector2 PointerScreenPos; // 鼠标屏幕坐标
    public float Zoom;    //滚轮
    public bool Shift;    // Shift
    public bool Cancel;      // ESC
    public bool Space;  //空格/跳跃
    public bool Dash;   //上侧键
    
    public void ClearOneFrameActions()
    {
        Space = false;
        Dash = false;
    }
}
