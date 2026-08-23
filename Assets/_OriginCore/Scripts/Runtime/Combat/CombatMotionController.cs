using OriginCore.Content;
using OriginCore.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatStatusController))]
    public sealed class CombatMotionController : MonoBehaviour
    {
        [SerializeField] private CombatStatusController _statuses;
        [SerializeField] private HybridPawnMotor _motor;
        [SerializeField] private HybridControlDriver _hybridControl;
        [SerializeField] private NavMeshAgent _agent;
        [Min(0.1f), SerializeField] private float _defaultDisplacementSpeed = 8f;
        [Min(0.1f), SerializeField] private float _launchSpeed = 8f;

        private CombatDisplacementRequest _request;
        private Vector3 _velocity;
        private Vector3 _launchStart;
        private Vector3 _launchLanding;
        private float _remaining;
        private float _duration;
        private float _elapsed;
        private float _launchHeight;
        private bool _hasDisplacement;
        private bool _isLaunch;
        private bool _agentStopCaptured;
        private bool _agentWasStopped;
        private bool _agentPositionCaptured;
        private bool _agentWasUpdatingPosition;

        public bool HasDisplacement => _hasDisplacement;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            BindEvents();
            ApplyTemporalLock();
        }

        private void OnDisable()
        {
            UnbindEvents();
            CancelDisplacement();
            RestoreAgentStopState();
        }

        private void OnValidate()
        {
            CacheComponents();
            _defaultDisplacementSpeed = Mathf.Max(0.1f, _defaultDisplacementSpeed);
            _launchSpeed = Mathf.Max(0.1f, _launchSpeed);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (!_hasDisplacement || deltaTime <= 0f)
            {
                return;
            }

            float step = Mathf.Min(deltaTime, _remaining);
            if (_isLaunch)
            {
                TickLaunch(step);
            }
            else
            {
                Move(_velocity, step);
            }
            _remaining = Mathf.Max(0f, _remaining - step);
            if (_remaining <= 0f)
            {
                CancelDisplacement();
            }
        }

        public void CancelDisplacement()
        {
            if (_isLaunch)
            {
                ApplyLaunchPosition(_launchLanding, 0.02f);
            }
            RestoreAgentPositionOwnership();
            _request = default;
            _velocity = Vector3.zero;
            _launchStart = Vector3.zero;
            _launchLanding = Vector3.zero;
            _remaining = 0f;
            _duration = 0f;
            _elapsed = 0f;
            _launchHeight = 0f;
            _hasDisplacement = false;
            _isLaunch = false;
            if (_statuses == null || !_statuses.IsTemporallyLocked)
            {
                RestoreAgentStopState();
            }
        }

        private void HandleDisplacementRequested(
            CombatStatusController controller,
            CombatDisplacementRequest request)
        {
            if (_hasDisplacement)
            {
                CancelDisplacement();
            }

            _request = request;
            _duration = Mathf.Max(0.02f, request.Duration);
            _remaining = _duration;
            _elapsed = 0f;
            _isLaunch = request.Type == WeaponStatusType.Launch ||
                        request.Type == WeaponStatusType.LaunchPair;
            if (_isLaunch)
            {
                PrepareLaunch(request);
                _hasDisplacement = true;
            }
            else
            {
                _velocity = ResolveVelocity(request, _duration);
                _hasDisplacement = _velocity.sqrMagnitude > 0.000001f;
            }
            if (_hasDisplacement)
            {
                StopAgent();
                if (_isLaunch)
                {
                    CaptureAgentPositionOwnership();
                }
            }
        }

        private void PrepareLaunch(CombatDisplacementRequest request)
        {
            float distance = request.Magnitude > 0f
                ? request.Magnitude
                : _launchSpeed * _duration * 0.5f;
            Vector3 planar = Vector3.ProjectOnPlane(request.Direction, Vector3.up);
            if (planar.sqrMagnitude > 0.000001f)
            {
                planar.Normalize();
            }

            float planarFactor = request.Type == WeaponStatusType.LaunchPair
                ? 0.25f
                : 0.35f;
            _launchStart = transform.position;
            _launchLanding = _launchStart + planar * (distance * planarFactor);
            _launchHeight = Mathf.Max(0.5f, distance);
            _velocity = Vector3.up;
        }

        private void TickLaunch(float deltaTime)
        {
            _elapsed = Mathf.Min(_duration, _elapsed + deltaTime);
            float progress = _duration > 0f
                ? Mathf.Clamp01(_elapsed / _duration)
                : 1f;
            float arc = 4f * progress * (1f - progress) * _launchHeight;
            Vector3 position = Vector3.Lerp(_launchStart, _launchLanding, progress) +
                               Vector3.up * arc;
            ApplyLaunchPosition(position, deltaTime);
        }

        private void ApplyLaunchPosition(Vector3 position, float deltaTime)
        {
            Vector3 displacement = position - transform.position;
            if (_motor != null && _motor.CharacterController != null &&
                _motor.CharacterController.enabled)
            {
                float step = Mathf.Max(0.0001f, deltaTime);
                _motor.Move(
                    Vector3.ProjectOnPlane(displacement, Vector3.up) / step,
                    displacement.y / step,
                    step);
                return;
            }

            transform.position = position;
        }

        private Vector3 ResolveVelocity(
            CombatDisplacementRequest request,
            float duration)
        {
            float distance = request.Magnitude > 0f
                ? request.Magnitude
                : _defaultDisplacementSpeed * duration;
            Vector3 planar = Vector3.ProjectOnPlane(request.Direction, Vector3.up);
            if (request.Type == WeaponStatusType.Pull && request.Source != null)
            {
                planar = Vector3.ProjectOnPlane(
                    request.Source.transform.position - transform.position,
                    Vector3.up);
            }
            if (planar.sqrMagnitude > 0.000001f)
            {
                planar.Normalize();
            }

            switch (request.Type)
            {
                case WeaponStatusType.Push:
                case WeaponStatusType.Pull:
                    return planar * (distance / duration);
                case WeaponStatusType.Launch:
                    return planar * (distance * 0.35f / duration) +
                           Vector3.up * Mathf.Max(_launchSpeed, distance / duration);
                case WeaponStatusType.LaunchPair:
                    return planar * (distance * 0.25f / duration) +
                           Vector3.up * Mathf.Max(_launchSpeed, distance / duration);
                case WeaponStatusType.GroundSlam:
                    return Vector3.down * Mathf.Max(_launchSpeed, distance / duration);
                default:
                    return Vector3.zero;
            }
        }

        private void Move(Vector3 velocity, float deltaTime)
        {
            if (_motor != null && _motor.CharacterController != null &&
                _motor.CharacterController.enabled)
            {
                _motor.Move(
                    Vector3.ProjectOnPlane(velocity, Vector3.up),
                    velocity.y,
                    deltaTime);
                if (_request.Type == WeaponStatusType.GroundSlam && _motor.IsGrounded)
                {
                    CancelDisplacement();
                }
                return;
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                // NavMeshAgent.Move keeps world/NavMesh ownership and collision handling.
                // Vertical launch is represented by its planar component for autopilot units;
                // direct-controlled pawns use HybridPawnMotor for the full trajectory.
                _agent.Move(Vector3.ProjectOnPlane(velocity, Vector3.up) * deltaTime);
            }
        }

        private void HandleStatusStarted(
            CombatStatusController controller,
            WeaponStatusType type,
            int handle)
        {
            if (type == WeaponStatusType.TemporalLock)
            {
                StopAgent();
            }
        }

        private void HandleStatusEnded(
            CombatStatusController controller,
            WeaponStatusType type,
            int handle)
        {
            if (type == WeaponStatusType.TemporalLock &&
                !controller.IsTemporallyLocked && !_hasDisplacement)
            {
                RestoreAgentStopState();
            }
        }

        private void ApplyTemporalLock()
        {
            if (_statuses != null && _statuses.IsTemporallyLocked)
            {
                StopAgent();
            }
        }

        private void StopAgent()
        {
            CacheComponents();
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }
            if (!_agentStopCaptured)
            {
                _agentWasStopped = _agent.isStopped;
                _agentStopCaptured = true;
            }
            _agent.isStopped = true;
        }

        private void CaptureAgentPositionOwnership()
        {
            if (_agentPositionCaptured || _agent == null || !_agent.enabled ||
                !_agent.isOnNavMesh)
            {
                return;
            }

            _agentWasUpdatingPosition = _agent.updatePosition;
            _agentPositionCaptured = true;
            _launchLanding = _agent.nextPosition +
                             Vector3.ProjectOnPlane(
                                 _launchLanding - _launchStart,
                                 Vector3.up);
            _agent.updatePosition = false;
        }

        private void RestoreAgentPositionOwnership()
        {
            if (!_agentPositionCaptured)
            {
                return;
            }

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.nextPosition = transform.position;
                _agent.updatePosition = _agentWasUpdatingPosition;
            }
            _agentPositionCaptured = false;
        }

        private void RestoreAgentStopState()
        {
            if (!_agentStopCaptured)
            {
                return;
            }
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = _agentWasStopped;
            }
            _agentStopCaptured = false;
        }

        private void BindEvents()
        {
            UnbindEvents();
            if (_statuses == null)
            {
                return;
            }
            _statuses.DisplacementRequested += HandleDisplacementRequested;
            _statuses.StatusStarted += HandleStatusStarted;
            _statuses.StatusEnded += HandleStatusEnded;
        }

        private void UnbindEvents()
        {
            if (_statuses == null)
            {
                return;
            }
            _statuses.DisplacementRequested -= HandleDisplacementRequested;
            _statuses.StatusStarted -= HandleStatusStarted;
            _statuses.StatusEnded -= HandleStatusEnded;
        }

        private void CacheComponents()
        {
            if (_statuses == null) _statuses = GetComponent<CombatStatusController>();
            if (_motor == null) _motor = GetComponent<HybridPawnMotor>();
            if (_hybridControl == null) _hybridControl = GetComponent<HybridControlDriver>();
            if (_agent == null)
            {
                _agent = _hybridControl != null
                    ? _hybridControl.NavMeshAgent
                    : GetComponent<NavMeshAgent>();
            }
        }
    }
}
