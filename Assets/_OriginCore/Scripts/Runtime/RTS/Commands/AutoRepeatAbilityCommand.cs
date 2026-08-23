using OriginCore.Abilities;
using OriginCore.Content;
using OriginCore.Core;
using OriginCore.Visibility;
using UnityEngine;

namespace OriginCore.RTS.Commands
{
    public sealed class AutoRepeatAbilityCommand : IRtsCommand
    {
        private readonly AbilityDefinition _definition;
        private readonly AbilityTarget _target;
        private AbilityLoadout _loadout;
        private UnitCommandContext _context;

        public AutoRepeatAbilityCommand(AbilityDefinition definition, AbilityTarget target)
        {
            _definition = definition;
            _target = target;
        }

        public string DisplayName => _definition != null
            ? _definition.DisplayName + " (Channel)"
            : "Channel";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _context = context;
            _loadout = context.Owner != null
                ? context.Owner.GetComponent<AbilityLoadout>()
                : null;
            if (_definition == null || _loadout == null || !IsTargetVisible())
            {
                Fail("The channel ability or its visible target is unavailable.");
                return;
            }

            context.MovementDriver?.CancelMovement();
            if (!TryCast())
            {
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

            if (!IsTargetVisible())
            {
                Fail("The channel target is no longer visible.");
                return;
            }

            if (!_loadout.TryGetRuntime(_definition.ContentId, out AbilityRuntime runtime) ||
                !runtime.IsReady)
            {
                return;
            }

            TryCast();
        }

        public void Cancel()
        {
            if (!Status.IsTerminal())
            {
                Status = CommandStatus.Cancelled;
                FailureReason = string.Empty;
            }
        }

        private bool TryCast()
        {
            if (_loadout.TryCast(
                    _definition,
                    OriginCore.Core.GameMode.RTS,
                    _target,
                    out AbilityCastResult result))
            {
                return true;
            }

            Fail(result.Message);
            return false;
        }

        private bool IsTargetVisible()
        {
            if (!_target.HasPoint)
            {
                return false;
            }

            VisibilitySystem visibility = AppRoot.TryGetInstance(out AppRoot appRoot) &&
                                          appRoot.Services != null &&
                                          appRoot.Services.CurrentSceneContext != null
                ? appRoot.Services.CurrentSceneContext.VisibilitySystem
                : null;
            return visibility == null ||
                   visibility.GetState(_target.Point) == VisibilityCellState.Visible;
        }

        private void Fail(string reason)
        {
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "Channel ability failed."
                : reason;
            Status = CommandStatus.Failed;
        }
    }
}
