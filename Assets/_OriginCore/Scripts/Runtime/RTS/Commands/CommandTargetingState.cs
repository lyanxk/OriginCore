namespace OriginCore.RTS.Commands
{
    public enum CommandTargetingState
    {
        None = 0,
        Move = 1,
        Attack = 2,
        SetRally = 3,
        Ability = 4,
        Build = 5,
        DeployProduction = 6
    }

    public readonly struct CommandTargetingChange
    {
        public CommandTargetingChange(
            CommandTargetingState previous,
            CommandTargetingState current,
            string prompt)
        {
            Previous = previous;
            Current = current;
            Prompt = prompt ?? string.Empty;
        }

        public CommandTargetingState Previous { get; }
        public CommandTargetingState Current { get; }
        public string Prompt { get; }
    }
}
