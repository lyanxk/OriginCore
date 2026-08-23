using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class AttackMoveCommand : IRtsCommand, IRtsCommandRoutePointProvider
    {
        private const float ScanInterval = 0.25f;
        private const float RepathInterval = 0.25f;
        private const float RepathDistance = 0.5f;
        private const float TargetLeashExtraRange = 2f;

        private readonly Vector3 _destination;
        private readonly float _requestedAggroRadius;
        private AttackCapability _attackCapability;
        private AutoTargetScanner _targetScanner;
        private NavMeshMovementDriver _movementDriver;
        private EntityIdentity _engagedTarget;
        private float _scanRemaining;
        private float _repathRemaining;
        private Vector3 _lastChasePoint;
        private bool _hasChasePoint;
        private bool _travellingToDestination;

        public AttackMoveCommand(Vector3 destination, float aggroRadius = -1f)
        {
            _destination = destination;
            _requestedAggroRadius = aggroRadius;
        }

        public string DisplayName => "Attack Move";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;
        public Vector3 Destination => _destination;
        public EntityIdentity EngagedTarget => _engagedTarget;
        public RtsRouteStyle RouteStyle => RtsRouteStyle.Attack;
        public float AggroRadius => _requestedAggroRadius > 0f
            ? _requestedAggroRadius
            : _attackCapability != null ? _attackCapability.AggroRadius : 0f;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _attackCapability = context.AttackCapability;
            _targetScanner = context.TargetScanner;
            _movementDriver = context.MovementDriver;
            if (_attackCapability == null)
            {
                Fail("The unit has no attack capability.");
                return;
            }

            if (_targetScanner == null)
            {
                Fail("The unit has no automatic target scanner.");
                return;
            }

            if (_movementDriver == null)
            {
                Fail("The unit has no NavMesh movement driver.");
                return;
            }

            FailureReason = string.Empty;
            Status = CommandStatus.Running;
            _scanRemaining = 0f;
            if (!BeginDestinationMove())
            {
                return;
            }
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running)
            {
                return;
            }

            float safeDelta = Mathf.Max(0f, deltaTime);
            _scanRemaining -= safeDelta;
            _repathRemaining -= safeDelta;

            if (_engagedTarget != null &&
                (!_targetScanner.IsValidVisibleHostile(_engagedTarget) ||
                 !_attackCapability.IsInRange(
                     _engagedTarget,
                     TargetLeashExtraRange)))
            {
                ClearTargetAndResume();
                if (Status != CommandStatus.Running)
                {
                    return;
                }
            }

            if (_engagedTarget == null && _scanRemaining <= 0f)
            {
                _scanRemaining = ScanInterval;
                if (_targetScanner.FindNearestHostile(AggroRadius, out EntityIdentity target))
                {
                    _engagedTarget = target;
                    _travellingToDestination = false;
                    _hasChasePoint = false;
                    _repathRemaining = 0f;
                }
            }

            if (_engagedTarget != null)
            {
                TickEngagement(safeDelta);
                return;
            }

            TickDestinationMove(safeDelta);
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            StopMovement();
            _engagedTarget = null;
            FailureReason = string.Empty;
            Status = CommandStatus.Cancelled;
        }

        public bool TryGetRoutePoint(out Vector3 worldPoint)
        {
            worldPoint = _engagedTarget != null
                ? _engagedTarget.transform.position
                : _destination;
            return true;
        }

        private void TickEngagement(float deltaTime)
        {
            if (!_attackCapability.IsValidHostileTarget(_engagedTarget))
            {
                ClearTargetAndResume();
                return;
            }

            if (_attackCapability.IsInRange(_engagedTarget) &&
                _attackCapability.HasLineOfSight(_engagedTarget))
            {
                StopMovement();
                _attackCapability.FaceTarget(_engagedTarget);
                AttackAttemptResult result = _attackCapability.TryAttack(
                    _engagedTarget,
                    out DamageResult _);
                if (result == AttackAttemptResult.InvalidTarget ||
                    result == AttackAttemptResult.NoDamageApplied &&
                    !_attackCapability.IsValidHostileTarget(_engagedTarget))
                {
                    ClearTargetAndResume();
                }

                return;
            }

            Vector3 chasePoint = _engagedTarget.transform.position;
            Vector3 chaseDelta = chasePoint - _lastChasePoint;
            chaseDelta.y = 0f;
            if (!_hasChasePoint || _repathRemaining <= 0f ||
                chaseDelta.sqrMagnitude >= RepathDistance * RepathDistance)
            {
                if (!_movementDriver.TryBeginMove(chasePoint, out string error))
                {
                    Fail(error);
                    return;
                }

                _lastChasePoint = chasePoint;
                _hasChasePoint = true;
                _repathRemaining = RepathInterval;
            }

            if (_movementDriver.TickMovement(deltaTime) == NavMeshMovementStatus.Failed)
            {
                Fail(_movementDriver.LastError);
            }
        }

        private void TickDestinationMove(float deltaTime)
        {
            if (!_travellingToDestination && !BeginDestinationMove())
            {
                return;
            }

            NavMeshMovementStatus movementStatus = _movementDriver.TickMovement(deltaTime);
            if (movementStatus == NavMeshMovementStatus.Arrived)
            {
                FailureReason = string.Empty;
                Status = CommandStatus.Succeeded;
                _travellingToDestination = false;
            }
            else if (movementStatus == NavMeshMovementStatus.Failed)
            {
                Fail(_movementDriver.LastError);
            }
        }

        private bool BeginDestinationMove()
        {
            if (!_movementDriver.TryBeginMove(_destination, out string error))
            {
                Fail(error);
                return false;
            }

            _travellingToDestination = true;
            _hasChasePoint = false;
            return true;
        }

        private void ClearTargetAndResume()
        {
            _engagedTarget = null;
            StopMovement();
            _travellingToDestination = false;
            _hasChasePoint = false;
            BeginDestinationMove();
        }

        private void StopMovement()
        {
            if (_movementDriver != null && _movementDriver.IsMoving)
            {
                _movementDriver.CancelMovement();
            }
        }

        private void Fail(string reason)
        {
            StopMovement();
            _engagedTarget = null;
            _travellingToDestination = false;
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The attack-move command could not continue."
                : reason.Trim();
            Status = CommandStatus.Failed;
        }
    }
}
