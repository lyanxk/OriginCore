using System.Collections.Generic;
using Unit.Ability;
using Unit.Combat;
using Unit.Command;
using Unit.Movement;
using UnityEngine;

namespace Unit.UI
{
    [DisallowMultipleComponent]
    public class UnitUIDataSource : CommandCardDataSourceBase
    {
        static readonly int[] AbilitySlotOrder = { 8, 9, 10, 11, 4, 5, 6, 7 };

        [Header("Unit Components")]
        [SerializeField] UnitBase motor;
        [SerializeField] CommandExecutor commandExecutor;
        [SerializeField] UnitCombat combat;

        [Header("Base Command UI")]
        [SerializeField] Sprite moveIcon;
        [SerializeField] Sprite attackIcon;
        [SerializeField] Sprite stopIcon;

        [SerializeField] string moveHotkey = "M";
        [SerializeField] string attackHotkey = "A";
        [SerializeField] string stopHotkey = "S";

        [TextArea]
        [SerializeField] string moveTooltip = "Move to target position.";
        [TextArea]
        [SerializeField] string attackTooltip = "Attack-move to target position.";
        [TextArea]
        [SerializeField] string stopTooltip = "Stop current command queue immediately.";

        public override bool CanMove => commandExecutor != null && motor != null;
        public override bool CanAttack => commandExecutor != null && combat != null;
        public override bool CanStop => commandExecutor != null;

        protected override void CacheSpecificComponents()
        {
            if (motor == null)
                motor = GetComponent<UnitBase>();

            if (commandExecutor == null)
                commandExecutor = GetComponent<CommandExecutor>();

            if (combat == null)
                combat = GetComponent<UnitCombat>();
        }

        protected override void AddCommandEntries(List<CommandEntry> entries)
        {
            entries.Add(new CommandEntry
            {
                Id = CommandEntryIds.Move,
                Icon = moveIcon,
                Name = "Move",
                HotkeyText = moveHotkey,
                Enabled = CanMove,
                SlotIndex = 0,
                Cooldown01 = 0f,
                Tooltip = moveTooltip,
                Type = CommandEntryType.Command
            });

            entries.Add(new CommandEntry
            {
                Id = CommandEntryIds.Attack,
                Icon = attackIcon,
                Name = "Attack",
                HotkeyText = attackHotkey,
                Enabled = CanAttack,
                SlotIndex = 1,
                Cooldown01 = 0f,
                Tooltip = attackTooltip,
                Type = CommandEntryType.Command
            });

            entries.Add(new CommandEntry
            {
                Id = CommandEntryIds.Stop,
                Icon = stopIcon,
                Name = "Stop",
                HotkeyText = stopHotkey,
                Enabled = CanStop,
                SlotIndex = 2,
                Cooldown01 = 0f,
                Tooltip = stopTooltip,
                Type = CommandEntryType.Command
            });
        }

        protected override void AddAbilityCommands(List<CommandEntry> entries)
        {
            if (AbilityRouter == null)
                return;

            IReadOnlyList<UnitAbility> abilitySlots = AbilityRouter.RtsAbilitySlots;
            int slotCount = Mathf.Min(abilitySlots.Count, AbilitySlotOrder.Length);
            for (int i = 0; i < slotCount; i++)
            {
                UnitAbility ability = abilitySlots[i];
                if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId))
                    continue;

                if (!ability.ShowInCommandCard || !ability.IsAvailableInCurrentMode)
                    continue;

                entries.Add(CreateAbilityEntry(ability, AbilitySlotOrder[i]));
            }
        }
    }
}
