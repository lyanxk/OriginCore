using UI.HUD;
using Unit.UI;
using UnityEngine;

namespace Input
{
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

            for (int slotIndex = 0; slotIndex < commandCard.SlotCount; slotIndex++)
            {
                if (!commandCard.TryGetEntryAtSlot(slotIndex, out CommandEntry entry))
                    continue;

                if (entry.Type == CommandEntryType.Passive || IsReservedBaseCommand(entry.Id))
                    continue;

                string hotkey = CommandHotkeyUtility.ResolveHotkeyToken(entry, slotIndex);
                if (!intent.GetCommandPressed(hotkey))
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

    }
}
