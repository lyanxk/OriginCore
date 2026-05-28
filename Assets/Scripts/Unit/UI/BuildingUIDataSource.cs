using System.Collections.Generic;
using Building;
using Content;
using UnityEngine;
namespace Unit.UI
{
    [DisallowMultipleComponent]
    public class BuildingUIDataSource : CommandCardDataSourceBase
    {
        [Header("Building Components")]
        [SerializeField] BuildingProduction production;

        protected override void CacheSpecificComponents()
        {
            if (production == null)
                production = GetComponent<BuildingProduction>();
        }

        public override bool TryProduce(string productionId)
        {
            return production != null && production.TryProduce(productionId);
        }

        protected override void AddCommandEntries(List<CommandEntry> entries)
        {
            if (production == null)
                return;

            for (int i = 0; i < BuildingProduction.MaxSlotCount; i++)
            {
                if (!production.TryGetSlot(i, out BuildingProductionSlot slot))
                    continue;

                string commandId = CommandEntryIds.GetProductionId(i);
                string fallbackName = slot.UnitPrefab != null ? slot.UnitPrefab.name : commandId;
                string resolvedName = GameText.GetName(commandId, fallbackName);

                entries.Add(new CommandEntry
                {
                    Id = commandId,
                    Icon = slot.Icon,
                    Name = resolvedName,
                    HotkeyText = GameText.GetHotkey(commandId),
                    Enabled = true,
                    SlotIndex = i,
                    Cooldown01 = 0f,
                    Tooltip = BuildProductionTooltip(commandId, resolvedName),
                    Type = CommandEntryType.Production
                });
            }
        }

        static string BuildProductionTooltip(string commandId, string displayName)
        {
            string tooltip = GameText.GetTooltip(commandId);
            if (!string.IsNullOrWhiteSpace(tooltip))
                return tooltip;

            string template = GameText.GetText("command.production.defaultTooltip");
            return string.IsNullOrWhiteSpace(template)
                ? string.Empty
                : string.Format(template, displayName);
        }
    }
}
