using System.Collections.Generic;
using UnityEngine;

public class RTSMode : IControlMode
{
    public string Name => "RTS";

    readonly UnitBaseMotor _unit; // 玩家运动（playerMotor）
    readonly Camera _cam; // 主相机（mainCamera）
    readonly LayerMask _groundMask; // 地面层（groundMask）

    Vector3 _camFocus; // 相机焦点

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

    public float EdgePanSpeed = 10f; //平移速度
    public float EdgeSizeX = 180f;
    public float EdgeSizeY = 100;
    
    public float ZoomStep = 5f; //每次滚轮触发的固定变化值

    // zoom 限制
    public float HeightMin = 6f;
    public float HeightMax = 40f;
    public float DistanceMin = 6f;
    public float DistanceMax = 45f;
    public float PitchMin = 0f;  // 最小视野：与地面平行
    public float PitchMax = 90f; // 最大视野：与地面垂直

    // 相机焦点边界 暂不启用
    public bool UseBounds = false;
    public Vector2 BoundsMinXZ = new Vector2(-50, -50);
    public Vector2 BoundsMaxXZ = new Vector2(50, 50);

    // 框选
    bool _isDragging;
    Vector2 _dragStart;
    const float DragThreshold = 8f;
    readonly RectTransform _selectionBox;
    
    //选择系统
    SelectionManager Sel => SelectionManager.Instance;

    public RTSMode(UnitBaseMotor unit, Camera mainCamera, LayerMask groundMask, RectTransform selectionBox,
        float camHeight = 24f,
        float camDistance = 24f)
    {
        _unit = unit;
        _cam = mainCamera;
        _groundMask = groundMask;

        _camFocus = unit.transform.position;
        _yaw = unit.GetYaw();
        _pitch = PitchMin;

        _height = camHeight;
        _distance = camDistance;
        _heightTarget = camHeight;
        _distanceTarget = camDistance;
        UpdatePitchFromZoom();

        _selectionBox = selectionBox;
        if (_selectionBox != null)
            _selectionBox.gameObject.SetActive(false);
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EdgeSizeX = Screen.width * 0.2f;
        EdgeSizeY = Screen.height * 0.2f;

        _camFocus = _unit.transform.position;
        _unit.CancelPathing();
    }

    public void Exit()
    {
        Sel.ClearSelection();
    }

    public void Tick(float dt, InputIntent intent)
    {
        // 相机平移
        Vector2 move = GetEdgePan(intent.PointerScreenPos);
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 pan = yawRot * new Vector3(move.x, 0f, move.y);
        _camFocus += pan * dt;
        // 相机缩放（滚轮）
        if (Mathf.Abs(intent.Zoom) > 0.0001f)
        {
            float z = Mathf.Sign(intent.Zoom) * ZoomStep;
            _heightTarget = Mathf.Clamp(_heightTarget - z, HeightMin, HeightMax);
            _distanceTarget = Mathf.Clamp(_distanceTarget - z, DistanceMin, DistanceMax);
        }

        _height = Mathf.SmoothDamp(_height, _heightTarget, ref _heightVel, 0.12f);
        _distance = Mathf.SmoothDamp(_distance, _distanceTarget, ref _distanceVel, 0.12f);
        UpdatePitchFromZoom();

        // 边界限制 防止视角离开合法范围
        if (UseBounds)
        {
            _camFocus.x = Mathf.Clamp(_camFocus.x, BoundsMinXZ.x, BoundsMaxXZ.x);
            _camFocus.z = Mathf.Clamp(_camFocus.z, BoundsMinXZ.y, BoundsMaxXZ.y);
        }

        // 选择：单选/框选
        HandleSelection(intent);

        // 命令：右键点击地面移动（ActionDown）
        if (intent.RightClick && Sel != null && Sel.SelectedCount > 0)
        {
            Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
            {
                bool append = intent.Shift;

                foreach (var s in Sel.Selected)
                {
                    if (s == null) continue;
                    var exec = s.GetComponent<CommandExecutor>();
                    if (exec == null) continue;
                    exec.Enqueue(new MoveCommand(hit.point), append);
                }
            }
        }
        // ESC 取消选择
        if (intent.Cancel)
            Sel?.ClearSelection();
    }

    //相机移动逻辑
    Vector2 GetEdgePan(Vector2 pointerScreenPos)
    {
        float x = 0f;
        float y = 0f;

        if (pointerScreenPos.x <= EdgeSizeX)
        {
            float t = 1f - (pointerScreenPos.x / EdgeSizeX);
            x = -t;
        }
        else if (pointerScreenPos.x >= Screen.width - EdgeSizeX)
        {
            float t = (pointerScreenPos.x - (Screen.width - EdgeSizeX)) / EdgeSizeX;
            x = t;
        }

        if (pointerScreenPos.y <= EdgeSizeY)
        {
            float t = 1f - (pointerScreenPos.y / EdgeSizeY);
            y = -t;
        }
        else if (pointerScreenPos.y >= Screen.height - EdgeSizeY)
        {
            float t = (pointerScreenPos.y - (Screen.height - EdgeSizeY)) / EdgeSizeY;
            y = t;
        }

        return new Vector2(x, y) * EdgePanSpeed;
    }

    void HandleSelection(InputIntent intent)
    {
        if (Sel == null) return;

        // 鼠标按下开始拖拽
        if (intent.LeftClick)
        {
            _isDragging = true;
            _dragStart = intent.PointerScreenPos;

            ShowSelectionBox(_dragStart, _dragStart);
        }

        // 拖拽中：更新选择框
        if (_isDragging && intent.LeftHeld)
        {
            Vector2 cur = intent.PointerScreenPos;
            float dragDist = (cur - _dragStart).magnitude;

            if (dragDist >= DragThreshold)
                ShowSelectionBox(_dragStart, cur);
            else
                ShowSelectionBox(_dragStart, _dragStart);
        }

        // 松开左键：判断点击/框选
        if (_isDragging && !intent.LeftHeld)
        {
            _isDragging = false;
            HideSelectionBox();

            Vector2 dragEnd = intent.PointerScreenPos;
            float dragDist = (dragEnd - _dragStart).magnitude;

            bool additive = intent.Shift; // Shift 追加

            if (dragDist < DragThreshold)
            {
                // 点击单选
                Ray ray = _cam.ScreenPointToRay(dragEnd);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    var sel = hit.collider.GetComponentInParent<Selectable>();

                    if (!additive) Sel.ClearSelection();

                    if (sel != null) Sel.AddSelection(sel);
                    else if (!additive) Sel.ClearSelection(); // 点空地清空
                }
                else
                {
                    if (!additive) Sel.ClearSelection();
                }
            }
            else
            {
                // 框选
                if (!additive) Sel.ClearSelection();

                Rect r = ScreenRect(_dragStart, dragEnd);
                foreach (var s in Sel.AllSelectables)
                {
                    if (s == null) continue;
                    Vector3 sp = _cam.WorldToScreenPoint(s.transform.position);
                    if (sp.z < 0f) continue;

                    if (r.Contains(new Vector2(sp.x, sp.y)))
                        Sel.AddSelection(s);
                }
            }
        }
    }

    void ShowSelectionBox(Vector2 startScreen, Vector2 endScreen)
    {
        if (_selectionBox == null) return;

        if (!_selectionBox.gameObject.activeSelf)
            _selectionBox.gameObject.SetActive(true);

        Vector2 min = Vector2.Min(startScreen, endScreen);
        Vector2 max = Vector2.Max(startScreen, endScreen);

        _selectionBox.anchoredPosition = min;
        _selectionBox.sizeDelta = max - min;
    }

    void HideSelectionBox()
    {
        if (_selectionBox != null && _selectionBox.gameObject.activeSelf)
            _selectionBox.gameObject.SetActive(false);
    }

    Rect ScreenRect(Vector2 a, Vector2 b)
    {
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        return new Rect(min, max - min);
    }

    void UpdatePitchFromZoom()
    {
        float t = Mathf.InverseLerp(HeightMin, HeightMax, _height);
        _pitch = Mathf.Lerp(PitchMin, PitchMax, t);
    }
    
    public CameraState GetCameraTarget()
    {
        // 根据缩放映射 pitch，并在 pitch 增大时缩短平面距离，让最大视野趋向俯视
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        float planarDistance = _distance * Mathf.Cos(_pitch * Mathf.Deg2Rad);
        Vector3 pos = _camFocus - (yawRot * Vector3.forward) * planarDistance + Vector3.up * _height;
        Quaternion lookRot = Quaternion.Euler(_pitch, _yaw, 0f);

        return new CameraState
        {
            Position = pos,
            Rotation = lookRot,
            Fov = 55f
        };
    }
}
