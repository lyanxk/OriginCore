using UnityEngine;

public class FPSMode : IControlMode
{
    public string Name => "FPS";

    readonly PlayerMotor _player;
    readonly Transform _fpsPivot;
    float _yaw;
    float _pitch;

    readonly float _pitchMin = -85f;
    readonly float _pitchMax = 85f;

    public FPSMode(PlayerMotor player, Transform fpsPivot)
    {
        _player = player;
        _fpsPivot = fpsPivot;
        _yaw = _player.GetYaw();
        _pitch = 0f;
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = _player.GetYaw();
        _pitch = 0f;
        _player.ClearDestination();
    }

    public void Exit() { }

    public void Tick(float dt, InputIntent intent)
    {
        _yaw += intent.Look.x;
        _pitch -= intent.Look.y;
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

        _player.SetYaw(_yaw);

        // 移动：按相机yaw方向（忽略pitch）
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
        _player.MoveImmediate(moveWorld, _player.walkSpeed);
    }

    public CameraState GetCameraTarget()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        return new CameraState
        {
            Position = _fpsPivot.position,
            Rotation = rot,
            Fov = 60f
        };
    }
}