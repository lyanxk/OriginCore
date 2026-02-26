using System;
using System.Collections.Generic;
using Unit.Ability;
using UnityEngine;

[DisallowMultipleComponent]
public class UnitUIDataSource : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] string unitName;
    [SerializeField] Sprite portrait;

    [Header("Core Components")]
    [SerializeField] Health health;
    [SerializeField] UnitBase motor;
    [SerializeField] CommandExecutor commandExecutor;
    [SerializeField] UnitCombat combat;
    [SerializeField] AbilityInputRouter abilityRouter;

    [Header("Energy (Reserved)")]
    [Min(0f)]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float startEnergy = 100f;

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

    readonly List<CommandEntry> _entries = new List<CommandEntry>(16);

    float _energy;

    public string DisplayName => string.IsNullOrWhiteSpace(unitName) ? gameObject.name : unitName;
    public Sprite Portrait => portrait;
    public Health HealthComponent => health;

    public bool CanMove => commandExecutor != null && motor != null;
    public bool CanAttack => commandExecutor != null && combat != null;
    public bool CanStop => commandExecutor != null;

    void Awake()
    {
        CacheComponents();
        _energy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
    }

    void OnValidate()
    {
        CacheComponents();
    }

    public IReadOnlyList<CommandEntry> GetCommandEntries()
    {
        _entries.Clear();

        AddBaseCommands();
        AddAbilityCommands();

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
        if (abilityRouter == null) return false;
        return abilityRouter.HasAbility(abilityId);
    }

    public bool TryActivateAbility(string abilityId)
    {
        if (abilityRouter == null) return false;
        return abilityRouter.TryActivate(abilityId);
    }

    void CacheComponents()
    {
        if (health == null) health = GetComponent<Health>();
        if (motor == null) motor = GetComponent<UnitBase>();
        if (commandExecutor == null) commandExecutor = GetComponent<CommandExecutor>();
        if (combat == null) combat = GetComponent<UnitCombat>();
        if (abilityRouter == null) abilityRouter = GetComponent<AbilityInputRouter>();
    }

    void AddBaseCommands()
    {
        _entries.Add(new CommandEntry
        {
            Id = CommandEntryIds.Move,
            Icon = moveIcon,
            Name = "Move",
            HotkeyText = moveHotkey,
            Enabled = CanMove,
            Cooldown01 = 0f,
            Tooltip = moveTooltip,
            Type = CommandEntryType.Command
        });

        _entries.Add(new CommandEntry
        {
            Id = CommandEntryIds.Attack,
            Icon = attackIcon,
            Name = "Attack",
            HotkeyText = attackHotkey,
            Enabled = CanAttack,
            Cooldown01 = 0f,
            Tooltip = attackTooltip,
            Type = CommandEntryType.Command
        });

        _entries.Add(new CommandEntry
        {
            Id = CommandEntryIds.Stop,
            Icon = stopIcon,
            Name = "Stop",
            HotkeyText = stopHotkey,
            Enabled = CanStop,
            Cooldown01 = 0f,
            Tooltip = stopTooltip,
            Type = CommandEntryType.Command
        });
    }

    void AddAbilityCommands()
    {
        if (abilityRouter == null) return;

        IReadOnlyList<IActivatableAbility> abilities = abilityRouter.ActivatableAbilities;
        for (int i = 0; i < abilities.Count; i++)
        {
            IActivatableAbility ability = abilities[i];
            if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId))
                continue;

            _entries.Add(new CommandEntry
            {
                Id = ability.AbilityId,
                Icon = ability.Icon,
                Name = string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.AbilityId : ability.DisplayName,
                HotkeyText = ability.HotkeyText,
                Enabled = ability.IsEnabled,
                Cooldown01 = ability.Cooldown01,
                Tooltip = ability.Tooltip,
                Type = CommandEntryType.Ability
            });
        }
    }
}
