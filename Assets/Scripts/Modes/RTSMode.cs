using System.Collections.Generic;
using UnityEngine;

public class RTSMode : IControlMode
{
    public string Name => "RTS";

    readonly PlayerMotor _player;                  // 玩家运动（playerMotor）
    readonly Camera _cam;                          // 主相机（mainCamera）
    readonly LayerMask _groundMask;                // 地面层（groundMask）

    // 相机焦点（camFocus）：地面上的关注点
    Vector3 _camFocus;

    // 相机角（yaw/pitch）
    float _yaw;
    float _pitch;

    // 相机缩放：当前值（height/distance）与目标值（heightTarget/distanceTarget）
    float _height;
    float _distance;
    float _heightTarget;
    float _distanceTarget;
    float _heightVel;
    float _distanceVel;

    // 平移速度（panSpeed），边缘滚动（edgeScroll）
    public float panSpeed = 12f;
    public float edgePanSpeed = 10f;
    public float edgeSizePx = 18f;

    // 旋转速度（rotateSpeed），缩放速度（zoomSpeed）
    public float rotateSpeed = 1.0f;
    public float zoomSpeed = 6f;

    // pitch 限制
    public float pitchMin = 25f;
    public float pitchMax = 80f;

    // zoom 限制
    public float heightMin = 6f;
    public float heightMax = 40f;
    public float distanceMin = 6f;
    public float distanceMax = 45f;

    // 相机焦点边界（focusBounds）：用 XZ 平面
    public bool useBounds = false;
    public Vector2 boundsMinXZ = new Vector2(-50, -50);
    public Vector2 boundsMaxXZ = new Vector2(50, 50);

    // 选择系统（selection）
    readonly List<Selectable> _allSelectables = new List<Selectable>();
    readonly List<Selectable> _selected = new List<Selectable>();

    // 框选（boxSelect）
    bool _isDragging;
    Vector2 _dragStart;
    const float DragThreshold = 8f;

    public RTSMode(PlayerMotor player, Camera mainCamera, LayerMask groundMask, float camHeight = 18f, float camDistance = 18f)
    {
        _player = player;
        _cam = mainCamera;
        _groundMask = groundMask;

        _camFocus = player.transform.position;
        _yaw = player.GetYaw();
        _pitch = 50f;

        _height = camHeight;
        _distance = camDistance;
        _heightTarget = camHeight;
        _distanceTarget = camDistance;

        RefreshSelectables();
    }

    void RefreshSelectables()
    {
        _allSelectables.Clear();
        _allSelectables.AddRange(Object.FindObjectsOfType<Selectable>());
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _camFocus = _player.transform.position;
        _player.ClearDestination();

        RefreshSelectables();
    }

    public void Exit() { }

    public void Tick(float dt, InputIntent intent)
    {
        // 1) 相机平移（Move + EdgeScroll）
        Vector2 move = intent.Move + GetEdgePan(intent.PointerScreenPos);

        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 pan = yawRot * new Vector3(move.x, 0f, move.y);
        _camFocus += pan * panSpeed * dt;

        // 2) 相机旋转（右键按住）
        if (intent.RightHeld)
        {
            _yaw += intent.Look.x * rotateSpeed;
            _pitch -= intent.Look.y * rotateSpeed;
            _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
        }

        // 3) 相机缩放（滚轮）
        if (Mathf.Abs(intent.Zoom) > 0.0001f)
        {
            float z = intent.Zoom * zoomSpeed;
            _heightTarget = Mathf.Clamp(_heightTarget - z, heightMin, heightMax);
            _distanceTarget = Mathf.Clamp(_distanceTarget - z, distanceMin, distanceMax);
        }

        _height = Mathf.SmoothDamp(_height, _heightTarget, ref _heightVel, 0.12f);
        _distance = Mathf.SmoothDamp(_distance, _distanceTarget, ref _distanceVel, 0.12f);

        // 4) 边界限制（Bounds）
        if (useBounds)
        {
            _camFocus.x = Mathf.Clamp(_camFocus.x, boundsMinXZ.x, boundsMaxXZ.x);
            _camFocus.z = Mathf.Clamp(_camFocus.z, boundsMinXZ.y, boundsMaxXZ.y);
        }

        // 5) 选择：单选/框选（Select）
        HandleSelection(intent);

        // 6) 命令：右键点击地面移动（ActionDown）
        if (intent.RightClick && _selected.Count > 0)
        {
            Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
            {
                // 目前先支持：所有选中单位走到同一点（后面再做编队散开）
                foreach (var s in _selected)
                {
                    var motor = s.GetComponent<PlayerMotor>();
                    if (motor != null) motor.SetDestination(hit.point);
                }
            }
        }

        // ESC 取消选择
        if (intent.Cancel)
            ClearSelection();
    }

    Vector2 GetEdgePan(Vector2 pointerScreenPos)
    {
        float x = 0f;
        float y = 0f;

        if (pointerScreenPos.x <= edgeSizePx) x = -1f;
        else if (pointerScreenPos.x >= Screen.width - edgeSizePx) x = 1f;

        if (pointerScreenPos.y <= edgeSizePx) y = -1f;
        else if (pointerScreenPos.y >= Screen.height - edgeSizePx) y = 1f;

        Vector2 v = new Vector2(x, y);
        if (v.sqrMagnitude > 1f) v.Normalize();
        return v * (edgePanSpeed / Mathf.Max(0.0001f, panSpeed)); // 归一到与Move叠加的尺度
    }

    void HandleSelection(InputIntent intent)
    {
        // 鼠标按下开始拖拽
        if (intent.LeftClick)
        {
            _isDragging = true;
            _dragStart = intent.PointerScreenPos;
        }

        // 松开左键：判断是点击还是框选
        if (_isDragging && !intent.LeftHeld)
        {
            _isDragging = false;

            Vector2 dragEnd = intent.PointerScreenPos;
            float dragDist = (dragEnd - _dragStart).magnitude;

            bool additive = intent.Shift;
            
            if (dragDist < DragThreshold)
            {
                // 视为点击单选
                Ray ray = _cam.ScreenPointToRay(dragEnd);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();
                    if (!additive) ClearSelection();

                    if (sel != null) AddSelection(sel);
                    else if (!additive) ClearSelection(); // 点空地清空
                }
            }
            else
            {
                // 框选
                if (!additive) ClearSelection();

                Rect r = ScreenRect(_dragStart, dragEnd);
                foreach (var s in _allSelectables)
                {
                    if (s == null) continue;
                    Vector3 sp = _cam.WorldToScreenPoint(s.transform.position);
                    if (sp.z < 0f) continue; // 在相机背后
                    if (r.Contains(new Vector2(sp.x, sp.y)))
                        AddSelection(s);
                }
            }
        }
    }

    Rect ScreenRect(Vector2 a, Vector2 b)
    {
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        return new Rect(min, max - min);
    }

    void AddSelection(Selectable s)
    {
        if (_selected.Contains(s)) return;
        _selected.Add(s);
        s.SetSelected(true);
    }

    void ClearSelection()
    {
        foreach (var s in _selected)
            if (s != null) s.SetSelected(false);
        _selected.Clear();
    }

    public CameraState GetCameraTarget()
    {
        // 根据 focus/yaw/pitch + zoom（height/distance）计算相机位置
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 dir = rot * Vector3.forward;
        Vector3 pos = _camFocus - dir * _distance + Vector3.up * _height;

        Quaternion lookRot = Quaternion.LookRotation((_camFocus - pos).normalized, Vector3.up);

        return new CameraState
        {
            Position = pos,
            Rotation = lookRot,
            Fov = 55f
        };
    }
}
