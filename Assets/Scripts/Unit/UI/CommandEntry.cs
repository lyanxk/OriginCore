using System;
using UnityEngine;

public enum CommandEntryType
{
    Command = 0,
    Ability = 1
}

public static class CommandEntryIds
{
    public const string Move = "command.move";
    public const string Attack = "command.attack";
    public const string Stop = "command.stop";
}

[Serializable]
public struct CommandEntry
{
    public string Id;
    public Sprite Icon;
    public string Name;
    public string HotkeyText;
    public bool Enabled;

    [Range(0f, 1f)]
    public float Cooldown01;

    [TextArea]
    public string Tooltip;

    public CommandEntryType Type;
}
