using OriginCore.Combat;
using OriginCore.Gameplay;
using OriginCore.RTS;
using OriginCore.Units;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public interface IRtsCommand
    {
        string DisplayName { get; }
        CommandStatus Status { get; }
        string FailureReason { get; }
        void Begin(UnitCommandContext context);
        void Tick(float deltaTime);
        void Cancel();
    }

    public interface IRtsCommandRoutePointProvider
    {
        RtsRouteStyle RouteStyle { get; }
        bool TryGetRoutePoint(out Vector3 worldPoint);
    }

    public readonly struct UnitCommandContext
    {
        public UnitCommandContext(
            UnitCommandQueue queue,
            GameObject owner,
            EntityIdentity identity,
            NavMeshMovementDriver movementDriver,
            AttackCapability attackCapability,
            AutoTargetScanner targetScanner)
        {
            Queue = queue;
            Owner = owner;
            Identity = identity;
            MovementDriver = movementDriver;
            AttackCapability = attackCapability;
            TargetScanner = targetScanner;
        }

        public UnitCommandQueue Queue { get; }
        public GameObject Owner { get; }
        public EntityIdentity Identity { get; }
        public NavMeshMovementDriver MovementDriver { get; }
        public AttackCapability AttackCapability { get; }
        public AutoTargetScanner TargetScanner { get; }
    }
}
