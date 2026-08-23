using OriginCore.Buildings;
using OriginCore.Combat;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class BuildCommand : IRtsCommand, IRtsCommandRoutePointProvider
    {
        private Builder _builder;
        private NavMeshMovementDriver _movement;
        private ResourceService _resources;
        private GameObject _owner;
        private bool _costCommitted;
        private bool _constructionStarted;

        public BuildCommand(BuildingDefinition definition, Vector3 position)
        {
            Definition = definition;
            Position = position;
        }

        public BuildingDefinition Definition { get; }
        public Vector3 Position { get; }
        public string DisplayName => Definition != null
            ? "Build " + Definition.DisplayName
            : "Build";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;
        public RtsRouteStyle RouteStyle => RtsRouteStyle.Standard;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _owner = context.Owner;
            _builder = _owner != null ? _owner.GetComponent<Builder>() : null;
            _movement = context.MovementDriver;
            if (_builder == null || _movement == null || Definition == null)
            {
                Fail("The unit cannot execute construction commands.");
                return;
            }

            if (!_builder.TryValidatePlacement(
                    Definition,
                    Position,
                    out _,
                    out Vector3 approachPosition,
                    out string placementError))
            {
                Fail(placementError);
                return;
            }

            if (!AppRoot.TryGetInstance(out AppRoot appRoot) || appRoot.Services == null)
            {
                Fail("Resource service is unavailable.");
                return;
            }

            _resources = appRoot.Services.ResourceService;
            if (!_resources.TrySpend(Definition.ConstructionCost))
            {
                Fail("Insufficient resources for construction.");
                return;
            }

            _costCommitted = true;
            if (!_movement.TryBeginMove(approachPosition, out string movementError))
            {
                RefundCost();
                Fail(movementError);
                return;
            }

            Status = CommandStatus.Running;
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running)
            {
                return;
            }

            NavMeshMovementStatus movementStatus = _movement != null
                ? _movement.TickMovement(deltaTime)
                : NavMeshMovementStatus.Failed;
            if (movementStatus == NavMeshMovementStatus.Moving)
            {
                return;
            }

            if (movementStatus != NavMeshMovementStatus.Arrived)
            {
                RefundCost();
                Fail(_movement != null ? _movement.LastError : "Construction movement failed.");
                return;
            }

            if (!_builder.TryPlace(Definition, Position, out BuildingRuntime _, out string error))
            {
                RefundCost();
                Fail(error);
                return;
            }

            _constructionStarted = true;
            _costCommitted = false;
            Status = CommandStatus.Succeeded;
            VitalsComponent vitals = _owner != null
                ? _owner.GetComponent<VitalsComponent>()
                : null;
            vitals?.SetHealth(0f);
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            _movement?.CancelMovement();
            if (!_constructionStarted)
            {
                RefundCost();
            }

            Status = CommandStatus.Cancelled;
            FailureReason = string.Empty;
        }

        public bool TryGetRoutePoint(out Vector3 worldPoint)
        {
            worldPoint = Position;
            return true;
        }

        private void RefundCost()
        {
            if (!_costCommitted || _resources == null || Definition == null)
            {
                return;
            }

            _resources.Refund(Definition.ConstructionCost);
            _costCommitted = false;
        }

        private void Fail(string reason)
        {
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Construction command failed."
                : reason;
            Status = CommandStatus.Failed;
        }
    }
}
