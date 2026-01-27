using UnityEngine;

public class RTSMode : IControlMode
{
    public string Name => "RTS";

    readonly PlayerMotor _player;
    readonly Selectable _selectable;
    readonly Camera _cam; // 用于射线（MainCamera）
    readonly LayerMask _groundMask;
    readonly float _camHeight;
    readonly float _camDistance;

    Vector3 _camFocus; // 相机关注点（地面上的点）
    float _yaw;        // 绕Y旋转
    float _pitch;      // 俯仰（固定或可调）

    public float panSpeed = 12f;
    public float rotateSpeed = 1.0f;

    public RTSMode(PlayerMotor player, Camera mainCamera, LayerMask groundMask, float camHeight = 18f, float camDistance = 18f)
    {
        _player = player;
        _selectable = player.GetComponent<Selectable>();
        _cam = mainCamera;
        _groundMask = groundMask;

        _camHeight = camHeight;
        _camDistance = camDistance;

        _camFocus = player.transform.position;
        _yaw = player.GetYaw();
        _pitch = 50f;
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 相机默认看玩家附近
        _camFocus = _player.transform.position;
        _player.ClearDestination();
    }

    public void Exit() { }

    public void Tick(float dt, InputIntent intent)
    {
        // 相机平移（基于yaw方向的平面移动）
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 pan = yawRot * new Vector3(intent.Move.x, 0f, intent.Move.y);
        _camFocus += pan * panSpeed * dt;

        // 右键按住旋转相机
        if (intent.ActionHeld)
        {
            _yaw += intent.Look.x * rotateSpeed;
            _pitch -= intent.Look.y * rotateSpeed;
            _pitch = Mathf.Clamp(_pitch, 25f, 80f);
        }

        // 选择（左键点选）
        if (intent.SelectDown)
        {
            Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                var sel = hit.collider.GetComponentInParent<Selectable>();
                if (_selectable != null)
                {
                    _selectable.SetSelected(sel == _selectable);
                }
            }
        }

        // 右键点击地面：下达移动命令
        if (intent.ActionDown && _selectable != null && _selectable.IsSelected)
        {
            Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
            {
                _player.SetDestination(hit.point);
            }
        }
    }

    public CameraState GetCameraTarget()
    {
        // 根据 focus/yaw/pitch 计算相机位置
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 dir = rot * Vector3.forward; // 相机朝向
        Vector3 pos = _camFocus - dir * _camDistance + Vector3.up * _camHeight;

        // 看向 focus
        Quaternion lookRot = Quaternion.LookRotation((_camFocus - pos).normalized, Vector3.up);

        return new CameraState
        {
            Position = pos,
            Rotation = lookRot,
            Fov = 55f
        };
    }
}
