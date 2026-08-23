using Cinemachine;
using OriginCore.Buildings;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.UI;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS
{
    [DisallowMultipleComponent]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private Transform _rig;
        [SerializeField] private Camera _outputCamera;
        [SerializeField] private CinemachineVirtualCamera _virtualCamera;
        [SerializeField] private RtsCameraBounds _bounds;
        [Min(0f), SerializeField] private float _edgeSizePixels = 18f;
        [Min(0f), SerializeField] private float _minimumMoveSpeed = 8f;
        [Min(0f), SerializeField] private float _maximumMoveSpeed = 24f;
        [Min(0.1f), SerializeField] private float _minimumHeight = 14f;
        [Min(0.1f), SerializeField] private float _maximumHeight = 40f;
        [Range(1f, 89f), SerializeField] private float _minimumTilt = 60f;
        [Range(1f, 89f), SerializeField] private float _maximumTilt = 60f;
        [Range(0.01f, 1f), SerializeField] private float _zoomStep = 0.12f;
        [Range(0f, 1f), SerializeField] private float _zoomNormalized = 1f;
        [Min(0f), SerializeField] private float _startupEdgePanDelay = 1f;
        [SerializeField] private Transform _initialFocus;

        private InputRouter _inputRouter;
        private GameModeController _modeController;
        private bool _initialFocusApplied;
        private bool _hasPointerSample;
        private bool _edgeInputArmed;
        private Vector2 _lastPointerPosition;
        private float _edgeInputArmTime;

        public Transform Rig => _rig;
        public Camera OutputCamera => _outputCamera;
        public CinemachineVirtualCamera VirtualCamera => _virtualCamera;
        public RtsCameraBounds CameraBounds => _bounds;
        public float EdgeSizePixels => _edgeSizePixels;
        public float MinimumMoveSpeed => _minimumMoveSpeed;
        public float MaximumMoveSpeed => _maximumMoveSpeed;
        public float MinimumHeight => _minimumHeight;
        public float MaximumHeight => _maximumHeight;
        public float MinimumTilt => _minimumTilt;
        public float MaximumTilt => _maximumTilt;
        public float ZoomStep => _zoomStep;
        public float ZoomNormalized => _zoomNormalized;
        public float CurrentHeight => Mathf.Lerp(_minimumHeight, _maximumHeight, _zoomNormalized);
        public float CurrentTilt => Mathf.Lerp(_minimumTilt, _maximumTilt, _zoomNormalized);
        public Transform InitialFocus => _initialFocus;
        public bool InitialFocusApplied => _initialFocusApplied;
        public bool IsConfigured => _rig != null && _outputCamera != null &&
                                    _virtualCamera != null && _bounds != null &&
                                    _bounds.IsConfigured;
        public bool IsServiceBound => _inputRouter != null && _modeController != null;

        private void OnEnable()
        {
            _initialFocusApplied = false;
            _hasPointerSample = false;
            _edgeInputArmed = false;
            _edgeInputArmTime = Time.unscaledTime + _startupEdgePanDelay;
            NormalizeSettings();
            ApplyDirectCameraResponse();
            ApplyZoom();
            TryBindServices();
        }

        private void Start()
        {
            if (!IsServiceBound)
            {
                TryBindServices();
            }

            CenterOnInitialBase();
        }

        private void OnDisable()
        {
            UnbindServices();
        }

        private void OnValidate()
        {
            NormalizeSettings();
            ApplyDirectCameraResponse();
            ApplyZoom();
        }

        public void Configure(
            Transform rig,
            Camera outputCamera,
            CinemachineVirtualCamera virtualCamera,
            RtsCameraBounds bounds,
            float edgeSizePixels = 18f,
            float minimumMoveSpeed = 8f,
            float maximumMoveSpeed = 24f,
            float minimumHeight = 14f,
            float maximumHeight = 40f,
            float minimumTilt = 60f,
            float maximumTilt = 60f,
            float zoomStep = 0.12f,
            float initialZoomNormalized = 1f)
        {
            _rig = rig;
            _outputCamera = outputCamera;
            _virtualCamera = virtualCamera;
            _bounds = bounds;
            _edgeSizePixels = edgeSizePixels;
            _minimumMoveSpeed = minimumMoveSpeed;
            _maximumMoveSpeed = maximumMoveSpeed;
            _minimumHeight = minimumHeight;
            _maximumHeight = maximumHeight;
            _minimumTilt = minimumTilt;
            _maximumTilt = maximumTilt;
            _zoomStep = zoomStep;
            _zoomNormalized = initialZoomNormalized;
            NormalizeSettings();
            ApplyDirectCameraResponse();
            ApplyZoom();
        }

        public bool SetZoomNormalized(float zoomNormalized)
        {
            float normalized = Mathf.Clamp01(zoomNormalized);
            if (Mathf.Approximately(_zoomNormalized, normalized))
            {
                ApplyZoom();
                return false;
            }

            _zoomNormalized = normalized;
            ApplyZoom();
            return true;
        }

        public bool CenterOnWorldPosition(Vector3 worldPosition)
        {
            if (_rig == null || _bounds == null || !_bounds.IsConfigured)
            {
                return false;
            }

            Vector3 target = worldPosition;
            target.y = _rig.position.y;
            target = _bounds.ClampPosition(target);
            if ((target - _rig.position).sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            _rig.position = target;
            return true;
        }

        public bool CenterOnInitialBase()
        {
            if (_initialFocusApplied)
            {
                return false;
            }

            Transform focus = _initialFocus != null
                ? _initialFocus
                : FindPreferredFriendlyBase();
            if (focus == null)
            {
                return false;
            }

            _initialFocus = focus;
            _initialFocusApplied = true;
            CenterOnWorldPosition(focus.position);
            return true;
        }

        public bool ProcessInput(
            RtsInputSnapshot input,
            float deltaTime,
            Vector2 screenSize,
            bool applicationFocused,
            bool pointerOverUi,
            bool allowEdgeMovement = true)
        {
            if (!IsConfigured || !applicationFocused || pointerOverUi)
            {
                return false;
            }

            bool changed = ApplyScroll(input.CameraZoom);
            if (!allowEdgeMovement)
            {
                return changed;
            }

            Vector2 edgeAxis = CalculateEdgeAxis(
                input.Point,
                screenSize,
                _edgeSizePixels);
            if (edgeAxis.sqrMagnitude <= 0f || deltaTime <= 0f)
            {
                return changed;
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                _outputCamera.transform.forward,
                Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(_rig.forward, Vector3.up);
            }

            forward.Normalize();
            Vector3 right = Vector3.ProjectOnPlane(
                _outputCamera.transform.right,
                Vector3.up).normalized;
            Vector3 direction = right * edgeAxis.x + forward * edgeAxis.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            float speed = Mathf.Lerp(
                _minimumMoveSpeed,
                _maximumMoveSpeed,
                _zoomNormalized);
            Vector3 target = _rig.position + direction * (speed * deltaTime);
            target = _bounds.ClampPosition(target);
            if ((target - _rig.position).sqrMagnitude <= 0.000001f)
            {
                return changed;
            }

            _rig.position = target;
            return true;
        }

        public static Vector2 CalculateEdgeAxis(
            Vector2 pointerPosition,
            Vector2 screenSize,
            float edgeSizePixels)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f || edgeSizePixels <= 0f)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;
            if (pointerPosition.x <= edgeSizePixels)
            {
                x = -1f;
            }
            else if (pointerPosition.x >= screenSize.x - edgeSizePixels)
            {
                x = 1f;
            }

            if (pointerPosition.y <= edgeSizePixels)
            {
                y = -1f;
            }
            else if (pointerPosition.y >= screenSize.y - edgeSizePixels)
            {
                y = 1f;
            }

            return new Vector2(x, y);
        }

        private bool TryBindServices()
        {
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            InputRouter inputRouter = appRoot.Services.InputRouter;
            GameModeController modeController = appRoot.Services.GameModeController;
            if (inputRouter == null || modeController == null)
            {
                return false;
            }

            if (_inputRouter == inputRouter && _modeController == modeController)
            {
                return true;
            }

            UnbindServices();
            _inputRouter = inputRouter;
            _modeController = modeController;
            _inputRouter.SnapshotReady += HandleSnapshotReady;
            return true;
        }

        private void UnbindServices()
        {
            if (_inputRouter != null)
            {
                _inputRouter.SnapshotReady -= HandleSnapshotReady;
            }

            _inputRouter = null;
            _modeController = null;
        }

        private void HandleSnapshotReady(InputSnapshot snapshot)
        {
            if (snapshot.GameplaySuppressed || snapshot.ActiveMap != GameplayInputMap.RTS ||
                _modeController == null || _modeController.CurrentMode != GameMode.RTS)
            {
                return;
            }

            Vector2 pointerPosition = snapshot.RTS.Point;
            if (Time.unscaledTime < _edgeInputArmTime)
            {
                _hasPointerSample = true;
                _edgeInputArmed = false;
                _lastPointerPosition = pointerPosition;
            }
            else if (!_hasPointerSample)
            {
                _hasPointerSample = true;
                _lastPointerPosition = pointerPosition;
            }
            else
            {
                if ((pointerPosition - _lastPointerPosition).sqrMagnitude > 0.01f)
                {
                    _edgeInputArmed = true;
                }

                _lastPointerPosition = pointerPosition;
            }

            bool pointerOverUi = UiPointerUtility.IsScreenPointOverUi(snapshot.RTS.Point);
            ProcessInput(
                snapshot.RTS,
                Time.unscaledDeltaTime,
                new Vector2(Screen.width, Screen.height),
                Application.isFocused,
                pointerOverUi,
                _edgeInputArmed);
        }

        private bool ApplyScroll(float rawScroll)
        {
            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return false;
            }

            float normalizedScroll = Mathf.Abs(rawScroll) > 1f
                ? rawScroll / 120f
                : rawScroll;
            normalizedScroll = Mathf.Clamp(normalizedScroll, -1f, 1f);
            return SetZoomNormalized(_zoomNormalized - normalizedScroll * _zoomStep);
        }

        private void ApplyZoom()
        {
            if (_virtualCamera == null)
            {
                return;
            }

            CinemachineTransposer transposer =
                _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer == null)
            {
                return;
            }

            float height = CurrentHeight;
            float tiltRadians = CurrentTilt * Mathf.Deg2Rad;
            float horizontalDistance = height / Mathf.Max(0.01f, Mathf.Tan(tiltRadians));
            transposer.m_FollowOffset = new Vector3(0f, height, -horizontalDistance);
        }

        private void ApplyDirectCameraResponse()
        {
            if (_virtualCamera == null)
            {
                return;
            }

            CinemachineTransposer transposer =
                _virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
            }

            CinemachineComposer composer =
                _virtualCamera.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
                composer.m_LookaheadTime = 0f;
                composer.m_LookaheadSmoothing = 0f;
                composer.m_HorizontalDamping = 0f;
                composer.m_VerticalDamping = 0f;
            }
        }

private void NormalizeSettings()
        {
            _edgeSizePixels = Mathf.Max(0f, _edgeSizePixels);
            _minimumMoveSpeed = Mathf.Max(0f, _minimumMoveSpeed);
            _maximumMoveSpeed = Mathf.Max(_minimumMoveSpeed, _maximumMoveSpeed);
            _minimumHeight = Mathf.Max(0.1f, _minimumHeight);
            _maximumHeight = Mathf.Max(_minimumHeight, _maximumHeight);
            _minimumTilt = Mathf.Clamp(_minimumTilt, 1f, 89f);
            _maximumTilt = Mathf.Clamp(_maximumTilt, _minimumTilt, 89f);
            _zoomStep = Mathf.Clamp(_zoomStep, 0.01f, 1f);
            _zoomNormalized = Mathf.Clamp01(_zoomNormalized);
            _startupEdgePanDelay = Mathf.Max(0f, _startupEdgePanDelay);
        }

        private static Transform FindPreferredFriendlyBase()
        {
            BuildingRuntime[] buildings = FindObjectsOfType<BuildingRuntime>(true);
            BuildingRuntime preferred = null;
            int preferredScore = int.MinValue;
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingRuntime building = buildings[i];
                if (building == null || !building.gameObject.scene.IsValid() ||
                    !building.gameObject.activeInHierarchy)
                {
                    continue;
                }

                FactionMember faction = building.GetComponent<FactionMember>();
                if (faction == null || faction.Faction != FactionId.Friendly)
                {
                    continue;
                }

                int score = building.IsOperational ? 1 : 0;
                if (building.GetComponent<UnitSpawner>() != null)
                {
                    score += 8;
                }

                if (building.GetComponent<OriginCore.Economy.ResourceDropoff>() != null)
                {
                    score += 4;
                }

                if (building.GetComponent<ProductionQueue>() != null)
                {
                    score += 2;
                }

                if (preferred == null || score > preferredScore ||
                    score == preferredScore &&
                    building.GetInstanceID() < preferred.GetInstanceID())
                {
                    preferred = building;
                    preferredScore = score;
                }
            }

            return preferred != null ? preferred.transform : null;
        }
    }
}
