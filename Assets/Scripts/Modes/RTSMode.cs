using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RTSMode : IControlMode
{
    const int PointPreviewSegments = 48;
    const float PointPreviewLineWidth = 0.08f;
    const float PointPreviewYOffset = 0.06f;
    static readonly Color PointPreviewColor = new Color(1f, 0.45f, 0.1f, 0.95f);

    static Material s_pointPreviewMaterial;

    public string Name => "RTS";

    readonly UnitBase _unit;
    readonly Camera _cam;
    readonly LayerMask _groundMask;

    Vector3 _camFocus;

    float _yaw;
    float _pitch;

    float _height;
    float _distance;
    float _heightTarget;
    float _distanceTarget;
    float _heightVel;
    float _distanceVel;

    public float EdgePanSpeed = 10f;
    public float EdgeSizeX = 180f;
    public float EdgeSizeY = 100f;

    public float ZoomStep = 5f;

    public float HeightMin = 6f;
    public float HeightMax = 40f;
    public float DistanceMin = 6f;
    public float DistanceMax = 45f;
    public float PitchMin = 0f;
    public float PitchMax = 90f;

    public bool UseBounds = false;
    public Vector2 BoundsMinXZ = new Vector2(-50, -50);
    public Vector2 BoundsMaxXZ = new Vector2(50, 50);

    bool _isDragging;
    bool _isAttackOrderMode;
    bool _isMoveOrderMode;
    Vector2 _dragStart;
    const float DragThreshold = 8f;
    readonly RectTransform _selectionBox;
    readonly List<CommandExecutor> _moveExecutors = new List<CommandExecutor>(32);
    readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(16);

    EventSystem _pointerEventSystem;
    PointerEventData _pointerEventData;
    Graphic _selectionBoxGraphic;
    GameObject _pointTargetPreviewObject;
    LineRenderer _pointTargetPreviewLine;

    SelectionManager Sel => SelectionManager.Instance;

    public RTSMode(
        UnitBase unit,
        Camera mainCamera,
        LayerMask groundMask,
        RectTransform selectionBox,
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
        {
            _selectionBox.gameObject.SetActive(false);

            _selectionBoxGraphic = _selectionBox.GetComponent<Graphic>();
            if (_selectionBoxGraphic != null)
                _selectionBoxGraphic.raycastTarget = false;
        }
    }

    public void Enter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EdgeSizeX = Screen.width * 0.12f;
        EdgeSizeY = Screen.height * 0.12f;

        _camFocus = _unit.transform.position;
        _unit.CancelPathing();
        RtsQueuedOrderState.Clear();
        HidePointTargetingPreview();
    }

    public void Exit()
    {
        RtsQueuedOrderState.Clear();
        DestroyPointTargetingPreview();
        Sel?.ClearSelection();
    }

    public void Tick(float dt, InputIntent intent)
    {
        bool hasSelection = Sel != null && Sel.SelectedCount > 0;
        if (!hasSelection)
        {
            _isAttackOrderMode = false;
            _isMoveOrderMode = false;
            RtsQueuedOrderState.Clear();
            RtsAbilityTargetingState.Clear();
        }

        ApplyQueuedOrderState(hasSelection);

        if (RtsAbilityTargetingState.HasPending)
        {
            _isAttackOrderMode = false;
            _isMoveOrderMode = false;
            RtsQueuedOrderState.Clear();
            CancelSelectionDrag();
        }

        if (intent.CommandS && hasSelection)
        {
            RtsOrderDispatcher.TryIssueStop(Sel.Selected);
            _isAttackOrderMode = false;
            _isMoveOrderMode = false;
            RtsQueuedOrderState.Clear();
            CancelSelectionDrag();
        }

        if ((intent.AttackPressed || intent.CommandA) && hasSelection)
        {
            _isAttackOrderMode = !_isAttackOrderMode;
            RtsQueuedOrderState.Clear();
            if (_isAttackOrderMode)
            {
                _isMoveOrderMode = false;
                CancelSelectionDrag();
            }
        }

        if (intent.CommandM && hasSelection)
        {
            _isMoveOrderMode = !_isMoveOrderMode;
            RtsQueuedOrderState.Clear();
            if (_isMoveOrderMode)
            {
                _isAttackOrderMode = false;
                CancelSelectionDrag();
            }
        }

        Vector2 move = GetEdgePan(intent.PointerScreenPos);
        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 pan = yawRot * new Vector3(move.x, 0f, move.y);
        _camFocus += pan * dt;

        if (Mathf.Abs(intent.Zoom) > 0.0001f)
        {
            float z = Mathf.Sign(intent.Zoom) * ZoomStep;
            _heightTarget = Mathf.Clamp(_heightTarget - z, HeightMin, HeightMax);
            _distanceTarget = Mathf.Clamp(_distanceTarget - z, DistanceMin, DistanceMax);
        }

        _height = Mathf.SmoothDamp(_height, _heightTarget, ref _heightVel, 0.12f);
        _distance = Mathf.SmoothDamp(_distance, _distanceTarget, ref _distanceVel, 0.12f);
        UpdatePitchFromZoom();

        if (UseBounds)
        {
            _camFocus.x = Mathf.Clamp(_camFocus.x, BoundsMinXZ.x, BoundsMaxXZ.x);
            _camFocus.z = Mathf.Clamp(_camFocus.z, BoundsMinXZ.y, BoundsMaxXZ.y);
        }

        bool canceledOrderWithClick = false;
        if (RtsAbilityTargetingState.HasPending &&
            intent.LeftClick &&
            !IsPointerOverBlockingUi(intent.PointerScreenPos))
        {
            canceledOrderWithClick = TryHandlePendingAbility(intent.PointerScreenPos);
        }

        if ((_isAttackOrderMode || _isMoveOrderMode) && intent.RightClick)
        {
            _isAttackOrderMode = false;
            _isMoveOrderMode = false;
            RtsQueuedOrderState.Clear();
            CancelSelectionDrag();
            canceledOrderWithClick = true;
        }

        if (_isAttackOrderMode)
        {
            CancelSelectionDrag();
            if (HandleAttackCommand(intent))
            {
                _isAttackOrderMode = false;
                RtsQueuedOrderState.Clear();
            }
        }
        else if (_isMoveOrderMode)
        {
            CancelSelectionDrag();
            if (HandleMoveCommand(intent))
            {
                _isMoveOrderMode = false;
                RtsQueuedOrderState.Clear();
            }
        }
        else if (RtsAbilityTargetingState.HasPending)
        {
            CancelSelectionDrag();
        }
        else
        {
            HandleSelection(intent);
        }

        if (!canceledOrderWithClick &&
            !RtsAbilityTargetingState.HasPending &&
            !_isAttackOrderMode &&
            !_isMoveOrderMode &&
            intent.RightClick &&
            !IsPointerOverBlockingUi(intent.PointerScreenPos) &&
            Sel != null &&
            Sel.SelectedCount > 0)
        {
            Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
                RtsOrderDispatcher.TryIssueMove(Sel.Selected, hit.point, intent.Shift, _moveExecutors);
        }

        if (intent.Cancel)
        {
            _isAttackOrderMode = false;
            _isMoveOrderMode = false;
            RtsQueuedOrderState.Clear();
            RtsAbilityTargetingState.Clear();
            Sel?.ClearSelection();
        }

        UpdateAbilityTargetingPreview(intent.PointerScreenPos);
    }

    bool HandleMoveCommand(InputIntent intent)
    {
        if (!intent.LeftClick) return false;
        if (Sel == null || Sel.SelectedCount <= 0) return false;
        if (IsPointerOverBlockingUi(intent.PointerScreenPos)) return false;

        Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
            return false;

        return RtsOrderDispatcher.TryIssueMove(Sel.Selected, hit.point, intent.Shift, _moveExecutors);
    }

    bool HandleAttackCommand(InputIntent intent)
    {
        if (!intent.LeftClick) return false;
        if (Sel == null || Sel.SelectedCount <= 0) return false;
        if (IsPointerOverBlockingUi(intent.PointerScreenPos)) return false;

        Ray ray = _cam.ScreenPointToRay(intent.PointerScreenPos);
        Vector3 orderPoint;
        if (Physics.Raycast(ray, out RaycastHit groundHit, 500f, _groundMask))
        {
            orderPoint = groundHit.point;
        }
        else if (Physics.Raycast(ray, out RaycastHit anyHit, 500f))
        {
            orderPoint = anyHit.point;
        }
        else
        {
            return false;
        }

        return RtsOrderDispatcher.TryIssueAttack(Sel.Selected, orderPoint, intent.Shift);
    }

    bool TryHandlePendingAbility(Vector2 pointerScreenPos)
    {
        Ray ray = _cam.ScreenPointToRay(pointerScreenPos);

        switch (RtsAbilityTargetingState.TargetingMode)
        {
            case Unit.Ability.RtsAbilityTargetingMode.Unit:
                if (!Physics.Raycast(ray, out RaycastHit unitHit, 500f))
                    return false;

                if (!Selectable.TryResolve(unitHit.collider, out Selectable target))
                    return false;

                return RtsAbilityTargetingState.TryActivateOnUnit(target, unitHit.point);

            case Unit.Ability.RtsAbilityTargetingMode.Point:
                if (!Physics.Raycast(ray, out RaycastHit pointHit, 500f, _groundMask))
                    return false;

                return RtsAbilityTargetingState.TryActivateAtPoint(pointHit.point);

            default:
                return false;
        }
    }

    void UpdateAbilityTargetingPreview(Vector2 pointerScreenPos)
    {
        if (!RtsAbilityTargetingState.HasPending ||
            RtsAbilityTargetingState.TargetingMode != Unit.Ability.RtsAbilityTargetingMode.Point)
        {
            HidePointTargetingPreview();
            return;
        }

        float previewRadius = RtsAbilityTargetingState.PointPreviewRadius;
        if (previewRadius <= 0.01f || IsPointerOverBlockingUi(pointerScreenPos))
        {
            HidePointTargetingPreview();
            return;
        }

        Ray ray = _cam.ScreenPointToRay(pointerScreenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, _groundMask))
        {
            HidePointTargetingPreview();
            return;
        }

        ShowPointTargetingPreview(hit.point, previewRadius);
    }

    void ShowPointTargetingPreview(Vector3 center, float radius)
    {
        EnsurePointTargetingPreview();
        if (_pointTargetPreviewLine == null)
            return;

        radius = Mathf.Max(0.05f, radius);
        _pointTargetPreviewLine.positionCount = PointPreviewSegments;
        _pointTargetPreviewLine.loop = true;

        for (int i = 0; i < PointPreviewSegments; i++)
        {
            float angle = i / (float)PointPreviewSegments * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            _pointTargetPreviewLine.SetPosition(i, center + offset + Vector3.up * PointPreviewYOffset);
        }

        _pointTargetPreviewLine.enabled = true;
    }

    void HidePointTargetingPreview()
    {
        if (_pointTargetPreviewLine != null)
            _pointTargetPreviewLine.enabled = false;
    }

    void EnsurePointTargetingPreview()
    {
        if (_pointTargetPreviewLine != null)
            return;

        _pointTargetPreviewObject = new GameObject("RtsPointTargetPreview");
        _pointTargetPreviewLine = _pointTargetPreviewObject.AddComponent<LineRenderer>();
        _pointTargetPreviewLine.useWorldSpace = true;
        _pointTargetPreviewLine.alignment = LineAlignment.View;
        _pointTargetPreviewLine.textureMode = LineTextureMode.Stretch;
        _pointTargetPreviewLine.loop = true;
        _pointTargetPreviewLine.startWidth = PointPreviewLineWidth;
        _pointTargetPreviewLine.endWidth = PointPreviewLineWidth;
        _pointTargetPreviewLine.startColor = PointPreviewColor;
        _pointTargetPreviewLine.endColor = PointPreviewColor;
        _pointTargetPreviewLine.numCapVertices = 2;
        _pointTargetPreviewLine.numCornerVertices = 2;
        _pointTargetPreviewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _pointTargetPreviewLine.receiveShadows = false;
        _pointTargetPreviewLine.enabled = false;

        Material previewMaterial = GetPointTargetingPreviewMaterial();
        if (previewMaterial != null)
            _pointTargetPreviewLine.sharedMaterial = previewMaterial;
    }

    void DestroyPointTargetingPreview()
    {
        if (_pointTargetPreviewObject != null)
            Object.Destroy(_pointTargetPreviewObject);

        _pointTargetPreviewObject = null;
        _pointTargetPreviewLine = null;
    }

    static Material GetPointTargetingPreviewMaterial()
    {
        if (s_pointPreviewMaterial != null)
            return s_pointPreviewMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            return null;

        s_pointPreviewMaterial = new Material(shader)
        {
            name = "RtsPointTargetPreview"
        };

        return s_pointPreviewMaterial;
    }

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
        if (Sel == null)
            return;

        if (IsPointerOverBlockingUi(intent.PointerScreenPos))
        {
            if (_isDragging && !intent.LeftHeld)
            {
                _isDragging = false;
                HideSelectionBox();
            }

            return;
        }

        if (intent.LeftClick)
        {
            _isDragging = true;
            _dragStart = intent.PointerScreenPos;
            ShowSelectionBox(_dragStart, _dragStart);
        }

        if (_isDragging && intent.LeftHeld)
        {
            Vector2 cur = intent.PointerScreenPos;
            float dragDist = (cur - _dragStart).magnitude;

            if (dragDist >= DragThreshold)
                ShowSelectionBox(_dragStart, cur);
            else
                ShowSelectionBox(_dragStart, _dragStart);
        }

        if (_isDragging && !intent.LeftHeld)
        {
            _isDragging = false;
            HideSelectionBox();

            Vector2 dragEnd = intent.PointerScreenPos;
            float dragDist = (dragEnd - _dragStart).magnitude;
            bool additive = intent.Shift;

            if (dragDist < DragThreshold)
            {
                Ray ray = _cam.ScreenPointToRay(dragEnd);
                if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                {
                    Selectable.TryResolve(hit.collider, out Selectable sel);

                    if (!additive) Sel.ClearSelection();

                    if (RtsOrderDispatcher.CanControlSelectable(sel)) Sel.AddSelection(sel);
                    else if (!additive) Sel.ClearSelection();
                }
                else
                {
                    if (!additive) Sel.ClearSelection();
                }
            }
            else
            {
                if (!additive) Sel.ClearSelection();

                Rect r = ScreenRect(_dragStart, dragEnd);
                foreach (var s in Sel.AllSelectables)
                {
                    if (s == null) continue;
                    if (!RtsOrderDispatcher.CanControlSelectable(s)) continue;

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

    void CancelSelectionDrag()
    {
        if (!_isDragging) return;

        _isDragging = false;
        HideSelectionBox();
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

    void ApplyQueuedOrderState(bool hasSelection)
    {
        if (!hasSelection)
            return;

        switch (RtsQueuedOrderState.PendingOrder)
        {
            case RtsQueuedOrderType.Move:
                _isMoveOrderMode = true;
                _isAttackOrderMode = false;
                CancelSelectionDrag();
                break;

            case RtsQueuedOrderType.Attack:
                _isAttackOrderMode = true;
                _isMoveOrderMode = false;
                CancelSelectionDrag();
                break;
        }
    }

    bool IsPointerOverBlockingUi(Vector2 pointerScreenPos)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (_pointerEventData == null || _pointerEventSystem != eventSystem)
        {
            _pointerEventData = new PointerEventData(eventSystem);
            _pointerEventSystem = eventSystem;
        }

        _pointerEventData.position = pointerScreenPos;
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);

        for (int i = 0; i < _uiRaycastResults.Count; i++)
        {
            GameObject hitObject = _uiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (_selectionBox != null && hitObject.transform.IsChildOf(_selectionBox))
                continue;

            Canvas canvas = hitObject.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled)
                continue;

            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;

            return true;
        }

        return false;
    }

    public CameraState GetCameraTarget()
    {
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
