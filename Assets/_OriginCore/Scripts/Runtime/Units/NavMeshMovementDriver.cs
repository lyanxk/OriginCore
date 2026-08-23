using System;
using OriginCore.Combat;
using OriginCore.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace OriginCore.Units
{
    public enum NavMeshMovementStatus
    {
        Idle = 0,
        Moving = 1,
        Arrived = 2,
        Failed = 3,
        Cancelled = 4
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshMovementDriver : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private EntityIdentity _identity;
        [SerializeField] private RuntimeStatBlock _stats;
        [Min(0.05f), SerializeField] private float _targetSampleDistance = 1.5f;
        [Min(0.05f), SerializeField] private float _maximumTargetRecoveryDistance = 6f;
        [Min(0.01f), SerializeField] private float _arrivalTolerance = 0.1f;
        [Min(0f), SerializeField] private float _arrivalVelocity = 0.05f;
        [Min(0.1f), SerializeField] private float _pathPendingTimeout = 2f;

        private float _moveElapsed;

        public NavMeshAgent Agent => _agent;
        public EntityIdentity Identity => _identity;
        public NavMeshMovementStatus Status { get; private set; } = NavMeshMovementStatus.Idle;
        public Vector3 Destination { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public int SetDestinationRequestCount { get; private set; }
        public bool IsMoving => Status == NavMeshMovementStatus.Moving;
        public float TargetSampleDistance => _targetSampleDistance;
        public float MaximumTargetRecoveryDistance => _maximumTargetRecoveryDistance;

        private void Awake()
        {
            CacheComponents();
            ApplyDefinitionSettings();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyDefinitionSettings();
        }

        private void OnValidate()
        {
            CacheComponents();
            _targetSampleDistance = Mathf.Max(0.05f, _targetSampleDistance);
            _maximumTargetRecoveryDistance = Mathf.Max(
                _targetSampleDistance,
                _maximumTargetRecoveryDistance);
            _arrivalTolerance = Mathf.Max(0.01f, _arrivalTolerance);
            _arrivalVelocity = Mathf.Max(0f, _arrivalVelocity);
            _pathPendingTimeout = Mathf.Max(0.1f, _pathPendingTimeout);
            ApplyDefinitionSettings();
        }

        public void Configure(
            NavMeshAgent agent,
            EntityIdentity identity,
            float targetSampleDistance = 1.5f,
            float arrivalTolerance = 0.1f,
            float arrivalVelocity = 0.05f,
            float pathPendingTimeout = 2f)
        {
            _agent = agent != null ? agent : GetComponent<NavMeshAgent>();
            _identity = identity != null ? identity : GetComponent<EntityIdentity>();
            _targetSampleDistance = Mathf.Max(0.05f, targetSampleDistance);
            _arrivalTolerance = Mathf.Max(0.01f, arrivalTolerance);
            _arrivalVelocity = Mathf.Max(0f, arrivalVelocity);
            _pathPendingTimeout = Mathf.Max(0.1f, pathPendingTimeout);
            ApplyDefinitionSettings();
        }

        public bool TryBeginMove(Vector3 requestedDestination, out string error)
        {
            error = string.Empty;
            CacheComponents();
            ApplyDefinitionSettings();

            if (!TryValidateAgent(out error))
            {
                return Fail(error);
            }

            float moveSpeed = _stats != null
                ? _stats.GetStat(RuntimeStatId.MoveSpeed)
                : _identity != null && _identity.Definition != null
                    ? _identity.Definition.MoveSpeed
                    : 0f;
            if (_identity != null && _identity.Definition != null && moveSpeed <= 0f)
            {
                error = "This entity is not movable.";
                return Fail(error);
            }

            try
            {
                if (!NavMeshPositionResolver.TryResolveReachablePosition(
                        _agent,
                        requestedDestination,
                        _targetSampleDistance,
                        _maximumTargetRecoveryDistance,
                        out Vector3 resolvedDestination))
                {
                    error = "No reachable NavMesh point was found near the requested destination.";
                    return Fail(error);
                }

                _agent.isStopped = false;
                if (!_agent.SetDestination(resolvedDestination))
                {
                    error = "NavMeshAgent rejected the destination.";
                    return Fail(error);
                }

                SetDestinationRequestCount++;
                Destination = resolvedDestination;
                LastError = string.Empty;
                _moveElapsed = 0f;
                Status = NavMeshMovementStatus.Moving;
                return true;
            }
            catch (Exception exception)
            {
                error = "NavMesh move could not start: " + exception.Message;
                return Fail(error);
            }
        }

        public NavMeshMovementStatus TickMovement(float deltaTime)
        {
            if (Status != NavMeshMovementStatus.Moving)
            {
                return Status;
            }

            CacheComponents();
            ApplyDefinitionSettings();
            _moveElapsed += Mathf.Max(0f, deltaTime);
            if (!TryValidateAgent(out string error))
            {
                Fail(error);
                return Status;
            }

            try
            {
                if (_agent.pathPending)
                {
                    if (_moveElapsed > _pathPendingTimeout)
                    {
                        Fail("NavMesh path calculation timed out.");
                    }

                    return Status;
                }

                if (_agent.pathStatus != NavMeshPathStatus.PathComplete)
                {
                    Fail("The active NavMesh path became invalid or incomplete.");
                    return Status;
                }

                float remainingDistance = _agent.remainingDistance;
                if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance))
                {
                    if (_moveElapsed > _pathPendingTimeout)
                    {
                        Fail("NavMeshAgent did not produce a finite remaining distance.");
                    }

                    return Status;
                }

                float arrivalDistance = Mathf.Max(_agent.stoppingDistance, _arrivalTolerance);
                if (remainingDistance <= arrivalDistance &&
                    (!_agent.hasPath || _agent.velocity.sqrMagnitude <= _arrivalVelocity * _arrivalVelocity))
                {
                    CompleteArrival();
                }

                return Status;
            }
            catch (Exception exception)
            {
                Fail("NavMesh move failed while updating: " + exception.Message);
                return Status;
            }
        }

        public void CancelMovement()
        {
            ResetAgentPathSafely();
            LastError = string.Empty;
            _moveElapsed = 0f;
            Status = NavMeshMovementStatus.Cancelled;
        }

        public void ResetToIdle()
        {
            ResetAgentPathSafely();
            LastError = string.Empty;
            _moveElapsed = 0f;
            Status = NavMeshMovementStatus.Idle;
        }

        private bool TryValidateAgent(out string error)
        {
            if (_agent == null)
            {
                error = "NavMeshAgent is missing.";
                return false;
            }

            if (!_agent.enabled)
            {
                error = "NavMeshAgent is disabled.";
                return false;
            }

            if (!_agent.isOnNavMesh)
            {
                error = "NavMeshAgent is not placed on a NavMesh.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool Fail(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message)
                ? "NavMesh movement failed."
                : message.Trim();
            _moveElapsed = 0f;
            ResetAgentPathSafely();
            Status = NavMeshMovementStatus.Failed;
            return false;
        }

        private void CompleteArrival()
        {
            ResetAgentPathSafely();
            LastError = string.Empty;
            _moveElapsed = 0f;
            Status = NavMeshMovementStatus.Arrived;
        }

        private void ResetAgentPathSafely()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }

            try
            {
                _agent.ResetPath();
                _agent.isStopped = true;
            }
            catch (Exception)
            {
                // A disappearing or detached agent is a normal cancellation edge case.
            }
        }

        private void CacheComponents()
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_identity == null)
            {
                _identity = GetComponent<EntityIdentity>();
            }

            if (_stats == null)
            {
                _stats = GetComponent<RuntimeStatBlock>();
            }
        }

        private void ApplyDefinitionSettings()
        {
            if (_agent == null || _identity == null || _identity.Definition == null)
            {
                return;
            }

            UnitDefinition definition = _identity.Definition;
            bool useRuntimeStats = Application.isPlaying && _stats != null;
            _agent.speed = useRuntimeStats
                ? Mathf.Max(0f, _stats.GetStat(RuntimeStatId.MoveSpeed))
                : definition.MoveSpeed;
            _agent.acceleration = useRuntimeStats
                ? Mathf.Max(0f, _stats.GetStat(RuntimeStatId.Acceleration))
                : definition.Acceleration;
            _agent.angularSpeed = definition.AngularSpeed;
            _agent.stoppingDistance = definition.StoppingDistance;
        }
    }
}
