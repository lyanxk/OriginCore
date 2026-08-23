namespace OriginCore.RTS.Commands
{
    public enum CommandStatus
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4
    }

    public static class CommandStatusExtensions
    {
        public static bool IsTerminal(this CommandStatus status)
        {
            return status == CommandStatus.Succeeded ||
                   status == CommandStatus.Failed ||
                   status == CommandStatus.Cancelled;
        }
    }

    public enum CommandRejectionReason
    {
        None = 0,
        InvalidCommand = 1,
        InvalidState = 2,
        QueueFull = 3,
        MissingDriver = 4,
        MissingAttackCapability = 5,
        MissingTargetScanner = 6
    }
}
