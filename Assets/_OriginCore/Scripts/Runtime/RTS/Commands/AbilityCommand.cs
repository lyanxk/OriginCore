using OriginCore.Abilities;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class AbilityCommand : IRtsCommand
    {
        private readonly AbilityDefinition _definition;
        private readonly AbilityTarget _target;
        private AbilityLoadout _loadout;
        private UnitCommandContext _context;
        private bool _movingIntoRange;

        public AbilityCommand(AbilityDefinition definition, AbilityTarget target)
        {
            _definition = definition;
            _target = target;
        }

        public string DisplayName => _definition != null ? _definition.DisplayName : "Ability";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _context = context;
            _loadout = context.Owner != null ? context.Owner.GetComponent<AbilityLoadout>() : null;
            if (_definition == null || _loadout == null)
            {
                Fail("Ability loadout or definition is unavailable.");
                return;
            }

            if (TryGetTargetPoint(out Vector3 point) && _definition.Range > 0f &&
                !IsInRange(point))
            {
                string movementError = "Movement driver is unavailable.";
                if (context.MovementDriver == null ||
                    !context.MovementDriver.TryBeginMove(point, out movementError))
                {
                    Fail(movementError);
                    return;
                }

                _movingIntoRange = true;
                Status = CommandStatus.Running;
                return;
            }

            CastNow();
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running || !_movingIntoRange)
            {
                return;
            }

            if (TryGetTargetPoint(out Vector3 point) && IsInRange(point))
            {
                _context.MovementDriver.CancelMovement();
                _movingIntoRange = false;
                CastNow();
                return;
            }

            NavMeshMovementStatus movement = _context.MovementDriver.TickMovement(deltaTime);
            if (movement == NavMeshMovementStatus.Arrived)
            {
                _movingIntoRange = false;
                CastNow();
            }
            else if (movement == NavMeshMovementStatus.Failed ||
                     movement == NavMeshMovementStatus.Cancelled)
            {
                Fail(_context.MovementDriver.LastError);
            }
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            if (_movingIntoRange && _context.MovementDriver != null)
            {
                _context.MovementDriver.CancelMovement();
            }

            _movingIntoRange = false;
            Status = CommandStatus.Cancelled;
        }

        private void CastNow()
        {
            if (_loadout.TryCast(
                    _definition,
                    GameMode.RTS,
                    _target,
                    out AbilityCastResult result))
            {
                Status = CommandStatus.Succeeded;
                FailureReason = string.Empty;
            }
            else
            {
                Fail(result.Message);
            }
        }

        private bool TryGetTargetPoint(out Vector3 point)
        {
            if (_target.Entity != null)
            {
                point = _target.Entity.transform.position;
                return true;
            }

            if (_target.HasPoint)
            {
                point = _target.Point;
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private bool IsInRange(Vector3 point)
        {
            Vector3 delta = point - _context.Owner.transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= _definition.Range * _definition.Range;
        }

        private void Fail(string reason)
        {
            Status = CommandStatus.Failed;
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Ability command failed."
                : reason.Trim();
        }
    }
}
