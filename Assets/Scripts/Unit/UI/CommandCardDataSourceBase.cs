using System.Collections.Generic;
using Unit.Ability;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class CommandCardDataSourceBase : MonoBehaviour, ICommandCardDataSource
{
    [Header("Display")]
    [SerializeField] string displayName;
    [SerializeField] Sprite portrait;
    [SerializeField] Health health;
    [SerializeField] AbilityInputRouter abilityRouter;

    [Header("Energy (Reserved)")]
    [Min(0f)]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float startEnergy = 100f;

    readonly List<CommandEntry> _entries = new List<CommandEntry>(16);

    float _energy;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public Sprite Portrait => portrait;
    public Health HealthComponent => health;

    public virtual bool CanMove => false;
    public virtual bool CanAttack => false;
    public virtual bool CanStop => false;

    protected AbilityInputRouter AbilityRouter => abilityRouter;

    protected virtual void Awake()
    {
        CacheComponents();
        _energy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
    }

    protected virtual void OnValidate()
    {
        CacheComponents();
        _energy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
    }

    public IReadOnlyList<CommandEntry> GetCommandEntries()
    {
        _entries.Clear();
        AddCommandEntries(_entries);
        AddAbilityCommands(_entries);
        return _entries;
    }

    public bool TryGetHealth(out float current, out float max)
    {
        if (health == null)
        {
            current = 0f;
            max = 0f;
            return false;
        }

        current = health.CurrentHp;
        max = health.MaxHp;
        return max > 0f;
    }

    public bool TryGetEnergy(out float current, out float max)
    {
        current = _energy;
        max = maxEnergy;
        return max > 0f;
    }

    public bool HasAbility(string abilityId)
    {
        return abilityRouter != null && abilityRouter.HasAbility(abilityId);
    }

    public virtual bool TryActivateAbility(string abilityId)
    {
        return abilityRouter != null && abilityRouter.TryActivate(abilityId);
    }

    public virtual bool TryProduce(string productionId)
    {
        return false;
    }

    protected abstract void AddCommandEntries(List<CommandEntry> entries);

    protected virtual void CacheComponents()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (abilityRouter == null)
            abilityRouter = GetComponent<AbilityInputRouter>();

        CacheSpecificComponents();
    }

    protected virtual void CacheSpecificComponents()
    {
    }

    protected virtual void AddAbilityCommands(List<CommandEntry> entries)
    {
        if (abilityRouter == null)
            return;

        IReadOnlyList<UnitAbility> abilities = abilityRouter.RtsAbilitySlots;
        for (int i = 0; i < abilities.Count; i++)
        {
            UnitAbility ability = abilities[i];
            if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId))
                continue;

            if (!ability.ShowInCommandCard || !ability.IsAvailableInCurrentMode)
                continue;

            entries.Add(CreateAbilityEntry(ability, -1));
        }
    }

    protected static CommandEntry CreateAbilityEntry(UnitAbility ability, int slotIndex)
    {
        return new CommandEntry
        {
            Id = ability.AbilityId,
            Icon = ability.Icon,
            Name = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.AbilityId : ability.DisplayName,
            HotkeyText = ability.HotkeyText,
            Enabled = ability.IsEnabled,
            SlotIndex = slotIndex,
            Cooldown01 = ability.Cooldown01,
            Tooltip = BuildAbilityTooltip(ability),
            Type = ability.EntryType
        };
    }

    protected static string BuildAbilityTooltip(UnitAbility ability)
    {
        if (string.IsNullOrWhiteSpace(ability.Tooltip))
            return string.Empty;

        return ability.Tooltip;
    }
}
