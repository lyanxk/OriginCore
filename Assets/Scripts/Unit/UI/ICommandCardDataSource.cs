using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace Unit.UI
{
    public interface ICommandCardDataSource
    {
        string DisplayName { get; }
        Sprite Portrait { get; }
        Health HealthComponent { get; }

        bool CanMove { get; }
        bool CanAttack { get; }
        bool CanStop { get; }

        IReadOnlyList<CommandEntry> GetCommandEntries();
        bool TryGetHealth(out float current, out float max);
        bool TryGetEnergy(out float current, out float max);
        bool HasAbility(string abilityId);
        bool TryActivateAbility(string abilityId, bool append = false);
        bool TryProduce(string productionId);
    }
}
