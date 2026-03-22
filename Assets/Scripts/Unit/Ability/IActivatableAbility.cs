using UnityEngine;

namespace Unit.Ability
{
    public interface IActivatableAbility
    {
        string AbilityId { get; }
        string DisplayName { get; }
        Sprite Icon { get; }
        string HotkeyText { get; }
        string Tooltip { get; }
        bool IsEnabled { get; }
        float Cooldown01 { get; }

        bool TryActivate();
    }
}
