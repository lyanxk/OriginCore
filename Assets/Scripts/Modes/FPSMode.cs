﻿﻿using UnityEngine;

public class FPSMode : IControlMode
{
    public string Name => "FPS";

    readonly UnitBase _unit;
    readonly Transform _fpsPivot;

    float _yaw;
    float _pitch;

    readonly float _pitchMin = -85f;
    readonly float _pitchMax = 85f;

    // 灵敏度/FOV
    public float lookSensitivityX = 45f; // 度/秒
    public float lookSensitivityY = 45f; // 度/秒
    public bool invertY = false;

    public float fov = 110f;

    public FPSMode(UnitBase unit, Transform fpsPivot)
    {
        _unit = unit;
        _fpsPivot = fpsPivot;

        _yaw = _unit.GetYaw();
        _pitch = 0f;
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = _unit.GetYaw();
        _pitch = 0f;
    }

    public void Exit()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Tick(float dt, InputIntent intent)
    {
        float lookX = intent.Look.x * lookSensitivityX * dt;
        float lookY = intent.Look.y * lookSensitivityY * dt;
        if (invertY) lookY = -lookY;

        _yaw += lookX;
        _pitch -= lookY;
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
        
        //跳跃
        if (intent.Space)
        {
            _unit.Jump();
        }
        
        //技能执行
        _unit.AbilityRouter?.Process(intent);
        
        _unit.SetYaw(_yaw);

        // 按住左键：沿屏幕中心（相机 forward）攻击
        Vector3 fireDir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
        if (intent.LeftHeld)
            _unit.Combat?.TryUsePrimaryInDirection(fireDir);
        
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
        _unit.MoveImmediate(moveWorld, _unit.walkSpeed);
    }

    public CameraState GetCameraTarget()
    {
        Quaternion camRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Transform pivot = _fpsPivot != null ? _fpsPivot : _unit.transform;

        return new CameraState
        {
            Position = pivot.position, 
            Rotation = camRot,
            Fov = fov
        };
    }
}
