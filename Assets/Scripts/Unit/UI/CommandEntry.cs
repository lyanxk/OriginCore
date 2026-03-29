using System;
using UnityEngine;

public enum CommandEntryType
{
    Command = 0,
    Ability = 1,
    Production = 2,
    Passive = 3
}

public static class CommandEntryIds
{
    public const string Move = "command.move";
    public const string Attack = "command.attack";
    public const string Stop = "command.stop";
    public const string ProductionPrefix = "command.production.";

    public static string GetProductionId(int index)
    {
        return $"{ProductionPrefix}{index}";
    }

    public static bool TryParseProductionIndex(string commandId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(commandId))
            return false;

        if (!commandId.StartsWith(ProductionPrefix))
            return false;

        string suffix = commandId.Substring(ProductionPrefix.Length);
        return int.TryParse(suffix, out index);
    }
}

[Serializable]
public struct CommandEntry
{
    public string Id;
    public Sprite Icon;
    public string Name;
    public string HotkeyText;
    public bool Enabled;
    public int SlotIndex;

    [Range(0f, 1f)]
    public float Cooldown01;

    [TextArea]
    public string Tooltip;

    public CommandEntryType Type;
}
