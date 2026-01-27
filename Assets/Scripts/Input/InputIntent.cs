using UnityEngine;

public struct InputIntent
{
    public Vector2 Move;     // x: 左右, y: 前后
    public Vector2 Look;     // 鼠标增量
    public bool ActionDown;  // 右键按下（默认）
    public bool ActionHeld;  // 右键按住
    public bool SelectDown;  // 左键按下
    public bool SelectHeld;  // 左键按住
    public Vector2 PointerScreenPos; // 鼠标屏幕坐标
}
