using UnityEngine;

public class HotkeyDispatcher : MonoBehaviour
{
    [SerializeField] InputIntentSource inputSource;
    [SerializeField] CommandCardController commandCard;

    void Awake()
    {
        if (inputSource == null)
            inputSource = FindObjectOfType<InputIntentSource>();

        if (commandCard == null)
            commandCard = FindObjectOfType<CommandCardController>();
    }

    void Update()
    {
        if (inputSource == null || commandCard == null || !commandCard.isActiveAndEnabled)
            return;

        InputIntent intent = inputSource.Current;
        var entries = commandCard.VisibleEntries;
        int maxSlots = Mathf.Min(commandCard.SlotCount, entries.Count);

        for (int slotIndex = 0; slotIndex < maxSlots; slotIndex++)
        {
            CommandEntry entry = entries[slotIndex];
            if (IsReservedBaseCommand(entry.Id))
                continue;

            string hotkey = ResolveHotkey(entry.HotkeyText, slotIndex);
            if (!IsHotkeyPressed(intent, hotkey))
                continue;

            commandCard.TryExecuteBySlot(slotIndex);
        }
    }

    static bool IsReservedBaseCommand(string commandId)
    {
        return commandId == CommandEntryIds.Move
               || commandId == CommandEntryIds.Attack
               || commandId == CommandEntryIds.Stop;
    }

    static string ResolveHotkey(string hotkeyText, int slotIndex)
    {
        if (!string.IsNullOrWhiteSpace(hotkeyText))
            return hotkeyText.Trim().ToUpperInvariant();

        // Fallback for entries without explicit hotkey text.
        switch (slotIndex)
        {
            case 0: return "Q";
            case 1: return "W";
            case 2: return "E";
            case 3: return "R";
            case 4: return "A";
            case 5: return "S";
            case 6: return "D";
            case 7: return "F";
            case 8: return "Z";
            case 9: return "X";
            case 10: return "C";
            case 11: return "V";
            default: return string.Empty;
        }
    }

    static bool IsHotkeyPressed(InputIntent intent, string hotkey)
    {
        switch (hotkey)
        {
            case "Q": return intent.CommandQ;
            case "W": return intent.CommandW;
            case "E": return intent.CommandE;
            case "R": return intent.CommandR;
            case "A": return intent.CommandA;
            case "S": return intent.CommandS;
            case "D": return intent.CommandD;
            case "F": return intent.CommandF;
            case "Z": return intent.CommandZ;
            case "X": return intent.CommandX;
            case "C": return intent.CommandC;
            case "V": return intent.CommandV;
            case "M": return intent.CommandM;
            default: return false;
        }
    }
}
