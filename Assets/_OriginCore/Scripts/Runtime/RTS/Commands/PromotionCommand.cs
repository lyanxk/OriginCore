using OriginCore.Progression;

namespace OriginCore.RTS.Commands
{
    public sealed class PromotionCommand : IRtsCommand
    {
        private PromotionController _promotion;

        public string DisplayName => "Promote";
        public CommandStatus Status { get; private set; } = CommandStatus.Pending;
        public string FailureReason { get; private set; } = string.Empty;

        public void Begin(UnitCommandContext context)
        {
            if (Status != CommandStatus.Pending)
            {
                return;
            }

            _promotion = context.Owner != null
                ? context.Owner.GetComponent<PromotionController>()
                : null;
            string error = string.Empty;
            if (_promotion == null || !_promotion.TryPromote(
                    out _,
                    out PromotionFailure _,
                    out error))
            {
                FailureReason = string.IsNullOrWhiteSpace(error)
                    ? "Promotion failed."
                    : error;
                Status = CommandStatus.Failed;
                return;
            }

            Status = CommandStatus.Succeeded;
        }

        public void Tick(float deltaTime)
        {
        }

        public void Cancel()
        {
            if (!Status.IsTerminal())
            {
                Status = CommandStatus.Cancelled;
            }
        }
    }
}
