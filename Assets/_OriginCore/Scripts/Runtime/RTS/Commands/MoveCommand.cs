using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class MoveCommand : IRtsCommand, IRtsCommandRoutePointProvider
    {
        private NavMeshMovementDriver _movementDriver;

        public MoveCommand(Vector3 destination)
        {
            Destination = destination;
        }

        public string DisplayName => "Move";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;
        public Vector3 Destination { get; }
        public RtsRouteStyle RouteStyle => RtsRouteStyle.Standard;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _movementDriver = context.MovementDriver;
            if (_movementDriver == null)
            {
                FailureReason = "The unit has no NavMesh movement driver.";
                Status = CommandStatus.Failed;
                return;
            }

            if (!_movementDriver.TryBeginMove(Destination, out string error))
            {
                FailureReason = error;
                Status = CommandStatus.Failed;
                return;
            }

            FailureReason = string.Empty;
            Status = CommandStatus.Running;
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running)
            {
                return;
            }

            NavMeshMovementStatus movementStatus = _movementDriver != null
                ? _movementDriver.TickMovement(deltaTime)
                : NavMeshMovementStatus.Failed;
            switch (movementStatus)
            {
                case NavMeshMovementStatus.Arrived:
                    Status = CommandStatus.Succeeded;
                    break;
                case NavMeshMovementStatus.Failed:
                    FailureReason = _movementDriver != null
                        ? _movementDriver.LastError
                        : "The unit lost its NavMesh movement driver.";
                    Status = CommandStatus.Failed;
                    break;
                case NavMeshMovementStatus.Cancelled:
                    Status = CommandStatus.Cancelled;
                    break;
            }
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            _movementDriver?.CancelMovement();
            FailureReason = string.Empty;
            Status = CommandStatus.Cancelled;
        }

        public bool TryGetRoutePoint(out Vector3 worldPoint)
        {
            worldPoint = Destination;
            return true;
        }
    }
}
