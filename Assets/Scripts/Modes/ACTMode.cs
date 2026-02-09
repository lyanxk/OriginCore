using UnityEngine;

public class ACTMode : IControlMode
{
    public string Name => "ACT";

    readonly UnitBaseMotor _unit;
    readonly Transform _tpsPivot;

    float _yaw;
    float _pitch;

    readonly float _pitchMin = -35f;
    readonly float _pitchMax = 65f;

    // 第三人称相机参数
    readonly float _distance = 6f;
    readonly float _height = 1f;
    
    // Look 手感参数（建议调这里）
    public float lookSensitivity = 45f;   // 基础灵敏度
    public float lookSmoothTime = 0.06f;   // 越小越跟手，越大越稳
    public float maxLookSpeed = 720f;      // 度/秒 上限，防止甩飞

    // 内部平滑用
    float _yawVel;
    float _pitchVel;
    float _yawTarget;
    float _pitchTarget;

    bool _isDashing;
    float _dashTimer;
    Vector3 _dashDir;


    public ACTMode(UnitBaseMotor unit, Transform tpsPivot)
    {
        _unit = unit;
        _tpsPivot = tpsPivot;
        _yaw = _unit.GetYaw();
        _pitch = 15f;

        _yawTarget = _yaw;
        _pitchTarget = _pitch;
        
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = _unit.GetYaw();
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

        _yawTarget = _yaw;
        _pitchTarget = _pitch;
        _yawVel = 0f;
        _pitchVel = 0f;

        _unit.ClearDestination();
    }


    public void Exit() { }

    public void Tick(float dt, InputIntent intent)
    {
        //把输入转换成“目标角度”
        float lookX = intent.Look.x;
        float lookY = intent.Look.y;

        _yawTarget   += lookX * lookSensitivity * dt;
        _pitchTarget -= lookY * lookSensitivity * dt;
        _pitchTarget = Mathf.Clamp(_pitchTarget, _pitchMin, _pitchMax);

        //平滑追随目标角度（角度用 DeltaAngle 避免 359->0 抽搐）
        _yaw = SmoothDampAngle(_yaw, _yawTarget, ref _yawVel, lookSmoothTime, maxLookSpeed, dt);
        _pitch = SmoothDampAngle(_pitch, _pitchTarget, ref _pitchVel, lookSmoothTime, maxLookSpeed, dt);

        //移动按相机yaw方向
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 moveWorld = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
        _unit.MoveImmediate(moveWorld, _unit.walkSpeed);
        
        //跳跃
        if (intent.Space)
        {
            _unit.Jump();
        }
        
        //技能执行
        _unit.AbilityRouter?.Process(intent);
        
        //角色朝向：跟随移动方向
        Vector3 planar = new Vector3(moveWorld.x, 0f, moveWorld.z);
        if (planar.sqrMagnitude > 0.0001f)
        {
            float facingYaw = Quaternion.LookRotation(planar, Vector3.up).eulerAngles.y;
            _unit.SetYaw(facingYaw);
        }
    }
    static float SmoothDampAngle(float current, float target, ref float currentVelocity,
        float smoothTime, float maxSpeed, float deltaTime)
    {
        // 把 target 映射到 current 附近的等效角度，避免绕圈
        float delta = Mathf.DeltaAngle(current, target);
        float fixedTarget = current + delta;
        return Mathf.SmoothDamp(current, fixedTarget, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
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
            Fov = 85f
        };
    }
}