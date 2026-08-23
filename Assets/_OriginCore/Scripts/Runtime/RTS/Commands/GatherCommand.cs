using OriginCore.Core;
using OriginCore.Economy;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class GatherCommand : IRtsCommand, IRtsCommandRoutePointProvider
    {
        private enum Phase
        {
            MovingToNode,
            Harvesting,
            MovingToDropoff,
            Depositing
        }

        private readonly ResourceNode _node;
        private NavMeshMovementDriver _movement;
        private Gatherer _gatherer;
        private EntityIdentity _identity;
        private FactionMember _faction;
        private ResourceDropoff _dropoff;
        private Phase _phase;

        public GatherCommand(ResourceNode node)
        {
            _node = node;
        }

        public string DisplayName => "Gather";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;
        public RtsRouteStyle RouteStyle => RtsRouteStyle.Standard;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _movement = context.MovementDriver;
            _identity = context.Identity;
            _gatherer = context.Owner != null ? context.Owner.GetComponent<Gatherer>() : null;
            _faction = context.Owner != null ? context.Owner.GetComponent<FactionMember>() : null;
            if (_node == null || _node.IsDepleted || _movement == null ||
                _gatherer == null || _identity == null)
            {
                Fail("Gather target or worker gathering capability is unavailable.");
                return;
            }

            Status = CommandStatus.Running;
            if (_gatherer.CarriedAmount > 0)
            {
                if (!BeginMoveToDropoff())
                {
                    Fail("No compatible resource dropoff is available.");
                }
            }
            else
            {
                BeginMoveToNode();
            }
        }

        public void Tick(float deltaTime)
        {
            if (Status != CommandStatus.Running)
            {
                return;
            }

            if (_node == null)
            {
                Fail("Resource node was removed.");
                return;
            }

            switch (_phase)
            {
                case Phase.MovingToNode:
                    TickMove(true, deltaTime);
                    break;
                case Phase.Harvesting:
                    TickHarvest(deltaTime);
                    break;
                case Phase.MovingToDropoff:
                    TickMove(false, deltaTime);
                    break;
                case Phase.Depositing:
                    DepositAndContinue();
                    break;
            }
        }

        public void Cancel()
        {
            if (Status.IsTerminal())
            {
                return;
            }

            _movement?.CancelMovement();
            Status = CommandStatus.Cancelled;
            FailureReason = string.Empty;
        }

        public bool TryGetRoutePoint(out Vector3 worldPoint)
        {
            if (_phase == Phase.MovingToDropoff && _dropoff != null)
            {
                worldPoint = _dropoff.transform.position;
                return true;
            }

            worldPoint = _node != null ? _node.transform.position : Vector3.zero;
            return _node != null;
        }

        private void BeginMoveToNode()
        {
            _phase = Phase.MovingToNode;
            if (!_movement.TryBeginMove(_node.transform.position, out string error))
            {
                Fail(error);
            }
        }

        private bool BeginMoveToDropoff()
        {
            _dropoff = FindNearestDropoff();
            if (_dropoff == null)
            {
                return false;
            }

            _phase = Phase.MovingToDropoff;
            if (!_movement.TryBeginMove(_dropoff.transform.position, out string error))
            {
                FailureReason = error;
                return false;
            }

            return true;
        }

        private void TickMove(bool towardNode, float deltaTime)
        {
            NavMeshMovementStatus movementStatus = _movement.TickMovement(deltaTime);
            if (movementStatus == NavMeshMovementStatus.Failed)
            {
                Fail(_movement.LastError);
                return;
            }
            if (movementStatus == NavMeshMovementStatus.Cancelled)
            {
                Status = CommandStatus.Cancelled;
                return;
            }
            if (movementStatus != NavMeshMovementStatus.Arrived)
            {
                return;
            }

            _phase = towardNode ? Phase.Harvesting : Phase.Depositing;
        }

        private void TickHarvest(float deltaTime)
        {
            if (_node.IsDepleted && _gatherer.CarriedAmount == 0)
            {
                Status = CommandStatus.Succeeded;
                return;
            }

            _gatherer.Harvest(_node, deltaTime);
            if (!_gatherer.IsFull && !_node.IsDepleted)
            {
                return;
            }

            if (_gatherer.CarriedAmount <= 0)
            {
                Status = CommandStatus.Succeeded;
                return;
            }

            if (!BeginMoveToDropoff())
            {
                Fail("No compatible resource dropoff is available.");
            }
        }

        private void DepositAndContinue()
        {
            FactionId faction = _faction != null ? _faction.Faction : FactionId.Friendly;
            if (_gatherer.DepositTo(_dropoff, faction) <= 0)
            {
                Fail("Resource delivery failed.");
                return;
            }

            if (_node.IsDepleted)
            {
                Status = CommandStatus.Succeeded;
                return;
            }

            BeginMoveToNode();
        }

        private ResourceDropoff FindNearestDropoff()
        {
            if (!AppRoot.TryGetInstance(out AppRoot root) || root.Services == null)
            {
                return null;
            }

            FactionId faction = _faction != null ? _faction.Faction : FactionId.Friendly;
            ResourceDropoff nearest = null;
            float nearestDistance = float.PositiveInfinity;
            var entities = root.Services.EntityRegistry.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityIdentity entity = entities[i];
                ResourceDropoff candidate = entity != null
                    ? entity.GetComponent<ResourceDropoff>()
                    : null;
                if (candidate == null ||
                    !candidate.CanAccept(_gatherer.CarriedType, faction))
                {
                    continue;
                }

                float distance = (candidate.transform.position - _identity.transform.position)
                    .sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        private void Fail(string reason)
        {
            _movement?.CancelMovement();
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Gather command failed."
                : reason;
            Status = CommandStatus.Failed;
        }
    }
}
