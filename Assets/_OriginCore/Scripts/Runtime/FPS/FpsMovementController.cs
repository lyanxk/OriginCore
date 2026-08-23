using Cinemachine;
using OriginCore.ACT;
using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using UnityEngine;

namespace OriginCore.FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(HybridControlDriver),
        typeof(HybridPawnMotor),
        typeof(FirstPersonVisibility))]
    public sealed class FpsMovementController : MonoBehaviour
    {
        private const float SprintStopSpeedEpsilon = 0.01f;

        private enum SprintPhase
        {
            Walking = 0,
            Accelerating = 1,
            Latched = 2
        }

        [SerializeField] private HybridControlDriver _hybridControl;
        [SerializeField] private HybridPawnMotor _motor;
        [SerializeField] private FlightController _flight;
        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private CombatStatusController _combatStatuses;
        [SerializeField] private DirectMovementTraits _movementTraits;
        [SerializeField] private FpsMovementConfig _config;
        [SerializeField] private MovementConfig _sharedMovementConfig;
        [SerializeField] private Transform _fpsCameraTarget;
        [SerializeField] private FirstPersonVisibility _firstPersonVisibility;
        [SerializeField] private LayerMask _groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask _obstructionMask = Physics.DefaultRaycastLayers;

        private readonly SlideState _slideState = new SlideState();
        private readonly AimState _aimState = new AimState();
        private int _lastProcessedFrame = -1;
        private bool _wasFpsMode;
        private SprintPhase _sprintPhase;
        private bool _cameraAnglesInitialized;
        private bool _cameraPositionInitialized;
        private float _cameraYaw;
        private float _cameraPitch;
        private Vector3 _standingCameraLocalPosition;

        public HybridControlDriver HybridControl => _hybridControl;
        public HybridPawnMotor Motor => _motor;
        public FpsMovementConfig Config => _config;
        public MovementConfig SharedMovementConfig => _sharedMovementConfig;
        public Transform FpsCameraTarget => _fpsCameraTarget;
        public FirstPersonVisibility FirstPersonVisibility => _firstPersonVisibility;
        public LayerMask GroundMask => _groundMask;
        public LayerMask ObstructionMask => _obstructionMask;
        public SlideState Slide => _slideState;
        public AimState Aim => _aimState;
        public float CameraYaw => _cameraYaw;
        public float CameraPitch => _cameraPitch;
        public bool IsSprinting => _sprintPhase != SprintPhase.Walking;
        public bool IsSprintLatched => _sprintPhase == SprintPhase.Latched;
        public bool JumpedThisStep { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            _lastProcessedFrame = -1;
            ResetSprint();
            JumpedThisStep = false;
        }

        private void OnDisable()
        {
            ExitFpsPresentation();
            _lastProcessedFrame = -1;
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void Update()
        {
            if (!TryGetRuntimeServices(out GameServices services) ||
                services.PossessionService.CurrentPawn != _hybridControl)
            {
                ExitFpsPresentation();
                return;
            }

            if (services.GameModeController.CurrentMode != GameMode.FPS)
            {
                ExitFpsPresentation();
                return;
            }

            CinemachineVirtualCamera fpsCamera =
                services.GameModeController.CameraCoordinator != null
                    ? services.GameModeController.CameraCoordinator.FpsCamera
                    : null;
            EnterFpsPresentation(fpsCamera);

            InputSnapshot snapshot = services.InputRouter.CurrentSnapshot;
            ApplyCameraLook(snapshot.ActiveMap == GameplayInputMap.FPS &&
                            !snapshot.GameplaySuppressed &&
                            !snapshot.FPS.WeaponWheel.IsPressed &&
                            !snapshot.FPS.Grenade.IsPressed
                ? snapshot.FPS.Look
                : Vector2.zero);

            if (snapshot.ActiveMap != GameplayInputMap.FPS ||
                snapshot.GameplaySuppressed ||
                snapshot.Frame == _lastProcessedFrame)
            {
                return;
            }

            _lastProcessedFrame = snapshot.Frame;
            if (_hybridControl.State == HybridControlState.PendingManualOverride)
            {
                services.PossessionService.TryHandleDirectInput(snapshot);
            }

            if (!_hybridControl.IsManualActive)
            {
                return;
            }

            Vector3 cameraForward = _fpsCameraTarget != null
                ? _fpsCameraTarget.forward
                : services.CurrentSceneContext != null &&
                  services.CurrentSceneContext.MainCamera != null
                    ? services.CurrentSceneContext.MainCamera.transform.forward
                    : transform.forward;
            Vector3 cameraRight = _fpsCameraTarget != null
                ? _fpsCameraTarget.right
                : services.CurrentSceneContext != null &&
                  services.CurrentSceneContext.MainCamera != null
                    ? services.CurrentSceneContext.MainCamera.transform.right
                    : transform.right;
            Step(snapshot.FPS, cameraForward, cameraRight, Time.deltaTime);
            ApplyCameraRotation();
        }

        private void LateUpdate()
        {
            if (_wasFpsMode && _cameraAnglesInitialized && _fpsCameraTarget != null)
            {
                ApplyCameraRotation();
            }
        }

        public void Configure(
            HybridControlDriver hybridControl,
            HybridPawnMotor motor,
            FpsMovementConfig config,
            MovementConfig sharedMovementConfig,
            Transform fpsCameraTarget,
            FirstPersonVisibility firstPersonVisibility,
            LayerMask groundMask,
            LayerMask obstructionMask)
        {
            _hybridControl = hybridControl != null
                ? hybridControl
                : GetComponent<HybridControlDriver>();
            _motor = motor != null ? motor : GetComponent<HybridPawnMotor>();
            _config = config;
            _sharedMovementConfig = sharedMovementConfig;
            _fpsCameraTarget = fpsCameraTarget;
            _firstPersonVisibility = firstPersonVisibility != null
                ? firstPersonVisibility
                : GetComponent<FirstPersonVisibility>();
            _groundMask = groundMask;
            _obstructionMask = obstructionMask;
            _cameraAnglesInitialized = false;
            _cameraPositionInitialized = false;
            _slideState.Reset();
            _aimState.End();
            ResetSprint();
            _lastProcessedFrame = -1;
        }

        public bool Step(
            FpsInputSnapshot input,
            Vector3 cameraForward,
            Vector3 cameraRight,
            float deltaTime)
        {
            CacheComponents();
            JumpedThisStep = false;
            if (_config == null || _sharedMovementConfig == null ||
                _motor == null || _hybridControl == null ||
                !_hybridControl.IsManualActive || deltaTime <= 0f)
            {
                return false;
            }

            if (_combatStatuses != null && _combatStatuses.BlocksMovement)
            {
                _motor.ResetMotion();
                ResetSprint();
                return false;
            }

            _aimState.Step(input.Aim.IsPressed, deltaTime);
            bool flightToggleConsumed = _flight != null &&
                                        _flight.ProcessJumpInput(input.Jump, deltaTime);
            bool flying = _flight != null && _flight.FlightEnabled;
            if (_sprintPhase == SprintPhase.Latched &&
                _motor.PlanarVelocity.sqrMagnitude <=
                SprintStopSpeedEpsilon * SprintStopSpeedEpsilon)
            {
                ResetSprint();
            }

            UpdateSprintState(input.Sprint);

            bool grounded = !flying && _motor.ProbeGrounded(
                _sharedMovementConfig.GroundProbeDistance,
                _groundMask);
            Vector3 planarForward = NormalizePlanar(cameraForward, transform.forward);
            Vector3 planarRight = NormalizePlanar(
                cameraRight,
                Vector3.Cross(Vector3.up, planarForward));
            AlignBodyWithView(planarForward);

            if (flying && _slideState.End(SlideExitReason.ModeExit))
            {
                ResolveCrouchAfterSlide(false);
            }

            if (!flying && !_slideState.IsSliding &&
                input.Crouch.WasPressedThisFrame &&
                input.Crouch.IsPressed &&
                _slideState.TryBegin(
                    _motor.PlanarVelocity,
                    planarForward,
                    grounded,
                    _config))
            {
                _motor.TrySetCrouched(
                    true,
                    _sharedMovementConfig.CrouchHeight,
                    _obstructionMask);
            }

            if (_slideState.IsSliding && input.Jump.WasPressedThisFrame)
            {
                _slideState.End(SlideExitReason.Jump);
                ResolveCrouchAfterSlide(input.Crouch.IsPressed);
            }

            Vector3 planarVelocity;
            if (_slideState.IsSliding)
            {
                _motor.TrySetCrouched(
                    true,
                    _sharedMovementConfig.CrouchHeight,
                    _obstructionMask);
                bool wasSliding = _slideState.IsSliding;
                planarVelocity = _slideState.Advance(
                    deltaTime,
                    _config,
                    _movementTraits != null
                        ? _movementTraits.SlideFrictionMultiplier
                        : 1f);
                if (wasSliding && !_slideState.IsSliding)
                {
                    ResolveCrouchAfterSlide(input.Crouch.IsPressed);
                }
            }
            else
            {
                _motor.TrySetCrouched(
                    !flying && input.Crouch.IsPressed,
                    _sharedMovementConfig.CrouchHeight,
                    _obstructionMask);
                Vector3 desiredDirection = CalculateCameraRelativeMove(
                    input.Move,
                    planarForward,
                    planarRight);
                float speed = IsSprinting && !_motor.IsCrouched
                    ? _config.SprintSpeed
                    : _config.WalkSpeed;
                speed *= ResolveMoveSpeedMultiplier() *
                         (_movementTraits != null
                             ? _movementTraits.DirectSpeedMultiplier
                             : 1f);
                if (flying)
                {
                    speed = _flight.ResolveHorizontalSpeed(speed);
                }
                Vector3 targetVelocity = desiredDirection * speed;
                planarVelocity = Vector3.MoveTowards(
                    _motor.PlanarVelocity,
                    targetVelocity,
                    _config.Acceleration * deltaTime);
            }

            if (input.Move.sqrMagnitude <= 0.0001f &&
                planarVelocity.sqrMagnitude <=
                SprintStopSpeedEpsilon * SprintStopSpeedEpsilon)
            {
                ResetSprint();
            }

            float verticalVelocity = _motor.VerticalVelocity;
            if (flying)
            {
                verticalVelocity = _flight.ResolveVerticalVelocity(
                    verticalVelocity,
                    input.Jump.IsPressed,
                    input.Crouch.IsPressed,
                    deltaTime);
            }
            else if (grounded && verticalVelocity <= 0f)
            {
                verticalVelocity = _sharedMovementConfig.GroundedVerticalSpeed;
            }

            if (!flying && input.Jump.WasPressedThisFrame &&
                !flightToggleConsumed && grounded)
            {
                verticalVelocity = Mathf.Sqrt(
                    2f * Mathf.Abs(_sharedMovementConfig.Gravity) *
                    _sharedMovementConfig.JumpHeight);
                JumpedThisStep = true;
            }
            else if (!flying && (!grounded || verticalVelocity > 0f))
            {
                verticalVelocity += _sharedMovementConfig.Gravity * deltaTime;
            }

            bool moved = _motor.Move(planarVelocity, verticalVelocity, deltaTime);
            if (_slideState.EndForCollision(_motor.LastCollisionFlags))
            {
                ResolveCrouchAfterSlide(input.Crouch.IsPressed);
            }

            ApplyCameraHeight(deltaTime);

            return moved;
        }

        public void EnterFpsPresentation(CinemachineVirtualCamera fpsCamera)
        {
            if (_wasFpsMode)
            {
                if (!_aimState.IsActive && fpsCamera != null)
                {
                    _aimState.Begin(fpsCamera, _config);
                }

                return;
            }

            _wasFpsMode = true;
            _lastProcessedFrame = -1;
            InitializeCameraAngles();
            InitializeCameraPosition();
            ApplyCameraRotation();
            _firstPersonVisibility?.SetFirstPersonActive(true);
            _aimState.Begin(fpsCamera, _config);
        }

        public void ExitFpsPresentation()
        {
            bool wasFpsMode = _wasFpsMode;
            ResetSprint();
            if (!_wasFpsMode && !_aimState.IsActive &&
                (_firstPersonVisibility == null ||
                 !_firstPersonVisibility.IsFirstPersonHidden) &&
                !_cameraPositionInitialized)
            {
                return;
            }

            if (_slideState.End(SlideExitReason.ModeExit))
            {
                ResolveCrouchAfterSlide(false);
            }

            _aimState.End();
            _firstPersonVisibility?.SetFirstPersonActive(false);
            RestoreCameraHeight();
            if (wasFpsMode)
            {
                _flight?.ClearTransientInput();
            }
            _wasFpsMode = false;
            _lastProcessedFrame = -1;
            JumpedThisStep = false;
        }

        public void ApplyCameraLook(Vector2 lookDelta)
        {
            if (_sharedMovementConfig == null || _fpsCameraTarget == null)
            {
                return;
            }

            InitializeCameraAngles();
            _cameraYaw += lookDelta.x * _sharedMovementConfig.LookSensitivity;
            _cameraPitch = Mathf.Clamp(
                _cameraPitch - lookDelta.y * _sharedMovementConfig.LookSensitivity,
                _sharedMovementConfig.MinimumPitch,
                _sharedMovementConfig.MaximumPitch);
            ApplyCameraRotation();
        }

        public static Vector3 CalculateCameraRelativeMove(
            Vector2 move,
            Vector3 cameraForward,
            Vector3 cameraRight)
        {
            Vector3 forward = NormalizePlanar(cameraForward, Vector3.forward);
            Vector3 right = NormalizePlanar(
                cameraRight,
                Vector3.Cross(Vector3.up, forward));
            Vector2 clampedInput = Vector2.ClampMagnitude(move, 1f);
            return Vector3.ClampMagnitude(
                forward * clampedInput.y + right * clampedInput.x,
                1f);
        }

        private void UpdateSprintState(InputButtonState sprint)
        {
            if (_sprintPhase == SprintPhase.Walking)
            {
                if (sprint.WasPressedThisFrame)
                {
                    _sprintPhase = SprintPhase.Accelerating;
                }

                return;
            }

            if (_sprintPhase == SprintPhase.Accelerating && !sprint.IsPressed)
            {
                _sprintPhase = _motor.PlanarVelocity.magnitude + Mathf.Epsilon >=
                               _config.SprintLatchMinSpeed
                    ? SprintPhase.Latched
                    : SprintPhase.Walking;
            }
        }

        private void ResetSprint()
        {
            _sprintPhase = SprintPhase.Walking;
        }

        private void ResolveCrouchAfterSlide(bool crouchHeld)
        {
            if (_motor == null || _sharedMovementConfig == null)
            {
                return;
            }

            _motor.TrySetCrouched(
                crouchHeld,
                _sharedMovementConfig.CrouchHeight,
                _obstructionMask);
        }

        private void AlignBodyWithView(Vector3 planarForward)
        {
            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(planarForward, Vector3.up);
            ApplyCameraRotation();
        }

        private void InitializeCameraAngles()
        {
            if (_cameraAnglesInitialized || _fpsCameraTarget == null)
            {
                return;
            }

            Vector3 euler = _fpsCameraTarget.rotation.eulerAngles;
            _cameraYaw = euler.y;
            _cameraPitch = NormalizeSignedAngle(euler.x);
            if (_sharedMovementConfig != null)
            {
                _cameraPitch = Mathf.Clamp(
                    _cameraPitch,
                    _sharedMovementConfig.MinimumPitch,
                    _sharedMovementConfig.MaximumPitch);
            }

            _cameraAnglesInitialized = true;
        }

        private void ApplyCameraRotation()
        {
            if (_fpsCameraTarget != null && _cameraAnglesInitialized)
            {
                _fpsCameraTarget.rotation = Quaternion.Euler(
                    _cameraPitch,
                    _cameraYaw,
                    0f);
            }
        }

        private void InitializeCameraPosition()
        {
            if (_cameraPositionInitialized || _fpsCameraTarget == null)
            {
                return;
            }

            _standingCameraLocalPosition = _fpsCameraTarget.localPosition;
            _cameraPositionInitialized = true;
        }

        private void ApplyCameraHeight(float deltaTime)
        {
            if (_fpsCameraTarget == null || _sharedMovementConfig == null || _motor == null)
            {
                return;
            }

            InitializeCameraPosition();
            float crouchedHeight = Mathf.Min(
                _standingCameraLocalPosition.y,
                Mathf.Max(0.2f, _sharedMovementConfig.CrouchHeight - 0.15f));
            float targetHeight = _motor.IsCrouched
                ? crouchedHeight
                : _standingCameraLocalPosition.y;
            Vector3 localPosition = _fpsCameraTarget.localPosition;
            localPosition.x = _standingCameraLocalPosition.x;
            localPosition.y = Mathf.MoveTowards(
                localPosition.y,
                targetHeight,
                6f * Mathf.Max(0f, deltaTime));
            localPosition.z = _standingCameraLocalPosition.z;
            _fpsCameraTarget.localPosition = localPosition;
        }

        private void RestoreCameraHeight()
        {
            if (_fpsCameraTarget != null && _cameraPositionInitialized)
            {
                _fpsCameraTarget.localPosition = _standingCameraLocalPosition;
            }

            _cameraPositionInitialized = false;
        }

        private static Vector3 NormalizePlanar(Vector3 value, Vector3 fallback)
        {
            Vector3 planar = Vector3.ProjectOnPlane(value, Vector3.up);
            if (planar.sqrMagnitude <= 0.000001f)
            {
                planar = Vector3.ProjectOnPlane(fallback, Vector3.up);
            }

            return planar.sqrMagnitude <= 0.000001f
                ? Vector3.forward
                : planar.normalized;
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void CacheComponents()
        {
            if (_hybridControl == null)
            {
                _hybridControl = GetComponent<HybridControlDriver>();
            }

            if (_motor == null)
            {
                _motor = GetComponent<HybridPawnMotor>();
            }

            if (_flight == null)
            {
                _flight = GetComponent<FlightController>();
            }

            if (_stats == null)
            {
                _stats = GetComponent<RuntimeStatBlock>();
            }

            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_combatStatuses == null)
            {
                _combatStatuses = GetComponent<CombatStatusController>();
            }

            if (_movementTraits == null)
            {
                _movementTraits = GetComponent<DirectMovementTraits>();
            }

            if (_firstPersonVisibility == null)
            {
                _firstPersonVisibility = GetComponent<FirstPersonVisibility>();
            }
        }

        private float ResolveMoveSpeedMultiplier()
        {
            if (_stats == null || _identity == null || _identity.Definition == null ||
                _identity.Definition.MoveSpeed <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0f, _stats.GetStat(RuntimeStatId.MoveSpeed)) /
                   _identity.Definition.MoveSpeed;
        }

        private static bool TryGetRuntimeServices(out GameServices services)
        {
            services = null;
            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                return false;
            }

            services = appRoot.Services;
            return services.GameModeController != null &&
                   services.InputRouter != null &&
                   services.PossessionService != null;
        }
    }
}
