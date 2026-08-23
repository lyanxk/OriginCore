using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class AttackCommand : IRtsCommand, IRtsCommandRoutePointProvider
    {
        private const float RepathInterval = 0.25f;
        private const float RepathDistance = 0.5f;

        private readonly EntityIdentity _target;
        private AttackCapability _attackCapability;
        private NavMeshMovementDriver _movementDriver;
        private float _repathRemaining;
        private Vector3 _lastChasePoint;
        private bool _hasChasePoint;

        public AttackCommand(EntityIdentity target)
        {
            _target = target;
        }

        public string DisplayName => "Attack";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;
        public EntityIdentity Target => _target;
        public RtsRouteStyle RouteStyle => RtsRouteStyle.Attack;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _attackCapability = context.AttackCapability;
            _movementDriver = context.MovementDriver;
            if (_attackCapability == null)
            {
                Fail("The unit has no attack capability.");
                return;
            }

            if (!_attackCapability.IsValidHostileTarget(_target))
            {
                Fail("The selected target is not a living hostile entity.");
                return;
            }

            FailureReason = string.Empty;
            Status = CommandStatus.Running;
            _repathRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running)
            {
                return;
            }

            if (!_attackCapability.IsValidHostileTarget(_target))
            {
                Complete();
                return;
            }

            _repathRemaining -= Mathf.Max(0f, deltaTime);
            if (_attackCapability.IsInRange(_target) &&
                _attackCapability.HasLineOfSight(_target))
            {
                StopChasing();
                _attackCapability.FaceTarget(_target);
                AttackAttemptResult result = _attackCapability.TryAttack(
                    _target,
                    out DamageResult _);
                if (result == AttackAttemptResult.InvalidTarget)
                {
                    Complete();
                }
                else if (result == AttackAttemptResult.NoDamageApplied &&
                         !_attackCapability.IsValidHostileTarget(_target))
                {
                    Complete();
                }

                return;
            }

            if (_movementDriver == null)
            {
                Fail("The target is out of range and the unit cannot move.");
                return;
            }

            Vector3 chasePoint = _target.transform.position;
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

            NavMeshMovementStatus movementStatus = _movementDriver.TickMovement(deltaTime);
            if (movementStatus == NavMeshMovementStatus.Failed)
            {
                Fail(_movementDriver.LastError);
            }
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            StopChasing();
            FailureReason = string.Empty;
            Status = CommandStatus.Cancelled;
        }

        public bool TryGetRoutePoint(out Vector3 worldPoint)
        {
            if (_target != null)
            {
                worldPoint = _target.transform.position;
                return true;
            }

            worldPoint = default(Vector3);
            return false;
        }

        private void Complete()
        {
            StopChasing();
            FailureReason = string.Empty;
            Status = CommandStatus.Succeeded;
        }

        private void Fail(string reason)
        {
            StopChasing();
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "The attack command could not continue."
                : reason.Trim();
            Status = CommandStatus.Failed;
        }

        private void StopChasing()
        {
            if (_movementDriver != null && _movementDriver.IsMoving)
            {
                _movementDriver.CancelMovement();
            }
        }
    }
}
