using System.Collections.Generic;
using Content;
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
                Name = GameText.GetName(CommandEntryIds.Move, CommandEntryIds.Move),
                HotkeyText = GameText.GetHotkey(CommandEntryIds.Move),
                Enabled = CanMove,
                SlotIndex = 0,
                Cooldown01 = 0f,
                Tooltip = GameText.GetTooltip(CommandEntryIds.Move),
                Type = CommandEntryType.Command
            });

            entries.Add(new CommandEntry
            {
                Id = CommandEntryIds.Attack,
                Icon = attackIcon,
                Name = GameText.GetName(CommandEntryIds.Attack, CommandEntryIds.Attack),
                HotkeyText = GameText.GetHotkey(CommandEntryIds.Attack),
                Enabled = CanAttack,
                SlotIndex = 1,
                Cooldown01 = 0f,
                Tooltip = GameText.GetTooltip(CommandEntryIds.Attack),
                Type = CommandEntryType.Command
            });

            entries.Add(new CommandEntry
            {
                Id = CommandEntryIds.Stop,
                Icon = stopIcon,
                Name = GameText.GetName(CommandEntryIds.Stop, CommandEntryIds.Stop),
                HotkeyText = GameText.GetHotkey(CommandEntryIds.Stop),
                Enabled = CanStop,
                SlotIndex = 2,
                Cooldown01 = 0f,
                Tooltip = GameText.GetTooltip(CommandEntryIds.Stop),
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
