using OriginCore.Abilities;
using OriginCore.Combat;
using OriginCore.Core;
using OriginCore.Gameplay;
using OriginCore.Input;
using OriginCore.Weapons;
using UnityEngine;

namespace OriginCore.ACT
{
    public enum ActJumpKind
    {
        None = 0,
        Ground = 1,
        Air = 2,
        Boost = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(HybridControlDriver), typeof(HybridPawnMotor))]
    public sealed class ActMovementController : MonoBehaviour
    {
        private const int MaximumAirJumps = 1;

        [SerializeField] private HybridControlDriver _hybridControl;
        [SerializeField] private HybridPawnMotor _motor;
        [SerializeField] private FlightController _flight;
        [SerializeField] private RuntimeStatBlock _stats;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private WeaponActionController _weaponActions;
        [SerializeField] private CombatStatusController _combatStatuses;
        [SerializeField] private DirectMovementTraits _movementTraits;
        [SerializeField] private MovementConfig _config;
        [SerializeField] private Transform _actCameraTarget;
        [SerializeField] private LayerMask _groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask _obstructionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask _boostMask;

        private readonly InputBuffer _inputBuffer = new InputBuffer();
        private int _airJumpsUsed;
        private float _coyoteRemaining;
        private float _boostCooldownRemaining;
        private int _lastProcessedFrame = -1;
        private bool _wasActMode;
        private bool _cameraAnglesInitialized;
        private float _cameraYaw;
        private float _cameraPitch;

        public HybridControlDriver HybridControl => _hybridControl;
        public HybridPawnMotor Motor => _motor;
        public MovementConfig Config => _config;
        public Transform ActCameraTarget => _actCameraTarget;
        public LayerMask GroundMask => _groundMask;
        public LayerMask ObstructionMask => _obstructionMask;
        public LayerMask BoostMask => _boostMask;
        public InputBuffer Buffer => _inputBuffer;
        public int AirJumpsUsed => _airJumpsUsed;
        public float CoyoteRemaining => _coyoteRemaining;
        public float BoostCooldownRemaining => _boostCooldownRemaining;
        public float CameraYaw => _cameraYaw;
        public float CameraPitch => _cameraPitch;
        public ActJumpKind LastJumpKind { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            _inputBuffer.Clear();
            _lastProcessedFrame = -1;
        }

        private void OnDisable()
        {
            _inputBuffer.Clear();
            _flight?.ClearTransientInput();
            _lastProcessedFrame = -1;
            _wasActMode = false;
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
                return;
            }

            GameMode mode = services.GameModeController.CurrentMode;
            if (mode != GameMode.ACT)
            {
                HandleInactiveMode(mode);
                return;
            }

            EnterActModeIfNeeded();
            InputSnapshot snapshot = services.InputRouter.CurrentSnapshot;
            ApplyCameraLook(snapshot.ActiveMap == GameplayInputMap.ACT &&
                            !snapshot.GameplaySuppressed &&
                            !snapshot.ACT.WeaponWheel.IsPressed &&
                            !snapshot.ACT.TargetLock.IsPressed
                ? snapshot.ACT.Look
                : Vector2.zero);

            if (snapshot.ActiveMap != GameplayInputMap.ACT || snapshot.GameplaySuppressed ||
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

            Vector3 cameraForward = _actCameraTarget != null
                ? _actCameraTarget.forward
                : services.CurrentSceneContext != null &&
                  services.CurrentSceneContext.MainCamera != null
                    ? services.CurrentSceneContext.MainCamera.transform.forward
                    : Vector3.forward;
            Vector3 cameraRight = _actCameraTarget != null
                ? _actCameraTarget.right
                : services.CurrentSceneContext != null &&
                  services.CurrentSceneContext.MainCamera != null
                    ? services.CurrentSceneContext.MainCamera.transform.right
                    : Vector3.right;
            Step(snapshot.ACT, cameraForward, cameraRight, Time.deltaTime);
            ApplyCameraRotation();
        }

        private void LateUpdate()
        {
            if (_wasActMode && _cameraAnglesInitialized && _actCameraTarget != null)
            {
                ApplyCameraRotation();
            }
        }

        public void Configure(
            HybridControlDriver hybridControl,
            HybridPawnMotor motor,
            MovementConfig config,
            Transform actCameraTarget,
            LayerMask groundMask,
            LayerMask obstructionMask,
            LayerMask boostMask)
        {
            _hybridControl = hybridControl != null
                ? hybridControl
                : GetComponent<HybridControlDriver>();
            _motor = motor != null ? motor : GetComponent<HybridPawnMotor>();
            _config = config;
            _actCameraTarget = actCameraTarget;
            _groundMask = groundMask;
            _obstructionMask = obstructionMask;
            _boostMask = boostMask;
            _cameraAnglesInitialized = false;
            ResetActState(false);
        }

        public bool Step(
            ActInputSnapshot input,
            Vector3 cameraForward,
            Vector3 cameraRight,
            float deltaTime)
        {
            CacheComponents();
            LastJumpKind = ActJumpKind.None;
            if (_config == null || _motor == null || _hybridControl == null ||
                !_hybridControl.IsManualActive || deltaTime <= 0f)
            {
                return false;
            }

            if (_combatStatuses != null && _combatStatuses.BlocksMovement)
            {
                _motor.ResetMotion();
                return false;
            }
            if (_weaponActions != null && _weaponActions.BlocksDirectMovement)
            {
                return false;
            }

            _inputBuffer.Tick(deltaTime);
            bool flightToggleConsumed = _flight != null &&
                                        _flight.ProcessJumpInput(input.Jump, deltaTime);
            bool flying = _flight != null && _flight.FlightEnabled;
            if (!flying && input.Jump.WasPressedThisFrame && !flightToggleConsumed)
            {
                _inputBuffer.BufferJump(_config.JumpBufferTime);
            }
            else if (flying)
            {
                _inputBuffer.Clear();
            }

            _boostCooldownRemaining = Mathf.Max(
                0f,
                _boostCooldownRemaining - deltaTime);
            bool grounded = !flying && _motor.ProbeGrounded(
                _config.GroundProbeDistance,
                _groundMask);
            if (grounded)
            {
                _coyoteRemaining = _config.CoyoteTime;
                _airJumpsUsed = 0;
            }
            else
            {
                _coyoteRemaining = Mathf.Max(0f, _coyoteRemaining - deltaTime);
            }

            _motor.TrySetCrouched(
                !flying && input.Crouch.IsPressed,
                _config.CrouchHeight,
                _obstructionMask);

            Vector3 desiredDirection = CalculateCameraRelativeMove(
                input.Move,
                cameraForward,
                cameraRight);
            float runSpeed = _config.RunSpeed * ResolveMoveSpeedMultiplier() *
                             (_movementTraits != null
                                 ? _movementTraits.DirectSpeedMultiplier
                                 : 1f);
            runSpeed = flying
                ? _flight.ResolveHorizontalSpeed(runSpeed)
                : runSpeed;
            Vector3 targetPlanarVelocity = desiredDirection * runSpeed;
            float acceleration = _config.Acceleration *
                                 (grounded ? 1f : _config.AirControl);
            Vector3 planarVelocity = Vector3.MoveTowards(
                _motor.PlanarVelocity,
                targetPlanarVelocity,
                acceleration * deltaTime);
            float verticalVelocity = _motor.VerticalVelocity;
            if (flying)
            {
                verticalVelocity = _flight.ResolveVerticalVelocity(
                    verticalVelocity,
                    input.Jump.IsPressed,
                    input.Crouch.IsPressed,
                    deltaTime);
            }

            if (!flying && grounded && verticalVelocity <= 0f)
            {
                verticalVelocity = _config.GroundedVerticalSpeed;
            }

            bool jumped = !flying && TryExecuteBufferedJump(
                grounded,
                ref planarVelocity,
                ref verticalVelocity);
            if (!flying && !jumped)
            {
                if (grounded && verticalVelocity <= 0f)
                {
                    verticalVelocity = _config.GroundedVerticalSpeed;
                }
                else
                {
                    verticalVelocity += _config.Gravity * deltaTime;
                }
            }

            Vector3 facingDirection = desiredDirection;
            if (facingDirection.sqrMagnitude <= 0.000001f &&
                LastJumpKind == ActJumpKind.Boost)
            {
                facingDirection = planarVelocity;
            }

            _motor.RotateTowards(
                facingDirection,
                _config.RotationSpeed,
                deltaTime);
            bool moved = _motor.Move(planarVelocity, verticalVelocity, deltaTime);
            if (_motor.IsGrounded && LastJumpKind == ActJumpKind.None)
            {
                _airJumpsUsed = 0;
            }

            return moved;
        }

        public void ApplyCameraLook(Vector2 lookDelta)
        {
            if (_config == null || _actCameraTarget == null)
            {
                return;
            }

            InitializeCameraAngles();
            _cameraYaw += lookDelta.x * _config.LookSensitivity;
            _cameraPitch = Mathf.Clamp(
                _cameraPitch - lookDelta.y * _config.LookSensitivity,
                _config.MinimumPitch,
                _config.MaximumPitch);
            ApplyCameraRotation();
        }

        public bool SetCameraLookDirection(Vector3 worldDirection)
        {
            if (_config == null || _actCameraTarget == null ||
                worldDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Quaternion rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            Vector3 euler = rotation.eulerAngles;
            _cameraYaw = euler.y;
            _cameraPitch = Mathf.Clamp(
                NormalizeSignedAngle(euler.x),
                _config.MinimumPitch,
                _config.MaximumPitch);
            _cameraAnglesInitialized = true;
            ApplyCameraRotation();
            return true;
        }

        public void ClearInputBuffer()
        {
            _inputBuffer.Clear();
        }

        public static Vector3 CalculateCameraRelativeMove(
            Vector2 move,
            Vector3 cameraForward,
            Vector3 cameraRight)
        {
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = Vector3.ProjectOnPlane(cameraRight, Vector3.up);
            if (right.sqrMagnitude <= 0.000001f)
            {
                right = Vector3.Cross(Vector3.up, forward);
            }
            else
            {
                right.Normalize();
            }

            Vector2 clampedInput = Vector2.ClampMagnitude(move, 1f);
            return Vector3.ClampMagnitude(
                forward * clampedInput.y + right * clampedInput.x,
                1f);
        }

        private bool TryExecuteBufferedJump(
            bool grounded,
            ref Vector3 planarVelocity,
            ref float verticalVelocity)
        {
            if (!_inputBuffer.HasBufferedJump)
            {
                return false;
            }

            if (_boostCooldownRemaining <= 0f &&
                _motor.TryFindNearestBoostContact(
                    _config.BoostRadius,
                    _boostMask,
                    out Vector3 contactPoint,
                    out _))
            {
                Vector3 away = Vector3.ProjectOnPlane(
                    _motor.WorldCenter - contactPoint,
                    Vector3.up);
                if (away.sqrMagnitude <= 0.000001f)
                {
                    away = -Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                }

                if (away.sqrMagnitude <= 0.000001f)
                {
                    away = Vector3.back;
                }

                Vector3 boostVelocity =
                    (away.normalized + Vector3.up).normalized * _config.BoostForce;
                planarVelocity = Vector3.ProjectOnPlane(boostVelocity, Vector3.up);
                verticalVelocity = Mathf.Max(
                    boostVelocity.y,
                    CalculateJumpVelocity(_config.JumpHeight));
                _airJumpsUsed = 0;
                _coyoteRemaining = 0f;
                _boostCooldownRemaining = _config.BoostCooldown;
                LastJumpKind = ActJumpKind.Boost;
                _inputBuffer.TryConsumeJump();
                return true;
            }

            if (grounded || _coyoteRemaining > 0f)
            {
                verticalVelocity = CalculateJumpVelocity(_config.JumpHeight);
                _coyoteRemaining = 0f;
                LastJumpKind = ActJumpKind.Ground;
                _inputBuffer.TryConsumeJump();
                return true;
            }

            if (_airJumpsUsed >= MaximumAirJumps)
            {
                return false;
            }

            verticalVelocity = CalculateJumpVelocity(_config.AirJumpHeight);
            _airJumpsUsed++;
            LastJumpKind = ActJumpKind.Air;
            _inputBuffer.TryConsumeJump();
            return true;
        }

        private float CalculateJumpVelocity(float height)
        {
            return Mathf.Sqrt(2f * Mathf.Abs(_config.Gravity) * Mathf.Max(0f, height));
        }

        private void EnterActModeIfNeeded()
        {
            if (_wasActMode)
            {
                return;
            }

            _wasActMode = true;
            _lastProcessedFrame = -1;
            InitializeCameraAngles();
            ApplyCameraLook(Vector2.zero);
        }

        private void HandleInactiveMode(GameMode mode)
        {
            if (_wasActMode)
            {
                _flight?.ClearTransientInput();
            }
            _inputBuffer.Clear();
            _lastProcessedFrame = -1;
            _wasActMode = false;
            if (mode == GameMode.RTS &&
                _hybridControl != null &&
                _hybridControl.State == HybridControlState.Autopilot)
            {
                ResetActState(true);
            }
        }

        private void ResetActState(bool resetMotor)
        {
            _inputBuffer.Clear();
            _airJumpsUsed = 0;
            _coyoteRemaining = 0f;
            _boostCooldownRemaining = 0f;
            LastJumpKind = ActJumpKind.None;
            if (resetMotor && _motor != null)
            {
                _motor.ResetMotion();
            }
        }

        private void InitializeCameraAngles()
        {
            if (_cameraAnglesInitialized || _actCameraTarget == null)
            {
                return;
            }

            Vector3 euler = _actCameraTarget.rotation.eulerAngles;
            _cameraYaw = euler.y;
            _cameraPitch = NormalizeSignedAngle(euler.x);
            if (_config != null)
            {
                _cameraPitch = Mathf.Clamp(
                    _cameraPitch,
                    _config.MinimumPitch,
                    _config.MaximumPitch);
            }

            _cameraAnglesInitialized = true;
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void ApplyCameraRotation()
        {
            _actCameraTarget.rotation = Quaternion.Euler(
                _cameraPitch,
                _cameraYaw,
                0f);
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

            if (_weaponActions == null)
            {
                _weaponActions = GetComponent<WeaponActionController>();
            }

            if (_combatStatuses == null)
            {
                _combatStatuses = GetComponent<CombatStatusController>();
            }

            if (_movementTraits == null)
            {
                _movementTraits = GetComponent<DirectMovementTraits>();
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
