using System.Collections.Generic;
using Building;
using UnityEngine;

namespace Unit.UI
{
    [DisallowMultipleComponent]
    public class BuildingUIDataSource : CommandCardDataSourceBase
    {
        static readonly string[] ProductionHotkeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };

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

                string resolvedName = string.IsNullOrWhiteSpace(slot.DisplayName)
                    ? slot.UnitPrefab.name
                    : slot.DisplayName;

                entries.Add(new CommandEntry
                {
                    Id = CommandEntryIds.GetProductionId(i),
                    Icon = slot.Icon,
                    Name = resolvedName,
                    HotkeyText = ProductionHotkeys[i],
                    Enabled = true,
                    SlotIndex = i,
                    Cooldown01 = 0f,
                    Tooltip = BuildProductionTooltip(slot, resolvedName),
                    Type = CommandEntryType.Production
                });
            }
        }

        static string BuildProductionTooltip(BuildingProductionSlot slot, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(slot.Tooltip))
                return slot.Tooltip;

            return $"Produce {displayName}.";
        }
    }
}
