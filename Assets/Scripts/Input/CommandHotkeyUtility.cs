using UnityEngine;

public static class CommandHotkeyUtility
{
    public static string NormalizeToken(string hotkeyToken)
    {
        return string.IsNullOrWhiteSpace(hotkeyToken)
            ? string.Empty
            : hotkeyToken.Trim().ToUpperInvariant();
    }

    public static string ResolveHotkeyToken(CommandEntry entry, int slotIndex)
    {
        if (entry.Type == CommandEntryType.Passive)
            return string.Empty;

        string explicitToken = NormalizeToken(entry.HotkeyText);
        if (!string.IsNullOrEmpty(explicitToken))
            return explicitToken;

        if (entry.Type != CommandEntryType.Ability)
            return string.Empty;

        return GetUnitAbilitySlotToken(slotIndex);
    }

    public static string GetUnitAbilitySlotToken(int slotIndex)
    {
        switch (slotIndex)
        {
            case 8: return "Q";
            case 9: return "W";
            case 10: return "E";
            case 11: return "R";
            case 4: return "D";
            case 5: return "F";
            case 6: return "C";
            case 7: return "V";
            default: return string.Empty;
        }
    }

    public static bool IsPressed(InputIntent intent, string hotkeyToken)
    {
        switch (NormalizeToken(hotkeyToken))
        {
            case "Q": return intent.CommandQ;
            case "W": return intent.CommandW;
            case "E": return intent.CommandE;
            case "R": return intent.CommandR;
            case "A": return intent.CommandA;
            case "S": return intent.CommandS;
            case "D": return intent.CommandD;
            case "F": return intent.CommandF;
            case "C": return intent.CommandC;
            case "V": return intent.CommandV;
            case "M": return intent.CommandM;
            case "Z": return intent.CommandZ;
            case "X": return intent.CommandX;
            default: return false;
        }
    }
}
