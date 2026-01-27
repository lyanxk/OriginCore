using UnityEngine;

public class ACTMode : IControlMode
{
    public string Name => "ACT";

    readonly PlayerMotor _player;
    readonly Transform _tpsPivot;

    float _yaw;
    float _pitch;

    readonly float _pitchMin = -35f;
    readonly float _pitchMax = 65f;

    // 第三人称相机参数
    readonly float _distance = 3.2f;
    readonly float _height = 0.2f;

    public ACTMode(PlayerMotor player, Transform tpsPivot)
    {
        _player = player;
        _tpsPivot = tpsPivot;
        _yaw = _player.GetYaw();
        _pitch = 15f;
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = _player.GetYaw();
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);
        _player.ClearDestination();
    }

    public void Exit() { }

    public void Tick(float dt, InputIntent intent)
    {
        _yaw += intent.Look.x;
        _pitch -= intent.Look.y;
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

        // 移动按相机yaw方向
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
        _player.MoveImmediate(moveWorld, _player.walkSpeed);

        // 角色朝向：跟随移动方向（可改成跟随相机yaw）
        Vector3 planar = new Vector3(moveWorld.x, 0f, moveWorld.z);
        if (planar.sqrMagnitude > 0.0001f)
        {
            float facingYaw = Quaternion.LookRotation(planar, Vector3.up).eulerAngles.y;
            _player.SetYaw(facingYaw);
        }
    }

    public CameraState GetCameraTarget()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 back = rot * Vector3.back; // 相机朝后
        Vector3 pos = _tpsPivot.position + back * _distance + Vector3.up * _height;

        return new CameraState
        {
            Position = pos,
            Rotation = rot,
            Fov = 65f
        };
    }
}