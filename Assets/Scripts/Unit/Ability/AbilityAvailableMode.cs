using System;
using UnityEngine;

namespace Unit.Ability
{
    public enum AbilityAvailableMode
    {
        RTS = 0,
        ACT = 1,
        FPS = 2,
        [InspectorName("ACT+FPS")]
        ACTAndFPS = 3,
        [InspectorName("all")]
        All = 4
    }

    public static class AbilityAvailableModeExtensions
    {
        public static bool IsAvailableInMode(this AbilityAvailableMode availableMode, string modeName)
        {
            switch (availableMode)
            {
                case AbilityAvailableMode.RTS:
                    return string.Equals(modeName, "RTS", StringComparison.OrdinalIgnoreCase);

                case AbilityAvailableMode.ACT:
                    return string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase);

                case AbilityAvailableMode.FPS:
                    return string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase);

                case AbilityAvailableMode.ACTAndFPS:
                    return string.Equals(modeName, "ACT", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(modeName, "FPS", StringComparison.OrdinalIgnoreCase);

                case AbilityAvailableMode.All:
                default:
                    return true;
            }
        }

        public static string ToDisplayLabel(this AbilityAvailableMode availableMode)
        {
            switch (availableMode)
            {
                case AbilityAvailableMode.RTS:
                    return "RTS";

                case AbilityAvailableMode.ACT:
                    return "ACT";

                case AbilityAvailableMode.FPS:
                    return "FPS";

                case AbilityAvailableMode.ACTAndFPS:
                    return "ACT+FPS";

                case AbilityAvailableMode.All:
                default:
                    return "all";
            }
        }
    }
}
