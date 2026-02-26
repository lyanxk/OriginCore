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
        if (inputSource == null || commandCard == null)
            return;

        InputIntent intent = inputSource.Current;
        int maxSlots = Mathf.Min(12, commandCard.SlotCount);

        for (int slot = 1; slot <= maxSlots; slot++)
        {
            if (IsReservedBaseCommandSlot(slot))
                continue;

            if (!intent.GetCommandPressed(slot))
                continue;

            commandCard.TryExecuteBySlot(slot - 1);
        }
    }

    static bool IsReservedBaseCommandSlot(int slot)
    {
        // A/S are reserved for RTS attack/stop command flow.
        return slot == 5 || slot == 6;
    }
}
